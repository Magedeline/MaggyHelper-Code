using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;
using System.Collections.Generic;
using MaggyHelper.Entities.Kirby;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's melee combat ability — punches, kicks, and weapon-based close-range attacks.
    /// 
    /// Behaviour:
    ///   • Press Attack to punch/kick in faced direction
    ///   • Hold Up+Attack for uppercut, Down+Attack for ground pound/low sweep 
    ///   • Combo system: repeated presses chain into 3-hit combo (jab → jab → hook)
    ///   • With a copy power that grants a melee weapon (Sword, Hammer, Fighter, etc.),
    ///     attacks become weapon swings with increased range/damage
    ///   • Attack hitbox spawns briefly in front of Kirby
    ///   • Attacks interact with KirbyActorBase enemies for damage
    /// </summary>
    public class KirbyMeleeAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "melee";
        public override string DisplayName => "Melee Attack";
        public override float MaxCooldown => ATTACK_COOLDOWN;

        #region Constants

        private const string SFX_PUNCH = "event:/desolozantas/char/kirby/kirby_knight/punch_A";
        private const string SFX_WEAPON = "event:/desolozantas/char/kirby/kirby_knight/punch_B";
        private const string SFX_COMBO_FINISH = "event:/desolozantas/char/kirby/kirby_knight/punch_Final";

        private const float ATTACK_COOLDOWN = 0.18f;
        private const float ATTACK_DURATION = 0.12f;
        private const float COMBO_WINDOW = 0.4f;
        private const int MAX_COMBO = 3;

        // Hitbox sizes (pixels)
        private const float FIST_RANGE = 16f;
        private const float FIST_HEIGHT = 12f;
        private const float WEAPON_RANGE = 28f;
        private const float WEAPON_HEIGHT = 16f;

        // Damage
        private const int FIST_DAMAGE = 1;
        private const int WEAPON_DAMAGE = 2;
        private const int COMBO_FINISH_DAMAGE = 3;

        // Knockback
        private const float KNOCKBACK_SPEED = 160f;
        private const float UPPERCUT_SPEED_Y = -180f;

        #endregion

        #region Enums

        public enum MeleeDirection
        {
            Forward,
            Up,
            Down
        }

        public enum MeleeType
        {
            Fist,       // No power / non-melee power
            Sword,      // Sword power
            Hammer,     // Hammer power
            Fighter,    // Fighter/martial arts
            Cutter      // Cutter (close-range slash)
        }

        #endregion

        #region Fields

        private int _comboStep;
        private float _comboTimer;
        private float _attackTimer;
        private MeleeDirection _attackDir;
        private MeleeType _meleeType;
        private bool _hitboxActive;
        private Hitbox _attackHitbox;
        private Rectangle _lastHitArea;

        #endregion

        #region Properties

        /// <summary>Current combo step (0 = not comboing, 1-3 = combo hits).</summary>
        public int ComboStep => _comboStep;

        /// <summary>Current melee weapon type (derived from copy power).</summary>
        public MeleeType CurrentWeaponType => _meleeType;

        /// <summary>Whether Kirby has an equipped melee weapon via copy power.</summary>
        public bool HasWeapon => _meleeType != MeleeType.Fist;

        #endregion

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Player == null || Extension.IsDead) return;

            // Update melee type from current power
            _meleeType = ResolveMeleeType(Extension.CurrentPower);

            // Combo timer decay
            if (_comboTimer > 0f)
            {
                _comboTimer -= Engine.DeltaTime;
                if (_comboTimer <= 0f)
                    _comboStep = 0;
            }

            // Active attack tick
            if (_hitboxActive)
            {
                _attackTimer -= Engine.DeltaTime;
                if (_attackTimer <= 0f)
                {
                    DeactivateHitbox();
                    FinishExecution();
                }
                else
                {
                    UpdateHitboxPosition();
                    CheckHits();
                }
            }

            HandleInput();
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            if (IsExecuting) return; // Can't attack while mid-swing

            bool attackPressed = Settings.IsKeyPressed("Attack");
            if (!attackPressed) return;

            // Determine direction
            int aimY = (int)Input.MoveY.Value;
            int aimX = (int)Input.MoveX.Value;

            if (aimY < 0)
                _attackDir = MeleeDirection.Up;
            else if (aimY > 0 && !Player.OnGround())
                _attackDir = MeleeDirection.Down;
            else
                _attackDir = MeleeDirection.Forward;

            // Only use melee if no range power is active, or if range ability isn't overriding attack
            var rangeAbility = Extension.Range;
            if (rangeAbility != null && rangeAbility.HasRangeWeapon && _attackDir == MeleeDirection.Forward)
            {
                // Let range ability handle forward attacks when it has a range weapon
                return;
            }

            TryActivate();
        }

        #endregion

        #region Activation

        protected override bool CanActivate()
        {
            // Inhale takes priority — can't punch while inhaling
            return !(Extension.Inhale?.IsInhaling ?? false);
        }

        protected override void OnActivate()
        {
            // Advance combo
            _comboStep = Math.Min(_comboStep + 1, MAX_COMBO);
            _comboTimer = COMBO_WINDOW;
            _attackTimer = ATTACK_DURATION;

            // Activate hitbox
            ActivateHitbox();

            // Sound
            if (_comboStep >= MAX_COMBO)
                PlaySfx(SFX_COMBO_FINISH);
            else if (HasWeapon)
                PlaySfx(SFX_WEAPON);
            else
                PlaySfx(SFX_PUNCH);

            // Small screen shake on combo finisher
            if (_comboStep >= MAX_COMBO && Level != null)
            {
                Level.DirectionalShake(FacingDirection, 0.04f);
            }
        }

        protected override void OnCancel()
        {
            DeactivateHitbox();
            _comboStep = 0;
        }

        #endregion

        #region Hitbox

        private void ActivateHitbox()
        {
            _hitboxActive = true;

            float range = HasWeapon ? WEAPON_RANGE : FIST_RANGE;
            float height = HasWeapon ? WEAPON_HEIGHT : FIST_HEIGHT;

            switch (_attackDir)
            {
                case MeleeDirection.Forward:
                    _attackHitbox = new Hitbox(range, height, 0f, -height / 2f);
                    break;
                case MeleeDirection.Up:
                    _attackHitbox = new Hitbox(height, range, -height / 2f, -range);
                    break;
                case MeleeDirection.Down:
                    _attackHitbox = new Hitbox(height, range, -height / 2f, 0f);
                    break;
            }

            UpdateHitboxPosition();
        }

        private void UpdateHitboxPosition()
        {
            // Hitbox is relative to player; no need to move it if we're checking manually
        }

        private void DeactivateHitbox()
        {
            _hitboxActive = false;
            _attackHitbox = null;
        }

        private void CheckHits()
        {
            if (Level == null || _attackHitbox == null) return;

            // Calculate hitbox world bounds
            Vector2 origin = Player.Position;
            float facing = Player.Facing == Facings.Left ? -1f : 1f;

            Rectangle hitArea;
            switch (_attackDir)
            {
                case MeleeDirection.Forward:
                    hitArea = new Rectangle(
                        (int)(origin.X + (facing > 0 ? 0 : -_attackHitbox.Width)),
                        (int)(origin.Y + _attackHitbox.Top),
                        (int)_attackHitbox.Width,
                        (int)_attackHitbox.Height
                    );
                    break;
                case MeleeDirection.Up:
                    hitArea = new Rectangle(
                        (int)(origin.X + _attackHitbox.Left),
                        (int)(origin.Y - _attackHitbox.Height),
                        (int)_attackHitbox.Width,
                        (int)_attackHitbox.Height
                    );
                    break;
                case MeleeDirection.Down:
                default:
                    hitArea = new Rectangle(
                        (int)(origin.X + _attackHitbox.Left),
                        (int)(origin.Y),
                        (int)_attackHitbox.Width,
                        (int)_attackHitbox.Height
                    );
                    break;
            }

            _lastHitArea = hitArea;

            int damage = GetCurrentDamage();
            Vector2 knockDir = GetKnockbackDirection();

            foreach (Entity candidate in Level.Entities)
            {
                if (candidate is not Actor entity) continue;

                if (entity == Player || entity == Extension) continue;
                if (entity is KirbyPlayerExtension || entity is KirbyMode) continue;

                // Check entity bounds against hit area
                if (entity.Collider == null) continue;
                Rectangle entityBounds = new Rectangle(
                    (int)(entity.Position.X + entity.Collider.Left),
                    (int)(entity.Position.Y + entity.Collider.Top),
                    (int)entity.Collider.Width,
                    (int)entity.Collider.Height
                );

                if (!hitArea.Intersects(entityBounds)) continue;

                // Hit!
                if (entity is KirbyActorBase kirbyActor)
                {
                    kirbyActor.OnHit(damage, knockDir * 100f);
                }

                // Particles
                Vector2 hitPoint = new Vector2(
                    Math.Clamp(entity.Position.X, hitArea.Left, hitArea.Right),
                    Math.Clamp(entity.Position.Y, hitArea.Top, hitArea.Bottom)
                );
                EmitParticles(ParticleTypes.SparkyDust, hitPoint, 4, Vector2.One * 6f);

                // Knockback
                if (entity is Actor actorEntity)
                {
                    actorEntity.Position += knockDir * KNOCKBACK_SPEED * Engine.DeltaTime;
                }
            }
        }

        #endregion

        #region Damage Calculation

        private int GetCurrentDamage()
        {
            int baseDmg = HasWeapon ? WEAPON_DAMAGE : FIST_DAMAGE;

            // Combo finisher bonus
            if (_comboStep >= MAX_COMBO)
                baseDmg = COMBO_FINISH_DAMAGE;

            // Direction bonus
            if (_attackDir == MeleeDirection.Down && !Player.OnGround())
                baseDmg += 1; // Aerial down attack bonus

            return baseDmg;
        }

        private Vector2 GetKnockbackDirection()
        {
            return _attackDir switch
            {
                MeleeDirection.Up => new Vector2(0f, -1f),
                MeleeDirection.Down => new Vector2(0f, 1f),
                _ => FacingDirection
            };
        }

        #endregion

        #region Melee Type Resolution

        private MeleeType ResolveMeleeType(KirbyMode.KirbyPowerState power)
        {
            return power switch
            {
                KirbyMode.KirbyPowerState.Sword or KirbyMode.KirbyPowerState.UltraSword => MeleeType.Sword,
                KirbyMode.KirbyPowerState.Hammer or KirbyMode.KirbyPowerState.GrandHammer => MeleeType.Hammer,
                KirbyMode.KirbyPowerState.Cutter => MeleeType.Cutter,
                KirbyMode.KirbyPowerState.Knight => MeleeType.Sword,
                // Fighter is mapped from KirbyMode if added
                _ => MeleeType.Fist
            };
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (!IsExecuting) return null;

            return _attackDir switch
            {
                MeleeDirection.Up => "melee_up",
                MeleeDirection.Down => "melee_down",
                _ => "melee"
            };
        }

        #endregion

        #region Debug Rendering

        protected override void OnRender()
        {
            // Uncomment for debug hitbox rendering:
             if (_hitboxActive && _attackHitbox != null)
             {
                 Draw.HollowRect(_lastHitArea, Color.Red);
             }
        }

        #endregion
    }
}
