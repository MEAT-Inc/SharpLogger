using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    public class FileLogger : BaseLogger
    {
        // File Paths for logger
        public string LoggerFile;           // Path of the logger file.
        public string OutputPath;           // Base output path.

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new falcon file logging object.
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        internal FileLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5, bool UseAsync = false) : base(LoggerActions.FileLogger, LoggerName, MinLevel, MaxLevel)
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
                this.LoggerFile =
                    LogBroker.MainLogFileName ??
                    Path.Combine(this.OutputPath, $"{this.LoggerName}_LoggerOutput_{DateTime.Now.ToString("ddMMyyy-hhmmss")}.log");
            }

            // Build target object and check for Async use cases
            this._isAsync = UseAsync;
            var FileTarget = LoggerConfiguration.GenerateFileLogger(LoggerName);
            if (this._isAsync) this.WrapperBuilt = LoggerConfiguration.ConvertToAsyncTarget(FileTarget, MinLevel);

            // Build Logger object now.
            LogManager.Configuration.AddRule(
                LogLevel.FromOrdinal(MinLevel),
                LogLevel.FromOrdinal(MaxLevel),
                this._isAsync ? WrapperBuilt : FileTarget, LoggerName, false);

            // Store configuration and print updated logger information
            LogManager.ReconfigExistingLoggers();
            this.LoggingConfig = LogManager.Configuration;
            this.NLogger = LogManager.GetCurrentClassLogger();
            this.PrintLoggerInfos();
        }

        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes all the logger info out to the current file.
        /// </summary>
        /// <summary>
        /// Writes all the logger info out to the current file.
        /// </summary>
        public override void PrintLoggerInfos(LogType LogType = LogType.DebugLog)
        {
            base.PrintLoggerInfos();
            this.NLogger.Log(LogType.ToNLevel(), $"--> LOGGER FILE:  {new FileInfo(this.LoggerFile).Name}");
            this.NLogger.Log(LogType.ToNLevel(), $"--> LOGGER PATH:  {this.OutputPath}");
        }
    }
}
