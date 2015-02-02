using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace DataLayer.Model
{
    public class StatisticsModel : INotifyPropertyChanged
    {
        public static readonly StatisticsModel Instance = new StatisticsModel();
        private float _databaseSize;
        private int _documentsInCacheCount;
        private float _ftsIndexSize;
        private bool _isBusy;
        private int _parsedDocumentsCount;
        private int _parsedFoldersCount;
        private SynchronizationContext _synchronizationContext;

        private StatisticsModel()
        {
            Refresh();
        }

        public float FtsIndexSize
        {
            get { return _ftsIndexSize; }
            set
            {
                SynchronizationContext.Send(c => _ftsIndexSize = (float)c, value);
                SynchronizationContext.Post(c => OnPropertyChanged(), null);
            }
        }

        public float DatabaseSize
        {
            get { return _databaseSize; }
            set
            {
                SynchronizationContext.Send(c => _databaseSize = (float)c, value);
                SynchronizationContext.Post(c => OnPropertyChanged(), null);
            }
        }

        public int ParsedFoldersCount
        {
            get { return _parsedFoldersCount; }
            set
            {
                SynchronizationContext.Send(c => _parsedFoldersCount = (int)c, value);
                SynchronizationContext.Post(c => OnPropertyChanged(), null);
            }
        }

        public int ParsedDocumentsCount
        {
            get { return _parsedDocumentsCount; }
            set
            {
                SynchronizationContext.Send(c => _parsedDocumentsCount = (int)c, value);
                SynchronizationContext.Post(c => OnPropertyChanged(), null);
            }
        }

        public int DocumentsInCacheCount
        {
            get { return _documentsInCacheCount; }
            set
            {
                SynchronizationContext.Send(c => _documentsInCacheCount = (int)c, value);
                SynchronizationContext.Post(c => OnPropertyChanged(), null);
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext ?? (_synchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext()); }
        }

        public void Refresh()
        {
            if(IsBusy)
            {
                return;
            }

            SynchronizationContext.Send(c => IsBusy = true, null);

            Task.Run(() =>
                     {
                         try
                         {
                             using(var ctx = new DdbContext())
                             {
                                 var ftsIndexSize = new DirectoryInfo(FtsService.LuceneDir).Size() / 1024.0f / 1024.0f;

                                 // ReSharper disable once PossibleNullReferenceException
                                 var dbIndexSize = new FileInfo(Path.Combine(Environment.CurrentDirectory,
                                     ctx.Database.Connection.DataSource.Replace("|DataDirectory|", "")))
                                     .Length / 1024.0f / 1024.0f;

                                 var parsedFolders = ctx.Folders.Count();
                                 var parsedDocs = ctx.Documents.Count();
                                 var cachedDocs = ctx.Documents.Count(c => c.Cached);

                                 SynchronizationContext.Post(c => FtsIndexSize = (float)c, ftsIndexSize);
                                 SynchronizationContext.Post(c => DatabaseSize = (float)c, dbIndexSize);
                                 SynchronizationContext.Post(c => ParsedFoldersCount = (int)c, parsedFolders);
                                 SynchronizationContext.Post(c => ParsedDocumentsCount = (int)c, parsedDocs);
                                 SynchronizationContext.Post(c => DocumentsInCacheCount = (int)c, cachedDocs);
                             }
                         }
                         finally
                         {
                             SynchronizationContext.Send(c => IsBusy = false, null);
                         }
                     });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if(handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}