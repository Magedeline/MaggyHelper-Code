using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Entities
{
    /// <summary>
    /// Unified player health management system that works with both Kirby and normal players.
    /// This provides HP-based gameplay instead of Celeste's one-hit death system.
    /// </summary>
    [Tracked]
    public class PlayerHealthManager : Entity
    {
        #region Constants
        
        private const int DEFAULT_MAX_HP = 6;
        private const int KIRBY_DEFAULT_MAX_HP = 6;
        private const float INVINCIBILITY_TIME = 1.5f;
        private const float FLASH_INTERVAL = 0.1f;
        private const float LOW_HEALTH_THRESHOLD = 0.3f;
        
        // Audio events
        private const string SFX_DAMAGE = "event:/game/general/spring";
        private const string SFX_HEAL = "event:/game/general/diamond_touch";
        private const string SFX_DEATH = "event:/char/madeline/death";
        private const string SFX_LOW_HEALTH = "event:/game/general/assist_dash_aim";
        
        #endregion
        
        #region Fields
        
        private global::Celeste.Player player;
        private Level level;
        
        // Health state
        private int currentHP;
        private int maxHP;
        private float invincibilityTimer;
        private bool isDead;
        
        // Visual effects
        private float flashTimer;
        private bool isFlashing;
        private float lowHealthPulse;
        
        // Mode tracking
        private bool isKirbyMode;
        
        // Events
        public event Action<int, int> OnHealthChanged;
        public event Action OnPlayerDeath;
        public event Action<int> OnDamageTaken;
        public event Action<int> OnHealed;
        
        #endregion
        
        #region Properties
        
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public float HealthPercent => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public bool IsDead => isDead;
        public bool IsInvincible => invincibilityTimer > 0f;
        public bool IsLowHealth => HealthPercent <= LOW_HEALTH_THRESHOLD;
        public bool IsKirbyMode => isKirbyMode;
        
        /// <summary>
        /// Singleton instance for easy access
        /// </summary>
        public static PlayerHealthManager Instance { get; private set; }
        
        #endregion
        
        #region Constructor
        
        public PlayerHealthManager(int maxHP = DEFAULT_MAX_HP, bool kirbyMode = false) : base(Vector2.Zero)
        {
            this.maxHP = maxHP;
            this.currentHP = maxHP;
            this.isKirbyMode = kirbyMode;
            this.isDead = false;
            
            Tag = Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;
            Depth = -1000000;
            
            Instance = this;
        }
        
        public PlayerHealthManager(EntityData data, Vector2 offset) 
            : this(data.Int("maxHP", DEFAULT_MAX_HP), data.Bool("kirbyMode", false))
        {
        }
        
        #endregion
        
        #region Lifecycle
        
        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = scene as Level;
            player = level?.Tracker.GetEntity<global::Celeste.Player>();
            
            // Check if already in Kirby mode from session
            if (level?.Session != null)
            {
                isKirbyMode = level.Session.GetFlag("kirby_mode");
                if (isKirbyMode && maxHP == DEFAULT_MAX_HP)
                {
                    maxHP = KIRBY_DEFAULT_MAX_HP;
                    currentHP = maxHP;
                }
            }
            
            Instance = this;
        }
        
        public override void Removed(Scene scene)
        {
            if (Instance == this)
                Instance = null;
            base.Removed(scene);
        }
        
        public override void Update()
        {
            base.Update();
            
            // Update player reference
            if (player == null || player.Dead)
            {
                player = level?.Tracker.GetEntity<global::Celeste.Player>();
            }
            
            // Update invincibility
            if (invincibilityTimer > 0f)
            {
                invincibilityTimer -= Engine.DeltaTime;
                
                // Flash effect
                flashTimer += Engine.DeltaTime;
                if (flashTimer >= FLASH_INTERVAL)
                {
                    flashTimer = 0f;
                    isFlashing = !isFlashing;
                    UpdatePlayerVisibility();
                }
                
                if (invincibilityTimer <= 0f)
                {
                    isFlashing = false;
                    UpdatePlayerVisibility();
                }
            }
            
            // Low health pulse
            if (IsLowHealth && !isDead)
            {
                lowHealthPulse += Engine.DeltaTime * 4f;
            }
        }
        
        private void UpdatePlayerVisibility()
        {
            if (player?.Sprite != null)
            {
                player.Sprite.Color = isFlashing ? Color.White * 0.5f : Color.White;
            }
        }
        
        #endregion
        
        #region Health Management
        
        /// <summary>
        /// Deal damage to the player
        /// </summary>
        /// <param name="amount">Amount of damage to deal</param>
        /// <param name="source">Optional source position for knockback direction</param>
        /// <returns>True if damage was dealt, false if player was invincible</returns>
        public bool TakeDamage(int amount = 1, Vector2? source = null)
        {
            if (isDead || invincibilityTimer > 0f || amount <= 0)
                return false;
            
            int previousHP = currentHP;
            currentHP = Math.Max(0, currentHP - amount);
            
            // Effects
            Audio.Play(SFX_DAMAGE, player?.Position ?? Position);
            level?.Shake(0.2f);
            level?.Flash(Color.Red * 0.3f, false);
            
            // Start invincibility
            invincibilityTimer = INVINCIBILITY_TIME;
            flashTimer = 0f;
            
            // Knockback
            if (player != null && source.HasValue)
            {
                Vector2 knockbackDir = (player.Position - source.Value).SafeNormalize();
                player.Speed = knockbackDir * 150f;
            }
            
            // Events
            OnDamageTaken?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHP, maxHP);
            
            // Check for low health warning
            if (IsLowHealth && currentHP > 0)
            {
                Audio.Play(SFX_LOW_HEALTH);
            }
            
            // Check for death
            if (currentHP <= 0)
            {
                Die();
            }
            
            return true;
        }
        
        /// <summary>
        /// Heal the player
        /// </summary>
        /// <param name="amount">Amount to heal</param>
        public void Heal(int amount = 1)
        {
            if (isDead || amount <= 0)
                return;
            
            int previousHP = currentHP;
            currentHP = Math.Min(maxHP, currentHP + amount);
            
            if (currentHP != previousHP)
            {
                Audio.Play(SFX_HEAL, player?.Position ?? Position);
                
                OnHealed?.Invoke(currentHP - previousHP);
                OnHealthChanged?.Invoke(currentHP, maxHP);
            }
        }
        
        /// <summary>
        /// Fully heal the player
        /// </summary>
        public void FullHeal()
        {
            Heal(maxHP);
        }
        
        /// <summary>
        /// Set max HP (and optionally heal to match)
        /// </summary>
        public void SetMaxHP(int newMax, bool healToMax = false)
        {
            maxHP = Math.Max(1, newMax);
            if (healToMax || currentHP > maxHP)
            {
                currentHP = maxHP;
            }
            OnHealthChanged?.Invoke(currentHP, maxHP);
        }
        
        /// <summary>
        /// Set current HP directly
        /// </summary>
        public void SetHP(int hp)
        {
            currentHP = Math.Clamp(hp, 0, maxHP);
            OnHealthChanged?.Invoke(currentHP, maxHP);
            
            if (currentHP <= 0 && !isDead)
            {
                Die();
            }
        }
        
        /// <summary>
        /// Handle player death
        /// </summary>
        private void Die()
        {
            if (isDead) return;
            
            isDead = true;
            Audio.Play(SFX_DEATH, player?.Position ?? Position);
            
            OnPlayerDeath?.Invoke();
            
            // Trigger standard Celeste death
            player?.Die(Vector2.Zero);
        }
        
        /// <summary>
        /// Reset health to full (used on respawn)
        /// </summary>
        public void Reset()
        {
            isDead = false;
            currentHP = maxHP;
            invincibilityTimer = 0f;
            isFlashing = false;
            UpdatePlayerVisibility();
            
            OnHealthChanged?.Invoke(currentHP, maxHP);
        }
        
        /// <summary>
        /// Grant temporary invincibility
        /// </summary>
        public void GrantInvincibility(float duration)
        {
            invincibilityTimer = Math.Max(invincibilityTimer, duration);
        }
        
        #endregion
        
        #region Mode Switching
        
        /// <summary>
        /// Enable Kirby mode HP system
        /// </summary>
        public void EnableKirbyMode(int kirbyMaxHP = KIRBY_DEFAULT_MAX_HP)
        {
            isKirbyMode = true;
            maxHP = kirbyMaxHP;
            currentHP = maxHP;
            
            level?.Session?.SetFlag("kirby_mode", true);
            OnHealthChanged?.Invoke(currentHP, maxHP);
        }
        
        /// <summary>
        /// Disable Kirby mode and return to normal player HP
        /// </summary>
        public void DisableKirbyMode(int normalMaxHP = DEFAULT_MAX_HP)
        {
            isKirbyMode = false;
            maxHP = normalMaxHP;
            currentHP = maxHP;
            
            level?.Session?.SetFlag("kirby_mode", false);
            OnHealthChanged?.Invoke(currentHP, maxHP);
        }
        
        #endregion
        
        #region Static Helpers
        
        /// <summary>
        /// Get or create the PlayerHealthManager for the current level
        /// </summary>
        public static PlayerHealthManager GetOrCreate(Level level, int maxHP = DEFAULT_MAX_HP)
        {
            if (Instance != null && Instance.Scene == level)
                return Instance;
            
            var existing = level.Tracker.GetEntity<PlayerHealthManager>();
            if (existing != null)
                return existing;
            
            var manager = new PlayerHealthManager(maxHP);
            level.Add(manager);
            return manager;
        }
        
        /// <summary>
        /// Try to deal damage through the health manager
        /// </summary>
        public static bool TryDamagePlayer(int amount = 1, Vector2? source = null)
        {
            return Instance?.TakeDamage(amount, source) ?? false;
        }
        
        /// <summary>
        /// Try to heal through the health manager
        /// </summary>
        public static void TryHealPlayer(int amount = 1)
        {
            Instance?.Heal(amount);
        }
        
        #endregion
    }
}
