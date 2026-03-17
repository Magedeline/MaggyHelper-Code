// =============================================================================
// BossesHelperBridge.cs — Kirby ↔ BossesHelper integration
// =============================================================================
// Integrates Kirby's health/damage system with BossesHelper's HealthSystemManager.
// Implements fake death, invincibility frames, damage interception, and
// safe-ground tracking for the Kirby player.
//
// Credit: BossesHelper (MIT License) — TheDavSmasher (David Higueros)
//         https://github.com/TheDavSmasher/BossesHelper
// =============================================================================

using System;
using System.Collections;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for BossesHelper integration.
    ///
    /// When BossesHelper's HealthSystemManager is active, this bridge:
    ///   - Routes BossesHelper damage events through Kirby's health system
    ///   - Implements fake death with Kirby-specific respawn animation
    ///   - Adds BossesHelper-compatible invincibility frames to Kirby
    ///   - Tracks safe ground positions for Kirby's respawn system
    ///   - Prevents double-death when both systems want to kill the player
    /// </summary>
    public class BossesHelperBridge : IKirbyModBridge
    {
        public string ModName => "BossesHelper";
        public bool IsActive => KirbyModCompatManager.BossesHelperLoaded;

        private bool _isFakeDeathActive;
#pragma warning disable CS0414
        private float _iframeDuration = 0.6f;
#pragma warning restore CS0414
        private Vector2 _lastSafePosition;

        public void Load()
        {
            if (!IsActive) return;

            // Hook Player.Die to intercept death when Kirby has health remaining
            On.Celeste.Player.Die += OnPlayerDie_BossesCompat;
            On.Celeste.Player.Update += OnPlayerUpdate_BossesCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "BossesHelper bridge: hooked health system / fake death compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            On.Celeste.Player.Die -= OnPlayerDie_BossesCompat;
            On.Celeste.Player.Update -= OnPlayerUpdate_BossesCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Track safe ground position for fake death respawn
            // (mirrors BossesHelper's UpdatePlayerLastSafe behavior)
            if (player.OnSafeGround)
            {
                _lastSafePosition = player.Position;
            }
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source)
        {
            if (!IsActive || kirby == null) return false;

            // If BossesHelper's HealthSystemManager is active, let it handle damage
            // through its own system — we just sync Kirby's visual health display
            // The actual damage routing happens in OnPlayerDie_BossesCompat
            return false;
        }

        public bool OnDeath(KirbyPlayerExtension kirby, Player player)
        {
            if (!IsActive || kirby == null) return false;

            // If Kirby still has health, perform fake death instead
            if (kirby.CurrentHealth > 1 && !_isFakeDeathActive)
            {
                PerformKirbyFakeDeath(kirby, player);
                return true; // Cancel real death
            }

            return false;
        }

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // No special handling needed for BossesHelper
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Intercept Player.Die to route through Kirby's health system.
        /// Inspired by BossesHelper's OnPlayerDie hook pattern.
        /// </summary>
        private PlayerDeadBody OnPlayerDie_BossesCompat(
            On.Celeste.Player.orig_Die orig, Player self, Vector2 dir, bool always, bool register)
        {
            if (self.Scene is not Level level) return orig(self, dir, always, register);

            var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (kirby == null || kirby.IsDead) return orig(self, dir, always, register);

            // Always-kill bypasses Kirby health (e.g., falling into void)
            if (always)
            {
                kirby.TakeDamage(kirby.MaxHealth, dir);
                return orig(self, dir, always, register);
            }

            // Route through Kirby damage system
            if (kirby.CurrentHealth > 1)
            {
                kirby.TakeDamage(1, dir);
                PerformKirbyFakeDeath(kirby, self);
                return null; // Cancel actual death
            }

            // Last hit — allow real death
            kirby.TakeDamage(1, dir);
            return orig(self, dir, always, register);
        }

        /// <summary>
        /// Track safe ground for respawn (mirrors BossesHelper pattern).
        /// </summary>
        private void OnPlayerUpdate_BossesCompat(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);

            if (self.OnSafeGround && self.Scene is Level level)
            {
                var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
                if (kirby != null)
                {
                    _lastSafePosition = self.Position;
                }
            }
        }

        /// <summary>
        /// Perform a fake death for Kirby — stagger animation, invulnerability,
        /// and snap back to safe position.
        /// Based on BossesHelper's FakeDie pattern.
        /// </summary>
        private void PerformKirbyFakeDeath(KirbyPlayerExtension kirby, Player player)
        {
            if (_isFakeDeathActive || player.Dead) return;
            _isFakeDeathActive = true;

            Level level = player.SceneAs<Level>();
            if (level == null) return;

            // Visual feedback
            player.Speed = Vector2.Zero;
            level.Shake(0.2f);
            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);

            // Kirby hurt animation
            kirby.KirbySprite?.Play(kirby.ResolveAnim("death"));

            // Brief stagger then reposition
            player.Add(new Coroutine(FakeDeathRoutine(kirby, player, level)));
        }

        /// <summary>
        /// Coroutine for Kirby fake death — brief freeze, then snap to safe position.
        /// </summary>
        private IEnumerator FakeDeathRoutine(KirbyPlayerExtension kirby, Player player, Level level)
        {
            // Brief freeze
            Celeste.Celeste.Freeze(0.05f);
            yield return 0.15f;

            // Flash screen
            level.Flash(Color.White * 0.5f, false);
            yield return 0.1f;

            // Snap to safe position
            if (_lastSafePosition != Vector2.Zero)
            {
                player.Position = _lastSafePosition;
                kirby.Position = _lastSafePosition;
            }

            // Restore movement
            player.Speed = Vector2.Zero;
            player.StateMachine.State = Player.StNormal;

            // Kirby idle animation
            kirby.KirbySprite?.Play(kirby.ResolveAnim("idle"));

            // Grant invulnerability frames
            // (Kirby's own invuln timer handles this via TakeDamage)

            _isFakeDeathActive = false;

            Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                $"Kirby fake death complete — health: {kirby.CurrentHealth}/{kirby.MaxHealth}");
        }
    }
}
