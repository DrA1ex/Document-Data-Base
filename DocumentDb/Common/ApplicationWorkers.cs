using DataLayer.Parser;
using DocumentDb.Common.Storage;

namespace DocumentDb.Common
{
    internal static class ApplicationWorkers
    {
        static ApplicationWorkers()
        {
            DirectoryMonitor = new DirectoryMonitor(AppConfigurationStorage.Storage.CatalogPath);
            DocumentParser = new DocumentParser();
        }

        public static DirectoryMonitor DirectoryMonitor { get; set; }
        public static DocumentParser DocumentParser { get; set; }

    }
}
