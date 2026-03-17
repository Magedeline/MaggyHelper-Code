using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Abstract base class for all Kirby abilities.
    /// Each ability is a self-contained module that hooks into the KirbyPlayerExtension
    /// lifecycle and can respond to input, update state, and render effects.
    /// </summary>
    public abstract class KirbyAbilityBase
    {
        /// <summary>Unique ability identifier.</summary>
        public abstract string AbilityId { get; }

        /// <summary>Display name for HUD/UI.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Whether this ability is currently active/usable.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Whether this ability is currently executing (e.g. mid-attack).</summary>
        public bool IsExecuting { get; protected set; }

        /// <summary>Global cooldown remaining (seconds).</summary>
        public float Cooldown { get; internal set; }

        /// <summary>Maximum cooldown duration (seconds).</summary>
        public virtual float MaxCooldown => 0f;

        /// <summary>Internal timer for sub-state tracking.</summary>
        protected float stateTimer;

        /// <summary>Reference to the owning extension.</summary>
        protected KirbyPlayerExtension Extension { get; private set; }

        /// <summary>Convenience: the vanilla Player.</summary>
        protected Player Player => Extension?.Player;

        /// <summary>Convenience: the current Level.</summary>
        protected Level Level => Player?.SceneAs<Level>();

        /// <summary>Convenience: Kirby settings.</summary>
        protected KirbySettings Settings => Extension?.Settings;

        /// <summary>
        /// Called once when the ability is registered with a KirbyPlayerExtension.
        /// </summary>
        public void Attach(KirbyPlayerExtension extension)
        {
            Extension = extension;
            OnAttach();
        }

        /// <summary>
        /// Called once when the ability is unregistered.
        /// </summary>
        public void Detach()
        {
            OnDetach();
            Extension = null;
        }

        /// <summary>
        /// Called every frame while Kirby mode is active.
        /// </summary>
        public void Update()
        {
            if (!IsEnabled) return;

            if (Cooldown > 0f)
                Cooldown = Math.Max(0f, Cooldown - Engine.DeltaTime);

            if (stateTimer > 0f)
                stateTimer = Math.Max(0f, stateTimer - Engine.DeltaTime);

            OnUpdate();
        }

        /// <summary>
        /// Called every render frame while Kirby mode is active.
        /// </summary>
        public void Render()
        {
            if (!IsEnabled) return;
            OnRender();
        }

        /// <summary>
        /// Attempt to activate the ability. Returns true if activation succeeded.
        /// </summary>
        public bool TryActivate()
        {
            if (!IsEnabled || Cooldown > 0f) return false;
            if (!CanActivate()) return false;

            IsExecuting = true;
            OnActivate();
            return true;
        }

        /// <summary>
        /// Force-cancel the ability mid-execution.
        /// </summary>
        public void Cancel()
        {
            if (IsExecuting)
            {
                IsExecuting = false;
                OnCancel();
            }
        }

        /// <summary>
        /// Start cooldown.
        /// </summary>
        protected void StartCooldown()
        {
            Cooldown = MaxCooldown;
        }

        /// <summary>
        /// Complete execution and start cooldown.
        /// </summary>
        protected void FinishExecution()
        {
            IsExecuting = false;
            StartCooldown();
            OnFinish();
        }

        #region Virtual Hooks

        /// <summary>Override to initialize on attach.</summary>
        protected virtual void OnAttach() { }

        /// <summary>Override to cleanup on detach.</summary>
        protected virtual void OnDetach() { }

        /// <summary>Override for per-frame logic.</summary>
        protected virtual void OnUpdate() { }

        /// <summary>Override for per-frame rendering.</summary>
        protected virtual void OnRender() { }

        /// <summary>Override to check prerequisites for activation.</summary>
        protected virtual bool CanActivate() => true;

        /// <summary>Override to handle activation.</summary>
        protected virtual void OnActivate() { }

        /// <summary>Override to handle cancellation.</summary>
        protected virtual void OnCancel() { }

        /// <summary>Override to handle finish.</summary>
        protected virtual void OnFinish() { }

        #endregion

        #region Utility

        /// <summary>Get the player's facing direction as a unit vector.</summary>
        protected Vector2 FacingDirection =>
            Player?.Facing == Facings.Left ? -Vector2.UnitX : Vector2.UnitX;

        /// <summary>Get the player's aim direction (8-directional).</summary>
        protected Vector2 AimDirection
        {
            get
            {
                if (Player == null) return Vector2.UnitX;
                Vector2 aim = Input.GetAimVector(Player.Facing);
                return aim == Vector2.Zero ? FacingDirection : aim;
            }
        }

        /// <summary>Play a sound at the player's position.</summary>
        protected void PlaySfx(string eventPath)
        {
            if (Player != null)
                Audio.Play(eventPath, Player.Position);
        }

        /// <summary>Emit particles at a position.</summary>
        protected void EmitParticles(ParticleType type, Vector2 position, int count = 5, Vector2? range = null)
        {
            Level?.ParticlesFG?.Emit(type, count, position, range ?? Vector2.One * 4f);
        }

        #endregion
    }
}
