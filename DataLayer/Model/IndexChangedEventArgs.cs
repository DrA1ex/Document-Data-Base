using System;
using System.IO;

namespace DataLayer.Model
{
    public enum IndexChangeKind
    {
        New,
        Removed,
        Updated,
        Moved
    }

    internal static class WatcherChangedEventUtils
    {
        public static IndexChangeKind AsIndexChangeKind(this WatcherChangeTypes changeType)
        {
            switch(changeType)
            {
                case WatcherChangeTypes.Created:
                    return IndexChangeKind.New;

                case WatcherChangeTypes.Deleted:
                    return IndexChangeKind.Removed;

                case WatcherChangeTypes.Changed:
                    return IndexChangeKind.Updated;

                case WatcherChangeTypes.Renamed:
                    return IndexChangeKind.Moved;
                
                default:
                    throw new ArgumentOutOfRangeException("changeType", changeType, null);
            }
        }
    }

    public class IndexChangedEventArgs : EventArgs
    {
        public IndexChangeKind Kind { get; set; }
    }

    public class DocumentChangedEventArgs : IndexChangedEventArgs
    {
        public Document Document { get; set; }
    }

    public class DocumentMovedEventArgs : DocumentChangedEventArgs
    {
        public Document OldDocument { get; set; }
    }

    internal class WatcherChangedEventArgs : IndexChangedEventArgs
    {
        public String FullPath { get; set; }
    }

    internal class WatcherRenameEventArgs : WatcherChangedEventArgs
    {
        public WatcherRenameEventArgs()
        {
            Kind = IndexChangeKind.Moved;
        }

        public String OldFullPath { get; set; }
    }
}
