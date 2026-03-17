using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;
using MaggyHelper.Entities.Kirby;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's ranged weapon ability — projectile attacks gained from copy powers.
    /// 
    /// Behaviour:
    ///   • With a range-type copy power, Attack fires a projectile
    ///   • Each power type has unique projectile properties:
    ///       Fire → fireball (short range, high damage, gravity arc)
    ///       Ice → ice shard (medium range, freezes on hit)
    ///       Spark → lightning bolt (instant, short range)
    ///       Beam → whip beam (medium range, arc pattern)
    ///       Bomb → thrown bomb (arc, explodes on timer)
    ///       Water → water stream (continuous while held)
    ///       Archer → charged arrow (hold to charge, long range)
    ///       Ranger → energy shot (long range, fast, low damage)
    ///       Painter → paint glob (medium range, area effect)
    ///   • Projectiles use the existing KirbyProjectile system
    ///   • Aiming follows 8-directional aim input
    ///   • Some powers support charge-and-release
    /// </summary>
    public class KirbyRangeAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "range";
        public override string DisplayName => "Range Attack";
        public override float MaxCooldown => GetCooldownForPower();

        #region Constants

        private const string SFX_FIRE = "event:/desolozantas/char/kirby/knight_attack";
        private const string SFX_CHARGE = "event:/desolozantas/char/kirby/knight_charge";
        private const string SFX_RELEASE = "event:/desolozantas/char/kirby/knight_special";

        private const float FIRE_SPEED = 180f;
        private const float ICE_SPEED = 200f;
        private const float SPARK_RANGE = 48f;
        private const float BEAM_SPEED = 160f;
        private const float BOMB_SPEED = 120f;
        private const float WATER_SPEED = 220f;
        private const float ARCHER_SPEED_MIN = 150f;
        private const float ARCHER_SPEED_MAX = 320f;
        private const float RANGER_SPEED = 300f;
        private const float PAINTER_SPEED = 140f;

        private const float CHARGE_TIME_MAX = 1.5f;

        #endregion

        #region Enums

        public enum RangeType
        {
            None,
            Fire,
            Ice,
            Spark,
            Beam,
            Bomb,
            Water,
            Archer,
            Ranger,
            Painter,
            Mirror,
            Light,
            Drill
        }

        #endregion

        #region Fields

        private RangeType _rangeType;
        private float _chargeTimer;
        private bool _isCharging;
        private float _animTimer;
        private float _streamTimer;      // For continuous attacks (water, spark)
        private bool _isContinuous;

        #endregion

        #region Properties

        /// <summary>Whether the current copy power provides a range weapon.</summary>
        public bool HasRangeWeapon => _rangeType != RangeType.None;

        /// <summary>Current ranged weapon type.</summary>
        public RangeType CurrentRangeType => _rangeType;

        /// <summary>Whether currently charging a shot.</summary>
        public bool IsCharging => _isCharging;

        /// <summary>Charge level (0-1).</summary>
        public float ChargeLevel => _chargeTimer / CHARGE_TIME_MAX;

        #endregion

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Player == null || Extension.IsDead) return;

            // Resolve range type from current power
            _rangeType = ResolveRangeType(Extension.CurrentPower);

            // Anim timer
            if (_animTimer > 0f)
                _animTimer -= Engine.DeltaTime;
            else if (IsExecuting && !_isCharging && !_isContinuous)
                FinishExecution();

            HandleInput();
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            if (!HasRangeWeapon) return;
            if (Extension.Inhale?.IsInhaling ?? false) return;

            bool attackPressed = Settings.IsKeyPressed("Attack");
            bool attackHeld = Settings.IsKeyCheck("Attack");

            // Charge-based powers (Archer)
            if (IsChargePower())
            {
                if (attackHeld && !_isCharging && Cooldown <= 0f)
                {
                    StartCharge();
                }
                else if (_isCharging)
                {
                    _chargeTimer = Math.Min(_chargeTimer + Engine.DeltaTime, CHARGE_TIME_MAX);

                    if (!attackHeld)
                    {
                        ReleaseCharge();
                    }
                }
                return;
            }

            // Continuous powers (Water, Spark)
            if (IsContinuousPower())
            {
                if (attackHeld && Cooldown <= 0f)
                {
                    if (!_isContinuous)
                        StartContinuous();

                    _streamTimer += Engine.DeltaTime;
                    if (_streamTimer >= 0.08f)
                    {
                        FireProjectile(1f);
                        _streamTimer = 0f;
                    }
                }
                else if (_isContinuous)
                {
                    StopContinuous();
                }
                return;
            }

            // Standard fire-and-forget powers
            if (attackPressed && Cooldown <= 0f)
            {
                TryActivate();
            }
        }

        #endregion

        #region Activation

        protected override bool CanActivate()
        {
            return HasRangeWeapon && !(Extension.Inhale?.IsInhaling ?? false);
        }

        protected override void OnActivate()
        {
            FireProjectile(1f);
            PlaySfx(SFX_FIRE);
            _animTimer = 0.15f;
        }

        #endregion

        #region Charge

        private void StartCharge()
        {
            _isCharging = true;
            IsExecuting = true;
            _chargeTimer = 0f;
            PlaySfx(SFX_CHARGE);
        }

        private void ReleaseCharge()
        {
            float power = ChargeLevel;
            _isCharging = false;
            _chargeTimer = 0f;

            FireProjectile(power);
            PlaySfx(SFX_RELEASE);
            _animTimer = 0.2f;
            StartCooldown();
        }

        #endregion

        #region Continuous

        private void StartContinuous()
        {
            _isContinuous = true;
            IsExecuting = true;
            _streamTimer = 0f;
        }

        private void StopContinuous()
        {
            _isContinuous = false;
            _streamTimer = 0f;
            FinishExecution();
        }

        #endregion

        #region Projectile Spawning

        private void FireProjectile(float chargePower)
        {
            if (Level == null) return;

            Vector2 aim = AimDirection;
            Vector2 spawnPos = Player.Position + aim * 10f;

            switch (_rangeType)
            {
                case RangeType.Fire:
                    SpawnKirbyProjectile(spawnPos, aim, FIRE_SPEED, KirbyProjectile.ProjectileType.Fire, 2);
                    break;

                case RangeType.Ice:
                    SpawnKirbyProjectile(spawnPos, aim, ICE_SPEED, KirbyProjectile.ProjectileType.Ice, 2);
                    break;

                case RangeType.Spark:
                    // Lightning is instant in a short range — spawn with short lifetime
                    SpawnKirbyProjectile(spawnPos, aim, SPARK_RANGE * 4f, KirbyProjectile.ProjectileType.Lightning, 3);
                    break;

                case RangeType.Beam:
                    SpawnKirbyProjectile(spawnPos, aim, BEAM_SPEED, KirbyProjectile.ProjectileType.Beam, 1);
                    break;

                case RangeType.Bomb:
                    var bomb = SpawnKirbyProjectile(spawnPos, aim, BOMB_SPEED, KirbyProjectile.ProjectileType.Bomb, 3);
                    break;

                case RangeType.Water:
                    SpawnKirbyProjectile(spawnPos, aim, WATER_SPEED, KirbyProjectile.ProjectileType.Star, 1);
                    break;

                case RangeType.Archer:
                    float speed = MathHelper.Lerp(ARCHER_SPEED_MIN, ARCHER_SPEED_MAX, chargePower);
                    int dmg = chargePower > 0.8f ? 3 : (chargePower > 0.4f ? 2 : 1);
                    SpawnKirbyProjectile(spawnPos, aim, speed, KirbyProjectile.ProjectileType.Star, dmg);
                    break;

                case RangeType.Ranger:
                    SpawnKirbyProjectile(spawnPos, aim, RANGER_SPEED, KirbyProjectile.ProjectileType.Star, 1);
                    break;

                case RangeType.Painter:
                    SpawnKirbyProjectile(spawnPos, aim, PAINTER_SPEED, KirbyProjectile.ProjectileType.Star, 2);
                    break;

                case RangeType.Mirror:
                    SpawnKirbyProjectile(spawnPos, aim, BEAM_SPEED, KirbyProjectile.ProjectileType.Mirror, 1);
                    break;

                case RangeType.Light:
                    SpawnKirbyProjectile(spawnPos, aim, RANGER_SPEED * 1.2f, KirbyProjectile.ProjectileType.Star, 2);
                    break;

                case RangeType.Drill:
                    SpawnKirbyProjectile(spawnPos, aim, BEAM_SPEED, KirbyProjectile.ProjectileType.Star, 2);
                    break;
            }

            // Muzzle particles
            EmitParticles(ParticleTypes.SparkyDust, spawnPos, 3, Vector2.One * 3f);
        }

        private KirbyProjectile SpawnKirbyProjectile(Vector2 pos, Vector2 dir, float speed, KirbyProjectile.ProjectileType type, int damage)
        {
            var proj = new KirbyProjectile(pos, dir.SafeNormalize() * speed, type, Player);
            proj.Damage = damage;
            Level.Add(proj);
            return proj;
        }

        #endregion

        #region Power Resolution

        private RangeType ResolveRangeType(KirbyMode.KirbyPowerState power)
        {
            return power switch
            {
                KirbyMode.KirbyPowerState.Fire or KirbyMode.KirbyPowerState.InfernoLight => RangeType.Fire,
                KirbyMode.KirbyPowerState.Ice or KirbyMode.KirbyPowerState.FrostMind => RangeType.Ice,
                KirbyMode.KirbyPowerState.Spark => RangeType.Spark,
                KirbyMode.KirbyPowerState.Beam => RangeType.Beam,
                KirbyMode.KirbyPowerState.Bomb => RangeType.Bomb,
                KirbyMode.KirbyPowerState.Water => RangeType.Water,
                KirbyMode.KirbyPowerState.Archer => RangeType.Archer,
                KirbyMode.KirbyPowerState.Ranger or KirbyMode.KirbyPowerState.MechanizeRanger => RangeType.Ranger,
                KirbyMode.KirbyPowerState.Painter => RangeType.Painter,
                KirbyMode.KirbyPowerState.Mirror => RangeType.Mirror,
                KirbyMode.KirbyPowerState.Light => RangeType.Light,
                KirbyMode.KirbyPowerState.Drill => RangeType.Drill,
                _ => RangeType.None
            };
        }

        private bool IsChargePower()
        {
            return _rangeType is RangeType.Archer;
        }

        private bool IsContinuousPower()
        {
            return _rangeType is RangeType.Water or RangeType.Spark or RangeType.Fire;
        }

        private float GetCooldownForPower()
        {
            return _rangeType switch
            {
                RangeType.Fire => 0.25f,
                RangeType.Ice => 0.3f,
                RangeType.Spark => 0.1f,
                RangeType.Beam => 0.2f,
                RangeType.Bomb => 0.6f,
                RangeType.Water => 0.05f,
                RangeType.Archer => 0.3f,
                RangeType.Ranger => 0.15f,
                RangeType.Painter => 0.35f,
                RangeType.Mirror => 0.25f,
                RangeType.Light => 0.2f,
                RangeType.Drill => 0.2f,
                _ => 0.2f
            };
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (!IsExecuting) return null;
            return "range";
        }

        #endregion

        #region Cancel

        protected override void OnCancel()
        {
            _isCharging = false;
            _isContinuous = false;
            _chargeTimer = 0f;
            _streamTimer = 0f;
            _animTimer = 0f;
        }

        #endregion
    }
}
