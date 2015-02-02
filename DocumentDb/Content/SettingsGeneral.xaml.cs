using DocumentDb.Content.ViewModel;

namespace DocumentDb.Content
{
    public partial class SettingsGeneral
    {
        private SettingsGeneralViewModel _viewModel;

        public SettingsGeneral()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public SettingsGeneralViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new SettingsGeneralViewModel()); }
        }
    }
}