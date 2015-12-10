using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Common;
using Common.Utils;
using DataLayer;
using DataLayer.Model;
using DocumentDb.Common.Storage;
using DocumentDb.Pages.Model;
using DocumentDb.Pages.ViewModel.Base;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public sealed class SearchViewModel : NavigationViewModelBase
    {
        private DdbContext _context;
        private IEnumerable<Document> _documents;
        private ObservableCollection<Folder> _folders;
        private bool _isBusy;
        private ICommand _refreshCommand;
        private string _searchString;


        public SearchViewModel()
        {
            Context.Configuration.LazyLoadingEnabled = false;
            Context.Configuration.AutoDetectChangesEnabled = false;
            Context.Configuration.ProxyCreationEnabled = false;
        }


        public DdbContext Context
        {
            get { return _context ?? (_context = new DdbContext()); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
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
                OnPropertyChanged();
            }
        }

        public string SearchString
        {
            get { return _searchString; }
            set
            {
                _searchString = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshCommand
        {
            get { return _refreshCommand ?? (_refreshCommand = new DelegateCommand(Refresh)); }
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
                        .Select(c =>
                        {
                            var folder = new Folder {FullPath = c.Key};
                            foreach(var doc in c.OrderBy(x => x.Order))
                            {
                                folder.Documents.Add(doc);
                            }

                            return folder;
                        });

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

        protected override void OpenFile(Document doc)
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
                ModernDialog.ShowMessage(
                    string.Format("Не удалось открыть файл '{0}'. Причина: {1}", fileToOpen, e.Message)
                    , "Ошибка открытия файла", MessageBoxButton.OK, Application.Current.MainWindow);
            }
        }

        private Document[] FetchDocumentsForClause(string clause)
        {
            if(string.IsNullOrWhiteSpace(clause))
            {
                return new Document[] {};
            }

            var docs = FtsService.Search(clause);

            var result = new List<Document>();
            long currentIndex = 0;

            foreach(var document in docs)
            {
                var existingDocument = Context.Documents
                    .AsNoTracking()
                    .SingleOrDefault(c => c.Id == document.Id);

                if(existingDocument == null)
                {
                    Logger.Instance.Warn(
                        "Документ с кодом {0} все еще присутствует в иднексе, но отсутствует в базе данных. Будет выполнена попытка удаления",
                        document.Id);
                    FtsService.ClearLuceneIndexRecord(document.Id);
                    continue;
                }

                existingDocument.Name = document.Name;
                existingDocument.DocumentContent = document.DocumentContent;
                existingDocument.Order = ++currentIndex;

                if(existingDocument.Type != DocumentType.Undefined ||
                   AppConfigurationStorage.Storage.IndexUnsupportedFormats)
                {
                    result.Add(existingDocument);
                }
            }

            return result.ToArray();
        }
    }
}