# SharpLogger
The AIO Logger used in most of if not all of our projects

---

## Configuring the SharpLogger for a new Session
- Using the SharpLogger is pretty simple. 
- Just add the NuGet package for this project, and include it as a using call in the top of your class file. 
- Then you build a new logging instance and configure it using whatever name and path you desire.
- From there you can optionally configure a new LoggingArchive object which contains information about the archival process for our loggers. 
  - A simple LogArchive object looks like the following:
  ```json
      "LogArchiveSetup": {
        "ProgressToConsole": false,
        "LogArchivePath": "C:\\Program Files (x86)\\MEAT Inc\\FulcrumShim\\FulcrumLogs\\FulcrumArchives",
        "ArchiveOnFileCount": 20,
        "ArchiveFileSetSize": 15,
        "ArchiveCleanupFileCount": 50,
        "CompressionLevel": "Optimal",
        "CompressionStyle": "ZIP_COMPRESSION"
    }
  ```
  - Once this object is passed into our config method, we then build out a new archive routine which will cleanup our log directory once conditions are met.

### Here's some sample source to build a new logger
- This block of code builds a new logging object using a set of values pulled in from a json configuration file. 
- The values of AppName, and LoggingPath are equal to thsoe shown in the JSON file object above this block of code. 
- Replace these with whatever you want to customize your output logging shema
- ```csharp
  /// <summary>
  /// Main entry point for the Fulcrum Injector configuration application
  /// </summary>
  /// <param name="args"></param>
  public static void Main(string[] args)
  {
      // Build our logging configurations
      string AppName = ValueLoaders.GetConfigValue<string>("AppInstanceName");
      string LoggingPath = ValueLoaders.GetConfigValue<string>("FulcrumLogging.DefaultLoggingPath");
      var ConfigObj = ValueLoaders.GetConfigValue<dynamic>("FulcrumLogging.LogArchiveSetup");
      LoggingSetup LoggerInit = new LoggingSetup(AppName, LoggingPath);

      // Configure loggers and their outputs here
      LoggerInit.ConfigureLogging();                  // Make loggers
      LoggerInit.ConfigureLogCleanup(ConfigObj);      // Build log cleanup routines
      var InjectorMainLogger = new SubServiceLogger("InjectorMainLogger");
      InjectorMainLogger.WriteLog("BUILT NEW LOGGING INSTANCE CORRECTLY!", LogType.InfoLog);
  }
  ```
  
--- 

## Questions, Comments, Angry Mobs of People Due to Bugs?
- Reach out to neo.smith@motorengineeringandtech.com or make a bug/issue report on this repo.
- As always, thanks for using MEAT. Stay MEETY
