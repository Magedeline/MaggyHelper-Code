
namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Compatibility alias for IngesteModule.
/// This allows existing code that references IngesteModule to continue working
/// while the main module class is now MaggyHelperModule.
/// </summary>
public static class IngesteModule
{
    /// <summary>
    /// Get the module settings (alias for MaggyHelperModule.Settings).
    /// </summary>
    public static MaggyHelperModuleSettings Settings => MaggyHelperModule.Settings;
    
    /// <summary>
    /// Get the module session (alias for MaggyHelperModule.Session).
    /// </summary>
    public static MaggyHelperModuleSession Session => MaggyHelperModule.Session;
    
    /// <summary>
    /// Get the module save data (alias for MaggyHelperModule.SaveData).
    /// </summary>
    public static MaggyHelperModuleSaveData SaveData => MaggyHelperModule.SaveData;
    
    /// <summary>
    /// Get the module instance (alias for MaggyHelperModule.Instance).
    /// </summary>
    public static global::MaggyHelper.MaggyHelperModule Instance => MaggyHelperModule.Instance;
    
    /// <summary>
    /// Get the sprite bank for this module.
    /// </summary>
    public static SpriteBank SpriteBank => MaggyHelperModule.SpriteBank;
    
    /// <summary>
    /// Particle type for star explosion effects.
    /// </summary>
    public static ParticleType P_StarExplosion => MaggyHelperModule.P_StarExplosion;
    
    /// <summary>
    /// Flag to trigger Part 1 credits (alias for MaggyHelperModule.LaunchPart1Credits).
    /// </summary>
    public static bool LaunchPart1Credits
    {
        get => MaggyHelperModule.LaunchPart1Credits;
        set => MaggyHelperModule.LaunchPart1Credits = value;
    }
    
    /// <summary>
    /// Flag to trigger Part 2 credits (alias for MaggyHelperModule.LaunchPart2Credits).
    /// </summary>
    public static bool LaunchPart2Credits
    {
        get => MaggyHelperModule.LaunchPart2Credits;
        set => MaggyHelperModule.LaunchPart2Credits = value;
    }
}

/// <summary>
/// Type alias for backward compatibility with IngesteModuleSettings.
/// </summary>
public class IngesteModuleSettings : MaggyHelperModuleSettings
{
}

/// <summary>
/// Type alias for backward compatibility with IngesteModuleSession.
/// </summary>
public class IngesteModuleSession : MaggyHelperModuleSession
{
}

/// <summary>
/// Type alias for backward compatibility with IngesteModuleSaveData.
/// </summary>
public class IngesteModuleSaveData : MaggyHelperModuleSaveData
{
}
