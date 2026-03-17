
namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Centralized logging utility for the MaggyHelper/Ingeste mod.
/// Provides consistent logging with mod identification and log levels.
/// </summary>
public static class IngesteLogger
{
    private const string LOG_TAG = "MaggyHelper";
    
    /// <summary>
    /// Log a verbose/trace message (only in debug mode).
    /// </summary>
    public static void Verbose(string message)
    {
        if (MaggyHelperModule.Settings?.DebugMode == true)
        {
            Logger.Log(LogLevel.Verbose, LOG_TAG, message);
        }
    }
    
    /// <summary>
    /// Log a debug message (only in debug mode).
    /// </summary>
    public static void Debug(string message)
    {
        if (MaggyHelperModule.Settings?.DebugMode == true)
        {
            Logger.Log(LogLevel.Debug, LOG_TAG, message);
        }
    }
    
    /// <summary>
    /// Log an informational message.
    /// </summary>
    public static void Info(string message)
    {
        Logger.Log(LogLevel.Info, LOG_TAG, message);
    }
    
    /// <summary>
    /// Log a warning message.
    /// </summary>
    public static void Warn(string message)
    {
        Logger.Log(LogLevel.Warn, LOG_TAG, message);
    }
    
    /// <summary>
    /// Log an error message.
    /// </summary>
    public static void Error(string message)
    {
        Logger.Log(LogLevel.Error, LOG_TAG, message);
    }
    
    /// <summary>
    /// Log an error message with exception details.
    /// </summary>
    public static void Error(Exception ex, string message)
    {
        Logger.Log(LogLevel.Error, LOG_TAG, $"{message}: {ex.Message}");
        Logger.Log(LogLevel.Debug, LOG_TAG, ex.StackTrace ?? "No stack trace");
    }
    
    /// <summary>
    /// Log an error message from an exception.
    /// </summary>
    public static void Error(string context, Exception ex)
    {
        Logger.Log(LogLevel.Error, LOG_TAG, $"{context}: {ex.Message}");
        Logger.Log(LogLevel.Debug, LOG_TAG, ex.StackTrace ?? "No stack trace");
    }
    
    /// <summary>
    /// Log with a custom tag (for subsystem identification).
    /// </summary>
    public static void Log(LogLevel level, string tag, string message)
    {
        Logger.Log(level, $"{LOG_TAG}/{tag}", message);
    }
    
    /// <summary>
    /// Log a formatted message at info level.
    /// </summary>
    public static void InfoFormat(string format, params object[] args)
    {
        Logger.Log(LogLevel.Info, LOG_TAG, string.Format(format, args));
    }
    
    /// <summary>
    /// Log a formatted message at debug level.
    /// </summary>
    public static void DebugFormat(string format, params object[] args)
    {
        if (MaggyHelperModule.Settings?.DebugMode == true)
        {
            Logger.Log(LogLevel.Debug, LOG_TAG, string.Format(format, args));
        }
    }
    
    /// <summary>
    /// Log a formatted message at warning level.
    /// </summary>
    public static void WarnFormat(string format, params object[] args)
    {
        Logger.Log(LogLevel.Warn, LOG_TAG, string.Format(format, args));
    }
    
    /// <summary>
    /// Log a formatted message at error level.
    /// </summary>
    public static void ErrorFormat(string format, params object[] args)
    {
        Logger.Log(LogLevel.Error, LOG_TAG, string.Format(format, args));
    }
}
