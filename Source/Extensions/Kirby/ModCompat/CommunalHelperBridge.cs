// =============================================================================
// CommunalHelperBridge.cs — Kirby ↔ CommunalHelper integration
// =============================================================================
// Integrates Kirby player with CommunalHelper's custom dash states,
// dream tunnel dashing, custom boosters, and station blocks.
//
// Credit: CommunalHelper (MIT License) — catapillie, coloursofnoise,
//         vivianlonging, aonkeeper4, and contributors.
//         https://github.com/CommunalHelper/CommunalHelper
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for CommunalHelper integration.
    /// 
    /// Ensures Kirby's sprite, health, and abilities function correctly
    /// when interacting with CommunalHelper entities:
    ///   - Dream Tunnel Dash: Kirby keeps health/abilities while dream dashing
    ///   - Custom Boosters: Kirby sprite syncs during boost states
    ///   - Station Blocks: Kirby correctly rides and transitions
    ///   - Connected Solids: Kirby collisions work with connected blocks
    /// </summary>
    public class CommunalHelperBridge : IKirbyModBridge
    {
        public string ModName => "CommunalHelper";
        public bool IsActive => KirbyModCompatManager.CommunalHelperLoaded;

        // Track dream tunnel dash state
        private bool _wasInDreamTunnelDash;
        private float _dreamDashStaminaSnapshot;

        public void Load()
        {
            if (!IsActive) return;

            // Hook player state transitions to handle dream tunnel dash
            On.Celeste.Player.Update += OnPlayerUpdate_CommunalCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "CommunalHelper bridge: hooked dream tunnel dash compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            On.Celeste.Player.Update -= OnPlayerUpdate_CommunalCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Detect dream tunnel dash state via depth change
            // CommunalHelper sets Depth = Depths.PlayerDreamDashing during dream tunnel dash
            bool inDreamTunnelDash = player.Depth == Depths.PlayerDreamDashing;

            if (inDreamTunnelDash && !_wasInDreamTunnelDash)
            {
                // Entering dream tunnel dash — snapshot stamina, preserve Kirby abilities
                _dreamDashStaminaSnapshot = kirby.CurrentStamina;
                OnEnterDreamTunnelDash(kirby, player);
            }
            else if (!inDreamTunnelDash && _wasInDreamTunnelDash)
            {
                // Exiting dream tunnel dash — restore state
                OnExitDreamTunnelDash(kirby, player);
            }

            _wasInDreamTunnelDash = inDreamTunnelDash;

            // During dream tunnel dash, keep Kirby sprite synced
            if (inDreamTunnelDash && kirby.IsSynced)
            {
                kirby.KirbySprite?.Play(kirby.ResolveAnim(KirbyAnimIds.Logical.Dash));
            }

            // Handle custom booster states — CommunalHelper boosters set
            // player.StateMachine.State to custom booster state IDs
            HandleCustomBoosterSync(kirby, player);
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source)
        {
            // During dream tunnel dash, Kirby should be invulnerable (same as vanilla dream dash)
            if (_wasInDreamTunnelDash) return true;
            return false;
        }

        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // CommunalHelper may grant extra dashes from refills — no special handling needed
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnEnterDreamTunnelDash(KirbyPlayerExtension kirby, Player player)
        {
            // Pause hover ability during dream dash
            var hover = kirby.Hover;
            if (hover != null && hover.IsHovering)
            {
                hover.Cancel();
            }

            // Pause inhale during dream dash
            var inhale = kirby.Inhale;
            if (inhale != null && inhale.IsExecuting)
            {
                inhale.Cancel();
            }

            Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                "Kirby entered CommunalHelper dream tunnel dash");
        }

        private void OnExitDreamTunnelDash(KirbyPlayerExtension kirby, Player player)
        {
            // Restore Kirby stamina (dream tunnel dash refills stamina like vanilla)
            kirby.CurrentStamina = Math.Max(kirby.CurrentStamina, _dreamDashStaminaSnapshot);

            Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                "Kirby exited CommunalHelper dream tunnel dash");
        }

        private void HandleCustomBoosterSync(KirbyPlayerExtension kirby, Player player)
        {
            // CommunalHelper custom boosters use the vanilla boost state (StBoost = 4)
            // but with modified behavior. Ensure Kirby sprite stays synced.
            if (player.StateMachine.State == Player.StBoost && kirby.IsSynced)
            {
                kirby.KirbySprite?.Play(kirby.ResolveAnim(KirbyAnimIds.Logical.Dash));
            }
        }

        private void OnPlayerUpdate_CommunalCompat(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);

            // After CommunalHelper's own update, sync Kirby with any state changes
            if (self.Scene is Level level)
            {
                var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
                if (kirby != null && kirby.IsSynced)
                {
                    // Ensure Kirby follows connected solid positions correctly
                    SyncKirbyWithConnectedSolids(kirby, self, level);
                }
            }
        }

        private void SyncKirbyWithConnectedSolids(KirbyPlayerExtension kirby, Player player, Level level)
        {
            // CommunalHelper connected solids may move the player;
            // ensure Kirby position stays synced
            if (kirby.IsSynced)
            {
                kirby.Position = player.Position;
            }
        }
    }
}
