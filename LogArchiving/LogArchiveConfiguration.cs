using System.ComponentModel;
using System.IO.Compression;
using Newtonsoft.Json;

namespace SharpLogger.LogArchiving
{
    /// <summary>
    /// Class which contains configuration type info for an archive session
    /// </summary>
    public class LogArchiveConfiguration
    {
        // Configuration values
        [DefaultValue(false)] 
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool ProgressToConsole;
        
        // Archive path value
        public string LogArchivePath;

        // Archive set info.
        [DefaultValue(15)] 
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]  
        public int ArchiveFileSetSize;
        [DefaultValue(20)] 
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int ArchiveOnFileCount;

        // Archive Cleanup File Size Trigger
        [DefaultValue(50)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int ArchiveCleanupFileCount;

        // Compression configuration
        [JsonIgnore] public CompressionLevel CompressionLevel;
        [JsonIgnore] public CompressionType CompressionStyle;
        [JsonProperty("CompressionLevel")] private string _compressionLevel => CompressionLevel.ToString();
        [JsonProperty("CompressionStyle")] private string _compressionStyle => CompressionStyle.ToString();

        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new instance of a log archive configuration object.
        /// </summary>
        public LogArchiveConfiguration() { }
    }
}
