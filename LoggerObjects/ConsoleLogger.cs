using System.Runtime.CompilerServices;
using NLog;
using SharpLogger.LoggerSupport;

namespace SharpLogger.LoggerObjects
{
    /// <summary>
    /// Builds a console logging object to show output with.
    /// </summary>
    public class ConsoleLogger : BaseLogger 
    {
        /// <summary>
        /// Builds a new falcon file logging object.
        /// </summary>
        /// <param name="LoggerName"></param>
        /// <param name="MinLevel"></param>
        /// <param name="MaxLevel"></param>
        public ConsoleLogger([CallerMemberName] string LoggerName = "", int MinLevel = 0, int MaxLevel = 5, bool UseAsync = false) : base(LoggerActions.ConsoleLogger, LoggerName, MinLevel, MaxLevel)
        {
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

            // Store configuration and print updated logger information
            this.NLogger = LogManager.GetCurrentClassLogger();
            this.PrintLoggerInfos();
        }
    }
}
