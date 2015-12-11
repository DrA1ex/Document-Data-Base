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
        [Description("Обработка изменений")] ProcessChanges,
        [Description("Очистка")] Deleting
    }

    public class DirectoryMonitor : INotifyPropertyChanged
    {
        private static readonly dynamic[] Types;

        private readonly ReaderWriterLockSlim _processingReadWriteLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly object _syncDummy = new object();
        private string _basePath;

        private CancellationTokenSource _cts;
        private long _processChangesThreads;
        private DocumentMonitorState _state;

        private long _updateIndexThreads;

        static DirectoryMonitor()
        {
            // ReSharper disable once CoVariantArrayConversion
            Types = ((DocumentType[])Enum.GetValues(typeof(DocumentType)))
                .Select(c => new {Attribute = c.GetAttributeOfType<ExtensionAttribute>(), Value = c})
                .Where(c => c.Attribute != null)
                .Select(c => new {c.Attribute.Extensions, c.Value})
                .ToArray();
        }

        public DirectoryMonitor(string path)
        {
            if(!string.IsNullOrWhiteSpace(path))
            {
                BasePath = path;
            }
            SynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
            PendingEvents = new List<WatcherChangedEventArgs>();
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

        public string BasePath
        {
            get { return _basePath; }
            set
            {
                if(!string.IsNullOrWhiteSpace(value))
                {
                    _basePath = Path.GetFullPath(value);
                    InitMonitoring(_basePath);
                }
            }
        }

        private List<WatcherChangedEventArgs> PendingEvents { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void InitMonitoring(string path)
        {
            if(Watcher != null)
            {
                Watcher.Dispose();
                Watcher = null;
                PendingEvents.Clear();
            }

            Watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            Watcher.Renamed += WatcherOnNewEvent;
            Watcher.Deleted += WatcherOnNewEvent;
            Watcher.Created += WatcherOnNewEvent;
            Watcher.Changed += WatcherOnNewEvent;

            Watcher.EnableRaisingEvents = true;
        }

        private void WatcherOnNewEvent(object sender, FileSystemEventArgs args)
        {
            //TODO: find way to determine is it directory or file
            if(!string.IsNullOrEmpty(Path.GetExtension(args.FullPath)))
            {
                ProcessChangeEvents(args);
            }
            else
            {
                //TODO: implement directory changes handling

                // This is temporal solution for handling directory changes
                // e.g. if directory with files was moved into/from BasePath (or subfolders),
                // only one event will be raised.
                Update();
            }
        }

        public void Update(bool raiseUpdateEvent = false)
        {
            if(Interlocked.Read(ref _updateIndexThreads) > 1 || string.IsNullOrWhiteSpace(BasePath))
            {
                return;
            }

            if(_cts == null)
            {
                _cts = new CancellationTokenSource();
            }

            var token = _cts.Token;

            Task.Run(() =>
            {
                Interlocked.Increment(ref _updateIndexThreads);

                lock(_syncDummy)
                {
                    SynchronizationContext.Post(c => State = DocumentMonitorState.Running, null);
                    try
                    {
                        ProcessDirectory(BasePath, token);

                        using(var ctx = new DdbContext())
                        {
                            ctx.Configuration.AutoDetectChangesEnabled = false;
                            ctx.Configuration.ValidateOnSaveEnabled = false;

                            SynchronizationContext.Post(c => State = DocumentMonitorState.Deleting, null);

                            var documentsToDelete = new List<Document>();

                            foreach(var document in ctx.Documents)
                            {
                                token.ThrowIfCancellationRequested();

                                var path = Path.Combine(document.FullPath, document.Name);
                                if(!File.Exists(path))
                                {
                                    documentsToDelete.Add(document);
                                    OnIndexChanged(new DocumentChangedEventArgs {Kind = IndexChangeKind.Removed, Document = document});
                                    StatisticsModel.Instance.ParsedDocumentsCount -= 1;
                                    if(document.Cached)
                                    {
                                        FtsService.ClearLuceneIndexRecord(document.Id);
                                        StatisticsModel.Instance.DocumentsInCacheCount -= 1;
                                    }
                                }
                            }

                            token.ThrowIfCancellationRequested();
                            ctx.Documents.RemoveRange(documentsToDelete);
                            ctx.SaveChanges();

                            StatisticsModel.Instance.Refresh(StatisticsModelRefreshMethod.UpdateForDocumentMonitor);
                        }
                    }
                    catch(OperationCanceledException)
                    {
/* ignore */
                    }
                    catch(Exception e)
                    {
                        Logger.Instance.ErrorException("Unable to update directory cache: {0}", e);
                    }
                    finally
                    {
                        SynchronizationContext.Post(c => State = DocumentMonitorState.Idle, null);
                        Interlocked.Decrement(ref _updateIndexThreads);
                        if(raiseUpdateEvent)
                        {
                            SynchronizationContext.Post(OnNeedUpdate);
                        }
                    }
                }
            }, token);
        }

        private void ProcessChangeEvents(FileSystemEventArgs arg)
        {
            //TODO: maybe use smth more simple instead of RW locks?
            _processingReadWriteLock.EnterReadLock();

            //TODO: implement directory changes handling
            switch(arg.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:
                    PendingEvents.Add(new WatcherChangedEventArgs {Kind = arg.ChangeType.AsIndexChangeKind(), FullPath = arg.FullPath});
                    break;
                case WatcherChangeTypes.Renamed:
                    if(arg is RenamedEventArgs)
                    {
                        PendingEvents.Add(new WatcherRenameEventArgs {FullPath = arg.FullPath, OldFullPath = ((RenamedEventArgs)arg).OldFullPath});
                    }
                    break;
            }

            _processingReadWriteLock.ExitReadLock();

            if(Interlocked.Read(ref _processChangesThreads) < 2)
            {
                Task.Run(() =>
                {
                    lock(_syncDummy)
                    {
                        _processingReadWriteLock.EnterWriteLock();
                        var changes = PendingEvents.ToArray();
                        PendingEvents.Clear();
                        _processingReadWriteLock.ExitWriteLock();

                        ProcessChangeEventsInternal(changes);
                    }
                });
            }
        }

        private void ProcessChangeEventsInternal(WatcherChangedEventArgs[] changes)
        {
            try
            {
                Interlocked.Increment(ref _processChangesThreads);
                SynchronizationContext.Post(c => State = DocumentMonitorState.ProcessChanges, null);

                //TODO: Refactor
                using(var ctx = new DdbContext())
                {
                    foreach(var change in changes)
                    {
                        var filePath = change.FullPath;
                        var fileName = Path.GetFileName(filePath);
                        var fileType = GetTypeForFileName(fileName);
                        var fullPath = Path.GetDirectoryName(filePath);

                        //TODO: Handle case when there are several events for single file
                        Document document = null;
                        Document oldDocument = null;
                        switch(change.Kind)
                        {
                            case IndexChangeKind.New:
                                if(!ctx.Documents.HasFile(fullPath, fileName))
                                {
                                    document = new Document
                                    {
                                        Name = fileName,
                                        FullPath = fullPath,
                                        Type = fileType,
                                        LastEditDateTime = File.GetLastWriteTime(filePath)
                                    };

                                    ctx.Documents.Add(document);
                                    StatisticsModel.Instance.ParsedDocumentsCount += 1;
                                }
                                break;

                            case IndexChangeKind.Updated:
                                document = ctx.Documents.FindFile(fullPath, fileName);
                                if(document != null)
                                {
                                    var lastEdit = File.GetLastWriteTime(filePath);
                                    if((lastEdit - document.LastEditDateTime).TotalSeconds > 1)
                                    {
                                        document.LastEditDateTime = lastEdit;
                                        StatisticsModel.Instance.DocumentsInCacheCount -= 1;
                                    }
                                }
                                break;

                            case IndexChangeKind.Moved:
                                var oldFilePath = ((WatcherRenameEventArgs)change).OldFullPath;
                                var oldFullPath = Path.GetDirectoryName(oldFilePath);
                                var oldFileName = Path.GetFileName(oldFilePath);

                                document = ctx.Documents.FindFile(oldFullPath, oldFileName);
                                if(document != null)
                                {
                                    oldDocument = (Document)document.Clone();
                                    document.FullPath = fullPath;
                                    document.Name = fileName;
                                    document.Cached = false;
                                    document.DocumentContent = null;
                                }
                                break;

                            case IndexChangeKind.Removed:
                                document = ctx.Documents.FindFile(fullPath, fileName);
                                if(document != null)
                                {
                                    ctx.Documents.Remove(document);
                                    StatisticsModel.Instance.ParsedDocumentsCount -= 1;
                                }
                                break;
                        }

                        if(document != null)
                        {
                            if(change.Kind != IndexChangeKind.Moved)
                            {
                                OnIndexChanged(new DocumentChangedEventArgs {Kind = change.Kind, Document = document});
                            }
                            else
                            {
                                OnIndexChanged(new DocumentMovedEventArgs {Kind = change.Kind, Document = document, OldDocument = oldDocument});
                            }
                        }
                    }

                    ctx.SaveChanges();
                }
            }
            catch(Exception e)
            {
                Logger.Instance.Error("Не удалось обработать изменения в отслеживаемой директории: {0}", (object)e);
            }
            finally
            {
                SynchronizationContext.Post(c => State = DocumentMonitorState.Idle, null);
                Interlocked.Decrement(ref _processChangesThreads);
            }
        }

        private void ProcessDirectory(string path, CancellationToken token)
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

                    ProcessFilesInFolder(ctx, path, token);

                    token.ThrowIfCancellationRequested();
                    ctx.SaveChanges();
                }
            }
            catch(OperationCanceledException)
            {
                throw;
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
                token.ThrowIfCancellationRequested();
                ProcessDirectory(Path.GetFullPath(s), token);
            }
        }

        private void ProcessFilesInFolder(DdbContext ctx, string path, CancellationToken token)
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
                token.ThrowIfCancellationRequested();

                var docs = ProcessFileInternal(ctx, file);
                docsInDirectory.AddRange(docs);
            }

            if(docsInDirectory.Any())
            {
                token.ThrowIfCancellationRequested();

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
                OnIndexChanged(new DocumentChangedEventArgs {Kind = IndexChangeKind.New, Document = document});
            }
            else if((lastEditTime - document.LastEditDateTime).TotalSeconds > 1) //SQL Server compact lose precision
            {
                document.LastEditDateTime = lastEditTime;
                document.Cached = false;
                document.DocumentContent = null;

                OnIndexChanged(new DocumentChangedEventArgs {Kind = IndexChangeKind.Updated, Document = document});

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

        public event EventHandler<DocumentChangedEventArgs> IndexChanged;
        public event EventHandler NeedUpdate;

        protected virtual void OnIndexChanged(DocumentChangedEventArgs e)
        {
            SynchronizationContext.Post(() =>
            {
                var handler = IndexChanged;
                if(handler != null) handler(this, e);
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if(handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public virtual void OnNeedUpdate()
        {
            if(NeedUpdate != null) NeedUpdate.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if(_cts != null)
            {
                _cts.Cancel(true);
                _cts.Dispose();
                _cts = null;
            }
        }
    }
}