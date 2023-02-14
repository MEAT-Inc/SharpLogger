using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpLogging;

namespace SharpLogger_Tests
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
        /// Test method used to build a new broker configuration structure from a JSON payload
        /// </summary>
        [TestMethod("Initialize Log Broker")]
        public void InitializeLogBroker()
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
            foreach (var BrokerConfiguration in ConfigBuilder.BrokerConfigs) {
                Assert.IsTrue(SharpLogBroker.InitializeLogging(BrokerConfiguration));
                Console.WriteLine(SharpLogBroker.ToString() + "\n");
                foreach (var ArchiveConfiguration in ConfigBuilder.ArchiveConfigs) {
                    Assert.IsTrue(SharpLogArchiver.InitializeArchiving(ArchiveConfiguration));
                    Console.WriteLine(SharpLogArchiver.ToString() + "\n");
                }

                // Split the console output once done logging the state values found
                LoggerTestHelpers.SeparateConsole();
                Console.WriteLine();
            }

            // Log that we've completed importing all routines here
            LoggerTestHelpers.LogTestMethodCompleted("Completed importing all test routines for JSON Configurations!");
        }
        /// <summary>
        /// Builds a new log broker and archiver configuration and applies them. This is the closest test to a real deployed environment
        /// </summary>
        [TestMethod("Initialize Log Broker (Release Use)")]
        public void InitializeLogBrokerRelease()
        {
            // Spawn a new test configuration for the Broker and the Archiver first
            var BrokerConfig = JsonConvert.DeserializeObject<SharpLogBroker.BrokerConfiguration>(this._brokerConfigJson);
            var ArchiverConfig = JsonConvert.DeserializeObject<SharpLogArchiver.ArchiveConfiguration>(this._archiverConfigJson);

            // Now using all of the built broker configurations, initialize the broker and log the state of it
            LoggerTestHelpers.SeparateConsole();
            Assert.IsTrue(SharpLogBroker.InitializeLogging(BrokerConfig));
            Console.WriteLine(SharpLogBroker.ToString() + "\n");
            Assert.IsTrue(SharpLogArchiver.InitializeArchiving(ArchiverConfig));
            Console.WriteLine(SharpLogArchiver.ToString() + "\n");
            LoggerTestHelpers.LogTestMethodCompleted("Completed loading configurations for a \"Release\" test without issues!");
        }

        /// <summary>
        /// Simple test method which is used to spawn new logger instances for testing output content
        /// </summary>
        [TestMethod("Spawn File Logger")]
        public void SpawnFileLoggers()
        {
            // Assert we loaded in all of our configurations correctly
            var BrokerConfig = JsonConvert.DeserializeObject<SharpLogBroker.BrokerConfiguration>(this._brokerConfigJson);
            Assert.IsTrue(BrokerConfig.LogBrokerName != null, "Error! Broker configuration failed to build!");
            
            // Setup our log broker now
            LoggerTestHelpers.SeparateConsole();
            SharpLogBroker.InitializeLogging(BrokerConfig);
            Console.WriteLine(SharpLogBroker.ToString() + "\n");
            Assert.IsTrue(SharpLogBroker.MasterLogger != null, "Error! Master logger for a broker instance was null!");

            // Log some basic information out and spawn a new logger for testing
            var SpawnedLogger = new SharpLogger(LoggerActions.FileLogger, "TestFileLogger");
            SpawnedLogger.WriteLog($"SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME {SpawnedLogger.LoggerName}!", LogType.InfoLog);
            Assert.IsTrue(SharpLogBroker.FindLoggers(SpawnedLogger.LoggerName).Count() != 0, "Error! Could not find spawned logger!");

            // Finally dispose this logger instance and log passed
            SpawnedLogger.Dispose();
            LoggerTestHelpers.LogTestMethodCompleted("Completed spawning a test file logger without issues!");
        }
        /// <summary>
        /// Simple test method which is used to spawn new logger instances for testing output content
        /// </summary>
        [TestMethod("Spawn Console Logger")]
        public void SpawnConsoleLoggers()
        {
            // Assert we loaded in all of our configurations correctly
            var BrokerConfig = JsonConvert.DeserializeObject<SharpLogBroker.BrokerConfiguration>(this._brokerConfigJson);
            Assert.IsTrue(BrokerConfig.LogBrokerName != null, "Error! Broker configuration failed to build!");

            // Setup our log broker now
            LoggerTestHelpers.SeparateConsole();
            SharpLogBroker.InitializeLogging(BrokerConfig);
            Console.WriteLine(SharpLogBroker.ToString() + "\n");
            Assert.IsTrue(SharpLogBroker.MasterLogger != null, "Error! Master logger for a broker instance was null!");

            // Log some basic information out and spawn a new logger for testing
            var SpawnedLogger = new SharpLogger(LoggerActions.ConsoleLogger, "TestConsoleLogger");
            SpawnedLogger.WriteLog($"SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME {SpawnedLogger.LoggerName}!", LogType.InfoLog);
            Assert.IsTrue(SharpLogBroker.FindLoggers(SpawnedLogger.LoggerName).Count() != 0, "Error! Could not find spawned logger!");

            // Finally dispose this logger instance and log passed
            SpawnedLogger.Dispose();
            LoggerTestHelpers.LogTestMethodCompleted("Completed spawning a test console logger without issues!");
        }
    }
}