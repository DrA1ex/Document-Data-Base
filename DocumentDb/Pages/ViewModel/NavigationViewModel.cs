using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Common;
using Common.Utils;
using DataLayer;
using DataLayer.Model;
using DataLayer.Parser;
using DocumentDb.Common;
using DocumentDb.Common.Storage;
using DocumentDb.Pages.Model;
using DocumentDb.Pages.ViewModel.Base;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public sealed class NavigationViewModel : NavigationViewModelBase
    {
        private CancellationTokenSource _cts;
        private ObservableCollection<Document> _documents;
        private bool _documentsIsLoading;
        private Folder _rootFolder;
        private bool _folderTreeIsLoading;
        private readonly object _syncDummy = new object();

        public NavigationViewModel()
        {
            ApplicationWorkers.DirectoryMonitor.PropertyChanged += (sender, args) =>
                                                                   {
                                                                       if(args.PropertyName == "State")
                                                                       {
                                                                           if(ApplicationWorkers.DirectoryMonitor.State == DocumentMonitorState.Idle)
                                                                           {
                                                                               Refresh();
                                                                           }
                                                                       }
                                                                   };

            Refresh();
        }

        public bool FolderTreeIsLoading
        {
            get { return _folderTreeIsLoading; }
            set
            {
                _folderTreeIsLoading = value;
                OnPropertyChanged();
            }
        }

        public bool DocumentsIsLoading
        {
            get { return _documentsIsLoading; }
            set
            {
                _documentsIsLoading = value;
                OnPropertyChanged();
            }
        }

        public string CollectionName
        {
            get
            {
                var catalogPath = AppConfigurationStorage.Storage.CatalogPath;
                if(!String.IsNullOrWhiteSpace(catalogPath))
                {
                    return String.Format("Коллекция \"{0}\"", catalogPath.Split(Path.DirectorySeparatorChar).Last());
                }

                return "Каталог не выбран";
            }
        }

        public Folder RootFolder
        {
            get { return _rootFolder; }
            set
            {
                _rootFolder = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Document> Documents
        {
            get { return _documents ?? (_documents = new ObservableCollection<Document>()); }
        }

        public void SetDocuments(long[] documentsId)
        {
            if(_cts != null)
            {
                _cts.Cancel();
            }

            SynchronizationContext.Post(c => DocumentsIsLoading = true, null);

            lock (_syncDummy)
            {
                if(_cts != null)
                {
                    _cts.Dispose();
                }
                _cts = new CancellationTokenSource();
            }

            Task.Run(() =>
                     {
                         lock (_syncDummy)
                         {
                             SynchronizationContext.Post(c => _documents.Clear(), null);
                             var token = _cts.Token;

                             try
                             {
                                 using(var ctx = new DdbContext())
                                 {
                                     var documents = ctx.Documents.Where(c => documentsId.Contains(c.Id));
                                     var docsEnumerated = AppConfigurationStorage.Storage.IndexUnsupportedFormats
                                         ? documents
                                         : documents.Where(c => c.Type != DocumentType.Undefined);


                                     foreach(var document in docsEnumerated)
                                     {
                                         if(!token.IsCancellationRequested)
                                         {
                                             SynchronizationContext.Post(c => _documents.Add((Document)c), document);
                                         }
                                         else
                                         {
                                             break;
                                         }
                                     }
                                 }
                             }
                             finally
                             {
                                 if(!token.IsCancellationRequested)
                                 {
                                     SynchronizationContext.Post(c => OnPropertyChanged("Documents"), null);
                                     SynchronizationContext.Post(c => OnPropertyChanged("CollectionName"), null);
                                     SynchronizationContext.Post(c => DocumentsIsLoading = false, null);
                                 }
                             }
                         }
                     }, _cts.Token);
        }

        protected override void OpenFile(Document doc)
        {
            var fileToOpen = Path.Combine(doc.FullPath, doc.Name);

            try
            {
                Process.Start(fileToOpen);
            }
            catch(Exception e)
            {
                ModernDialog.ShowMessage(String.Format("Не удалось открыть файл '{0}'. Причина: {1}", fileToOpen, e.Message)
                    , "Ошибка открытия файла", MessageBoxButton.OK, Application.Current.MainWindow);
            }
        }

        public void Refresh()
        {
            if(FolderTreeIsLoading)
            {
                return;
            }

            SynchronizationContext.Post(c => FolderTreeIsLoading = true, null);
            
            Task.Run(() =>
                     {
                         try
                         {
                             SynchronizationContext.Post(folder => RootFolder = folder, BuildFolderTree());
                         }
                         finally
                         {
                             SynchronizationContext.Post(c => FolderTreeIsLoading = false, null);
                         }
                     });
        }

        public Folder BuildFolderTree()
        {
            var baseCatalog = AppConfigurationStorage.Storage.CatalogPath;

            Document[] docs;
            using(var ctx = new DdbContext())
            {
                docs = ctx.Documents
                    .Where(c => c.FullPath.StartsWith(baseCatalog))
                    .OrderBy(c => c.FullPath).ToArray();
            }

            var folders = new List<Folder>();

            if(docs.Any())
            {
                var searchMap = new Dictionary<string, Folder>();
                foreach(var document in docs)
                {
                    var folder = GetFolder(folders, searchMap, baseCatalog, document.FullPath);
                    folder.Documents.Add(document);
                }
            }

            return folders.SingleOrDefault();
        }

        private Folder GetFolder(List<Folder> folders, IDictionary<string, Folder> searchMap, string basePath, string path)
        {
            if(searchMap.ContainsKey(path))
            {
                var existingFolder = searchMap[path];
                return existingFolder;
            }

            var parts = path
                .Replace(basePath, "")
                .TrimStart(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar);

            Folder lastFolder;
            searchMap.TryGetValue(basePath, out lastFolder);
            foreach(var part in parts)
            {
                var fullPath = Path.Combine(lastFolder != null ? lastFolder.FullPath : basePath, part);

                Folder folder;
                if(searchMap.ContainsKey(fullPath))
                {
                    folder = searchMap[fullPath];
                }
                else if(lastFolder != null)
                {
                    folder = lastFolder.Folders.SingleOrDefault(c => c.Name == part);
                }
                else
                {
                    folder = folders.SingleOrDefault(c => c.Name == part);
                }


                if(folder == null)
                {
                    folder = new Folder
                    {
                        Name = part,
                        FullPath = fullPath,
                        Folders = new List<Folder>(),
                        Documents = new List<Document>()
                    };

                    searchMap.Add(folder.FullPath, folder);

                    if(lastFolder != null)
                    {
                        lastFolder.Folders.Add(folder);
                    }
                    else
                    {
                        folders.Add(folder);
                    }
                }

                lastFolder = folder;
            }

            return lastFolder;
        }
    }
}