using System;
using NLog;

namespace SharpLogger
{
    /// <summary>
    /// Wrapped log level type so NLog isn't a required ref for anything that uses this.
    /// </summary>
    public enum LogType : int
    {
        // Basic logging level values
        TraceLog,   // Compares to LogLevel.Trac
        DebugLog,   // Compares to LogLevel.Debug
        InfoLog,    // Compares to LogLevel.Info
        WarnLog,    // Compares to LogLevel.Warn
        ErrorLog,   // Compares to LogLevel.Error
        FatalLog,   // Compares to LogLevel.Fatal
        NoLogging   // Compares to LogLevel.Off
    }
    /// <summary>
    /// Custom type of logger being used.
    /// </summary>
    public enum LoggerActions : int
    {
        // Main Logger Types
        ConsoleLogger = 0x01,          // Logger made to write to a Console window
        FileLogger = 0x02,             // Logger made to write to a file output
        SqlLogger = 0x04,              // Logger made to write to a SQL table
    }

    // ----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Extensions for log type objects to convert between Nlog levels and LogType levels
    /// </summary>
    internal static class SharpLoggerExtensions
    {
        /// <summary>
        /// Converts a LogType into a LogLevel for NLOG use.
        /// </summary>
        /// <param name="Level"></param>
        /// <returns>LogLevel Pulled out of here.</returns>
        public static LogLevel ToNLevel(this LogType Level)
        {
            // Find if the ordinal value is out of range or not. Default to the lowest level supported
            return (int)Level > 6
                ? SharpLogBroker.MinLevel
                : LogLevel.FromOrdinal((int)Level);
        }
        /// <summary>
        /// Converts a given NLogLevel into a LogType
        /// </summary>
        /// <param name="Level">Level to check</param>
        /// <returns>Gives back a default log type.</returns>
        public static LogType ToLogType(this LogLevel Level)
        {
            // Find if the ordinal value is out of range or not. Default to the lowest level supported
            return Level.Ordinal > 6 
                ? (LogType)SharpLogBroker.MinLevel.Ordinal
                : (LogType)Level.Ordinal;
        } 
    }
}
