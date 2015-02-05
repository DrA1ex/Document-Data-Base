using System.Data.Entity;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Common.Utils;
using DataLayer;
using DataLayer.Migrations;
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

            Database.SetInitializer(new MigrateDatabaseToLatestVersion<DdbContext, Configuration>());
            ApplicationWorkers.DirectoryMonitor.Update();
            ApplicationWorkers.DocumentParser.Start();

            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            Logger.Instance.Error("Необработанное исключение: {0}", (object)args.Exception);
            args.Handled = true;
        }
    }
}