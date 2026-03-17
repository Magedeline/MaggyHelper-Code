namespace MaggyHelper
{
    /// <summary>
    /// Kirby-specific settings. These mirror the corresponding settings in IngesteModuleSettings
    /// but provide a grouped accessor for Kirby-related functionality.
    /// </summary>
    public class KirbySettings
    {
        #region Health & Stamina
        
        /// <summary>
        /// Maximum health for Kirby form.
        /// </summary>
        public int MaxHealth => IngesteModule.Settings?.KirbyMaxHealth ?? 6;
        
        /// <summary>
        /// Maximum stamina for floating.
        /// </summary>
        public float MaxStamina => IngesteModule.Settings?.KirbyMaxStaminaFloat ?? 100f;
        
        #endregion

        #region Input Modes
        
        /// <summary>
        /// Whether inhale requires holding the button.
        /// </summary>
        public bool InhaleHoldMode => IngesteModule.Settings?.InhaleHoldMode ?? false;
        
        /// <summary>
        /// Whether hover requires holding the button.
        /// </summary>
        public bool HoverHoldMode => IngesteModule.Settings?.HoverHoldMode ?? false;
        
        #endregion

        #region Movement
        
        /// <summary>
        /// Inhale range in pixels.
        /// </summary>
        public float InhaleRange => IngesteModule.Settings?.InhaleRangeFloat ?? 64f;
        
        /// <summary>
        /// Fall speed while hovering.
        /// </summary>
        public float HoverFallSpeed => IngesteModule.Settings?.HoverFallSpeedFloat ?? 60f;
        
        /// <summary>
        /// Maximum float jumps before landing required.
        /// </summary>
        public int MaxFloatJumps => IngesteModule.Settings?.KirbyMaxFloatJumps ?? 5;
        
        /// <summary>
        /// Dash speed multiplier.
        /// </summary>
        public float DashSpeedMultiplier => IngesteModule.Settings?.KirbyDashSpeedMultiplier ?? 1f;
        
        /// <summary>
        /// Slide speed multiplier.
        /// </summary>
        public float SlideSpeedMultiplier => IngesteModule.Settings?.KirbySlideSpeedMultiplier ?? 1f;
        
        /// <summary>
        /// Movement precision multiplier (1.0 = normal, higher = more precise).
        /// </summary>
        public float MovementPrecision => 1.0f;

        /// <summary>
        /// Inhale duration in seconds before auto-cancel.
        /// </summary>
        public float InhaleDuration => 0.25f;

        /// <summary>
        /// Mouth-open buffer after inhale (seconds).
        /// </summary>
        public float MouthOpenTime => 0.15f;

        /// <summary>
        /// Inhale pull speed (pixels per second).
        /// </summary>
        public float InhalePullSpeed => 120f;

        /// <summary>
        /// Lerp factor for inhale pull.
        /// </summary>
        public float InhalePullLerp => 6f;

        /// <summary>
        /// Minimum dot product for inhale cone (1.0 = straight).
        /// </summary>
        public float InhaleConeDot => 0.35f;

        /// <summary>
        /// Distance to swallow target.
        /// </summary>
        public float InhaleSwallowDistance => 8f;

        /// <summary>
        /// Horizontal mouth offset for inhale effect.
        /// </summary>
        public float InhaleMouthOffset => 12f;

        /// <summary>
        /// Spit projectile speed (pixels per second).
        /// </summary>
        public float SpitSpeed => 260f;

        /// <summary>
        /// Spit cooldown in seconds.
        /// </summary>
        public float SpitCooldown => 0.2f;

        /// <summary>
        /// Hover stamina drain per second.
        /// </summary>
        public float HoverStaminaDrain => 18f;

        /// <summary>
        /// Hover flap upward speed.
        /// </summary>
        public float HoverFlapSpeed => -140f;

        /// <summary>
        /// Hover gravity approach speed.
        /// </summary>
        public float HoverGravity => 240f;
        
        #endregion

        #region Power System
        
        /// <summary>
        /// Whether power copy abilities are enabled.
        /// </summary>
        public bool PowerCopyEnabled => IngesteModule.Settings?.PowerCopyEnabled ?? true;
        
        /// <summary>
        /// Duration of copied powers in seconds (0 = infinite).
        /// </summary>
        public int PowerDurationSeconds => IngesteModule.Settings?.PowerDurationSeconds ?? 0;
        
        /// <summary>
        /// Whether powers persist through death.
        /// </summary>
        public bool PowerPersistOnDeath => IngesteModule.Settings?.PowerPersistOnDeath ?? false;
        
        /// <summary>
        /// Allow dropping power with dedicated button.
        /// </summary>
        public bool PowerDropEnabled => IngesteModule.Settings?.PowerDropEnabled ?? true;
        
        /// <summary>
        /// Show power indicator HUD.
        /// </summary>
        public bool ShowPowerHud => IngesteModule.Settings?.ShowPowerHud ?? true;

        /// <summary>
        /// Allow inhaling carryable objects (Theo, jellyfish, crystals).
        /// </summary>
        public bool AllowInhaleCarryables => true;

        /// <summary>
        /// Allow inhaling bosses.
        /// </summary>
        public bool AllowInhaleBosses => true;
        
        #endregion

        #region Knight Mode
        
        /// <summary>
        /// Whether Knight mode is enabled.
        /// </summary>
        public bool KnightModeEnabled => IngesteModule.Settings?.KnightModeEnabled ?? true;
        
        /// <summary>
        /// Knight damage multiplier.
        /// </summary>
        public float KnightDamageMultiplier => IngesteModule.Settings?.KnightDamageMultiplier ?? 2f;
        
        /// <summary>
        /// Allow forced knight transformation at low health.
        /// </summary>
        public bool KnightLowHealthTransform => IngesteModule.Settings?.KnightLowHealthTransform ?? true;
        
        #endregion

        #region General
        
        /// <summary>
        /// Whether Kirby player mode is enabled.
        /// </summary>
        public bool KirbyPlayerEnabled => IngesteModule.Settings?.KirbyPlayerEnabled ?? true;
        
        /// <summary>
        /// Whether Kirby player mode is active (global toggle).
        /// </summary>
        public bool KirbyPlayerMode => IngesteModule.Settings?.KirbyPlayerMode ?? false;
        
        /// <summary>
        /// Whether Star Warrior mode is active (enhanced abilities).
        /// </summary>
        public bool StarWarriorMode => false;

        /// <summary>
        /// Damage invulnerability time in seconds after being hit.
        /// </summary>
        public float DamageInvulnTime => 0.6f;
        
        #endregion

        #region Input Helpers
        
        /// <summary>
        /// Check if a key is currently pressed.
        /// Used for input handling in Kirby states.
        /// </summary>
        public bool IsKeyPressed(string keyName)
        {
            var settings = IngesteModule.Settings;
            if (settings == null) return false;
            
            return keyName switch
            {
                "Jump" => Input.Jump.Pressed,
                "Dash" => Input.Dash.Pressed,
                "Grab" => Input.Grab.Pressed,
                "Talk" => Input.Talk.Pressed,
                "CrouchDash" => Input.CrouchDash.Pressed,
                "Inhale" => settings.KirbyInhaleBind.Pressed,
                "Attack" => settings.KirbyAttackBind.Pressed,
                "Hover" => settings.KirbyHoverBind.Pressed,
                "Spit" => settings.KirbySpitBind.Pressed,
                "CyclePower" => settings.KirbyCyclePowerBind.Pressed,
                "DropPower" => settings.KirbyDropPowerBind.Pressed,
                "Slide" => settings.KirbySlideBind.Pressed,
                "Knight" => settings.KirbyKnightBind.Pressed,
                _ => false
            };
        }
        
        /// <summary>
        /// Check if a key is currently held down.
        /// </summary>
        public bool IsKeyCheck(string keyName)
        {
            var settings = IngesteModule.Settings;
            if (settings == null) return false;
            
            return keyName switch
            {
                "Jump" => Input.Jump.Check,
                "Dash" => Input.Dash.Check,
                "Grab" => Input.Grab.Check,
                "Talk" => Input.Talk.Check,
                "CrouchDash" => Input.CrouchDash.Check,
                "Inhale" => settings.KirbyInhaleBind.Check,
                "Attack" => settings.KirbyAttackBind.Check,
                "Hover" => settings.KirbyHoverBind.Check,
                "Spit" => settings.KirbySpitBind.Check,
                "CyclePower" => settings.KirbyCyclePowerBind.Check,
                "DropPower" => settings.KirbyDropPowerBind.Check,
                "Slide" => settings.KirbySlideBind.Check,
                "Knight" => settings.KirbyKnightBind.Check,
                _ => false
            };
        }
        
        /// <summary>
        /// Get the button binding for a specific action.
        /// </summary>
        public ButtonBinding GetBinding(string actionName)
        {
            var settings = IngesteModule.Settings;
            if (settings == null) return null;
            
            return actionName switch
            {
                "Inhale" => settings.KirbyInhaleBind,
                "Attack" => settings.KirbyAttackBind,
                "Hover" => settings.KirbyHoverBind,
                "Spit" => settings.KirbySpitBind,
                "CyclePower" => settings.KirbyCyclePowerBind,
                "DropPower" => settings.KirbyDropPowerBind,
                "Slide" => settings.KirbySlideBind,
                "Knight" => settings.KirbyKnightBind,
                _ => null
            };
        }
        
        #endregion
    }
}
