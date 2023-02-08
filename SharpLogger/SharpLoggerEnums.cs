namespace SharpLogger
{
    /// <summary>
    /// Wrapped log level type so NLog isn't a required ref for anything that uses this.
    /// </summary>
    public enum LogType : int
    {
        // Basic logging level values
        TraceLog,       // Compares to LogLevel.Trac
        DebugLog,       // Compares to LogLevel.Debug
        InfoLog,        // Compares to LogLevel.Info
        WarnLog,        // Compares to LogLevel.Warn
        ErrorLog,       // Compares to LogLevel.Error
        FatalLog,       // Compares to LogLevel.Fatal
        NoLogging       // Compares to LogLevel.Off
    }
    /// <summary>
    /// Custom type of logger being used.
    /// </summary>
    public enum LoggerActions : int
    {
        // Main Logger Types. These will be expanded to output to more location over time
        UniversalLogger = 0x0000000,          // Logger which will write out to all possible formats
        ConsoleLogger   = 0x0000001,          // Logger made to write to a Console window
        FileLogger      = 0x0000002,          // Logger made to write to a file output
    }
    /// <summary>
    /// Compression type methods for this compressor
    /// </summary>
    public enum CompressionType
    {
        ZipCompression,        // ZIP File compression
        GZipCompression,       // GZ file compression
    }
}
