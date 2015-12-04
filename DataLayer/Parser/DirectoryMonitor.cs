using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Utils;
using DataLayer.Model;
using ExtensionAttribute = DataLayer.Attributes.ExtensionAttribute;

namespace DataLayer.Parser
{
    public enum DocumentMonitorState
    {
        [Description("Ожидание")] Idle,
        [Description("Индексирование")] Running,
        [Description("Очистка")] Deleting
    }

    public class DirectoryMonitor : INotifyPropertyChanged
    {
        private static readonly dynamic[] Types;
        private string _basePath;
        private DocumentMonitorState _state;

        public DirectoryMonitor(string path)
        {
            if(!String.IsNullOrWhiteSpace(path))
            {
                BasePath = path;
            }
            SynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        private FileSystemWatcher Watcher { get; set; }

        public DocumentMonitorState State
        {
            get { return _state; }
            set
            {
                _state = value;
                OnPropertyChanged();
            }
        }

        private SynchronizationContext SynchronizationContext { get; set; }

        static DirectoryMonitor()
        {
            // ReSharper disable once CoVariantArrayConversion
            Types = ((DocumentType[])Enum.GetValues(typeof(DocumentType)))
                .Select(c => new {Attribute = c.GetAttributeOfType<ExtensionAttribute>(), Value = c})
                .Where(c => c.Attribute != null)
                .Select(c => new {c.Attribute.Extensions, c.Value})
                .ToArray();
        }

        public string BasePath
        {
            get { return _basePath; }
            set
            {
                if(!String.IsNullOrWhiteSpace(value))
                {
                    _basePath = Path.GetFullPath(value);
                    InitMonitoring(_basePath);
                }
            }
        }

        private void InitMonitoring(string path)
        {
            if(Watcher != null)
            {
                Watcher.Dispose();
                Watcher = null;
            }

            Watcher = new FileSystemWatcher(path)
                      {
                          IncludeSubdirectories = true,
                          NotifyFilter = NotifyFilters.LastWrite
                                         | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size
                      };

            Watcher.Renamed += (sender, args) => { Update(); };
            Watcher.Deleted += (sender, args) => { Update(); };
            Watcher.Created += (sender, args) => { Update(); };

            Watcher.EnableRaisingEvents = true;
        }

        private readonly object _syncDummy = new object();
        private int _waitingThreads;

        public void Update()
        {
            if(_waitingThreads > 1 || String.IsNullOrWhiteSpace(BasePath))
            {
                return;
            }

            Task.Run(() =>
                     {
                         ++_waitingThreads;
                         lock(_syncDummy)
                         {
                             SynchronizationContext.Post(c => State = DocumentMonitorState.Running, null);
                             try
                             {
                                 var directories = Directory.GetDirectories(BasePath);

                                 //Parallel.ForEach(directories, s => { ProcessDirectory(Path.GetFullPath(s)); });
                                 foreach(var s in directories)
                                 {
                                     ProcessDirectory(Path.GetFullPath(s));
                                 }

                                 using(var ctx = new DdbContext())
                                 {
                                     SynchronizationContext.Post(c => State = DocumentMonitorState.Deleting, null);

                                     foreach(var document in ctx.Documents)
                                     {
                                         var path = Path.Combine(document.FullPath, document.Name);
                                         if(!File.Exists(path))
                                         {
                                             ctx.Documents.Remove(document);
                                             StatisticsModel.Instance.ParsedDocumentsCount -= 1;
                                             if(document.Cached)
                                             {
                                                 FtsService.ClearLuceneIndexRecord(document.Id);
                                                 StatisticsModel.Instance.DocumentsInCacheCount -= 1;
                                             }
                                         }
                                     }

                                     ctx.SaveChanges();

                                     StatisticsModel.Instance.Refresh(StatisticsModelRefreshMethod.UpdateForDocumentMonitor);
                                 }
                             }
                             catch(Exception e)
                             {
                                 Logger.Instance.ErrorException("Unable to update directory cache: {0}", e);
                             }
                             finally
                             {
                                 SynchronizationContext.Post(c => State = DocumentMonitorState.Idle, null);
                                 --_waitingThreads;
                             }
                         }
                     });
        }

        private void ProcessDirectory(string path)
        {
            if(!Directory.Exists(path))
            {
                return;
            }

            try
            {
                using(var ctx = new DdbContext())
                {
                    ctx.Configuration.AutoDetectChangesEnabled = false;
                    ctx.Configuration.ValidateOnSaveEnabled = false;

                    ProcessFilesInFolder(ctx, path);
                    ctx.SaveChanges();
                }
            }
            catch(Exception e)
            {
                Logger.Instance.Error("Не удалось сорханить документы для папки '{0}': {1}", path, (object)e);
            }

            string[] directories = {};

            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить субдиректории каталога '{0}': {1}", path, e);
            }

            //Parallel.ForEach(directories, s => { ProcessDirectory(Path.GetFullPath(s)); });
            foreach(var s in directories)
            {
                ProcessDirectory(Path.GetFullPath(s));
            }
        }

        private void ProcessFilesInFolder(DdbContext ctx, string path)
        {
            string[] filesInDirectory = {};
            try
            {
                filesInDirectory = Directory.GetFiles(path);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить файлы каталога '{0}': {1}", path, e);
            }

            var docsInDirectory = new List<Document>();
            foreach(var file in filesInDirectory)
            {
                var docs = ProcessFileInternal(ctx, file);
                docsInDirectory.AddRange(docs);
            }

            if(docsInDirectory.Any())
            {
                ctx.Documents.AddRange(docsInDirectory);
            }
        }

        private IEnumerable<Document> ProcessFileInternal(DdbContext ctx, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileType = GetTypeForFileName(fileName);
            var fullPath = Path.GetDirectoryName(filePath);

            var document = ctx.Documents.SingleOrDefault(c => c.FullPath == fullPath && c.Name == fileName);

            var result = new List<Document>();

            var lastEditTime = File.GetLastWriteTime(filePath);
            if(document == null)
            {
                document = new Document
                           {
                               Name = fileName,
                               Type = fileType,
                               FullPath = fullPath,
                               Cached = false,
                               LastEditDateTime = lastEditTime
                           };

                StatisticsModel.Instance.ParsedDocumentsCount += 1;

                result.Add(document);
            }
            else if((lastEditTime - document.LastEditDateTime).TotalSeconds > 1) //SQL Server compact lose precision
            {
                document.LastEditDateTime = lastEditTime;
                document.Cached = false;

                ctx.Entry(document).State = EntityState.Modified;
            }

            return result;
        }

        private DocumentType GetTypeForFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName.ToLower());

            if(extension.Length > 0)
            {
                extension = extension.Substring(1);
                var value = Types.SingleOrDefault(c => Enumerable.Contains(c.Extensions, extension));
                if(value != null)
                {
                    return value.Value;
                }
            }

            return DocumentType.Undefined;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if(handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}