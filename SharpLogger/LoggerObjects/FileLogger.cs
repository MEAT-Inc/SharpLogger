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
        public FileLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5) : base(LoggerActions.FileLogger, LoggerName, MinLevel, MaxLevel)
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
                // Check for broker file.
                if (LogBroker.MainLogFileName != null) this.LoggerFile = LogBroker.MainLogFileName;
                else
                {
                    // Generate Dynamic values.
                    this.OutputPath = LogBroker.BaseOutputPath;
                    this.LoggerFile = Path.Combine(
                        this.OutputPath,
                        $"{this.LoggerName}_LoggerOutput_{DateTime.Now.ToString("ddMMyyy-hhmmss")}.log"
                    );
                }
            }

            // Build Logger object now.
            this.LoggingConfig = LogManager.Configuration;
            this.LoggingConfig.AddRule(
                LogLevel.FromOrdinal(MinLevel),
                LogLevel.FromOrdinal(MaxLevel),
                LoggerConfiguration.GenerateFileLogger(this.LoggerFile));

            // Store configuration
            LogManager.Configuration = this.LoggingConfig;
            this.NLogger = LogManager.GetCurrentClassLogger();
            this.PrintLoggerInfos();
        }


        /// <summary>
        /// Writes all the logger info out to the current file.
        /// </summary>
        /// <summary>
        /// Writes all the logger info out to the current file.
        /// </summary>
        public override void PrintLoggerInfos(LogType LogType = LogType.DebugLog)
        {
            base.PrintLoggerInfos();
            this.NLogger.Log(LogType.ToNLevel(), $"--> LOGGER FILE:   {new FileInfo(this.LoggerFile).Name}");
            this.NLogger.Log(LogType.ToNLevel(), $"--> LOGGER PATH:   {this.OutputPath}");
        }
    }
}
