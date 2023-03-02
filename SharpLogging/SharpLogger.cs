using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace SharpLogging
{
    /// <summary>
    /// Main Logger object setup for logging to different output types/formats.
    /// </summary>
    public class SharpLogger : INotifyPropertyChanged, IDisposable
    {
        #region Custom Events

        // Event handler for when properties or collections on this logger instance are changed
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Event invoker for when a property is changed on our logger instance
        /// </summary>
        /// <param name="PropertyName">The name of the property being changed/updated</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            // Invoke the event handler using a new event argument object if it's not null
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
        /// <summary>
        /// Sets the backing FieldToUpdate for a property when called
        /// </summary>
        /// <typeparam name="T">The type of the FieldToUpdate being set</typeparam>
        /// <param name="FieldToUpdate">The reference to the FieldToUpdate being updated</param>
        /// <param name="FieldValue">The FieldValue stored on our FieldToUpdate</param>
        /// <param name="PropertyName">The name of the property being updated</param>
        /// <returns>True if the property changed event is invoked, false if it is not</returns>
        protected bool SetField<T>(ref T FieldToUpdate, T FieldValue, [CallerMemberName] string PropertyName = null)
        {
            // If the field needs a new value to be set, set it and invoke the property updated event
            if (!EqualityComparer<T>.Default.Equals(FieldToUpdate, FieldValue))
            {
                FieldToUpdate = FieldValue;
                this.OnPropertyChanged(PropertyName);
                return true;
            }

            // If the field is not updated/the same value is found, don't do anything
            return false;
        }

        /// <summary>
        /// Event handler for when we update the rules stored on our logger instance
        /// </summary>
        /// <param name="SendingCollection">The collection of logger rules being updated</param>
        /// <param name="EventArgs">The changes made to our collection of logger rules</param>
        private void _loggerRulesOnCollectionChanged(object SendingCollection, NotifyCollectionChangedEventArgs EventArgs)
        {
            // Make sure we've got Added or Remove as the event type and make sure the sending list is usable
            if ((int)EventArgs.Action is not 0 or 1) return; if (SendingCollection is not ObservableCollection<LoggingRule>)
                throw new InvalidOperationException($"Error! Sending collection was invalid type of {SendingCollection.GetType().Name}!");

            // Now find what's been added/removed from the collection and update our log broker
            var UpdatedRules = EventArgs.Action == NotifyCollectionChangedAction.Add
                ? EventArgs.NewItems.Cast<LoggingRule>()
                : EventArgs.OldItems.Cast<LoggingRule>();
            
            // Update our log configuration based on the operation applied to our collection
            var LoggerConfiguration = LogManager.Configuration;
            foreach (var UpdatedRule in UpdatedRules)
            {
                // Make sure this target is not one of our master targets before trying to add or remove it
                if (SharpLogBroker.MasterLogger != null && UpdatedRule.LoggerNamePattern.Contains(SharpLogBroker.MasterLogger.LoggerName))
                    continue;

                // If adding new rules, make sure the rule does not exist yet
                if (EventArgs.Action == NotifyCollectionChangedAction.Add)
                    if (LoggerConfiguration.FindRuleByName(UpdatedRule.RuleName) == null)
                        LoggerConfiguration.AddRule(UpdatedRule);

                // If removing rules, only do so when the rule can actually be found
                if (EventArgs.Action == NotifyCollectionChangedAction.Remove)
                    if (LoggerConfiguration.FindRuleByName(UpdatedRule.RuleName) != null)
                        LoggerConfiguration.RemoveRuleByName(UpdatedRule.RuleName);
            }

            // Store the new collection of logging rules on the log broker and exit out
            LogManager.Configuration = LoggerConfiguration; 
        }
        /// <summary>
        /// Event handler for when we update the targets stored on our logger instance
        /// </summary>
        /// <param name="SendingCollection">The collection of logger targets being updated</param>
        /// <param name="EventArgs">The changes made to our collection of logger rules</param>
        private void _loggerTargetsOnCollectionChanged(object SendingCollection, NotifyCollectionChangedEventArgs EventArgs)
        {
            // Make sure we've got Added or Remove as the event type and make sure the sending list is usable
            if ((int)EventArgs.Action is not 0 or 1) return;
            if (SendingCollection is not ObservableCollection<Target>)
                throw new InvalidOperationException($"Error! Sending collection was invalid type of {SendingCollection.GetType().Name}!");

            // Now find what's been added/removed from the collection and update our log broker
            var UpdatedTargets = EventArgs.Action == NotifyCollectionChangedAction.Add
                ? EventArgs.NewItems.Cast<Target>()
                : EventArgs.OldItems.Cast<Target>();

            // Update our log configuration based on the operation applied to our collection
            var LoggerConfiguration = LogManager.Configuration;
            foreach (var UpdatedTarget in UpdatedTargets)
            {
                // Make sure this target is not one of our master targets before trying to add or remove it
                if (SharpLogBroker.MasterLogger != null && UpdatedTarget.Name.StartsWith($"Master_{SharpLogBroker.LogBrokerName}")) 
                    continue;

                // If adding new targets, make sure the target does not exist yet
                if (EventArgs.Action == NotifyCollectionChangedAction.Add)
                    if (LoggerConfiguration.FindTargetByName(UpdatedTarget.Name) == null)
                        LoggerConfiguration.AddTarget(UpdatedTarget);

                // If removing targets, only do so when the target can actually be found
                if (EventArgs.Action == NotifyCollectionChangedAction.Remove)
                    if (LoggerConfiguration.FindTargetByName(UpdatedTarget.Name) != null)
                        LoggerConfiguration.RemoveTarget(UpdatedTarget.Name);
            }

            // Store the new collection of logging rules on the log broker and exit out
            LogManager.Configuration = LoggerConfiguration;
        }

        #endregion //Custom Events

        #region Fields

        // Private backing fields which hold information about our logger instance
        private Guid _loggerGuid;                                           // The GUID of this logger object
        private string _loggerName;                                         // The Name of this logger object
        private string _loggerClass;                                        // The class which called this logger
        private DateTime _timeCreated;                                      // The time this logger object was built

        // Backing fields holding information about the logger levels and actions
        private LogLevel _minLevel;                                         // Lowest level of supported logging output
        private LogLevel _maxLevel;                                         // Highest level of supported logging output
        private LoggerActions _loggerType;                                  // Type of logger being built/controlled
        private readonly Logger _nLogger;                                   // NLog object that does our output writing

        // Private fields holding information about our built target objects
        private readonly ObservableCollection<Target> _loggerTargets;       // Collection of built targets for this logger
        private readonly ObservableCollection<LoggingRule> _loggerRules;    // A collection of rules related to each target

        // Private backing fields which hold our format configurations for output
        private SharpFileTargetFormat _fileLoggerFormat;                    // Configuration to build File format strings
        private SharpConsoleTargetFormat _consoleLoggerFormat;              // Configuration to build Console format strings
        
        // Private backing fields for scope properties
        private List<KeyValuePair<string, object>> _scopeProperties;        // Used to setup logging output variables in string formats

        #endregion //Fields

        #region Properties

        // Public properties holding information about our logger name and state
        public Guid LoggerGuid
        {
            get => this._loggerGuid;
            private set => this.SetField(ref this._loggerGuid, value);
        }
        public string LoggerName
        {
            get => this._loggerName;
            private set => this.SetField(ref this._loggerName, value);
        }
        public string LoggerClass
        {
            get => this._loggerClass;
            private set => this.SetField(ref this._loggerClass, value);
        }
        public DateTime TimeCreated
        {
            get => this._timeCreated;
            private set => this.SetField(ref this._timeCreated, value);
        }

        // Public facing log levels. Used to help convert between LogTypes and LogLevels
        public LogType MinLevel
        {
            get => this._minLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                this.SetField(ref this._minLevel, ConvertedLevel);
            }
        }
        public LogType MaxLevel
        {
            get => this._maxLevel.ToLogType();
            private set
            {
                // Convert the value into a LogLevel and set it
                LogLevel ConvertedLevel = value.ToNLevel();
                this.SetField(ref this._maxLevel, ConvertedLevel);
            }
        }
        public bool LoggingEnabled => 
            SharpLogBroker.LoggingEnabled && 
            (this._minLevel != LogLevel.Off && this._maxLevel != LogLevel.Off);

        // Public facing properties to hold our logging rules and targets
        public Target[] LoggerTargets
        {
            get
            {
                // Lock the collection of targets and return it out as an array
                lock (_loggerTargets) return _loggerTargets.ToArray();
            }
        }
        public LoggingRule[] LoggerRules
        {
            get
            {
                // Lock the collection of rules and return it out as an array
                lock (_loggerRules) return _loggerRules.ToArray();
            }
        }

        // Public facing properties holding our configurations for format output strings
        public SharpFileTargetFormat FileTargetFormat
        {
            get => this._fileLoggerFormat ?? SharpLogBroker.DefaultFileFormat;
            set
            {
                // Store the new format value and apply it to all of our targets
                this.SetField(ref this._fileLoggerFormat, value);
                lock (_loggerTargets)
                {
                    // Reconfigure all the formats for the targets currently on this logger
                    foreach (var LoggerTarget in this._loggerTargets)
                    {
                        // Check the the target is a file target or something else
                        if (LoggerTarget is not FileTarget FileLayoutTarget) continue;

                        // If it's a valid file target and not our master target instance, apply our new format
                        if (!FileLayoutTarget.Name.StartsWith($"Master_{SharpLogBroker.LogBrokerName}"))
                            FileLayoutTarget.Layout = new SimpleLayout(this._fileLoggerFormat.LoggerFormatString);
                    }
                }

                // Once done, refresh our log configuration values
                LogManager.ReconfigExistingLoggers();
            }
        }
        public SharpConsoleTargetFormat ConsoleTargetFormat
        {
            get => this._consoleLoggerFormat ?? SharpLogBroker.DefaultConsoleFormat;
            set
            {
                // Store the new format value and apply it to all of our targets
                this.SetField(ref this._consoleLoggerFormat, value);
                lock (_loggerTargets)
                {
                    // Reconfigure all the formats for the targets currently on this logger
                    foreach (var LoggerTarget in this._loggerTargets)
                    {
                        // Check the the target is a file target or something else
                        if (LoggerTarget is not ColoredConsoleTarget ConsoleLayoutTarget) continue;

                        // If it's a valid file target and not our master target instance, apply our new format
                        if (!ConsoleLayoutTarget.Name.StartsWith($"Master_{SharpLogBroker.LogBrokerName}"))
                            ConsoleLayoutTarget.Layout = new SimpleLayout(this._consoleLoggerFormat.LoggerFormatString);
                    }
                }

                // Once done, refresh our log configuration values
                LogManager.ReconfigExistingLoggers();
            }
        }

        // Public facing scope properties used to configure logging output variables
        public KeyValuePair<string, object>[] ScopeProperties
        {
            get => this._evaluateScopeProperties();
            private set => this._scopeProperties = value.ToList();
        }

        // Public facing collection of supported logging types
        public LoggerActions LoggerType
        {
            get => this._loggerType;
            set => this.SetField(ref this._loggerType, value);
        }
        public bool IsCustomLogger => this._loggerType == LoggerActions.CustomLogger;
        public bool IsUniversalLogger => this._loggerType.HasFlag(LoggerActions.UniversalLogger);
        public bool IsFileLogger => this.IsUniversalLogger || this._loggerType.HasFlag(LoggerActions.FileLogger);
        public bool IsConsoleLogger => this.IsUniversalLogger || this._loggerType.HasFlag(LoggerActions.ConsoleLogger);

        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Overrides the ToString call on a logger object to return a friendly logger name string
        /// </summary>
        /// <returns>String holding information about this logger</returns>
        public override string ToString()
        {
            // Build and return a new string value here which will hold our logger information
            string LoggerString = $"{this.LoggerName} ({this.LoggerType}) - ";
            LoggerString += $"{this.LoggerRules.Length} Rule{(this.LoggerRules.Length == 1 ? string.Empty : "s")} - ";
            LoggerString += $"{this.LoggerTargets.Length} Target{(this.LoggerTargets.Length == 1 ? string.Empty : "s")}";
            
            // Return the built string holding our logger values
            return LoggerString;
        }
        /// <summary>
        /// Routine to run when this logger instance is destroyed/released from the broker
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Lock our rules and targets to avoid issues with access
                lock (this._loggerRules) lock (this._loggerTargets)
                {
                    // Remove all the rules and targets using the event handler to we clear out of the broker list too
                    for (int RuleIndex = 0; RuleIndex < this._loggerRules.Count; RuleIndex++)
                            this._loggerRules.RemoveAt(RuleIndex);
                    for (int TargetIndex = 0; TargetIndex < this._loggerTargets.Count; TargetIndex++)
                        this._loggerTargets.RemoveAt(TargetIndex);
                }
                
                // Remove the logger from the Broker pool and log out that we've completed this routine.
                SharpLogBroker.DestroyLogger(this);
                SharpLogBroker.MasterLogger.WriteLog($"CLEARED OUT ALL RULES AND TARGETS FOR LOGGER {this.LoggerName}!", LogType.TraceLog);
                SharpLogBroker.MasterLogger.WriteLog($"LOGGER {this.LoggerName} HAS BEEN REMOVED FROM OUR BROKER!", LogType.TraceLog);
            }
            catch (Exception DestroyLoggerEx)
            {
                // Log the Exception out using the log broker logger
                SharpLogBroker.MasterLogger.WriteLog($"EXCEPTION THROWN DURING LOGGER REMOVAL PROCESS!", LogType.TraceLog);
                SharpLogBroker.MasterLogger.WriteException($"EXCEPTION IS BEING LOGGED BELOW", DestroyLoggerEx, LogType.TraceLog);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a new FalconLogger object and adds it to the logger pool.
        /// </summary>
        /// <param name="LoggerType">Type of actions/targets to configure for this logger</param>
        /// <param name="MinLevel">Min Log Level for output values being written</param>
        /// <param name="MaxLevel">Max Log Level for output values being written</param>
        /// <param name="LoggerName">Name of this logger which will be included in the output strings for it</param>
        public SharpLogger(LoggerActions LoggerType, string LoggerName = "", LogType MinLevel = LogType.TraceLog, LogType MaxLevel = LogType.FatalLog)
        {
            // If the broker isn't built, just set it up with no logging levels supported
            if (!SharpLogBroker.LogBrokerInitialized) SharpLogBroker.InitializeLogging();
            
            // Set Min and Max logging levels and make sure they comply with the logging broker
            this.MinLevel = SharpLogBroker.MinLevel == LogType.NoLogging
                ? LogType.NoLogging
                : MinLevel > SharpLogBroker.MinLevel
                    ? SharpLogBroker.MinLevel
                    : MinLevel;
            this.MaxLevel = SharpLogBroker.MaxLevel == LogType.NoLogging
                ? LogType.NoLogging
                : MaxLevel > SharpLogBroker.MaxLevel
                    ? SharpLogBroker.MaxLevel
                    : MaxLevel;

            // Store values and configure the name/GUID/time values for this logger instance now
            this.LoggerType = LoggerType;
            this.TimeCreated = DateTime.Now;
            this.LoggerGuid = Guid.NewGuid();
            this.LoggerClass = this._getCallingClass();

            // Store new name value for the logger based on the provided input value
            LoggerName = string.IsNullOrWhiteSpace(LoggerName) ? this._getCallingClass(true) : LoggerName;
            this.LoggerName =  $"{LoggerName}_{this.LoggerGuid.ToString("D").ToUpper()}";

            // Build new lists for our logger target types and store event handlers for processing changes
            LogManager.Configuration ??= new LoggingConfiguration();
            this._nLogger = LogManager.GetLogger(this.LoggerName);
            this._loggerTargets = new ObservableCollection<Target>();
            this._loggerRules = new ObservableCollection<LoggingRule>();
            this._loggerRules.CollectionChanged += this._loggerRulesOnCollectionChanged;
            this._loggerTargets.CollectionChanged += this._loggerTargetsOnCollectionChanged;

            // Configure the basic scope properties for this logging instance now
            this._scopeProperties = new List<KeyValuePair<string, object>>()
            {
                new("calling-class", null),
                new("calling-method", null),
                new("calling-class-short", null),
                new("calling-method-short", null),
                new("logger-name", this.LoggerName),
                new("logger-class", this.LoggerClass)
            };

            // If we have the custom logger type specified, then build new default targets for this logger
            if (!this.IsCustomLogger)
            {
                // Get our master target from the SharpLogBroker and add a rule into this logger for it
                if (this.IsFileLogger || this.IsUniversalLogger)
                    if (!this.RegisterTarget(SharpLogBroker.MasterFileTarget))
                        throw new InvalidOperationException(
                            $"Error! Failed to store default file target for logger {this.LoggerName}!");

                    // Get our master target from the SharpLogBroker and add a rule into this logger for it
                if (this.IsConsoleLogger || this.IsUniversalLogger) 
                    if (!this.RegisterTarget(SharpLogBroker.MasterConsoleTarget))
                        throw new InvalidOperationException(
                            $"Error! Failed to store default console target for logger {this.LoggerName}!");
            }

            // Add self to queue and validate our _nLogger has been built
            if (!SharpLogBroker.RegisterLogger(this))
                throw new InvalidOperationException("Error! Failed to register logger on the session broker!");
            
            // Log out this logger has been built correctly and exit out 
            this.WriteLog($"LOGGER '{this.LoggerName}' HAS BEEN SPAWNED CORRECTLY! (LOGGER COUNT: {SharpLogBroker.LoggerPool.Length})", LogType.InfoLog);
            this.WriteLog($"LOGGER INFORMATION HAS BEEN REPORTED AS FOLLOWS: {this}", LogType.TraceLog);
        }
        /// <summary>
        /// Routine to run when this logger instance is destroyed/released from the broker
        /// </summary>
        ~SharpLogger()
        {
            // Run the dispose method on this object if it needs to be killed
            if (SharpLogBroker.LoggerPool.Contains(this) || SharpLogBroker.FindLoggers(this.LoggerName).Any())
                this.Dispose();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------


        /// <summary>
        /// Sets new scope properties onto our logger instances and clears out the old values
        /// </summary>
        /// <param name="PropertiesToAdd">The properties to include in the scope class</param>
        public void SetScopeProperties(params KeyValuePair<string, object>[] PropertiesToAdd)
        {
            // First make sure we don't have duplicate key values being provided in
            PropertiesToAdd = PropertiesToAdd
                .GroupBy(PropObj => PropObj.Key)
                .Select(PropKeySet => PropKeySet.FirstOrDefault())
                .ToArray();

            // Take each of these property values built and store them on our instance
            this._scopeProperties = PropertiesToAdd.ToList();
        }
        /// <summary>
        /// Inserts new scope properties onto our logger instances
        /// </summary>
        /// <param name="PropertiesToAdd">The properties to include in the scope class</param>
        public void AddScopeProperties(params KeyValuePair<string, object>[] PropertiesToAdd)
        {
            // Take each of these property values built and store them on our instance
            foreach (var ScopeProperty in PropertiesToAdd)
            {
                // Check if this property key exists or not first by looking for an index matching the name provided
                int IndexOfExisting = this._scopeProperties.FindIndex(PropObj => PropObj.Key == ScopeProperty.Key);

                // If no match is found, then add this instance in. Otherwise update an existing one
                if (IndexOfExisting != -1) this._scopeProperties[IndexOfExisting] = ScopeProperty; 
                else this._scopeProperties.Add(new KeyValuePair<string, object>(ScopeProperty.Key, ScopeProperty.Value));
            }
        }
        /// <summary>
        /// Removes desired scope properties from the logger instance by name lookups
        /// </summary>
        /// <param name="PropertiesToRemove">The names of the properties to remove from the scope class</param>
        public void RemoveScopeProperties(params string[] PropertiesToRemove)
        {
            // Loop all the names and remove the properties from the collection as needed
            this._scopeProperties.RemoveAll(PropPair => PropertiesToRemove.Contains(PropPair.Key));
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes our a log entry using the given message and log level
        /// </summary>
        /// <param name="LogMessage">Message to write</param>
        /// <param name="Level">Level to log</param>
        public void WriteLog(string LogMessage, LogType Level = LogType.DebugLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Using the built scope properties, write our log entries out to the targets now
            using (this._nLogger.PushScopeProperties(this.ScopeProperties))
            {
                // Check to see if we've got new line splits in the log message.
                if (!LogMessage.Contains("\r") && !LogMessage.Contains("\n")) this._nLogger.Log(Level.ToNLevel(), LogMessage);
                else 
                {
                    // Split the log message contents into an array and print them out
                    string[] SplitLogMessages = LogMessage
                        .Split(new[] { "\n\r" }, StringSplitOptions.None)
                        .ToArray();

                    // Log each of the log lines out using the class NLogger
                    foreach (var MessageString in SplitLogMessages)
                        this._nLogger.Log(Level.ToNLevel(), MessageString);
                }
            }
        }
        /// <summary>
        /// Writes an exceptions contents out to the logger
        /// </summary>
        /// <param name="LoggedEx">LoggedEx to write</param>
        /// <param name="Level">Level to log it</param>
        public void WriteException(Exception LoggedEx, LogType Level = LogType.ErrorLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Using the built scope properties, write our log entries out to the targets now
            using (this._nLogger.PushScopeProperties(this.ScopeProperties))
            {
                // If the exception thrown is null, don't do any of this
                if (LoggedEx == null) return;

                // Log out information about the exception being thrown
                this._nLogger.Log(Level.ToNLevel(), $"EXCEPTION THROWN FROM {LoggedEx?.TargetSite}. DETAILS ARE SHOWN BELOW");
                this._nLogger.Log(Level.ToNLevel(), $"\tEX MESSAGE {LoggedEx?.Message}");
                this._nLogger.Log(Level.ToNLevel(), $"\tEX SOURCE  {LoggedEx?.Source}");
                this._nLogger.Log(Level.ToNLevel(), $"\tEX TARGET  {LoggedEx?.TargetSite?.Name}");
                this._nLogger.Log(Level.ToNLevel(),
                    LoggedEx?.StackTrace == null
                        ? "FURTHER DIAGNOSTIC INFO IS NOT AVAILABLE AT THIS TIME."
                        : $"\tEX STACK\n{LoggedEx.StackTrace.Replace("\n", "\n\t")}");

                // If our inner exception is not null, run it through this logger.
                this._nLogger.Log(Level.ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW");
                this.WriteException($"{LoggedEx.GetType().Name} -- INNER EXCEPTION", LoggedEx.InnerException, Level);
            }
        }
        /// <summary>
        /// Writes an exception object out.
        /// </summary>
        /// <param name="MessageExInfo">Info message</param>
        /// <param name="LoggedEx">LoggedEx to write</param>
        /// <param name="LogLevels">Levels. Msg and then LoggedEx</param>
        public void WriteException(string MessageExInfo, Exception LoggedEx, params LogType[] LogLevels)
        {
            // Check level count and make sure logging is set to on
            if (!this.LoggingEnabled) return;
            if (LogLevels.Length == 0) { LogLevels = new LogType[] { LogType.ErrorLog, LogType.ErrorLog }; }
            if (LogLevels.Length == 1) { LogLevels = LogLevels.Append(LogLevels[0]).ToArray(); }

            // Write Log Message then exception and all information found from the exception here
            using (this._nLogger.PushScopeProperties(this.ScopeProperties))
            {
                // If the exception thrown is null, don't do any of this
                if (LoggedEx == null) return;

                // Log out information about the exception being thrown
                this._nLogger.Log(LogLevels[0].ToNLevel(), MessageExInfo);
                this._nLogger.Log(LogLevels[0].ToNLevel(), $"EXCEPTION THROWN FROM {LoggedEx?.TargetSite}. DETAILS ARE SHOWN BELOW");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX MESSAGE {LoggedEx?.Message}");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX SOURCE  {LoggedEx?.Source}");
                this._nLogger.Log(LogLevels[1].ToNLevel(), $"\tEX TARGET  {LoggedEx?.TargetSite?.Name}");
                this._nLogger.Log(LogLevels[1].ToNLevel(),
                    LoggedEx?.StackTrace == null
                        ? "FURTHER DIAGNOSTIC INFO IS NOT AVAILABLE AT THIS TIME."
                        : $"\tEX STACK\n{LoggedEx.StackTrace.Replace("\n", "\n\t")}");

                // If our inner exception is not null, run it through this logger.
                this._nLogger.Log(LogLevels[1].ToNLevel(), "EXCEPTION CONTAINS CHILD EXCEPTION! LOGGING IT NOW");
                this.WriteException(MessageExInfo, LoggedEx.InnerException, LogLevels);
            }
        }
        /// <summary>
        /// Writes an object of any kind out as a JSON string to the log file
        /// </summary>
        /// <param name="ObjectToLog">The object we need to write out to our log file</param>
        /// <param name="UseTabs">When true, the output json string will be tab formatted</param>
        /// <param name="Level">The level to log the object at</param>
        public void WriteObjectJson(object ObjectToLog, bool UseTabs = true, LogType Level = LogType.DebugLog)
        {
            // Make sure logging is not set to off right now
            if (!this.LoggingEnabled) return;

            // Convert the object to a string and split it out based on new line characters if we're using JSON tabs
            string ObjectString = JsonConvert.SerializeObject(ObjectToLog, UseTabs ? Formatting.Indented : Formatting.None);
            using (this._nLogger.PushScopeProperties(this.ScopeProperties))
            {
                // If not using tab formatting, then just write the string out
                if (!UseTabs) this._nLogger.Log(Level.ToNLevel(), ObjectString);
                else
                {
                    // Split the JSON content into a set of strings and log them all out
                    string[] ObjectStrings = ObjectString
                        .Split(new[] { "\n\r" }, StringSplitOptions.None)
                        .ToArray();

                    // Log each of the log lines out using the class NLogger
                    foreach (var ObjectJsonString in ObjectStrings)
                        this._nLogger.Log(Level.ToNLevel(), ObjectJsonString);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Allows us to register new custom targets for a logger instance on the go
        /// </summary>
        /// <param name="TargetToRegister">The target we wish to add into this logger</param>
        /// <returns>True if the target is built, false if it is not</returns>
        public bool RegisterTarget(Target TargetToRegister)
        {
            // Lock our collection of targets before trying to query them
            lock (this._loggerRules) lock (this._loggerTargets)
            {
                // Make sure we don't have this target anywhere before trying to add it in now 
                if (this._loggerTargets.Any(ExistingTarget => ExistingTarget.Name == TargetToRegister.Name))
                    return false;

                // BUG: This seems to be stopping us from using custom formats
                /*
                 * // If we don't have a target from the master logger, we can apply this loggers configuration to it now
                 * if (TargetToRegister.Name.StartsWith($"Master_{SharpLogBroker.LogBrokerName}"))
                 * {
                 *     // For each target on this logger that is NOT the master target type, apply a new format for it
                 *     if (TargetToRegister is FileTarget FileLayoutTarget)
                 *         FileLayoutTarget.Layout = new SimpleLayout(this.FileTargetFormat.LoggerFormatString);
                 *     if (TargetToRegister is ColoredConsoleTarget ConsoleLayoutTarget)
                 *         ConsoleLayoutTarget.Layout = new SimpleLayout(this.ConsoleTargetFormat.LoggerFormatString);
                 * }
                */

                // Now register a new rule for the file target and store it on the logging configuration for NLog
                LoggingRule CustomTargetRule = new LoggingRule(
                    $"*{this.LoggerGuid.ToString("D").ToUpper()}*",
                    this._minLevel,
                    this._maxLevel,
                    TargetToRegister
                );

                // Finally insert the newly built rule and target onto our logger instance then exit out
                this._loggerRules.Add(CustomTargetRule);
                this._loggerTargets.Add(TargetToRegister);
                return true;
            }
        }
        /// <summary>
        /// Allows us to register new custom targets for a logger instance on the go
        /// </summary>
        /// <param name="TargetToDestroy">The target we wish to add into this logger</param>
        /// <returns>True if the target is built, false if it is not</returns>
        public bool RemoveTarget(Target TargetToDestroy)
        {
            // Lock our collection of targets before trying to query them
            lock (this._loggerRules) lock (this._loggerTargets)
            {
                // Make sure we don't have this target anywhere before trying to add it in now 
                var RulesToRemove = this._loggerRules
                    .Where(ExistingRule => ExistingRule.Targets.Contains(TargetToDestroy))
                    .ToList();
                var TargetsToRemove = this._loggerTargets
                    .Where(ExistingTarget => ExistingTarget.Name != TargetToDestroy.Name)
                    .ToList();

                // Make sure we've got at least one or more object to remove out from our logger
                if (RulesToRemove.Count == 0 && TargetsToRemove.Count == 0) return false;
                foreach (var LoggingRule in RulesToRemove) this._loggerRules.Remove(LoggingRule);
                foreach (var LoggingTarget in TargetsToRemove) this._loggerTargets.Remove(LoggingTarget);

                // Once done removing targets and rule, return out passed
                return true;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the name of the calling method.
        /// </summary>
        /// <param name="SkipFrames">The number of frames to skip when running this method. Used to get correct names when evaluating properties</param>
        /// <returns>String of the full method name.</returns>
        private string _getCallingMethod(bool SplitString = false, int SkipFrames = 2)
        {
            // Setup values for finding our calling type 
            Type DeclaredType;      // The type being declared to call this method
            string FullCallName;    // The full name of the call to return out

            // Iterate the stack frame while possible or until our declared type is null
            do
            {
                // Find the current method caller and store the stack. 
                MethodBase MethodBase = new StackFrame(SkipFrames, false).GetMethod();
                
                // Store the declared type value here
                DeclaredType = MethodBase.DeclaringType;
                if (DeclaredType == null) { return MethodBase.Name; }

                // Skip frame increased and keep checking.
                FullCallName = (DeclaredType.FullName + "." + MethodBase.Name).Replace("..", ".");
                SkipFrames++;
            }
            while (DeclaredType.Module.Name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));

            // Check for split values and return out accordingly 
            if (!SplitString) { return FullCallName; }
            var FullNameSplit = FullCallName.Split('.');
            return FullNameSplit[FullNameSplit.Length - 1];
        }
        /// <summary>
        /// Gets the name of the calling method.
        /// </summary>
        /// <param name="SkipFrames">The number of frames to skip when running this method. Used to get correct names when evaluating properties</param>
        /// <returns>String of the full method name.</returns>
        private string _getCallingClass(bool SplitString = false, int SkipFrames = 2)
        {
            // Find the current method caller and store the stack. 
            MethodBase MethodBase = new StackFrame(SkipFrames, false).GetMethod();
            if (MethodBase.DeclaringType == null) return string.Empty;

            // Store the full name value pulled in here
            string FullClassName = MethodBase.DeclaringType.FullName;
        
            // Check for split values and return out accordingly 
            if (!SplitString) { return FullClassName; }
            var FullNameSplit = FullClassName.Split('.');
            return FullNameSplit[FullNameSplit.Length - 1]; 
        }

        /// <summary>
        /// Calculates the values in the scope properties to log out to our targets
        /// </summary>
        /// <returns>The calculated scope properties for our output log line values</returns>
        private KeyValuePair<string, object>[] _evaluateScopeProperties()
        {
            // Setup an output list of values to log out
            var OutputProperties = new List<KeyValuePair<string, object>>();

            // Look at all of our scope properties and find their values here
            lock (this._scopeProperties)
            {
                foreach (var ScopeProperty in this._scopeProperties)
                {
                    // Look at our string value here and calculate the value if needed
                    if (ScopeProperty.Key == "calling-method")
                        OutputProperties.Add(new KeyValuePair<string, object>("calling-method", this._getCallingMethod(false, 4)));
                    else if (ScopeProperty.Key == "calling-method-short")
                        OutputProperties.Add(new KeyValuePair<string, object>("calling-method-short", this._getCallingMethod(true, 4)));
                    else if (ScopeProperty.Key == "calling-class")
                        OutputProperties.Add(new KeyValuePair<string, object>("calling-class", this._getCallingClass(false, 4)));
                    else if (ScopeProperty.Key == "calling-class-short")
                        OutputProperties.Add(new KeyValuePair<string, object>("calling-class-short", this._getCallingClass(true, 4)));
                    else if (ScopeProperty.Value is Func<object> ScopeFunction)
                        OutputProperties.Add(new KeyValuePair<string, object>(ScopeProperty.Key, ScopeFunction.Invoke().ToString()));
                    else
                    {
                        // If it's not a calling class value or a calculated value, then just copy the value across here
                        OutputProperties.Add(new KeyValuePair<string, object>(ScopeProperty.Key, ScopeProperty.Value));
                    }
                }
            }

            // Once done looping our values, return out the built list of properties
            return OutputProperties.ToArray();
        }
    }
}
