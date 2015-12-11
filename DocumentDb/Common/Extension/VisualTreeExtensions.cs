using System.Windows;
using System.Windows.Media;

namespace DocumentDb.Common.Extension
{
    public static class VisualTreeExtensions
    {
        public static T FindVisualChild<T>(this DependencyObject parent)
            where T : DependencyObject
        {
            for(var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                var visualChild = child as T;
                if(visualChild != null)
                    return visualChild;

                var childOfChild = FindVisualChild<T>(child);
                if(childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}