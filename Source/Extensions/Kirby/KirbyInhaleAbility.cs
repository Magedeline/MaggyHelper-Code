using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MaggyHelper.Extensions.Core;
using System;
using System.Collections.Generic;
using MaggyHelper.Entities.Kirby;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's signature ability — inhale enemies and objects.
    ///
    /// Behaviour:
    ///   • Press/hold Inhale to open mouth and create a pull cone
    ///   • Entities in cone are pulled toward Kirby's mouth
    ///   • When close enough, they are swallowed
    ///   • Swallowing food heals; swallowing enemies optionally copies power
    ///   • If no copy power gained, Kirby gets "mouthful" state (can spit)
    ///
    /// Real-Player.cs integration (PlayerPatchCore):
    ///   • StartInhale() transitions the real Player.StateMachine to StKirbyInhale.
    ///   • StopInhale() returns the state machine to StNormal.
    ///   • KirbyPlayerStatePatch.KirbyInhaleUpdate() handles restricted walk-only movement
    ///     while inhaling, aligned with NormalUpdate's horizontal movement structure.
    ///   • [UPSTREAM-REF] https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
    /// </summary>
    public class KirbyInhaleAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "inhale";
        public override string DisplayName => "Inhale";

        private const string SFX_INHALE = "event:/desolozantas/char/kirby/inhale_start";
        private const string SFX_INHALE_END = "event:/desolozantas/char/kirby/spit";

        private float _inhaleTimer;
        private float _mouthOpenTimer;

        /// <summary>Whether inhale is currently active (pulling entities).</summary>
        public bool IsInhaling { get; private set; }

        /// <summary>Whether Kirby has swallowed something (can spit).</summary>
        public bool HasMouthful { get; set; }

        /// <summary>Entities that have been inhaled this cycle.</summary>
        public List<KirbyActorBase> InhaledEntities { get; } = new();

        /// <summary>The power that was last copied from a swallowed entity.</summary>
        public KirbyMode.KirbyPowerState PendingCopyPower { get; private set; } = KirbyMode.KirbyPowerState.None;

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Player == null || Extension.IsDead || !Settings.KirbyPlayerEnabled)
            {
                if (IsInhaling)
                    StopInhale();
                return;
            }

            UpdateMouthTimer();
            HandleInput();
            UpdateInhalePull();
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            if (Extension?.PrecisionCombat?.CombatModeActive == true)
                return;

            bool held = Settings.IsKeyCheck("Inhale");
            bool pressed = Settings.IsKeyPressed("Inhale");
            bool inhaleInput = Settings.InhaleHoldMode ? held : pressed;

            // Start inhale
            if (inhaleInput && !IsInhaling && !HasMouthful && _mouthOpenTimer <= 0f)
                StartInhale();

            // Stop inhale (hold mode: release cancels)
            if (!held && IsInhaling && Settings.InhaleHoldMode)
                StopInhale();

            // Auto-stop on timer (tap mode)
            if (IsInhaling && !Settings.InhaleHoldMode)
            {
                _inhaleTimer -= Engine.DeltaTime;
                if (_inhaleTimer <= 0f)
                    StopInhale();
            }
        }

        #endregion

        #region Inhale Core

        private void StartInhale()
        {
            IsInhaling = true;
            IsExecuting = true;
            _inhaleTimer = Settings.InhaleDuration;
            InhaledEntities.Clear();
            PendingCopyPower = KirbyMode.KirbyPowerState.None;
            PlaySfx(SFX_INHALE);

            // [MOD-SPECIFIC] Transition real Player.StateMachine to StKirbyInhale.
            // KirbyPlayerStatePatch.KirbyInhaleUpdate() will handle walk-only movement
            // aligned with Player.NormalUpdate's horizontal movement structure.
            // [UPSTREAM-REF] Player.cs NormalUpdate returns StClimb / StDash on transitions.
            if (PlayerCharacterStates.StKirbyInhale >= 0
                && Player?.StateMachine.State == CelestePlayer.StNormal)
            {
                Player.StateMachine.State = PlayerCharacterStates.StKirbyInhale;
            }
        }

        public void StopInhale()
        {
            if (!IsInhaling) return;
            IsInhaling = false;
            IsExecuting = false;
            _mouthOpenTimer = Settings.MouthOpenTime;
            _inhaleTimer = 0f;
            PlaySfx(SFX_INHALE_END);

            // [MOD-SPECIFIC] Return state machine to StNormal when inhale ends.
            if (PlayerCharacterStates.StKirbyInhale >= 0
                && Player?.StateMachine.State == PlayerCharacterStates.StKirbyInhale)
            {
                Player.StateMachine.State = CelestePlayer.StNormal;
            }
        }

        private void UpdateMouthTimer()
        {
            if (_mouthOpenTimer > 0f)
                _mouthOpenTimer = Math.Max(0f, _mouthOpenTimer - Engine.DeltaTime);
        }

        #endregion

        #region Pull Logic

        private void UpdateInhalePull()
        {
            if (!IsInhaling || Level == null) return;

            Vector2 facingDir = FacingDirection;
            Vector2 mouthPos = Player.Position + new Vector2(facingDir.X * Settings.InhaleMouthOffset, -6f);
            float rangeSq = Settings.InhaleRange * Settings.InhaleRange;

            EmitInhaleParticles(mouthPos);

            Entity swallowTarget = null;

            foreach (Entity candidate in Level.Entities)
            {
                if (candidate is not Actor entity) continue;

                if (entity == Extension || entity == Player) continue;
                if (!IsInhalable(entity)) continue;

                Vector2 toEntity = entity.Position - mouthPos;
                if (toEntity.LengthSquared() > rangeSq) continue;

                float dirDot = Vector2.Dot(toEntity.SafeNormalize(), facingDir);
                if (dirDot < Settings.InhaleConeDot) continue;

                // Pull entity toward mouth
                entity.Position = Vector2.Lerp(
                    entity.Position, mouthPos,
                    Settings.InhalePullLerp * Engine.DeltaTime
                );
                entity.Position -= facingDir * Settings.InhalePullSpeed * Engine.DeltaTime;

                // Swallow if close enough
                if (Vector2.Distance(entity.Position, mouthPos) <= Settings.InhaleSwallowDistance)
                {
                    swallowTarget = entity;
                    break;
                }
            }

            if (swallowTarget != null)
                SwallowEntity(swallowTarget);
        }

        private void EmitInhaleParticles(Vector2 mouthPos)
        {
            Level?.ParticlesFG?.Emit(ParticleTypes.Dust, 1, mouthPos, Vector2.One * 4f);
        }

        #endregion

        #region Swallow

        private bool IsInhalable(Entity entity)
        {
            if (entity == null || !entity.Active) return false;
            if (entity is KirbyMode) return false;
            if (entity is KirbyPlayerExtension) return false;

            if (entity.Get<Holdable>() != null) return true;

            string typeName = entity.GetType().Name;
            if (typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase)) return true;
            if (typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase)) return Settings.AllowInhaleBosses;
            if (typeName.Contains("Seeker", StringComparison.OrdinalIgnoreCase)) return true;
            if (typeName.Contains("Oshiro", StringComparison.OrdinalIgnoreCase)) return true;
            if (typeName.Contains("Theo", StringComparison.OrdinalIgnoreCase)) return Settings.AllowInhaleCarryables;
            if (typeName.Contains("Crystal", StringComparison.OrdinalIgnoreCase)) return Settings.AllowInhaleCarryables;

            return entity is KirbyFood;
        }

        private void SwallowEntity(Entity entity)
        {
            if (entity == null) return;

            // Track inhaled actor
            if (entity is KirbyActorBase actorBase && !InhaledEntities.Contains(actorBase))
                InhaledEntities.Add(actorBase);

            // Food → heal
            if (entity is KirbyFood)
            {
                Extension.Heal(1);
                entity.RemoveSelf();
                return;
            }

            // Try to copy power
            PendingCopyPower = ResolveCopyPower(entity);

            if (Settings.PowerCopyEnabled && PendingCopyPower != KirbyMode.KirbyPowerState.None)
            {
                // Automatic copy: set power immediately
                Extension.SetPowerState(PendingCopyPower);
                HasMouthful = false;
            }
            else
            {
                // No copy — store mouthful for spit
                HasMouthful = true;
            }

            entity.RemoveSelf();
            StopInhale();
        }

        /// <summary>
        /// Determine which power to copy from the swallowed entity.
        /// </summary>
        private KirbyMode.KirbyPowerState ResolveCopyPower(Entity entity)
        {
            // Check KirbyActorBase.PowerType first
            if (entity is KirbyActorBase actor)
            {
                return actor.CurrentPower switch
                {
                    KirbyActorBase.PowerType.Fire => KirbyMode.KirbyPowerState.Fire,
                    KirbyActorBase.PowerType.Ice => KirbyMode.KirbyPowerState.Ice,
                    KirbyActorBase.PowerType.Spark => KirbyMode.KirbyPowerState.Spark,
                    KirbyActorBase.PowerType.Stone => KirbyMode.KirbyPowerState.Stone,
                    KirbyActorBase.PowerType.Sword => KirbyMode.KirbyPowerState.Sword,
                    KirbyActorBase.PowerType.Beam => KirbyMode.KirbyPowerState.Beam,
                    KirbyActorBase.PowerType.Bomb => KirbyMode.KirbyPowerState.Bomb,
                    KirbyActorBase.PowerType.Cutter => KirbyMode.KirbyPowerState.Cutter,
                    KirbyActorBase.PowerType.Wheel => KirbyMode.KirbyPowerState.Wheel,
                    KirbyActorBase.PowerType.Needle => KirbyMode.KirbyPowerState.None, // KirbyMode.KirbyPowerState has no Needle entry
                    KirbyActorBase.PowerType.Mirror => KirbyMode.KirbyPowerState.Mirror,
                    _ => KirbyMode.KirbyPowerState.None
                };
            }

            // Fallback: name-based detection
            string name = entity.GetType().Name;
            if (name.Contains("Fire", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Fire;
            if (name.Contains("Ice", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Ice;
            if (name.Contains("Spark", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Spark;
            if (name.Contains("Stone", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Stone;
            if (name.Contains("Sword", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Sword;
            if (name.Contains("Beam", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Beam;
            if (name.Contains("Water", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Water;
            if (name.Contains("Hammer", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Hammer;
            if (name.Contains("Bomb", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Bomb;
            if (name.Contains("Cutter", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Cutter;
            if (name.Contains("Knight", StringComparison.OrdinalIgnoreCase)) return KirbyMode.KirbyPowerState.Knight;

            return KirbyMode.KirbyPowerState.None;
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (IsInhaling) return "inhale";
            if (HasMouthful) return "mouthful";
            return null;
        }

        #endregion

        #region Public API

        /// <summary>Clear mouthful state (called after spit).</summary>
        public void ClearMouthful()
        {
            HasMouthful = false;
            InhaledEntities.Clear();
            PendingCopyPower = KirbyMode.KirbyPowerState.None;
        }

        #endregion
    }
}
