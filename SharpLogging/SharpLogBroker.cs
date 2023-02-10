using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace SharpLogging
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
        private static DateTime _brokerCreated;                         // The time the broker instance was built
        private static bool _logBrokerInitialized;                      // Sets if the log broker has been built or not at this point
        private static BrokerConfiguration _logBrokerConfig;            // Passed in configuration values used to setup this log broker instance
        private static List<SharpLogger> _loggerPool = new();           // The collection of all built loggers in this instance

        // Private backing fields for logger states and values
        private static bool _loggingEnabled = true;                     // Sets if logging is currently enabled or disabled for this log broker instance
        private static LogLevel _minLevel = LogLevel.Off;               // Minimum logging level for this logging session (Defaults to trace for debug)
        private static LogLevel _maxLevel = LogLevel.Off;               // Maximum logging level for this logging session (Defaults to fatal for all sessions)

        // Private backing fields for logging path information and logger instance
        private static string _logFileName;                             // Path to the output log file being used for all logging instances. Shared across all classes
        private static string _logFilePath;                             // The Path to the output log file minus the log file name. Can not be set using the property
        private static string _logBrokerName;                           // Name of the logger instance for this session setup. (Calling application name)
        
        // Default format objects for writing output to our targets 
        private static SharpFileTargetFormat _defaultFileFormat;        // Default format for a file target. Contains the output format string and configuration values
        private static SharpConsoleTargetFormat _defaultConsoleFormat;  // Default format for a console target. Contains the output format string and configuration values

        // NLogger and SharpLogger objects used for logging output
        private static SharpLogger _masterLogger;                       // The main SharpLogger that is used to write our output content to a log file or targets

        #endregion //Fields

        #region Properties

        // Default log broker and archiver configurations and overall state of the broker
        public static bool LogBrokerInitialized
        {
            get => _logBrokerInitialized;
            private set => _logBrokerInitialized = value;
        }
        public static BrokerConfiguration LogBrokerConfig
        {
            get => _logBrokerConfig;
            internal set => _logBrokerConfig = value;
        }

        // Public facing properties holding configuration for our logging levels and master enabled value
        public static LogType MinLevel
        {
            get => _minLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                _minLevel = ConvertedLevel;
            }
        }
        public static LogType MaxLevel
        {
            get => _maxLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                _maxLevel = ConvertedLevel;
            }
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
            private set => _logFilePath = value;
        }
        public static string LogBrokerName
        {
            get => _logBrokerName;
            internal set => _logBrokerName = value;
        }

        // Main logger objects used to configure new child logger instances and the public logger pool
        public static SharpLogger MasterLogger
        {
            get => _masterLogger;
            private set
            {
                // Remove the master logger from our queue first
                _masterLogger?.Dispose();
                _masterLogger = value;
            }
        }
        public static SharpLogger[] LoggerPool
        {
            get
            {
                // Ensure the list of loggers exists and convert it to an array
                _loggerPool ??= new List<SharpLogger>();
                lock (_loggerPool) return _loggerPool.ToArray();
            }
        }

        // Logger targets and rules for this log broker instance
        public static Target[] LoggingTargets
        {
            get
            {
                // Ensure the list of targets exists and convert it to an array
                lock (_loggerPool) return _loggerPool.SelectMany(LoggerObj => LoggerObj.LoggerTargets).ToArray();
            }
        }
        public static LoggingRule[] LoggingRules
        {
            get
            {
                // Ensure the list of rules exists and convert it to an array
                lock (_loggerPool) return _loggerPool.SelectMany(LoggerObj => LoggerObj.LoggerRules).ToArray();
            }
        }

        // Public facing constant logger configuration objects for formatting output strings
        public static SharpFileTargetFormat DefaultFileFormat
        {
            get => _defaultFileFormat ??= new SharpFileTargetFormat(); 
            set => _defaultFileFormat = value;
        }
        public static SharpConsoleTargetFormat DefaultConsoleFormat
        {
            get => _defaultConsoleFormat ??= new SharpConsoleTargetFormat();
            set => _defaultConsoleFormat = value;
        }

        #endregion //Properties

        #region Structs and Classes

        /// <summary>
        /// Structure used to help configure a new instance of a sharp logger session
        /// </summary>
        public struct BrokerConfiguration
        {
            #region Custom Events
            #endregion //Custom Events

            #region Fields

            // Public facing field for the logging location and session name (Name defaults to the calling EXE name)
            [DefaultValue("SharpLogging")] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string LogBrokerName = "SharpLogging";
            [DefaultValue(_defaultOutputPath)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string LogFilePath = _defaultOutputPath;
            [DefaultValue("")] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string LogFileName = $"SharpLogging_{DateTime.Now.ToString("MMddyyy-HHmmss")}.log";

            // Public facing fields for logging configuration/values
            [JsonIgnore] public LogType MinLogLevel = LogType.DebugLog;
            [JsonIgnore] public LogType MaxLogLevel = LogType.FatalLog;

            #endregion //Fields

            #region Properties

            // Private properties used to help build JSON configuration objects for logging levels
            [JsonProperty("MinLogLevel", DefaultValueHandling = DefaultValueHandling.Populate)]
            private string _minLogLevel
            {
                get => MinLogLevel.ToString();
                set => MinLogLevel = (LogType)Enum.Parse(typeof(LogType), value);
            }
            [JsonProperty("MaxLogLevel", DefaultValueHandling = DefaultValueHandling.Populate)]
            private string _maxLogLevel
            {
                get => MaxLogLevel.ToString();
                set => MaxLogLevel = (LogType)Enum.Parse(typeof(LogType), value);
            }

            #endregion //Properties

            #region Structs and Classes
            #endregion //Structs and Classes

            // --------------------------------------------------------------------------------------------------------------------------------------

            /// <summary>
            /// Builds a new default configuration for a Log broker setup structure
            /// </summary>
            public BrokerConfiguration() { }
        }

        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Overrides the ToString call on a log broker instance to write out information about it
        /// </summary>
        public new static string ToString()
        {
            // Build the output string to return based on properties
            string OutputString =
                $"Log Broker Information - '{LogBrokerName}' - Version {Assembly.GetExecutingAssembly().GetName().Version}\n" +
                $"\t\\__ Broker Status:  {(_logBrokerInitialized ? "Log Broker Ready!" : "Not Configured!")}\n" +
                $"\t\\__ Creation Time:  {_brokerCreated:g}\n" +
                $"\t\\__ Logging State:  {(LoggingEnabled ? "Logging Currently ON" : "Logging Currently OFF")}\n" +
                $"\t\\__ Min Log Level:  {MinLevel} (NLevel: {_minLevel})\n" +
                $"\t\\__ Max Log Level:  {MaxLevel} (NLevel: {_maxLevel})\n" +
                $"\t\\__ Log File Name:  {LogFileName}\n" +
                $"\t\\__ Log File Path:  {LogFilePath}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Loggers Built:  {LoggerPool.Length} Logger{(LoggerPool.Length != 1 ? "s": string.Empty)} Constructed\n" +
                $"\t\\__ Master Logger:  {(MasterLogger == null ? "No Master Built" : MasterLogger.LoggerName)}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Broker Config (JSON):  {JsonConvert.SerializeObject(LogBrokerConfig)}";

            // Return this built output string here
            return OutputString;
        }
        /// <summary>
        /// Configures a new instance of a log broker for logging configuration/output for an application
        /// When a name of a file is provided to output file path, the name of the log file is used
        /// </summary>
        /// <param name="BrokerConfig">The broker configuration to use for building a new logging session</param>
        /// <param name="ArchiveConfig">An optional archive configuration object we use to setup default archive configurations</param>
        public static bool InitializeLogging(BrokerConfiguration BrokerConfig)
        {
            // Start by storing new configuration values for the log broker and archiver configurations
            _brokerCreated = DateTime.Now;
            LogBrokerConfig = BrokerConfig;
            LogBrokerName = string.IsNullOrWhiteSpace(BrokerConfig.LogBrokerName)
                ? Assembly.GetExecutingAssembly()
                    .FullName.Split(' ').FirstOrDefault()
                    ?.Replace(",", string.Empty).Trim()
                : BrokerConfig.LogBrokerName;

            // Check our logging level values provided in our configurations and see what needs to be updated
            if ((int)BrokerConfig.MinLogLevel == 6 && (int)BrokerConfig.MaxLogLevel == 6) LoggingEnabled = false;
            _minLevel = (int)BrokerConfig.MinLogLevel < 0 ? LogLevel.Trace : BrokerConfig.MinLogLevel.ToNLevel();
            _maxLevel = (int)BrokerConfig.MaxLogLevel > 6 ? LogLevel.Fatal : BrokerConfig.MaxLogLevel.ToNLevel();

            // Now find our output log file path/name value and create the logging output file
            if (string.IsNullOrWhiteSpace(BrokerConfig.LogFilePath))
            {
                // Store our new log file path value and exit out once stored since we now have a logging path
                string LoggerTime = DateTime.Now.ToString("MMddyyy-HHmmss");
                LogFileName = string.IsNullOrWhiteSpace(BrokerConfig.LogFileName)
                    ?  $"{LogBrokerName}_Logging_{LoggerTime}.log"
                    : $"{Path.GetFileNameWithoutExtension(BrokerConfig.LogFileName)}_{LoggerTime}{Path.GetExtension(BrokerConfig.LogFileName)}";
                
                // Build the full path based on the default output location and the log file name pulled in
                LogFilePath = Path.Combine(_defaultOutputPath, LogFileName).Replace("\\\\", "\\").Trim();
                if (!Directory.Exists(_defaultOutputPath)) Directory.CreateDirectory(_defaultOutputPath);
            }
            else
            {
                // Try and find the name of the file if one is given
                BrokerConfig.LogFilePath = BrokerConfig.LogFilePath.Trim();
                bool EndsWithDirChars =
                    BrokerConfig.LogFilePath.EndsWith("" + Path.DirectorySeparatorChar) ||
                    BrokerConfig.LogFilePath.EndsWith("" + Path.AltDirectorySeparatorChar);

                // Now using the cleaned up path, find out if we've got a folder or a file being passed in
                if (File.Exists(BrokerConfig.LogFilePath))
                {
                    // If we found an actual file for the input path, we know that's the final result
                    LogFileName = Path.GetFileName(BrokerConfig.LogFilePath);
                    LogFilePath = BrokerConfig.LogFilePath.Trim();
                }
                else if ((Directory.Exists(BrokerConfig.LogFilePath) || EndsWithDirChars) && !Path.HasExtension(BrokerConfig.LogFilePath))
                {
                    // Setup a new log file name based on the instance name and the path given
                    string LoggerTime = DateTime.Now.ToString("MMddyyy-HHmmss");
                    LogFileName = string.IsNullOrWhiteSpace(BrokerConfig.LogFileName)
                        ? $"{LogBrokerName}_Logging_{LoggerTime}.log"
                        : $"{Path.GetFileNameWithoutExtension(BrokerConfig.LogFileName)}_{LoggerTime}{Path.GetExtension(BrokerConfig.LogFileName)}";

                    // Build the full path based on the default output location and the log file name pulled in
                    LogFilePath = Path.Combine(BrokerConfig.LogFilePath, LogFileName);
                }
                else if (Path.HasExtension(BrokerConfig.LogFilePath) && !EndsWithDirChars)
                {
                    // If the path provided is an actual file name that isn't real, build a path for it
                    LogFileName = Path.GetFileName(BrokerConfig.LogFilePath);
                    LogFilePath = BrokerConfig.LogFilePath.Trim();
                }
                else
                {
                    // Throw a new failure here if we got to this point somehow
                    string ArgNameAndValue = $"{nameof(BrokerConfig.LogFilePath)} -- {BrokerConfig.LogFilePath}";
                    throw new ArgumentException($"Error! Broker configuration could not be used to setup logging! ({ArgNameAndValue})");
                }
            }

            // Clean up double slash values in our file and path names.
            LogFileName = LogFileName.Replace("\\\\", "\\").Trim();
            LogFilePath = LogFilePath.Replace("\\\\", "\\").Trim();

            // Using the newly build path and file name values, setup an output folder if it needs to be built
            string OutputFolder = LogFilePath.Replace(LogFileName, string.Empty).Trim();
            if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);

            // Update our configuration to reflect the newly determined values for files
            _logBrokerConfig.MaxLogLevel = MaxLevel;
            _logBrokerConfig.MinLogLevel = MinLevel;
            _logBrokerConfig.LogFileName = LogFileName;
            _logBrokerConfig.LogFilePath = LogFilePath;
            _logBrokerConfig.LogBrokerName = LogBrokerName;

            // Lock our logger pool before accessing it for removal routines
            lock (_loggerPool)
            {
                // Wipe out the lists of old logger instances and targets here by disposing them all
                for (int LoggerIndex = 0; LoggerIndex < _loggerPool.Count - 1; LoggerIndex++)
                    _loggerPool[LoggerIndex].Dispose();
            }

            // Spawn a new SharpLogger which will use our master logger instance to write log output
            LogManager.Configuration = new LoggingConfiguration();
            MasterLogger = new SharpLogger(LoggerActions.UniversalLogger,"LogBrokerLogger");
            MasterLogger.WriteLog("LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!", LogType.WarnLog);
            MasterLogger.WriteLog($"SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!", LogType.InfoLog);
            MasterLogger.WriteLog(SharpLogBroker.ToString(), LogType.TraceLog);

            // Return true once this broker instance booted up and ready to run
            _logBrokerInitialized = true;
            return _logBrokerInitialized;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets loggers based on a given type of logger.
        /// </summary>
        /// <param name="TypeOfLogger">Type of logger to get.</param>
        /// <returns>List of all loggers for this type.</returns>
        public static IEnumerable<SharpLogger> FindLoggers(LoggerActions TypeOfLogger)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find and return the matching logger object instances
                return _loggerPool.Where(LogObj => LogObj.LoggerType == TypeOfLogger);
            }
        }
        /// <summary>
        /// Gets loggers based on a given logger name value
        /// </summary>
        /// <param name="LoggerName">Name of loggers to get.</param>
        /// <param name="UseRegex">When true, the logger name is used as a regex pattern</param>
        /// <returns>List of all loggers for this type.</returns>
        public static IEnumerable<SharpLogger> FindLoggers(string LoggerName, bool UseRegex = false)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find and return the matching logger object instances
                return !UseRegex 
                    ? _loggerPool.Where(LogObj => LogObj.LoggerName.Contains(LoggerName)) 
                    : _loggerPool.Where(LogObj => Regex.Match(LogObj.LoggerName, LoggerName).Success);
            }
        }
        /// <summary>
        /// Gets loggers based on a given search expression
        /// </summary>
        /// <param name="SearchExpression">The lambda expression used to find loggers</param>
        /// <returns>List of all loggers for this type.</returns>
        public static IEnumerable<SharpLogger> FindLoggers(Expression<Func<SharpLogger, bool>> SearchExpression)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find and return the matching logger object instances based on the search
                return _loggerPool
                    .Where(SearchExpression.Compile())
                    .OrderBy(LoggerObj => LoggerObj.LoggerType)
                    .ThenBy(LoggerObj => LoggerObj.LoggerGuid);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Adds a logger item to the pool of all loggers.
        /// </summary>
        /// <param name="LoggerItem">Item to add to the pool.</param>
        /// <returns>True if the logger is registered. False if it's replaced</returns>
        internal static bool RegisterLogger(SharpLogger LoggerItem)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find existing loggers that may have the same name as this logger obj.
                var MatchedLogger = _loggerPool.FirstOrDefault(LogObj =>
                {
                    // Compare the names and the GUID values here
                    bool HasName = LogObj.LoggerName == LoggerItem.LoggerName;
                    bool HasGuid = LogObj.LoggerGuid == LoggerItem.LoggerGuid;
                    return HasName || HasGuid;
                });

                // If we don't have an existing one, add it into our pool here
                if (MatchedLogger == null) _loggerPool.Add(LoggerItem);
                else
                {
                    // Update a logger where this GUID existed already.
                    int IndexOfExisting = _loggerPool.IndexOf(MatchedLogger);
                    _loggerPool[IndexOfExisting] = LoggerItem;
                }

                // Return out based on if the logger is found in our pool or not
                return _loggerPool.Contains(LoggerItem);
            }
        }
        /// <summary>
        /// Removes the logger passed from the logger queue
        /// </summary>
        /// <param name="LoggerItem">Logger to yank</param>
        /// <param name="DisposeLogger">When true, the logger instance will be disposed</param>
        /// <returns>True if the logger is removed. False if it is not</returns>
        internal static bool DestroyLogger(SharpLogger LoggerItem)
        {
            // Remove the logger instance from the pool and then remove all rules and targets
            lock (_loggerPool)
            {
                // If we wanted to dispose the logger, only do so if we need to wipe targets or rules
                bool DisposeLogger = LoggerItem.LoggerRules.Length > 0 || LoggerItem.LoggerTargets.Length > 0;
                if (DisposeLogger)
                {
                    // If we need to dispose the logger, do so first and let the dispose routine call this method again
                    LoggerItem.Dispose();
                    return true;
                }

                // Remove any loggers with a matching GUID or name value
                _loggerPool.Remove(LoggerItem);
                _loggerPool.RemoveAll(LoggerObj => LoggerObj.LoggerName == LoggerItem.LoggerName);
                _loggerPool.RemoveAll(LoggerObj => LoggerObj.LoggerGuid == LoggerItem.LoggerGuid);

                // After removing the logger instance, look for any matching ones again
                bool HasLogger = _loggerPool.Any(LogObj =>
                {
                    // Compare the names and the GUID values here
                    bool HasName = LogObj.LoggerName == LoggerItem.LoggerName;
                    bool HasGuid = LogObj.LoggerGuid == LoggerItem.LoggerGuid;
                    return HasName || HasGuid;
                });
                
                // Return out based on if any loggers are located or not (Should have none)
                return !HasLogger;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Converts a LogType into a LogLevel for NLOG use.
        /// </summary>
        /// <param name="Level"></param>
        /// <returns>LogLevel Pulled out of here.</returns>
        internal static LogLevel ToNLevel(this LogType Level)
        {
            // Find if the ordinal value is out of range or not. Default to the lowest level supported
            return (int)Level > 6
                ? MinLevel.ToNLevel()
                : LogLevel.FromOrdinal((int)Level);
        }
        /// <summary>
        /// Converts a given NLogLevel into a LogType
        /// </summary>
        /// <param name="Level">Level to check</param>
        /// <returns>Gives back a default log type.</returns>
        internal static LogType ToLogType(this LogLevel Level)
        {
            // Find if the ordinal value is out of range or not. Default to the lowest level supported
            return Level.Ordinal > 6
                ? MinLevel
                : (LogType)Level.Ordinal;
        }
    }
}