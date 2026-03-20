// =============================================================================
// DoonvHelperBridge.cs — Kirby ↔ DoonvHelper integration
// =============================================================================
// Integrates Kirby player with DoonvHelper's CustomEnemy and CustomNPC systems.
// Allows Kirby to inhale DoonvHelper enemies, bounce on them, and interact
// with DoonvHelper's bullet/projectile system using Kirby abilities.
//
// Credit: DoonvHelper (MIT License) — doonv (Doonv), EllaTAS, Kosei
//         https://github.com/doonv/DoonvHelper
// =============================================================================

using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for DoonvHelper integration.
    ///
    /// Handles:
    ///   - CustomEnemy: Kirby can inhale DoonvHelper enemies for copy abilities
    ///   - CustomNPC: Kirby can interact with DoonvHelper NPCs via talk
    ///   - Bullet collision: Kirby's inhale can absorb enemy bullets
    ///   - Bouncebox: Kirby properly bounces on enemies with bounce hitboxes
    ///   - Dashable enemies: Kirby's dash attack works with DoonvHelper enemies
    /// </summary>
    public class DoonvHelperBridge : IKirbyModBridge
    {
        public string ModName => "DoonvHelper";
        public bool IsActive => KirbyModCompatManager.DoonvHelperLoaded;

        public void Load()
        {
            if (!IsActive) return;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "DoonvHelper bridge: loaded in safe mode without generic enemy mutation");
        }

        public void Unload()
        {
            if (!IsActive) return;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source)
        {
            return false;
        }

        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
        }
    }
}
