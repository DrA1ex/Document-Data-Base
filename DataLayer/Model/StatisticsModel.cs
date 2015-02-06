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
    public enum StatisticsModelRefreshMethod
    {
        UpdateAll,
        UpdateForDocumentParser,
        UpdateForDocumentMonitor
    }

    public class StatisticsModel : INotifyPropertyChanged
    {
        public static readonly StatisticsModel Instance = new StatisticsModel();
        private float _databaseSize;
        private int _documentsInCacheCount;
        private float _ftsIndexSize;
        private bool _isBusy;
        private int _parsedDocumentsCount;
        private readonly object _syncDummy = new object();
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

        public int ParsedDocumentsCount
        {
            get { return _parsedDocumentsCount; }
            set
            {
                lock(_syncDummy)
                {
                    _parsedDocumentsCount = value;
                    SynchronizationContext.Send(c => OnPropertyChanged(), null);
                }
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

        public void Refresh(StatisticsModelRefreshMethod refreshMethod = StatisticsModelRefreshMethod.UpdateAll)
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
                                 float ftsIndexSize = 0;
                                 if(Directory.Exists(FtsService.LuceneDir))
                                 {
                                     ftsIndexSize = new DirectoryInfo(FtsService.LuceneDir).Size() / 1024.0f / 1024.0f;
                                 }

                                 // ReSharper disable once PossibleNullReferenceException
                                 var dbPath = Path.Combine(Environment.CurrentDirectory,
                                     ctx.Database.Connection.DataSource.Replace("|DataDirectory|", ""));
                                 float dbIndexSize = 0;
                                 if(!String.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
                                 {
                                     dbIndexSize = new FileInfo(dbPath)
                                         .Length / 1024.0f / 1024.0f;
                                 }

                                 var parsedDocs = ctx.Documents.Count();
                                 var cachedDocs = ctx.Documents.Count(c => c.Cached);

                                 SynchronizationContext.Post(c => FtsIndexSize = (float)c, ftsIndexSize);
                                 SynchronizationContext.Post(c => DatabaseSize = (float)c, dbIndexSize);

                                 if(refreshMethod == StatisticsModelRefreshMethod.UpdateAll || refreshMethod == StatisticsModelRefreshMethod.UpdateForDocumentParser)
                                 {
                                     SynchronizationContext.Post(c => DocumentsInCacheCount = (int)c, cachedDocs);
                                 }
                                 if(refreshMethod == StatisticsModelRefreshMethod.UpdateAll || refreshMethod == StatisticsModelRefreshMethod.UpdateForDocumentMonitor)
                                 {
                                     SynchronizationContext.Post(c => ParsedDocumentsCount = (int)c, parsedDocs);
                                 }
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