using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Fluent;
using SharpLogger.LogArchiving;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

namespace SharpLogger
{
    /// <summary>
    /// Base falcon logging broker object.
    /// </summary>
    public sealed class LogBroker
    {
        // Singleton instance configuration from the broker.
        private static LogBroker _brokerInstance;
        public static LogBroker BrokerInstance => _brokerInstance ?? (_brokerInstance = new LogBroker());

        // Logging infos.
        public static string MainLogFileName;
        public static string AppInstanceName;
        public static string BaseOutputPath;
        public static MasterLogger Logger;
        public static WatchdogLoggerQueue LoggerQueue = new WatchdogLoggerQueue();

        // Init Done or not.
        public static LogType MinLevel;
        public static LogType MaxLevel;

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new ERS Object and generates the logger output object.
        /// </summary>
        /// <param name="LoggerName"></param>
        private LogBroker()
        {
            // Setup App constants here.
            if (AppInstanceName == null) 
            {
                // Try and Set Process name. If Null, get the name of the called app
                var ProcessModule = Process.GetCurrentProcess().MainModule;
                AppInstanceName = ProcessModule != null
                    ? new FileInfo(ProcessModule.FileName).Name
                    : new FileInfo(Environment.GetCommandLineArgs()[0]).Name;
            }

            // Path to output and base file name.
            if (BaseOutputPath == null)
            {
                // Setup Outputs in the docs folder.
                string DocsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                BaseOutputPath = Path.Combine(DocsFolder, AppInstanceName + "_Logging");
            }

            // Get Root logger and build queue.
            if (MinLevel == default) MinLevel = LogType.TraceLog;
            if (MaxLevel == default) MaxLevel = LogType.FatalLog;
        }


        /// <summary>
        /// Stores the broker initial object values before calling the CTOR so the values provided may be used for configuration
        /// </summary>
        /// <param name="InstanceName">Name of the app being run.</param>
        /// <param name="BaseLogPath">Path to write output to.</param>
        public static void ConfigureLoggingSession(string InstanceName, string BaseLogPath, int MinLogLevel = 0, int MaxLogLevel = 5)
        {
            // Store values and Log Levels
            AppInstanceName = InstanceName;
            BaseOutputPath = BaseLogPath;

            // Store log values
            MinLevel = (LogType)MinLogLevel;
            MaxLevel = (LogType)MaxLogLevel;
        }
        /// <summary>
        /// Cleans out the log history objects for the current server
        /// <param name="ArchivePath">Path to store the archived files in</param>
        /// <param name="MaxFileCount">Max number of current logs to contain</param>
        /// <param name="ArchiveSetSize">Number of files to contain inside each archive file.</param>
        /// </summary>
        public static void CleanupLogHistory(string ArchiveConfigString, string FileNameFilter = "")
        {
            // Build an archive object here.
            LogArchiveConfiguration Config;
            Config = JsonConvert.DeserializeObject<LogArchiveConfiguration>(ArchiveConfigString);
            Logger?.WriteLog($"PULLED ARCHIVE CONFIG FROM JSON CONFIG FILE OK! JSON: \n{JsonConvert.SerializeObject(Config, Formatting.Indented)}", LogType.TraceLog);

            // Gets the lists of files in the log file directory and splits them into sets for archiving.
            if (FileNameFilter == "") { FileNameFilter = Process.GetCurrentProcess().ProcessName; }
            Logger?.WriteLog("CLEANING UP OLD FILES IN THE LOG OUTPUT DIRECTORY NOW...", LogType.InfoLog);
            string[] LogFilesLocated = Directory.GetFiles(BaseOutputPath).OrderBy(FileObj => new FileInfo(FileObj).CreationTime)
                .Where(FileObj => FileObj.Contains(FileNameFilter))
                .ToArray();

            // Remove 5 files from this list to keep current log files out.
            int RemainderFiles = LogFilesLocated.Length > 5 ? 5 : 0;
            LogFilesLocated = LogFilesLocated.Take(LogFilesLocated.Length - RemainderFiles).ToArray();
            List<string[]> LogFileArchiveSets = LogFilesLocated.Select((FileName, FileIndex) => new { Index = FileIndex, Value = FileName })
                .GroupBy(CurrentFile => CurrentFile.Index / Config.ArchiveFileSetSize)
                .Select(FileSet => FileSet.Select(FileValue => FileValue.Value).ToArray())
                .ToList();

            // Build output dir.
            Directory.CreateDirectory(Config.LogArchivePath);
            Logger?.WriteLog($"VERIFIED DIRECTORY {Config.LogArchivePath} EXISTS FOR OUTPUT GZ FILES!", LogType.InfoLog);
            Logger?.WriteLog($"FOUND A TOTAL OF {LogFilesLocated.Length} FILES AND SPLIT THEM INTO A TOTAL OF {LogFileArchiveSets.Count} SETS OF FILES", LogType.InfoLog);

            // Now loop each set, build a new Archiver and get output objects.
            LogArchiver.ArchiveConfig = Config;
            Logger?.WriteLog("STORED CONFIG FOR ARCHIVES OK! KICKING OFF LOG ARCHIVAL PROCESS IN A BACKGROUND THREAD NOW...", LogType.WarnLog);

            // Run this in a task so we don't hang up the whole main operation of the API
            Task.Run(() =>
            {
                // Run loop on all file set objects
                foreach (var LogFileSet in LogFileArchiveSets)
                {
                    // Build Archiver here then build compressed set.
                    var ArchiveBuilder = new LogArchiver(LogFileSet);
                    string ArchiveName = new FileInfo(ArchiveBuilder.OutputFileName).Name;
                    Logger?.WriteLog($"[{ArchiveName}] --> PULLING ARCHIVE OBJECT TO USE NOW...", LogType.TraceLog);

                    // Get archive object here and then store file information into it.
                    ZipArchive OutputArchive;
                    Logger?.WriteLog($"[{ArchiveName}] --> COMPRESSION FOR FILE SET STARTING NOW...", LogType.TraceLog);

                    try
                    {
                        // Write entries for the files into the archiver now.
                        if (ArchiveBuilder.CompressFiles(out OutputArchive)) Logger?.WriteLog($"[{ArchiveName}] --> GENERATED NEW ZIP FILE OK!", LogType.InfoLog);
                        else Logger?.WriteLog($"[{ArchiveName}] --> FAILED TO WRITE LOG ENTRIES FOR ARCHIVE SET!", LogType.ErrorLog);
                    }
                    catch (Exception CompressEx)
                    {
                        // Log failure out
                        Logger?.WriteLog($"FAILED TO COMPRESS ARCHIVE {ArchiveName}!", LogType.ErrorLog);
                        Logger?.WriteLog("EXCEPTION THROWN DURING COMPRESSION!", CompressEx);
                    }
                }

                // Now once done, configure the logging archive cleanup process
                Logger?.WriteLog("CLEANING UP ARCHIVE SETS NOW...", LogType.WarnLog);
                Logger?.WriteLog($"DESIRED TO KEEP A TOTAL OF {Config.ArchiveCleanupFileCount} ARCHIVE OVERALL!", LogType.WarnLog);

                // Run the cleanup routine
                CleanupArchiveHistory(Config.LogArchivePath, FileNameFilter, Config.ArchiveCleanupFileCount);
                Logger?.WriteLog("DONE CLEANING UP LOGGING OUTPUT FOR THE ARCHIVE FOLDER AND CURRENT LOG FILES!", LogType.InfoLog);
            });
        }
        /// <summary>
        /// Cleans out the log history objects for the current server
        /// <param name="ArchivePath">Path to store the archived files in</param>
        /// <param name="MaxFileCount">Max number of current logs to contain</param>
        /// <param name="ArchiveSetSize">Number of files to contain inside each archive file.</param>
        /// </summary>
        public static void CleanupArchiveHistory(string LogArchivePath, string ArchiveNameFilter = "", int ArchiveLimit = 50)
        {
            // Pull files out of the archive directory now
            var LogArchivesLocated = Directory.GetFiles(LogArchivePath, "*.zip*", SearchOption.AllDirectories);
            if (ArchiveNameFilter != "")
            {
                Logger?.WriteLog($"ARCHIVE FILTERING BY NAME IS IN EFFECT! FILTERING WITH {ArchiveNameFilter}", LogType.WarnLog);
                LogArchivesLocated = LogArchivesLocated.Where(FileObj => FileObj.Contains(ArchiveNameFilter)).ToArray();
            }
            
            // Log how many files are found
            Logger?.WriteLog($"PULLED A TOTAL OF {LogArchivesLocated.Length} LOG ARCHIVE OBJECTS", LogType.InfoLog);
            if (LogArchivesLocated.Length < ArchiveLimit)
            {
                // If less than the limit return out
                Logger?.WriteLog($"NOT CLEANING OUT ARCHIVES SINCE OUR ARCHIVE COUNT IS LESS THAN OUR SPECIFIED VALUE OF {ArchiveLimit}", LogType.WarnLog);
                return;
            }

            // Locate the archive sets to keep
            var ArchiveSetsToKeep = LogArchivesLocated
                .OrderBy(ArchiveObj => new FileInfo(ArchiveObj).LastWriteTime)
                .Reverse().Take(ArchiveLimit).ToArray();
            var ArchiveSetsToRemove = LogArchivesLocated
                .Where(ArchiveObj => !ArchiveSetsToKeep.Contains(ArchiveObj))
                .ToArray();

            // Now delete the ones we don't want to use.
            Logger?.WriteLog($"REMOVING A TOTAL OF {ArchiveSetsToRemove.Length} ARCHIVE FILES NOW...", LogType.InfoLog);
            Logger?.WriteLog("RUNNING REMOVAL OPERATION IN BACKGROUND TO KEEP MAIN THREADS ALIVE AND WELL!", LogType.WarnLog);

            // Run this in a task so we don't hang up the whole main operation of the API
            Task.Run(() =>
            {
                // Run loop on all file set objects
                foreach (var LogArchiveSet in ArchiveSetsToRemove)
                {
                    // Log information
                    Logger?.WriteLog($"REMOVING ARCHIVE OBJECT {LogArchiveSet} NOW...", LogType.TraceLog);

                    // Try and remove the file. If failed log so
                    try { File.Delete(LogArchiveSet); }
                    catch { Logger?.WriteLog($"FAILED TO REMOVE ARCHIVE SET: {LogArchiveSet}!! THIS IS WEIRD!", LogType.WarnLog); }
                }
            });
        }


        /// <summary>
        /// Actually spins up a new logger object once the broker is initialized.
        /// </summary>
        public void FillBrokerPool()
        {
            // DO NOT RUN THIS MORE THAN ONCE!
            if (Logger != null) { return; }

            // Make a new NLogger Config
            if (LogManager.Configuration == null) LogManager.Configuration = new LoggingConfiguration();

            // Build logger object now.
            MainLogFileName = Path.Combine(BaseOutputPath, $"{AppInstanceName}_Logging_{DateTime.Now.ToString("MMddyyy-HHmmss")}.log");
            Logger = new MasterLogger(
                $"{AppInstanceName}",
                MainLogFileName,
                (int)MinLevel,
                (int)MaxLevel
            );

            // Build and add to queue.
            LoggerQueue.AddLoggerToPool(Logger);

            // Log output info for the current DLL Assy
            string AssyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.WriteLog("LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!", LogType.WarnLog);
            Logger.WriteLog($"--> TIME OF DLL INIT: {DateTime.Now.ToString("g")}", LogType.InfoLog);
            Logger.WriteLog($"--> DLL ASSEMBLY VER: {AssyVersion}", LogType.InfoLog);
            Logger.WriteLog($"--> HAPPY LOGGING. LETS HOPE EVERYTHING GOES WELL...", LogType.InfoLog);
        }
    }
}
