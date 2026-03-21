
using global::MaggyHelper.Extensions.Core;

namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Session data for MaggyHelper that persists during a single play session.
/// This data is reset when the player returns to the main menu or starts a new save file.
/// </summary>
public class MaggyHelperModuleSession : EverestModuleSession
{
    #region Kirby State
    
    /// <summary>
    /// Whether Kirby mode is currently active in this session.
    /// </summary>
    public bool IsKirbyModeActive { get; set; } = false;
    
    /// <summary>
    /// Current Kirby power state as string (for serialization).
    /// </summary>
    public string CurrentKirbyPower { get; set; } = "None";

    /// <summary>
    /// Currently active character for this session.
    /// </summary>
    public string ActiveCharacterId { get; set; } = PlayerCharacterIds.Madeline;
    
    /// <summary>
    /// Kirby's current health.
    /// </summary>
    public int KirbyHealth { get; set; } = 6;
    
    /// <summary>
    /// Kirby's current stamina.
    /// </summary>
    public float KirbyStamina { get; set; } = 100f;
    
    /// <summary>
    /// Whether Knight mode is currently active.
    /// </summary>
    public bool IsKnightModeActive { get; set; } = false;
    
    /// <summary>
    /// Time remaining for current power (if using timed powers).
    /// </summary>
    public float PowerTimeRemaining { get; set; } = 0f;
    
    #endregion

    #region Gameplay State
    
    /// <summary>
    /// Number of enemies defeated in this session.
    /// </summary>
    public int EnemiesDefeated { get; set; } = 0;
    
    /// <summary>
    /// Total damage dealt in this session.
    /// </summary>
    public int TotalDamageDealt { get; set; } = 0;
    
    /// <summary>
    /// Total damage received in this session.
    /// </summary>
    public int TotalDamageReceived { get; set; } = 0;
    
    /// <summary>
    /// Number of powers copied in this session.
    /// </summary>
    public int PowersCopied { get; set; } = 0;
    
    #endregion

    #region Cutscene State
    
    /// <summary>
    /// ID of the last completed cutscene.
    /// </summary>
    public string LastCompletedCutscene { get; set; } = "";
    
    /// <summary>
    /// Whether we're currently in a custom cutscene.
    /// </summary>
    public bool InCustomCutscene { get; set; } = false;
    
    /// <summary>
    /// Name of the current cutscene (if any).
    /// </summary>
    public string CurrentCutsceneName { get; set; } = "";
    
    #endregion

    #region NPC State
    
    /// <summary>
    /// Dictionary of NPC states (NPC ID -> state name).
    /// </summary>
    public Dictionary<string, string> NPCStates { get; set; } = new Dictionary<string, string>();
    
    /// <summary>
    /// List of NPCs that have been talked to this session.
    /// </summary>
    public List<string> TalkedToNPCs { get; set; } = new List<string>();
    
    #endregion

    #region Boss State
    
    /// <summary>
    /// Whether a boss fight is currently active.
    /// </summary>
    public bool IsBossFightActive { get; set; } = false;
    
    /// <summary>
    /// Alias for IsBossFightActive used by boss entities.
    /// </summary>
    public bool BossFightActive
    {
        get => IsBossFightActive;
        set => IsBossFightActive = value;
    }
    
    /// <summary>
    /// Number of bosses defeated this session.
    /// </summary>
    public int BossesDefeated { get; set; } = 0;
    
    /// <summary>
    /// Current copy ability (nullable for Kirby boss system).
    /// </summary>
    public global::MaggyHelper.Entities.Bosses.CopyAbilityType? CurrentCopyAbility { get; set; } = null;
    
    /// <summary>
    /// Current boss name (if in boss fight).
    /// </summary>
    public string CurrentBossName { get; set; } = "";
    
    /// <summary>
    /// Current boss phase.
    /// </summary>
    public int CurrentBossPhase { get; set; } = 0;
    
    /// <summary>
    /// Current boss health percentage.
    /// </summary>
    public float CurrentBossHealthPercent { get; set; } = 1f;
    
    #endregion

    #region Misc State
    
    /// <summary>
    /// Custom session flags set by triggers and entities.
    /// </summary>
    public Dictionary<string, bool> CustomFlags { get; set; } = new Dictionary<string, bool>();
    
    /// <summary>
    /// Custom session counters.
    /// </summary>
    public Dictionary<string, int> CustomCounters { get; set; } = new Dictionary<string, int>();
    
    /// <summary>
    /// Custom session strings.
    /// </summary>
    public Dictionary<string, string> CustomStrings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Lives remaining in the current chapter attempt. Game over music plays when this hits zero.
    /// Resets to <see cref="MaxLives"/> on a fresh (non-save) chapter entry.
    /// </summary>
    public int LivesRemaining { get; set; } = MaxLives;

    /// <summary>Starting lives for a chapter attempt.</summary>
    public const int MaxLives = 3;

    /// <summary>
    /// Whether the current chapter entry used a saved respawn state.
    /// </summary>
    public bool UsedSavedChapterRespawn { get; set; } = false;

    /// <summary>
    /// Whether a save point/checkpoint has been registered for this session.
    /// </summary>
    public bool HasRegisteredChapterSavePoint { get; set; } = false;

    /// <summary>
    /// Last save point checkpoint identifier for this session.
    /// </summary>
    public string LastCheckpointId { get; set; } = string.Empty;
    
    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Reset Kirby state to defaults.
    /// </summary>
    public void ResetKirbyState()
    {
        IsKirbyModeActive = false;
        CurrentKirbyPower = "None";
        ActiveCharacterId = PlayerCharacterIds.Madeline;
        KirbyHealth = MaggyHelperModule.Settings?.KirbyMaxHealth ?? 6;
        KirbyStamina = MaggyHelperModule.Settings?.KirbyMaxStaminaFloat ?? 100f;
        IsKnightModeActive = false;
        PowerTimeRemaining = 0f;
    }

    /// <summary>
    /// Get the active player character as a typed value.
    /// </summary>
    public PlayerCharacter GetActivePlayerCharacter()
    {
        return PlayerCharacter.FromId(ActiveCharacterId);
    }

    /// <summary>
    /// Set the active player character and synchronize Kirby mode state.
    /// </summary>
    public void SetActivePlayerCharacter(PlayerCharacter character)
    {
        ActiveCharacterId = character.Id;
        IsKirbyModeActive = character.IsKirby;
    }
    
    /// <summary>
    /// Get a custom flag value.
    /// </summary>
    public bool GetFlag(string flagName)
    {
        return CustomFlags.TryGetValue(flagName, out bool value) && value;
    }
    
    /// <summary>
    /// Set a custom flag value.
    /// </summary>
    public void SetFlag(string flagName, bool value)
    {
        CustomFlags[flagName] = value;
    }
    
    /// <summary>
    /// Get a custom counter value.
    /// </summary>
    public int GetCounter(string counterName)
    {
        return CustomCounters.TryGetValue(counterName, out int value) ? value : 0;
    }
    
    /// <summary>
    /// Set a custom counter value.
    /// </summary>
    public void SetCounter(string counterName, int value)
    {
        CustomCounters[counterName] = value;
    }
    
    /// <summary>
    /// Increment a custom counter.
    /// </summary>
    public int IncrementCounter(string counterName, int amount = 1)
    {
        int newValue = GetCounter(counterName) + amount;
        SetCounter(counterName, newValue);
        return newValue;
    }
    
    /// <summary>
    /// Get a custom string value.
    /// </summary>
    public string GetString(string stringName)
    {
        return CustomStrings.TryGetValue(stringName, out string value) ? value : "";
    }
    
    /// <summary>
    /// Set a custom string value.
    /// </summary>
    public void SetString(string stringName, string value)
    {
        CustomStrings[stringName] = value;
    }
    
    #endregion
}
