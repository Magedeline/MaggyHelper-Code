namespace MaggyHelper.HotReload;

/// <summary>
/// Service that monitors source files for changes and triggers recompilation.
/// Uses FileSystemWatcher to detect file modifications in real-time.
/// </summary>
public class FileWatcherService : IDisposable
{
    #region Fields
    
    private readonly string _watchPath;
    private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
    private readonly object _lock = new object();
    private readonly Dictionary<string, DateTime> _lastChangeTimes = new Dictionary<string, DateTime>();
    private readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(300);
    private bool _running = false;
    private bool _disposed = false;
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Fired when a source file is modified.
    /// </summary>
    public event Action<string> OnFileChanged;
    
    /// <summary>
    /// Fired when a new source file is created.
    /// </summary>
    public event Action<string> OnFileCreated;
    
    /// <summary>
    /// Fired when a source file is deleted.
    /// </summary>
    public event Action<string> OnFileDeleted;
    
    /// <summary>
    /// Fired when a source file is renamed.
    /// </summary>
    public event Action<string, string> OnFileRenamed;
    
    /// <summary>
    /// Fired when an error occurs in the watcher.
    /// </summary>
    public event Action<Exception> OnError;
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Whether the file watcher is currently active.
    /// </summary>
    public bool IsRunning => _running;
    
    /// <summary>
    /// The path being watched.
    /// </summary>
    public string WatchPath => _watchPath;
    
    /// <summary>
    /// List of file extensions to watch.
    /// </summary>
    public string[] WatchedExtensions { get; set; } = new[] { ".cs" };
    
    /// <summary>
    /// Directories to ignore (relative to watch path).
    /// </summary>
    public string[] IgnoredDirectories { get; set; } = new[] { "obj", "bin", ".git", ".vs" };
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    /// Create a new file watcher for the given path.
    /// </summary>
    /// <param name="watchPath">The root directory to watch.</param>
    public FileWatcherService(string watchPath)
    {
        if (string.IsNullOrEmpty(watchPath))
            throw new ArgumentNullException(nameof(watchPath));
        
        if (!Directory.Exists(watchPath))
            throw new DirectoryNotFoundException($"Watch path does not exist: {watchPath}");
        
        _watchPath = watchPath;
        
        Logger.Log(LogLevel.Debug, "HotReload", $"FileWatcherService created for: {watchPath}");
    }
    
    #endregion
    
    #region Start/Stop
    
    /// <summary>
    /// Start watching for file changes.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_running)
            {
                Logger.Log(LogLevel.Warn, "HotReload", "File watcher already running");
                return;
            }
            
            try
            {
                // Create a watcher for each file type
                foreach (var ext in WatchedExtensions)
                {
                    var watcher = CreateWatcher($"*{ext}");
                    _watchers.Add(watcher);
                }
                
                _running = true;
                Logger.Log(LogLevel.Info, "HotReload", $"File watcher started for {_watchPath}");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "HotReload", $"Failed to start file watcher: {ex}");
                Stop();
                throw;
            }
        }
    }
    
    /// <summary>
    /// Stop watching for file changes.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_running)
                return;
            
            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            
            _watchers.Clear();
            _running = false;
            
            Logger.Log(LogLevel.Info, "HotReload", "File watcher stopped");
        }
    }
    
    /// <summary>
    /// Restart the file watcher.
    /// </summary>
    public void Restart()
    {
        Stop();
        Start();
    }
    
    #endregion
    
    #region Watcher Creation
    
    /// <summary>
    /// Create a FileSystemWatcher for the given filter.
    /// </summary>
    private FileSystemWatcher CreateWatcher(string filter)
    {
        var watcher = new FileSystemWatcher(_watchPath, filter)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | 
                          NotifyFilters.FileName | 
                          NotifyFilters.DirectoryName |
                          NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        
        // Wire up events
        watcher.Changed += OnWatcherChanged;
        watcher.Created += OnWatcherCreated;
        watcher.Deleted += OnWatcherDeleted;
        watcher.Renamed += OnWatcherRenamed;
        watcher.Error += OnWatcherError;
        
        return watcher;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcess(e.FullPath))
            return;
        
        // Debounce rapid changes
        if (!ShouldProcessChange(e.FullPath))
            return;
        
        Logger.Log(LogLevel.Debug, "HotReload", $"File changed: {e.Name}");
        
        try
        {
            OnFileChanged?.Invoke(e.FullPath);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Error in OnFileChanged handler: {ex.Message}");
        }
    }
    
    private void OnWatcherCreated(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcess(e.FullPath))
            return;
        
        Logger.Log(LogLevel.Debug, "HotReload", $"File created: {e.Name}");
        
        try
        {
            OnFileCreated?.Invoke(e.FullPath);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Error in OnFileCreated handler: {ex.Message}");
        }
    }
    
    private void OnWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcess(e.FullPath))
            return;
        
        Logger.Log(LogLevel.Debug, "HotReload", $"File deleted: {e.Name}");
        
        try
        {
            OnFileDeleted?.Invoke(e.FullPath);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Error in OnFileDeleted handler: {ex.Message}");
        }
    }
    
    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        if (!ShouldProcess(e.FullPath))
            return;
        
        Logger.Log(LogLevel.Debug, "HotReload", $"File renamed: {e.OldName} -> {e.Name}");
        
        try
        {
            OnFileRenamed?.Invoke(e.OldFullPath, e.FullPath);
            
            // Also trigger created event for the new file
            OnFileCreated?.Invoke(e.FullPath);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Error in OnFileRenamed handler: {ex.Message}");
        }
    }
    
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        Logger.Log(LogLevel.Error, "HotReload", $"File watcher error: {exception}");
        
        try
        {
            OnError?.Invoke(exception);
        }
        catch { }
        
        // Try to restart the watcher
        try
        {
            Logger.Log(LogLevel.Info, "HotReload", "Attempting to restart file watcher after error...");
            Restart();
        }
        catch (Exception restartEx)
        {
            Logger.Log(LogLevel.Error, "HotReload", $"Failed to restart file watcher: {restartEx}");
        }
    }
    
    #endregion
    
    #region Filtering
    
    /// <summary>
    /// Check if a file should be processed based on path and extension.
    /// </summary>
    private bool ShouldProcess(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return false;
        
        // Check extension
        string extension = Path.GetExtension(fullPath);
        if (!WatchedExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        // Check for ignored directories
        string relativePath = GetRelativePath(fullPath);
        foreach (var ignored in IgnoredDirectories)
        {
            if (relativePath.Contains($"\\{ignored}\\", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith($"{ignored}\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Debounce rapid file changes to prevent multiple triggers.
    /// </summary>
    private bool ShouldProcessChange(string fullPath)
    {
        lock (_lastChangeTimes)
        {
            if (_lastChangeTimes.TryGetValue(fullPath, out var lastChange))
            {
                if (DateTime.Now - lastChange < _debounceTime)
                    return false;
            }
            
            _lastChangeTimes[fullPath] = DateTime.Now;
            
            // Clean up old entries periodically
            if (_lastChangeTimes.Count > 1000)
            {
                var oldEntries = _lastChangeTimes
                    .Where(kvp => DateTime.Now - kvp.Value > TimeSpan.FromMinutes(5))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in oldEntries)
                    _lastChangeTimes.Remove(key);
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// Get the relative path from the watch root.
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        if (fullPath.StartsWith(_watchPath, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(_watchPath.Length).TrimStart('\\', '/');
        return fullPath;
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        Stop();
        
        Logger.Log(LogLevel.Debug, "HotReload", "FileWatcherService disposed");
    }
    
    #endregion
}
