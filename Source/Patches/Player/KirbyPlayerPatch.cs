using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Extensions.Kirby;
using MaggyHelper.Extensions.Kirby.Core;

namespace Celeste.Mod.MaggyHelper.Patches.Player
{
    /// <summary>
    /// Kirby-specific behavioral patch layer for the real Player.cs migration architecture.
    ///
    /// Responsibilities:
    ///   - Intercept vanilla Player lifecycle events when Kirby mode is active.
    ///   - Bridge ForkedPlayerCore per-state dispatch to KirbyPlayerExtension ability logic.
    ///   - Override or extend specific player states (dash, normal, climb) for Kirby behavior.
    ///   - Serve as the single Kirby-mode behavior owner inside the new architecture,
    ///     replacing the old extension-only path as the primary logic center.
    ///
    /// Upstream reference:
    ///   NoelFB/Celeste @ 1b0ce45c75e05649ae91b44a8bb6b196684e4352
    ///   Source/Player/Player.cs
    ///
    /// Important:
    ///   This class works on top of the vanilla Player.cs update loop (already run).
    ///   It does not replace vanilla physics; it extends and modifies Kirby-specific outcomes.
    /// </summary>
    public sealed class KirbyPlayerPatch
    {
        private readonly KirbyPlayerCore _playerCore;

        public KirbyPlayerPatch(KirbyPlayerCore playerCore)
        {
            _playerCore = playerCore ?? throw new ArgumentNullException(nameof(playerCore));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ForkedPlayerCore dispatch entry points
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called each frame by ForkedPlayerCore when Kirby mode is active.
        /// Dispatches per-state Kirby behavior after vanilla Player.Update has run.
        /// </summary>
        public void OnUpdate(Celeste.Player player, Level level)
        {
            if (player == null || level == null)
                return;

            // Ensure the Kirby runtime entity is attached (idempotent — safe to call each frame).
            try
            {
                _playerCore.EnsureRuntime(player, level, enableSync: true);
            }
            catch (Exception ex)
            {
                IngesteLogger.Warn($"KirbyPlayerPatch.OnUpdate: EnsureRuntime failed: {ex.Message}");
                return;
            }

            var ext = _playerCore.GetExtension(level);
            if (ext == null)
                return;

            // Route per-state Kirby behavior using the same state IDs as upstream Player.cs.
            int state = player.StateMachine.State;

            switch (state)
            {
                case ForkedPlayerCore.StNormal:
                    OnKirbyNormalUpdate(player, level, ext);
                    break;

                case ForkedPlayerCore.StDash:
                    OnKirbyDashUpdate(player, level, ext);
                    break;

                case ForkedPlayerCore.StClimb:
                    OnKirbyClimbUpdate(player, level, ext);
                    break;

                case ForkedPlayerCore.StSwim:
                    OnKirbySwimUpdate(player, level, ext);
                    break;

                case ForkedPlayerCore.StIntroRespawn:
                    OnKirbyRespawn(player, level, ext);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Called by ForkedPlayerCore's die hook when Kirby mode is active.
        /// Implements Kirby's health-buffering death prevention logic.
        /// Returns null if death was prevented; otherwise returns the vanilla dead body.
        /// </summary>
        public PlayerDeadBody OnPlayerDie(
            On.Celeste.Player.orig_Die orig,
            Celeste.Player player,
            Vector2 direction,
            bool evenIfInvincible,
            bool registerDeathInStats)
        {
            if (player?.Scene is not Level level)
                return orig(player, direction, evenIfInvincible, registerDeathInStats);

            // Instant-kill hazards (spinners, blades, crush blocks) bypass health buffering.
            // This matches vanilla Kirby behavior: certain hazards stay lethal regardless of HP.
            if (IsInstantKillHazardContact(player))
            {
                return orig(player, direction, evenIfInvincible, registerDeathInStats);
            }

            var ext = _playerCore.GetExtension(level);
            if (ext != null && ext.CurrentHealth > 1)
            {
                ext.TakeDamage(1);
                // Push player away from hazard to avoid getting stuck.
                if (direction != Vector2.Zero)
                    player.Position -= direction * 4f;
                return null;
            }

            var legacy = _playerCore.GetLegacy(level);
            if (legacy != null && legacy.CurrentHealth > 1)
            {
                legacy.TakeDamage(1);
                if (direction != Vector2.Zero)
                    player.Position -= direction * 4f;
                return null;
            }

            return orig(player, direction, evenIfInvincible, registerDeathInStats);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Per-state Kirby behavior
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Kirby normal-movement extensions (StNormal = 0).
        ///
        /// Vanilla NormalUpdate has already run.
        /// This adds Kirby-specific behaviors on top:
        ///   - Hover initiation (multi-flap float)
        ///   - Inhale input detection
        ///   - Grounded slide ability trigger
        /// </summary>
        private void OnKirbyNormalUpdate(Celeste.Player player, Level level, KirbyPlayerExtension ext)
        {
            // Ability manager handles its own input polling; no explicit delegation needed here
            // because KirbyPlayerExtension.Update() already calls _abilityManager.Update().
            // This entry point is for state-aware overrides that require knowledge of the
            // current Player.StateMachine state — e.g., only allow inhale on ground/air,
            // suppress hover while holding a grab, etc.

            // Phase 2: add inhale-trigger gating, float stamina restoration on ground, etc.
        }

        /// <summary>
        /// Kirby dash-state extensions (StDash = 2).
        ///
        /// Vanilla DashUpdate + DashCoroutine have already run.
        /// Kirby-specific dash overrides: cancel inhale on dash start, dash particle colors, etc.
        /// </summary>
        private void OnKirbyDashUpdate(Celeste.Player player, Level level, KirbyPlayerExtension ext)
        {
            // Cancel active inhale if dash begins — consistent with mainline Kirby games.
            if (ext.Inhale?.IsInhaling == true)
            {
                ext.Inhale.StopInhale();
            }

            // Phase 2: add Kirby-specific dash trail, ability-driven dash overrides, etc.
        }

        /// <summary>
        /// Kirby climb-state extensions (StClimb = 1).
        ///
        /// Vanilla ClimbUpdate has already run.
        /// Kirby-specific climb: cancel hover while climbing, adjust stamina consumption rate if needed.
        /// </summary>
        private void OnKirbyClimbUpdate(Celeste.Player player, Level level, KirbyPlayerExtension ext)
        {
            // Cancel all abilities (including hover) while climbing.
            // Using CancelAll since KirbyHoverAbility.EndHover is private.
            if (ext.Hover?.IsHovering == true)
            {
                ext.AbilityManager.CancelAll();
            }

            // Phase 2: stamina overrides, Kirby-specific climb SFX, etc.
        }

        /// <summary>
        /// Kirby swim-state extensions (StSwim = 3).
        ///
        /// Vanilla SwimUpdate has already run.
        /// </summary>
        private void OnKirbySwimUpdate(Celeste.Player player, Level level, KirbyPlayerExtension ext)
        {
            // Phase 2: Kirby-specific swim behavior (puffed float in water, etc.).
        }

        /// <summary>
        /// Kirby respawn handling (StIntroRespawn = 14).
        /// Resets transient Kirby runtime state after a death + respawn.
        /// </summary>
        private void OnKirbyRespawn(Celeste.Player player, Level level, KirbyPlayerExtension ext)
        {
            // KirbyPlayerExtension.UpdateRespawnState() already handles health/stamina reset
            // when it detects the respawn state. This hook is for any additional coordination
            // that must happen at the ForkedPlayerCore layer (e.g., clearing mode-router flags).
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Hazard classification
        // ─────────────────────────────────────────────────────────────────────────

        private static readonly string[] InstantKillHazardKeywords =
        {
            "Spinner", "Blade", "Crush", "CrushingBlock", "CrushBlock", "Spikes", "Spike", "Saw"
        };

        private static bool IsInstantKillHazardContact(Celeste.Player player)
        {
            if (player?.Scene is not Level level)
                return false;

            foreach (Entity entity in level.Entities)
            {
                if (entity == null || entity == player || !entity.Collidable)
                    continue;

                if (!LooksLikeInstantKillHazard(entity.GetType().Name))
                    continue;

                if (player.CollideCheck(entity))
                    return true;

                if (player.CollideCheck(entity, Vector2.UnitY))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeInstantKillHazard(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;

            for (int i = 0; i < InstantKillHazardKeywords.Length; i++)
            {
                if (typeName.Contains(InstantKillHazardKeywords[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
