using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private bool _isBusy;
        private ICommand _openFileCommand;
        private SynchronizationContext _synchronizationContext;

        public NavigationViewModel()
        {
            Refresh();
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
                foreach(var document in value)
                {
                    Context.Entry(document).Reload();
                }

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
            if(IsBusy)
            {
                return;
            }

            SynchronizationContext.Send(c => IsBusy = true, null);


            Task.Run(() =>
                     {
                         try
                         {
                             var baseCatalog = Context.BaseFolders
                                 .SingleOrDefault(c => c.FullPath == AppConfigurationStorage.Storage.CatalogPath);

                             if(baseCatalog != null)
                             {
                                 SynchronizationContext.Send(c => Folders.Clear(), null);

                                 foreach(var folder in baseCatalog.Folders)
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
    }
}