using Newtonsoft.Json;
using SharpLogger.LoggerSupport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpLogger.LogArchiving
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
            Logger?.WriteLog($"[{e.ArchiveFileName}] ::: ADDED {NameOnly} TO ARCHIVE OK! ({e.FilesRemaining} FILES LEFT. {e.PercentDone:F2}%)", LogType.TraceLog);
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
            Logger?.WriteLog($"[{e.ArchiveFileName}] ::: ARCHIVE WAS BUILT OK! WROTE OUT {FileSizeString} BYES IN {TimeSpentString}", LogType.InfoLog);
            this.ArchiveCompleted?.Invoke(this, e);
        }
        /// <summary>
        /// Event trigger for progress on file stream compression built.
        /// </summary>
        /// <param name="e">Argument object of type ArchiveProgressEventArgs</param>
        protected virtual void OnFileOperationFailure(ArchiveProgressEventArgs e, Exception ExThrown)
        {
            // Write some info about this archive process here.
            Logger?.WriteLog($"[{e.ArchiveFileName}] ::: FAILED TO PERFORM OPERATION ON FILE!", LogType.ErrorLog);
            Logger?.WriteLog($"[{e.ArchiveFileName}] ::: EXCEPTION THROWN: {ExThrown.Message}");
            this.FileOperationFailure?.Invoke(this, e);
        }

        #endregion //Custom Events

        #region Fields

        // Instance fields used to control operations on this archiver
        private readonly Stopwatch _archiveTimer;                 
        
        #endregion //Fields

        #region Properties

        // Instance properties used to configure new archive objects and routines 
        public string ArchiverName { get; private set; }
        public string[] ArchivedFiles { get; private set; }
        public string ArchiveOutputFile { get; private set; }
        public ZipArchive ArchiveZipOutput { get; private set; }
        public ArchiveConfiguration ArchiveConfig { get; private set; }

        #endregion //Properties

        #region Structs and Classes

        /// <summary>
        /// Compression type methods for this compressor
        /// </summary>
        public enum CompressionType
        {
            ZIP_COMPRESSION,        // ZIP File compression
            GZIP_COMPRESSION,       // GZ file compression
        }
        /// <summary>
        /// Class which contains configuration type info for an archive session
        /// </summary>
        public struct ArchiveConfiguration
        {
            #region Custom Events
            #endregion //Custom Events

            #region Fields

            // Public facing field for the archive location
            [DefaultValue("C:\\Program Files (x86)\\MEAT Inc\\SharpLogging\\LogArchives")]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string ArchivePath;

            // Public facing fields for configuring a log archive session
            [DefaultValue(15)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveFileSetSize;
            [DefaultValue(20)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveOnFileCount;
            [DefaultValue(50)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int ArchiveCleanupFileCount;

            // Public facing fields to configure compression types for log archives
            [JsonIgnore] public CompressionType CompressionStyle;
            [JsonIgnore] public CompressionLevel CompressionLevel;

            #endregion //Fields

            #region Properties

            // Private properties used to help build JSON configuration objects for archives
            [JsonProperty("CompressionLevel")] private string _compressionLevel => CompressionLevel.ToString();
            [JsonProperty("CompressionStyle")] private string _compressionStyle => CompressionStyle.ToString();

            // Internal JSON ignored configuration properties about this configuration
            [JsonIgnore]
            internal bool IsDefaultConfig =>
                this.ArchiveFileSetSize == 0 &&
                this.ArchiveOnFileCount == 0 &&
                this.ArchiveCleanupFileCount == 0 &&
                string.IsNullOrWhiteSpace(this.ArchivePath);

            #endregion //Properties

            #region Structs and Classes
            #endregion //Structs and Classes

            // ------------------------------------------------------------------------------------------------------------------------------------------

            /// <summary>
            /// Builds a new instance of a log archive configuration object.
            /// </summary>
            public ArchiveConfiguration()
            {
                // Setup and store default archive path values
                this.ArchivePath = string.Empty;

                // Configure file count values for archive triggers
                this.ArchiveOnFileCount = 0;
                this.ArchiveFileSetSize = 0;
                this.ArchiveCleanupFileCount = 0;

                // Finally setup our compression types and exit out
                this.CompressionLevel = CompressionLevel.Optimal;
                this.CompressionStyle = CompressionType.ZIP_COMPRESSION;
            }
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
            internal ArchiveProgressEventArgs(string CurrentFile, SharpLogArchiver ArchiverInstance)
            {
                // Store values here. Most of these are pulled by class instances.
                this.FileAdded = CurrentFile;
                this.ArchiveZipOutput = ArchiverInstance.ArchiveZipOutput;
                this.ArchiveOutputFile = ArchiverInstance.ArchiveOutputFile;
                this.CombinedFileSet = ArchiverInstance.ArchivedFiles;
                this.TimeSpentRunning = ArchiverInstance._archiveTimer.Elapsed;
            }
        }

        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new log archiving instance which will archive the given file names
        /// </summary>
        /// <param name="FilesToArchive">Files to archive</param>
        /// <param name="ArchiveNameBase">The name of the archiver which is performing these operations</param>
        public SharpLogArchiver(IEnumerable<string> FilesToArchive, string ArchiveNameBase = null)
        {
            // Log booting up a new archiver instance and prepare to build it
            Logger?.WriteLog($"--> SPAWNING NEW LOG ARCHIVER WITH A DEFAULT CONFIGURATION SETUP NOW...", LogType.InfoLog);

            // Configure new values for the archive timer, the archived files, and build a new configuration
            this._archiveTimer = new Stopwatch();
            this.ArchivedFiles = FilesToArchive.ToArray();
            this.ArchiverName = ArchiveNameBase ?? LogBroker.AppInstanceName;

            // Build a new archive configuration using default values for this instance
            this.ArchiveConfig = new ArchiveConfiguration
            {
                ArchiveOnFileCount = 50,
                ArchiveFileSetSize = 15,
                CompressionLevel = CompressionLevel.Optimal,
                CompressionStyle = CompressionType.ZIP_COMPRESSION,
                ArchivePath = "C:\\Program Files (x86)\\MEAT Inc\\SharpLogging\\LogArchives"
            };

            // Build our archiver file output name and store it
            if (!this._initializeArchiveName(this.ArchivedFiles, out string NewArchiveName))
                throw new InvalidOperationException("Error! Failed to configure new archive output file name!");
            if (!this._initializeArchiveZip(out ZipArchive ArchiveBuilt))
                throw new InvalidOperationException("Error! Failed to configure a new archive ZipArchive object!");

            // Store the new values for our ZipArchive and the file name being built and exit out
            this.ArchiveZipOutput = ArchiveBuilt;
            this.ArchiveOutputFile = NewArchiveName;
        }
        /// <summary>
        /// Builds a new log archiving instance which will archive the given file names
        /// </summary>
        /// <param name="ArchiverConfig">The configuration to use for archive routines</param>
        /// <param name="FilesToArchive">Files to archive</param>
        /// <param name="ArchiveNameBase">The name of the archiver which is performing these operations</param>
        public SharpLogArchiver(ArchiveConfiguration ArchiverConfig, IEnumerable<string> FilesToArchive, string ArchiveNameBase = null)
        {
            // Log booting up a new archiver instance and prepare to build it
            Logger?.WriteLog($"--> SPAWNING NEW LOG ARCHIVER WITH A USER DEFINED CONFIGURATION SETUP NOW...", LogType.InfoLog);

            // Store our base configuration values and setup this archiver
            this.ArchiveConfig = ArchiverConfig;
            this._archiveTimer = new Stopwatch();
            this.ArchivedFiles = FilesToArchive.ToArray();
            this.ArchiverName = ArchiveNameBase ?? LogBroker.AppInstanceName;

            // Build our archiver file output name and store it
            if (!this._initializeArchiveName(out string NewArchiveName))
                throw new InvalidOperationException("Error! Failed to configure new archive output file name!");
            if (!this._initializeArchiveZip(out ZipArchive ArchiveBuilt))
                throw new InvalidOperationException("Error! Failed to configure a new archive ZipArchive object!");

            // Store the new values for our ZipArchive and the file name being built and exit out
            this.ArchiveZipOutput = ArchiveBuilt;
            this.ArchiveOutputFile = NewArchiveName;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Private helper method used to build the name of a new output archive file based on timestamp values provided in the name of it
        /// </summary>
        /// <param name="ArchiveFileName">The built archive file name and path</param>
        /// <returns>True if a new archive file name is built, false if it is not</returns>
        private bool _initializeArchiveName(out string ArchiveFileName)
        {
            // Find matches in the file names provided to setup what our range of files is
            Regex MatchTimeRegex = new Regex(@"(\d{2})(\d{2})(\d{4})-(\d{6})", RegexOptions.Compiled);
            Match FirstFileMatch = MatchTimeRegex.Match(Path.GetFileName(this.ArchivedFiles.First()));
            Match LastFileMatch = MatchTimeRegex.Match(Path.GetFileName(this.ArchivedFiles.Last()));

            // Get date values.
            string StartTime = $"{FirstFileMatch.Groups[1].Value}{FirstFileMatch.Groups[2].Value}{FirstFileMatch.Groups[3].Value.Substring(1)}-{FirstFileMatch.Groups[4].Value}";
            string StopTime = $"{LastFileMatch.Groups[1].Value}{LastFileMatch.Groups[2].Value}{LastFileMatch.Groups[3].Value.Substring(1)}-{LastFileMatch.Groups[4].Value}";
            string ArchiveExt = ArchiveConfig.CompressionStyle == CompressionType.GZIP_COMPRESSION ? "gz" : "zip";
            ArchiveFileName = $"{this.ArchiverName}_{StartTime}_{StopTime}.{ArchiveExt}";

            // Create our path for the archive output file if needed and remove old instance of it
            Directory.CreateDirectory(this.ArchiveConfig.ArchivePath);
            ArchiveFileName = Path.Combine(this.ArchiveConfig.ArchivePath, ArchiveFileName);
            if (File.Exists(ArchiveFileName)) File.Delete(ArchiveFileName);

            // Log out the name of the newly built archive file
            string FirstFileName = Path.GetFileName(this.ArchivedFiles.First());
            string LastFileName = Path.GetFileName(this.ArchivedFiles.Last());
            Logger?.WriteLog($"--> BUILT ARCHIVE FILE NAME: {this.ArchiveOutputFile}");
            Logger?.WriteLog($"--> ARCHIVE FILE WILL HOLD {ArchivedFiles.Length} FILES STARTING FROM {FirstFileName} THROUGH {LastFileName}");

            // Return based on if our output archive file name is valid or not
            return !string.IsNullOrWhiteSpace(ArchiveFileName);
        }
        /// <summary>
        /// Creates a new instance of a ZipArchive object which is used to hold archived files
        /// </summary>
        /// <param name="ArchiveBuilt">The built ZipArchive instance used to hold our output files</param>
        /// <returns>True if built correctly, false if it fails</returns>
        private bool _initializeArchiveZip(out ZipArchive ArchiveBuilt)
        {
            try
            {
                // Construct a new archive instance from a file stream pointed at our output path
                ArchiveBuilt = new ZipArchive(
                    new FileStream(ArchiveOutputFile, FileMode.OpenOrCreate),
                    ZipArchiveMode.Update
                );

                // Return passed once we've built and assigned our archive object
                return true;
            }
            catch (Exception ArchiveSetupEx)
            {
                // Log the failure out and exit with a failed state
                Logger?.WriteLog("Error! Failed to configure a new ZipArchive object for an archiver!");
                Logger?.WriteLog(ArchiveSetupEx, LogType.ErrorLog);

                // Null our output archive object value and return failed
                ArchiveBuilt = null;
                return false;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Takes the files listed to archive on our instance and converts them into a compressed object
        /// </summary>
        /// <returns>True if all files are archived correctly. False if they are not</returns>
        public bool CompressArchiveFiles()
        {
            try 
            {
                // Start the archive timer object here and get the file info for each file being archived
                this._archiveTimer.Restart();
                FileInfo[] ArchiveFileInfos = this.ArchivedFiles
                    .Select(FileObj => new FileInfo(FileObj))
                    .ToArray();
                
                // Now using the built ZipArchive on our class instance, setup a new archive
                using (this.ArchiveZipOutput)
                {
                    // Append all entries now.
                    foreach (var LogFileInfo in ArchiveFileInfos)
                    {
                        try
                        {
                            // Get name of file and use it for entry contents and then store it in our archive
                            string FullPath = LogFileInfo.FullName;
                            string NameOnly = Path.GetFileName(LogFileInfo.Name);
                            this.ArchiveZipOutput.CreateEntryFromFile(FullPath, NameOnly, CompressionLevel.Optimal);
                            this.OnFileAddedToArchive(new ArchiveProgressEventArgs(FullPath, this));
                        }
                        catch (Exception ArchiveException)
                        {
                            // Catch our failure and invoke a new event handler for it
                            var ArchiveFailedArgs = new ArchiveProgressEventArgs(LogFileInfo.FullName, this);
                            this.OnFileOperationFailure(ArchiveFailedArgs, ArchiveException);
                        }

                        // Remove the old base file now and move onto our next log file entry
                        try { File.Delete(LogFileInfo.FullName); }
                        catch { Logger?.WriteLog($"FAILED TO DELETE INPUT LOG FILE {LogFileInfo.Name}!", LogType.WarnLog); }
                    }

                    // Send out a new archive done event once our archive has been built completely from the given file names
                    this.OnArchiveCompleted(new ArchiveProgressEventArgs(this.ArchiveOutputFile, this));
                    return true;
                }
            }
            catch (Exception BuildLogArchiveEx)
            {
                // Log Archive creation failed and return out false. 
                Logger?.WriteLog("FAILED TO BUILD NEW ZIP ARCHIVE! THIS IS A REAL WHAT THE FUCK MOMENT", LogType.ErrorLog);
                Logger?.WriteLog("ERROR IS BEING LOGGED BELOW", BuildLogArchiveEx);
                return false;   
            }
        }
    }
}
