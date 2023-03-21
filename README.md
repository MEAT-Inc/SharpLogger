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
    - The `$LOGGER_TIME` keyword is used to insert the Date and Time logging started into the log file name where you specify. So for this file, we would end up having a file name of something like `MyCoolCSharpApp_Logging_02172023-111747.log`.
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
    - In this case, that log file would be `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-111747.log`, or something similar to that since we've got the `$LOGGER_TIME` keyword in our log file path.
    - Once you've configured the LogBtoker, you're free to go and spawn any other logger instances you want, or setup a new LogArchiver configuration for automatic log file cleanup.
        ```log
        [11:17:47][INFO][LogBrokerLogger][ctor] ::: LOGGER 'LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670' HAS BEEN SPAWNED CORRECTLY!
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TIME CREATED:   2/17/2023 11:17:47 AM
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER GUID:    198BF50C-00B9-486A-A465-0559C1031670
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ IS UNIVERSAL:   YES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ RULE COUNT:     2 RULES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670 (UniversalLogger) - 2 Rules and 2 Targets
        [11:17:47][WARN][LogBrokerLogger][InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [11:17:47][INFO][LogBrokerLogger][InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [11:17:47][TRACE][LogBrokerLogger][InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.5.2.260
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 11:17 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-111747.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-111747.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  4 Loggers Constructed
            \__ Master Logger:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670
            ----------------------------------------------------------------------------------------------------
            \__ Targets Built:  8 Logging Targets Constructed
            \__ Rules Defined:  8 Logging Rules Defined
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                    "LogBrokerName": "MyCoolCSharpApp",
                    "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-111747.log",
                    "LogFileName": "MyCoolCSharpApp_Logging_02172023-111747.log",
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
            "SubFolderCleanupFileCount": 5,
            "SubFolderRemainingFileCount": 0,
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
                SubFolderCleanupFileCount = 5,                        // The number of child folder logs to leave before cleanup
                SubFolderRemainingFileCount = 0,                      // The number of child folder log files to leave after cleanup
                CompressionLevel = CompressionLevel.Optimal,          // The compression type for the archive generation
                CompressionStyle = CompressionType.ZipCompression     // The type of archive to make (Zip or GZip)
            };

            // Using the built configuration object, we can now initialize our log archiver.
            if (!SharpLogArchiver.InitializeArchiving(ArchiverConfiguration))
                throw new InvalidOperationException("Error! Failed to configure the SharpArchiver!");
        }
        ```   
    - When executed, the code snipped above will log out something similar to the following. Since we used the same configuration for the log broker as we did for the example above, the log file will still be saved using the time format string, and stored in the folder `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp` inside a file with a name similar to `MyCoolCSharpApp_Logging_02172023-111747.log`.
    - If you see that no archives are built when the archiver is first created, either you've got no log files to archive in the path you're requesting to search through, or something in your archiver configuration is not setup correctly. 
    - The output from this method demonstrates how the LogArchiver determines the best possible paths and filters to use based on the LogBroker configuration determined before configuring the LogArchiver.
    - Once you've configured the archiver, you're free to go and spawn any other logger instances you want.
        ```log
        [11:17:47][INFO][LogBrokerLogger][ctor] ::: LOGGER 'LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670' HAS BEEN SPAWNED CORRECTLY!
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TIME CREATED:   2/17/2023 11:17:47 AM
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER GUID:    198BF50C-00B9-486A-A465-0559C1031670
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ IS UNIVERSAL:   YES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ RULE COUNT:     2 RULES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670 (UniversalLogger) - 2 Rules and 2 Targets
        [11:17:47][WARN][LogBrokerLogger][InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [11:17:47][INFO][LogBrokerLogger][InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [11:17:47][TRACE][LogBrokerLogger][InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.5.2.260
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 11:17 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-111747.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-111747.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  4 Loggers Constructed
            \__ Master Logger:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670
            ----------------------------------------------------------------------------------------------------
            \__ Targets Built:  8 Logging Targets Constructed
            \__ Rules Defined:  8 Logging Rules Defined
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                    "LogBrokerName": "MyCoolCSharpApp",
                    "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-111747.log",
                    "LogFileName": "MyCoolCSharpApp_Logging_02172023-111747.log",
                    "MinLogLevel": "TraceLog",
                    "MaxLogLevel": "FatalLog"
                }
            ----------------------------------------------------------------------------------------------------

        [11:17:47][INFO][LogArchiverLogger][ctor] ::: LOGGER 'LogArchiverLogger_5B4294FA-97DB-47B5-8535-FE132D861851' HAS BEEN SPAWNED CORRECTLY!
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ TIME CREATED:   2/17/2023 11:17:47 AM
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ LOGGER GUID:    5B4294FA-97DB-47B5-8535-FE132D861851
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ IS UNIVERSAL:   YES
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ RULE COUNT:     2 RULES
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [11:17:47][TRACE][LogArchiverLogger][ctor] ::: \__ LOGGER STRING:  LogArchiverLogger_5B4294FA-97DB-47B5-8535-FE132D861851 (UniversalLogger) - 2 Rules and 2 Targets
        [11:17:47][INFO][LogArchiverLogger][InitializeArchiving] ::: ARCHIVE HELPER BUILT WITHOUT ISSUES! READY TO PULL IN ARCHIVES USING PROVIDED CONFIGURATION!
        [11:17:47][TRACE][LogArchiverLogger][InitializeArchiving] ::: 

        Log Archiver Information - 'MyCoolCSharpApp (Archives)' - Version 2.5.2.260
            \__ Archiver State:  Archiver Ready!
            \__ Creation Time:   2/17/2023 11:17 AM
            \__ Archive Size:    15 files
            \__ Trigger Count:   20 files
            \__ Max Archives:    50 archives
            \__ Search Filter:   MyCoolCSharpApp*.*
            \__ Search Path:     C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\
            \__ Archive Path:    C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\LogArchives
            ----------------------------------------------------------------------------------------------------
            \__ Child Cleanup:   5 files
	        \__ Child Leftovers: 0 files
        	----------------------------------------------------------------------------------------------------
            \__ Archive Logger:  LogArchiverLogger_5B4294FA-97DB-47B5-8535-FE132D861851
            \__ Logger Targets:  UniversalLogger
            ----------------------------------------------------------------------------------------------------
            \__ Archiver Config (JSON):
                {
                    "SearchPath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\",
                    "ArchivePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\LogArchives",
                    "ArchiveFileFilter": "MyCoolCSharpApp*.*",
                    "ArchiveFileSetSize": 15,
                    "ArchiveOnFileCount": 20,
                    "ArchiveCleanupFileCount": 50,
		            "SubFolderCleanupFileCount": 5,
		            "SubFolderRemainingFileCount": 0,
                    "CompressionLevel": "Optimal",
                    "CompressionStyle": "ZipCompression"
                }
            ----------------------------------------------------------------------------------------------------

        [11:17:47][WARN][LogArchiverLogger][InitializeArchiving] ::: ATTEMPTING TO BUILD ARCHIVE SETS FOR INPUT PATH: C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\...
        [11:17:47][WARN][LogArchiverLogger][InitializeArchiving] ::: NO LOG FILE ARCHIVE SETS COULD BE BUILT! THIS IS LIKELY BECAUSE THERE AREN'T ENOUGH FILES TO ARCHIVE!
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
    - When executed, the code snipped above will log out something similar to the following. Since we used the same configuration for the log broker as we did for the example above, the log file will still be saved using the time format string, and stored in the folder `C:\Program Files (x86)\MyOrg\MyCoolCSharpApp` inside a file with a name similar to `MyCoolCSharpApp_Logging_02172023-111747.log`.
    - This output is essentially identical to the output for when you simply configure the LogBroker instance, but it will also include our new logging information and output for the logger we built named `TestFileLogger`. Sample output from this method is shown below.
        ```log
        [11:17:47][INFO][LogBrokerLogger][ctor] ::: LOGGER 'LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670' HAS BEEN SPAWNED CORRECTLY!
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TIME CREATED:   2/17/2023 11:17:47 AM
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER GUID:    198BF50C-00B9-486A-A465-0559C1031670
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ IS UNIVERSAL:   YES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ RULE COUNT:     2 RULES
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [11:17:47][TRACE][LogBrokerLogger][ctor] ::: \__ LOGGER STRING:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670 (UniversalLogger) - 2 Rules and 2 Targets
        [11:17:47][WARN][LogBrokerLogger][InitializeLogging] ::: LOGGER BROKER BUILT AND SESSION MAIN LOGGER HAS BEEN BOOTED CORRECTLY!
        [11:17:47][INFO][LogBrokerLogger][InitializeLogging] ::: SHOWING BROKER STATUS INFORMATION BELOW. HAPPY LOGGING!
        [11:17:47][TRACE][LogBrokerLogger][InitializeLogging] ::: 

        Log Broker Information - 'MyCoolCSharpApp' - Version 2.5.2.260
            \__ Broker Status:  Log Broker Ready!
            \__ Creation Time:  2/17/2023 11:17 AM
            \__ Logging State:  Logging Currently ON
            \__ Min Log Level:  TraceLog (NLevel: Trace)
            \__ Max Log Level:  FatalLog (NLevel: Fatal)
            \__ Log File Name:  MyCoolCSharpApp_Logging_02172023-111747.log
            \__ Log File Path:  C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\MyCoolCSharpApp_Logging_02172023-111747.log
            ----------------------------------------------------------------------------------------------------
            \__ Loggers Built:  4 Loggers Constructed
            \__ Master Logger:  LogBrokerLogger_198BF50C-00B9-486A-A465-0559C1031670
            ----------------------------------------------------------------------------------------------------
            \__ Targets Built:  8 Logging Targets Constructed
            \__ Rules Defined:  8 Logging Rules Defined
            ----------------------------------------------------------------------------------------------------
            \__ Broker Config (JSON):
                {
                   "LogBrokerName": "MyCoolCSharpApp",
                    "LogFilePath": "C:\\Program Files (x86)\\MyOrg\\MyCoolCSharpApp\\MyCoolCSharpApp_Logging_02172023-111747.log",
                    "LogFileName": "MyCoolCSharpApp_Logging_02172023-111747.log",
                    "MinLogLevel": "TraceLog",
                    "MaxLogLevel": "FatalLog"
                }
            ----------------------------------------------------------------------------------------------------
        
        [11:17:47][WARN][LogArchiverLogger][InitializeArchiving] ::: ATTEMPTING TO BUILD ARCHIVE SETS FOR INPUT PATH: C:\Program Files (x86)\MyOrg\MyCoolCSharpApp\...
        [11:17:47][WARN][LogArchiverLogger][InitializeArchiving] ::: NO LOG FILE ARCHIVE SETS COULD BE BUILT! THIS IS LIKELY BECAUSE THERE AREN'T ENOUGH FILES TO ARCHIVE!
        [11:17:47][INFO][TestFileLogger][ctor] ::: LOGGER 'TestFileLogger_F2FDA296-D069-47CB-8758-2282A08F28DA' HAS BEEN SPAWNED CORRECTLY!
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ TIME CREATED:   2/17/2023 11:17:47 AM
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ LOGGER GUID:    F2FDA296-D069-47CB-8758-2282A08F28DA
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ IS UNIVERSAL:   YES
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ RULE COUNT:     2 RULES
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ TARGET COUNT:   2 TARGETS
        [11:17:47][TRACE][TestFileLogger][ctor] ::: \__ LOGGER STRING:  TestFileLogger_F2FDA296-D069-47CB-8758-2282A08F28DA (FileLogger) - 2 Rules and 2 Targets
        [11:17:47][INFO][TestFileLogger][InitializeLogBrokerForDocs] ::: SPAWNED FILE LOGGER REPORTING IN! LOGGER NAME TestFileLogger_F2FDA296-D069-47CB-8758-2282A08F28DA!
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
