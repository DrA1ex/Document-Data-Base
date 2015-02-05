using System;
using System.ComponentModel;
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
        [Description("Ожидание")]
        Idle,
        [Description("Выполняется")]
        Running
    }

    public class DirectoryMonitor : INotifyPropertyChanged
    {
        private const long DateTimeTicksRound = 10000000;
        private static readonly dynamic[] Types;
        private string _basePath;
        private DocumentMonitorState _state;

        public DirectoryMonitor(string path)
        {
            if(!String.IsNullOrWhiteSpace(path))
                BasePath = path;
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
                .Select(c => new { Attribute = c.GetAttributeOfType<ExtensionAttribute>(), Value = c })
                .Where(c => c.Attribute != null)
                .Select(c => new { c.Attribute.Extension, c.Value })
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

            SynchronizationContext.Send(c => State = DocumentMonitorState.Running, null);
            Task.Run(() =>
                     {
                         ++_waitingThreads;
                         lock(_syncDummy)
                         {
                             try
                             {
                                 using(var ctx = new DdbContext())
                                 {
                                     var baseDir = ctx.BaseFolders.SingleOrDefault(c => c.FullPath == BasePath);
                                     if(baseDir == null)
                                     {
                                         baseDir = new BaseFolder { FullPath = BasePath };
                                         ctx.BaseFolders.Add(baseDir);
                                         ctx.SaveChanges();
                                     }

                                     var directories = Directory.GetDirectories(BasePath);
                                     foreach(var directory in directories)
                                     {
                                         ProcessDirectory(ctx, baseDir, Path.GetFullPath(directory));
                                     }

                                     ctx.SaveChanges();

                                     foreach(var document in ctx.Documents.Include("ParentFolder"))
                                     {
                                         var path = Path.Combine(document.ParentFolder.FullPath, document.Name);
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

                                     foreach(var folder in ctx.Folders)
                                     {
                                         if(!Directory.Exists(folder.FullPath))
                                         {
                                             ctx.Folders.Remove(folder);
                                             StatisticsModel.Instance.ParsedFoldersCount -= 1;
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
                                 SynchronizationContext.Send(c => State = DocumentMonitorState.Idle, null);
                                 --_waitingThreads;
                             }
                         }
                     });
        }

        private void ProcessDirectory(DdbContext ctx, BaseFolder baseFolder, string path, Folder parentFolder = null)
        {
            if(!Directory.Exists(path))
            {
                return;
            }

            Folder folder;
            if(parentFolder != null && parentFolder.Folders != null && parentFolder.Folders.Any())
            {
                folder = parentFolder.Folders.SingleOrDefault(c => c.FullPath == path);
            }
            else if(baseFolder.Folders != null && baseFolder.Folders.Any())
            {
                folder = baseFolder.Folders.SingleOrDefault(c => c.FullPath == path);
            }
            else
            {
                folder = ctx.Folders.SingleOrDefault(c => c.FullPath == path);
            }

            if(folder == null)
            {
                folder = new Folder
                         {
                             FullPath = path,
                             Label = Path.GetFileName(path)
                         };

                StatisticsModel.Instance.ParsedFoldersCount += 1;

                if(parentFolder == null)
                {
                    folder.BaseFolder = baseFolder;
                    ctx.Folders.Add(folder);
                }
                else
                {
                    folder.Parent = parentFolder;
                    ctx.Folders.Add(folder);
                }
            }


            ProcessFilesInFolder(ctx, folder);

            string[] directories = { };

            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить субдиректории каталога '{0}': {1}", path, e);
            }

            foreach(var directory in directories)
            {
                ProcessDirectory(ctx, baseFolder, Path.GetFullPath(directory), folder);
            }
        }

        private void ProcessFilesInFolder(DdbContext ctx, Folder folder)
        {
            string[] filesInDirectory = { };
            try
            {
                filesInDirectory = Directory.GetFiles(folder.FullPath);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить файлы каталога '{0}': {1}", folder.FullPath, e);
            }

            foreach(var file in filesInDirectory)
            {
                ProcessFileInternal(ctx, folder, file);
            }

            ctx.SaveChanges();
        }

        private void ProcessFileInternal(DdbContext ctx, Folder folder, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileType = GetTypeForFileName(fileName);

            Document document = null;
            if(folder.Folders != null)
            {
                document = folder.Documents.SingleOrDefault(c => c.Name == Path.GetFileName(fileName));
            }

            var lastEditTime = File.GetLastWriteTime(filePath);
            if(document == null)
            {
                document = new Document
                           {
                               Name = fileName,
                               Type = fileType,
                               ParentFolder = folder,
                               Cached = false,
                               LastEditDateTime = lastEditTime
                           };

                StatisticsModel.Instance.ParsedDocumentsCount += 1;

                ctx.Documents.Add(document);
            }
            else if(lastEditTime.Ticks / DateTimeTicksRound != document.LastEditDateTime.Ticks / DateTimeTicksRound) //SQL Server compact lose precision
            {
                document.LastEditDateTime = lastEditTime;
                document.Cached = false;
            }
        }

        private DocumentType GetTypeForFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName.ToLower());

            if(extension.Length > 0)
            {
                extension = extension.Substring(1);

                var value = Types.SingleOrDefault(c => c.Extension == extension);
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