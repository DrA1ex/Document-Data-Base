using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using DataLayer.Model;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.Model
{
    public class Folder : NotifyPropertyChanged
    {
        private ICollection<Document> _documents = new List<Document>();
        private readonly ObservableCollection<Folder> _folders = new ObservableCollection<Folder>();
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

        public ICollection<Document> Documents
        {
            get { return _documents; }
            set
            {
                _documents = value;
                RaiseDocumentsChanged();
            }
        }

        public void RaiseDocumentsChanged()
        {
            OnPropertyChanged("Documents");
        }

        public ObservableCollection<Folder> Folders
        {
            get { return _folders; }
        }

        public bool HasChildren
        {
            get { return Folders != null && Folders.Any() || Documents != null && Documents.Any(); }
        }
    }
}