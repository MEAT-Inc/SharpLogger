# **SharpLogger**
The AIO Logger used in most of if not all of our projects. This is a simple wrapper for NLog (Version 5.0 or higher), which does nothing more than allow easy configuration of logging output for multiple packages/DLLs in one shot. 

The SharpLogger supports three main logging output types at this point, with support for adding custom targets on a per-logger or global basis. 
- **File Logging** - Writing log entries to a universal log file for a running application or setting specific file output locations for a specific logging instance
- **Console Logging** - Writing log entries to a console (virtual or physical). This is usually helpful for unit testing or debug output logging
- **SQL Server Logging** - Writing to a SQL server/DB for globalized logging. This is typically used when writing exceptions that are thrown and logged during exectuion. (**NOTE** SQL Logging is not yet completed, but will be soon)

---

## **Configuring the SharpLogger**
- First thing you need to do is pull this package in using NuGet and ensure you've got the latest version. This package depends on the Newtonsoft.JSON package (13.0+) and NLog (5.0+) to run correctly.
- Once installed, you need to setup the LogBroker to prepare to write logging output. You can configure it progmatically using the `SharpLogBroker.BrokerConfiguration` structure, or by defining a JSON payload in your settings file and deserializing it into this structue type. 
- The configuration objects are *very* flexible. You can put paths in the name entries or leave values blank, and the configuration objects **should** be able to deal with it. The only true requirement is to provide a configuration in general to initialize the logging session.
- All of the C# code included below is directly pulled out of the Unit Testing project for this package. If the code below doesn't work/won't compile, it's something you're configuring incorrectly. If logging behavior is erratic or incorrect, then maybe you've found an actual bug that needs to be dealt with. If that's the case, report it so it can be rectified.

    ### **SharpLogBroker Setup**
    - Keep in mind than when configuring the LogBroker, you *must* configure it at the very start of your program (inside App.xaml.cs or Main), in order to ensure that all packages/loggers are ready to use when they're called. 
    - A sample `SharpLogBroker.BrokerConfiguration` JSON payload would be something similar to what is shown below.
    - The`$LOGGER_TIME` keyword is used to insert the Date and Time logging started into the log file name where you specify. So for this file, we would end up having a file name of something like `MyCoolCSharpApp_Logging_02172023-082222.log`.
    - If any of these values are left empty/null when the configuration is loaded in, the default/fallback values will be applied to the configuration object. Some of those defaults are listed below
        - `LogBrokerName` - Will default to the name of the EXE/Service that is calling this logging session with the word Logger appended to it. (Example: MyCoolCSharpAppLogging)
        - `LogFilePath` - Will default to the base location of the calling executable. (Example: `C:\Program Files (x86)\MyOrg\MyCoolSharpApp\MyCoolCSharpApp.exe` would store `C:\Program Files (x86)\MyOrg\MyCoolSharpApp`)
        - `MinLogLevel` - Will default to TraceLog if no value is provided. This should really always be something higher than TraceLog if you're using any of the MEAT Inc projects in a release environment. TracLog generates a **TON** of logging output.
        - `MaxLogLevel` - Will default to FatalLog if no value is provided. This should usually be acceptable for all sitiuations as FatalLogs will usually indicate an unhandled excepiton that crashes your program. 
        ```json
        { 
            "LogBrokerName": "MyCoolCSharpApp",
            "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp",
            "LogFileName": "MyCoolCSharpApp_Logging_$LOGGER_TIME.log",
            "MinLogLevel": "TraceLog",
            "MaxLogLevel": "FatalLog" 
        }
        ```
    - To convert this JSON Payload into a configuration, you can use the DeserializeObject routine from inside Newtonsoft.JSON which is included in this project by default.
    - When configuring the LogBroker progrmatically, you would issue a routine such as what is shown below.
        ```csharp
        /// <summary>
        /// Main entry point for your application. This will begin by configuring logging for your application
        /// </summary>
        /// <param name="args">Arguments provided to the Main method of this application from the CLI</param>
        public static void Main(string[] args)
        {
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
            // This will build a new logger on the LogBroker which will log out the newly set broker configuration
            if (!SharpLogBroker.InitializeLogging(BrokerConfiguration))
                throw new InvalidOperationException("Error! Failed to configure a new SharpLogging session!");
        }
        ```
    - The code snippet above would log out something like the following to the log file specified. 
    - In this case, that log file would be `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-082222.log`, or something similar to that since we've got the `$LOGGER_TIME` keyword in our log file path.
    - Once you've configured the LogBtoker, you're free to go and spawn any other logger instances you want, or setup a new LogArchiver configuration for automatic log file cleanup.
        ```log
        [02-17-2023 08:24:32][INFO][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: LOGGER 'LogBrokerLogger_47A9CB0E-9FB8-487A-B8C3-5A1DCD270C50' HAS BEEN SPAWNED CORRECTLY!
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TIME CREATED:   2/17/2023 8:24:32 AM
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER GUID:    47A9CB0E-9FB8-487A-B8C3-5A1DCD270C50
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ IS UNIVERSAL:   YES
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ RULE COUNT:     2 RULES
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [02-17-2023 08:24:32][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_47A9CB0E-9FB8-487A-B8C3-5A1DCD270C50 (UniversalLogger) - 2 Rules and 2 Targets
        [02-17-2023 08:24:33][WARN][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [02-17-2023 08:24:34][INFO][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [02-17-2023 08:24:35][TRACE][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.4.8.253
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 8:22 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-082222.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-082222.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  1 Logger Constructed
            \__ Master Logger:  LogBrokerLogger_47A9CB0E-9FB8-487A-B8C3-5A1DCD270C50
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                    "LogBrokerName": "MyCoolCSharpApp",
                    "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-082222.log",
                    "LogFileName": "MyCoolCSharpApp_Logging_02172023-082222.log",
                    "MinLogLevel": "TraceLog",
                    "MaxLogLevel": "FatalLog"
                }
            ----------------------------------------------------------------------------------------------------
        ```

    ### **SharpLogArchiver Setup**
    - You can also configure optional archiving support inside the SharpLogger, which can be configured using the `SharpLogArchiver.ArchiveConfiguration` structure. 
    - This can also be configured by defining a JSON payload in your settings file and deserializing it into this structue type
    - As with the LogBroker configuration, null values will be populated automatically based on the configuration setup for the LogBroker instance. 
        ```json
        {
            "SearchPath": null,
            "ArchivePath": null,
            "ArchiveFileFilter": null,
            "ArchiveFileSetSize": 15,
            "ArchiveOnFileCount": 20,
            "ArchiveCleanupFileCount": 50,
            "CompressionLevel": "Optimal",
            "CompressionStyle": "ZipCompression"
        }
        ```
    - To convert that JSON Payload into a configuration, you can use the DeserializeObject routine from inside Newtonsoft.JSON which is included in this project by default.
    - Be advised that it's best to configure the LogArchiver right after configuring the LogBroker so it's able to run as soon as the program is started up. This way any archive routines that run won't clog up the execution of your app 
    - When configuring the LogArchiver progrmatically, you would issue a routine such as what is shown below. Keep in mind that the log broker MUST be setup before the LogArchiver is built. Otherwise it will fail out.
    - Keep in mind that if you provide the same path value for `SearchPath` and `ArchivePath`, the value for `ArchivePath` will be `SearchPath` plus `LogArchives`. So in this example, `ArchivePath` will be set to `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\LogArchives` when the LogArchiver is configured.
    - It's also worth nothing that if no `ArchiveFileFilter` is provided, the LogArchiver will automatically set it to the name of the 
    - In the code sample below, since we don't provide values for `SearchPath`, `ArchivePath`, or `ArchiveFileFilter` the LogArchiver will generate the correct (or most logical) paths for archiving and searching.
        ```csharp
        /// <summary>
        /// Main entry point for your application. This will begin by configuring logging for your application and then setting up an archiving configuration for archiving previously built log files.
        /// </summary>
        /// <param name="args">Arguments provided to the Main method of this application from the CLI</param>
        public static void Main(string[] args)
        {
            // Define a new log broker configuration and setup the log broker
            SharpLogBroker.BrokerConfiguration BrokerConfiguration = new SharpLogBroker.BrokerConfiguration()
            {
                LogBrokerName = "MyCoolCSharpApp",                                 // Name of the logging session
                LogFileName = "MyCoolCSharpApp_Logging_$LOGGER_TIME.log",          // Name of the log file to write
                LogFilePath = "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp",   // Path to the log file to write
                MinLogLevel = LogType.TraceLog,                                    // The lowest level of logging
                MaxLogLevel = LogType.FatalLog                                     // The highest level of logging
            };

            // Using the built configuration object, we can now initialize our log broker.
            if (!SharpLogBroker.InitializeLogging(BrokerConfiguration))
                throw new InvalidOperationException("Error! Failed to configure a new SharpLogging session!");

            // Now define a new log archiver configuration and setup the log archiver
            SharpLogArchiver.ArchiveConfiguration ArchiverConfiguration = new SharpLogArchiver.ArchiveConfiguration()
            {
                SearchPath = null,                                    // The path to search for files to archive
                ArchivePath = null,                                   // The path to store the archived files into
                ArchiveFileFilter = null,                             // The filter to use when searching for archive
                ArchiveFileSetSize = 15,                              // The number of files to store in each archiv
                ArchiveOnFileCount = 20,                              // The number of files to trigger an archive even
                ArchiveCleanupFileCount = 50,                         // The max number of archives to store in the ArchivePath
                CompressionLevel = CompressionLevel.Optimal,          // The compression type for the archive generatio
                CompressionStyle = CompressionType.ZipCompression     // The type of archive to make (Zip or GZip)
            };

            // Using the built configuration object, we can now initialize our log archiver.
            if (!SharpLogArchiver.InitializeArchiving(ArchiverConfiguration))
                throw new InvalidOperationException("Error! Failed to configure the SharpArchiver!");
        }
        ```   
    - When executed, the code snipped above will log out something similar to the following. Since we used the same configuration for the log broker as we did for the example above, the log file will still be saved using the time format string, and stored in the folder `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp` inside a file with a name similar to `MyCoolCSharpApp_Logging_02172023-090359.log`.
    - If you see that no archives are built when the archiver is first created, either you've got no log files to archive in the path you're requesting to search through, or something in your archiver configuration is not setup correctly. 
    - The output from this method demonstrates how the LogArchiver determines the best possible paths and filters to use based on the LogBroker configuration determined before configuring the LogArchiver.
    - Once you've configured the archiver, you're free to go and spawn any other logger instances you want.
        ```log
        [02-17-2023 09:04:00][INFO][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: LOGGER 'LogBrokerLogger_706D03C1-3793-4A0B-A780-A81C66BB6C69' HAS BEEN SPAWNED CORRECTLY!
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TIME CREATED:   2/17/2023 9:03:59 AM
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER GUID:    706D03C1-3793-4A0B-A780-A81C66BB6C69
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ IS UNIVERSAL:   YES
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ RULE COUNT:     2 RULES
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_706D03C1-3793-4A0B-A780-A81C66BB6C69 (UniversalLogger) - 2 Rules and 2 Targets
        [02-17-2023 09:04:00][WARN][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [02-17-2023 09:04:00][INFO][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [02-17-2023 09:04:00][TRACE][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.4.8.254
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 9:03 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-090359.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-090359.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  1 Logger Constructed
            \__ Master Logger:  LogBrokerLogger_706D03C1-3793-4A0B-A780-A81C66BB6C69
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                    "LogBrokerName": "MyCoolCSharpApp",
                    "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-090359.log",
                    "LogFileName": "MyCoolCSharpApp_Logging_02172023-090359.log",
                    "MinLogLevel": "TraceLog",
                    "MaxLogLevel": "FatalLog"
                }
            ----------------------------------------------------------------------------------------------------

        [02-17-2023 09:04:00][INFO][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: LOGGER 'LogArchiverLogger_01435B9A-B159-48B4-8101-DA8CF402BB81' HAS BEEN SPAWNED CORRECTLY!
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ TIME CREATED:   2/17/2023 9:04:00 AM
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER GUID:    01435B9A-B159-48B4-8101-DA8CF402BB81
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ IS UNIVERSAL:   YES
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ RULE COUNT:     2 RULES
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER STRING:  LogArchiverLogger_01435B9A-B159-48B4-8101-DA8CF402BB81 (UniversalLogger) - 2 Rules and 2 Targets
        [02-17-2023 09:04:00][INFO][LogArchiverLogger][SharpLogging.SharpLogArchiver.InitializeArchiving] ::: ARCHIVE HELPER BUILT WITHOUT ISSUES! READY TO PULL IN ARCHIVES USING PROVIDED CONFIGURATION!
        [02-17-2023 09:04:00][TRACE][LogArchiverLogger][SharpLogging.SharpLogArchiver.InitializeArchiving] ::: 

        Log Archiver Information - 'MyCoolCSharpApp (Archives)' - Version 2.4.8.254
            \__ Archiver State:  Archiver Ready!
            \__ Creation Time:   2/17/2023 9:04 AM
            \__ Archive Size:    15 files
            \__ Trigger Count:   20 files
            \__ Max Archives:    50 archives
            \__ Search Filter:   MyCoolCSharpApp*.log
            \__ Search Path:     C:\Program Files (x86)\MyOrg\MyCoolCSharpApp
            \__ Archive Path:    C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\LogArchives
            ----------------------------------------------------------------------------------------------------
            \__ Archive Logger:  LogArchiverLogger_01435B9A-B159-48B4-8101-DA8CF402BB81
            \__ Logger Targets:  UniversalLogger
            ----------------------------------------------------------------------------------------------------
            \__ Archiver Config (JSON):
                {
                    "SearchPath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp",
                    "ArchivePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\LogArchives",
                    "ArchiveFileFilter": "MyCoolCSharpApp*.log",
                    "ArchiveFileSetSize": 15,
                    "ArchiveOnFileCount": 20,
                    "ArchiveCleanupFileCount": 50,
                    "CompressionLevel": "Optimal",
                    "CompressionStyle": "ZipCompression"
                }
            ----------------------------------------------------------------------------------------------------

        [02-17-2023 09:04:00][WARN][LogArchiverLogger][SharpLogging.SharpLogArchiver.InitializeArchiving] ::: ATTEMPTING TO BUILD ARCHIVE SETS FOR INPUT PATH: C:\Program Files (x86)\MEAT Inc\SharpLogging\...
        [02-17-2023 09:04:00][WARN][LogArchiverLogger][SharpLogging.SharpLogArchiver.InitializeArchiving] ::: NO LOG FILE ARCHIVE SETS COULD BE BUILT! THIS IS LIKELY BECAUSE THERE AREN'T ENOUGH FILES TO ARCHIVE!
        ```

    ### **Spawning SharpLoggers**
    - Once you've configured the SharpLogBroker, you can spawn any number of loggers for any number of processes you wish. 
    - Spawning loggers is as simple as constructing a new instance of the `SharpLogger` class. When called, it spawns a new logging instance and registers it onto the LogBroker so everything stays in sync and all targets/rules inside the NLog configuration are automatically kept up to date.
    - To spawn a logger, you'd use something similar to the code snippet below. Always remember you must configure the SharpLogBroker **before** trying to spawn new loggers. Otherwise, you'll end up having some pretty serious issues.
        ```csharp
        /// <summary>
        /// Main entry point for your application. This will begin by configuring logging for your application and then setting up a new instance of a SharpLogger for writing to the main log file.
        /// </summary>
        /// <param name="args">Arguments provided to the Main method of this application from the CLI</param>
        public static void Main(string[] args)
        {
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

            // Log some basic information out and spawn a new logger for testing
            var TestFileLogger = new SharpLogger(LoggerActions.FileLogger, "TestFileLogger");
            TestFileLogger.WriteLog($"SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME {TestFileLogger.LoggerName}!", LogType.InfoLog);
        }
        ```
    - When executed, the code snipped above will log out something similar to the following. Since we used the same configuration for the log broker as we did for the example above, the log file will still be saved using the time format string, and stored in the folder `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp` inside a file with a name similar to `MyCoolCSharpApp_Logging_02172023-090359.log`.
    - This output is essentially identical to the output for when you simply configure the LogBroker instance, but it will also include our new logging information and output for the logger we built named `TestFileLogger`. Sample output from this method is shown below.
        ```log
        [02-17-2023 09:39:17][INFO][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: LOGGER 'LogBrokerLogger_53EB7170-0C97-4A0D-BA4E-82430627E605' HAS BEEN SPAWNED CORRECTLY!
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TIME CREATED:   2/17/2023 9:39:16 AM
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER GUID:    53EB7170-0C97-4A0D-BA4E-82430627E605
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ IS UNIVERSAL:   YES
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ RULE COUNT:     2 RULES
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_53EB7170-0C97-4A0D-BA4E-82430627E605 (UniversalLogger) - 2 Rules and 2 Targets
        [02-17-2023 09:39:17][WARN][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [02-17-2023 09:39:17][INFO][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [02-17-2023 09:39:17][TRACE][LogBrokerLogger][SharpLogging.SharpLogBroker.InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.4.8.255
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 9:39 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-093916.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-093916.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  1 Logger Constructed
            \__ Master Logger:  LogBrokerLogger_53EB7170-0C97-4A0D-BA4E-82430627E605
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                "LogBrokerName": "MyCoolCSharpApp",
                "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-093916.log",
                "LogFileName": "MyCoolCSharpApp_Logging_02172023-093916.log",
                "MinLogLevel": "TraceLog",
                "MaxLogLevel": "FatalLog"
                }
            ----------------------------------------------------------------------------------------------------

        [02-17-2023 09:39:17][INFO][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: LOGGER 'TestFileLogger_6F2A3D46-7747-4832-AC25-4AB12E4BEAB8' HAS BEEN SPAWNED CORRECTLY!
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ TIME CREATED:   2/17/2023 9:39:17 AM
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER GUID:    6F2A3D46-7747-4832-AC25-4AB12E4BEAB8
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ IS UNIVERSAL:   YES
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ RULE COUNT:     2 RULES
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [02-17-2023 09:39:17][TRACE][TestFileLogger][SharpLogging.SharpLogger..ctor] ::: \__ LOGGER STRING:  TestFileLogger_6F2A3D46-7747-4832-AC25-4AB12E4BEAB8 (FileLogger) - 2 Rules and 2 Targets
        [02-17-2023 09:39:17][INFO][TestFileLogger][SharpLogger_Tests.LogBrokerInitializeTests.InitializeLogBrokerForDocs] ::: SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME TestFileLogger_6F2A3D46-7747-4832-AC25-4AB12E4BEAB8!
        ```
--- 

## **Development Setup**
- **NOTE:** As of 2/17/2023 - I've closed down the readonly access for anyone to use. If you want to use these packages, please contact zack.walsh@meatinc.autos for an API key, and someone will walk you through getting into this package repository. This decision was made after realizing that while the key was readonly and on a dedicated bot account, it's not the best idea to leave API keys exposed. And since making these projects public, it was only logical to remove the keys from here.
- If you're looking to help develop this project, you'll need to add the NuGet server for the MEAT Inc workspace into your nuget configuration. 
- To do so, navigate to your AppData\Roaming folder (You can do this by opening windows explorer and clicking the top path bar and typing %appdata%)
- Now find the folder named NuGet and open the file named NuGet.config
- Inside this file, under packageSources, you need to add a new source. Insert the following line into here 
     ```XML 
      <add key="MEAT-Inc" value="https://nuget.pkg.github.com/MEAT-Inc/index.json/" protocolVersion="3" />
    ```
- Once added in, scroll down to packageSourceCredentials (if it's not there, just make a new section for it)
- Inside this section, put the following block of code into it.
   ```XML
    <MEAT-Inc>
       <add key="Username" value="meatincreporting" />
       <add key="ClearTextPassword" value="{INSERT_API_KEY_HERE}" />
    </MEAT-Inc>
    ```
 - Once added in, save this file and close it out. 
 - Your NuGet.config should look something like this. This will allow you to access the packages inside the MEAT Inc repo/workspaces to be able to build the solution.
    ```XML
      <?xml version="1.0" encoding="utf-8"?>
          <configuration>
              <packageSources>
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                  <add key="MEAT-Inc" value="https://nuget.pkg.github.com/MEAT-Inc/index.json/" protocolVersion="3" />
              </packageSources>
              <packageSourceCredentials>
                  <MEAT-Inc>
                      <add key="Username" value="meatincreporting" />
                      <add key="ClearTextPassword" value="{INSERT_API_KEY_HERE}" />
                  </MEAT-Inc>
              </packageSourceCredentials>
              <packageRestore>
                  <add key="enabled" value="True" />
                  <add key="automatic" value="True" />
              </packageRestore>
              <bindingRedirects>
                  <add key="skip" value="False" />
              </bindingRedirects>
              <packageManagement>
                  <add key="format" value="1" />
                  <add key="disabled" value="True" />
              </packageManagement>
          </configuration> 
    </xml>
---

### Questions, Comments, Concerns? 
- I don't wanna hear it...
- But feel free to send an email to zack.walsh@meatinc.autos. He might feel like being generous sometimes...
- Or if you're feeling like a good little nerd, make an issue on this repo's project and I'll take a peek at it.
