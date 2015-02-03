﻿using System;
using System.Linq.Expressions;
using System.Windows.Media;
using Common.Utils;
using DocumentDb.Common.Storage.Model;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Common.Storage
{
    public class AppConfigurationStorage
    {
        public static readonly AppConfigurationStorage Storage = new AppConfigurationStorage();
        private static readonly DataStorage<AppConfiguration> DataStorage = DataStorage<AppConfiguration>.Instance;

        private AppConfigurationStorage()
        {
        }

        public string CatalogPath
        {
            get { return GetterForModelProperty(c => c.CatalogPath); }
            set { SetterForModelProperty(c => c.CatalogPath, value); }
        }

        public string Palette
        {
            get { return GetterForModelProperty(c => c.Palette); }
            set { SetterForModelProperty(c => c.Palette, value); }
        }

        public Uri Theme
        {
            get { return GetterForModelProperty(c => c.Theme); }
            set { SetterForModelProperty(c => c.Theme, value); }
        }

        public Color AccentColor
        {
            get { return GetterForModelProperty(c => c.AccentColor); }
            set { SetterForModelProperty(c => c.AccentColor, value); }
        }

        public FontSize FontSize
        {
            get { return GetterForModelProperty(c => c.FontSize); }
            set { SetterForModelProperty(c => c.FontSize, value); }
        }

        public bool ValentineDayThemeUnlocked
        {
            get { return GetterForModelProperty(c => c.ValentineDayThemeUnlocked); }
            set { SetterForModelProperty(c => c.ValentineDayThemeUnlocked, value); }
        }

        public void LoadAppearanceConfiguration()
        {
            AppearanceManager.Current.FontSize = FontSize;

            if(Theme != null && !String.IsNullOrEmpty(Theme.ToString()))
            {
                AppearanceManager.Current.ThemeSource = Theme;
            }

            if(AccentColor != default(Color))
            {
                AppearanceManager.Current.AccentColor = AccentColor;
            }

            var today = DateTime.Today;
            if(today.Day == 14 && today.Month == 2)
            {
                AppearanceManager.Current.ThemeSource = Theme = new Uri("/DocumentDb;component/Assets/ModernUI.Valentine.xaml", UriKind.Relative);
                AppearanceManager.Current.AccentColor = AccentColor = Color.FromRgb(233, 30, 99);
                Palette = "Material";
                ValentineDayThemeUnlocked = true;
            }
        }

        private TReturn GetterForModelProperty<TReturn>(Expression<Func<AppConfiguration, TReturn>> propertyExpression)
        {
            var storage = DataStorage.GetStorage();
            if(storage != null)
            {
                return storage.GetValueFromPath(propertyExpression);
            }

            return default(TReturn);
        }

        private void SetterForModelProperty<TReturn>(Expression<Func<AppConfiguration, TReturn>> propertyExpression,
            TReturn value)
        {
            var storage = DataStorage.GetStorage() ?? new AppConfiguration();
            storage.SetValueFromPath(propertyExpression, value);
            DataStorage.UpdateStorage(storage);
            DataStorage.Save();
        }
    }
}