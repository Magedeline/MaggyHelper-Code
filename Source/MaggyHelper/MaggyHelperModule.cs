using System.Reflection;
using System.IO;
using MaggyHelper.Entities;
using MaggyHelper.HotReload;
using MaggyHelper.Popstarberry;
using MaggyHelper;
using MaggyHelper.Effects.ShaderEffects;
using MaggyHelper.Extensions.Core;
using MaggyHelper; // Add this using directive
using MonoMod.RuntimeDetour;
using Monocle;
using MaggyHelper;

namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Main module class for MaggyHelper - a comprehensive helper mod for Celeste.
/// Handles initialization, loading, unloading, and lifecycle management.
/// </summary>
public class MaggyHelperModule : EverestModule
{
    private static string GetBuildFingerprint()
    {
        Assembly assembly = typeof(MaggyHelperModule).Assembly;

        string mvidShort;
        try
        {
            mvidShort = assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];
        }
        catch
        {
            mvidShort = "na";
        }

        string version = assembly.GetName().Version?.ToString() ?? "na";
        string? location = null;

        try
        {
            location = assembly.Location;
        }
        catch
        {
            location = null;
        }

        if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
        {
            string timestampUtc = File.GetLastWriteTimeUtc(location).ToString("yyyy-MM-dd HH:mm:ss");
            return $"{timestampUtc}Z v:{version} mvid:{mvidShort}";
        }

        // Some load contexts expose no physical assembly path. Still return a stable identifier.
        return $"v:{version} mvid:{mvidShort} path:na";
    }

    [Command("maggy_build", "Prints the active MaggyHelper build fingerprint. Usage: maggy_build")]
    private static void CmdMaggyBuild()
    {
        string fingerprint = GetBuildFingerprint();
        string message = $"[MaggyHelper] Active build: {fingerprint}";
        Engine.Commands?.Log(message);
        Logger.Log(LogLevel.Info, "MaggyHelper", message);
    }

    #region Static Instance
    
    /// <summary>
    /// Singleton instance of the module.
    /// </summary>
    public static MaggyHelperModule Instance { get; private set; } = null!;
    
    #endregion

    #region Module Properties
    
    /// <summary>
    /// Module settings accessible from Everest's mod options menu.
    /// </summary>
    public static MaggyHelperModuleSettings Settings => (MaggyHelperModuleSettings)Instance?._Settings;
    
    /// <summary>
    /// Session data that persists during a single play session.
    /// </summary>
    public static MaggyHelperModuleSession Session => (MaggyHelperModuleSession)Instance?._Session;
    
    /// <summary>
    /// Save data that persists across game sessions.
    /// </summary>
    public static MaggyHelperModuleSaveData SaveData => (MaggyHelperModuleSaveData)Instance?._SaveData;

    /// <summary>
    /// Resets all mod save-data for a clean new-game start.
    /// Exposed as a static method so any Source/ file can call this without
    /// manually casting the Everest-generated save-data type.
    /// </summary>
    public static void ResetModSaveData() => SaveData?.ResetForNewGame();
    
    /// <summary>
    /// The type of settings class for this module.
    /// </summary>
    public override Type SettingsType => typeof(MaggyHelperModuleSettings);
    
    /// <summary>
    /// The type of session data class for this module.
    /// </summary>
    public override Type SessionType => typeof(MaggyHelperModuleSession);
    
    /// <summary>
    /// The type of save data class for this module.
    /// </summary>
    public override Type SaveDataType => typeof(MaggyHelperModuleSaveData);
    
    #endregion

    #region State Tracking
    
    private bool _initialized = false;
    private bool _hooksLoaded = false;
    private Hook _levelUpdateHook;
    
    /// <summary>
    /// Flag to trigger Part 1 credits sequence.
    /// </summary>
    public static bool LaunchPart1Credits { get; set; } = false;
    
    /// <summary>
    /// Flag to trigger Part 2 credits sequence.
    /// </summary>
    public static bool LaunchPart2Credits { get; set; } = false;
    
    /// <summary>
    /// Sprite bank for this module containing custom sprite definitions.
    /// </summary>
    public static SpriteBank SpriteBank { get; private set; }
    
    /// <summary>
    /// Particle type for star explosion effects.
    /// </summary>
    public static ParticleType P_StarExplosion { get; private set; }
    
    #endregion

    #region Constructor
    
    public MaggyHelperModule()
    {
        Instance = this;
    }
    
    #endregion

    #region Lifecycle Methods
    
    /// <summary>
    /// Called when the module is first loaded.
    /// Use this for early initialization that doesn't depend on game content.
    /// </summary>
    public override void Load()
    {
        Logger.Log(LogLevel.Info, "MaggyHelper", $"Loading MaggyHelper module... build={GetBuildFingerprint()}");
        
        try
        {
            // Register hooks with the HookManager
            MaggyHelperHooks.Load();
            _hooksLoaded = true;
            
            // Initialize Player Extension Core (Aqua-style character system)
            PlayerExtensionCore.Instance.Hook();
            
            // Initialize Popstarberry map editor integration
            PopstarberryIntegration.Initialize();
            
            // Load AreaModeExtender (extends vanilla 3-mode to 5-mode: A/B/C/D/DX)
            AreaModeExtender.Load();
            
            // Load HeartGemManager (proper heart gem collection for all 5 modes)
            HeartGemManager.Load();
            
            // Load IntroRemixHooks (VHS-style B/C-Side intro cutscenes)
            IntroRemixHooks.Load();
            
            // Load OverworldMusicManager (replaces vanilla music with custom events)
            OverworldMusicManager.Load();
            
            // Load MountainOverworldManager (3D mountain camera + state per chapter)
            MountainOverworldManager.Load();

            // Load hardcoded late-game chapter progression rules (restart-gated unlocks)
            ChapterProgressionManager.Load();

            // Hook audio init and keep mod bank ingestion available
            GlobalModAudioLoader.Load();
            
            // Initialize Hot Reload system (development feature)
            InitializeHotReload();

            // RuntimeDetour hook for hot reload input handling (side-by-side compatible)
            InstallHotReloadHook();
            
            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper module loaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error during Load: {ex}");
            throw;
        }
    }
    
    /// <summary>
    /// Called after all mods are loaded and game content is available.
    /// Use this for initialization that depends on game content or other mods.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        
        if (_initialized)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", "Module already initialized, skipping...");
            return;
        }
        
        Logger.Log(LogLevel.Info, "MaggyHelper", "Initializing MaggyHelper module...");
        
        try
        {
            // Phase 1: Initialize core systems
            InitializeCoreIntegrations();
            
            // Phase 2: Initialize metadata registries
            InitializeMetadataRegistries();

            // Phase 2.5: Index audio assets and ingest newly discovered banks
            GlobalModAudioLoader.Initialize();
            
            // Phase 3: Initialize area map data (chapter definitions for vanilla-like integration)
            AreaMapData.Initialize();

            // Migrate legacy save data paths (Maggy/*Side -> Maggy/Main)
            MaggySaveDataMigration.Run();
            
            // Phase 4: Initialize effect manager for shaders
            EffectManager.Initialize();
            
            // Phase 5: Initialize particle systems
            InitializeParticleSystems();
            
            // Phase 6: Initialize custom systems
            InitializeCustomSystems();
            
            _initialized = true;
            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper module initialized successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error during Initialize: {ex}");
            throw;
        }
    }
    
    /// <summary>
    /// Called when the module is unloaded (game closing or mod being disabled).
    /// Clean up all resources and unregister hooks.
    /// </summary>
    public override void Unload()
    {
        Logger.Log(LogLevel.Info, "MaggyHelper", "Unloading MaggyHelper module...");
        
        try
        {
            // Unregister hooks
            if (_hooksLoaded)
            {
                MaggyHelperHooks.Unload();
                _hooksLoaded = false;
            }
            
            // Unhook Player Extension Core
            PlayerExtensionCore.Instance.Unhook();
            
            // Shutdown Hot Reload system
            RemoveHotReloadHook();
            ShutdownHotReload();
            
            // Unload Popstarberry map editor integration
            PopstarberryIntegration.Unload();
            
            // Unload AreaModeExtender (vanilla mode extension)
            AreaModeExtender.Unload();
            
            // Unload HeartGemManager (heart gem collection hooks)
            HeartGemManager.Unload();
            
            // Unload IntroRemixHooks (VHS intro cutscene hooks)
            IntroRemixHooks.Unload();
            
            // Unload OverworldMusicManager
            OverworldMusicManager.Unload();
            
            // Unload MountainOverworldManager
            MountainOverworldManager.Unload();

            // Unload hardcoded late-game chapter progression rules
            ChapterProgressionManager.Unload();
            
            // Unload effect manager
            EffectManager.UnloadAll();
            
            // Clean up custom systems
            CleanupCustomSystems();

            // Clean up global audio registry cache
            GlobalModAudioLoader.Unload();
            
            _initialized = false;
            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper module unloaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error during Unload: {ex}");
        }
    }
    
    #endregion

    #region Initialization Helpers
    
    /// <summary>
    /// Initialize integrations with other mods and core systems.
    /// </summary>
    private void InitializeCoreIntegrations()
    {
        Logger.Log(LogLevel.Debug, "MaggyHelper", "Initializing core integrations...");
        
        // Add any FrostHelper, CommunalHelper, or other mod integrations here
    }
    
    /// <summary>
    /// Initialize metadata registries for areas, cutscenes, etc.
    /// </summary>
    private void InitializeMetadataRegistries()
    {
        Logger.Log(LogLevel.Debug, "MaggyHelper", "Initializing metadata registries...");
        
        try
        {
            MetadataRegistries.Initialize();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"MetadataRegistries initialization skipped or failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Initialize particle systems for visual effects.
    /// </summary>
    private void InitializeParticleSystems()
    {
        Logger.Log(LogLevel.Debug, "MaggyHelper", "Initializing particle systems...");
        
        // Initialize any custom particle types here
    }
    
    /// <summary>
    /// Initialize custom gameplay systems.
    /// </summary>
    private void InitializeCustomSystems()
    {
        Logger.Log(LogLevel.Debug, "MaggyHelper", "Initializing custom systems...");
        
        // Initialize Popstarberry module (loads plugins and registers commands)
        PopstarberryModule.Instance?.Initialize();
        
        // Initialize any other custom gameplay systems here
    }
    
    /// <summary>
    /// Clean up custom systems during unload.
    /// </summary>
    private void CleanupCustomSystems()
    {
        Logger.Log(LogLevel.Debug, "MaggyHelper", "Cleaning up custom systems...");
        
        // Clean up any custom systems here
    }
    
    #endregion

    #region Session Callbacks
    
    /// <summary>
    /// Called when a new game session begins.
    /// </summary>
    public override void PrepareMapDataProcessors(MapDataFixup context)
    {
        base.PrepareMapDataProcessors(context);
        
        // Add any map data processors here
    }
    
    #endregion

    #region Utility Methods
    
    /// <summary>
    /// Get the mod's content path for loading resources.
    /// </summary>
    public static string GetContentPath(string relativePath)
    {
        return $"MaggyHelper/{relativePath}";
    }
    
    /// <summary>
    /// Check if we're currently in a MaggyHelper map.
    /// </summary>
    public static bool IsInMaggyHelperMap()
    {
        var level = Engine.Scene as Level;
        if (level?.Session == null)
            return false;
            
        string sid = level.Session.Area.GetSID();
        return sid != null && (
            sid.StartsWith("Maggy/", StringComparison.OrdinalIgnoreCase) ||
            sid.StartsWith("DesoloZantas/", StringComparison.OrdinalIgnoreCase)
        );
    }
    
    /// <summary>
    /// Log a debug message if debug mode is enabled in settings.
    /// </summary>
    public static void LogDebug(string message)
    {
        if (Settings?.DebugMode == true)
        {
            Logger.Log(LogLevel.Debug, "MaggyHelper", message);
        }
    }
    
    #endregion
    
    #region Hot Reload System
    
    /// <summary>
    /// Initialize the Hot Code Reloading system.
    /// </summary>
    private void InitializeHotReload()
    {
        try
        {
            // Initialize settings handler
            HotReloadSettingsHandler.Initialize();
            
            Logger.Log(LogLevel.Info, "MaggyHelper", "Hot reload system initialized");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload initialization skipped: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Shutdown the Hot Code Reloading system.
    /// </summary>
    private void ShutdownHotReload()
    {
        try
        {
            HotReloadManager.Shutdown();
            Logger.Log(LogLevel.Info, "MaggyHelper", "Hot reload system shutdown");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload shutdown error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Install RuntimeDetour hook to handle hot reload input during Level.Update.
    /// </summary>
    private void InstallHotReloadHook()
    {
        if (_levelUpdateHook is not null)
        {
            return;
        }

        MethodInfo targetMethod = typeof(Level).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo hookMethod = typeof(MaggyHelperModule).GetMethod(nameof(Level_Update_HotReload_Detour), BindingFlags.Static | BindingFlags.NonPublic);

        if (targetMethod is null || hookMethod is null)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", "Failed to install hot reload hook: Level.Update or detour method not found");
            return;
        }

        _levelUpdateHook = new Hook(targetMethod, hookMethod);
    }

    /// <summary>
    /// Remove RuntimeDetour hook for Level.Update.
    /// </summary>
    private void RemoveHotReloadHook()
    {
        _levelUpdateHook?.Dispose();
        _levelUpdateHook = null;
    }

    /// <summary>
    /// RuntimeDetour callback to keep hot reload input handling side-by-side with other hooks.
    /// </summary>
    private static void Level_Update_HotReload_Detour(Action<Level> orig, Level self)
    {
        orig(self);
        
        // Handle hot reload input
        try
        {
            HotReloadSettingsHandler.HandleInput();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload input error: {ex.Message}");
        }
    }
    
    #endregion
}
