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
using DataLayer;
using DataLayer.Model;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public class SearchViewModel : NotifyPropertyChanged
    {
        private BaseFolder _baseCatalog;
        private DdbContext _context;
        private IEnumerable<Document> _documents;
        private ObservableCollection<Folder> _folders;
        private bool _isBusy;
        private ICommand _openFileCommand;
        private ICommand _refreshCommand;
        private string _searchString;
        private SynchronizationContext _synchronizationContext;

        public SearchViewModel()
        {
            Context.Configuration.LazyLoadingEnabled = false;
            Context.Configuration.AutoDetectChangesEnabled = false;
            Context.Configuration.ProxyCreationEnabled = false;
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged("isBusy");
            }
        }

        public SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext ?? (_synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext()); }
        }

        public DdbContext Context
        {
            get { return _context ?? (_context = new DdbContext()); }
        }

        public ObservableCollection<Folder> Folders
        {
            get { return _folders ?? (_folders = new ObservableCollection<Folder>()); }
        }

        public IEnumerable<Document> Documents
        {
            get { return _documents; }
            set
            {
                _documents = value;
                OnPropertyChanged("Documents");
            }
        }

        public string SearchString
        {
            get { return _searchString; }
            set
            {
                _searchString = value;
                OnPropertyChanged("SearchString");
            }
        }

        public ICommand RefreshCommand
        {
            get { return _refreshCommand ?? (_refreshCommand = new DelegateCommand(Refresh)); }
        }

        public ICommand OpenFileCommand
        {
            get { return _openFileCommand ?? (_openFileCommand = new DelegateCommand<Document>(OpenFile)); }
        }

        private void OpenFile(Document doc)
        {
            var docName = doc.Name;
            foreach(var highlightTag in FtsService.HighlightTags)
            {
                docName = docName.Replace(highlightTag, "");
            }

            var fileToOpen = Path.Combine(_baseCatalog.FullPath, doc.ParentFolder.FullPath.TrimStart('\\'), docName);
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
            if(IsBusy)
            {
                return;
            }

            SynchronizationContext.Send(c => IsBusy = true, null);


            Task.Run(() =>
                     {
                         try
                         {
                             _baseCatalog = Context.BaseFolders
                                 .SingleOrDefault(c => c.FullPath == AppConfigurationStorage.Storage.CatalogPath);

                             if(_baseCatalog != null)
                             {
                                 var docs = FetchDocumentsForClause(SearchString);

                                 var folders = docs.Select(c => c.ParentFolder)
                                     .GroupBy(c => c.Id)
                                     .Select(c =>
                                             {
                                                 var folder = c.First();
                                                 folder.Documents = null;
                                                 return folder;
                                             })
                                     .Where(c => c.FullPath.StartsWith(_baseCatalog.FullPath))
                                     .ToList();

                                 var resultFolders = new List<Folder>();

                                 foreach(var doc in docs)
                                 {
                                     var folderForDoc = folders.SingleOrDefault(c => c.Id == doc.ParentFolder.Id);
                                     if(folderForDoc == null)
                                     {
                                         continue;
                                     }

                                     if(folderForDoc.Documents == null)
                                     {
                                         folderForDoc.FullPath = folderForDoc.FullPath.Replace(_baseCatalog.FullPath, "");
                                         folderForDoc.Documents = new List<Document>();
                                     }

                                     folderForDoc.Documents.Add(doc);

                                     if(!resultFolders.Contains(folderForDoc))
                                     {
                                         resultFolders.Add(folderForDoc);
                                     }
                                 }

                                 SynchronizationContext.Send(c => Folders.Clear(), null);

                                 foreach(var folder in resultFolders)
                                 {
                                     SynchronizationContext.Post(c => Folders.Add((Folder)c), folder);
                                 }
                             }
                         }
                         finally
                         {
                             SynchronizationContext.Send(c => IsBusy = false, null);
                         }
                     });
        }

        private Document[] FetchDocumentsForClause(string clause)
        {
            if(String.IsNullOrWhiteSpace(clause))
            {
                return new Document[] { };
            }

            var docs = FtsService.Search(clause);

            var result = new List<Document>();

            // ReSharper disable once PossibleMultipleEnumeration
            foreach(var document in docs)
            {
                var existingDocument = Context.Documents
                    .Include("ParentFolder")
                    .AsNoTracking()
                    .Single(c => c.Id == document.Id);

                existingDocument.Name = document.Name;
                existingDocument.FtsCaptures = document.FtsCaptures;

                if(existingDocument.Type != DocumentType.Undefined || AppConfigurationStorage.Storage.IndexUnsupportedFormats)
                {
                    result.Add(existingDocument);
                }
            }

            return result.ToArray();
        }
    }
}