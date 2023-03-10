using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SharpLogging
{
    /// <summary>
    /// Class object which is used to generate compressed files from log file sets
    /// Use this to cleanup logging outputs 
    /// </summary>
    public static class SharpLogArchiver
    {
        #region Custom Events

        // Event to process progress changed and done archiving.
        public static event EventHandler<ArchiveProgressEventArgs> FileOperationFailure;
        public static event EventHandler<ArchiveProgressEventArgs> FileAddedToArchive;
        public static event EventHandler<ArchiveProgressEventArgs> ArchiveCompleted;

        /// <summary>
        /// Event trigger for progress on the log file archive process
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        private static void OnFileAddedToArchive(ArchiveProgressEventArgs e)
        {
            // Write some info about this archive process here.
            string NameOnly = Path.GetFileNameWithoutExtension(e.FileAdded);
            _archiveLogger.WriteLog($"[{e.ArchiveFileName}] ::: ADDED {NameOnly} TO ARCHIVE OK! ({e.FilesRemaining} FILES LEFT. {e.PercentDone:F2}%)", LogType.TraceLog);
            FileAddedToArchive?.Invoke(e.ArchiveZipOutput, e);
        }
        /// <summary>
        /// Event trigger for when an archive is done.
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        private static void OnArchiveCompleted(ArchiveProgressEventArgs e)
        {
            // Write some info about this archive process here.
            long FileSizeString = new FileInfo(e.ArchiveOutputFile).Length;
            string TimeSpentString = e.TimeSpentRunning.ToString("mm\\:ss\\:fff");
            _archiveLogger.WriteLog($"[{e.ArchiveFileName}] ::: ARCHIVE WAS BUILT OK! WROTE OUT {FileSizeString} BYES IN {TimeSpentString}", LogType.InfoLog);
            ArchiveCompleted?.Invoke(e.ArchiveZipOutput, e);
        }
        /// <summary>
        /// Event trigger for progress on file stream compression built.
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        private static void OnFileOperationFailure(ArchiveProgressEventArgs e, Exception ExThrown)
        {
            // Write some info about this archive process here.
            _archiveLogger.WriteLog($"[{e.ArchiveFileName}] ::: FAILED TO PERFORM OPERATION ON FILE!", LogType.ErrorLog);
            _archiveLogger.WriteException($"[{e.ArchiveFileName}] ::: EXCEPTION THROWN!", ExThrown);
            FileOperationFailure?.Invoke(e.ArchiveZipOutput, e);
        }

        #endregion //Custom Events

        #region Fields
        
        // Private logger instance and backing archive configuration
        private static SharpLogger _archiveLogger;                                 // SharpLogger instance used to write information about archiving
        private static ArchiveConfiguration _logArchiveConfig;                     // Configuration to track how to archive our output files

        // Private backing fields for archive configuration values
        private static Stopwatch _archiveTimer;                                    // Timer to track archive routine execution time
        private static DateTime _archiverCreated;                                  // The time our archiver instance was built
        private static bool _logArchiverInitialized;                               // Tells us if a log archiver instance is built and setup or not
        private static List<string> _archiveOutputFiles;                           // List of all the file names being built by this archiver
        private static Dictionary<string, string[]> _archivedFileSets;             // List of all files in each archive set
        private static Dictionary<string, ZipArchive> _archiveZipOutputs;          // List of all the zipArchive objects built

        #endregion //Fields

        #region Properties

        // Instance properties used to configure new archive objects and routines 
        public static bool LogArchiverInitialized
        {
            get => _logArchiverInitialized;
            private set => _logArchiverInitialized = value;
        }
        public static ArchiveConfiguration LogArchiveConfig
        {
            get => _logArchiveConfig;
            internal set => _logArchiveConfig = value;
        }
        public static string[] ArchiveOutputFiles => _archiveOutputFiles.ToArray();
        
        #endregion //Properties

        #region Structs and Classes

        /// <summary>
        /// Class which contains configuration type info for an archive session
        /// </summary>
        public struct ArchiveConfiguration
        {
            #region Custom Events
            #endregion //Custom Events

            #region Fields

            // Public facing field for the archive location and the location to search
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string SearchPath = AppDomain.CurrentDomain.BaseDirectory;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string ArchivePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogArchives");

            // The default file filtering name value for an archive configuration
            [DefaultValue("*.*")] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string ArchiveFileFilter = $"{SharpLogBroker.LogBrokerName}.*";

            // Public facing fields for configuring a log archive session
            [DefaultValue(15)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveFileSetSize = 15;
            [DefaultValue(20)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveOnFileCount = 20;
            [DefaultValue(50)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveCleanupFileCount = 50;

            // Public facing fields to configure compression types for log archives
            [JsonIgnore] public CompressionLevel CompressionLevel = CompressionLevel.Optimal;
            [JsonIgnore] public CompressionType CompressionStyle = CompressionType.ZipCompression;

            #endregion //Fields

            #region Properties

            // Private properties used to help build JSON configuration objects for archives
            [DefaultValue("Optimal")] 
            [JsonProperty("CompressionLevel", DefaultValueHandling = DefaultValueHandling.Populate)]
            private string _compressionLevel => CompressionLevel.ToString();
            [DefaultValue("ZipCompression")] 
            [JsonProperty("CompressionStyle", DefaultValueHandling = DefaultValueHandling.Populate)] 
            private string _compressionStyle => CompressionStyle.ToString();

            #endregion //Properties

            #region Structs and Classes
            #endregion //Structs and Classes

            // ------------------------------------------------------------------------------------------------------------------------------------------

            /// <summary>
            /// Builds a new instance of a log archive configuration object.
            /// </summary>
            public ArchiveConfiguration() { }
        }
        /// <summary>
        /// Event arguments for updating the current archive object.
        /// </summary>
        public class ArchiveProgressEventArgs : EventArgs
        {
            #region Custom Events
            #endregion //Custom Events

            #region Fields

            // Public fields that hold information about the archiver
            public readonly string ArchiveOutputFile;
            public readonly ZipArchive ArchiveZipOutput;

            // Information about the next file added into our archive and time elapsed
            public readonly string FileAdded;
            public readonly string[] CombinedFileSet;
            public readonly TimeSpan TimeSpentRunning;

            #endregion //Fields

            #region Properties

            // Public facing properties holding information about this archive
            public string ArchiveFileName => new FileInfo(ArchiveOutputFile).Name;
            public string[] PreviousFiles => CombinedFileSet.TakeWhile(FileObj => new FileInfo(FileObj).Name != new FileInfo(FileAdded).Name).ToArray();
            public string[] RemainingFiles => CombinedFileSet.SkipWhile(FileObj => new FileInfo(FileObj).Name != new FileInfo(FileAdded).Name).ToArray();

            // Public facing properties holding information about the progress of this operation
            public int FilesAdded => ArchiveZipOutput.Entries.Count;
            public int FilesRemaining => NumberOfFilesToArchive - FilesAdded;
            public int NumberOfFilesToArchive => CombinedFileSet.Length;
            public double PercentDone => ((double)FilesAdded / (double)NumberOfFilesToArchive) * 100;

            #endregion //Properties

            #region Structs and Classes
            #endregion //Structs and Classes

            // --------------------------------------------------------------------------------------------------------------------------------------

            /// <summary>
            /// Builds a new event arg object for an added file entry on the archiver.
            /// </summary>
            /// <param name="CurrentFile">The file which was just added to this archive</param>
            internal ArchiveProgressEventArgs(string ParentZipFile, string CurrentFile)
            {
                // Store values here. Most of these are pulled by class instances.
                FileAdded = CurrentFile;
                ArchiveOutputFile = ParentZipFile;
                TimeSpentRunning = _archiveTimer.Elapsed;
                CombinedFileSet = _archivedFileSets[ParentZipFile];
                ArchiveZipOutput = _archiveZipOutputs[ParentZipFile];
            }
        }

        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Overrides the ToString call on a log archiver instance to write out information about it
        /// </summary>
        public new static string ToString()
        {
            // Make sure the log broker is built before doing this 
            if (!SharpLogBroker.LogBrokerInitialized || !LogArchiverInitialized)
                throw new InvalidOperationException("Error! Please configure the SharpLogBroker And SharpLogArchiver before using archives!");

            // Build the output string to return based on properties
            string OutputString =
                $"Log Archiver Information - '{SharpLogBroker.LogBrokerName} (Archives)' - Version {Assembly.GetExecutingAssembly().GetName().Version}\n" +
                $"\t\\__ Archiver State:  {(_logArchiverInitialized ? "Archiver Ready!" : "Archiver Not Configured!")}\n" +
                $"\t\\__ Creation Time:   {_archiverCreated:g}\n" +
                $"\t\\__ Archive Size:    {LogArchiveConfig.ArchiveFileSetSize} file{(LogArchiveConfig.ArchiveFileSetSize != 1 ? "s" : string.Empty)}\n" +
                $"\t\\__ Trigger Count:   {LogArchiveConfig.ArchiveOnFileCount} file{(LogArchiveConfig.ArchiveOnFileCount != 1 ? "s" : string.Empty)}\n" +
                $"\t\\__ Max Archives:    {LogArchiveConfig.ArchiveCleanupFileCount} archive{(LogArchiveConfig.ArchiveCleanupFileCount != 1 ? "s" : string.Empty)}\n" +
                $"\t\\__ Search Filter:   {LogArchiveConfig.ArchiveFileFilter}\n" +
                $"\t\\__ Search Path:     {LogArchiveConfig.SearchPath}\n" +
                $"\t\\__ Archive Path:    {LogArchiveConfig.ArchivePath}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Archive Logger:  {_archiveLogger.LoggerName}\n" + 
                $"\t\\__ Logger Targets:  {_archiveLogger.LoggerType}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n" +
                $"\t\\__ Archiver Config (JSON):\n\t\t{JsonConvert.SerializeObject(LogArchiveConfig, Formatting.Indented).Replace("\n", "\n\t\t")}\n" +
                $"\t{string.Join(string.Empty, Enumerable.Repeat('-', 100))}\n";

            // Return this built output string here
            return OutputString;
        }
        /// <summary>
        /// Configures a new session for Log archiving based on the configuration value provided in here
        /// </summary>
        /// <param name="ArchiveConfig">The configuration to spawn in with for archiving</param>
        /// <returns>True if the configuration is loaded and usable. False if not</returns>
        public static bool InitializeArchiving(ArchiveConfiguration ArchiveConfig)
        {
            // If the log broker is not built, then we can't run this routine. 
            if (!SharpLogBroker.LogBrokerInitialized)
                throw new InvalidOperationException("Error! Please configure the SharpLogBroker before configuring archiving!");

            // Configure new values for the archive timer, the archived files, and build a new configuration
            _archiveTimer = new Stopwatch();
            _archiverCreated = DateTime.Now;
            _logArchiveConfig = ArchiveConfig;

            // Configure our backing fields here
            _archiveOutputFiles = new List<string>();
            _archivedFileSets = new Dictionary<string, string[]>();
            _archiveZipOutputs = new Dictionary<string, ZipArchive>();

            // Make sure we've got a valid log archive configuration value built out here for filtering files
            if (_logArchiveConfig.ArchiveFileSetSize <= 0) _logArchiveConfig.ArchiveFileSetSize = 15;
            if (_logArchiveConfig.ArchiveOnFileCount <= 0) _logArchiveConfig.ArchiveOnFileCount = 20;
            if (_logArchiveConfig.ArchiveCleanupFileCount <= 0) _logArchiveConfig.ArchiveCleanupFileCount = 50;
            if (string.IsNullOrWhiteSpace(_logArchiveConfig.ArchivePath))
                _logArchiveConfig.ArchivePath = Path.GetFullPath(SharpLogBroker.LogFileFolder);
            if (string.IsNullOrWhiteSpace(_logArchiveConfig.SearchPath))
                _logArchiveConfig.SearchPath = Path.GetFullPath(SharpLogBroker.LogFileFolder);
            if (_logArchiveConfig.SearchPath == _logArchiveConfig.ArchivePath)
                _logArchiveConfig.ArchivePath = Path.GetFullPath(Path.Combine(_logArchiveConfig.ArchivePath, "LogArchives"));
            if (string.IsNullOrWhiteSpace(_logArchiveConfig.ArchiveFileFilter))
            {
                // Find the needed splitting character for the search filter to build and build the new filter value
                char SplittingChar = SharpLogBroker.LogFileName.Contains("_") ? '_' : '.';
                _logArchiveConfig.ArchiveFileFilter = $"{SharpLogBroker.LogFileName.Split(SplittingChar).FirstOrDefault()}*.*";
            }

            // Ensure our new archive configuration values can be used for this routine
            if (_logArchiveConfig.SearchPath == null || !Directory.Exists(_logArchiveConfig.SearchPath)) return false;

            // Configure a new logger for this archive helper. Then try to build sets of files to archive
            _logArchiverInitialized = true;
            _archiveLogger = new SharpLogger(LoggerActions.UniversalLogger, "LogArchiverLogger");
            _archiveLogger.WriteLog("ARCHIVE HELPER BUILT WITHOUT ISSUES! READY TO PULL IN ARCHIVES USING PROVIDED CONFIGURATION!", LogType.InfoLog);
            _archiveLogger.WriteLog($"SHOWING LOG ARCHIVER STATE AND CONFIGURATION BELOW\n\n{ToString()}", LogType.TraceLog);

            // Find all the files to be archived now and setup triggers for file counts
            _archiveLogger.WriteLog($"ATTEMPTING TO BUILD ARCHIVE SETS FOR INPUT PATH: {_logArchiveConfig.SearchPath}...", LogType.WarnLog);
            IEnumerable<string[]> LogFileArchiveSets = Directory
                .GetFiles(_logArchiveConfig.SearchPath, _logArchiveConfig.ArchiveFileFilter)
                .OrderBy(FileFound => new FileInfo(FileFound).CreationTime)
                .Select((FileName, FileIndex) => new { Index = FileIndex, Value = FileName })
                .GroupBy(CurrentFile => CurrentFile.Index / _logArchiveConfig.ArchiveFileSetSize)
                .Select(FileSet => FileSet.Select(FileValue => FileValue.Value).ToArray())
                .Where(ArchiveSet => ArchiveSet.Count() >= _logArchiveConfig.ArchiveFileSetSize).ToList();

            // If no archive sets were built, then log that and move o
            if (!LogFileArchiveSets.Any())
            {
                // Log no sets were created and exit out of this method
                _archiveLogger.WriteLog("NO LOG FILE ARCHIVE SETS COULD BE BUILT! THIS IS LIKELY BECAUSE THERE AREN'T ENOUGH FILES TO ARCHIVE!", LogType.WarnLog);
                return _logArchiverInitialized;
            }

            // Now loop through all the archive file sets to build and find names for them
            foreach (var ArchiveSet in LogFileArchiveSets)
            {
                // Find matches in the file names provided to setup what our range of files is
                Regex MatchTimeRegex = new Regex(@"(\d{2})(\d{2})(\d{4})-(\d{6})", RegexOptions.Compiled);
                Match FirstFileMatch = MatchTimeRegex.Match(Path.GetFileName(ArchiveSet.First()));
                Match LastFileMatch = MatchTimeRegex.Match(Path.GetFileName(ArchiveSet.Last()));

                // Get date values.
                string StartTime = $"{FirstFileMatch.Groups[1].Value}{FirstFileMatch.Groups[2].Value}{FirstFileMatch.Groups[3].Value.Substring(1)}-{FirstFileMatch.Groups[4].Value}";
                string StopTime = $"{LastFileMatch.Groups[1].Value}{LastFileMatch.Groups[2].Value}{LastFileMatch.Groups[3].Value.Substring(1)}-{LastFileMatch.Groups[4].Value}";
                string ArchiveExt = _logArchiveConfig.CompressionStyle == CompressionType.GZipCompression ? "gz" : "zip";
                string ArchiveFileName = $"{SharpLogBroker.LogBrokerName}_{StartTime}_{StopTime}.{ArchiveExt}";

                // Remove and recreate this output file if needed now
                Directory.CreateDirectory(_logArchiveConfig.ArchivePath);
                ArchiveFileName = Path.Combine(_logArchiveConfig.ArchivePath, ArchiveFileName);
                if (File.Exists(ArchiveFileName)) File.Delete(ArchiveFileName);

                // Now using the built archive file name, store it and the contents of this set on our class instance
                _archiveOutputFiles.Add(ArchiveFileName);
                _archivedFileSets.Add(ArchiveFileName, ArchiveSet.ToArray());

                // Finally build a new zip archive for this file set
                FileStream ArchiveStream = new FileStream(ArchiveFileName, FileMode.OpenOrCreate);
                ZipArchive ArchiveZipObject = new ZipArchive(ArchiveStream, ZipArchiveMode.Update);
                _archiveZipOutputs.Add(ArchiveFileName, ArchiveZipObject);

                // Log the next file set built and move on
                _archiveLogger.WriteLog($"--> BUILT ARCHIVE FILE NAME: {ArchiveFileName}");
                _archiveLogger.WriteLog($"--> ARCHIVE FILE WILL HOLD {ArchiveSet.Length} FILES STARTING FROM {ArchiveSet.First()} THROUGH {ArchiveSet.Last()}");
            }

            // Once our configuration is done, exit out with a passed state
            return _logArchiverInitialized;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Attempts to archive all log files found when this log archiver was built
        /// </summary>
        /// <returns>True if all archive objects were configured and built. False if any of them failed</returns>
        public static bool ArchiveLogFiles()
        {
            // Make sure archiving is configured first
            if (!_logArchiverInitialized)
            {
                // Log out that archiving is not configured and must be setup
                SharpLogBroker.MasterLogger.WriteLog("ERROR! THE LOG ARCHIVER HAS NOT YET BEEN CONFIGURED! PLEASE SET IT UP BEFORE ARCHIVING!");
                SharpLogBroker.MasterLogger.WriteException(new InvalidOperationException("Error! Please setup the SharpLogArchiver before using it"));
                return false;
            }

            // Log that we're starting to build output Archive Zip files now 
            _archiveLogger.WriteLog($"BUILDING ARCHIVE LOG FILE SETS FROM ARCHIVE PATH {LogArchiveConfig.SearchPath} NOW...", LogType.InfoLog);

            // Loop all the archive objects stored on this class instance and compress our file sets now
            _archiveTimer.Restart(); bool ArchivesPassed = true;
            foreach (var ArchiveOutputFile in ArchiveOutputFiles)
            {
                // Build new file information for each file and get our zip file value here
                ZipArchive ArchiveOutputZip = _archiveZipOutputs[ArchiveOutputFile];
                FileInfo[] ArchiveFileInfos = _archivedFileSets[ArchiveOutputFile]
                    .Select(FileObj => new FileInfo(FileObj))
                    .ToArray();

                // Now using the built ZipArchive from the collection of built Zips, compress the found file set
                _archiveLogger.WriteLog($"--> ARCHIVING FILE SET FOR OUTPUT ZIP NAMED: {ArchiveOutputFile} NOW...", LogType.DebugLog);
                using (ArchiveOutputZip)
                {
                    // Append all entries now.
                    foreach (var LogFileInfo in ArchiveFileInfos)
                    {
                        try
                        {
                            // Get name of file and use it for entry contents and then store it in our archive
                            string FullPath = LogFileInfo.FullName;
                            string NameOnly = Path.GetFileName(LogFileInfo.Name);
                            ArchiveOutputZip.CreateEntryFromFile(FullPath, NameOnly, CompressionLevel.Optimal);
                            OnFileAddedToArchive(new ArchiveProgressEventArgs(ArchiveOutputFile, FullPath));

                            // Now remove the old log file found and move onto the next file
                            File.Delete(LogFileInfo.FullName);
                        }
                        catch (Exception ArchiveException)
                        {
                            // Catch our failure and invoke a new event handler for it
                            var ArchiveFailedArgs = new ArchiveProgressEventArgs(ArchiveOutputFile, LogFileInfo.FullName);
                            OnFileOperationFailure(ArchiveFailedArgs, ArchiveException);

                            // Log our exception thrown during an archive process and exit out failed
                            _archiveLogger.WriteLog("--> FAILED TO BUILD NEW ZIP ARCHIVE! THIS IS A REAL WHAT THE FUCK MOMENT", LogType.ErrorLog);
                            _archiveLogger.WriteException("--> ERROR IS BEING LOGGED BELOW", ArchiveException);
                            ArchivesPassed = false;
                        }
                    }

                    // Send out a new archive done event once our archive has been built completely from the given file names
                    OnArchiveCompleted(new ArchiveProgressEventArgs(ArchiveOutputFile, ArchiveOutputFile));
                }
            }

            // Stop the timer for this operation and reset our lists of files being archived
            _archiveTimer.Stop();
            _archiveLogger.WriteLog($"ARCHIVED ALL FOUND LOG FILES CORRECTLY IN {_archiveTimer.Elapsed}", LogType.WarnLog);
            return ArchivesPassed && ArchiveOutputFiles.All(File.Exists);
        }
        /// <summary>
        /// Attempts to remove all archives from a log archive directory based on the number of files in the config trigger 
        /// </summary>
        /// <returns>True if all archive sets are removed, false if they are not</returns>
        public static bool CleanupArchiveHistory()
        {
            // Make sure archiving is configured first
            if (!_logArchiverInitialized)
            {
                // Log out that archiving is not configured and must be setup
                SharpLogBroker.MasterLogger.WriteLog("ERROR! THE LOG ARCHIVER HAS NOT YET BEEN CONFIGURED! PLEASE SET IT UP BEFORE ARCHIVING!");
                SharpLogBroker.MasterLogger.WriteException(new InvalidOperationException("Error! Please setup the SharpLogArchiver before using it"));
                return false;
            }

            // Build a correct filter for our archive values here and find all needed files
            string ExtensionValue = LogArchiveConfig.CompressionStyle == CompressionType.ZipCompression 
                ? $"{SharpLogBroker.LogBrokerName}*.zip".Trim()
                : $"{SharpLogBroker.LogBrokerName}*.gz".Trim();

            // Log that we're trying to prune/cleanup our archive files found from our archive path now
            var LogArchivesLocated = Directory
                .GetFiles(LogArchiveConfig.ArchivePath, ExtensionValue, SearchOption.AllDirectories)
                .OrderBy(ArchiveObj => new FileInfo(ArchiveObj).LastWriteTime)
                .Reverse();

            // Log our how many archives we found now and exit out of this routine if we're not at the trigger count
            if (LogArchivesLocated.Count() < LogArchiveConfig.ArchiveCleanupFileCount)
            {
                // If less than the limit return out
                int ArchiveLimit = LogArchiveConfig.ArchiveCleanupFileCount;
                _archiveLogger.WriteLog($"NOT CLEANING OUT ARCHIVES SINCE OUR ARCHIVE COUNT IS LESS THAN OUR SPECIFIED VALUE OF {ArchiveLimit}", LogType.WarnLog);
                _archiveLogger.WriteLog($"ONLY SAW A TOTAL OF {LogArchivesLocated.Count()} LOG ARCHIVES IN PATH {LogArchiveConfig.ArchivePath}", LogType.WarnLog);
                return true;
            }

            // Now locate and delete the archive sets found that aren't desired for the history based on our configuration
            var ArchiveSetsToRemove = LogArchivesLocated.Take(LogArchiveConfig.ArchiveCleanupFileCount);
            _archiveLogger.WriteLog($"FOUND A TOTAL OF {LogArchivesLocated.Count()} LOG ARCHIVES IN PATH {LogArchiveConfig.ArchivePath}", LogType.InfoLog);
            _archiveLogger.WriteLog($"REMOVING A TOTAL OF {ArchiveSetsToRemove.Count()} ARCHIVE FILES NOW...", LogType.InfoLog);
            _archiveLogger.WriteLog("RUNNING REMOVAL OPERATION IN BACKGROUND TO KEEP MAIN THREADS ALIVE AND WELL!", LogType.WarnLog);

            // Loop them all and delete each file set one by one
            bool CleanupPassed = true;
            foreach (var LogArchiveSet in ArchiveSetsToRemove)
            {
                // Log information about each archive file we're tossing our and try to delete it now
                _archiveLogger.WriteLog($"REMOVING ARCHIVE OBJECT {LogArchiveSet} NOW...", LogType.TraceLog);
                try { File.Delete(LogArchiveSet); }
                catch (Exception DeleteArchiveEx)
                {
                    // Log our exception thrown during an archive process and exit out failed
                    _archiveLogger.WriteLog($"FAILED TO REMOVE ARCHIVE SET: {LogArchiveSet}!! THIS IS WEIRD!", LogType.WarnLog);
                    _archiveLogger.WriteException("ERROR IS BEING LOGGED BELOW", DeleteArchiveEx);
                    CleanupPassed = false;
                }
            }
            
            // Return based on if any archives failed to delete or not
            return CleanupPassed;
        }
        /// <summary>
        /// Looks at all the child paths inside our main log folder and purges those folders out where requested.
        /// </summary>
        /// <param name="FolderFilter">The filter to use when looking at subfolders</param>
        /// <returns>True if the paths are cleaned up. False if not</returns>
        public static bool CleanupSubdirectories(string FolderFilter = "*Logs")
        {
            // Make sure archiving is configured first
            if (!_logArchiverInitialized)
            {
                // Log out that archiving is not configured and must be setup
                SharpLogBroker.MasterLogger.WriteLog("ERROR! THE LOG ARCHIVER HAS NOT YET BEEN CONFIGURED! PLEASE SET IT UP BEFORE ARCHIVING!");
                SharpLogBroker.MasterLogger.WriteException(new InvalidOperationException("Error! Please setup the SharpLogArchiver before using it"));
                return false;
            }

            // Find all directories in the main logging folder and purge them out now
            string[] LoggingSubFolders = Directory.GetDirectories(SharpLogBroker.LogFileFolder, FolderFilter);
            _archiveLogger.WriteLog($"FOUND A TOTAL OF {LoggingSubFolders.Length} SUBFOLDERS TO CHECK FOR LOGS TO PURGE!", LogType.InfoLog);
            foreach (var LoggingSubFolder in LoggingSubFolders)
            {
                // Log we're cleaning out this sub folder now and purge it
                string[] LoggingSubFiles = Directory.GetFiles(LoggingSubFolder);
                if (LoggingSubFiles.Length < LogArchiveConfig.ArchiveOnFileCount)
                {
                    // Log we're skipping this path value and move on
                    _archiveLogger.WriteLog($"--> NOT PURGING PATH {LoggingSubFolder}! ONLY {LoggingSubFiles.Length} FILES FOUND");
                    continue;
                }

                // Log that we're now purging this subfolder and remove all files where needed
                _archiveLogger.WriteLog($"--> PURGING SUBFOLDER {LoggingSubFolder}...");
                LoggingSubFiles = LoggingSubFiles
                    .OrderBy(FileFound => new FileInfo(FileFound).CreationTime)
                    .Take(LogArchiveConfig.ArchiveCleanupFileCount)
                    .ToArray();

                // Now remove every file from this newly built subset of file paths
                foreach (var LogFile in LoggingSubFiles)
                {
                    try
                    {
                        // Delete the file here in a try catch block
                        File.Delete(LogFile);
                    }
                    catch (Exception DeleteLogEx)
                    {
                        // Throw and log a failure out when this delete routine fails for some reason
                        _archiveLogger.WriteException($"ERROR! FAILED TO DELETE LOG FILE {LogFile}!", DeleteLogEx, LogType.ErrorLog);
                    }
                }
            }

            // Return true at this point
            _archiveLogger.WriteLog("CHECKED ALL LOGGING SUBFOLDER PATHS AND CLEANED UP WHERE NEEDED!", LogType.InfoLog);
            return true;
        }

    }
}
