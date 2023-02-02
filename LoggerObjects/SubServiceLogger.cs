using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    /// <summary>
    /// Service logger. Basic logger object output
    /// </summary>
    public class SubServiceLogger : BaseLogger
    {
        // File Paths for logger
        public string LoggerFile;           // Path of the logger file.
        public string OutputPath;           // Base output path.

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new falcon file logging object.
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        /// <param name="UseAsync"></param>
        public SubServiceLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5, bool UseAsync = false) : base(LoggerActions.SubServiceLogger, LoggerName, MinLevel, MaxLevel)
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

            // Build target object and check for Async use cases
            this._isAsync = UseAsync;
            var ConsoleTarget = LoggerConfiguration.GenerateConsoleLogger(LoggerName);
            if (this._isAsync) this.WrapperBuilt = LoggerConfiguration.ConvertToAsyncTarget(ConsoleTarget, MinLevel);

            // Build Logger object now.
            this.LoggingConfig = LogManager.Configuration;
            this.LoggingConfig.AddRule(
                LogLevel.FromOrdinal(MinLevel), 
                LogLevel.FromOrdinal(MaxLevel),
                this._isAsync ? WrapperBuilt : ConsoleTarget, LoggerName, false
            );

            // Store configuration
            this.NLogger = LogManager.GetCurrentClassLogger();
            this.PrintLoggerInfos();
        }
    }
}
