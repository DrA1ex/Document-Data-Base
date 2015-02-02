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
        [Description("Ожидание")]
        Iddle,
        [Description("Выполняется")]
        Running,
        [Description("Приостановлено")]
        Paused
    }

    public class DocumentParser : IDisposable, INotifyPropertyChanged
    {
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
                SynchronizationContext.Send(state => State = DocumentParserState.Running, null);

                if(!ct.IsCancellationRequested)
                {
                    using(var ctx = new DdbContext())
                    {
                        var docsToParse = ctx.Documents
                            .Include("ParentFolder")
                            .Where(c => c.Cached == false);

                        foreach(var document in docsToParse)
                        {
                            if(ct.IsCancellationRequested || !PauseResetEvent.WaitOne(0))
                            {
                                break;
                            }

                            try
                            {
                                FtsService.AddUpdateLuceneIndex(document, ContentExtractor.GetContent(document));
                                document.Cached = true;

                                StatisticsModel.Instance.DocumentsInCacheCount += 1;
                            }
                            catch(Exception e)
                            {
                                Logger.Instance.Error("Cannot add file '{0}' to index: {1}", document.Name, e);
                            }
                        }

                        try
                        {
                            ctx.SaveChanges();
                            StatisticsModel.Instance.Refresh();
                        }
                        catch(Exception e)
                        {
                            Logger.Instance.ErrorException("Unable to save indexed documents: {0}", e);
                        }
                    }
                }

                if(PauseResetEvent.WaitOne(0))
                {
                    SynchronizationContext.Send(state => State = DocumentParserState.Iddle, null);
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

                WorkerTask.Wait();
                WorkerTask = null;
                CancellationTokenSource.Dispose();
                _cancellationTokenSource = null;

                SynchronizationContext.Send(state => State = DocumentParserState.Stopped, null);
            }
        }

        public void Pause()
        {
            PauseResetEvent.Reset();
            SynchronizationContext.Send(state => State = DocumentParserState.Paused, null);
        }

        public void Start()
        {
            if(State != DocumentParserState.Paused)
            {
                if(WorkerTask == null)
                {
                    PauseResetEvent.Set();
                    WorkerTask = Task.Run((Action)ParseDocuments, CancellationTokenSource.Token);
                }
            }
            else
            {
                PauseResetEvent.Set();
                SynchronizationContext.Send(state => State = DocumentParserState.Running, null);
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