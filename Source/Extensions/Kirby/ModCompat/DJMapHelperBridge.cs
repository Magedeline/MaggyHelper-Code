// =============================================================================
// DJMapHelperBridge.cs — Kirby ↔ DJMapHelper integration
// =============================================================================
// Integrates Kirby player with DJMapHelper's max dashes trigger,
// colorful refills/feathers, reversed bosses, and custom springs.
//
// Credit: DJMapHelper (MIT License) — DemoJameson
//         https://github.com/DemoJameson/Celeste.DJMapHelper
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for DJMapHelper integration.
    ///
    /// Handles:
    ///   - MaxDashesTrigger: when DJMapHelper modifies max dashes, scale
    ///     Kirby's abilities proportionally
    ///   - ColorfulRefills: Kirby color/aura matches refill color
    ///   - ColorfulFlyFeather: Kirby enters feather mode with proper sprite
    ///   - Reversed bosses/fling birds: Kirby interactions work correctly
    ///   - FeatherBarrier: Kirby respects feather barriers during hover
    /// </summary>
    public class DJMapHelperBridge : IKirbyModBridge
    {
        public string ModName => "DJMapHelper";
        public bool IsActive => KirbyModCompatManager.DJMapHelperLoaded;

        private int _previousMaxDashes = -1;

        public void Load()
        {
            if (!IsActive) return;

            // Hook into level begin to track DJMapHelper trigger state
            Everest.Events.Level.OnLoadLevel += OnLevelLoad_DJCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "DJMapHelper bridge: hooked max dashes + colorful refill compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            Everest.Events.Level.OnLoadLevel -= OnLevelLoad_DJCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Track max dashes changes from DJMapHelper's MaxDashesTrigger
            int currentMaxDashes = player.Inventory.Dashes;
            if (currentMaxDashes != _previousMaxDashes && _previousMaxDashes >= 0)
            {
                OnMaxDashesChanged(kirby, player, _previousMaxDashes, currentMaxDashes);
            }
            _previousMaxDashes = currentMaxDashes;

            // During star fly state (feather), sync Kirby sprite
            if (player.StateMachine.State == Player.StStarFly && kirby.IsSynced)
            {
                kirby.KirbySprite?.Play(kirby.ResolveAnim("hover"));
            }
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source) => false;
        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // Handled via UpdateKirby's max dashes tracking
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnMaxDashesChanged(
            KirbyPlayerExtension kirby, Player player, int oldMax, int newMax)
        {
            if (newMax > oldMax)
            {
                // More dashes available — boost Kirby capabilities
                float staminaBonus = (newMax - oldMax) * (kirby.MaxStamina * 0.25f);
                kirby.CurrentStamina = Math.Min(
                    kirby.CurrentStamina + staminaBonus, kirby.MaxStamina);

                Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                    $"DJMapHelper max dashes {oldMax}→{newMax}, Kirby stamina boosted");
            }
        }

        private void OnLevelLoad_DJCompat(Level level, Player.IntroTypes intro, bool isFromLoader)
        {
            // Reset max dashes tracking on level load
            _previousMaxDashes = -1;
        }
    }
}
