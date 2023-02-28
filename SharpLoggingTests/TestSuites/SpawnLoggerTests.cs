using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SharpLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpLogger_Tests.TestSuites
{
    /// <summary>
    /// Test class used to spawn the different types of logger objects needed for writing output
    /// </summary>
    [TestClass]
    public class SpawnLoggerTests
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
