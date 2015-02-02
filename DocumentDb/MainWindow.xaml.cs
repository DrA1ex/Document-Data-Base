using DocumentDb.Common.Storage;

namespace DocumentDb
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            AppConfigurationStorage.Storage.LoadAppearanceConfiguration();
        }
    }
}