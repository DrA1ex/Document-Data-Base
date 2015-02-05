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
        private IEnumerable<Document> _documents;
        private ObservableCollection<Folder> _folders;
        private bool _isBusy;
        private ICommand _openFileCommand;
        private SynchronizationContext _synchronizationContext;

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
                Document[] docsEnumerated;
                if(AppConfigurationStorage.Storage.IndexUnsupportedFormats)
                {
                    docsEnumerated = value.ToArray();
                }
                else
                {
                    docsEnumerated = value.Where(c => c.Type != DocumentType.Undefined).ToArray();
                }

                foreach(var document in docsEnumerated)
                {
                    Context.Entry(document).Reload();
                }

                _documents = docsEnumerated;

                OnPropertyChanged("Documents");
            }
        }

        public ICommand OpenFileCommand
        {
            get { return _openFileCommand ?? (_openFileCommand = new DelegateCommand<Document>(OpenFile)); }
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
            if(IsBusy)
            {
                return;
            }

            SynchronizationContext.Send(c => IsBusy = true, null);


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
                             SynchronizationContext.Send(c => IsBusy = false, null);
                         }
                     });
        }
    }
}