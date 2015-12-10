using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Common.Monad;
using Common.Utils;
using DataLayer;
using DataLayer.Model;
using DocumentDb.Common;
using DocumentDb.Common.Storage;
using DocumentDb.Pages.Model;
using DocumentDb.Pages.ViewModel.Base;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public sealed class NavigationViewModel : NavigationViewModelBase
    {
        private readonly object _syncDummy = new object();
        private ICollectionView _documentsSource;
        private bool _documentsIsLoading;
        private bool _folderTreeIsLoading;
        private Folder _rootFolder;

        public NavigationViewModel()
        {
            ApplicationWorkers.DirectoryMonitor.IndexChanged += DirectoryMonitorOnIndexChanged;
            ApplicationWorkers.DirectoryMonitor.NeedUpdate += ClearData;

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
                if(!string.IsNullOrWhiteSpace(catalogPath))
                {
                    return string.Format("Коллекция \"{0}\"", catalogPath.Split(Path.DirectorySeparatorChar).Last());
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

        private IDictionary<string, Folder> SearchMap { get; set; }

        public ICollectionView DocumentsSource
        {
            get { return _documentsSource; }
            set
            {
                _documentsSource = value;
                OnPropertyChanged();
            }
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
                ModernDialog.ShowMessage(string.Format("Не удалось открыть файл '{0}'. Причина: {1}", fileToOpen, e.Message)
                    , "Ошибка открытия файла", MessageBoxButton.OK, Application.Current.MainWindow);
            }
        }


        private void ClearData(object sender, EventArgs e)
        {
            RootFolder = null;
            SearchMap = null;
            DocumentsSource = null;

            Refresh();
        }

        public void Refresh()
        {
            if(FolderTreeIsLoading)
            {
                return;
            }

            if(RootFolder == null && AppConfigurationStorage.Storage.CatalogPath != null)
            {
                if(SearchMap == null)
                {
                    SearchMap = new Dictionary<string, Folder>();
                }

                RootFolder = new Folder {FullPath = AppConfigurationStorage.Storage.CatalogPath};
                SearchMap.Add(RootFolder.FullPath, RootFolder);
            }

            SynchronizationContext.Post(c => FolderTreeIsLoading = true, null);

            Task.Run(() =>
            {
                lock(_syncDummy)
                {
                    try
                    {
                        var searchMap = new Dictionary<string, Folder>();
                        var folder = BuildFolderTree(searchMap);
                        if(folder != null)
                        {
                            SynchronizationContext.Send(f => RootFolder = folder, folder);
                            SynchronizationContext.Send(s => SearchMap = s, searchMap);
                        }
                    }
                    finally
                    {
                        SynchronizationContext.Post(c => OnPropertyChanged("CollectionName"), null);
                        SynchronizationContext.Post(c => FolderTreeIsLoading = false, null);
                    }
                }
            });
        }

        public Folder BuildFolderTree(IDictionary<string, Folder> searchMap)
        {
            var baseCatalog = AppConfigurationStorage.Storage.CatalogPath;
            if(String.IsNullOrEmpty(baseCatalog))
                return null;

            Document[] docs;
            using(var ctx = new DdbContext())
            {
                docs = ctx.Documents
                    .Where(c => c.FullPath.StartsWith(baseCatalog))
                    .OrderBy(c => c.FullPath).ToArray();
            }

            if(docs.Any())
            {
                foreach(var document in docs)
                {
                    var folder = GetOrCreateFolder(searchMap, baseCatalog, document.FullPath)
                        .Get();
                    folder.Documents.Add(document);
                }
            }

            return searchMap[baseCatalog];
        }

        private void DirectoryMonitorOnIndexChanged(object sender, DocumentChangedEventArgs args)
        {
            if(SearchMap != null && RootFolder != null)
            {
                var movedEventArgs = args as DocumentMovedEventArgs;
                var docToFind = movedEventArgs != null ? movedEventArgs.OldDocument : args.Document;
                switch(args.Kind)
                {
                    case IndexChangeKind.New:
                        GetOrCreateFolder(docToFind.FullPath)
                            .Then(folder => AddDocument(args.Document));
                        break;

                    case IndexChangeKind.Updated:
                        UpdateDocument(args.Document);
                        break;

                    case IndexChangeKind.Removed:
                        FindFolder(docToFind.FullPath)
                            .Then(folder => RemoveDocument(folder, docToFind.Name));
                        break;

                    case IndexChangeKind.Moved:
                        if(docToFind.FullPath != args.Document.FullPath)
                        {
                            FindFolder(docToFind.FullPath)
                                .Then(folder => RemoveDocument(folder, docToFind.Name))
                                .Always(() => AddDocument(args.Document));
                        }
                        else
                        {
                            RenameDocument(docToFind, args.Document);
                        }

                        break;
                }
            }
        }

        private Optional<Folder> FindFolder(string fullPath)
        {
            return SearchMap.TryGet(fullPath);
        }

        private Optional<Folder> GetOrCreateFolder(string fullPath)
        {
            return GetOrCreateFolder(SearchMap, RootFolder.FullPath, fullPath);
        }

        private Optional<Folder> GetOrCreateFolder(IDictionary<string, Folder> searchMap, string basePath, string fullPath)
        {
            return searchMap.TryGet(fullPath)
                .OrElseGet(() =>
                {
                    var parts = fullPath
                        .Replace(basePath, "")
                        .TrimStart(Path.DirectorySeparatorChar)
                        .Split(Path.DirectorySeparatorChar);

                    var lastFolder = searchMap.GetOrCreate(basePath, () => new Folder
                    {
                        FullPath = basePath,
                        Name = Path.GetFileName(basePath)
                    });

                    foreach(var part in parts)
                    {
                        var currentPath = Path.Combine(lastFolder.FullPath, part);

                        var lambdaPart = part;
                        var lambdaLastFolder = lastFolder;

                        searchMap.TryGet(currentPath)
                            .Otherwise(() =>
                            {
                                var newFolder = new Folder {FullPath = currentPath, Name = lambdaPart};
                                searchMap[currentPath] = newFolder;

                                lambdaLastFolder.Folders.Add(newFolder);
                            });

                        lastFolder = searchMap[currentPath];
                    }

                    return lastFolder;
                });
        }

        private void RemoveDocument(Folder folder, string name)
        {
            folder.Documents.Get(c => c.Name == name)
                .ThenGet(doc => folder.Documents.Remove(doc))
                .ThenIf(!folder.HasChildren, () => RemoveFolderTreeIfEmpty(folder));
        }

        private void AddDocument(Document document)
        {
            GetOrCreateFolder(document.FullPath)
                .Then(f => f.Documents.Get(d => d.Name == document.Name)
                    .Otherwise(() => f.Documents.Add(document)));
        }

        private void UpdateDocument(Document document)
        {
            GetOrCreateFolder(document.FullPath)
                .Then(f => f.Documents.Get(d => d.Name == document.Name)
                    .Then(d =>
                    {
                        d.Cached = document.Cached;
                        d.LastEditDateTime = document.LastEditDateTime;
                    })
                    .Otherwise(() => AddDocument(document)));
        }

        private void RenameDocument(Document original, Document changed)
        {
            FindFolder(original.FullPath)
                .Then(folder => folder.Documents.Get(c => c.Name == original.Name)
                    .Then(doc => { doc.Name = changed.Name; }))
                .Otherwise(() => AddDocument(changed));
        }

        private Optional<Folder> GetParentFolder(Folder folder)
        {
            var parentParts = folder.FullPath
                .Split(Path.DirectorySeparatorChar)
                .Reverse()
                .Skip(1)
                .Reverse().ToArray();
            if(parentParts.Any())
            {
                var parentPath = string.Join(Path.DirectorySeparatorChar.ToString(), parentParts);
                return SearchMap.TryGet(parentPath);
            }

            return Optional<Folder>.Empty();
        }

        private void RemoveFolderTreeIfEmpty(Folder folder)
        {
            if(!folder.HasChildren)
            {
                SearchMap.TryGet(folder.FullPath)
                    .Then(() => SearchMap.Remove(folder.FullPath));

                GetParentFolder(folder)
                    .Then(parent => parent.Folders.Remove(folder))
                    .ThenIf(parent => !parent.HasChildren, RemoveFolderTreeIfEmpty);
            }
        }
    }
}