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
public class MaggyHelperModule
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
        string location = null;

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
    /// Singleton instance — delegates to the root <see cref="global::MaggyHelper.MaggyHelperModule"/>
    /// which is the single <c>EverestModule</c> registered with Everest.
    /// </summary>
    public static global::MaggyHelper.MaggyHelperModule Instance
        => global::MaggyHelper.MaggyHelperModule.Instance;
    
    #endregion

    #region Module Properties
    
    /// <summary>Module settings (delegates to root module).</summary>
    public static MaggyHelperModuleSettings Settings
        => global::MaggyHelper.MaggyHelperModule.Settings;
    
    /// <summary>Session data for the current play session (delegates to root module).</summary>
    public static MaggyHelperModuleSession Session
        => global::MaggyHelper.MaggyHelperModule.Session;
    
    /// <summary>Persistent save data (delegates to root module).</summary>
    public static MaggyHelperModuleSaveData SaveData
        => global::MaggyHelper.MaggyHelperModule.SaveData;

    /// <summary>Resets all mod save-data (delegates to root module).</summary>
    public static void ResetModSaveData()
        => global::MaggyHelper.MaggyHelperModule.ResetModSaveData();

    #endregion

    #region State Tracking
    
    /// <summary>Flag to trigger Part 1 credits sequence (delegates to root module).</summary>
    public static bool LaunchPart1Credits
    {
        get => global::MaggyHelper.MaggyHelperModule.LaunchPart1Credits;
        set => global::MaggyHelper.MaggyHelperModule.LaunchPart1Credits = value;
    }
    
    /// <summary>Flag to trigger Part 2 credits sequence (delegates to root module).</summary>
    public static bool LaunchPart2Credits
    {
        get => global::MaggyHelper.MaggyHelperModule.LaunchPart2Credits;
        set => global::MaggyHelper.MaggyHelperModule.LaunchPart2Credits = value;
    }
    
    /// <summary>Sprite bank for custom sprite definitions (delegates to root module).</summary>
    public static SpriteBank SpriteBank
        => global::MaggyHelper.MaggyHelperModule.SpriteBank;
    
    /// <summary>Particle type for star explosion VFX (delegates to root module).</summary>
    public static ParticleType P_StarExplosion
        => global::MaggyHelper.MaggyHelperModule.P_StarExplosion;
    
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
    /// Delegates to the root module implementation.
    /// </summary>
    public static bool IsInMaggyHelperMap()
    {
        return global::MaggyHelper.MaggyHelperModule.IsInMaggyHelperMap();
    }

    /// <summary>
    /// Log a debug message if debug mode is enabled in settings.
    /// Delegates to the root module implementation.
    /// </summary>
    public static void LogDebug(string message)
    {
        global::MaggyHelper.MaggyHelperModule.LogDebug(message);
    }

    #endregion
}
