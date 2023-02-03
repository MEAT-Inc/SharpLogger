using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    /// <summary>
    /// Base Logger object setup for logging to different output types/formats.
    /// </summary>
    public abstract class BaseLogger
    {
        // Host broker object which provides needed info to these loggers.
        public static LogBroker BrokerHost;

        // Time info and GUID for logger.
        public Guid LoggerGuid;            // Name of the logger
        public DateTime TimeStarted;       // Time the logger was launched.

        // Setup Basic properties of all loggers
        public string LoggerName;           // Name of the logger
        public LoggerActions LoggerType;    // Type of logger

        // NLog objects
        protected Logger NLogger;                              // NLog object
        protected LoggingConfiguration LoggingConfig;          // Logging config item
                                                               
        // Log Level Info (0 is Trace, 6 Is Off)               
        internal bool DisableLogging = false;                  // Toggle to turn logging on or off.
        internal LogLevel MinLevel = LogLevel.Trace;           // Lowest level
        internal LogLevel MaxLevel = LogLevel.Fatal;           // Top most level
                                                               
        // Async Target object properties                      
        protected bool _isAsync;                               // Main bool to sef it we're async or not.
        protected int _logOutputCount = 0;                     // Counter for how many iterations of write were called
        protected static int _forceFlushValue = 50;            // Force flush operations when the number of writes is this value   
        protected internal AsyncTargetWrapper WrapperBuilt;    // Target object itself

        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new FalconLogger object and adds it to the logger pool.
        /// </summary>
        /// <param name="LoggerName">Name of this logger</param>
        /// <param name="LoggerType">Type of actions</param>
        /// <param name="MinLevel">Min Log Level</param>
        /// <param name="MaxLevel">Max Log Level</param>
        protected BaseLogger(LoggerActions LoggerType, [CallerMemberName] string LoggerName = "", int MinLevel = 0, int MaxLevel = 5)
        {
            // Set Min and Max
            this.MinLevel = LogLevel.FromOrdinal(MinLevel);
            this.MaxLevel = LogLevel.FromOrdinal(MaxLevel);

            // Store values and configure now.
            this.LoggerType = LoggerType;
            this.TimeStarted = DateTime.Now;
            this.LoggerGuid = Guid.NewGuid();
            this.LoggerName = LoggerName + "_" + LoggerType.ToString();

            // Add self to queue and validate our NLogger has been built
            LoggerQueue.AddLoggerToPool(this);
        }

        // -------------------------------------- ASYNC CONFIGURATIONS -----------------------------

        /// <summary>
        /// Stores a new flush trigger value onto our instances for async targets
        /// </summary>
        /// <param name="FlushTrigger">Value to set for our flush routines</param>
        public static void SetFlushTrigger(int FlushTrigger)
        {
            // Find our main logger
            LogBroker.Logger?.WriteLog("[ASYNC_SETUP] ::: SETTING NEW ASYNC TARGET FLUSH TRIGGER VALUE!", LogType.InfoLog);
            LogBroker.Logger?.WriteLog($"[ASYNC_SETUP] ::: CURRENT FLUSH VALUE:   {_forceFlushValue}");
            LogBroker.Logger?.WriteLog($"[ASYNC_SETUP] ::: NEWLY SET FLUSH VALUE: {FlushTrigger}");

            // Store the value here
            _forceFlushValue = FlushTrigger; if (FlushTrigger > 0) return;
            LogBroker.Logger?.WriteLog("[ASYNC_SETUP] ::: NEGATIVE REFRESH INTERVAL HAS BEEN PROCESSED! NOT USING FORCE TRIGGER TO FLUSH!", LogType.WarnLog);
        }

        // -------------------------------------- LOG WRITING ACTIONS -----------------------------

        /// <summary>
        /// Methods for writing log values
        /// </summary>
        /// <param name="LogMessage">Message to write</param>
        /// <param name="Level">Level to log</param>
        public virtual void WriteLog(string LogMessage, LogType Level = LogType.DebugLog)
        {
            // Set Context here and store
            string ClassName = this.GetCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short", 
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

            // Write value and flush outputs
            this.NLogger.Log(Level.ToNLevel(), LogMessage);
            this.FlushAsyncLogger();
        }
        /// <summary>
        /// Writes an exceptions contents out to the logger
        /// </summary>
        /// <param name="Ex">Ex to write</param>
        /// <param name="Level">Level to log it</param>
        public virtual void WriteLog(Exception Ex, LogType Level = LogType.ErrorLog)
        {
            // Set Context here and store
            if (Ex == null) return;
            string ClassName = this.GetCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short",
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

            // Write Exception values
            this.NLogger.Log(Level.ToNLevel(), $"Ex Message {Ex.Message}");
            this.NLogger.Log(Level.ToNLevel(), $"Ex Source  {Ex.Source}");
            this.NLogger.Log(Level.ToNLevel(), $"Ex Target  {Ex.TargetSite.Name}");
            
            // Write the Ex Stack trace
            if (Ex.StackTrace == null) { this.FlushAsyncLogger(); return; }
            this.NLogger.Log(Level.ToNLevel(), $"Ex Stack\n{Ex.StackTrace}");

            // If our inner exception is not null, run it through this logger.
            if (Ex.InnerException == null) { this.FlushAsyncLogger(); return; }
            this.NLogger.Log(Level.ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW");
            this.WriteLog(Ex.InnerException, Level);

            // Flush outputs for Async targets
            this.FlushAsyncLogger();
        }
        /// <summary>
        /// Writes an exception object out.
        /// </summary>
        /// <param name="MessageExInfo">Info message</param>
        /// <param name="Ex">Ex to write</param>
        /// <param name="LevelTypes">Levels. Msg and then Ex</param>
        public virtual void WriteLog(string MessageExInfo, Exception Ex, LogType[] LevelTypes = null)
        {
            // Check level count.
            if (Ex == null) return;
            if (LevelTypes == null) { LevelTypes = new LogType[2] { LogType.ErrorLog, LogType.ErrorLog }; }
            if (LevelTypes.Length == 1) { LevelTypes = LevelTypes.Append(LevelTypes[0]).ToArray(); }

            // Store Calling Class
            // Set Context here and store
            string ClassName = this.GetCallingClass();
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", ClassName);
            MappedDiagnosticsContext.Set("calling-class-short",
                ClassName.Contains('.') ? ClassName.Split('.').Last() : ClassName);

            // Write Log Message then exception
            this.NLogger.Log(LevelTypes[0].ToNLevel(), MessageExInfo);
            this.NLogger.Log(LevelTypes[0].ToNLevel(), $"EXCEPTION THROWN FROM {Ex.TargetSite}. DETAILS ARE SHOWN BELOW");

            // Write Exception
            this.NLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX MESSAGE {Ex.Message}");
            this.NLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX SOURCE  {Ex?.Source}");
            this.NLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX TARGET  {Ex.TargetSite?.Name}");

            // Write the Ex Stack trace
            if (Ex.StackTrace == null) { this.NLogger.Log(LevelTypes[1].ToNLevel(), "FURTHER DIAGNOSTIC INFO IS NOT AVALIABLE AT THIS TIME."); }
            else { this.NLogger.Log(LevelTypes[1].ToNLevel(), $"\tEX STACK\n{Ex.StackTrace.Replace("\n", "\n\t")}"); }

            // If our inner exception is not null, run it through this logger.
            if (Ex.InnerException == null) { this.FlushAsyncLogger(); return; }
            this.NLogger.Log(LevelTypes[1].ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW"); 
            this.WriteLog(MessageExInfo, Ex.InnerException, LevelTypes);

            // Flush outputs for Async targets
            this.FlushAsyncLogger();
        }

        /// <summary>
        /// Forces the async targets to flush their output when called
        /// </summary>
        private void FlushAsyncLogger()
        {
            // Make sure we're actually async here and tick our counter
            this._logOutputCount += 1;
            if (!this._isAsync) return;

            // Reset the counter, flush output
            if (_forceFlushValue > 0 && (this._logOutputCount != _forceFlushValue)) return; this._logOutputCount = 0;
            this.WrapperBuilt.Flush(FlushEx => this.WriteLog("[ASYNC_LOG] ::: ", FlushEx));
        }

        // --------------------------------- BASE CONFIG METHODS AND INFO --------------------------

        /// <summary>
        /// Writes all the logger info out to the current file.
        /// </summary>
        public virtual void PrintLoggerInfos(LogType LogType = LogType.DebugLog)
        {
            // Store a log type first.
            MappedDiagnosticsContext.Set("custom-name", this.LoggerName);
            MappedDiagnosticsContext.Set("calling-class", "StartupLogger (" + this.LoggerName + ")");
            MappedDiagnosticsContext.Set("calling-class-short", "StartupLogger");
            this.NLogger.Log(LogType.ToNLevel(), $"LOGGER NAME: {this.LoggerName}");
            this.NLogger.Log(LogType.ToNLevel(), $"--> TIME STARTED:  {this.TimeStarted}");
            this.NLogger.Log(LogType.ToNLevel(), $"--> LOGGER GUID:   {this.LoggerGuid.ToString("D").ToUpper()}");
        }
        /// <summary>
        /// Gets the name of the calling method.
        /// </summary>
        /// <returns>String of the full method name.</returns>
        public string GetCallingClass(bool SplitString = false)
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
