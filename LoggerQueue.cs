using System;
using System.Collections.Generic;
using System.Linq;
using SharpLogger.LoggerObjects;
using SharpLogger.LoggerSupport;

// Static using for logger objects 

namespace SharpLogger
{
    /// <summary>
    /// Class which holds all built loggers for this active instance.
    /// </summary>
    public static class LoggerQueue
    {
        #region Custom Events
        #endregion // Custom Events

        #region Fields
        
        // List of all logger items in the pool.
        private static List<BaseLogger> _loggerPool = new();
        
        #endregion // Fields

        #region Properties
        #endregion // Properties

        #region Structs and Classes
        #endregion // Structs and Classes

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Spawns in a new logger instance based on a name and type value provided for the logger 
        /// </summary>
        /// <param name="LoggerName">Name of the logger to build</param>
        /// <param name="LoggerType">Type of the logger to build</param>
        /// <returns>The logger instance built if one exists or null if something goes wrong here</returns>
        public static BaseLogger SpawnLogger(string LoggerName, LoggerActions LoggerType)
        {
            // Find the logger instance we need from the pool if it exists. Otherwise build a new one
            BaseLogger BuiltLogger = GetLoggers(LoggerType).FirstOrDefault(LoggerObj => LoggerObj.LoggerName.StartsWith(LoggerName));
            if (BuiltLogger != null) return BuiltLogger;

            // If the logger object was null, then we build a new one and return it out
            BuiltLogger = LoggerType switch
            {
                // Deal with all the possible logger types and build one as needed
                LoggerActions.ConsoleLogger => new ConsoleLogger(LoggerName),
                LoggerActions.FileLogger => new FileLogger(LoggerName),
                LoggerActions.MasterLogger => new MasterLogger(LoggerName),
                LoggerActions.SubServiceLogger => new SubServiceLogger(LoggerName),
                _ => throw new ArgumentOutOfRangeException(nameof(LoggerType), LoggerType, $"LOGGER TYPE {LoggerType} IS INVALID!")
            };

            // Add this logger object to our pool and return it out here
            return BuiltLogger;
        }

        /// <summary>
        /// Adds a logger item to the pool of all loggers.
        /// </summary>
        /// <param name="LoggerItem">Item to add to the pool.</param>
        public static void AddLoggerToPool(BaseLogger LoggerItem)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find existing loggers that may have the same name as this logger obj.
                if (_loggerPool.Any(LogObj => LogObj.LoggerGuid == LoggerItem.LoggerGuid))
                {
                    // Update current.
                    int IndexOfExisting = _loggerPool.IndexOf(LoggerItem);
                    _loggerPool[IndexOfExisting] = LoggerItem;
                    return;
                }

                // If the logger didn't get added (no dupes) do it not.
                _loggerPool.Add(LoggerItem);
            }
        }
        /// <summary>
        /// Removes the logger passed from the logger queue
        /// </summary>
        /// <param name="LoggerItem">Logger to yank</param>
        public static void RemoveLoggerFromPool(BaseLogger LoggerItem)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Pull out all the dupes.
                var NewLoggers = _loggerPool.Where(LogObj =>
                LogObj.LoggerGuid != LoggerItem.LoggerGuid).ToList();

                // Check if new logger is in loggers filtered or not and store it.
                if (NewLoggers.Contains(LoggerItem)) NewLoggers.Remove(LoggerItem);
                _loggerPool = NewLoggers;
            }
        }

        /// <summary>
        /// Gets all loggers that exist currently.
        /// </summary>
        /// <returns></returns>
        public static List<BaseLogger> GetLoggers()
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Get the logger objects and return out 
                return _loggerPool;
            }
        }
        /// <summary>
        /// Gets loggers based on a given type of logger.
        /// </summary>
        /// <param name="TypeOfLogger">Type of logger to get.</param>
        /// <returns>List of all loggers for this type.</returns>
        public static List<BaseLogger> GetLoggers(LoggerActions TypeOfLogger)
        {
            // Lock the logger pool so we don't have thread issues
            lock (_loggerPool)
            {
                // Find and return the matching logger object instances
                return _loggerPool.Where(LogObj => LogObj.LoggerType == TypeOfLogger).ToList();
            }
        }
    }
}
