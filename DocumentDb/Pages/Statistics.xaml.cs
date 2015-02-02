using DocumentDb.Pages.ViewModel;

namespace DocumentDb.Pages
{
    public partial class Statistics
    {
        private StatisticsViewModel _viewModel;

        public Statistics()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public StatisticsViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new StatisticsViewModel()); }
        }
    }
}