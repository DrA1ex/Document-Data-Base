using System.Windows;
using DocumentDb.Pages.Model;
using DocumentDb.Pages.ViewModel;

namespace DocumentDb.Pages
{
    public partial class Navigation
    {
        private NavigationViewModel _viewModel;

        public Navigation()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public NavigationViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new NavigationViewModel()); }
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var folder = e.NewValue != null
                ? e.NewValue as Folder ?? ViewModel.RootFolder
                : null;
            if(folder != null)
            {
                DocumentsScrollViewer.ScrollToTop();
                ViewModel.DocumentsSource = folder.DocumentsSource;
            }
        }
    }
}