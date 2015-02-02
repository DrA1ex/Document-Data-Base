using System;
using System.Windows.Media;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Common.Storage.Model
{
    public class AppConfiguration
    {
        public string CatalogPath { get; set; }
        public string Palette { get; set; }
        public Uri Theme { get; set; }
        public Color AccentColor { get; set; }
        public FontSize FontSize { get; set; }
        public bool ValentineDayThemeUnlocked { get; set; }
    }
}