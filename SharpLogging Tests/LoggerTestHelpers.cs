using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpLogger_Tests
{
    /// <summary>
    /// Helper class holding routines that are commonly used across all different test suites
    /// </summary>
    internal static class LoggerTestHelpers
    {
        #region Custom Events
        #endregion //Custom Events

        #region Fields
        
        // Constants for logging output
        private static readonly int _splittingLineSize = 100;               // Size of the splitting lines to write in console output
        private static readonly string _splittingLineChar = "=";            // Character to use in the splitting line output

        #endregion //Fields

        #region Properties
        #endregion //Properties

        #region Structs and Classes
        #endregion //Structs and Classes

        // ------------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Prints out a splitting line in the console window during unit tests
        /// </summary>
        /// <param name="LineSize">Size of the line to print in characters</param>
        /// <param name="LineChar">The character to print for the line</param>
        public static void SeparateConsole(int LineSize = -1, string LineChar = null)
        {
            // Setup line size and character values and print out the splitting line
            LineChar ??= _splittingLineChar;
            LineSize = LineSize == -1 ? _splittingLineSize : LineSize;

            // Print out the splitting line and exit out
            Console.WriteLine(string.Join(string.Empty, Enumerable.Repeat(LineChar, LineSize)) + "\n");
        }
        /// <summary>
        /// Logs out that test suites are building and starting for a given test class
        /// </summary>
        /// <param name="TestSuite">The sending test suite which tests are running for</param>
        public static void LogTestSuiteSetupStarting(object TestSuite)
        {
            // Split the console output, log starting up and exit out
            SeparateConsole();
            Console.WriteLine($"Starting Setup for Test Class {TestSuite.GetType().Name} now...");
        }
        /// <summary>
        /// Logs out that test suites are ready to run for a given test class
        /// </summary>
        /// <param name="TestSuite">The sending test suite which tests are running for</param>
        public static void LogTestSuiteStartupEnded(object TestSuite)
        {
            // Write the completed state value, split the console output, and exit out
            Console.WriteLine($"\t--> DONE! All required invokers and backing objects for test class {TestSuite.GetType().Name} have been built correctly!\n");
            SeparateConsole();
        }
        /// <summary>
        /// Logs out that a test method has completed without issues
        /// </summary>
        /// <param name="Message">An optional message to log out when this routine is done</param>
        /// <param name="CallingName">Name of the method which has been run</param>
        public static void LogTestMethodCompleted(string Message = "", [CallerMemberName] string CallingName = "")
        {
            // Log passed and exit out of this test method
            Console.WriteLine();
            SeparateConsole();
            Console.WriteLine($"Test method {CallingName} has completed without issues!");
            if (!string.IsNullOrWhiteSpace(Message)) Console.WriteLine(Message);
            Console.WriteLine();
            SeparateConsole();
        }
    }
}
