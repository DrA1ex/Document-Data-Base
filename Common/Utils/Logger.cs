using NLog;

namespace Common.Utils
{
    public class Logger
    {
        private Logger()
        {
        }

        static Logger()
        {
            Instance = LogManager.GetLogger(typeof(Logger).Name);
        }

        public static NLog.Logger Instance { get; private set; }
    }
}