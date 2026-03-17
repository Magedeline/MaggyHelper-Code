// =============================================================================
// IKirbyModBridge.cs — Interface for mod compatibility bridges
// =============================================================================

using MaggyHelper.Entities;
using Microsoft.Xna.Framework;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Interface all Kirby mod-compatibility bridges must implement.
    /// Each bridge integrates one external helper mod with the Kirby player system.
    /// </summary>
    public interface IKirbyModBridge
    {
        /// <summary>Display name of the helper mod this bridge targets.</summary>
        string ModName { get; }

        /// <summary>Whether the target mod is loaded and this bridge is active.</summary>
        bool IsActive { get; }

        /// <summary>Hook into the target mod's systems.</summary>
        void Load();

        /// <summary>Unhook from the target mod's systems.</summary>
        void Unload();

        /// <summary>Per-frame update for Kirby player integration.</summary>
        void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level);

        /// <summary>
        /// Called when Kirby takes damage. Return true to block the damage.
        /// </summary>
        bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source);

        /// <summary>
        /// Called when Kirby dies. Return true to cancel the death (fake death, etc.).
        /// </summary>
        bool OnDeath(KirbyPlayerExtension kirby, Player player);

        /// <summary>
        /// Called when the player's dash count changes.
        /// </summary>
        void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount);
    }
}
