using System;
using System.Data.Entity;
using System.IO;
using System.Threading;
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

            Current.Exit += OnApplicationExit;

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            //TODO: Remove this. Use Task more accurate instead. i.e. run long-running tasks in separate Thread
            ThreadPool.SetMaxThreads(16, 16);

            Database.SetInitializer(new MigrateDatabaseToLatestVersion<DdbContext, Configuration>());
            ApplicationWorkers.DirectoryMonitor.Update();
            ApplicationWorkers.DocumentParser.Start();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            ApplicationWorkers.DocumentParser.Stop();
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Logger.Instance.Error("Необработанное исключение: {0}", args.ExceptionObject);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            Logger.Instance.Error("Необработанное исключение: {0}", (object)args.Exception);
            args.Handled = true;
        }
    }
}