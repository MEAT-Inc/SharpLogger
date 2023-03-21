using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Layouts;
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

        // Private backing fields for logger states and values
        private static bool _logBrokerInitialized;                          // Sets if the log broker has been built or not at this point
        private static bool _loggingEnabled = true;                         // Sets if logging is currently enabled or disabled for this log broker instance
        private static LogLevel _minLevel = LogLevel.Off;                   // Minimum logging level for this logging session (Defaults to trace for debug)
        private static LogLevel _maxLevel = LogLevel.Off;                   // Maximum logging level for this logging session (Defaults to fatal for all sessions)
                                                                         
        // Private static collection of all current logger instances     
        private static DateTime _brokerCreated;                             // The time the broker instance was built
        private static SharpLogger _masterLogger;                           // The main SharpLogger that is used to write our output content to a log file or targets
        private static BrokerConfiguration _logBrokerConfig;                // Passed in configuration values used to setup this log broker instance
        private static List<SharpLogger> _loggerPool = new();               // The collection of all built loggers in this instance
        
        // Private backing fields for logging path information and logger instance
        private static string _logFileName;                                 // Path to the output log file being used for all logging instances. Shared across all classes
        private static string _logFilePath;                                 // The Path to the output log file minus the log file name. Can not be set using the property
        private static string _logFileFolder;                               // The path to the folder holding all output logs for this session (Base of the file path)
        private static string _logBrokerName;                               // Name of the logger instance for this session setup. (Calling application name)
        
        // Default format objects for writing output to our targets     
        private static SharpFileTargetFormat _defaultFileFormat;            // Default format for a file target. Contains the output format string and configuration values
        private static SharpConsoleTargetFormat _defaultConsoleFormat;      // Default format for a console target. Contains the output format string and configuration values

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
            get => _logFileName ??= Path.GetFileName(Path.GetTempFileName());
            internal set => _logFileName = value;
        }
        public static string LogFilePath
        {
            get => _logFilePath ?? Path.Combine(LogFileFolder, LogFileName);
            private set => _logFilePath = value;
        }
        public static string LogFileFolder
        {
            get => _logFileFolder ??= Path.GetTempPath();
            private set => _logFileFolder = value;
        }
        public static string LogBrokerName
        {
            get => _logBrokerName ?? "SharpLogBroker";
            internal set => _logBrokerName = value;
        }

        // Public facing properties holding configuration for child directories and files
        public static string[] LoggingSubfolders => Directory.GetDirectories(LogFileFolder);
        public static Tuple<string, IEnumerable<string>>[] LoggingSubfolderFiles =>
            LoggingSubfolders.Select(ChildFolder =>
                new Tuple<string, IEnumerable<string>>(ChildFolder, Directory.GetFiles(ChildFolder))).ToArray();

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

        // Logger targets and rules for this log broker instance along with logged exceptions
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
        public static Exception[] LoggedExceptions
        {
            get
            {
                // Ensure the list of exceptions exists and convert it to an array
                lock (_loggerPool) return _loggerPool.SelectMany(LoggerObj => LoggerObj.LoggedExceptions).ToArray();
            }
        }

        // Public facing constant logger configuration objects for formatting output strings
        public static SharpFileTargetFormat DefaultFileFormat
        {
            get => _defaultFileFormat ??= new SharpFileTargetFormat();
            set
            {
                // Configure the layout for our new master logger instance if needed
                _defaultFileFormat = value;
                if (MasterFileTarget == null || MasterLogger == null) return;

                // Reconfigure the target instances for our logging output now 
                MasterFileTarget.Layout = new SimpleLayout(_defaultFileFormat.LoggerFormatString);
                LogManager.ReconfigExistingLoggers();
            }
        }
        public static SharpConsoleTargetFormat DefaultConsoleFormat
        {
            get => _defaultConsoleFormat ??= new SharpConsoleTargetFormat();
            set
            {
                // Configure the layout for our new master logger instance if needed
                _defaultConsoleFormat = value;
                if (MasterConsoleTarget == null || MasterLogger == null) return;

                // Reconfigure the target instances for our logging output now 
                MasterConsoleTarget.Layout = new SimpleLayout(_defaultConsoleFormat.LoggerFormatString);
                LogManager.ReconfigExistingLoggers();
            }
        }

        // Internal main logging targets for the master logging output target locations
        internal static FileTarget MasterFileTarget { get; private set; }
        internal static ColoredConsoleTarget MasterConsoleTarget { get; private set; }

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
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string LogFilePath = AppDomain.CurrentDomain.BaseDirectory;
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

            // Public facing property to tell us if we've got a default configuration or not
            [JsonIgnore]
            public bool IsDefault =>
                this.LogBrokerName == null && this.LogFileName == null && this.LogFilePath == null &&
                this.MinLogLevel == LogType.NoLogging && this.MaxLogLevel == LogType.NoLogging;

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
            // Make sure the log broker is built before doing this 
            if (!LogBrokerInitialized)
                throw new InvalidOperationException("Error! Please configure the SharpLogBroker before using archives!");

            // Using all the build child folder objects, convert them into a string of values now
            var LoggingFoldersFound = LoggingSubfolderFiles;
            string ChildFolderInfos = LoggingFoldersFound.Length == 0
                ? "No Child Directories"
                : "\n" + string.Join("\n", LoggingFoldersFound
                    .Select(FolderInfo =>
                    {
                        // Build our information string and return it out here
                        int FileCount = FolderInfo.Item2.Count();
                        return FolderInfo.Item1 + " - " + (FileCount == 0
                            ? "No Files Found"
                            : $"{FileCount} File{(FileCount == 1 ? string.Empty : "s")}");
                    }).Select(FolderInfoString => $"\t\t\\__ {FolderInfoString}"));

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
                $"\t\\__ Child Folders:  {ChildFolderInfos}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Loggers Built:  {LoggerPool.Length} Logger{(LoggerPool.Length != 1 ? "s" : string.Empty)} Constructed\n" +
                $"\t\\__ Master Logger:  {(MasterLogger == null ? "No Master Built" : MasterLogger.LoggerName)}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Targets Built:  {LoggingTargets.Length} Logging Target{(LoggingTargets.Length != 1 ? "s" : string.Empty)} Constructed\n" +
                $"\t\\__ Rules Defined:  {LoggingRules.Length} Logging Rule{(LoggingRules.Length != 1 ? "s" : string.Empty)} Defined\n" +
                $"\t\\__ Logged Errors:  {LoggedExceptions.Length} Exception{(LoggingRules.Length != 1 ? "s" : string.Empty)} Logged\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Broker Config (JSON):\n\t\t{JsonConvert.SerializeObject(LogBrokerConfig, Formatting.Indented).Replace("\n", "\n\t\t")}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n";

            // Return this built output string here
            return OutputString;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Internal configuration method used to build and apply a no logging/default configuration
        /// </summary>
        /// <returns>True if logging is setup, false if it's not</returns>
        internal static void InitializeLogging()
        {
            // Build our default configuration for logging and exit out
            LogBrokerConfig = new BrokerConfiguration()
            {
                LogFilePath = null,                       // Output file path location
                LogFileName = null,                       // Name of the log file to write
                LogBrokerName = null,                     // Name of the log broker session      
                MinLogLevel = LogType.NoLogging,          // The lowest level of logging (Off for this instance)
                MaxLogLevel = LogType.NoLogging,          // The highest level of logging (Off for this instance)
            };

            // Turn of logging output routines and store default log levels
            LoggingEnabled = false;
            _maxLevel = LogLevel.Off;
            _minLevel = LogLevel.Off;
        }
        /// <summary>
        /// Configures a new instance of a log broker for logging configuration/output for an application
        /// When a name of a file is provided to output file path, the name of the log file is used
        /// </summary>
        /// <param name="BrokerConfig">The broker configuration to use for building a new logging session</param>
        public static bool InitializeLogging(BrokerConfiguration BrokerConfig)
        {
            // If this broker configuration is the default value for logging off, then just apply those values
            if (BrokerConfig.IsDefault)
            {
                // Initialize logging for the default configuration and exit out 
                InitializeLogging();
                return false;
            }

            // Start by storing new configuration values for the log broker and archiver configurations
            _brokerCreated = DateTime.Now;
            LogBrokerConfig = BrokerConfig;
            LogBrokerName = string.IsNullOrWhiteSpace(BrokerConfig.LogBrokerName)
                ? AppDomain.CurrentDomain.FriendlyName
                    .Split(Path.DirectorySeparatorChar).Last()
                    .Split('.')[0].Replace(" ", string.Empty)
                : BrokerConfig.LogBrokerName;

            // Check our logging level values provided in our configurations and see what needs to be updated
            if ((int)BrokerConfig.MinLogLevel == 6 && (int)BrokerConfig.MaxLogLevel == 6) LoggingEnabled = false;
            _minLevel = (int)BrokerConfig.MinLogLevel < 0 ? LogLevel.Trace : BrokerConfig.MinLogLevel.ToNLevel();
            _maxLevel = (int)BrokerConfig.MaxLogLevel > 6 ? LogLevel.Fatal : BrokerConfig.MaxLogLevel.ToNLevel();

            // Now find our output log file path/name value and create the logging output file
            string LoggerTime = _brokerCreated.ToString("MMddyyy-HHmmss");
            if (string.IsNullOrWhiteSpace(BrokerConfig.LogFilePath))
            {
                // Store our new log file path value and exit out once stored since we now have a logging path
                LogFileName = string.IsNullOrWhiteSpace(BrokerConfig.LogFileName)
                    ?  $"{LogBrokerName}_Logging_{LoggerTime}.log"
                    : $"{Path.GetFileNameWithoutExtension(BrokerConfig.LogFileName).Replace($"$LOGGER_TIME", LoggerTime)}{Path.GetExtension(BrokerConfig.LogFileName)}";
                
                // Build the full path based on the path to our calling executable
                LogFilePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName));
            }
            else
            {
                // Try and find the name of the file if one is given
                BrokerConfig.LogFilePath = Path.GetFullPath(BrokerConfig.LogFilePath);
                bool PathEndsWithDirChars =
                    BrokerConfig.LogFilePath.EndsWith("" + Path.DirectorySeparatorChar) ||
                    BrokerConfig.LogFilePath.EndsWith("" + Path.AltDirectorySeparatorChar);

                // Now using the cleaned up path, find out if we've got a folder or a file being passed in
                if (File.Exists(BrokerConfig.LogFilePath) || Path.HasExtension(BrokerConfig.LogFilePath))
                {
                    // If we found an actual file for the input path, we know that's the final result
                    LogFileName = Path.GetFileName(BrokerConfig.LogFilePath);
                    LogFilePath = Path.GetFullPath(BrokerConfig.LogFilePath.Trim());
                }
                else if (Directory.Exists(BrokerConfig.LogFilePath) || PathEndsWithDirChars || !Path.HasExtension(BrokerConfig.LogFilePath))
                {
                    // Setup a new log file name based on the instance name and the path given
                    LogFileName = string.IsNullOrWhiteSpace(BrokerConfig.LogFileName)
                        ? $"{LogBrokerName}_Logging_{LoggerTime}.log"
                        : $"{Path.GetFileNameWithoutExtension(BrokerConfig.LogFileName).Replace($"$LOGGER_TIME", LoggerTime)}{Path.GetExtension(BrokerConfig.LogFileName)}";
                    
                    // Build the full path based on the default output location and the log file name pulled in
                    LogFilePath = Path.GetFullPath(Path.Combine(BrokerConfig.LogFilePath, LogFileName));
                }
                else
                {
                    // Throw a new failure here if we got to this point somehow. This means we didn't have a legal path value for the configuration
                    string ArgNameAndValue = $"{nameof(BrokerConfig.LogFilePath)} -- {BrokerConfig.LogFilePath}";
                    throw new ArgumentException($"Error! Broker configuration could not be used to setup logging! ({ArgNameAndValue})");
                }
            }
            
            // Using the newly build path and file name values, setup an output folder if it needs to be built
            LogFileFolder = Path.GetFullPath(LogFilePath.Replace(LogFileName, string.Empty));
            if (!Directory.Exists(LogFileFolder)) Directory.CreateDirectory(LogFileFolder);

            // Update our configuration to reflect the newly determined values for files
            _logBrokerInitialized = true;
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

            // Reconfigure our master file and console target objects for the newly set broker configuration
            MasterFileTarget = new FileTarget($"Master_{LogBrokerName}_FileTarget")
            {
                // Define basic configuration and store the desired layout on it
                KeepFileOpen = false,
                FileName = LogFilePath,
                ConcurrentWrites = true,
                Layout = new SimpleLayout(DefaultFileFormat.LoggerFormatString),
            };
            MasterConsoleTarget = new ColoredConsoleTarget($"Master_{LogBrokerName}_ColoredConsoleTarget")
            {
                // Setup our layout for content formatting and set our new highlighting rules
                Layout = new SimpleLayout(DefaultConsoleFormat.LoggerFormatString),
                RowHighlightingRules =
                {
                    new ConsoleRowHighlightingRule("level == LogLevel.Trace", ConsoleOutputColor.DarkGray, ConsoleOutputColor.Black),
                    new ConsoleRowHighlightingRule("level == LogLevel.Debug", ConsoleOutputColor.Gray, ConsoleOutputColor.Black),
                    new ConsoleRowHighlightingRule("level == LogLevel.Info", ConsoleOutputColor.Green, ConsoleOutputColor.Black),
                    new ConsoleRowHighlightingRule("level == LogLevel.Warn", ConsoleOutputColor.Red, ConsoleOutputColor.Yellow),
                    new ConsoleRowHighlightingRule("level == LogLevel.Error", ConsoleOutputColor.Red, ConsoleOutputColor.Gray),
                    new ConsoleRowHighlightingRule("level == LogLevel.Fatal", ConsoleOutputColor.Red, ConsoleOutputColor.White)
                }
            };

            // Spawn a new SharpLogger which will use our master logger instance to write log output
            LogManager.Configuration = new LoggingConfiguration();
            MasterLogger = new SharpLogger(LoggerActions.UniversalLogger,"LogBrokerLogger");
            MasterLogger.WriteLog("LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!", LogType.InfoLog);
            MasterLogger.WriteLog($"SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!\n\n{ToString()}", LogType.TraceLog);
            
            // Return passed at this point since we've written all our logging routines out
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
            // Make sure logging is configured first
            if (!_logBrokerInitialized)
                throw new InvalidOperationException("Error! Please setup the SharpLogBroker before using it");

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
            // Make sure logging is configured first
            if (!_logBrokerInitialized)
                throw new InvalidOperationException("Error! Please setup the SharpLogBroker before using it");

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