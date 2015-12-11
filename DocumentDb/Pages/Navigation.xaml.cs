using System.Windows;
using System.Windows.Controls;
using DocumentDb.Common.Extension;
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
                //TODO: Find more elegant way to reset ScrollViewer position after ItemsSource changed
                if(DocumentsListView.Items.Count > 0)
                {
                    var scroll = DocumentsListView.FindVisualChild<ScrollViewer>();
                    if(scroll != null)
                    {
                        scroll.ScrollToTop();
                    }
                }

                ViewModel.DocumentsSource = folder.DocumentsSource;
            }
        }

        private void DocumentsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DocumentsListView.SelectedIndex = -1;
        }
    }
}