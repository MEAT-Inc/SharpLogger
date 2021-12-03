using System.IO;
using System.Reflection;
using SharpLogger.LoggerSupport;

namespace SharpLogger
{
    /// <summary>
    /// Class used to configure new Fulcrum logging configurations
    /// </summary>
    public class LoggingSetup
    {
        // Class values for the name and logging path fo this application
        public readonly string AppName;
        public readonly string LoggingPath;

        // ------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new fulcrum logging setup class routine.
        /// </summary>
        /// <param name="AppName"></param>
        /// <param name="LoggingPath"></param>
        public LoggingSetup(string AppName, string LoggingPath)
        {
            // Store class values here.
            this.AppName = AppName;
            this.LoggingPath = LoggingPath;
        }


        /// <summary>
        /// Configure new logging instance setup for configurations.
        /// </summary>
        public void ConfigureLogging()
        {
            // Make logger and build global logger object.
            LogBroker.ConfigureLoggingSession(this.AppName, this.LoggingPath);
            LogBroker.BrokerInstance.FillBrokerPool();

            // Log information and current application version.
            string CurrentAppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LogBroker.Logger?.WriteLog($"LOGGING FOR {AppName} HAS BEEN STARTED OK!", LogType.WarnLog);
            LogBroker.Logger?.WriteLog($"{AppName} APPLICATION IS NOW LIVE! VERSION: {CurrentAppVersion}", LogType.WarnLog);
        }
        /// <summary>
        /// Configures logging cleanup to archives if needed.
        /// </summary>
        public void ConfigureLogCleanup(dynamic ConfigObj)
        {
            // Pull values for log archive trigger and set values
            LogBroker.Logger?.WriteLog($"CLEANUP ARCHIVE FILE SETUP STARTED! CHECKING FOR {ConfigObj.ArchiveOnFileCount} OR MORE LOG FILES...");
            if (Directory.GetFiles(LogBroker.BaseOutputPath).Length < (int)ConfigObj.ArchiveOnFileCount)
            {
                // Log not cleaning up and return.
                LogBroker.Logger?.WriteLog("NO NEED TO ARCHIVE FILES AT THIS TIME! MOVING ON", LogType.WarnLog);
                return;
            }

            // Begin archive process 
            LogBroker.Logger?.WriteLog($"ARCHIVE PROCESS IS NEEDED! PATH TO STORE FILES IS SET TO {ConfigObj.LogArchivePath}");
            LogBroker.Logger?.WriteLog($"SETTING UP SETS OF {ConfigObj.ArchiveFileSetSize} FILES IN EACH ARCHIVE OBJECT!");

            // Run cleanup for the main app files and the DLL Log files
            LogBroker.CleanupLogHistory(ConfigObj.ToString());
            LogBroker.CleanupLogHistory(ConfigObj.ToString(), "FulcrumShim");

            // Log done.
            LogBroker.Logger?.WriteLog($"DONE CLEANING UP LOG FILES! CHECK {ConfigObj.LogArchivePath} FOR NEWLY BUILT ARCHIVE FILES", LogType.InfoLog);
        }
    }
}
