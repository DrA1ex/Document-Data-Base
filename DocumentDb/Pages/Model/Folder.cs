using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using DataLayer.Model;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.Model
{
    public class Folder : NotifyPropertyChanged
    {
        private readonly ObservableCollection<Document> _documents = new ObservableCollection<Document>();
        private readonly ObservableCollection<Folder> _folders = new ObservableCollection<Folder>();
        private CollectionViewSource _documentsSource;
        private CollectionViewSource _folderSource;
        private string _fullPath;
        private string _name;

        public string FullPath
        {
            get { return _fullPath; }
            set
            {
                _fullPath = value;
                OnPropertyChanged("FullPath");
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        public ObservableCollection<Document> Documents
        {
            get { return _documents; }
        }

        public ObservableCollection<Folder> Folders
        {
            get { return _folders; }
        }

        public ICollectionView DocumentsSource
        {
            get
            {
                if(_documentsSource == null)
                {
                    _documentsSource = new CollectionViewSource {Source = Documents};
                    _documentsSource.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                    _documentsSource.IsLiveSortingRequested = true;
                    _documentsSource.Filter += (sender, args) =>
                    {
                        var doc = (Document)args.Item;
                        args.Accepted = doc.Type != DocumentType.Undefined || AppConfigurationStorage.Storage.IndexUnsupportedFormats;
                    };
                    _documentsSource.IsLiveFilteringRequested = true;
                }

                return _documentsSource.View;
            }
        }

        public ICollectionView FoldersSource
        {
            get
            {
                if(_folderSource == null)
                {
                    _folderSource = new CollectionViewSource {Source = Folders};
                    _folderSource.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                    _folderSource.IsLiveSortingRequested = true;
                }

                return _folderSource.View;
            }
        }

        public bool HasChildren
        {
            get { return Folders != null && Folders.Any() || Documents != null && Documents.Any(); }
        }

        public void RaiseDocumentsChanged()
        {
            OnPropertyChanged("Documents");
        }
    }
}