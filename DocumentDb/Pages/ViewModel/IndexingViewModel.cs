using System.Windows.Input;
using Common;
using DataLayer;
using DataLayer.Parser;
using DocumentDb.Common;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.ViewModel
{
    public class IndexingViewModel : NotifyPropertyChanged
    {
        private DocumentMonitorState _documentMonitorState;
        private DocumentParserState _documentParserState;
        private ICommand _optimizeFtsIndexCommand;
        private RelayCommand _pauseParserCommand;
        private RelayCommand _startParserCommand;
        private RelayCommand _stopParserCommand;
        private ICommand _updateIndexCommand;

        public IndexingViewModel()
        {
            DocumentMonitorState = ApplicationWorkers.DirectoryMonitor.State;
            ApplicationWorkers.DirectoryMonitor.PropertyChanged += (sender, args) =>
                                                                   {
                                                                       if(args.PropertyName == "State")
                                                                       {
                                                                           DocumentMonitorState = ApplicationWorkers.DirectoryMonitor.State;
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
                return _stopParserCommand ?? (_stopParserCommand = new RelayCommand(StopParser, o => DocumentParserState == DocumentParserState.Running ||
                                                                                                     DocumentParserState == DocumentParserState.Iddle));
            }
        }

        public RelayCommand StartParserCommand
        {
            get { return _startParserCommand ?? (_startParserCommand = new RelayCommand(StartParser, o => DocumentParserState == DocumentParserState.Stopped || DocumentParserState == DocumentParserState.Paused)); }
        }

        public RelayCommand PauseParserCommand
        {
            get
            {
                return _pauseParserCommand ?? (_pauseParserCommand = new RelayCommand(PauseParser, o => DocumentParserState == DocumentParserState.Running ||
                                                                                                        DocumentParserState == DocumentParserState.Iddle));
            }
        }

        public ICommand UpdateIndexCommand
        {
            get { return _updateIndexCommand ?? (_updateIndexCommand = new DelegateCommand(UpdateIndex)); }
        }

        public ICommand OptimizeFtsIndexCommand
        {
            get { return _optimizeFtsIndexCommand ?? (_optimizeFtsIndexCommand = new DelegateCommand(OptimizeFtsIndex)); }
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

        private void UpdateIndex()
        {
            ApplicationWorkers.DirectoryMonitor.Update();
        }

        private void OptimizeFtsIndex()
        {
            FtsService.Optimize();
        }
    }
}