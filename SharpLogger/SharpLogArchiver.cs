using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpLogger
{
    /// <summary>
    /// Class object which is used to generate compressed files from log file sets
    /// Use this to cleanup logging outputs 
    /// </summary>
    public class SharpLogArchiver
    {
        #region Custom Events

        // Event to process progress changed and done archiving.
        public event EventHandler<ArchiveProgressEventArgs> FileOperationFailure;
        public event EventHandler<ArchiveProgressEventArgs> FileAddedToArchive;
        public event EventHandler<ArchiveProgressEventArgs> ArchiveCompleted;

        /// <summary>
        /// Event trigger for progress on the log file archive process
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        protected virtual void OnFileAddedToArchive(ArchiveProgressEventArgs e)
        {
            // Write some info about this archive process here.
            string NameOnly = Path.GetFileNameWithoutExtension(e.FileAdded);
            this._archiveLogger?.WriteLog($"[{e.ArchiveFileName}] ::: ADDED {NameOnly} TO ARCHIVE OK! ({e.FilesRemaining} FILES LEFT. {e.PercentDone:F2}%)", LogType.TraceLog);
            this.FileAddedToArchive?.Invoke(this, e);
        }
        /// <summary>
        /// Event trigger for when an archive is done.
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        protected virtual void OnArchiveCompleted(ArchiveProgressEventArgs e)
        {
            // Write some info about this archive process here.
            long FileSizeString = new FileInfo(e.ArchiveOutputFile).Length;
            string TimeSpentString = e.TimeSpentRunning.ToString("mm\\:ss\\:fff");
            this._archiveLogger?.WriteLog($"[{e.ArchiveFileName}] ::: ARCHIVE WAS BUILT OK! WROTE OUT {FileSizeString} BYES IN {TimeSpentString}", LogType.InfoLog);
            this.ArchiveCompleted?.Invoke(this, e);
        }
        /// <summary>
        /// Event trigger for progress on file stream compression built.
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        protected virtual void OnFileOperationFailure(ArchiveProgressEventArgs e, Exception ExThrown)
        {
            // Write some info about this archive process here.
            this._archiveLogger?.WriteLog($"[{e.ArchiveFileName}] ::: FAILED TO PERFORM OPERATION ON FILE!", LogType.ErrorLog);
            this._archiveLogger?.WriteLog($"[{e.ArchiveFileName}] ::: EXCEPTION THROWN: {ExThrown.Message}");
            this.FileOperationFailure?.Invoke(this, e);
        }

        #endregion //Custom Events

        #region Fields

        // Default private field holding our base output location for archives 
        private const string _defaultArchivePath = SharpLogBroker._defaultOutputPath + "\\" + "LogArchives";

        // Private logger instance used to help log information about this archive process
        private SharpLogger _archiveLogger;                                 // SharpLogger instance used to write information about archiving

        // Timer for archive operations and the archive configuration used for this archiver
        private readonly Stopwatch _archiveTimer;                           // Timer to track archive routine execution time
        private ArchiveConfiguration _archiveConfig;                        // Configuration to track how to archive our output files

        // Private backing fields for archive configuration values
        private string _archiverName;                                       // Name of the archiver object
        private List<string> _archiveOutputFiles;                           // List of all the file names being built by this archiver
        private Dictionary<string, string[]> _archivedFileSets;             // List of all files in each archive set
        private Dictionary<string, ZipArchive> _archiveZipOutputs;          // List of all the zipArchive objects built

        #endregion //Fields

        #region Properties

        // Instance properties used to configure new archive objects and routines 
        public string ArchiverName
        {
            get => this._archiverName;
            private set => this._archiverName = value;
        }
        public string[] ArchiveOutputFiles
        {
            get => this._archiveOutputFiles.ToArray();
            private set => this._archiveOutputFiles = value.ToList();
        }
        public ArchiveConfiguration ArchiveConfig
        {
            get => _archiveConfig;
            private set => _archiveConfig = value;
        }

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
            [DefaultValue(SharpLogBroker._defaultOutputPath)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string SearchPath = SharpLogBroker._defaultOutputPath;
            [DefaultValue(_defaultArchivePath)] 
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string ArchivePath = _defaultArchivePath;

            // The default file filtering name value for an archive configuration
            [DefaultValue("*.*")] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string ArchiveFileFilter = "*.*";

            // Public facing fields for configuring a log archive session
            [DefaultValue(15)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveFileSetSize = 0;
            [DefaultValue(20)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveOnFileCount = 0;
            [DefaultValue(50)] [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveCleanupFileCount = 0;

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

            // Internal JSON ignored configuration properties about this configuration
            [JsonIgnore]
            internal bool IsDefaultConfig =>
                this.ArchiveFileSetSize == 0 &&
                this.ArchiveOnFileCount == 0 &&
                this.ArchiveCleanupFileCount == 0;

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
            public string ArchiveFileName => new FileInfo(this.ArchiveOutputFile).Name;
            public string[] PreviousFiles => this.CombinedFileSet.TakeWhile(FileObj => new FileInfo(FileObj).Name != new FileInfo(FileAdded).Name).ToArray();
            public string[] RemainingFiles => this.CombinedFileSet.SkipWhile(FileObj => new FileInfo(FileObj).Name != new FileInfo(FileAdded).Name).ToArray();

            // Public facing properties holding information about the progress of this operation
            public int FilesAdded => this.ArchiveZipOutput.Entries.Count;
            public int FilesRemaining => this.NumberOfFilesToArchive - FilesAdded;
            public int NumberOfFilesToArchive => CombinedFileSet.Length;
            public double PercentDone => ((double)this.FilesAdded / (double)this.NumberOfFilesToArchive) * 100;

            #endregion //Properties

            #region Structs and Classes
            #endregion //Structs and Classes

            // --------------------------------------------------------------------------------------------------------------------------------------

            /// <summary>
            /// Builds a new event arg object for an added file entry on the archiver.
            /// </summary>
            /// <param name="CurrentFile">The file which was just added to this archive</param>
            /// <param name="ArchiverInstance">The archiver which is building this event</param>
            internal ArchiveProgressEventArgs(string ParentZipFile, string CurrentFile, SharpLogArchiver ArchiverInstance)
            {
                // Store values here. Most of these are pulled by class instances.
                this.FileAdded = CurrentFile;
                this.ArchiveOutputFile = ParentZipFile;
                this.TimeSpentRunning = ArchiverInstance._archiveTimer.Elapsed;
                this.CombinedFileSet = ArchiverInstance._archivedFileSets[ParentZipFile];
                this.ArchiveZipOutput = ArchiverInstance._archiveZipOutputs[ParentZipFile];
            }
        }

        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new log archiving instance which will archive content based on the provided archiver configuration
        /// <param name="ArchiverName">Name of the archive configuration being used</param>
        /// <param name="ArchiveConfig">The configuration used to perform archive routines</param>
        /// </summary>
        public SharpLogArchiver(string ArchiverName, ArchiveConfiguration ArchiveConfig)
        {
            // Configure new values for the archive timer, the archived files, and build a new configuration
            this._archiverName = ArchiverName;
            this._archiveConfig = ArchiveConfig;
            this._archiveTimer = new Stopwatch();

            // Configure our backing fields here
            this._archiveOutputFiles = new List<string>();
            this._archivedFileSets = new Dictionary<string, string[]>();
            this._archiveZipOutputs = new Dictionary<string, ZipArchive>();

            // Configure a new logger for this archive helper
            this._archiveLogger = new SharpLogger(LoggerActions.FileLogger | LoggerActions.ConsoleLogger);

            // Build our archiver file output name and store it
            if (!this._initializeArchiveSets())
                throw new InvalidOperationException("Error! Failed to configure a collection of ZipArchives!");
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Attempts to archive all log files found when this log archiver was built
        /// </summary>
        /// <returns>True if all archive objects were configured and built. False if any of them failed</returns>
        public bool ArchiveLogFiles()
        {
            // Log that we're starting to build output Archive Zip files now 
            this._archiveLogger?.WriteLog($"BUILDING ARCHIVE LOG FILE SETS FOR ARCHIVER {this.ArchiverName} NOW...", LogType.InfoLog);

            // Loop all the archive objects stored on this class instance and compress our file sets now
            bool ArchivesPassed = true;
            this._archiveTimer.Restart();
            foreach (var ArchiveOutputFile in this.ArchiveOutputFiles)
            {
                // Build new file information for each file and get our zip file value here
                ZipArchive ArchiveOutputZip = this._archiveZipOutputs[ArchiveOutputFile];
                FileInfo[] ArchiveFileInfos = this._archivedFileSets[ArchiveOutputFile]
                    .Select(FileObj => new FileInfo(FileObj))
                    .ToArray();

                // Now using the built ZipArchive from the collection of built Zips, compress the found file set
                this._archiveLogger?.WriteLog($"--> ARCHIVING FILE SET FOR OUTPUT ZIP NAMED: {ArchiveOutputFile} NOW...", LogType.DebugLog);
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
                            this.OnFileAddedToArchive(new ArchiveProgressEventArgs(ArchiveOutputFile, FullPath, this));

                            // Now remove the old log file found and move onto the next file
                            File.Delete(LogFileInfo.FullName);
                        }
                        catch (Exception ArchiveException)
                        {
                            // Catch our failure and invoke a new event handler for it
                            var ArchiveFailedArgs = new ArchiveProgressEventArgs(ArchiveOutputFile, LogFileInfo.FullName, this);
                            this.OnFileOperationFailure(ArchiveFailedArgs, ArchiveException);

                            // Log our exception thrown during an archive process and exit out failed
                            this._archiveLogger?.WriteLog("--> FAILED TO BUILD NEW ZIP ARCHIVE! THIS IS A REAL WHAT THE FUCK MOMENT", LogType.ErrorLog);
                            this._archiveLogger?.WriteException("--> ERROR IS BEING LOGGED BELOW", ArchiveException);
                            ArchivesPassed = false;
                        }
                    }

                    // Send out a new archive done event once our archive has been built completely from the given file names
                    this.OnArchiveCompleted(new ArchiveProgressEventArgs(ArchiveOutputFile, ArchiveOutputFile, this));
                }
            }

            // Once all archives are built and logged out, return based on if any failed or not and if all archives exist
            this._archiveTimer.Stop();
            this._archiveLogger?.WriteLog($"ARCHIVED ALL FOUND LOG FILES CORRECTLY IN {this._archiveTimer.Elapsed}", LogType.WarnLog);
            return ArchivesPassed && this.ArchiveOutputFiles.All(File.Exists);
        }
        /// <summary>
        /// Attempts to remove all archives from a log archive directory based on the number of files in the config trigger 
        /// </summary>
        /// <returns>True if all archive sets are removed, false if they are not</returns>
        public bool CleanupArchiveHistory()
        {
            // Log that we're trying to prune/cleanup our archive files found from our archive path now
            string ExtensionValue = this.ArchiveConfig.CompressionStyle == CompressionType.ZipCompression ? "*.zip*" : "*.gz*";
            var LogArchivesLocated = Directory
                .GetFiles(this.ArchiveConfig.ArchivePath, ExtensionValue, SearchOption.AllDirectories)
                .Where(FileObj => FileObj.Contains(this.ArchiveConfig.ArchiveFileFilter))
                .OrderBy(ArchiveObj => new FileInfo(ArchiveObj).LastWriteTime)
                .Reverse();

            // Log our how many archives we found now and exit out of this routine if we're not at the trigger count
            this._archiveLogger?.WriteLog($"PULLED A TOTAL OF {LogArchivesLocated.Count()} LOG ARCHIVE OBJECTS", LogType.InfoLog);
            if (LogArchivesLocated.Count() < this.ArchiveConfig.ArchiveCleanupFileCount)
            {
                // If less than the limit return out
                int ArchiveLimit = this.ArchiveConfig.ArchiveCleanupFileCount;
                this._archiveLogger?.WriteLog($"NOT CLEANING OUT ARCHIVES SINCE OUR ARCHIVE COUNT IS LESS THAN OUR SPECIFIED VALUE OF {ArchiveLimit}", LogType.WarnLog);
                return true;
            }

            // Now locate and delete the archive sets found that aren't desired for the history based on our configuration
            var ArchiveSetsToRemove = LogArchivesLocated.Take(this.ArchiveConfig.ArchiveCleanupFileCount);
            this._archiveLogger?.WriteLog($"REMOVING A TOTAL OF {ArchiveSetsToRemove.Count()} ARCHIVE FILES NOW...", LogType.InfoLog);
            this._archiveLogger?.WriteLog("RUNNING REMOVAL OPERATION IN BACKGROUND TO KEEP MAIN THREADS ALIVE AND WELL!", LogType.WarnLog);

            // Loop them all and delete each file set one by one
            bool CleanupPassed = true;
            foreach (var LogArchiveSet in ArchiveSetsToRemove)
            {
                // Log information about each archive file we're tossing our and try to delete it now
                this._archiveLogger?.WriteLog($"REMOVING ARCHIVE OBJECT {LogArchiveSet} NOW...", LogType.TraceLog);
                try { File.Delete(LogArchiveSet); }
                catch (Exception DeleteArchiveEx)
                {
                    // Log our exception thrown during an archive process and exit out failed
                    this._archiveLogger?.WriteLog($"FAILED TO REMOVE ARCHIVE SET: {LogArchiveSet}!! THIS IS WEIRD!", LogType.WarnLog);
                    this._archiveLogger?.WriteException("ERROR IS BEING LOGGED BELOW", DeleteArchiveEx);
                    CleanupPassed = false;
                }
            }
            
            // Return based on if any archives failed to delete or not
            return CleanupPassed;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Private helper method used to build the name of a new output archive file based on timestamp values provided in the name of it
        /// </summary>
        /// <returns>True if a new set of archive file names are built, false if they are not</returns>
        private bool _initializeArchiveSets()
        {
            // Log our what we're doing and find all of the file sets needed
            this._archiveLogger?.WriteLog($"ATTEMPTING TO BUILD ARCHIVE SETS FOR INPUT PATH: {this.ArchiveConfig.SearchPath}...", LogType.WarnLog);

            // Ensure our new archive configuration values can be used for this routine
            if (this.ArchiveConfig.SearchPath == null || !Directory.Exists(this.ArchiveConfig.SearchPath))
                return false;

            // Make sure we've got a valid log archive configuration value built out here for filtering files
            if (this.ArchiveConfig.ArchiveFileSetSize <= 0) this._archiveConfig.ArchiveFileSetSize = 15;
            if (this.ArchiveConfig.ArchiveOnFileCount <= 0) this._archiveConfig.ArchiveOnFileCount = 20;
            if (this.ArchiveConfig.ArchiveCleanupFileCount <= 0) this._archiveConfig.ArchiveCleanupFileCount = 50;
            if (string.IsNullOrWhiteSpace(this.ArchiveConfig.ArchiveFileFilter))
                this._archiveConfig.ArchiveFileFilter = "*.*";
            if (string.IsNullOrWhiteSpace(this.ArchiveConfig.SearchPath))
                this._archiveConfig.SearchPath = SharpLogBroker._defaultOutputPath;
            if (string.IsNullOrWhiteSpace(this.ArchiveConfig.ArchivePath))
                this._archiveConfig.ArchivePath = _defaultArchivePath;

            // Find all the files to be archived now and setup triggers for file counts
            IEnumerable<string[]> LogFileArchiveSets = Directory.GetFiles(ArchiveConfig.SearchPath, ArchiveConfig.ArchiveFileFilter)
                .OrderBy(FileFound => new FileInfo(FileFound).CreationTime)
                .Select((FileName, FileIndex) => new { Index = FileIndex, Value = FileName })
                .GroupBy(CurrentFile => CurrentFile.Index / this.ArchiveConfig.ArchiveFileSetSize)
                .Select(FileSet => FileSet.Select(FileValue => FileValue.Value).ToArray());

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
                string ArchiveExt = ArchiveConfig.CompressionStyle == CompressionType.GZipCompression ? "gz" : "zip";
                string ArchiveFileName = $"{this.ArchiverName}_{StartTime}_{StopTime}.{ArchiveExt}";

                // Remove and recreate this output file if needed now
                Directory.CreateDirectory(this.ArchiveConfig.ArchivePath);
                ArchiveFileName = Path.Combine(this.ArchiveConfig.ArchivePath, ArchiveFileName);
                if (File.Exists(ArchiveFileName)) File.Delete(ArchiveFileName);

                // Now using the built archive file name, store it and the contents of this set on our class instance
                this._archiveOutputFiles.Add(ArchiveFileName);
                this._archivedFileSets.Add(ArchiveFileName, ArchiveSet.ToArray());

                // Finally build a new zip archive for this file set
                FileStream ArchiveStream = new FileStream(ArchiveFileName, FileMode.OpenOrCreate);
                ZipArchive ArchiveZipObject = new ZipArchive(ArchiveStream, ZipArchiveMode.Update);
                this._archiveZipOutputs.Add(ArchiveFileName, ArchiveZipObject);

                // Log the next file set built and move on
                this._archiveLogger?.WriteLog($"--> BUILT ARCHIVE FILE NAME: {ArchiveFileName}");
                this._archiveLogger?.WriteLog($"--> ARCHIVE FILE WILL HOLD {ArchiveSet.Length} FILES STARTING FROM {ArchiveSet.First()} THROUGH {ArchiveSet.Last()}");
            }

            // Return out based on the number of files found for our archives
            return this._archiveOutputFiles.Count != 0;
        }
    }
}
