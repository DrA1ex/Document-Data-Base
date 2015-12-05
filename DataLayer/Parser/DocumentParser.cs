using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Utils;
using DataLayer.Model;

namespace DataLayer.Parser
{
    public enum DocumentParserState
    {
        [Description("Остановлено")]
        Stopped,
        [Description("Останавливается")]
        Stopping,
        [Description("Ожидание")]
        Idle,
        [Description("Выполняется")]
        Running,
        [Description("Приостановлено")]
        Paused
    }

    public class DocumentParser : IDisposable, INotifyPropertyChanged
    {
        private const int MaxDocumentPerPass = 100;
        private CancellationTokenSource _cancellationTokenSource;
        private ManualResetEvent _pauseResetEvent;
        private DocumentParserState _state;
        private ManualResetEvent _waitEvent;

        public DocumentParser()
        {
            SynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        private SynchronizationContext SynchronizationContext { get; set; }

        public DocumentParserState State
        {
            get { return _state; }
            set
            {
                _state = value;
                OnPropertyChanged();
            }
        }

        private Task WorkerTask { get; set; }

        private CancellationTokenSource CancellationTokenSource
        {
            get { return _cancellationTokenSource ?? (_cancellationTokenSource = new CancellationTokenSource()); }
        }

        private ManualResetEvent PauseResetEvent
        {
            get { return _pauseResetEvent ?? (_pauseResetEvent = new ManualResetEvent(true)); }
        }

        private ManualResetEvent WaitEvent
        {
            get { return _waitEvent ?? (_waitEvent = new ManualResetEvent(false)); }
        }

        public void Dispose()
        {
            Stop();
            WaitEvent.Dispose();
            PauseResetEvent.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ParseDocuments()
        {
            var ct = CancellationTokenSource.Token;
            var pauseHandles = new[] { ct.WaitHandle, PauseResetEvent };
            var delayHandles = new[] { ct.WaitHandle, WaitEvent };

            while(!ct.IsCancellationRequested)
            {
                WaitHandle.WaitAny(pauseHandles);
                SynchronizationContext.Post(state => State = DocumentParserState.Running, null);


                while(!ct.IsCancellationRequested)
                {
                    using(var ctx = new DdbContext())
                    {
                        var docsToParse = ctx.Documents
                            .Where(c => c.Cached == false)
                            .Take(MaxDocumentPerPass);

                        if(!docsToParse.Any())
                        {
                            break;
                        }

                        var docsWithContent = docsToParse
                            .AsParallel()
                            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                            .WithCancellation(ct)
                            .Select(c => new { Document = c, Content = ContentExtractor.GetContent(c, ct) });

                        try
                        {
                            foreach(var record in docsWithContent)
                            {
                                if(ct.IsCancellationRequested || !PauseResetEvent.WaitOne(0))
                                {
                                    break;
                                }

                                try
                                {
                                    FtsService.AddUpdateLuceneIndex(record.Document, record.Content);
                                    record.Document.Cached = true;

                                    StatisticsModel.Instance.DocumentsInCacheCount += 1;
                                }
                                catch(Exception e)
                                {
                                    Logger.Instance.Error("Cannot add file '{0}' to index: {1}", record.Document.Name, e);
                                }
                            }
                        }
                        catch(OperationCanceledException)
                        {
                            //Do nothing
                        }

                        try
                        {
                            ctx.SaveChanges();
                            StatisticsModel.Instance.Refresh(StatisticsModelRefreshMethod.UpdateForDocumentParser);
                        }
                        catch(Exception e)
                        {
                            Logger.Instance.ErrorException("Unable to save indexed documents: {0}", e);
                        }
                    }
                }

                if(PauseResetEvent.WaitOne(0))
                {
                    SynchronizationContext.Post(state => State = DocumentParserState.Idle, null);
                }
                WaitHandle.WaitAny(delayHandles, TimeSpan.FromMinutes(1));
            }
        }

        public void Stop()
        {
            if(WorkerTask != null)
            {
                CancellationTokenSource.Cancel();
                PauseResetEvent.Set();

                SynchronizationContext.Post(state => State = DocumentParserState.Stopping, null);

                WorkerTask.ContinueWith(state =>
                                        {
                                            WorkerTask = null;
                                            CancellationTokenSource.Dispose();
                                            _cancellationTokenSource = null;

                                            SynchronizationContext.Post(c => State = DocumentParserState.Stopped, null);
                                        });
            }
        }

        public void Pause()
        {
            PauseResetEvent.Reset();
            SynchronizationContext.Post(state => State = DocumentParserState.Paused, null);
        }

        public void Start()
        {
            if(State == DocumentParserState.Paused)
            {
                PauseResetEvent.Set();
                SynchronizationContext.Post(state => State = DocumentParserState.Idle, null);
            }
            else if(State == DocumentParserState.Stopped)
            {
                if(WorkerTask == null)
                {
                    PauseResetEvent.Set();
                    WorkerTask = Task.Run((Action)ParseDocuments, CancellationTokenSource.Token);
                }
            }
        }

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