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

### Development Setup
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
       <add key="ClearTextPassword" value="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" />
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
                      <add key="ClearTextPassword" value="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" />
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
          
---

### Questions, Comments, Concerns? 
- I don't wanna hear it...
- But feel free to send an email to zack.walsh@meatinc.autos. He might feel like being generous sometimes...
- Or if you're feeling like a good little nerd, make an issue on this repo's project and I'll take a peek at it.