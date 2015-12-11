using System.Windows.Controls;
using DocumentDb.Common.Extension;
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
            //TODO: Find more elegant way to reset ScrollViewer position after ItemsSource changed
            if(DocumentsListView.Items.Count > 0)
            {
                var scroll = DocumentsListView.FindVisualChild<ScrollViewer>();
                if(scroll != null)
                {
                    scroll.ScrollToTop();
                }
            }
        }

        private void DocumentsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DocumentsListView.SelectedIndex = -1;
        }
    }
}