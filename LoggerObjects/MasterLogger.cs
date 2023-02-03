using System;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    /// <summary>
    /// Builds a logger object which can write to all possible targets at once.
    /// </summary>
    public class MasterLogger : BaseLogger
    {
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new master logger object. THERE SHOULD ONLY BE ONE OF THESE AT ANY POINT IN TIME!
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="LogFileName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        internal MasterLogger([CallerMemberName] string LoggerName = "", string LogFileName = "", int MinLevel = 0, int MaxLevel = 5, bool UseAsync = false) :
            base(LoggerActions.MasterLogger, LoggerName, MinLevel, MaxLevel)
        {
            // Check file name.
            if (string.IsNullOrEmpty(LogFileName))
            {
                // Generate Dynamic values.
                LogFileName =
                    LogBroker.MainLogFileName ??
                    Path.Combine(LogBroker.BaseOutputPath, $"{this.LoggerName}_LoggerOutput_{DateTime.Now.ToString("ddMMyyy-hhmmss")}.log");
            }

            // Build Master Logging Configuration.
            LogManager.Configuration.AddRule(
                LogLevel.FromOrdinal(MinLevel),
                LogLevel.FromOrdinal(MaxLevel),
                LoggerConfiguration.GenerateFileLogger(LogFileName));
            LogManager.Configuration.AddRule(
                LogLevel.FromOrdinal(MinLevel),
                LogLevel.FromOrdinal(MaxLevel),
                LoggerConfiguration.GenerateConsoleLogger(LoggerName));

            // Store config
            LogManager.ReconfigExistingLoggers();
            this.LoggingConfig = LogManager.Configuration; 
            this.NLogger = LogManager.GetCurrentClassLogger();
            this.PrintLoggerInfos();
        }
    }
}
