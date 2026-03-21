using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's hover / float ability.
    /// 
    /// Behaviour:
    ///   • While airborne, hold Hover to slow descent and use flap jumps
    ///   • Each flap costs stamina and provides a small upward boost
    ///   • Limited number of flap jumps before must land
    ///   • Stamina and flap count reset on ground touch
    ///   • Visual: puffed-up Kirby sprite while hovering
    /// </summary>
    public class KirbyHoverAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "hover";
        public override string DisplayName => "Hover";

        private const string SFX_FLAP = "event:/desolozantas/char/kirby/jump";
        private const string SFX_EXHALE = "event:/desolozantas/char/kirby/spit";

        private int _flapCount;
        private bool _wasOnGround;

        /// <summary>Whether Kirby is currently hovering (slowed fall + flap available).</summary>
        public bool IsHovering { get; private set; }

        /// <summary>Current hover stamina (shared with extension).</summary>
        public float Stamina
        {
            get => Extension?.CurrentStamina ?? 0f;
            set { if (Extension != null) Extension.CurrentStamina = value; }
        }

        /// <summary>Flaps used since last ground touch.</summary>
        public int FlapsUsed => _flapCount;

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Settings == null || !Settings.HoverEnabled)
            {
                if (IsHovering)
                    EndHover();

                IsExecuting = false;
                return;
            }

            if (Player == null || Extension.IsDead) return;

            bool onGround = Player.OnGround();

            // Reset on ground
            if (onGround)
            {
                if (IsHovering)
                    EndHover();

                _flapCount = 0;
                Stamina = Settings.MaxStamina;
            }

            // Check hover input
            bool hoverHeld = Settings.IsKeyCheck("Hover");
            bool hoverPressed = Settings.IsKeyPressed("Hover");

            if (hoverHeld && Stamina > 0f && !onGround)
            {
                if (!IsHovering)
                    BeginHover();

                // Drain stamina
                Stamina = Math.Max(0f, Stamina - Settings.HoverStaminaDrain * Engine.DeltaTime);

                // Flap on press
                if (hoverPressed && _flapCount < Settings.MaxFloatJumps)
                {
                    Flap();
                }

                // Slow fall
                if (Player.Speed.Y > Settings.HoverFallSpeed)
                {
                    Player.Speed = new Vector2(
                        Player.Speed.X,
                        Calc.Approach(Player.Speed.Y, Settings.HoverFallSpeed, Settings.HoverGravity * Engine.DeltaTime)
                    );
                }
            }
            else if (IsHovering)
            {
                EndHover();
            }

            _wasOnGround = onGround;
        }

        #endregion

        #region Hover State

        private void BeginHover()
        {
            IsHovering = true;
            IsExecuting = true;
        }

        private void EndHover()
        {
            if (!IsHovering) return;
            IsHovering = false;
            IsExecuting = false;

            // Exhale puff when stopping hover
            PlaySfx(SFX_EXHALE);
            EmitParticles(ParticleTypes.Dust, Player.Position + Vector2.UnitY * -12f, 3);
        }

        private void Flap()
        {
            Player.Speed = new Vector2(Player.Speed.X, Settings.HoverFlapSpeed);
            _flapCount++;
            PlaySfx(SFX_FLAP);

            // Small puff particles
            EmitParticles(ParticleTypes.Dust, Player.Position + Vector2.UnitY * -4f, 2);
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (IsHovering) return "hover";
            return null;
        }

        #endregion

        #region Cancel

        protected override void OnCancel()
        {
            IsHovering = false;
        }

        #endregion
    }
}
