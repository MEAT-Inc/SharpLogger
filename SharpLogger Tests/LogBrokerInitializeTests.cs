using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpLogger;

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
                foreach (var ArchiveConfiguration in ConfigBuilder.ArchiveConfigs) {
                    Assert.IsTrue(SharpLogBroker.InitializeLogging(BrokerConfiguration, ArchiveConfiguration));
                    Console.WriteLine(SharpLogBroker.ToString() + "\n");
                    LoggerTestHelpers.SeparateConsole();
                }
            }
        }
    }
}