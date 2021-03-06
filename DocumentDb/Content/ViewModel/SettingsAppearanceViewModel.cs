﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using DocumentDb.Common.Storage;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Content.ViewModel
{
    public class SettingsAppearanceViewModel
        : NotifyPropertyChanged
    {
        private const string FontSmall = "Мелкий";
        private const string FontLarge = "Крупный";

        private Color _selectedAccentColor;
        private string _selectedFontSize;
        private string _selectedTheme;
        private readonly Color[] _accentColors =
        {
            Color.FromRgb(33, 150, 243),
            Color.FromRgb(3, 169, 244),
            Color.FromRgb(0x1b, 0xa1, 0xe2),
            Color.FromRgb(0x33, 0x99, 0xff),
            Color.FromRgb(0x00, 0x50, 0xef),
            Color.FromRgb(63, 81, 181),

            Color.FromRgb(0, 188, 212),
            Color.FromRgb(0x00, 0xab, 0xa9),
            Color.FromRgb(0, 150, 136),

            Color.FromRgb(205, 220, 57),
            Color.FromRgb(0xa4, 0xc4, 0x00),
            Color.FromRgb(139, 195, 74),
            Color.FromRgb(0x8c, 0xbf, 0x26),
            Color.FromRgb(0x60, 0xa9, 0x17),
            Color.FromRgb(76, 175, 80),
            Color.FromRgb(0x33, 0x99, 0x33),
            Color.FromRgb(0x00, 0x8a, 0x00),
            
            Color.FromRgb(0xe3, 0xc8, 0x00),
            Color.FromRgb(255, 193, 7),
            Color.FromRgb(0xf0, 0xa3, 0x0a),
            Color.FromRgb(255, 152, 0),
            Color.FromRgb(0xf0, 0x96, 0x09),
            Color.FromRgb(0xfa, 0x68, 0x00),
          
            Color.FromRgb(0xff, 0x57, 0x22),
            Color.FromRgb(0xff, 0x45, 0x00),
            Color.FromRgb(0xf4, 0x43, 0x36),
            Color.FromRgb(0xe5, 0x14, 0x00),
            Color.FromRgb(0xa2, 0x00, 0x25),

            Color.FromRgb(0xf4, 0x72, 0xd0),
            Color.FromRgb(0xe9, 0x1e, 0x63),
            Color.FromRgb(0xff, 0x00, 0x97),
            Color.FromRgb(0xd8, 0x00, 0x73),
            Color.FromRgb(156, 39, 176),

            Color.FromRgb(103, 58, 183),
            Color.FromRgb(0x6a, 0x00, 0xff),
            Color.FromRgb(0xa2, 0x00, 0xff),
            Color.FromRgb(0xaa, 0x00, 0xff),

            Color.FromRgb(0x82, 0x5a, 0x2c),
            Color.FromRgb(0x79, 0x55, 0x48),
            Color.FromRgb(0x6d, 0x87, 0x64),
            Color.FromRgb(0x64, 0x76, 0x87),
            Color.FromRgb(96, 125, 139),
            Color.FromRgb(0x76, 0x60, 0x8a),
            Color.FromRgb(0x87, 0x79, 0x4e),
            Color.FromRgb(158, 158, 158),
        };

        private readonly LinkCollection _themes = new LinkCollection();

        public SettingsAppearanceViewModel()
        {
            // add the default themes
            _themes.Add(new Link { DisplayName = "Темная", Source = AppearanceManager.DarkThemeSource });
            _themes.Add(new Link { DisplayName = "Светлая", Source = AppearanceManager.LightThemeSource });

            // add additional themes
            _themes.Add(new Link { DisplayName = "Бинг", Source = new Uri("/DocumentDb;component/Assets/ModernUI.BingImage.xaml", UriKind.Relative) });
            _themes.Add(new Link { DisplayName = "Hello Kitty", Source = new Uri("/DocumentDb;component/Assets/ModernUI.HelloKitty.xaml", UriKind.Relative) });
            _themes.Add(new Link { DisplayName = "Любовь", Source = new Uri("/DocumentDb;component/Assets/ModernUI.Love.xaml", UriKind.Relative) });
            _themes.Add(new Link { DisplayName = "Снежинки", Source = new Uri("/DocumentDb;component/Assets/ModernUI.Snowflakes.xaml", UriKind.Relative) });
            if(AppConfigurationStorage.Storage.ValentineDayThemeUnlocked)
            {
                _themes.Add(new Link { DisplayName = "День св. Валентина", Source = new Uri("/DocumentDb;component/Assets/ModernUI.Valentine.xaml", UriKind.Relative) });
            }

            LoadSettings();

            SelectedFontSize = AppearanceManager.Current.FontSize == FontSize.Large ? FontLarge : FontSmall;

            SyncThemeAndColor();

            AppearanceManager.Current.PropertyChanged += OnAppearanceManagerPropertyChanged;
        }

        public LinkCollection Themes
        {
            get { return _themes; }
        }

        public string[] FontSizes
        {
            get { return new[] { FontSmall, FontLarge }; }
        }

        public Color[] AccentColors
        {
            get
            {return _accentColors;
            }
        }

        public string SelectedTheme
        {
            get { return _selectedTheme; }
            set
            {
                if(_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged("SelectedTheme");

                    // and update the actual theme
                    var theme = _themes.FirstOrDefault(c => c.DisplayName == value);
                    if(theme != null)
                    {
                        AppearanceManager.Current.ThemeSource = theme.Source;
                        AppConfigurationStorage.Storage.Theme = AppearanceManager.Current.ThemeSource;
                    }
                }
            }
        }

        public string SelectedFontSize
        {
            get { return _selectedFontSize; }
            set
            {
                if(_selectedFontSize != value)
                {
                    _selectedFontSize = value;
                    OnPropertyChanged("SelectedFontSize");

                    AppearanceManager.Current.FontSize = value == FontLarge ? FontSize.Large : FontSize.Small;
                    AppConfigurationStorage.Storage.FontSize = AppearanceManager.Current.FontSize;
                }
            }
        }

        public Color SelectedAccentColor
        {
            get { return _selectedAccentColor; }
            set
            {
                if(_selectedAccentColor != value)
                {
                    _selectedAccentColor = value;
                    OnPropertyChanged("SelectedAccentColor");

                    AppearanceManager.Current.AccentColor = value;
                    AppConfigurationStorage.Storage.AccentColor = AppearanceManager.Current.AccentColor;
                }
            }
        }

        public void LoadSettings()
        {
            if(AppConfigurationStorage.Storage.AccentColor != Colors.White)
            {
                SelectedAccentColor = AppConfigurationStorage.Storage.AccentColor;
            }
        }

        private void SyncThemeAndColor()
        {
            // synchronizes the selected viewmodel theme with the actual theme used by the appearance manager.
            var theme = _themes.FirstOrDefault(l => l.Source.Equals(AppearanceManager.Current.ThemeSource));
            if(theme != null)
            {
                SelectedTheme = theme.DisplayName;
            }

            // and make sure accent color is up-to-date
            SelectedAccentColor = AppearanceManager.Current.AccentColor;
        }

        private void OnAppearanceManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "ThemeSource" || e.PropertyName == "AccentColor")
            {
                SyncThemeAndColor();
            }
        }
    }
}