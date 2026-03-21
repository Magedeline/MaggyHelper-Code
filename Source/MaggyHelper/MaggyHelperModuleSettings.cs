using Microsoft.Xna.Framework.Input;

namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Module settings for MaggyHelper that appear in Everest's mod options menu.
/// Settings are automatically saved and loaded by Everest.
/// </summary>
[SettingName("MAGGYHELPER_SETTINGS")]
public partial class MaggyHelperModuleSettings : EverestModuleSettings
{
    #region General Settings
    
    /// <summary>
    /// Enable debug mode for additional logging and debug features.
    /// </summary>
    [SettingName("MAGGYHELPER_DEBUG_MODE")]
    [SettingSubText("MAGGYHELPER_DEBUG_MODE_DESC")]
    public bool DebugMode { get; set; } = false;
    
    /// <summary>
    /// If true, skip the mod intro and go straight to Celeste.
    /// </summary>
    public bool SkipModIntro { get; set; } = false;
    
    /// <summary>
    /// Enable visual effects (particles, shaders, etc.)
    /// </summary>
    [SettingName("MAGGYHELPER_VISUAL_EFFECTS")]
    [SettingSubText("MAGGYHELPER_VISUAL_EFFECTS_DESC")]
    public bool VisualEffectsEnabled { get; set; } = true;
    
    #endregion

    #region Madeline Combat Settings

    /// <summary>
    /// Enable additive Madeline combat runtime (HUD + melee + side charge).
    /// </summary>
    [SettingName("MAGGYHELPER_MAD_COMBAT_ENABLED")]
    public bool MadelineCombatEnabled { get; set; } = false;

    /// <summary>
    /// Show Madeline health / stamina HUD bars.
    /// </summary>
    [SettingName("MAGGYHELPER_MAD_COMBAT_HUD")]
    public bool MadelineCombatShowHud { get; set; } = true;

    /// <summary>
    /// Max custom health for Madeline combat mode.
    /// </summary>
    [SettingName("MAGGYHELPER_MAD_COMBAT_MAX_HEALTH")]
    [SettingRange(1, 12)]
    public int MadelineCombatMaxHealth { get; set; } = 5;

    #endregion

    #region Kirby Player Settings
    
    /// <summary>
    /// Header for Kirby settings section.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_HEADER")]
    [SettingSubHeader("MAGGYHELPER_KIRBY_HEADER")]
    public string KirbyHeader { get; set; } = "";
    
    /// <summary>
    /// Enable Kirby player mode globally.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_ENABLED")]
    [SettingSubText("MAGGYHELPER_KIRBY_ENABLED_DESC")]
    public bool KirbyPlayerEnabled { get; set; } = true;
    
    /// <summary>
    /// Whether Kirby mode is currently active.
    /// </summary>
    public bool KirbyPlayerMode { get; set; } = false;
    
    /// <summary>
    /// Maximum health for Kirby.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_MAX_HEALTH")]
    [SettingRange(1, 10)]
    public int KirbyMaxHealth { get; set; } = 6;
    
    /// <summary>
    /// Maximum stamina for floating (as integer for slider).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_MAX_STAMINA")]
    [SettingRange(50, 200)]
    public int KirbyMaxStamina { get; set; } = 100;
    
    /// <summary>
    /// Get stamina as float value.
    /// </summary>
    public float KirbyMaxStaminaFloat => KirbyMaxStamina;
    
    /// <summary>
    /// Maximum float jumps before landing required.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_MAX_FLOAT_JUMPS")]
    [SettingRange(1, 10)]
    public int KirbyMaxFloatJumps { get; set; } = 5;
    
    /// <summary>
    /// Inhale range in pixels (as integer for slider).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_INHALE_RANGE")]
    [SettingRange(32, 128)]
    public int KirbyInhaleRange { get; set; } = 64;
    
    /// <summary>
    /// Get inhale range as float value.
    /// </summary>
    public float InhaleRangeFloat => KirbyInhaleRange;
    
    /// <summary>
    /// Hover fall speed (as integer for slider).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_HOVER_FALL_SPEED")]
    [SettingRange(20, 100)]
    public int KirbyHoverFallSpeed { get; set; } = 60;
    
    /// <summary>
    /// Get hover fall speed as float value.
    /// </summary>
    public float HoverFallSpeedFloat => KirbyHoverFallSpeed;
    
    /// <summary>
    /// Dash speed multiplier (as percentage).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_DASH_SPEED")]
    [SettingRange(50, 200)]
    public int KirbyDashSpeed { get; set; } = 100;
    
    /// <summary>
    /// Get dash speed as multiplier.
    /// </summary>
    public float KirbyDashSpeedMultiplier => KirbyDashSpeed / 100f;
    
    /// <summary>
    /// Slide speed multiplier (as percentage).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_SLIDE_SPEED")]
    [SettingRange(50, 200)]
    public int KirbySlideSpeed { get; set; } = 100;
    
    /// <summary>
    /// Get slide speed as multiplier.
    /// </summary>
    public float KirbySlideSpeedMultiplier => KirbySlideSpeed / 100f;
    
    /// <summary>
    /// Whether inhale requires holding the button.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_INHALE_HOLD")]
    public bool InhaleHoldMode { get; set; } = false;
    
    /// <summary>
    /// Whether hover requires holding the button.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_HOVER_HOLD")]
    public bool HoverHoldMode { get; set; } = false;

    /// <summary>
    /// Enable Kirby hovering/floating gameplay.
    /// Default is off to keep movement precision-focused.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_HOVER_ENABLED")]
    public bool KirbyHoverEnabled { get; set; } = false;

    /// <summary>
    /// Use vanilla Player rendering with Kirby sprite bank applied.
    /// This keeps Player.cs behavior 1:1 while changing visuals.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_USE_PLAYER_RENDER")]
    public bool KirbyUseVanillaPlayerRender { get; set; } = true;

    /// <summary>
    /// Enable the precision combat controller (punch/parry state machine).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_PRECISION_COMBAT")]
    public bool KirbyPrecisionCombatEnabled { get; set; } = true;

    /// <summary>
    /// Default combat mode on chapter/session start.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_COMBAT_MODE_DEFAULT")]
    public bool KirbyCombatModeDefault { get; set; } = false;
    
    #endregion

    #region Kirby Power System Settings
    
    /// <summary>
    /// Enable copy ability system.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_POWER_COPY")]
    public bool PowerCopyEnabled { get; set; } = true;
    
    /// <summary>
    /// Duration of copied powers in seconds (0 = infinite).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_POWER_DURATION")]
    [SettingRange(0, 300)]
    public int PowerDurationSeconds { get; set; } = 0;
    
    /// <summary>
    /// Whether powers persist through death.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_POWER_PERSIST")]
    public bool PowerPersistOnDeath { get; set; } = false;
    
    /// <summary>
    /// Allow dropping power with dedicated button.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_POWER_DROP")]
    public bool PowerDropEnabled { get; set; } = true;
    
    /// <summary>
    /// Show power indicator HUD.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_POWER_HUD")]
    public bool ShowPowerHud { get; set; } = true;
    
    #endregion

    #region Kirby Knight Mode Settings
    
    /// <summary>
    /// Enable Knight mode transformation.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_KNIGHT_ENABLED")]
    public bool KnightModeEnabled { get; set; } = true;
    
    /// <summary>
    /// Knight damage multiplier (as percentage).
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_KNIGHT_DAMAGE")]
    [SettingRange(100, 400)]
    public int KnightDamage { get; set; } = 200;
    
    /// <summary>
    /// Get knight damage as multiplier.
    /// </summary>
    public float KnightDamageMultiplier => KnightDamage / 100f;
    
    /// <summary>
    /// Allow forced knight transformation at low health.
    /// </summary>
    [SettingName("MAGGYHELPER_KIRBY_KNIGHT_LOW_HEALTH")]
    public bool KnightLowHealthTransform { get; set; } = true;
    
    #endregion

    #region Key Bindings
    
    /// <summary>
    /// Header for key bindings section.
    /// </summary>
    [SettingName("MAGGYHELPER_BINDINGS_HEADER")]
    [SettingSubHeader("MAGGYHELPER_BINDINGS_HEADER")]
    public string BindingsHeader { get; set; } = "";
    
    /// <summary>
    /// Kirby inhale ability binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_INHALE")]
    [DefaultButtonBinding(Buttons.RightShoulder, Keys.Z)]
    public ButtonBinding KirbyInhaleBind { get; set; }
    
    /// <summary>
    /// Kirby attack ability binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_ATTACK")]
    [DefaultButtonBinding(Buttons.X, Keys.X)]
    public ButtonBinding KirbyAttackBind { get; set; }

    /// <summary>
    /// Precision combat punch binding.
    /// Falls back to the general Attack bind when used by runtime logic.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_PUNCH")]
    [DefaultButtonBinding(Buttons.X, Keys.X)]
    public ButtonBinding KirbyPunchBind { get; set; }
    
    /// <summary>
    /// Kirby hover ability binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_HOVER")]
    [DefaultButtonBinding(Buttons.A, Keys.C)]
    public ButtonBinding KirbyHoverBind { get; set; }
    
    /// <summary>
    /// Kirby spit ability binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_SPIT")]
    [DefaultButtonBinding(Buttons.B, Keys.V)]
    public ButtonBinding KirbySpitBind { get; set; }

    /// <summary>
    /// Precision combat parry binding.
    /// Falls back to Spit bind when used by runtime logic.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_PARRY")]
    [DefaultButtonBinding(Buttons.B, Keys.V)]
    public ButtonBinding KirbyParryBind { get; set; }

    /// <summary>
    /// Toggle precision combat mode on/off.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_COMBAT_TOGGLE")]
    [DefaultButtonBinding(Buttons.LeftStick, Keys.F)]
    public ButtonBinding KirbyCombatToggleBind { get; set; }
    
    /// <summary>
    /// Kirby cycle power binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_CYCLE_POWER")]
    [DefaultButtonBinding(Buttons.LeftShoulder, Keys.Q)]
    public ButtonBinding KirbyCyclePowerBind { get; set; }
    
    /// <summary>
    /// Kirby drop power binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_DROP_POWER")]
    [DefaultButtonBinding(Buttons.Y, Keys.E)]
    public ButtonBinding KirbyDropPowerBind { get; set; }
    
    /// <summary>
    /// Kirby slide ability binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_SLIDE")]
    [DefaultButtonBinding(Buttons.LeftTrigger, Keys.LeftShift)]
    public ButtonBinding KirbySlideBind { get; set; }
    
    /// <summary>
    /// Kirby knight mode binding.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_KNIGHT")]
    [DefaultButtonBinding(Buttons.RightTrigger, Keys.R)]
    public ButtonBinding KirbyKnightBind { get; set; }

    /// <summary>
    /// Madeline melee combat input.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_MAD_MELEE")]
    [DefaultButtonBinding(Buttons.X, Keys.G)]
    public ButtonBinding MadelineMeleeBind { get; set; }

    /// <summary>
    /// Madeline side charge attack hold input.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_MAD_CHARGE")]
    [DefaultButtonBinding(Buttons.LeftTrigger, Keys.LeftShift)]
    public ButtonBinding MadelineChargeBind { get; set; }

    /// <summary>
    /// Cycle Madeline's equipped weapon.
    /// </summary>
    [SettingName("MAGGYHELPER_BIND_MAD_WEAPON_CYCLE")]
    [DefaultButtonBinding(Buttons.RightShoulder, Keys.Tab)]
    public ButtonBinding MadelineWeaponCycleBind { get; set; }
    
    #endregion

    #region Boss Settings
    
    /// <summary>
    /// Difficulty multiplier for boss health.
    /// </summary>
    [SettingName("MAGGYHELPER_BOSS_DIFFICULTY")]
    [SettingRange(1, 5)]
    public int BossDifficultyMultiplier { get; set; } = 1;
    
    /// <summary>
    /// Enable boss-specific music tracks.
    /// </summary>
    [SettingName("MAGGYHELPER_BOSS_MUSIC")]
    public bool EnableBossMusic { get; set; } = true;
    
    /// <summary>
    /// Enable Kirby player in boss encounters (e.g. Meta Knight offers sword).
    /// </summary>
    [SettingName("MAGGYHELPER_ENABLE_KIRBY_PLAYER")]
    public bool EnableKirbyPlayer { get; set; } = true;
    
    #endregion

    #region Accessibility Settings
    
    /// <summary>
    /// Header for accessibility section.
    /// </summary>
    [SettingName("MAGGYHELPER_ACCESSIBILITY_HEADER")]
    [SettingSubHeader("MAGGYHELPER_ACCESSIBILITY_HEADER")]
    public string AccessibilityHeader { get; set; } = "";
    
    /// <summary>
    /// Enable screen shake effects.
    /// </summary>
    [SettingName("MAGGYHELPER_SCREEN_SHAKE")]
    public bool ScreenShakeEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable flash effects.
    /// </summary>
    [SettingName("MAGGYHELPER_FLASH_EFFECTS")]
    public bool FlashEffectsEnabled { get; set; } = true;
    
    /// <summary>
    /// Enable controller rumble.
    /// </summary>
    [SettingName("MAGGYHELPER_RUMBLE")]
    public bool RumbleEnabled { get; set; } = true;
    
    #endregion
}
