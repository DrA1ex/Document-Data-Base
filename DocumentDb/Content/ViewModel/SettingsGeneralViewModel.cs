﻿using System;
using System.Windows.Forms;
using System.Windows.Input;
using Common;
using DataLayer.Parser;
using DocumentDb.Common.Extension;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;
using Application = System.Windows.Application;

namespace DocumentDb.Content.ViewModel
{
    public class SettingsGeneralViewModel : NotifyPropertyChanged
    {
        private string _currentDirectory = AppConfigurationStorage.Storage.CatalogPath;
        private ICommand _pickCatalogCommand;

        public string CurrentDirectory
        {
            get { return _currentDirectory; }
            set
            {
                _currentDirectory = value;
                AppConfigurationStorage.Storage.CatalogPath = value;
                OnPropertyChanged("CurrentDirectory");
            }
        }

        public ICommand PickCatalogCommand
        {
            get { return _pickCatalogCommand ?? (_pickCatalogCommand = new DelegateCommand(PickCatalog)); }
        }

        private void PickCatalog()
        {
            var dialog = new FolderBrowserDialog
                         {
                             SelectedPath = CurrentDirectory
                         };

            dialog.ShowDialog(Application.Current.MainWindow.GetIWin32Window());

            if(!String.IsNullOrEmpty(dialog.SelectedPath))
            {
                CurrentDirectory = dialog.SelectedPath;

                var monitor = new DirectoryMonitor(CurrentDirectory);
                monitor.Update();
            }
        }
    }
}