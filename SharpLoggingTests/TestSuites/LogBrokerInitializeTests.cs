using System;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SharpLogging;

namespace SharpLogger_Tests.TestSuites
{
    /// <summary>
    /// Main test class used to build test instances of the SharpLogBroker instance and test basic operations for our loggers
    /// </summary>
    [TestClass]
    public class LogBrokerInitializeTests
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields

        // Private backing fields for JSON configuration setup routines
        private readonly string _brokerConfigJson =
            @"{ 
                ""LogBrokerName"": ""SharpLoggingTests"",
                ""LogFilePath"": ""C:\\Program Files (x86)\\MEAT Inc\\SharpLogging"",
                ""LogFileName"": ""SharpLoggingTests_OutputResults.log"",
                ""MinLogLevel"": ""TraceLog"",
                ""MaxLogLevel"": ""FatalLog"" 
            }";
        private readonly string _archiverConfigJson =
            @"{
                ""SearchPath"": null,
                ""ArchivePath"": null,
                ""ArchiveFileFilter"": ""SharpLoggingTests*.log"",
                ""ArchiveFileSetSize"": 15,
                ""ArchiveOnFileCount"": 20,
                ""ArchiveCleanupFileCount"": 50,
                ""CompressionLevel"": ""Optimal"",
                ""CompressionStyle"": ""ZipCompression""
            }";

        #endregion //Fields

        #region Properties
        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new log broker and archiver configuration and applies them. This is the closest test to a real deployed environment
        /// </summary>
        [TestMethod("Initialize Log Broker (JSON Conversion)")]
        public void InitializeLogBrokerFromJson()
        {
            // Spawn a new test configuration for the Broker and the Archiver first
            var BrokerConfig = JsonConvert.DeserializeObject<SharpLogBroker.BrokerConfiguration>(this._brokerConfigJson);
            var ArchiverConfig = JsonConvert.DeserializeObject<SharpLogArchiver.ArchiveConfiguration>(this._archiverConfigJson);

            // Now using all of the built broker configurations, initialize the broker and log the state of it
            LoggerTestHelpers.SeparateConsole();
            Assert.IsTrue(SharpLogBroker.InitializeLogging(BrokerConfig));
            Console.WriteLine($"\n\n{SharpLogBroker.ToString()}\n");
            Assert.IsTrue(SharpLogArchiver.InitializeArchiving(ArchiverConfig));
            Console.WriteLine($"\n\n{SharpLogArchiver.ToString()}\n");
            LoggerTestHelpers.LogTestMethodCompleted("Completed loading configurations for a \"Release\" test without issues!");
        }
        /// <summary>
        /// Test method used to configure a new log broker using the code snippets found in the README file for this repository.
        /// Cant be giving out bad code samples now can we.
        /// </summary>
        [TestMethod("Initialize Log Broker (Defined Configuration)")]
        public void InitializeLogBrokerForDocs()
        {
            // Split our console output and build a new configuration
            LoggerTestHelpers.SeparateConsole();

            // Define a new log broker configuration and setup the log broker
            SharpLogBroker.BrokerConfiguration BrokerConfiguration = new SharpLogBroker.BrokerConfiguration()
            {
                LogBrokerName = "MyCoolCSharpApp",                                  // Name of the logging session
                LogFileName = "MyCoolCSharpApp_Logging_$LOGGER_TIME.log",           // Name of the log file to write
                LogFilePath = "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp",    // Path to the log file to write
                MinLogLevel = LogType.TraceLog,                                     // The lowest level of logging
                MaxLogLevel = LogType.FatalLog                                      // The highest level of logging
            };

            // Using the built configuration object, we can now initialize our log broker.
            if (!SharpLogBroker.InitializeLogging(BrokerConfiguration))
                throw new InvalidOperationException("Error! Failed to configure a new SharpLogging session!");

            // Now define a new log archiver configuration and setup the log archiver
            SharpLogArchiver.ArchiveConfiguration ArchiverConfiguration = new SharpLogArchiver.ArchiveConfiguration()
            {
                SearchPath = null,                                   // The path to search for files to archive
                ArchivePath = null,                                  // The path to store the archived files into
                ArchiveFileFilter = null,                            // The filter to use when searching for archives
                ArchiveFileSetSize = 15,                             // The number of files to store in each archive
                ArchiveOnFileCount = 20,                             // The number of files to trigger an archive event
                ArchiveCleanupFileCount = 50,                        // The max number of archives to store in the ArchivePath
                CompressionLevel = CompressionLevel.Optimal,         // The compression type for the archive generation
                CompressionStyle = CompressionType.ZipCompression    // The type of archive to make (Zip or GZip)
            };

            // Using the built configuration object, we can now initialize our log archiver.
            if (!SharpLogArchiver.InitializeArchiving(ArchiverConfiguration))
                throw new InvalidOperationException("Error! Failed to configure the SharpArchiver!");

            // Log some basic information out and spawn a new logger for testing
            var TestFileLogger = new SharpLogger(LoggerActions.FileLogger, "TestFileLogger");
            TestFileLogger.WriteLog($"SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME {TestFileLogger.LoggerName}!", LogType.InfoLog);

            // Once done, log this test is completed and exit out 
            LoggerTestHelpers.LogTestMethodCompleted("Completed loading configurations for the \"README Broker Setup Snippets\" test without issues!");
        }

        /// <summary>
        /// Test method used to build a new broker configuration structure from a JSON payload
        /// </summary>
        [TestMethod("Initialize Log Broker (All Test Configurations)")]
        public void InitializeLogBrokerAllConfigurations()
        {
            // Spawn a new test class for our JSON importing routines first
            ConfigurationBuilderTests ConfigBuilder = new ConfigurationBuilderTests();
            ConfigBuilder.BuildBrokerConfigurations();
            ConfigBuilder.BuildArchiveConfigurations();

            // Assert we loaded in all of our configurations correctly
            Assert.IsTrue(ConfigBuilder.BrokerConfigs.Any(), "Error! No broker configurations were found!");
            Assert.IsTrue(ConfigBuilder.ArchiveConfigs.Any(), "Error! No archiver configurations were found!");

            // Now using all of the built broker configurations, initialize the broker and log the state of it
            LoggerTestHelpers.SeparateConsole();
            foreach (var BrokerConfiguration in ConfigBuilder.BrokerConfigs)
            {
                Assert.IsTrue(SharpLogBroker.InitializeLogging(BrokerConfiguration));
                Console.WriteLine($"\n\n{SharpLogBroker.ToString()}\n");
                foreach (var ArchiveConfiguration in ConfigBuilder.ArchiveConfigs)
                {
                    // Setup log archiving and print out the state of the logger to the console
                    Assert.IsTrue(SharpLogArchiver.InitializeArchiving(ArchiveConfiguration));
                    Console.WriteLine($"\n\n{SharpLogArchiver.ToString()}\n");
                }

                // Split the console output once done logging the state values found
                LoggerTestHelpers.SeparateConsole();
                Console.WriteLine();
            }

            // Log that we've completed importing all routines here
            LoggerTestHelpers.LogTestMethodCompleted("Completed importing all test routines for JSON Configurations!");
        }
    }
}