using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Popstarberry;

/// <summary>
/// Popstarberry - An enhanced map editor for Celeste based on Snowberry.
/// Integrates with MaggyHelper and provides ImGui-style UI and Loenn/Lua support.
/// </summary>
public static class PopstarberryModule
{
    #region Constants
    
    public const string ModName = "Popstarberry";
    public const string Version = "1.0.0";
    
    #endregion

    #region Properties
    
    /// <summary>
    /// Dummy instance property for compatibility with EverestModule patterns.
    /// Returns a helper object that exposes Initialize().
    /// </summary>
    public static PopstarberryModuleInstance Instance { get; } = new PopstarberryModuleInstance();
    
    /// <summary>
    /// Whether the editor module is currently loaded and active.
    /// </summary>
    public static bool IsLoaded { get; private set; }
    
    /// <summary>
    /// All loaded Popstarberry sub-modules (entity plugins, tools, etc.)
    /// </summary>
    public static PopstarModule[] Modules { get; private set; }
    
    /// <summary>
    /// The current editor settings.
    /// </summary>
    public static PopstarberrySettings Settings { get; private set; }
    
    #endregion

    #region Initialization
    
    /// <summary>
    /// Initialize the Popstarberry map editor module.
    /// Call this from MaggyHelperModule.Load()
    /// </summary>
    public static void Load()
    {
        if (IsLoaded) return;
        
        try
        {
            // Initialize settings
            Settings = new PopstarberrySettings();
            
            // Load core editor systems
            Editor.PopstarEditor.Initialize();
            
            // Load ImGui-style UI system
            ImGui.PopstarImGui.Initialize();
            
            // Load Lua/Loenn integration
            Loenn.LoennBridge.Initialize();
            
            // Load entity plugins
            LoadPlugins();
            
            // Load sub-modules
            LoadModules();
            
            // Register console commands
            RegisterCommands();
            
            IsLoaded = true;
            Logger.Log(LogLevel.Info, ModName, $"Popstarberry v{Version} loaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, ModName, $"Failed to load Popstarberry: {ex}");
        }
    }
    
    /// <summary>
    /// Unload the Popstarberry map editor module.
    /// Call this from MaggyHelperModule.Unload()
    /// </summary>
    public static void Unload()
    {
        if (!IsLoaded) return;
        
        try
        {
            // Unload in reverse order
            UnloadModules();
            
            Loenn.LoennBridge.Cleanup();
            ImGui.PopstarImGui.Cleanup();
            Editor.PopstarEditor.Cleanup();
            
            Modules = null;
            Settings = null;
            
            IsLoaded = false;
            Logger.Log(LogLevel.Info, ModName, "Popstarberry unloaded.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, ModName, $"Error during Popstarberry unload: {ex}");
        }
    }
    
    #endregion

    #region Plugin Loading
    
    private static void LoadPlugins()
    {
        // Scan for [PopstarPlugin] attributes in the assembly
        var assembly = typeof(PopstarberryModule).Assembly;
        var pluginTypes = new List<Type>();
        
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<PopstarPluginAttribute>() != null)
            {
                pluginTypes.Add(type);
            }
        }
        
        // Initialize entity plugins
        Entities.PluginRegistry.LoadPlugins(pluginTypes);
        
        // Initialize styleground plugins
        Stylegrounds.StylegroundRegistry.LoadPlugins(pluginTypes);
        
        // Initialize tool plugins  
        Tools.ToolRegistry.LoadPlugins(pluginTypes);
        
        Logger.Log(LogLevel.Info, ModName, $"Loaded {pluginTypes.Count} plugins.");
    }
    
    private static void LoadModules()
    {
        var modules = new List<PopstarModule>();
        
        // Add built-in modules
        modules.Add(new Tools.SelectionTool());
        modules.Add(new Tools.PlacementTool());
        modules.Add(new Tools.TileBrushTool());
        modules.Add(new Tools.StylegroundsTool());
        
        Modules = modules.ToArray();
        
        foreach (var module in Modules)
        {
            try
            {
                module.Load();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ModName, $"Failed to load module {module.Name}: {ex}");
            }
        }
    }
    
    private static void UnloadModules()
    {
        if (Modules == null) return;
        
        foreach (var module in Modules)
        {
            try
            {
                module.Unload();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ModName, $"Error unloading module {module.Name}: {ex}");
            }
        }
    }
    
    #endregion

    #region Commands
    
    private static void RegisterCommands()
    {
        // Commands are registered via Everest's console command system
        // Note: In Celeste/Everest, console commands are typically registered differently
        // For now, we'll use Everest.Commands if available, or simply log that hotkeys are available
        Log(LogLevel.Info, "Use F10/F11/F12 in MapEditor to access Popstarberry features");
    }
    
    /// <summary>
    /// Command handler for "popstar" command - opens editor with current map
    /// </summary>
    [Command("popstar", "Opens the Popstarberry map editor")]
    public static void PopstarCommand()
    {
        // Open the editor with current map
        if (Engine.Scene is Level level)
        {
            Editor.PopstarEditor.Open(level.Session.MapData);
        }
        else
        {
            Editor.PopstarEditor.Open(null);
        }
    }
    
    /// <summary>
    /// Command handler for "popstar_new" command - opens editor with new map  
    /// </summary>
    [Command("popstar_new", "Opens the Popstarberry editor with a new map")]
    public static void PopstarNewCommand()
    {
        Editor.PopstarEditor.OpenNew();
    }
    
    #endregion

    #region Logging Utility
    
    public static void Log(LogLevel level, string message)
    {
        Logger.Log(level, ModName, message);
    }
    
    #endregion
}

/// <summary>
/// Settings for the Popstarberry map editor.
/// </summary>
public class PopstarberrySettings
{
    public bool EnableSmoothCamera { get; set; } = true;
    public float CameraLerpSpeed { get; set; } = 8f;
    public bool ShowEntityOverlay { get; set; } = true;
    public bool ShowRoomConnections { get; set; } = false;
    public bool ShowGrid { get; set; } = true;
    public int GridSize { get; set; } = 8;
    public bool DarkMode { get; set; } = true;
    public Color AccentColor { get; set; } = new Color(255, 105, 180); // Pink/star themed
    public bool EnableLuaPlugins { get; set; } = true;
    public bool EnableAutoSave { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 300;
    public List<string> RecentMaps { get; set; } = new();
    public Dictionary<string, string> HotKeys { get; set; } = new()
    {
        { "Save", "Ctrl+S" },
        { "Undo", "Ctrl+Z" },
        { "Redo", "Ctrl+Y" },
        { "SelectAll", "Ctrl+A" },
        { "Delete", "Delete" },
        { "Copy", "Ctrl+C" },
        { "Paste", "Ctrl+V" },
        { "Cut", "Ctrl+X" }
    };
}

/// <summary>
/// Base class for Popstarberry sub-modules.
/// </summary>
public abstract class PopstarModule
{
    public abstract string Name { get; }
    public abstract void Load();
    public abstract void Unload();
}

/// <summary>
/// Helper class that wraps PopstarberryModule static methods for compatibility.
/// </summary>
public class PopstarberryModuleInstance
{
    public void Initialize()
    {
        // Called from MaggyHelperModule.Initialize() for late initialization tasks
        // The main Load() was already called from MaggyHelperModule.Load()
        // This allows for tasks that need game content to be loaded first
        PopstarberryModule.Log(LogLevel.Info, "Popstarberry late initialization complete.");
    }
}

/// <summary>
/// Attribute to mark a class as a Popstarberry plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class PopstarPluginAttribute : Attribute
{
    public string Name { get; }
    public string Category { get; }
    
    public PopstarPluginAttribute(string name, string category = "General")
    {
        Name = name;
        Category = category;
    }
}

/// <summary>
/// Attribute to mark a property as an editable option in the editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class PopstarOptionAttribute : Attribute
{
    public string Name { get; }
    public string Tooltip { get; }
    public object DefaultValue { get; }
    
    public PopstarOptionAttribute(string name = null, string tooltip = null, object defaultValue = null)
    {
        Name = name;
        Tooltip = tooltip;
        DefaultValue = defaultValue;
    }
}
