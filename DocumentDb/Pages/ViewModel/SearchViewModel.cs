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
using DocumentDb.Common.Storage;
using DocumentDb.Pages.Model;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public class SearchViewModel : NotifyPropertyChanged
    {
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

            var fileToOpen = Path.Combine(doc.FullPath, docName);
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

            SynchronizationContext.Post(c => IsBusy = true, null);

            Task.Run(() =>
                     {
                         try
                         {
                             var docs = FetchDocumentsForClause(SearchString);

                             var folders = docs
                                 .GroupBy(c => c.FullPath)
                                 .OrderBy(c => c.Min(x => x.Order))
                                 .Select(c => new Folder() { FullPath = c.Key, Documents = c.OrderBy(x => x.Order).ToArray() });

                             SynchronizationContext.Post(c => Folders.Clear(), null);

                             foreach(var folder in folders)
                             {
                                 SynchronizationContext.Post(c => Folders.Add((Folder)c), folder);
                             }
                         }
                         finally
                         {
                             SynchronizationContext.Post(c => IsBusy = false, null);
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
            long currentIndex = 0;

            // ReSharper disable once PossibleMultipleEnumeration
            foreach(var document in docs)
            {
                var existingDocument = Context.Documents
                    .AsNoTracking()
                    .SingleOrDefault(c => c.Id == document.Id);

                if(existingDocument == null)
                {
                    Logger.Instance.Warn("Документ с кодом {0} все еще присутствует в иднексе, но отсутствует в базе данных. Будет выполнена попытка удаления", document.Id);
                    FtsService.ClearLuceneIndexRecord(document.Id);
                    continue;
                }

                existingDocument.Name = document.Name;
                existingDocument.DocumentContent = document.DocumentContent;
                existingDocument.Order = ++currentIndex;

                if(existingDocument.Type != DocumentType.Undefined || AppConfigurationStorage.Storage.IndexUnsupportedFormats)
                {
                    result.Add(existingDocument);
                }
            }

            return result.ToArray();
        }
    }
}