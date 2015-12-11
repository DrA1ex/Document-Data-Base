using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Common;
using DataLayer.Model;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.ViewModel.Base
{
    public abstract class NavigationViewModelBase : INotifyPropertyChanged
    {
        
        private ICommand _openFileCommand;
        private SynchronizationContext _synchronizationContext;

        public SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext ?? (_synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext()); }
        }

        public ICommand OpenFileCommand
        {
            get { return _openFileCommand ?? (_openFileCommand = new DelegateCommand<Document>(OpenFile)); }
        }

        protected abstract void OpenFile(Document doc);
        public event PropertyChangedEventHandler PropertyChanged;


        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if(PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
