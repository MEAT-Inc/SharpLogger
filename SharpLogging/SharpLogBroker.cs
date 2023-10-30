using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            set
            {
                // Convert the value into a LogLevel and set it
                _minLevel = value.ToNLevel();

                // Find our current logging rules and see if they need to be updated or not
                var CurrentLoggerRules = LoggingRules;
                foreach (var LoggingRule in CurrentLoggerRules)
                {
                    // Update the level settings for the rule now
                    LoggingRule.SetLoggingLevels(_minLevel, _maxLevel);
                }

                // Now reconfigure the log manager to apply the new rule changes
                LogManager.ReconfigExistingLoggers();
            }
        }
        public static LogType MaxLevel
        {
            get => _maxLevel.ToLogType();
            set
            {
                // Convert the value into a LogLevel and set it
                _maxLevel = value.ToNLevel();

                // Find our current logging rules and see if they need to be updated or not
                var CurrentLoggerRules = LoggingRules;
                foreach (var LoggingRule in CurrentLoggerRules)
                {
                    // Update the level settings for the rule now
                    LoggingRule.SetLoggingLevels(_minLevel, _maxLevel);
                }

                // Now reconfigure the log manager to apply the new rule changes
                LogManager.ReconfigExistingLoggers();
            }
        }
        public static bool LoggingEnabled { get; set; }

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
            var LoggingFoldersFound = LoggingSubfolders;
            string ChildFolderInfos = LoggingFoldersFound.Length == 0
                ? "No Child Directories"
                : "\n" + string.Join("\n", LoggingFoldersFound
                    .OrderBy(LogFolder => LogFolder.Length)
                    .Select(FolderInfoString => $"\t\t\\__ {FolderInfoString}"));

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
        internal static bool InitializeLogging()
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

            // Set our initialization state to true even though no logging is configured
            _logBrokerInitialized = true;
            return _logBrokerInitialized;
        }
        /// <summary>
        /// Configures a new instance of a log broker for logging configuration/output for an application
        /// When a name of a file is provided to output file path, the name of the log file is used
        /// </summary>
        /// <param name="BrokerConfig">The broker configuration to use for building a new logging session</param>
        public static bool InitializeLogging(BrokerConfiguration BrokerConfig)
        {
            // If the configuration provided is default, setup a new no logging configuration
            LogBrokerConfig = BrokerConfig;
            if (LogBrokerConfig.IsDefault) InitializeLogging();

            // Execute configuration routines for the remainder of the needed broker properties now
            if (!_initializeBrokerConfig())
                throw new ConfigurationErrorsException("Error! Failed to validate a new log broker configuration!");
            if (!_initializeLoggingTargets())
                throw new ConfigurationErrorsException("Error! Failed to configure new log broker master targets!");
            if (!_initializeBrokerLogger())
                throw new ConfigurationErrorsException("Error! Failed to spawn a new broker master logger instance!");

            // Log out that we've configured a new session correctly and return out based on our initialization state
            MasterLogger.WriteLog("LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!", LogType.InfoLog);
            MasterLogger.WriteLog($"SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!\n\n{ToString()}", LogType.TraceLog);

            // Return passed at this point since we've written all our logging routines out
            return _logBrokerInitialized;
        }

        /// <summary>
        /// Completes configuration of the log broker once we've provided a configuration to it
        /// </summary>
        /// <returns>True if our broker instance is updated and stored. False if not</returns>
        /// <exception cref="ArgumentException">Thrown when the broker configuration provided is not usable</exception>
        private static bool _initializeBrokerConfig()
        {
            // Start by storing new configuration values for the log broker and archiver configurations
            _brokerCreated = DateTime.Now;
            LogBrokerName = string.IsNullOrWhiteSpace(LogBrokerConfig.LogBrokerName)
                ? AppDomain.CurrentDomain.FriendlyName
                    .Split(Path.DirectorySeparatorChar).Last()
                    .Split('.')[0].Replace(" ", string.Empty)
                : LogBrokerConfig.LogBrokerName;

            // Check our logging level values provided in our configurations and see what needs to be updated
            LoggingEnabled = ((int)LogBrokerConfig.MinLogLevel != 6 && (int)LogBrokerConfig.MaxLogLevel != 6);
            if (!LoggingEnabled)
            {
                // If logging is disabled, then we setup our levels to be both Off/NoLogging
                _minLevel = LogLevel.Off;
                _maxLevel = LogLevel.Off;
            }
            else
            {
                // If logging is enabled, use the levels from our configuration file here
                _minLevel = (int)LogBrokerConfig.MinLogLevel < 0
                    ? LogLevel.Trace
                    : LogBrokerConfig.MinLogLevel.ToNLevel();
                _maxLevel = (int)LogBrokerConfig.MaxLogLevel > 6
                    ? LogLevel.Fatal
                    : LogBrokerConfig.MaxLogLevel.ToNLevel();
            }

            // Make sure our min level is less than the max level. If it's not, then switch them
            if (_minLevel.Ordinal > _maxLevel.Ordinal)
            {
                // Store and swap the values for our min and max levels here
                int MinInt = _minLevel.Ordinal;
                int MaxInt = _maxLevel.Ordinal;

                // Swap the values and move on
                _minLevel = LogLevel.FromOrdinal(MaxInt);
                _maxLevel = LogLevel.FromOrdinal(MinInt);
            }

            // Now find our output log file path/name value and create the logging output file
            string LoggerTime = _brokerCreated.ToString("MMddyyy-HHmmss");
            if (string.IsNullOrWhiteSpace(LogBrokerConfig.LogFilePath) || !Path.IsPathRooted(LogBrokerConfig.LogFilePath))
            {
                // Store our new log file path value and exit out once stored since we now have a logging path
                LogFileName = string.IsNullOrWhiteSpace(LogBrokerConfig.LogFileName)
                    ? $"{LogBrokerName}_Logging_{LoggerTime}.log"
                    : $"{Path.GetFileNameWithoutExtension(LogBrokerConfig.LogFileName).Replace($"$LOGGER_TIME", LoggerTime)}{Path.GetExtension(LogBrokerConfig.LogFileName)}";

                // Build the full path based on the path to our calling executable
                LogFilePath = Path.IsPathRooted(LogBrokerConfig.LogFilePath)
                    ? Path.Combine(LogBrokerConfig.LogFilePath, LogFileName)
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogBrokerConfig.LogFilePath, LogFileName);
            }
            else
            {
                // Try and find the name of the file if one is given
                _logBrokerConfig.LogFilePath = Path.GetFullPath(LogBrokerConfig.LogFilePath);
                bool PathEndsWithDirChars =
                    LogBrokerConfig.LogFilePath.EndsWith("" + Path.DirectorySeparatorChar) ||
                    LogBrokerConfig.LogFilePath.EndsWith("" + Path.AltDirectorySeparatorChar);

                // Now using the cleaned up path, find out if we've got a folder or a file being passed in
                if (File.Exists(LogBrokerConfig.LogFilePath) || Path.HasExtension(LogBrokerConfig.LogFilePath))
                {
                    // If we found an actual file for the input path, we know that's the final result
                    LogFileName = Path.GetFileName(LogBrokerConfig.LogFilePath);
                    LogFilePath = Path.GetFullPath(LogBrokerConfig.LogFilePath.Trim());
                }
                else if (Directory.Exists(LogBrokerConfig.LogFilePath) || PathEndsWithDirChars || !Path.HasExtension(LogBrokerConfig.LogFilePath))
                {
                    // Setup a new log file name based on the instance name and the path given
                    LogFileName = string.IsNullOrWhiteSpace(LogBrokerConfig.LogFileName)
                        ? $"{LogBrokerName}_Logging_{LoggerTime}.log"
                        : $"{Path.GetFileNameWithoutExtension(LogBrokerConfig.LogFileName).Replace($"$LOGGER_TIME", LoggerTime)}{Path.GetExtension(LogBrokerConfig.LogFileName)}";

                    // Build the full path based on the default output location and the log file name pulled in
                    LogFilePath = Path.GetFullPath(Path.Combine(LogBrokerConfig.LogFilePath, LogFileName));
                }
                else
                {
                    // Throw a new failure here if we got to this point somehow. This means we didn't have a legal path value for the configuration
                    string ArgNameAndValue = $"{nameof(LogBrokerConfig.LogFilePath)} -- {LogBrokerConfig.LogFilePath}";
                    throw new ArgumentException($"Error! Broker configuration could not be used to setup logging! ({ArgNameAndValue})");
                }
            }

            // Using the newly build path and file name values, setup an output folder if it needs to be built
            LogFileFolder = Path.GetFullPath(LogFilePath.Replace(LogFileName, string.Empty));
            if (!Directory.Exists(LogFileFolder)) Directory.CreateDirectory(LogFileFolder);

            // Update our configuration to reflect the newly determined values for files
            _logBrokerConfig.MaxLogLevel = MaxLevel;
            _logBrokerConfig.MinLogLevel = MinLevel;
            _logBrokerConfig.LogFileName = LogFileName;
            _logBrokerConfig.LogFilePath = LogFilePath;
            _logBrokerConfig.LogBrokerName = LogBrokerName;

            // The broker is configured if logging is enabled and out path values are setup correctly, or if logging is disabled
            _logBrokerInitialized = LoggingEnabled == false || (LoggingEnabled && _logBrokerConfig.LogFilePath != null && _logBrokerConfig.LogFileName != null);
            return _logBrokerInitialized;
        }
        /// <summary>
        /// Configures new master logging targets for file and console output
        /// </summary>
        /// <returns>True if the logger targets are built, false if not</returns>
        private static bool _initializeLoggingTargets()
        {
            // Lock our logger pool before running this operation
            List<SharpLogger> FileLoggers = new List<SharpLogger>();
            lock (_loggerPool)
            {
                // Setup a temp list of logger targets to reconfigure
                foreach (var LoggerInstance in _loggerPool)
                {
                    // Check if the logger has our existing targets
                    if (MasterFileTarget == null) break;
                    if (!LoggerInstance.LoggerTargets.Contains(MasterFileTarget)) continue;
                    
                    // Remove the master target and add this logger to our temp list
                    LoggerInstance.RemoveTarget(MasterFileTarget);
                    FileLoggers.Add(LoggerInstance);
                }
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

            // Now add back all file logger targets as needed
            foreach (var ExistingLogger in FileLoggers)
                ExistingLogger.RegisterTarget(MasterFileTarget);

            // Return out if both targets were built correctly 
            return MasterFileTarget != null && MasterConsoleTarget != null;
        }
        /// <summary>
        /// Spawns and stores a new log broker logger instance. This is our main logging helper instance
        /// </summary>
        /// <returns>True if our logger is configured and stored. False if not</returns>
        private static bool _initializeBrokerLogger()
        {
            // Lock the logger pool and try to purge previous loggers here
            lock (_loggerPool) 
            {
                // Remove previous master loggers here
                if (MasterLogger != null && _loggerPool.Contains(MasterLogger))
                    DestroyLogger(MasterLogger);
            }

            // Spawn a new SharpLogger which will use our master logger instance to write log output
            LogManager.Configuration = new LoggingConfiguration();
            MasterLogger = new SharpLogger(LoggerActions.UniversalLogger, $"{LogBrokerName}_LogBrokerLogger");
            MasterLogger.WriteLog($"MASTER LOGGER {MasterLogger.LoggerName} HAS BEEN SPAWNED CORRECTLY!", LogType.InfoLog);

            // Return out if our logger instance was built or not 
            return MasterLogger != null;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Stores and applies new logging levels for this log broker session
        /// </summary>
        /// <param name="MinLogLevel">The minimum logging level</param>
        /// <param name="MaxLogLevel">The maximum logging level</param>
        public static void SetLogLevels(LogType MinLogLevel, LogType MaxLogLevel)
        {
            // Store our new logging level values and reconfigure targets
            _minLevel = MinLogLevel.ToNLevel();
            _maxLevel = MaxLogLevel.ToNLevel();
            MasterLogger?.WriteLog($"CONFIGURED NEW LOGGING LEVELS! MIN LEVEL: {MinLogLevel} | MAX LEVEL: {MaxLogLevel}", LogType.InfoLog);
            MasterLogger?.WriteLog($"APPLYING THESE NEW LOGGING LEVEL CHANGES TO ALL EXISTING LOGGING RULES NOW...", LogType.InfoLog);

            // Find our current logging rules and see if they need to be updated or not
            var CurrentLoggerRules = LoggingRules;
            foreach (var LoggingRule in CurrentLoggerRules)
            {
                // Update the level settings for the rule now
                LoggingRule.SetLoggingLevels(_minLevel, _maxLevel);
            }

            // Now reconfigure the log manager to apply the new rule changes
            LogManager.ReconfigExistingLoggers();
            MasterLogger?.WriteLog($"LOGGER LEVEL RECONFIGURATION HAS BEEN COMPLETED WITHOUT ISSUES!", LogType.InfoLog);
        }

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