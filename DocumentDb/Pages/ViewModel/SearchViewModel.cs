using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Common;
using DataLayer;
using DataLayer.Model;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.ViewModel
{
    public class SearchViewModel : NotifyPropertyChanged
    {
        private DdbContext _context;
        private IEnumerable<Document> _documents;
        private ObservableCollection<Folder> _folders;
        private ICommand _openFileCommand;
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
            Process.Start(Path.Combine(doc.ParentFolder.FullPath, doc.Name));
        }

        public void Refresh()
        {
            var baseCatalog = Context.BaseFolders
                .SingleOrDefault(c => c.FullPath == AppConfigurationStorage.Storage.CatalogPath);

            if(baseCatalog != null)
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
                    .Where(c => c.FullPath.StartsWith(baseCatalog.FullPath))
                    .OrderBy(c => c.FullPath)
                    .ToList();

                foreach(var doc in docs)
                {
                    var folderForDoc = folders.SingleOrDefault(c => c.Id == doc.ParentFolder.Id);
                    if(folderForDoc == null)
                        continue;

                    if(folderForDoc.Documents == null)
                    {
                        folderForDoc.FullPath = folderForDoc.FullPath.Replace(baseCatalog.FullPath, "");
                        folderForDoc.Documents = new List<Document>();
                    }

                    folderForDoc.Documents.Add(doc);
                }

                Folders.Clear();
                foreach(var folder in folders)
                {
                    Folders.Add(folder);
                }
            }
        }

        private Document[] FetchDocumentsForClause(string clause)
        {
            if(String.IsNullOrWhiteSpace(clause))
            {
                return new Document[] { };
            }

            return Context.Documents
                .Include("ParentFolder")
                .Where(c => c.Name.Contains(clause))
                .AsNoTracking()
                .ToArray();
        }
    }
}