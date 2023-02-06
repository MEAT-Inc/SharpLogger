using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;

namespace SharpLogger
{
    /// <summary>
    /// Main Logger object setup for logging to different output types/formats.
    /// </summary>
    public class SharpLogger
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields

        // Time info and GUID for logger.
        public readonly Guid LoggerGuid;               // Name of the logger
        public readonly DateTime TimeStarted;          // Time the logger was launched.

        // Setup Basic properties of all loggers
        private readonly Logger _nLogger;              // NLog object that does our output writing
        public readonly string LoggerName;             // Name of the logger being built/used
        public readonly LoggerActions LoggerType;      // Type of logger being built/controlled

        // Log Level Info (0 is Trace, 6 Is Off)               
        internal readonly LogLevel MinLevel;           // Lowest level of supported logging output
        internal readonly LogLevel MaxLevel;           // Highest level of supported logging output
        private readonly bool _loggingEnabled;         // Sets if logging is on or off based on level values
        
        #endregion //Fields

        #region Properties

        // Tells us if logging is on or off for this logger instance
        internal bool LoggingEnabled => !this._loggingEnabled || !SharpLogBroker.LoggingEnabled;

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
            this.MinLevel = LogLevel.FromOrdinal((int)MinLevel);
            if (this.MinLevel.Ordinal > SharpLogBroker.MinLevel.Ordinal)
                this.MinLevel = SharpLogBroker.MinLevel;
            this.MaxLevel = LogLevel.FromOrdinal((int)MaxLevel);
            if (this.MaxLevel.Ordinal > SharpLogBroker.MaxLevel.Ordinal)
                this.MaxLevel = SharpLogBroker.MaxLevel;

            // Set if logging is on or off now
            this._loggingEnabled = this.MinLevel != LogLevel.Off && this.MaxLevel != LogLevel.Off;

            // Store values and configure the name/GUID/time values for this logger instance now
            this.LoggerType = LoggerType;
            this.TimeStarted = DateTime.Now;
            this.LoggerGuid = Guid.NewGuid();
            this.LoggerName = LoggerName + "_" + LoggerType.ToString();

            // Configure new logger targets based on the type of this logger
            LogManager.Configuration ??= new LoggingConfiguration();
            this._nLogger = LogManager.GetCurrentClassLogger();
 
            // Now store new targets for these loggers based on the types provided
            if (this.LoggerType.HasFlag(LoggerActions.ConsoleLogger))
            {
                // Build the new Console Target and add it to our configuration
                var ConsoleTarget = SharpTargetBuilder.GenerateConsoleLogger(this.LoggerName);
                LogManager.Configuration.AddTarget(ConsoleTarget);
            }
            if (this.LoggerType.HasFlag(LoggerActions.FileLogger))
            {
                // Build the new Console Target and add it to our configuration
                var FileTarget = SharpTargetBuilder.GenerateFileLogger(SharpLogBroker.LogFileName, this.LoggerName);
                LogManager.Configuration.AddTarget(FileTarget);
            }
            if (this.LoggerType.HasFlag(LoggerActions.SqlLogger))
            {
                // Since SQL Logging is not yet setup, just move on from this routine
                if (this.LoggerType == LoggerActions.SqlLogger)
                    throw new InvalidOperationException("Error! SQL Logging is not yet configured for the SharpLogger!");
            }

            // Print out some logger information values and store this logger in our broker pool
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", "StartupLogger (" + this.LoggerName + ")");
            MappedDiagnosticsContext.Set("calling-class-short", "StartupLogger");
            this._nLogger.Log(LogLevel.Info, $"LOGGER NAME: {this.LoggerName}");
            this._nLogger.Log(LogLevel.Info, $"--> TIME STARTED:  {this.TimeStarted}");
            this._nLogger.Log(LogLevel.Info, $"--> LOGGER GUID:   {this.LoggerGuid.ToString("D").ToUpper()}");

            // Add self to queue and validate our _nLogger has been built
            SharpLogBroker.RegisterLogger(this);
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
            if (this.LoggingEnabled) return;

            // Set Context here and store
            string ClassName = this._getCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short", 
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

            // Write value and flush outputs
            this._nLogger.Log(Level.ToNLevel(), LogMessage);
        }
        /// <summary>
        /// Writes an exceptions contents out to the logger
        /// </summary>
        /// <param name="LoggedEx">LoggedEx to write</param>
        /// <param name="Level">Level to log it</param>
        public void WriteLog(Exception LoggedEx, LogType Level = LogType.ErrorLog)
        {
            // Make sure logging is not set to off right now
            if (this.LoggingEnabled) return;

            // Set Context here and store
            string ClassName = this._getCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short",
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

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
                this.WriteLog(LoggedEx.InnerException, Level);
            }
        }
        /// <summary>
        /// Writes an exception object out.
        /// </summary>
        /// <param name="MessageExInfo">Info message</param>
        /// <param name="Ex">LoggedEx to write</param>
        /// <param name="LevelTypes">Levels. Msg and then LoggedEx</param>
        public void WriteLog(string MessageExInfo, Exception Ex, params LogType[] LevelTypes)
        {
            // Check level count and make sure logging is set to on
            if (this.LoggingEnabled) return;
            if (LevelTypes.Length == 0) { LevelTypes = new LogType[] { LogType.ErrorLog, LogType.ErrorLog }; }
            if (LevelTypes.Length == 1) { LevelTypes = LevelTypes.Append(LevelTypes[0]).ToArray(); }

            // Store Calling Class and set our context for the logger output types
            string ClassName = this._getCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short",
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

            // Write Log Message then exception and all information found from the exception here
            this._nLogger.Log(LevelTypes[0].ToNLevel(), MessageExInfo);
            this._nLogger.Log(LevelTypes[0].ToNLevel(), $"EXCEPTION THROWN FROM {Ex.TargetSite}. DETAILS ARE SHOWN BELOW");
            this._nLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX MESSAGE {Ex.Message}");
            this._nLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX SOURCE  {Ex?.Source}");
            this._nLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX TARGET  {Ex.TargetSite?.Name}");
            this._nLogger.Log(LevelTypes[1].ToNLevel(),
                Ex.StackTrace == null
                    ? "FURTHER DIAGNOSTIC INFO IS NOT AVAILABLE AT THIS TIME."
                    : $"\tEX STACK\n{Ex.StackTrace.Replace("\n", "\n\t")}");

            // If our inner exception is not null, run it through this logger.
            this._nLogger.Log(LevelTypes[1].ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW"); 
            this.WriteLog(MessageExInfo, Ex.InnerException, LevelTypes);
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
