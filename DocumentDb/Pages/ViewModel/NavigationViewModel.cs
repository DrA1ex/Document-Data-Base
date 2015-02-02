using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class NavigationViewModel : NotifyPropertyChanged
    {
        private DdbContext _context;
        private IEnumerable<Document> _documents;
        private ObservableCollection<Folder> _folders;
        private ICommand _openFileCommand;

        public NavigationViewModel()
        {
            Refresh();
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
                Folders.Clear();

                foreach(var folder in baseCatalog.Folders)
                {
                    Folders.Add(folder);
                }
            }
        }
    }
}