namespace SharpLogger
{
    /// <summary>
    /// Base format type for a logger output configuration
    /// </summary>
    public class SharpTargetFormat
    {
        // Base fields of our logger configuration. These apply to all format values
        public readonly string LoggerMessage = "${message}";
        public readonly string LoggerLevel = "${level:uppercase=true}";
        public readonly string LoggerName = "${scope-property:logger-class:whenEmpty=NULL}";

        // Public facing string property holding our base configuration string
        public string LoggerFormatString => $"[{this.LoggerName}][{this.LoggerLevel}] ::: {this.LoggerMessage}";
    }

    /// <summary>
    /// Class which holds the layout for a logger format type when writing to a file
    /// </summary>
    public class SharpFileTargetFormat : SharpTargetFormat
    {
        // Key values to help setup our string for the console format
        public string LoggerDate = "${date:format=MM-dd-yyyy hh\\:mm\\:ss}";
        public string LoggerCallingClass = "${scope-property:calling-class:whenEmpty=NO_CALLER_FOUND}";

        // Public facing string which holds our configuration string built out based on this structure
        public new string LoggerFormatString =>
            $"[{this.LoggerDate}][{this.LoggerLevel}][{this.LoggerName}][{this.LoggerCallingClass}] ::: {this.LoggerMessage}";
    }
    /// <summary>
    /// Class which holds the layout for a logger format type when writing to a console
    /// </summary>
    public class SharpConsoleTargetFormat : SharpTargetFormat
    {
        // Key values to help setup our string for the console format
        public string LoggerDate = "${date:format=hh\\:mm\\:ss}";
        public string LoggerCallingClass = "${scope-property:calling-class-short:whenEmpty=NULL}";

        // Public facing string which holds our configuration string built out based on this structure
        public new string LoggerFormatString =>
            $"[{this.LoggerDate}][{this.LoggerLevel}][{this.LoggerName}][{this.LoggerCallingClass}] ::: {this.LoggerMessage}";
    }
}
