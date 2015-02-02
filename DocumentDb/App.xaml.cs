using System.Data.Entity;
using System.IO;
using System.Windows;
using DataLayer;

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
        }
    }
}