using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NLog;
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

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new falcon file logging object.
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        public AsyncSubServiceLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5) : base(LoggerActions.AsyncServiceLogger, LoggerName, MinLevel, MaxLevel)
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
            LoggerConfiguration.ConvertToAsyncTarget(ConsoleTarget, MinLevel);

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
    }
}
