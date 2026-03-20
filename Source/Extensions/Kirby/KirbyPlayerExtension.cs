using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using MaggyHelper.Extensions.Core;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// KirbyPlayerExtension — the replacement for the monolithic KirbyPlayer.
    /// 
    /// This is a lightweight Actor that attaches to the vanilla Celeste Player
    /// via the PlayerExtensionCore system. Instead of reimplementing all 26 Player
    /// states, it layers Kirby-specific abilities on top of the standard Player:
    /// 
    ///   • Inhale / Swallow / Spit
    ///   • Hover (multi-flap floating)
    ///   • Melee fist / weapon attacks
    ///   • Range weapon / projectile attacks
    ///   • Copy Ability system (gain powers from swallowed enemies)
    ///   • Health / Stamina HUD
    /// 
    /// Each ability is a self-contained KirbyAbilityBase subclass registered
    /// with the KirbyAbilityManager.
    /// </summary>
    [CustomEntity("MaggyHelper/KirbyPlayer")]
    [Tracked]
    public class KirbyPlayerExtension : Actor
    {
        #region Constants

        private const string SFX_PATH = "event:/desolozantas/char/kirby/";
        private const string SFX_TRANSFORM = SFX_PATH + "transform_in";
        private const string SFX_HURT = SFX_PATH + "predeath";
        private const string SFX_DIE = SFX_PATH + "predeath";
        private const string SFX_HEAL = SFX_PATH + "revive";
        private const string SFX_BOUNCE = SFX_PATH + "bounce";

        #endregion

        #region Fields

        private Player _player;
        private Sprite _sprite;
        private KirbyHealthDisplay _hud;
        private bool _syncToPlayer;
        private bool _useExtSprite;
        private float _invulnTimer;
        private bool _wasDead;
        private bool _isDead;

        private readonly KirbySettings _settings = new KirbySettings();
        private readonly KirbyAbilityManager _abilityManager;

        #endregion

        #region Properties

        /// <summary>The vanilla Player this extension is attached to.</summary>
        public Player Player => _player;

        /// <summary>Kirby tuning settings.</summary>
        public KirbySettings Settings => _settings;

        /// <summary>The ability manager hosting all Kirby abilities.</summary>
        public KirbyAbilityManager AbilityManager => _abilityManager;

        /// <summary>Current health.</summary>
        public int CurrentHealth { get; private set; }

        /// <summary>Maximum health.</summary>
        public int MaxHealth => _settings.MaxHealth;

        /// <summary>Current hover/float stamina.</summary>
        public float CurrentStamina { get; set; }

        /// <summary>Maximum stamina.</summary>
        public float MaxStamina => _settings.MaxStamina;

        /// <summary>Whether Kirby is dead.</summary>
        public bool IsDead => _isDead;

        /// <summary>Whether player sync is active (Kirby sprite replaces player).</summary>
        public bool IsSynced => _syncToPlayer;

        /// <summary>Player facing direction.</summary>
        public Facings Facing => _player?.Facing ?? Facings.Right;

        /// <summary>Whether the player is dashing.</summary>
        public bool IsDashing => _player?.DashAttacking ?? false;

        /// <summary>The Kirby sprite.</summary>
        public Sprite KirbySprite => _sprite;

        /// <summary>Current copy ability power state.</summary>
        public KirbyMode.KirbyPowerState CurrentPower { get; private set; } = KirbyMode.KirbyPowerState.None;

        /// <summary>Quick access: Inhale ability.</summary>
        public KirbyInhaleAbility Inhale => _abilityManager.Get<KirbyInhaleAbility>();

        /// <summary>Quick access: Hover ability.</summary>
        public KirbyHoverAbility Hover => _abilityManager.Get<KirbyHoverAbility>();

        /// <summary>Quick access: Spit ability.</summary>
        public KirbySpitAbility Spit => _abilityManager.Get<KirbySpitAbility>();

        /// <summary>Quick access: Melee ability.</summary>
        public KirbyMeleeAbility Melee => _abilityManager.Get<KirbyMeleeAbility>();

        /// <summary>Quick access: Range ability.</summary>
        public KirbyRangeAbility Range => _abilityManager.Get<KirbyRangeAbility>();

        /// <summary>Quick access: Copy ability.</summary>
        public KirbyCopyAbility CopyAbility => _abilityManager.Get<KirbyCopyAbility>();

        #endregion

        #region Constructor

        public KirbyPlayerExtension(Vector2 position) : base(position)
        {
            Tag = Tags.Persistent | Tags.TransitionUpdate;
            Depth = Depths.Player;
            Collider = new Hitbox(16f, 20f, -8f, -20f);

            _abilityManager = new KirbyAbilityManager(this);

            // Register all core abilities
            _abilityManager.Register(new KirbyInhaleAbility());
            _abilityManager.Register(new KirbyHoverAbility());
            _abilityManager.Register(new KirbySpitAbility());
            _abilityManager.Register(new KirbyMeleeAbility());
            _abilityManager.Register(new KirbyRangeAbility());
            _abilityManager.Register(new KirbyCopyAbility());
        }

        /// <summary>
        /// EntityData constructor for Lönn placement.
        /// </summary>
        public KirbyPlayerExtension(EntityData data, Vector2 offset)
            : this(data.Position + offset)
        {
        }

        #endregion

        #region Lifecycle

        public override void Added(Scene scene)
        {
            base.Added(scene);

            _player = scene.Tracker.GetEntity<Player>();
            LoadFromSession();
            CreateSprite();

            if (_settings.ShowPowerHud)
            {
                _hud = new KirbyHealthDisplay(this);
                scene.Add(_hud);
            }

            _abilityManager.OnAdded(scene);
        }

        public override void Removed(Scene scene)
        {
            _abilityManager.OnRemoved(scene);
            base.Removed(scene);
        }

        public override void Update()
        {
            base.Update();

            if (Scene is not Level)
                return;

            // Re-acquire player if lost
            if (_player == null || _player.Scene != Scene)
                _player = Scene.Tracker.GetEntity<Player>();

            if (_player == null)
                return;

            // Sync position
            if (_syncToPlayer)
            {
                Position = _player.Position;
                Depth = _player.Depth - 1;
            }

            // Timers
            if (_invulnTimer > 0f)
                _invulnTimer -= Engine.DeltaTime;

            // Update all abilities
            _abilityManager.Update();

            // Update mod compatibility bridges (CommunalHelper, BossesHelper, etc.)
            if (Scene is Level level)
            {
                ModCompat.KirbyModCompatManager.Update(this, _player, level);
            }

            // Animation
            UpdateAnimation();

            // Respawn detection
            UpdateRespawnState();
        }

        public override void Render()
        {
            if (!Visible || _sprite == null)
                return;

            _sprite.Render();

            // Let abilities render overlays (charge indicators, aim lines, etc.)
            _abilityManager.Render();
        }

        #endregion

        #region Player Sync

        /// <summary>
        /// Activate sync: hide vanilla player visuals, show Kirby sprite overlay.
        /// </summary>
        public void EnablePlayerSync()
        {
            _syncToPlayer = true;
            if (_player != null)
            {
                _player.Visible = false;
                if (_player.Sprite != null) _player.Sprite.Visible = false;
                if (_player.Hair != null) _player.Hair.Visible = false;
            }
        }

        /// <summary>
        /// Deactivate sync: restore vanilla player visuals.
        /// </summary>
        public void DisablePlayerSync()
        {
            _syncToPlayer = false;
            if (_player != null)
            {
                _player.Visible = true;
                if (_player.Sprite != null) _player.Sprite.Visible = true;
                if (_player.Hair != null) _player.Hair.Visible = true;
            }
        }

        #endregion

        #region Health

        /// <summary>Heal Kirby by the given amount (clamped to max).</summary>
        public void Heal(int amount = 1)
        {
            if (amount <= 0 || _isDead) return;

            int prev = CurrentHealth;
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
            if (CurrentHealth > prev)
                Audio.Play(SFX_HEAL, Position);
        }

        /// <summary>Damage Kirby. Returns true if damage was applied.</summary>
        public bool TakeDamage(int amount = 1, Vector2? source = null)
        {
            if (amount <= 0 || _isDead || _invulnTimer > 0f) return false;

            // Allow mod compatibility bridges to block damage
            // (e.g., CommunalHelper dream tunnel dash invulnerability)
            if (ModCompat.KirbyModCompatManager.OnKirbyDamage(this, amount, source ?? Vector2.Zero))
                return false;

            CurrentHealth -= amount;
            _invulnTimer = _settings.DamageInvulnTime;
            Audio.Play(SFX_HURT, Position);

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                TriggerDeath(source ?? Vector2.Zero);
            }

            return true;
        }

        private void TriggerDeath(Vector2 source)
        {
            if (_isDead) return;

            // Allow mod compatibility bridges to cancel death
            // (e.g., BossesHelper fake death with remaining health)
            if (ModCompat.KirbyModCompatManager.OnKirbyDeath(this, _player))
                return;

            _isDead = true;
            Audio.Play(SFX_DIE, Position);

            if (Scene is Level level)
            {
                level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 20, Position, Vector2.One * 16f, Color.Pink);
                level.Displacement.AddBurst(Position, 0.4f, 16f, 48f, 0.4f);
            }

            _player?.Die((_player.Position - source).SafeNormalize());
        }

        #endregion

        #region Power State

        /// <summary>Set the current copy power state.</summary>
        public void SetPowerState(KirbyMode.KirbyPowerState power)
        {
            var prev = CurrentPower;
            CurrentPower = power;

            var session = MaggyHelperModule.Session;
            if (session != null)
            {
                session.CurrentKirbyPower = power.ToString();
                if (power != KirbyMode.KirbyPowerState.None && power != prev)
                    session.PowersCopied += 1;
            }

            // Notify copy ability system
            CopyAbility?.OnPowerChanged(prev, power);
        }

        #endregion

        #region Session Persistence

        /// <summary>Save current Kirby state to session.</summary>
        public void SaveToSession()
        {
            var session = MaggyHelperModule.Session;
            if (session == null) return;

            session.KirbyHealth = CurrentHealth;
            session.KirbyStamina = CurrentStamina;
            session.CurrentKirbyPower = CurrentPower.ToString();
            session.IsKirbyModeActive = true;
        }

        private void LoadFromSession()
        {
            var session = MaggyHelperModule.Session;
            if (session == null)
            {
                CurrentHealth = MaxHealth;
                CurrentStamina = MaxStamina;
                return;
            }

            CurrentHealth = Math.Min(session.KirbyHealth, MaxHealth);
            CurrentStamina = Math.Min(session.KirbyStamina, MaxStamina);

            if (Enum.TryParse(session.CurrentKirbyPower, out KirbyMode.KirbyPowerState power))
                CurrentPower = power;

            if (CurrentHealth <= 0)
                CurrentHealth = MaxHealth;
        }

        #endregion

        #region Animation

        private void CreateSprite()
        {
            try
            {
                _sprite = GFX.SpriteBank.Create("kirby_player_ext");
                _useExtSprite = true;
            }
            catch
            {
                _sprite = GFX.SpriteBank.Create("kirby_player");
                _useExtSprite = false;
            }

            Add(_sprite);
            _sprite.Position = Vector2.Zero;
            _sprite.Scale = Vector2.One;
            _sprite.Play(ResolveAnim("idle"));
        }

        private void UpdateAnimation()
        {
            if (_sprite == null || _player == null) return;

            _sprite.FlipX = _player.Facing == Facings.Left;
            _sprite.FlipY = false;

            if (_isDead) { _sprite.Play(ResolveAnim("death")); return; }

            // Let active abilities override animation
            string abilityAnim = _abilityManager.GetActiveAnimation();
            if (abilityAnim != null)
            {
                _sprite.Play(ResolveAnim(abilityAnim));
                return;
            }

            // Dash
            if (_player.DashAttacking || _player.StateMachine.State == 2)
            {
                _sprite.Play(ResolveAnim("dash")); return;
            }

            // Airborne
            if (!_player.OnGround())
            {
                if (Hover != null && Hover.IsHovering)
                    _sprite.Play(ResolveAnim("hover"));
                else if (_player.Speed.Y > 0f)
                    _sprite.Play(ResolveAnim("fall"));
                else
                    _sprite.Play(ResolveAnim("jump"));
                return;
            }

            // Ground
            float speedX = Math.Abs(_player.Speed.X);
            if (speedX <= 1f)
                _sprite.Play(ResolveAnim("idle"));
            else if (speedX < 90f)
                _sprite.Play(ResolveAnim("walk"));
            else
                _sprite.Play(ResolveAnim("run"));
        }

        /// <summary>Map a logical animation id to the sprite bank id.</summary>
        public string ResolveAnim(string baseId)
        {
            if (!_useExtSprite)
            {
                return baseId switch
                {
                    "run" => "runFast",
                    _ => baseId
                };
            }

            return baseId switch
            {
                "idle" => "kirby_idle",
                "walk" => "kirby_walk",
                "run" => "kirby_run",
                "jump" => "kirby_jump",
                "fall" => "kirby_fall",
                "dash" => "kirby_run",
                "inhale" => "kirby_inhale",
                "hover" => "kirby_hover",
                "slide" => "kirby_slide",
                "death" => "kirby_death",
                "spit" => "kirby_spit",
                "melee" => "kirby_melee",
                "melee_up" => "kirby_melee_up",
                "melee_down" => "kirby_melee_down",
                "range" => "kirby_range",
                "swallow" => "kirby_swallow",
                "copy" => "kirby_copy",
                "mouthful" => "kirby_mouthful",
                _ => "kirby_idle"
            };
        }

        #endregion

        #region Respawn

        private void UpdateRespawnState()
        {
            if (_player == null) return;

            const int StIntroRespawn = 14;
            bool isRespawn = _player.StateMachine.State == StIntroRespawn;
            if (isRespawn && !_wasDead)
            {
                CurrentHealth = MaxHealth;
                CurrentStamina = MaxStamina;
                _isDead = false;
                _abilityManager.OnRespawn();
            }
            _wasDead = isRespawn;
        }

        #endregion
    }
}


