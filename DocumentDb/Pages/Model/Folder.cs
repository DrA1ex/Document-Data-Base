using System.Collections.Generic;
using System.Linq;
using DataLayer.Model;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.Model
{
    public class Folder : NotifyPropertyChanged
    {
        private ICollection<Document> _documents;
        private ICollection<Folder> _folders;
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
                OnPropertyChanged("Documents");
            }
        }

        public ICollection<Folder> Folders
        {
            get { return _folders; }
            set
            {
                _folders = value;
                OnPropertyChanged("Folders");
            }
        }

        public bool HasChildren
        {
            get { return Folders != null && Folders.Any() || Documents != null && Documents.Any(); }
        }
    }
}