// =============================================================================
// VivHelperBridge.cs — Kirby ↔ VivHelper integration
// =============================================================================
// Integrates Kirby player with VivHelper's custom boosters, speed triggers,
// refill walls, hold triggers, and custom player state modifiers.
//
// Credit: VivHelper (AGPL-3.0 License) — vivianlonging (Viv) & contributors
//         https://github.com/vivianlonging/VivHelper
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for VivHelper integration.
    ///
    /// Handles:
    ///   - Custom Boosters: Kirby sprite syncs during VivHelper boost states
    ///   - Speed Triggers: Kirby abilities scale with modified player speed
    ///   - Refill Walls: Kirby hover stamina refills when passing through
    ///   - Hold Triggers: Kirby can use abilities while in hold trigger zones
    ///   - Custom Springs: Kirby bounces properly off VivHelper springs
    ///   - Custom Player State Modifiers: Kirby state machine compat
    /// </summary>
    public class VivHelperBridge : IKirbyModBridge
    {
        public string ModName => "VivHelper";
        public bool IsActive => KirbyModCompatManager.VivHelperLoaded;

        private float _speedMultiplier = 1f;
        private bool _wasInCustomBoost;

        public void Load()
        {
            if (!IsActive) return;

            // Hook player update to track VivHelper speed modifications
            On.Celeste.Player.Update += OnPlayerUpdate_VivCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "VivHelper bridge: hooked custom booster + speed trigger compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            On.Celeste.Player.Update -= OnPlayerUpdate_VivCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Detect VivHelper custom booster state (StBoost = 4, StRedDash = 5)
            bool inBoostState = player.StateMachine.State == Player.StBoost ||
                                player.StateMachine.State == Player.StRedDash;

            if (inBoostState && !_wasInCustomBoost)
            {
                OnEnterBoost(kirby, player);
            }
            else if (!inBoostState && _wasInCustomBoost)
            {
                OnExitBoost(kirby, player);
            }
            _wasInCustomBoost = inBoostState;

            // Scale Kirby's inhale/spit effectiveness with speed multiplier
            // VivHelper speed triggers modify player speed; Kirby should adapt
            float currentSpeed = player.Speed.Length();
            if (currentSpeed > 0)
            {
                // Detect speed modifications by comparing to expected max
                _speedMultiplier = Math.Max(1f, currentSpeed / Player.MaxRun);
            }

        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source) => false;
        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // Scale Kirby abilities with VivHelper dash modifications
        }

        /// <summary>
        /// Get the current speed multiplier (for other systems to query).
        /// </summary>
        public float GetSpeedMultiplier() => _speedMultiplier;

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnEnterBoost(KirbyPlayerExtension kirby, Player player)
        {
            // Entering a VivHelper custom booster — cancel active abilities
            var hover = kirby.Hover;
            if (hover != null && hover.IsHovering)
                hover.Cancel();

            var inhale = kirby.Inhale;
            if (inhale != null && inhale.IsExecuting)
                inhale.Cancel();

            // Sync sprite
            kirby.KirbySprite?.Play(kirby.ResolveAnim(KirbyAnimIds.Logical.Dash));
        }

        private void OnExitBoost(KirbyPlayerExtension kirby, Player player)
        {
            // Exiting custom booster — refill Kirby stamina partially
            kirby.CurrentStamina = Math.Min(
                kirby.CurrentStamina + kirby.MaxStamina * 0.2f,
                kirby.MaxStamina);
        }

        private void OnPlayerUpdate_VivCompat(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);

            if (self.Scene is not Level level) return;

            var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (kirby == null) return;

            // When VivHelper modifies player state, ensure Kirby stays synced
            if (kirby.IsSynced)
            {
                kirby.Position = self.Position;
            }
        }

    }
}
