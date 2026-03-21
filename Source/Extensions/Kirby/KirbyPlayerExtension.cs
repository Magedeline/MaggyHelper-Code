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
        private bool _kirbySkinApplied;
        private float _invulnTimer;
        private float _hurtAnimTimer;
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

        /// <summary>
        /// Optional compat-provided animation override evaluated after ability animations
        /// but before default movement animation selection.
        /// </summary>
        public string CompatAnimationOverride { get; set; }

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

        /// <summary>Quick access: Precision combat ability.</summary>
        public KirbyPrecisionCombatAbility PrecisionCombat => _abilityManager.Get<KirbyPrecisionCombatAbility>();

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
            _abilityManager.Register(new KirbyPrecisionCombatAbility());
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
            SaveToSession();
            DisablePlayerSync();

            if (_hud != null)
            {
                _hud.RemoveSelf();
                _hud = null;
            }

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
            {
                _player = Scene.Tracker.GetEntity<Player>();
                // Re-apply visibility suppression so the real player doesn't reappear after transitions
                if (_player != null && _syncToPlayer)
                {
                    if (_settings.UseVanillaPlayerRender)
                    {
                        SetPlayerVisualsVisible(true);
                        ApplyKirbyPlayerSkin();
                    }
                    else
                    {
                        SetPlayerVisualsVisible(false);
                    }
                }
            }

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
                _invulnTimer = Math.Max(0f, _invulnTimer - Engine.DeltaTime);

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
                if (_settings.UseVanillaPlayerRender)
                {
                    SetPlayerVisualsVisible(true);
                    ApplyKirbyPlayerSkin();
                    if (_player.Hair != null)
                        _player.Hair.Visible = false;
                    if (_sprite != null)
                        _sprite.Visible = false;
                }
                else
                {
                    SetPlayerVisualsVisible(false);
                    if (_sprite != null)
                        _sprite.Visible = true;
                }
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
                if (_settings.UseVanillaPlayerRender)
                {
                    RestoreDefaultPlayerSkin();
                    if (_sprite != null)
                        _sprite.Visible = true;
                }

                SetPlayerVisualsVisible(true);
            }
        }

        private void SetPlayerVisualsVisible(bool visible)
        {
            if (_player == null)
                return;

            _player.Visible = visible;
            if (_player.Sprite != null)
                _player.Sprite.Visible = visible;
            if (_player.Hair != null)
                _player.Hair.Visible = visible;

            if (_settings.UseVanillaPlayerRender && _syncToPlayer)
            {
                if (_player.Hair != null)
                    _player.Hair.Visible = false;
            }
        }

        private void ApplyKirbyPlayerSkin()
        {
            if (_player?.Sprite == null || _kirbySkinApplied)
                return;

            if (!GFX.SpriteBank.Has("kirby_player"))
                return;

            GFX.SpriteBank.CreateOn(_player.Sprite, "kirby_player");
            _kirbySkinApplied = true;
        }

        private void RestoreDefaultPlayerSkin()
        {
            if (_player?.Sprite == null || !_kirbySkinApplied)
                return;

            string restoreId = null;
            if (_player.Scene is Level level)
            {
                var areaData = AreaData.Get(level.Session.Area);
                if (areaData != null && AreaModeExtender.IsOurMap(areaData))
                {
                    restoreId = _player.Sprite.Mode == PlayerSpriteMode.Playback
                        ? "maggy_player_playback"
                        : "maggy_player";
                }
            }

            restoreId ??= "player";

            if (GFX.SpriteBank.Has(restoreId))
                GFX.SpriteBank.CreateOn(_player.Sprite, restoreId);

            _kirbySkinApplied = false;
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

            // Cancel any active abilities so their animation IDs stop being requested.
            _abilityManager.CancelAll();

            CurrentHealth -= amount;
            _invulnTimer = _settings.DamageInvulnTime;
            Audio.Play(SFX_HURT, Position);

            if (CurrentHealth > 0)
                _hurtAnimTimer = 0.4f;

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

            Vector2 dieDirection = _player != null
                ? (_player.Position - source).SafeNormalize()
                : Vector2.Zero;

            _player?.Die(dieDirection);
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

            CurrentHealth = Math.Clamp(session.KirbyHealth, 0, MaxHealth);
            CurrentStamina = Math.Clamp(session.KirbyStamina, 0f, MaxStamina);

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
            }
            catch
            {
                _sprite = GFX.SpriteBank.Create("kirby_player");
            }

            Add(_sprite);
            _sprite.Position = Vector2.Zero;
            _sprite.Scale = Vector2.One;
            PlayResolvedAnim(KirbyAnimIds.Logical.Idle);
        }

        private void UpdateAnimation()
        {
            if (_sprite == null || _player == null) return;

            _sprite.FlipX = _player.Facing == Facings.Left;
            _sprite.FlipY = false;

            if (_isDead) { PlayResolvedAnim(KirbyAnimIds.Logical.Death); return; }

            // Hurt flash: play damage animation while the invuln window is active.
            if (_hurtAnimTimer > 0f)
            {
                _hurtAnimTimer -= Engine.DeltaTime;
                PlayResolvedAnim(KirbyAnimIds.Logical.Damage);
                return;
            }

            // Let active abilities override animation (guarded against missing IDs).
            string abilityAnim = _abilityManager.GetActiveAnimation();
            if (abilityAnim != null)
            {
                PlayResolvedAnim(abilityAnim);
                return;
            }

            if (!string.IsNullOrEmpty(CompatAnimationOverride))
            {
                PlayResolvedAnim(CompatAnimationOverride);
                return;
            }

            // Dash
            if (_player.DashAttacking || _player.StateMachine.State == 2)
            {
                PlayResolvedAnim(KirbyAnimIds.Logical.Dash); return;
            }

            // Airborne
            if (!_player.OnGround())
            {
                if (Hover != null && Hover.IsHovering)
                    PlayResolvedAnim(KirbyAnimIds.Logical.Hover);
                else if (_player.Speed.Y > 0f)
                    PlayResolvedAnim(KirbyAnimIds.Logical.Fall);
                else
                    PlayResolvedAnim(KirbyAnimIds.Logical.Jump);
                return;
            }

            // Ground
            float speedX = Math.Abs(_player.Speed.X);
            if (speedX <= 1f)
                PlayResolvedAnim(KirbyAnimIds.Logical.Idle);
            else if (speedX < 90f)
                PlayResolvedAnim(KirbyAnimIds.Logical.Walk);
            else
                PlayResolvedAnim(KirbyAnimIds.Logical.Run);
        }

        private void PlayResolvedAnim(string requestedAnimId)
        {
            if (_sprite == null)
                return;

            string resolvedAnimId = ResolveAnim(requestedAnimId);
            if (string.IsNullOrEmpty(resolvedAnimId))
                return;

            if (!_sprite.Has(resolvedAnimId))
            {
                if (_sprite.Has(KirbyAnimIds.Idle))
                    resolvedAnimId = KirbyAnimIds.Idle;
                else
                    return;
            }

            try
            {
                _sprite.Play(resolvedAnimId);
            }
            catch
            {
                // Keep gameplay running if a third-party modifier mutates animation dictionaries at runtime.
            }
        }

        /// <summary>Map a logical animation id to the sprite bank id.</summary>
        public string ResolveAnim(string baseId)
        {
            return KirbyPlayerSpriteCore.ResolveAnimId(_sprite, baseId);
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


