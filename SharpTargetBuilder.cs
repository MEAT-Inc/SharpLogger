using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace SharpLogger
{
    internal static class SharpTargetBuilder
    {
        // Configuration Strings
        public static string BaseFormatConsole =
            "[${date:format=hh\\:mm\\:ss}][${level:uppercase=true}][${mdc:custom-name}][${mdc:item=calling-class-short}] ::: ${message}";
        public static string BaseFormatFile =
            "[${date:format=MM-dd-yyyy hh\\:mm\\:ss}][${level:uppercase=true}][${mdc:custom-name}][${mdc:item=calling-class}] ::: ${message}";

        // Logging setup for timer logging.
        public static string TimedFormatConsole =
            "[${date:format=hh\\:mm\\:ss}][${level:uppercase=true}][${mdc:custom-name}][${mdc:item=calling-class-short}] ::: [TIMER: ${mdc:item=stopwatch-time}] ::: ${message}";
        public static string TimedFormatFile =
            "[${date:format=MM-dd-yyyy hh\\:mm\\:ss}][${level:uppercase=true}][${mdc:custom-name}][${mdc:item=calling-class}] ::: [TIMER: ${mdc:item=stopwatch-time}] ::: ${message}";

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a colored console logger.
        /// </summary>
        /// <returns>Console Logging Object</returns>
        public static ColoredConsoleTarget GenerateConsoleLogger(string TargetName, string Format = null)
        {
            // Get formatting string.
            string FormatValue = Format ?? BaseFormatConsole;

            // Make Logger and set format.
            var ConsoleLogger = new ColoredConsoleTarget(TargetName);
            ConsoleLogger.Layout = new SimpleLayout(FormatValue);

            // Add Coloring Rules
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Trace",
                ConsoleOutputColor.DarkGray,
                ConsoleOutputColor.Black)
            );
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Debug",
                ConsoleOutputColor.Gray,
                ConsoleOutputColor.Black)
            );
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Info",
                ConsoleOutputColor.Green,
                ConsoleOutputColor.Black)
            );
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Warn",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.Yellow)
            );
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Error",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.Gray)
            );
            ConsoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                "level == LogLevel.Fatal",
                ConsoleOutputColor.Red,
                ConsoleOutputColor.White)
            );

            // Return Logger
            return ConsoleLogger;
        }
        /// <summary>
        /// Builds a new file logging target object.
        /// </summary>
        /// <param name="FileName">Name of file to log into</param>
        /// <param name="LogFormat">Format string.</param>
        /// <returns>File Logging target</returns>
        public static FileTarget GenerateFileLogger(string FileName, string Format = null)
        {
            // Get formatting string.
            string FormatValue = Format ?? BaseFormatFile;

            // Build Target
            var FileLogger = new FileTarget($"FileLogger_{FileName}");
            FileLogger.FileName = FileName;
            FileLogger.Layout = new SimpleLayout(FormatValue);
            FileLogger.ArchiveFileName = "${basedir}/LogArchives/" + FileName.Split('_')[0] + ".{####}.log";
            FileLogger.ArchiveEvery = FileArchivePeriod.Day;
            FileLogger.ArchiveNumbering = ArchiveNumberingMode.DateAndSequence;
            FileLogger.ArchiveAboveSize = 1953125;
            FileLogger.MaxArchiveFiles = 20;
            FileLogger.ConcurrentWrites = true;
            FileLogger.KeepFileOpen = false;

            // Return the logger
            return FileLogger;
        }

        /// <summary>
        /// Used to build a new logger object with builtin Async operations from the start.
        /// </summary>
        public static AsyncTargetWrapper ConvertToAsyncTarget(Target TargetToConvert, int MinLogLevel = 0)
        {
            // Build the wrapper object and convert our input target
            AsyncTargetWrapper AsyncWrapper = new AsyncTargetWrapper();

            // Configure properties of the async target object.
            AsyncWrapper.QueueLimit = 5000;
            AsyncWrapper.WrappedTarget = TargetToConvert;
            AsyncWrapper.OverflowAction = AsyncTargetWrapperOverflowAction.Grow;

            // Return the built target object.
            var LogLevelParsed = LogLevel.FromOrdinal(MinLogLevel);
            SimpleConfigurator.ConfigureForTargetLogging(AsyncWrapper, LogLevelParsed);
            return AsyncWrapper;
        }

        // ----------------------------------------------------------------------------------------------

    }
}
