using System.Linq;
using System.Windows.Input;
using Common;
using DataLayer;
using DataLayer.Model;
using DataLayer.Parser;
using DocumentDb.Common;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.ViewModel
{
    public class IndexingViewModel : NotifyPropertyChanged
    {
        private RelayCommand _clearFtsIndexCommand;
        private RelayCommand _clearIndexCommand;
        private DocumentMonitorState _documentMonitorState;
        private DocumentParserState _documentParserState;
        private ICommand _optimizeFtsIndexCommand;
        private RelayCommand _pauseParserCommand;
        private RelayCommand _startParserCommand;
        private RelayCommand _stopParserCommand;
        private RelayCommand _updateIndexCommand;

        public IndexingViewModel()
        {
            DocumentMonitorState = ApplicationWorkers.DirectoryMonitor.State;
            ApplicationWorkers.DirectoryMonitor.PropertyChanged += (sender, args) =>
            {
                if(args.PropertyName == "State")
                {
                    DocumentMonitorState = ApplicationWorkers.DirectoryMonitor.State;
                    UpdateIndexCommand.OnCanExecuteChanged();
                    ClearIndexCommand.OnCanExecuteChanged();
                    ClearFtsIndexCommand.OnCanExecuteChanged();
                }
            };

            DocumentParserState = ApplicationWorkers.DocumentParser.State;
            ApplicationWorkers.DocumentParser.PropertyChanged += (sender, args) =>
            {
                if(args.PropertyName == "State")
                {
                    DocumentParserState = ApplicationWorkers.DocumentParser.State;
                    StopParserCommand.OnCanExecuteChanged();
                    StartParserCommand.OnCanExecuteChanged();
                    PauseParserCommand.OnCanExecuteChanged();
                    ClearIndexCommand.OnCanExecuteChanged();
                    ClearFtsIndexCommand.OnCanExecuteChanged();
                }
            };

            StatisticsModel.Instance.PropertyChanged += (sender, args) =>
            {
                if(args.PropertyName == "IsBusy")
                {
                    ClearIndexCommand.OnCanExecuteChanged();
                    ClearFtsIndexCommand.OnCanExecuteChanged();
                }
            };
        }

        public DocumentMonitorState DocumentMonitorState
        {
            get { return _documentMonitorState; }
            set
            {
                _documentMonitorState = value;
                OnPropertyChanged("DocumentMonitorState");
            }
        }

        public DocumentParserState DocumentParserState
        {
            get { return _documentParserState; }
            set
            {
                _documentParserState = value;
                OnPropertyChanged("DocumentParserState");
            }
        }

        public RelayCommand StopParserCommand
        {
            get
            {
                return _stopParserCommand ??
                       (_stopParserCommand =
                           new RelayCommand(StopParser, o => DocumentParserState == DocumentParserState.Running
                                                             || DocumentParserState == DocumentParserState.Idle));
            }
        }

        public RelayCommand StartParserCommand
        {
            get
            {
                return _startParserCommand ??
                       (_startParserCommand =
                           new RelayCommand(StartParser,
                               o =>
                                   DocumentParserState == DocumentParserState.Stopped
                                   || DocumentParserState == DocumentParserState.Paused));
            }
        }

        public RelayCommand PauseParserCommand
        {
            get
            {
                return _pauseParserCommand ??
                       (_pauseParserCommand =
                           new RelayCommand(PauseParser, o => DocumentParserState == DocumentParserState.Running
                                                              || DocumentParserState == DocumentParserState.Idle));
            }
        }

        public RelayCommand UpdateIndexCommand
        {
            get
            {
                return _updateIndexCommand ??
                       (_updateIndexCommand =
                           new RelayCommand(UpdateIndex, o => DocumentMonitorState == DocumentMonitorState.Idle));
            }
        }

        public ICommand OptimizeFtsIndexCommand
        {
            get { return _optimizeFtsIndexCommand ?? (_optimizeFtsIndexCommand = new DelegateCommand(OptimizeFtsIndex)); }
        }

        public RelayCommand ClearIndexCommand
        {
            get
            {
                return _clearIndexCommand ??
                       (_clearIndexCommand =
                           new RelayCommand(ClearDbIndex, o => !StatisticsModel.Instance.IsBusy
                                                               && (DocumentParserState == DocumentParserState.Idle
                                                                   || DocumentParserState == DocumentParserState.Stopped)
                                                               && DocumentMonitorState == DocumentMonitorState.Idle));
            }
        }

        public RelayCommand ClearFtsIndexCommand
        {
            get
            {
                return _clearFtsIndexCommand ??
                       (_clearFtsIndexCommand =
                           new RelayCommand(ClearFtsIndex, o => !StatisticsModel.Instance.IsBusy
                                                                && (DocumentParserState == DocumentParserState.Idle
                                                                    || DocumentParserState == DocumentParserState.Stopped)
                                                                && DocumentMonitorState == DocumentMonitorState.Idle));
            }
        }

        private void StopParser(object o)
        {
            ApplicationWorkers.DocumentParser.Stop();
        }

        private void StartParser(object o)
        {
            ApplicationWorkers.DocumentParser.Start();
        }

        private void PauseParser(object o)
        {
            ApplicationWorkers.DocumentParser.Pause();
        }

        private void UpdateIndex(object o)
        {
            ApplicationWorkers.DirectoryMonitor.Update(true);
        }

        private void OptimizeFtsIndex()
        {
            FtsService.Optimize();
        }

        private void ClearDbIndex(object o)
        {
            FtsService.ClearLuceneIndex();
            using(var ctx = new DdbContext())
            {
                ctx.Documents.RemoveRange(ctx.Documents);
                ctx.SaveChanges();

                StatisticsModel.Instance.DocumentsInCacheCount = 0;
                StatisticsModel.Instance.ParsedDocumentsCount = 0;
            }

            ApplicationWorkers.DirectoryMonitor.OnNeedUpdate();
        }

        private void ClearFtsIndex(object o)
        {
            FtsService.ClearLuceneIndex();
            using(var ctx = new DdbContext())
            {
                foreach(var document in ctx.Documents.Where(c => c.Cached))
                {
                    document.Cached = false;
                }

                ctx.SaveChanges();

                StatisticsModel.Instance.DocumentsInCacheCount = 0;
            }

            ApplicationWorkers.DirectoryMonitor.OnNeedUpdate();
        }
    }
}