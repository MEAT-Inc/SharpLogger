using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SharpLogging;

namespace SharpLogger_Tests.TestSuites
{
    /// <summary>
    /// Main test class used to build test instances of broker and archiver configuration structures.
    /// </summary>
    [TestClass]
    public class ConfigurationBuilderTests
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields

        // Private fields populated during the test init routine to use for building new broker and archiver instances
        private List<SharpLogBroker.BrokerConfiguration> _brokerConfigs;          // Configuration structure for the log broker
        private List<SharpLogArchiver.ArchiveConfiguration> _archiveConfigs;      // Configuration structure for the log archiver

        // Private backing JSON string used to configure this test instance
        private readonly string _jsonConfigurationStrings = @"
            {
                ""BrokerConfigStrings"": [                            
                    { 
                        ""LogBrokerName"": null,
                        ""LogFilePath"": null, 
                        ""LogFileName"": null,
                        ""MinLogLevel"": ""TraceLog"",
                        ""MaxLogLevel"": ""FatalLog""
                    },                    
                    { 
                        ""LogBrokerName"": ""SharpLoggingTests"",
                        ""LogFilePath"": null, 
                        ""LogFileName"": null,
                        ""MinLogLevel"": ""TraceLog"",
                        ""MaxLogLevel"": ""FatalLog""
                    },
                    { 
                        ""LogBrokerName"": ""SharpLoggingTests"",
                        ""LogFilePath"": ""C:\\Program Files (x86)\\MEAT Inc\\SharpLogging"",
                        ""LogFileName"": null,
                        ""MinLogLevel"": ""TraceLog"",
                        ""MaxLogLevel"": ""FatalLog"" 
                    },
                    { 
                        ""LogBrokerName"": ""SharpLoggingTests"",
                        ""LogFilePath"": ""C:\\Program Files (x86)\\MEAT Inc\\SharpLogging\\"",
                        ""LogFileName"": ""SharpLoggingTests_$LOGGER_TIME.log"",
                        ""MinLogLevel"": ""TraceLog"",
                        ""MaxLogLevel"": ""FatalLog"" 
                    },
                    {
                        ""LogBrokerName"": ""SharpLoggingTests"",
                         ""LogFilePath"": ""C:\\Program Files (x86)\\MEAT Inc\\SharpLogging\\SharpLoggingTests_TestResults.log"",
                         ""LogFileName"": null,
                         ""MinLogLevel"": ""TraceLog"",
                         ""MaxLogLevel"": ""FatalLog""
                    },
                ], 
                ""ArchiverConfigStrings"": [ 
                    { 
                        ""SearchPath"": null,
                        ""ArchivePath"": null,
                        ""ArchiveFileFilter"": null,
                        ""ArchiveFileSetSize"": 0,
                        ""ArchiveOnFileCount"": 0,
                        ""ArchiveCleanupFileCount"": 0,
                        ""SubFolderCleanupFileCount"": 5,
                        ""SubFolderRemainingFileCount"": 0,
                        ""CompressionLevel"": null,
                        ""CompressionStyle"": null, 
                    },
                    {
                        ""SearchPath"": null,
                        ""ArchivePath"": null,
                        ""ArchiveFileFilter"": ""SharpLoggingTests*.log"",
                        ""ArchiveFileSetSize"": -10,
                        ""ArchiveOnFileCount"": 30,
                        ""ArchiveCleanupFileCount"": 100,
                        ""SubFolderCleanupFileCount"": 5,
                        ""SubFolderRemainingFileCount"": 0,
                        ""CompressionLevel"": null,
                        ""CompressionStyle"": null, 
                    }, 
                    {
                        ""SearchPath"": null,
                        ""ArchivePath"": null,
                        ""ArchiveFileFilter"": ""SharpLoggingTests*.log"",
                        ""ArchiveFileSetSize"": 15,
                        ""ArchiveOnFileCount"": 20,
                        ""ArchiveCleanupFileCount"": 50,
                        ""SubFolderCleanupFileCount"": 5,
                        ""SubFolderRemainingFileCount"": 0,
                        ""CompressionLevel"": ""Optimal"",
                        ""CompressionStyle"": ""ZipCompression"" 
                    }
                ]
            }";

        #endregion //Fields

        #region Properties

        // Public facing properties holding enumerators for the broker and archiver configurations built
        public IEnumerable<SharpLogBroker.BrokerConfiguration> BrokerConfigs => this._brokerConfigs;
        public IEnumerable<SharpLogArchiver.ArchiveConfiguration> ArchiveConfigs => this._archiveConfigs;

        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Test method used to build a new broker configuration structure from a JSON payload
        /// </summary>
        [TestMethod("Build Broker Configurations")]
        public void BuildBrokerConfigurations()
        {
            // Define our JSON configuration string values here and attempt to parse each one in
            JObject ConfigurationObject = JObject.Parse(this._jsonConfigurationStrings);
            JTokenReader BrokerConfigReader = new JTokenReader(
                ConfigurationObject.SelectToken("BrokerConfigStrings") 
                ?? throw new InvalidOperationException("Error! Token \"BrokerConfigStrings\" could not be found in the JSON test string!"));
                
            // Load the broker configuration string values now and import each configuration
            JArray BrokerConfigStrings = JArray.Load(BrokerConfigReader);
            this._brokerConfigs = BrokerConfigStrings
                .Select(JObject.FromObject)
                .Select(ObjectValue => ObjectValue.ToObject<SharpLogBroker.BrokerConfiguration>())
                .ToList();

            // Assert that we've correctly loaded in the JSON configurations for this method
            Assert.IsTrue(this._brokerConfigs != null, "Broker configurations were unable to be imported!");
            Assert.IsTrue(this._brokerConfigs.Count != 0, "No broker configurations were found despite a passed import routine!");

            // Log that we've completed importing all routines here
            LoggerTestHelpers.LogTestMethodCompleted("Completed importing all test routines for Broker JSON Configurations!");
        }
        /// <summary>
        /// Test method used to build a new archiver configuration structure from a JSON payload
        /// </summary>
        [TestMethod("Build Archiver Configurations")]
        public void BuildArchiveConfigurations()
        {
            // Define our JSON configuration string values here and attempt to parse each one in
            JObject ConfigurationObject = JObject.Parse(this._jsonConfigurationStrings);
            JTokenReader ArchiverConfigReader = new JTokenReader(
                ConfigurationObject.SelectToken("ArchiverConfigStrings") 
                ?? throw new InvalidOperationException("Error! Token \"ArchiverConfigStrings\" could not be found in the JSON test string!"));

            // Load the broker configuration string values now and import each configuration
            JArray ArchiverConfigStrings = JArray.Load(ArchiverConfigReader);
            this._archiveConfigs = ArchiverConfigStrings
                .Select(JObject.FromObject)
                .Select(ObjectValue => ObjectValue.ToObject<SharpLogArchiver.ArchiveConfiguration>())
                .ToList();

            // Assert that we've correctly loaded in the JSON configurations for this method
            Assert.IsTrue(this._archiveConfigs != null, "Archiver configurations were unable to be imported!");
            Assert.IsTrue(this._archiveConfigs.Count != 0, "No archiver configurations were found despite a passed import routine!");

            // Log that we've completed importing all routines here
            LoggerTestHelpers.LogTestMethodCompleted("Completed importing all test routines for Archiver JSON Configurations!");
        }
    }
}