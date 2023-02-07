using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace SharpLogger
{
    /// <summary>
    /// Main Logger object setup for logging to different output types/formats.
    /// </summary>
    public class SharpLogger : INotifyPropertyChanged
    {
        #region Custom Events

        // Event handler for when properties on this logger instance are changed
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Event invoker for when a property is changed on our logger instance
        /// </summary>
        /// <param name="PropertyName">The name of the property being changed/updated</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            // Invoke the event handler using a new event argument object if it's not null
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
        /// <summary>
        /// Sets the backing FieldToUpdate for a property when called
        /// </summary>
        /// <typeparam name="T">The type of the FieldToUpdate being set</typeparam>
        /// <param name="FieldToUpdate">The reference to the FieldToUpdate being updated</param>
        /// <param name="FieldValue">The FieldValue stored on our FieldToUpdate</param>
        /// <param name="PropertyName">The name of the property being updated</param>
        /// <returns>True if the property changed event is invoked, false if it is not</returns>
        protected bool SetField<T>(ref T FieldToUpdate, T FieldValue, [CallerMemberName] string PropertyName = null)
        {
            // If the field needs a new value to be set, set it and invoke the property updated event
            if (!EqualityComparer<T>.Default.Equals(FieldToUpdate, FieldValue))
            {
                FieldToUpdate = FieldValue;
                OnPropertyChanged(PropertyName);
                return true;
            }

            // If the field is not updated/the same value is found, don't do anything
            return false;
        }

        #endregion //Custom Events

        #region Fields

        // Private backing fields which hold information about our logger instance
        private Guid _loggerGuid;                                         // The GUID of this logger object
        private string _loggerName;                                       // The Name of this logger object
        private string _loggerClass;                                      // The class which called this logger
        private DateTime _timeCreated;                                    // The time this logger object was built

        // Backing fields holding information about the logger levels and actions
        private LogLevel _minLevel;                                       // Lowest level of supported logging output
        private LogLevel _maxLevel;                                       // Highest level of supported logging output
        private LoggerActions _loggerType;                                // Type of logger being built/controlled
        private readonly Logger _nLogger;                                 // NLog object that does our output writing

        // Private fields holding information about our built target objects
        private readonly List<Target> _loggerTargets;                     // Collection of built targets for this logger
        private readonly List<AsyncTargetWrapper> _asyncWrappers;         // A collection of async target wrappers for our logger

        // Private backing fields which hold our format configurations for output
        private SharpFileTargetFormat _fileLoggerFormat;                  // Configuration to build File format strings
        private SharpConsoleTargetFormat _consoleLoggerFormat;            // Configuration to build Console format strings

        #endregion //Fields

        #region Properties

        // Public properties holding information about our logger name and state
        public Guid LoggerGuid
        {
            get => this._loggerGuid;
            private set => this.SetField(ref this._loggerGuid, value);
        }
        public string LoggerName
        {
            get => this._loggerName;
            private set => this.SetField(ref this._loggerName, value);
        }
        public string LoggerClass
        {
            get => this._loggerClass;
            private set => this.SetField(ref this._loggerClass, value);
        }
        public DateTime TimeCreated
        {
            get => this._timeCreated;
            private set => this.SetField(ref this._timeCreated, value);
        }

        // Public facing log levels. Used to help convert between LogTypes and LogLevels
        public LogType MinLevel
        {
            get => this._minLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                this.SetField(ref this._minLevel, ConvertedLevel);
            }
        }
        public LogType MaxLevel
        {
            get => this._maxLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                this.SetField(ref this._maxLevel, ConvertedLevel);
            }
        }
        public bool LoggingEnabled => 
            SharpLogBroker.LoggingEnabled && 
            (this._minLevel != LogLevel.Off && this._maxLevel != LogLevel.Off);

        // Public facing properties holding our configurations for format output strings
        public SharpFileTargetFormat FileTargetFormat
        {
            get => this._fileLoggerFormat ?? SharpLogBroker.DefaultFileFormat;
            set => this._fileLoggerFormat = value;
        }
        public SharpConsoleTargetFormat ConsoleTargetFormat
        {
            get => this._consoleLoggerFormat ?? SharpLogBroker.DefaultConsoleFormat;
            set => this._consoleLoggerFormat = value;
        }

        // Public facing collection of supported logging types
        public LoggerActions LoggerType
        {
            get => this._loggerType;
            set => this.SetField(ref this._loggerType, value);
        }
        public bool IsFileLogger => this._loggerType.HasFlag(LoggerActions.FileLogger);
        public bool IsAsyncLogger => this._loggerType.HasFlag(LoggerActions.AsyncLogger);
        public bool IsConsoleLogger => this._loggerType.HasFlag(LoggerActions.ConsoleLogger);
        
        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new FalconLogger object and adds it to the logger pool.
        /// </summary>
        /// <param name="LoggerName">Name of this logger which will be included in the output strings for it</param>
        /// <param name="LoggerType">Type of actions/targets to configure for this logger</param>
        /// <param name="MinLevel">Min Log Level for output values being written</param>
        /// <param name="MaxLevel">Max Log Level for output values being written</param>
        public SharpLogger(LoggerActions LoggerType, [CallerMemberName] string LoggerName = "", LogType MinLevel = LogType.TraceLog, LogType MaxLevel = LogType.FatalLog)
        {
            // Set Min and Max logging levels and make sure they comply with the logging broker
            this.MinLevel = SharpLogBroker.MinLevel == LogType.NoLogging
                ? LogType.NoLogging
                : MinLevel > SharpLogBroker.MinLevel
                    ? SharpLogBroker.MinLevel
                    : MinLevel;
            this.MaxLevel = SharpLogBroker.MaxLevel == LogType.NoLogging
                ? LogType.NoLogging
                : MaxLevel > SharpLogBroker.MaxLevel
                    ? SharpLogBroker.MaxLevel
                    : MaxLevel;

            // Store values and configure the name/GUID/time values for this logger instance now
            this.LoggerType = LoggerType;
            this.LoggerClass = LoggerName;
            this.TimeCreated = DateTime.Now;
            this.LoggerGuid = Guid.NewGuid();
            this.LoggerName = $"{LoggerName}_{LoggerGuid.ToString("D").ToUpper()}";

            // Configure new logger targets based on the type of this logger
            LogManager.Configuration ??= new LoggingConfiguration();
            this._nLogger = LogManager.GetCurrentClassLogger();

            // Build new lists for our logger target types
            this._loggerTargets = new List<Target>();
            this._asyncWrappers = new List<AsyncTargetWrapper>();

            // Now store new targets for these loggers based on the types provided
            if (this.IsFileLogger) this._spawnFileTarget();
            if (this.IsConsoleLogger) this._spawnConsoleTarget();

            // Print out some logger information values and store this logger in our broker pool
            this.WriteLog($"LOGGER NAME: {this.LoggerName} HAS BEEN SPAWNED CORRECTLY!", LogType.InfoLog);
            this.WriteLog($"--> IS ASYNC:      {(this.IsAsyncLogger ? "YES" : "NO")}", LogType.TraceLog);
            this.WriteLog($"--> TIME CREATED:  {this.TimeCreated:G}", LogType.TraceLog);
            this.WriteLog($"--> LOGGER GUID:   {this.LoggerGuid.ToString("D").ToUpper()}", LogType.TraceLog);
            this.WriteLog($"--> TARGET COUNT:  {this._loggerTargets.Count} TARGETS", LogType.TraceLog);

            // Add self to queue and validate our _nLogger has been built
            SharpLogBroker.RegisterLogger(this);
        }
        /// <summary>
        /// Routine to run when this logger instance is destroyed/released from the broker
        /// </summary>
        ~SharpLogger()
        {
            try
            {
                // Begin by removing all the logging targets for this logger from our configuration
                foreach (var LoggerTarget in this._loggerTargets)
                    LogManager.Configuration.RemoveTarget(LoggerTarget.Name);
                foreach (var AsyncLoggerTarget in this._asyncWrappers)
                    LogManager.Configuration.RemoveTarget(AsyncLoggerTarget.Name);

                // Remove the logger from the broker pool and
                SharpLogBroker.DestroyLogger(this);
                SharpLogBroker.MasterLogger.WriteLog($"LOGGER {this.LoggerName} HAS BEEN REMOVED FROM OUR BROKER!", LogType.TraceLog);
            }
            catch (Exception DestroyLoggerEx)
            {
                // Log the Exception out using the log broker logger
                SharpLogBroker.MasterLogger.WriteLog($"EXCEPTION THROWN DURING LOGGER REMOVAL PROCESS!", LogType.TraceLog);
                SharpLogBroker.MasterLogger.WriteException($"EXCEPTION IS BEING LOGGED BELOW", DestroyLoggerEx, LogType.TraceLog);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes our a log entry using the given message and log level
        /// </summary>
        /// <param name="LogMessage">Message to write</param>
        /// <param name="Level">Level to log</param>
        public void WriteLog(string LogMessage, LogType Level = LogType.DebugLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Configure a set of new scope properties for our output content log entries
            var ScopeProperties = new KeyValuePair<string, object>[]
            {
                new("logger-class", this.LoggerClass),
                new("calling-class", this._getCallingClass()),
                new("calling-class-short", this._getCallingClass(true)),
            };
            
            // Using the built scope properties, write our log entries out to the targets now
            using (this._nLogger.PushScopeProperties(ScopeProperties))
                this._nLogger.Log(Level.ToNLevel(), LogMessage);
        }

        /// <summary>
        /// Writes an exceptions contents out to the logger
        /// </summary>
        /// <param name="LoggedEx">LoggedEx to write</param>
        /// <param name="Level">Level to log it</param>
        public void WriteException(Exception LoggedEx, LogType Level = LogType.ErrorLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Configure a set of new scope properties for our output content log entries
            var ScopeProperties = new KeyValuePair<string, object>[]
            {
                new("logger-class", this.LoggerClass),
                new("calling-class", this._getCallingClass()),
                new("calling-class-short", this._getCallingClass(true)),
            };

            // Using the built scope properties, write our log entries out to the targets now
            using (this._nLogger.PushScopeProperties(ScopeProperties))
            {
                // Write Exception values
                this._nLogger.Log(Level.ToNLevel(), $"LoggedEx Message {LoggedEx.Message}");
                this._nLogger.Log(Level.ToNLevel(), $"LoggedEx Source  {LoggedEx.Source}");
                this._nLogger.Log(Level.ToNLevel(), $"LoggedEx Target  {LoggedEx.TargetSite.Name}");

                // Write the LoggedEx Stack trace and the inner exceptions if they're not null
                if (LoggedEx.StackTrace != null) this._nLogger.Log(Level.ToNLevel(), $"LoggedEx Stack\n{LoggedEx.StackTrace}");
                if (LoggedEx.InnerException != null)
                {
                    // If our inner exception is not null, run it through this logger.
                    this._nLogger.Log(Level.ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW");
                    this.WriteException(LoggedEx.InnerException, Level);
                }
            }
        }
        /// <summary>
        /// Writes an exception object out.
        /// </summary>
        /// <param name="MessageExInfo">Info message</param>
        /// <param name="Ex">LoggedEx to write</param>
        /// <param name="LogLevels">Levels. Msg and then LoggedEx</param>
        public void WriteException(string MessageExInfo, Exception Ex, params LogType[] LogLevels)
        {
            // Check level count and make sure logging is set to on
            if (!this.LoggingEnabled) return;
            if (LogLevels.Length == 0) { LogLevels = new LogType[] { LogType.ErrorLog, LogType.ErrorLog }; }
            if (LogLevels.Length == 1) { LogLevels = LogLevels.Append(LogLevels[0]).ToArray(); }

            // Configure a set of new scope properties for our output content log entries
            var ScopeProperties = new KeyValuePair<string, object>[]
            {
                new("logger-class", this.LoggerClass),
                new("calling-class", this._getCallingClass()),
                new("calling-class-short", this._getCallingClass(true)),
            };

            // Write Log Message then exception and all information found from the exception here
            using (this._nLogger.PushScopeProperties(ScopeProperties))
            {
                this._nLogger.Log(LogLevels[0].ToNLevel(), MessageExInfo);
                this._nLogger.Log(LogLevels[0].ToNLevel(), $"EXCEPTION THROWN FROM {Ex.TargetSite}. DETAILS ARE SHOWN BELOW");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX MESSAGE {Ex.Message}");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX SOURCE  {Ex?.Source}");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX TARGET  {Ex.TargetSite?.Name}");
                this._nLogger.Log(LogLevels[1].ToNLevel(),
                    Ex.StackTrace == null
                        ? "FURTHER DIAGNOSTIC INFO IS NOT AVAILABLE AT THIS TIME."
                        : $"\tEX STACK\n{Ex.StackTrace.Replace("\n", "\n\t")}");

                // If our inner exception is not null, run it through this logger.
                this._nLogger.Log(LogLevels[1].ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW");
                this.WriteException(MessageExInfo, Ex.InnerException, LogLevels);
            }
        }

        /// <summary>
        /// Writes an object of any kind out after calling the ToString method on it.
        /// This splits the result of the ToString call and logs each line out accordingly
        /// </summary>
        /// <param name="ObjectToLog">The object we wish to log out</param>
        /// <param name="Level">The level to log the object at</param>
        public void WriteObjectString(object ObjectToLog, LogType Level = LogType.DebugLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Configure a set of new scope properties for our output content log entries
            var ScopeProperties = new KeyValuePair<string, object>[]
            {
                new("logger-class", this.LoggerClass),
                new("calling-class", this._getCallingClass()),
                new("calling-class-short", this._getCallingClass(true)),
            };

            // Convert the object to a string and split it out based on new line characters
            string[] ObjectStrings = ObjectToLog.ToString()
                .Split(new[] { "\n\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(LogLine => !string.IsNullOrWhiteSpace(LogLine))
                .ToArray();

            // Using the built scope properties, write our log entries out to the targets now
            using (this._nLogger.PushScopeProperties(ScopeProperties))
                foreach (var ObjectString in ObjectStrings) this._nLogger.Log(Level.ToNLevel(), ObjectString);
        }
        /// <summary>
        /// Writes an object of any kind out as a JSON string to the log file
        /// </summary>
        /// <param name="ObjectToLog">The object we need to write out to our log file</param>
        /// <param name="UseTabs">When true, the output json string will be tab formatted</param>
        /// <param name="Level">The level to log the object at</param>
        public void WriteObjectJson(object ObjectToLog, bool UseTabs = true, LogType Level = LogType.DebugLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Configure a set of new scope properties for our output content log entries
            var ScopeProperties = new KeyValuePair<string, object>[]
            {
                new("logger-class", this.LoggerClass),
                new("calling-class", this._getCallingClass()),
                new("calling-class-short", this._getCallingClass(true)),
            };

            // Convert the object to a string and split it out based on new line characters if we're using JSON tabs
            string ObjectString = JsonConvert.SerializeObject(ObjectToLog, UseTabs ? Formatting.Indented : Formatting.None);
            using (this._nLogger.PushScopeProperties(ScopeProperties))
            {
                // If not using tab formatting, then just write the string out
                if (!UseTabs) this._nLogger.Log(Level.ToNLevel(), ObjectString);
                else
                {
                    // Split the JSON content into a set of strings and log them all out
                    string[] ObjectStrings = ObjectString
                        .Split(new[] { "\n\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .ToArray();

                    // Log each of the log lines out using the class NLogger
                    foreach (var ObjectJsonString in ObjectStrings)
                        this._nLogger.Log(Level.ToNLevel(), ObjectJsonString);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Spawns a new ColoredConsoleTarget for this logger instance
        /// </summary>
        /// <returns>The built logger target object which contains rules and formats for our outputs</returns>
        private ColoredConsoleTarget _spawnConsoleTarget()
        {
            // Make Logger and set format.
            var ConsoleLoggerTarget = new ColoredConsoleTarget($"{this.LoggerName}_ColoredConsoleTarget");
            ConsoleLoggerTarget.Layout = new SimpleLayout(this.ConsoleTargetFormat.LoggerFormatString);

            // Add Coloring Rules
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Trace",
                ConsoleOutputColor.DarkGray,
                ConsoleOutputColor.Black));
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Debug",
                ConsoleOutputColor.Gray,
                ConsoleOutputColor.Black));
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Info",
                ConsoleOutputColor.Green,
                ConsoleOutputColor.Black));
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Warn",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.Yellow));
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Error",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.Gray));
            ConsoleLoggerTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Fatal",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.White));

            // Register the new console logger target in the log manager configuration
            this._loggerTargets.Add(ConsoleLoggerTarget);
            LogManager.Configuration.AddTarget(ConsoleLoggerTarget);

            // If the logger is async, then spawn an async target too and store it on this instance
            if (this.IsAsyncLogger)
            {
                // Spawn the async target and store it on our instance
                var AsyncConsoleTarget = this._convertToAsyncTarget(ConsoleLoggerTarget);
                this._asyncWrappers.Add(AsyncConsoleTarget);
                LogManager.Configuration.AddTarget(AsyncConsoleTarget);
            }

            // Now return the built console logging target and exit out
            return ConsoleLoggerTarget;
        }
        /// <summary>
        /// Spawns a new FileTarget for this logger instance
        /// </summary>
        /// <param name="LogFilePath">An optional path to a log file we wish to write out to</param>
        /// <returns>The built logger target object which contains rules and formats for our outputs</returns>
        private FileTarget _spawnFileTarget(string LogFilePath = null)
        {
            // Find the name of the log file we're writing into first
            LogFilePath ??= SharpLogBroker.LogFileName;
            string LogFileName = Path.GetFileNameWithoutExtension(LogFilePath);
            string FileTargetName = $"{this.LoggerName}_{LogFileName}";

            // Build the new file target for our logger instance
            var FileLoggerTarget = new FileTarget(FileTargetName);
            FileLoggerTarget.KeepFileOpen = false;
            FileLoggerTarget.MaxArchiveFiles = 20;
            FileLoggerTarget.ConcurrentWrites = true;
            FileLoggerTarget.FileName = LogFilePath;
            FileLoggerTarget.ArchiveAboveSize = 1953125;
            FileLoggerTarget.ArchiveEvery = FileArchivePeriod.Day;
            FileLoggerTarget.ArchiveNumbering = ArchiveNumberingMode.DateAndSequence;
            FileLoggerTarget.Layout = new SimpleLayout(this.FileTargetFormat.LoggerFormatString);
            FileLoggerTarget.ArchiveFileName = "${basedir}/LogArchives/" + SharpLogBroker.LogBrokerName + ".{####}.log";

            // Register the new console logger target in the log manager configuration
            this._loggerTargets.Add(FileLoggerTarget);
            LogManager.Configuration.AddTarget(FileLoggerTarget);

            // If the logger is async, then spawn an async target too and store it on this instance
            if (this.IsAsyncLogger)
            {
                // Spawn the async target and store it on our instance
                var AsyncConsoleTarget = this._convertToAsyncTarget(FileLoggerTarget);
                this._asyncWrappers.Add(AsyncConsoleTarget);
                LogManager.Configuration.AddTarget(AsyncConsoleTarget);
            }

            // Now return the built file logging target and exit out
            return FileLoggerTarget;
        }
        /// <summary>
        /// Wraps a built logging target into an async target instance and returns it out
        /// </summary>
        /// <param name="LoggingTarget">The target to convert</param>
        /// <param name="MinLevel">The minimum logging level for the async target</param>
        /// <returns>A Wrapped async target</returns>
        private AsyncTargetWrapper _convertToAsyncTarget(Target LoggingTarget, LogType MinLevel = LogType.NoLogging)
        {
            // Build the wrapper object and convert our input target
            AsyncTargetWrapper AsyncWrapper = new AsyncTargetWrapper();

            // Configure properties of the async target object.
            AsyncWrapper.QueueLimit = 5000;
            AsyncWrapper.WrappedTarget = LoggingTarget;
            AsyncWrapper.OverflowAction = AsyncTargetWrapperOverflowAction.Grow;

            // Return the built target object.
            var LogLevelParsed = MinLevel == LogType.NoLogging ? _minLevel : MinLevel.ToNLevel();
            SimpleConfigurator.ConfigureForTargetLogging(AsyncWrapper, LogLevelParsed);
            return AsyncWrapper;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the name of the calling method.
        /// </summary>
        /// <returns>String of the full method name.</returns>
        private string _getCallingClass(bool SplitString = false)
        {
            // Setup values.
            string FullCallName; Type DeclaredType; int SkipFrames = 2;
            do
            {
                // Find the current method caller and store the stack. 
                MethodBase MethodBase = new StackFrame(SkipFrames, false).GetMethod();
                DeclaredType = MethodBase.DeclaringType;
                if (DeclaredType == null) { return MethodBase.Name; }

                // Skip frame increased and keep checking.
                SkipFrames++;
                FullCallName = DeclaredType.FullName + "." + MethodBase.Name;
            }
            while (DeclaredType.Module.Name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));

            // Check for split values.
            if (!SplitString) { return FullCallName; }
            var FullNameSplit = FullCallName.Split('.');
            FullCallName = FullNameSplit[FullNameSplit.Length - 1];

            // Return the name here.
            return FullCallName;
        }
    }
}
