using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Fluent;

namespace SharpLogger
{
    /// <summary>
    /// The log broker instance used to help configure and boot up new instances of a logging session
    /// </summary>
    public static class SharpLogBroker
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields

        // Private constants holding default configuration values for the basic output path locations
        internal const string _defaultOutputPath = @"C:\\Program Files (x86)\\MEAT Inc\\SharpLogging\\";

        // Private static collection of all current logger instances
        private static List<SharpLogger> _loggerPool = new();

        // Private backing fields for logger states and values
        private static bool _loggingEnabled;                       // Sets if logging is currently enabled or disabled for this log broker instance
        private static LogLevel _minLevel = LogLevel.Off;          // Minimum logging level for this logging session (Defaults to trace for debug)
        private static LogLevel _maxLevel = LogLevel.Off;          // Maximum logging level for this logging session (Defaults to fatal for all sessions)

        // Private backing fields for logging path information and logger instance
        private static string _logFileName;                        // Path to the output log file being used for all logging instances. Shared across all classes
        private static string _logFilePath;                        // The Path to the output log file minus the log file name. Can not be set using the property
        private static string _logBrokerName;                      // Name of the logger instance for this session setup. (Calling application name)

        // NLogger and SharpLogger objects used for logging output
        private static Logger _logBrokerLogger;                    // The main logger object used for logging output. Can only be assigned while null
        private static SharpLogger _masterLogger;                  // The main SharpLogger that wraps the Nlogger built on this instance
        
        #endregion //Fields

        #region Properties
        
        // Public facing properties holding configuration for our logging levels and master enabled value
        public static LogLevel MinLevel
        {
            get => _minLevel;
            internal set => _minLevel = value;
        }
        public static LogLevel MaxLevel
        {
            get => _maxLevel;
            internal set => _maxLevel = value;
        }
        public static bool LoggingEnabled
        {
            get => _loggingEnabled;
            internal set => _loggingEnabled = value;
        }

        // Public facing properties holding configuration for logger session configuration
        public static string LogFileName
        {
            get => _logFileName;
            internal set => _logFileName = value;
        }
        public static string LogFilePath
        {
            get => _logFilePath;
            private set => throw new InvalidOperationException("Error! Can not force specify a log file base path!");
        }
        public static string LogBrokerName
        {
            get => _logBrokerName;
            internal set => _logBrokerName = value;
        }

        // Main logger objects used to configure new child logger instances
        public static Logger LogBrokerLogger
        {
            get => _logBrokerLogger;
            private set
            {
                // Only allow setting this logger instance from outside the broker if the logger is currently null
                if (_logBrokerLogger == null) _logBrokerLogger = value;
                else throw new InvalidOperationException("Error! Can not reset log broker logger after being built!");
            }
        }
        public static SharpLogger MasterLogger
        {
            get => _masterLogger;
            private set
            {
                // Only allow setting this logger instance from outside the broker if the logger is currently null
                if (_masterLogger == null) _masterLogger = value;
                else throw new InvalidOperationException("Error! Can not reset log broker logger after being built!");
            }
        }
        
        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Configures a new instance of a log broker for logging configuration/output for an application
        /// When a name of a file is provided to output file path, the name of the log file is used
        /// </summary>
        /// <param name="InstanceName">Name of the logger session being configured</param>
        /// <param name="OutputFilePath">The FULL path to our desired output log file</param>
        /// <param name="MinLevel">Minimum logging level for our configuration</param>
        /// <param name="MaxLevel">Maximum logging level for our configuration</param>
        public static void InitializeLogging(string InstanceName, string OutputFilePath = null, LogType MinLogLevel = LogType.TraceLog, LogType MaxLogLevel = LogType.FatalLog)
        {
            // Begin by setting our instance name and setting up the min and max logging values
            LogBrokerName = InstanceName;
            if ((int)MinLogLevel == 6 && (int)MaxLogLevel == 6) LoggingEnabled = false;
            else
            {
                MinLevel = (int)MinLogLevel < 0 ? LogLevel.Trace : MinLogLevel.ToNLevel();
                MaxLevel = (int)MaxLogLevel > 6 ? LogLevel.Fatal : MaxLogLevel.ToNLevel();
            }

            // Now find our output log file path/name value and create the logging output file
            if (OutputFilePath == null)
            {
                // Get the documents path folder and store our base output file here
                string LoggerTime = DateTime.Now.ToString("MMddyyy-HHmmss");
                string ExecutingName = Assembly.GetExecutingAssembly()
                    .FullName.Split(' ')
                    .FirstOrDefault();

                // Store our new log file path value and exit out once stored since we now have a logging path
                if (!Directory.Exists(_defaultOutputPath)) Directory.CreateDirectory(_defaultOutputPath);
                LogFileName = Path.Combine(_defaultOutputPath, $"{ExecutingName}_Logging_{LoggerTime}.log");
                LogFilePath = Path.GetDirectoryName(LogFilePath);
            }
            else
            {
                // Try and find the name of the file if one is given
                OutputFilePath = OutputFilePath.Trim();
                bool EndsWithDirChars =
                    OutputFilePath.EndsWith("" + Path.DirectorySeparatorChar) ||
                    OutputFilePath.EndsWith("" + Path.AltDirectorySeparatorChar);

                // Now using the cleaned up path, find out if we've got a folder or a file being passed in
                if (File.Exists(OutputFilePath))
                {
                    // If we found an actual file for the input path, we know that's the final result
                    LogFileName = OutputFilePath;
                    LogFilePath = Path.GetDirectoryName(LogFilePath);
                }
                else if (Directory.Exists(OutputFilePath) || EndsWithDirChars || !Path.HasExtension(OutputFilePath))
                {
                    // Setup a new log file name based on the instance name and the path given
                    string LoggerTime = DateTime.Now.ToString("MMddyyy-HHmmss");
                    LogFileName = Path.Combine(OutputFilePath, $"{LogBrokerName}_Logging_{LoggerTime}.log");
                    LogFilePath = Path.GetDirectoryName(LogFilePath);

                    // Finally, make sure the output log file path exists and move on
                    if (!Directory.Exists(OutputFilePath)) Directory.CreateDirectory(OutputFilePath);
                }
            }

            // Spawn a new SharpLogger which will use our master logger instance to write log output
            string AssyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MasterLogger = new SharpLogger(LoggerActions.FileLogger | LoggerActions.ConsoleLogger);
            MasterLogger.WriteLog("LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!", LogType.WarnLog);
            MasterLogger.WriteLog($"--> TIME OF DLL INIT: {DateTime.Now:g}", LogType.InfoLog);
            MasterLogger.WriteLog($"--> DLL ASSEMBLY VER: {AssyVersion}", LogType.InfoLog);
            MasterLogger.WriteLog($"--> HAPPY LOGGING. LETS HOPE EVERYTHING GOES WELL...", LogType.InfoLog);
        }

        /// <summary>
        /// Gets all loggers that exist currently.
        /// </summary>
        /// <returns></returns>
        public static List<SharpLogger> GetLoggers()
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Get the logger objects and return out 
                return _loggerPool;
            }
        }
        /// <summary>
        /// Gets loggers based on a given type of logger.
        /// </summary>
        /// <param name="TypeOfLogger">Type of logger to get.</param>
        /// <returns>List of all loggers for this type.</returns>
        public static List<SharpLogger> GetLoggers(LoggerActions TypeOfLogger)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find and return the matching logger object instances
                return _loggerPool.Where(LogObj => LogObj.LoggerType == TypeOfLogger).ToList();
            }
        }

        /// <summary>
        /// Adds a logger item to the pool of all loggers.
        /// </summary>
        /// <param name="LoggerItem">Item to add to the pool.</param>
        internal static void RegisterLogger(SharpLogger LoggerItem)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find existing loggers that may have the same name as this logger obj.
                if (_loggerPool.Any(LogObj => LogObj.LoggerGuid == LoggerItem.LoggerGuid))
                {
                    // Update current.
                    int IndexOfExisting = _loggerPool.IndexOf(LoggerItem);
                    _loggerPool[IndexOfExisting] = LoggerItem;
                    return;
                }

                // If the logger didn't get added (no dupes) do it not.
                _loggerPool.Add(LoggerItem);
            }
        }
        /// <summary>
        /// Removes the logger passed from the logger queue
        /// </summary>
        /// <param name="LoggerItem">Logger to yank</param>
        internal static void DestroyLogger(SharpLogger LoggerItem)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Pull out all the dupes.
                var NewLoggers = _loggerPool.Where(LogObj =>
                    LogObj.LoggerGuid != LoggerItem.LoggerGuid).ToList();

                // Check if new logger is in loggers filtered or not and store it.
                if (NewLoggers.Contains(LoggerItem)) NewLoggers.Remove(LoggerItem);
                _loggerPool = NewLoggers;
            }
        }
    }
}
