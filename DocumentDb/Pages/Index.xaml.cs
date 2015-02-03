using DocumentDb.Pages.ViewModel;

namespace DocumentDb.Pages
{
    public partial class Index
    {
        private IndexingViewModel _viewModel;

        public Index()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public IndexingViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new IndexingViewModel()); }
        }
    }
}