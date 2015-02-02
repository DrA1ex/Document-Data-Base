using System.Windows;
using DataLayer.Model;
using DocumentDb.Pages.ViewModel;

namespace DocumentDb.Pages
{
    public partial class Search
    {
        private SearchViewModel _viewModel;

        public SearchViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new SearchViewModel()); }
        }

        public Search()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var folder = e.NewValue as Folder;
            if(folder != null)
            {
                ViewModel.Documents = folder.Documents;
            }
        }
    }
}
