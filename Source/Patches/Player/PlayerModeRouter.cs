using System;
using Monocle;

namespace Celeste.Mod.MaggyHelper.Patches.Player
{
    /// <summary>
    /// Identifies the active rewrite path for the player.
    /// </summary>
    public enum PlayerRewriteMode
    {
        /// <summary>
        /// Standard Celeste player behavior with mod extensions.
        /// Behavior is routed through ForkedPlayerCore normal-state dispatch.
        /// </summary>
        Normal,

        /// <summary>
        /// Kirby-mode player behavior.
        /// Behavior is routed through KirbyPlayerPatch on top of ForkedPlayerCore.
        /// </summary>
        Kirby
    }

    /// <summary>
    /// Routes player behavior between the normal-player path and the Kirby-player path
    /// inside the real NoelFB/Celeste Source/Player/Player.cs-based patch/fork architecture.
    ///
    /// This is the single source of truth for which rewrite path currently owns the player.
    /// It coordinates with the existing session-flag and LevelStateManager infrastructure
    /// so that mode decisions are consistent with the rest of the mod.
    ///
    /// Upstream reference:
    ///   NoelFB/Celeste @ 1b0ce45c75e05649ae91b44a8bb6b196684e4352
    ///   Source/Player/Player.cs
    /// </summary>
    public sealed class PlayerModeRouter
    {
        /// <summary>The Kirby-specific patch layer.</summary>
        public KirbyPlayerPatch KirbyPatch { get; }

        public PlayerModeRouter(KirbyPlayerPatch kirbyPatch)
        {
            KirbyPatch = kirbyPatch ?? throw new ArgumentNullException(nameof(kirbyPatch));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Mode resolution
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determine the active rewrite mode for the given player.
        ///
        /// Resolution order:
        ///   1. Session flag "kirby_mode" (set by triggers/cutscenes).
        ///   2. LevelStateManager global state.
        ///   Fallback: Normal.
        /// </summary>
        public PlayerRewriteMode GetMode(Celeste.Player player)
        {
            if (player?.Scene is not Level level)
                return PlayerRewriteMode.Normal;

            if (IsKirbyActive(level))
                return PlayerRewriteMode.Kirby;

            return PlayerRewriteMode.Normal;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static bool IsKirbyActive(Level level)
        {
            if (level?.Session?.GetFlag("kirby_mode") == true)
                return true;

            return LevelStateManager.IsKirbyModeEnabled();
        }
    }
}
