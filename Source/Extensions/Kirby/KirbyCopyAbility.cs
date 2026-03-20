using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;
using System.Collections.Generic;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's copy ability system — manages the lifecycle of powers gained from enemies.
    /// 
    /// Behaviour:
    ///   • When a power is gained (via inhale), this ability activates a visual transformation
    ///   • Provides a HUD icon showing the current power
    ///   • Handles power duration timers (if configured)
    ///   • Power-specific passive effects (Stone → take less knockback, Leaf → slow fall, etc.)
    ///   • CyclePower to swap between stored abilities (if TripleSwap power)
    ///   • DropPower to discard current copy ability
    ///   • Super abilities (InfernoLight, GrandHammer, etc.) have limited duration
    /// 
    /// Power Categories:
    ///   MELEE:    Sword, Hammer, Cutter, Knight, Fighter
    ///   RANGE:    Fire, Ice, Spark, Beam, Bomb, Water, Archer, Ranger, Painter, Mirror, Light, Drill
    ///   PASSIVE:  Stone, Leaf, Wheel, Esp, Bell, Umbrella, Mini, Phase
    ///   UTILITY:  Mike, Crash, Cook (single-use room-wide effects)
    ///   SUPER:    InfernoLight, GrandHammer, MechanizeRanger, FrostMind, UltraSword
    /// </summary>
    public class KirbyCopyAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "copy";
        public override string DisplayName => "Copy Ability";

        #region Constants

        private const string SFX_COPY = "event:/desolozantas/char/kirby/transform_in";
        private const string SFX_LOSE = "event:/desolozantas/char/kirby/transform_out";
        private const string SFX_SUPER = "event:/desolozantas/char/kirby/kirby_knight/backflip";
        private const string SFX_UTILITY = "event:/desolozantas/char/kirby/kirby_knight/spin";

        private const float SUPER_ABILITY_DURATION = 20f;
        private const float COPY_TRANSFORM_ANIM_TIME = 0.4f;

        #endregion

        #region Enums

        /// <summary>Category a power falls into — drives which sub-ability handles the attack.</summary>
        public enum PowerCategory
        {
            None,
            Melee,
            Range,
            Passive,
            Utility,
            Super
        }

        #endregion

        #region Fields

        private KirbyMode.KirbyPowerState _activePower = KirbyMode.KirbyPowerState.None;
        private float _powerDurationTimer;
        private float _transformAnimTimer;
        private bool _isTransforming;

        // TripleSwap stored powers
        private readonly List<KirbyMode.KirbyPowerState> _storedPowers = new(3);
        private int _storedIndex;

        // Passive effect state
        private bool _stoneActive;
        private float _wheelSpeed;

        #endregion

        #region Properties

        /// <summary>Currently active power.</summary>
        public KirbyMode.KirbyPowerState ActivePower => _activePower;

        /// <summary>Category of the active power.</summary>
        public PowerCategory Category => ClassifyPower(_activePower);

        /// <summary>Whether a super ability is active (limited duration).</summary>
        public bool IsSuperActive => Category == PowerCategory.Super;

        /// <summary>Remaining duration for timed powers (seconds).</summary>
        public float PowerTimeRemaining => _powerDurationTimer;

        /// <summary>Whether in mid-transformation animation.</summary>
        public bool IsTransforming => _isTransforming;

        /// <summary>Stored powers for TripleSwap.</summary>
        public IReadOnlyList<KirbyMode.KirbyPowerState> StoredPowers => _storedPowers;

        #endregion

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Player == null || Extension.IsDead) return;

            // Transform animation
            if (_isTransforming)
            {
                _transformAnimTimer -= Engine.DeltaTime;
                if (_transformAnimTimer <= 0f)
                    _isTransforming = false;
            }

            // Power duration
            UpdatePowerDuration();

            // Passive effects
            UpdatePassiveEffects();

            // Input: cycle / drop
            HandleInput();
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            // Cycle power (TripleSwap)
            if (Settings.IsKeyPressed("CyclePower") && _activePower == KirbyMode.KirbyPowerState.TripleSwap)
            {
                CyclePower();
            }
        }

        #endregion

        #region Power Change

        /// <summary>
        /// Called by KirbyPlayerExtension.SetPowerState when the power changes.
        /// </summary>
        public void OnPowerChanged(KirbyMode.KirbyPowerState previous, KirbyMode.KirbyPowerState next)
        {
            _activePower = next;

            if (next != KirbyMode.KirbyPowerState.None && next != previous)
            {
                // Play transformation
                _isTransforming = true;
                _transformAnimTimer = COPY_TRANSFORM_ANIM_TIME;
                IsExecuting = true;

                if (IsSuperAbility(next))
                {
                    PlaySfx(SFX_SUPER);
                    _powerDurationTimer = SUPER_ABILITY_DURATION;

                    // Screen flash for super abilities
                    if (Level != null)
                    {
                        Level.Flash(Color.White * 0.5f);
                        Level.Shake(0.15f);
                    }
                }
                else if (IsUtilityAbility(next))
                {
                    PlaySfx(SFX_UTILITY);
                }
                else
                {
                    PlaySfx(SFX_COPY);
                }

                // Particles
                EmitParticles(ParticleTypes.SparkyDust, Player.Position, 12, Vector2.One * 12f);
            }
            else if (next == KirbyMode.KirbyPowerState.None && previous != KirbyMode.KirbyPowerState.None)
            {
                // Lost power
                PlaySfx(SFX_LOSE);
                EmitParticles(ParticleTypes.Dust, Player.Position, 6, Vector2.One * 8f);
                IsExecuting = false;
            }

            // Handle TripleSwap storage
            if (next == KirbyMode.KirbyPowerState.TripleSwap && previous != KirbyMode.KirbyPowerState.None)
            {
                StorePower(previous);
            }

            // Reset passive state
            _stoneActive = false;
            _wheelSpeed = 0f;
        }

        #endregion

        #region Power Duration

        private void UpdatePowerDuration()
        {
            if (_activePower == KirbyMode.KirbyPowerState.None) return;

            // Super abilities have fixed duration
            if (IsSuperAbility(_activePower))
            {
                _powerDurationTimer -= Engine.DeltaTime;
                if (_powerDurationTimer <= 0f)
                {
                    Extension.SetPowerState(KirbyMode.KirbyPowerState.None);
                    return;
                }
            }

            // Configurable duration for regular powers
            if (Settings.PowerDurationSeconds > 0 && !IsSuperAbility(_activePower))
            {
                var session = MaggyHelperModule.Session;
                if (session != null)
                {
                    if (session.PowerTimeRemaining <= 0f)
                        session.PowerTimeRemaining = Settings.PowerDurationSeconds;

                    session.PowerTimeRemaining -= Engine.DeltaTime;
                    if (session.PowerTimeRemaining <= 0f)
                    {
                        Extension.SetPowerState(KirbyMode.KirbyPowerState.None);
                        session.PowerTimeRemaining = 0f;
                    }
                }
            }
        }

        #endregion

        #region Passive Effects

        private void UpdatePassiveEffects()
        {
            if (Player == null) return;

            switch (_activePower)
            {
                case KirbyMode.KirbyPowerState.Stone:
                    UpdateStonePassive();
                    break;

                case KirbyMode.KirbyPowerState.Leaf:
                    UpdateLeafPassive();
                    break;

                case KirbyMode.KirbyPowerState.Wheel:
                    UpdateWheelPassive();
                    break;

                case KirbyMode.KirbyPowerState.Esp:
                    UpdateEspPassive();
                    break;

                case KirbyMode.KirbyPowerState.Umbrella:
                    UpdateUmbrellaPassive();
                    break;

                case KirbyMode.KirbyPowerState.Mini:
                    UpdateMiniPassive();
                    break;
            }
        }

        private void UpdateStonePassive()
        {
            // Stone: activating (crouch while airborne) makes Kirby invulnerable
            // and slams downward. Press down + attack in air.
            if (!Player.OnGround() && Settings.IsKeyCheck("Attack") && Input.MoveY.Value > 0)
            {
                if (!_stoneActive)
                {
                    _stoneActive = true;
                    Player.Speed = new Vector2(0f, 300f); // Fast drop
                }
            }
            else if (Player.OnGround() && _stoneActive)
            {
                _stoneActive = false;
                // Ground pound impact
                if (Level != null)
                {
                    Level.DirectionalShake(Vector2.UnitY, 0.08f);
                    EmitParticles(ParticleTypes.SparkyDust, Player.Position, 8, Vector2.One * 12f);
                }
            }
        }

        private void UpdateLeafPassive()
        {
            // Leaf: slower fall speed always
            if (!Player.OnGround() && Player.Speed.Y > 40f)
            {
                Player.Speed = new Vector2(Player.Speed.X, Math.Min(Player.Speed.Y, 40f));
            }
        }

        private void UpdateWheelPassive()
        {
            // Wheel: hold dash to accelerate to high speed on ground
            if (Player.OnGround() && Settings.IsKeyCheck("Dash"))
            {
                _wheelSpeed = Math.Min(_wheelSpeed + 400f * Engine.DeltaTime, 260f);
                float dir = Player.Facing == Facings.Left ? -1f : 1f;
                Player.Speed = new Vector2(dir * _wheelSpeed, Player.Speed.Y);
            }
            else
            {
                _wheelSpeed = Math.Max(_wheelSpeed - 200f * Engine.DeltaTime, 0f);
            }
        }

        private void UpdateEspPassive()
        {
            // ESP: slight telekinetic float — reduces gravity by 30%
            if (!Player.OnGround() && Player.Speed.Y > 0f)
            {
                Player.Speed = new Vector2(Player.Speed.X, Player.Speed.Y * 0.97f);
            }
        }

        private void UpdateUmbrellaPassive()
        {
            // Umbrella: very slow fall (like glider)
            if (!Player.OnGround() && Player.Speed.Y > 30f)
            {
                Player.Speed = new Vector2(Player.Speed.X, Math.Min(Player.Speed.Y, 30f));
            }
        }

        private void UpdateMiniPassive()
        {
            // Mini: smaller hitbox, higher jumps, less damage
            // Note: hitbox changes should ideally be done via the extension,
            // but we can modify jump speed here.
        }

        #endregion

        #region Utility Powers

        /// <summary>
        /// Activate a utility power (single-use room-wide effect).
        /// Call from external trigger or button press.
        /// </summary>
        public void ActivateUtilityPower()
        {
            if (Level == null) return;

            switch (_activePower)
            {
                case KirbyMode.KirbyPowerState.Mike:
                    // Damage all enemies in the room
                    DamageAllEnemies(5);
                    Level.Shake(0.3f);
                    Level.Flash(Color.White * 0.3f);
                    break;

                case KirbyMode.KirbyPowerState.Crash:
                    // Massive explosion
                    DamageAllEnemies(10);
                    Level.Shake(0.5f);
                    Level.Flash(Color.OrangeRed * 0.5f);
                    break;

                case KirbyMode.KirbyPowerState.Cook:
                    // Turn all enemies into food (heal)
                    ConvertEnemiesToHealth();
                    break;
            }

            // Utility powers are consumed after use
            Extension.SetPowerState(KirbyMode.KirbyPowerState.None);
        }

        private void DamageAllEnemies(int damage)
        {
            if (Level == null) return;

            foreach (Entity candidate in Level.Entities)
            {
                if (candidate is not Actor entity) continue;

                if (entity == Player || entity == Extension) continue;
                if (entity is Entities.Kirby.KirbyActorBase kirbyActor)
                {
                    kirbyActor.OnHit(damage, (entity.Position - Player.Position).SafeNormalize() * 200f);
                }
            }
        }

        private void ConvertEnemiesToHealth()
        {
            if (Level == null) return;
            int healed = 0;

            foreach (Entity candidate in Level.Entities)
            {
                if (candidate is not Actor entity) continue;

                if (entity == Player || entity == Extension) continue;
                if (entity is Entities.Kirby.KirbyActorBase)
                {
                    entity.RemoveSelf();
                    healed++;
                }
            }

            Extension.Heal(Math.Max(1, healed));
        }

        #endregion

        #region TripleSwap

        private void StorePower(KirbyMode.KirbyPowerState power)
        {
            if (power == KirbyMode.KirbyPowerState.None || power == KirbyMode.KirbyPowerState.TripleSwap)
                return;

            if (_storedPowers.Count >= 3)
                _storedPowers.RemoveAt(0);

            _storedPowers.Add(power);
            _storedIndex = _storedPowers.Count - 1;
        }

        private void CyclePower()
        {
            if (_storedPowers.Count == 0) return;

            _storedIndex = (_storedIndex + 1) % _storedPowers.Count;
            var power = _storedPowers[_storedIndex];

            // Set power without re-triggering TripleSwap storage
            _activePower = power;
            Extension.SetPowerState(power);
        }

        #endregion

        #region Classification

        /// <summary>Classify a power into its category.</summary>
        public static PowerCategory ClassifyPower(KirbyMode.KirbyPowerState power)
        {
            return power switch
            {
                KirbyMode.KirbyPowerState.None => PowerCategory.None,

                // Melee
                KirbyMode.KirbyPowerState.Sword or
                KirbyMode.KirbyPowerState.Hammer or
                KirbyMode.KirbyPowerState.Cutter or
                KirbyMode.KirbyPowerState.Knight => PowerCategory.Melee,

                // Range
                KirbyMode.KirbyPowerState.Fire or
                KirbyMode.KirbyPowerState.Ice or
                KirbyMode.KirbyPowerState.Spark or
                KirbyMode.KirbyPowerState.Beam or
                KirbyMode.KirbyPowerState.Bomb or
                KirbyMode.KirbyPowerState.Water or
                KirbyMode.KirbyPowerState.Archer or
                KirbyMode.KirbyPowerState.Ranger or
                KirbyMode.KirbyPowerState.Painter or
                KirbyMode.KirbyPowerState.Mirror or
                KirbyMode.KirbyPowerState.Light or
                KirbyMode.KirbyPowerState.Drill => PowerCategory.Range,

                // Passive
                KirbyMode.KirbyPowerState.Stone or
                KirbyMode.KirbyPowerState.Leaf or
                KirbyMode.KirbyPowerState.Wheel or
                KirbyMode.KirbyPowerState.Esp or
                KirbyMode.KirbyPowerState.Bell or
                KirbyMode.KirbyPowerState.Umbrella or
                KirbyMode.KirbyPowerState.Mini or
                KirbyMode.KirbyPowerState.Phase or
                KirbyMode.KirbyPowerState.Recycler or
                KirbyMode.KirbyPowerState.TripleSwap or
                KirbyMode.KirbyPowerState.TimeCrash => PowerCategory.Passive,

                // Utility (single-use)
                KirbyMode.KirbyPowerState.Mike or
                KirbyMode.KirbyPowerState.Crash or
                KirbyMode.KirbyPowerState.Cook => PowerCategory.Utility,

                // Super (limited duration enhanced versions)
                KirbyMode.KirbyPowerState.InfernoLight or
                KirbyMode.KirbyPowerState.GrandHammer or
                KirbyMode.KirbyPowerState.MechanizeRanger or
                KirbyMode.KirbyPowerState.FrostMind or
                KirbyMode.KirbyPowerState.UltraSword => PowerCategory.Super,

                _ => PowerCategory.None
            };
        }

        /// <summary>Whether the given power is a super ability.</summary>
        public static bool IsSuperAbility(KirbyMode.KirbyPowerState power)
        {
            return ClassifyPower(power) == PowerCategory.Super;
        }

        /// <summary>Whether the given power is a utility ability.</summary>
        public static bool IsUtilityAbility(KirbyMode.KirbyPowerState power)
        {
            return ClassifyPower(power) == PowerCategory.Utility;
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (_isTransforming) return "copy";
            return null;
        }

        #endregion

        #region Color

        /// <summary>Get the thematic color for a copy power (for HUD/particles).</summary>
        public static Color GetPowerColor(KirbyMode.KirbyPowerState power)
        {
            return power switch
            {
                KirbyMode.KirbyPowerState.Fire or KirbyMode.KirbyPowerState.InfernoLight => Color.OrangeRed,
                KirbyMode.KirbyPowerState.Ice or KirbyMode.KirbyPowerState.FrostMind => Color.LightBlue,
                KirbyMode.KirbyPowerState.Spark => Color.Yellow,
                KirbyMode.KirbyPowerState.Stone => Color.Gray,
                KirbyMode.KirbyPowerState.Sword or KirbyMode.KirbyPowerState.UltraSword => Color.LimeGreen,
                KirbyMode.KirbyPowerState.Archer => Color.Brown,
                KirbyMode.KirbyPowerState.Leaf => Color.Green,
                KirbyMode.KirbyPowerState.Water => Color.DodgerBlue,
                KirbyMode.KirbyPowerState.Esp => Color.MediumPurple,
                KirbyMode.KirbyPowerState.Hammer or KirbyMode.KirbyPowerState.GrandHammer => Color.SaddleBrown,
                KirbyMode.KirbyPowerState.Ranger or KirbyMode.KirbyPowerState.MechanizeRanger => Color.DarkCyan,
                KirbyMode.KirbyPowerState.Mike => Color.Orange,
                KirbyMode.KirbyPowerState.Crash => Color.Red,
                KirbyMode.KirbyPowerState.Bomb => Color.DarkOrange,
                KirbyMode.KirbyPowerState.Cutter => Color.Silver,
                KirbyMode.KirbyPowerState.Painter => Color.HotPink,
                KirbyMode.KirbyPowerState.Cook => Color.White,
                KirbyMode.KirbyPowerState.Bell => Color.Gold,
                KirbyMode.KirbyPowerState.Light => Color.LightYellow,
                KirbyMode.KirbyPowerState.Drill => Color.DarkGoldenrod,
                KirbyMode.KirbyPowerState.Beam => Color.Goldenrod,
                KirbyMode.KirbyPowerState.Wheel => Color.DarkRed,
                KirbyMode.KirbyPowerState.Phase => Color.MediumSlateBlue,
                KirbyMode.KirbyPowerState.TripleSwap => Color.Magenta,
                KirbyMode.KirbyPowerState.TimeCrash => Color.DarkViolet,
                KirbyMode.KirbyPowerState.Umbrella => Color.Coral,
                KirbyMode.KirbyPowerState.Mirror => Color.Purple,
                KirbyMode.KirbyPowerState.Recycler => Color.ForestGreen,
                KirbyMode.KirbyPowerState.Mini => Color.Pink,
                KirbyMode.KirbyPowerState.Knight => Color.Gold,
                _ => Color.White
            };
        }

        #endregion
    }
}

