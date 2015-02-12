using System.Windows.Controls;
using DocumentDb.Pages.ViewModel;

namespace DocumentDb.Pages
{
    public partial class Search
    {
        private SearchViewModel _viewModel;

        public Search()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public SearchViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new SearchViewModel()); }
        }

        private void SelectorOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DocumentScrollViewer.ScrollToTop();
        }
    }
}