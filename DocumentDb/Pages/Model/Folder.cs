using DataLayer.Model;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.Model
{
    public class Folder : NotifyPropertyChanged
    {
        private Document[] _documents;
        private Folder[] _folders;
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

        public Document[] Documents
        {
            get { return _documents; }
            set
            {
                _documents = value;
                OnPropertyChanged("Documents");
            }
        }

        public Folder[] Folders
        {
            get { return _folders; }
            set
            {
                _folders = value;
                OnPropertyChanged("Folders");
            }
        }
    }
}