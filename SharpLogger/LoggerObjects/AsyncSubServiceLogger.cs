using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Targets.Wrappers;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    /// <summary>
    /// Async task wrapped logger for async output operations.
    /// </summary>
    public class AsyncSubServiceLogger : BaseLogger
    {
        // File Paths for logger
        public string LoggerFile;           // Path of the logger file.
        public string OutputPath;           // Base output path.

        // Async Target object
        private int _logOutputCount = 0;                    // Counter for how many iterations of write were called
        private readonly int _forceFlushValue = 50;         // Force flush operations when the number of writes is this value   
        public readonly AsyncTargetWrapper WrapperBuilt;    // Target object itself

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new falcon file logging object.
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        public AsyncSubServiceLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5) : base(LoggerActions.AsyncSubServiceLogger, LoggerName, MinLevel, MaxLevel)
        {
            // Store path and file name here.
            if (!string.IsNullOrEmpty(LogFileName))
            {
                // Store values here.
                this.LoggerFile = LogFileName;
                int SplitNameLength = this.LoggerFile.Split('\\').Length - 2;
                this.OutputPath = this.LoggerFile.Split(Path.DirectorySeparatorChar).Take(SplitNameLength).ToString();
            }
            else
            {
                // Generate Dynamic values.
                this.OutputPath = LogBroker.BaseOutputPath;
                this.LoggerFile = Path.Combine(
                    this.OutputPath,
                    $"{this.LoggerName}_LoggerOutput_{DateTime.Now.ToString("ddMMyyy-hhmmss")}.log"
                );
            }


            // Convert to an Async wrapper for our target if specified
            var ConsoleTarget = LoggerConfiguration.GenerateConsoleLogger(LoggerName);
            this.WrapperBuilt = LoggerConfiguration.ConvertToAsyncTarget(ConsoleTarget, MinLevel);

            // Build Logger object now.
            this.LoggingConfig = LogManager.Configuration;
            this.LoggingConfig.AddRule(
                LogLevel.Warn, LogLevel.Fatal,
                ConsoleTarget, LoggerName, false
            );

            // Store configuration
            LogManager.Configuration = this.LoggingConfig;
            this.NLogger = LogManager.GetLogger(LoggerName);
            this.PrintLoggerInfos();
        }

        /// <summary>
        /// Overrides our default write operation to include a flush at the end of our writing.
        /// </summary>
        /// <param name="LogMessage"></param>
        /// <param name="Level"></param>
        public override void WriteLog(string LogMessage, LogType Level = LogType.DebugLog)
        {
            // Write base value log output and then flush our writer
            base.WriteLog(LogMessage, Level); this._logOutputCount += 1;

            // Check for our output counter value. If matched, flush output
            if (this._logOutputCount != _forceFlushValue) return;
            this.WrapperBuilt.Flush(FlushEx => base.WriteLog("[ASYNC LOGGER] ::: ASYNC FLUSH EXCEPTION", FlushEx));
            this._logOutputCount = 0;
        }
    }
}
