using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using MonoMod.RuntimeDetour;

namespace MaggyHelper.HotReload;

/// <summary>
/// Hot Code Reloading System for Celeste Mods.
/// Allows developers to change C# code without restarting the game.
/// 
/// Features:
/// - Watches source files for changes
/// - Dynamically compiles modified code
/// - Hot-swaps method implementations
/// - Preserves game state during reloads
/// </summary>
public static class HotReloadManager
{
    #region Fields
    
    private static readonly object _lock = new object();
    private static bool _initialized = false;
    private static bool _enabled = false;
    private static FileWatcherService _fileWatcher;
    private static CodeCompiler _compiler;
    private static readonly List<IDetour> _activeDetours = new List<IDetour>();
    private static readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
    private static readonly Dictionary<MethodBase, MethodInfo> _methodMappings = new Dictionary<MethodBase, MethodInfo>();
    private static string _sourcePath;
    private static HotReloadUI _ui;
    
    // Statistics
    private static int _reloadCount = 0;
    private static DateTime _lastReloadTime = DateTime.MinValue;
    private static readonly List<string> _reloadHistory = new List<string>();
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Whether hot reload is currently enabled and active.
    /// </summary>
    public static bool IsEnabled => _enabled && _initialized;
    
    /// <summary>
    /// Whether the hot reload system has been initialized.
    /// </summary>
    public static bool IsInitialized => _initialized;
    
    /// <summary>
    /// Number of successful reloads this session.
    /// </summary>
    public static int ReloadCount => _reloadCount;
    
    /// <summary>
    /// Time of the last successful reload.
    /// </summary>
    public static DateTime LastReloadTime => _lastReloadTime;
    
    /// <summary>
    /// Current status message for UI display.
    /// </summary>
    public static string StatusMessage { get; private set; } = "Not initialized";
    
    /// <summary>
    /// Whether a compilation is currently in progress.
    /// </summary>
    public static bool IsCompiling { get; private set; } = false;
    
    /// <summary>
    /// List of recent reload events for history display.
    /// </summary>
    public static IReadOnlyList<string> ReloadHistory => _reloadHistory.AsReadOnly();
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Fired before a hot reload begins.
    /// </summary>
    public static event Action<string> OnBeforeReload;
    
    /// <summary>
    /// Fired after a successful hot reload.
    /// </summary>
    public static event Action<string, Assembly> OnAfterReload;
    
    /// <summary>
    /// Fired when a reload fails.
    /// </summary>
    public static event Action<string, Exception> OnReloadFailed;
    
    /// <summary>
    /// Fired when status changes (for UI updates).
    /// </summary>
    public static event Action<string> OnStatusChanged;
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Initialize the hot reload system.
    /// </summary>
    /// <param name="sourcePath">Path to the source code directory to watch.</param>
    public static void Initialize(string sourcePath = null)
    {
        lock (_lock)
        {
            if (_initialized)
            {
                Logger.Log(LogLevel.Warn, "HotReload", "Hot reload already initialized");
                return;
            }
            
            try
            {
                // Determine source path
                _sourcePath = sourcePath ?? FindSourcePath();
                
                if (string.IsNullOrEmpty(_sourcePath) || !Directory.Exists(_sourcePath))
                {
                    SetStatus("Source path not found - Hot reload disabled");
                    Logger.Log(LogLevel.Warn, "HotReload", $"Source path not found: {_sourcePath}");
                    return;
                }
                
                Logger.Log(LogLevel.Info, "HotReload", $"Initializing hot reload with source path: {_sourcePath}");
                
                // Initialize compiler
                _compiler = new CodeCompiler(_sourcePath);
                
                // Initialize file watcher
                _fileWatcher = new FileWatcherService(_sourcePath);
                _fileWatcher.OnFileChanged += HandleFileChanged;
                _fileWatcher.OnFileCreated += HandleFileCreated;
                _fileWatcher.OnFileDeleted += HandleFileDeleted;
                
                // Initialize UI
                _ui = new HotReloadUI();
                
                _initialized = true;
                SetStatus("Initialized - Waiting for changes");
                
                Logger.Log(LogLevel.Info, "HotReload", "Hot reload system initialized successfully");
                
                AddToHistory("System initialized");
            }
            catch (Exception ex)
            {
                SetStatus($"Initialization failed: {ex.Message}");
                Logger.Log(LogLevel.Error, "HotReload", $"Failed to initialize hot reload: {ex}");
            }
        }
    }
    
    /// <summary>
    /// Shutdown the hot reload system and clean up resources.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            if (!_initialized)
                return;
            
            Logger.Log(LogLevel.Info, "HotReload", "Shutting down hot reload system");
            
            try
            {
                // Disable if enabled
                if (_enabled)
                    Disable();
                
                // Dispose file watcher
                _fileWatcher?.Dispose();
                _fileWatcher = null;
                
                // Dispose compiler
                _compiler?.Dispose();
                _compiler = null;
                
                // Clear detours
                ClearAllDetours();
                
                // Clear loaded assemblies
                _loadedAssemblies.Clear();
                _methodMappings.Clear();
                
                // Dispose UI
                _ui?.Dispose();
                _ui = null;
                
                _initialized = false;
                SetStatus("Shutdown complete");
                
                Logger.Log(LogLevel.Info, "HotReload", "Hot reload system shutdown complete");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "HotReload", $"Error during shutdown: {ex}");
            }
        }
    }
    
    #endregion
    
    #region Enable/Disable
    
    /// <summary>
    /// Enable hot reload watching and auto-compilation.
    /// </summary>
    public static void Enable()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                Logger.Log(LogLevel.Warn, "HotReload", "Cannot enable - not initialized");
                return;
            }
            
            if (_enabled)
            {
                Logger.Log(LogLevel.Info, "HotReload", "Hot reload already enabled");
                return;
            }
            
            _fileWatcher?.Start();
            _enabled = true;
            SetStatus("Enabled - Watching for changes");
            
            Logger.Log(LogLevel.Info, "HotReload", "Hot reload enabled");
            AddToHistory("Watching enabled");
        }
    }
    
    /// <summary>
    /// Disable hot reload watching.
    /// </summary>
    public static void Disable()
    {
        lock (_lock)
        {
            if (!_enabled)
                return;
            
            _fileWatcher?.Stop();
            _enabled = false;
            SetStatus("Disabled");
            
            Logger.Log(LogLevel.Info, "HotReload", "Hot reload disabled");
            AddToHistory("Watching disabled");
        }
    }
    
    /// <summary>
    /// Toggle hot reload on/off.
    /// </summary>
    public static void Toggle()
    {
        if (_enabled)
            Disable();
        else
            Enable();
    }
    
    #endregion
    
    #region Manual Reload
    
    /// <summary>
    /// Manually trigger a reload of a specific file.
    /// </summary>
    public static bool ReloadFile(string filePath)
    {
        if (!_initialized)
        {
            Logger.Log(LogLevel.Warn, "HotReload", "Cannot reload - not initialized");
            return false;
        }
        
        return ProcessFileChange(filePath, "manual reload");
    }
    
    /// <summary>
    /// Reload all source files.
    /// </summary>
    public static bool ReloadAll()
    {
        if (!_initialized)
        {
            Logger.Log(LogLevel.Warn, "HotReload", "Cannot reload - not initialized");
            return false;
        }
        
        Logger.Log(LogLevel.Info, "HotReload", "Starting full reload...");
        SetStatus("Full reload in progress...");
        IsCompiling = true;
        
        try
        {
            OnBeforeReload?.Invoke("all");
            
            var result = _compiler.CompileAll();
            
            if (result.Success)
            {
                ApplyAssembly(result.Assembly, "full reload");
                _reloadCount++;
                _lastReloadTime = DateTime.Now;
                SetStatus($"Full reload complete - {result.CompiledFiles} files");
                OnAfterReload?.Invoke("all", result.Assembly);
                AddToHistory($"Full reload: {result.CompiledFiles} files");
                return true;
            }
            else
            {
                SetStatus($"Reload failed: {result.Errors.Count} errors");
                Logger.Log(LogLevel.Error, "HotReload", $"Full reload failed:\n{string.Join("\n", result.Errors)}");
                OnReloadFailed?.Invoke("all", new Exception(string.Join("\n", result.Errors)));
                AddToHistory($"Full reload FAILED: {result.Errors.Count} errors");
                return false;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Reload error: {ex.Message}");
            Logger.Log(LogLevel.Error, "HotReload", $"Full reload exception: {ex}");
            OnReloadFailed?.Invoke("all", ex);
            AddToHistory($"Full reload EXCEPTION: {ex.Message}");
            return false;
        }
        finally
        {
            IsCompiling = false;
        }
    }
    
    #endregion
    
    #region File Change Handlers
    
    private static void HandleFileChanged(string filePath)
    {
        if (!_enabled)
            return;
        
        // Debounce rapid changes
        Task.Run(async () =>
        {
            await Task.Delay(100);
            ProcessFileChange(filePath, "modified");
        });
    }
    
    private static void HandleFileCreated(string filePath)
    {
        if (!_enabled)
            return;
        
        Logger.Log(LogLevel.Info, "HotReload", $"New file detected: {filePath}");
        AddToHistory($"New file: {Path.GetFileName(filePath)}");
        
        // For new files, we need to compile the entire project
        Task.Run(async () =>
        {
            await Task.Delay(200);
            ReloadAll();
        });
    }
    
    private static void HandleFileDeleted(string filePath)
    {
        if (!_enabled)
            return;
        
        Logger.Log(LogLevel.Info, "HotReload", $"File deleted: {filePath}");
        AddToHistory($"Deleted: {Path.GetFileName(filePath)}");
        
        // For deleted files, we need to recompile everything
        Task.Run(async () =>
        {
            await Task.Delay(200);
            ReloadAll();
        });
    }
    
    private static bool ProcessFileChange(string filePath, string changeType)
    {
        lock (_lock)
        {
            if (!_initialized)
                return false;
            
            string fileName = Path.GetFileName(filePath);
            Logger.Log(LogLevel.Info, "HotReload", $"Processing {changeType}: {fileName}");
            SetStatus($"Compiling: {fileName}...");
            IsCompiling = true;
            
            try
            {
                OnBeforeReload?.Invoke(filePath);
                
                var result = _compiler.CompileFile(filePath);
                
                if (result.Success)
                {
                    ApplyAssembly(result.Assembly, filePath);
                    _reloadCount++;
                    _lastReloadTime = DateTime.Now;
                    SetStatus($"Reloaded: {fileName}");
                    OnAfterReload?.Invoke(filePath, result.Assembly);
                    AddToHistory($"Reloaded: {fileName}");
                    return true;
                }
                else
                {
                    SetStatus($"Compile failed: {result.Errors.FirstOrDefault() ?? "Unknown error"}");
                    Logger.Log(LogLevel.Error, "HotReload", $"Compilation failed for {fileName}:\n{string.Join("\n", result.Errors)}");
                    OnReloadFailed?.Invoke(filePath, new Exception(string.Join("\n", result.Errors)));
                    AddToHistory($"FAILED: {fileName} ({result.Errors.Count} errors)");
                    return false;
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                Logger.Log(LogLevel.Error, "HotReload", $"Exception processing {fileName}: {ex}");
                OnReloadFailed?.Invoke(filePath, ex);
                AddToHistory($"EXCEPTION: {fileName}");
                return false;
            }
            finally
            {
                IsCompiling = false;
            }
        }
    }
    
    #endregion
    
    #region Assembly Application
    
    /// <summary>
    /// Apply a newly compiled assembly, hot-swapping method implementations.
    /// </summary>
    private static void ApplyAssembly(Assembly newAssembly, string source)
    {
        if (newAssembly == null)
            return;
        
        string assemblyName = newAssembly.GetName().Name;
        Logger.Log(LogLevel.Info, "HotReload", $"Applying assembly from {source}: {assemblyName}");
        
        // Get the original assembly
        Assembly originalAssembly = typeof(MaggyHelperModule).Assembly;
        
        // Find types marked with HotReloadable attribute or matching naming conventions
        foreach (Type newType in newAssembly.GetTypes())
        {
            try
            {
                // Find corresponding original type
                Type originalType = originalAssembly.GetType(newType.FullName);
                
                if (originalType == null)
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"New type (no original): {newType.FullName}");
                    continue;
                }
                
                // Check if type is hot-reloadable
                if (!IsTypeHotReloadable(originalType))
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"Type not hot-reloadable: {newType.FullName}");
                    continue;
                }
                
                // Hot-swap methods
                HotSwapMethods(originalType, newType);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "HotReload", $"Failed to apply type {newType.FullName}: {ex.Message}");
            }
        }
        
        // Store assembly reference
        _loadedAssemblies[assemblyName] = newAssembly;
        
        Logger.Log(LogLevel.Info, "HotReload", $"Assembly applied successfully");
    }
    
    /// <summary>
    /// Hot-swap method implementations between original and new types.
    /// </summary>
    private static void HotSwapMethods(Type originalType, Type newType)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
                          BindingFlags.Instance | BindingFlags.Static | 
                          BindingFlags.DeclaredOnly;
        
        foreach (MethodInfo newMethod in newType.GetMethods(bindingFlags))
        {
            try
            {
                // Skip special methods (constructors, property accessors, etc.)
                if (newMethod.IsSpecialName)
                    continue;
                
                // Find matching original method
                MethodInfo originalMethod = originalType.GetMethod(
                    newMethod.Name,
                    bindingFlags,
                    null,
                    newMethod.GetParameters().Select(p => p.ParameterType).ToArray(),
                    null
                );
                
                if (originalMethod == null)
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"New method (no original): {newType.Name}.{newMethod.Name}");
                    continue;
                }
                
                // Check if method is hot-reloadable
                if (!IsMethodHotReloadable(originalMethod))
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"Method not hot-reloadable: {newType.Name}.{newMethod.Name}");
                    continue;
                }
                
                // Create detour
                CreateMethodDetour(originalMethod, newMethod);
                
                Logger.Log(LogLevel.Debug, "HotReload", $"Hot-swapped: {newType.Name}.{newMethod.Name}");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "HotReload", $"Failed to hot-swap {newType.Name}.{newMethod.Name}: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Create a runtime detour from original method to new method.
    /// </summary>
    private static void CreateMethodDetour(MethodInfo originalMethod, MethodInfo newMethod)
    {
        // Remove existing detour for this method if any
        if (_methodMappings.ContainsKey(originalMethod))
        {
            // Find and dispose the old detour
            var oldDetour = _activeDetours.FirstOrDefault(d => 
                d is Hook hook && hook.Method == originalMethod);
            
            if (oldDetour != null)
            {
                oldDetour.Dispose();
                _activeDetours.Remove(oldDetour);
            }
        }
        
        // Create new detour
        var detour = new Hook(originalMethod, newMethod);
        _activeDetours.Add(detour);
        _methodMappings[originalMethod] = newMethod;
    }
    
    /// <summary>
    /// Clear all active detours.
    /// </summary>
    private static void ClearAllDetours()
    {
        foreach (var detour in _activeDetours)
        {
            try
            {
                detour?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "HotReload", $"Failed to dispose detour: {ex.Message}");
            }
        }
        
        _activeDetours.Clear();
        _methodMappings.Clear();
    }
    
    #endregion
    
    #region Hot-Reloadable Checks
    
    /// <summary>
    /// Check if a type supports hot reloading.
    /// </summary>
    private static bool IsTypeHotReloadable(Type type)
    {
        // Check for HotReloadable attribute
        if (type.GetCustomAttribute<HotReloadableAttribute>() != null)
            return true;
        
        // Check if any method has the attribute
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
                          BindingFlags.Instance | BindingFlags.Static | 
                          BindingFlags.DeclaredOnly;
        
        return type.GetMethods(bindingFlags).Any(m => m.GetCustomAttribute<HotReloadableAttribute>() != null);
    }
    
    /// <summary>
    /// Check if a method supports hot reloading.
    /// </summary>
    private static bool IsMethodHotReloadable(MethodInfo method)
    {
        // Check for HotReloadable attribute on method
        if (method.GetCustomAttribute<HotReloadableAttribute>() != null)
            return true;
        
        // Check for attribute on declaring type
        if (method.DeclaringType?.GetCustomAttribute<HotReloadableAttribute>() != null)
            return true;
        
        // Check settings for "reload all" mode
        if (MaggyHelperModule.Settings?.HotReloadAllMethods == true)
            return true;
        
        return false;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Try to find the source code path automatically.
    /// </summary>
    private static string FindSourcePath()
    {
        try
        {
            // Try to locate the Source folder relative to the running mod assembly.
            // When Everest loads the mod DLL from Code/net8.0/, the Source folder
            // is two levels up from the DLL directory.
            string asmLocation = typeof(MaggyHelperModule).Assembly.Location;
            if (!string.IsNullOrEmpty(asmLocation))
            {
                // e.g. D:\celeste\Mods\MaggyHelper\Code\net8.0\MaggyHelper.dll
                //      -> D:\celeste\Mods\MaggyHelper\Source
                string modRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(asmLocation), "..", ".."));
                string fromAssembly = Path.Combine(modRoot, "Source");
                if (Directory.Exists(fromAssembly))
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"Found source path via assembly location: {fromAssembly}");
                    return fromAssembly;
                }
            }
            
            // Try common development paths
            var possiblePaths = new[]
            {
                // Relative to the mod folder
                Path.Combine(Everest.Loader.PathMods, "MaggyHelper", "Source"),
                // Absolute common paths
                @"D:\celeste\Mods\MaggyHelper\Source",
                @"C:\Celeste\Mods\MaggyHelper\Source",
                // Environment variable
                Environment.GetEnvironmentVariable("MAGGYHELPER_SOURCE")
            };
            
            foreach (var path in possiblePaths)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Logger.Log(LogLevel.Debug, "HotReload", $"Found source path: {path}");
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "HotReload", $"Error finding source path: {ex.Message}");
        }
        
        return null;
    }
    
    private static void SetStatus(string message)
    {
        StatusMessage = message;
        OnStatusChanged?.Invoke(message);
        Logger.Log(LogLevel.Debug, "HotReload", $"Status: {message}");
    }
    
    private static void AddToHistory(string message)
    {
        string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _reloadHistory.Add(timestampedMessage);
        
        // Keep only last 50 entries
        while (_reloadHistory.Count > 50)
            _reloadHistory.RemoveAt(0);
    }
    
    /// <summary>
    /// Get diagnostic information about the hot reload system.
    /// </summary>
    public static string GetDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Hot Reload Diagnostics ===");
        sb.AppendLine($"Initialized: {_initialized}");
        sb.AppendLine($"Enabled: {_enabled}");
        sb.AppendLine($"Source Path: {_sourcePath ?? "Not set"}");
        sb.AppendLine($"Reload Count: {_reloadCount}");
        sb.AppendLine($"Last Reload: {(_lastReloadTime == DateTime.MinValue ? "Never" : _lastReloadTime.ToString("HH:mm:ss"))}");
        sb.AppendLine($"Active Detours: {_activeDetours.Count}");
        sb.AppendLine($"Loaded Assemblies: {_loadedAssemblies.Count}");
        sb.AppendLine($"Status: {StatusMessage}");
        sb.AppendLine();
        sb.AppendLine("Recent History:");
        foreach (var entry in _reloadHistory.TakeLast(10))
        {
            sb.AppendLine($"  {entry}");
        }
        return sb.ToString();
    }
    
    #endregion
}
