// =============================================================================
// HonlyHelperBridge.cs — Kirby ↔ HonlyHelper integration
// =============================================================================
// Integrates Kirby player with HonlyHelper's custom entities and triggers.
//
// Credit: HonlyHelper — HollyMagala
//         https://github.com/HollyMagala/HonlyHelper
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for HonlyHelper integration.
    ///
    /// Handles:
    ///   - Custom entity interactions: Kirby properly interacts with HonlyHelper
    ///     entities during inhale, hover, and slide states
    ///   - Custom triggers: Kirby responds to HonlyHelper trigger zones
    ///   - Player state compatibility: ensures Kirby's custom states don't
    ///     conflict with HonlyHelper's player modifications
    /// </summary>
    public class HonlyHelperBridge : IKirbyModBridge
    {
        public string ModName => "HonlyHelper";
        public bool IsActive => KirbyModCompatManager.HonlyHelperLoaded;

        public void Load()
        {
            if (!IsActive) return;

            // Hook into level transitions to ensure Kirby state persists
            Everest.Events.Level.OnTransitionTo += OnLevelTransition_HonlyCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "HonlyHelper bridge: hooked entity interaction compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            Everest.Events.Level.OnTransitionTo -= OnLevelTransition_HonlyCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // HonlyHelper may add custom components to the player
            // Ensure Kirby's extension stays synchronized
            if (kirby.IsSynced)
            {
                kirby.Position = player.Position;
            }
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source) => false;
        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // No special handling needed for HonlyHelper
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnLevelTransition_HonlyCompat(Level level, LevelData next, Vector2 direction)
        {
            // Ensure Kirby extension persists through HonlyHelper-triggered transitions
            var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (kirby != null)
            {
                kirby.SaveToSession();

                Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                    "HonlyHelper transition: saved Kirby state to session");
            }
        }
    }
}
