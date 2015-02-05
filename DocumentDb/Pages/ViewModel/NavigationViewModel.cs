using System;
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
using DataLayer.Parser;
using DocumentDb.Common;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace DocumentDb.Pages.ViewModel
{
    public class NavigationViewModel : NotifyPropertyChanged
    {
        private DdbContext _context;
        private CancellationTokenSource _cts;
        private ObservableCollection<Document> _documents;
        private bool _documentsIsLoading;
        private ObservableCollection<Folder> _folders;
        private bool _folderTreeIsLoading;
        private ICommand _openFileCommand;
        private SynchronizationContext _synchronizationContext;
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
                OnPropertyChanged("FolderTreeIsLoading");
            }
        }

        public bool DocumentsIsLoading
        {
            get { return _documentsIsLoading; }
            set
            {
                _documentsIsLoading = value;
                OnPropertyChanged("DocumentsIsLoading");
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

        public ObservableCollection<Document> Documents
        {
            get { return _documents ?? (_documents = new ObservableCollection<Document>()); }
        }

        public ICommand OpenFileCommand
        {
            get { return _openFileCommand ?? (_openFileCommand = new DelegateCommand<Document>(OpenFile)); }
        }

        public void SetDocuments(long[] documentsId)
        {
            if(_cts != null)
            {
                _cts.Cancel();
            }

            lock(_syncDummy)
            {
                if(_cts != null)
                {
                    _cts.Dispose();
                }
                _cts = new CancellationTokenSource();
            }
            Task.Run(() =>
                     {
                         lock(_syncDummy)
                         {
                             SynchronizationContext.Post(c => DocumentsIsLoading = true, null);
                             SynchronizationContext.Post(c => _documents.Clear(), null);
                             var token = _cts.Token;

                             try
                             {
                                 IQueryable<Document> docsEnumerated;
                                 using(var ctx = new DdbContext())
                                 {
                                     var documents = ctx.Documents.Where(c => documentsId.Contains(c.Id));
                                     if(AppConfigurationStorage.Storage.IndexUnsupportedFormats)
                                     {
                                         docsEnumerated = documents;
                                     }
                                     else
                                     {
                                         docsEnumerated = documents.Where(c => c.Type != DocumentType.Undefined);
                                     }


                                     if(!token.IsCancellationRequested)
                                     {
                                         foreach(var document in docsEnumerated)
                                         {
                                             SynchronizationContext.Send(c => _documents.Add((Document)c), document);
                                         }
                                     }
                                 }
                             }
                             finally
                             {
                                 if(!token.IsCancellationRequested)
                                 {
                                     SynchronizationContext.Post(c => OnPropertyChanged("Documents"), null);
                                     SynchronizationContext.Post(c => DocumentsIsLoading = false, null);
                                 }
                             }
                         }
                     }, _cts.Token);
        }

        private void OpenFile(Document doc)
        {
            var fileToOpen = Path.Combine(doc.ParentFolder.FullPath, doc.Name);

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

        public void RecreateContext()
        {
            Context.Dispose();
            _context = null;
        }

        public void Refresh()
        {
            if(FolderTreeIsLoading)
            {
                return;
            }

            SynchronizationContext.Send(c => FolderTreeIsLoading = true, null);


            Task.Run(() =>
                     {
                         try
                         {
                             SynchronizationContext.Send(c => Folders.Clear(), null);
                             RecreateContext();

                             var baseCatalog = Context.BaseFolders
                                 .SingleOrDefault(c => c.FullPath == AppConfigurationStorage.Storage.CatalogPath);

                             if(baseCatalog != null)
                             {
                                 foreach(var folder in baseCatalog.Folders)
                                 {
                                     SynchronizationContext.Post(c => Folders.Add((Folder)c), folder);
                                 }
                             }
                         }
                         finally
                         {
                             SynchronizationContext.Send(c => FolderTreeIsLoading = false, null);
                         }
                     });
        }
    }
}