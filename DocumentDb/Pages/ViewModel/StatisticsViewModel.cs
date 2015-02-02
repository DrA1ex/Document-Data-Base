using DataLayer.Model;
using DataLayer.Parser;
using DocumentDb.Common;
using FirstFloor.ModernUI.Presentation;

namespace DocumentDb.Pages.ViewModel
{
    public class StatisticsViewModel : NotifyPropertyChanged
    {
        private DocumentMonitorState _documentMonitorState;
        private DocumentParserState _documentParserState;

        public StatisticsViewModel()
        {
            StatisticsModel.PropertyChanged += (sender, args) => { OnPropertyChanged("StatisticsModel"); };

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
                                                                     }
                                                                 };
        }

        public StatisticsModel StatisticsModel
        {
            get { return StatisticsModel.Instance; }
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
    }
}