using System.Data.Entity;
using System.IO;
using System.Windows;
using DataLayer;
using DocumentDb.Common;

namespace DocumentDb
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if(!Directory.Exists("data"))
            {
                Directory.CreateDirectory("data");
            }

            Database.SetInitializer(new MigrateDatabaseToLatestVersion<DdbContext, DataLayer.Migrations.Configuration>());
            ApplicationWorkers.DirectoryMonitor.Update();
            ApplicationWorkers.DocumentParser.Start();
        }
    }
}