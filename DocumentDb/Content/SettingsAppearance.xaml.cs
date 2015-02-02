using DocumentDb.Content.ViewModel;

namespace DocumentDb.Content
{
    public partial class SettingsAppearance
    {
        private SettingsAppearanceViewModel _viewModel;

        public SettingsAppearance()
        {
            InitializeComponent();

            DataContext = ViewModel;
        }

        public SettingsAppearanceViewModel ViewModel
        {
            get { return _viewModel ?? (_viewModel = new SettingsAppearanceViewModel()); }
        }
    }
}