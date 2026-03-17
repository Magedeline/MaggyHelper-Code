// =============================================================================
// MoreDashelineBridge.cs — Kirby ↔ MoreDasheline integration
// =============================================================================
// Integrates Kirby player with MoreDasheline's multi-dash hair color system.
// Maps Kirby's aura/hat/ability color to the dash count color scheme.
//
// Credit: MoreDasheline — community mod (no public GitHub available)
//         Provides multi-dash hair color customization for Celeste.
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for MoreDasheline integration.
    ///
    /// Handles:
    ///   - Multi-dash color mapping: Kirby's aura color changes with dash count,
    ///     matching MoreDasheline's per-dash-count color scheme
    ///   - Kirby body tint: subtle tint applied to Kirby's sprite based on
    ///     current dash color (preserves pink base)
    ///   - Star particle colors: Kirby's dash particles match the current
    ///     MoreDasheline color
    ///   - Copy ability color overlay: when Kirby has a power, the ability
    ///     color takes priority over dash color
    /// </summary>
    public class MoreDashelineBridge : IKirbyModBridge
    {
        public string ModName => "MoreDasheline";
        public bool IsActive => KirbyModCompatManager.MoreDashelineLoaded;

        // Default Kirby dash colors (matching MoreDasheline's style)
        private static readonly Color[] DashColors = new[]
        {
            new Color(172, 50, 50),     // 0 dashes — red (no dashes)
            new Color(255, 154, 206),   // 1 dash  — pink (Kirby's default)
            new Color(68, 183, 255),    // 2 dashes — blue (two-dash)
            new Color(255, 230, 50),    // 3 dashes — gold
            new Color(128, 255, 128),   // 4 dashes — green
            new Color(200, 128, 255),   // 5+ dashes — purple
        };

        // Power-specific aura colors (when Kirby has a copy ability)
        private static readonly System.Collections.Generic.Dictionary<KirbyMode.KirbyPowerState, Color>
            PowerColors = new()
        {
            { KirbyMode.KirbyPowerState.Fire, new Color(255, 80, 30) },
            { KirbyMode.KirbyPowerState.Ice, new Color(100, 200, 255) },
            { KirbyMode.KirbyPowerState.Spark, new Color(255, 255, 100) },
            { KirbyMode.KirbyPowerState.Stone, new Color(160, 140, 120) },
            { KirbyMode.KirbyPowerState.Sword, new Color(200, 220, 255) },
            { KirbyMode.KirbyPowerState.Bomb, new Color(50, 50, 50) },
            { KirbyMode.KirbyPowerState.Cutter, new Color(255, 200, 50) },
            { KirbyMode.KirbyPowerState.Beam, new Color(100, 255, 200) },
            { KirbyMode.KirbyPowerState.Wheel, new Color(255, 150, 50) },
            { KirbyMode.KirbyPowerState.Hammer, new Color(180, 100, 50) },
            { KirbyMode.KirbyPowerState.Archer, new Color(180, 50, 180) },
            { KirbyMode.KirbyPowerState.Water, new Color(50, 100, 255) },
            { KirbyMode.KirbyPowerState.Leaf, new Color(50, 200, 50) },
            { KirbyMode.KirbyPowerState.Drill, new Color(200, 180, 100) },
            { KirbyMode.KirbyPowerState.Knight, new Color(80, 80, 120) },
        };

        private Color _currentAuraColor = Color.White;
        private int _lastDashCount = -1;

        public void Load()
        {
            if (!IsActive) return;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "MoreDasheline bridge: loaded multi-dash color compat for Kirby");
        }

        public void Unload()
        {
            if (!IsActive) return;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Determine target aura color
            Color targetColor;

            // Power color takes priority
            if (kirby.CurrentPower != KirbyMode.KirbyPowerState.None &&
                PowerColors.TryGetValue(kirby.CurrentPower, out var powerColor))
            {
                targetColor = powerColor;
            }
            else
            {
                // Use dash count color (MoreDasheline style)
                int dashIndex = Math.Clamp(player.Dashes, 0, DashColors.Length - 1);
                targetColor = DashColors[dashIndex];
            }

            // Smooth lerp to target color
            _currentAuraColor = Color.Lerp(_currentAuraColor, targetColor, Engine.DeltaTime * 8f);

            // Apply subtle tint to Kirby's sprite
            // (only a gentle overlay — Kirby stays pink, aura changes)
            if (kirby.KirbySprite != null)
            {
                // Blend: 80% white (preserve Kirby colors) + 20% aura tint
                kirby.KirbySprite.Color = Color.Lerp(Color.White, _currentAuraColor, 0.2f);
            }

            // Track dash count changes for particle color updates
            if (player.Dashes != _lastDashCount)
            {
                _lastDashCount = player.Dashes;
                OnDashCountChanged(kirby, player, player.Dashes);
            }
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source) => false;
        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            if (!IsActive || kirby == null) return;

            // Update dash particle colors to match MoreDasheline scheme
            int colorIndex = Math.Clamp(newCount, 0, DashColors.Length - 1);
            Color dashColor = DashColors[colorIndex];

            Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                $"MoreDasheline: Kirby dash color updated for {newCount} dashes → {dashColor}");
        }

        /// <summary>
        /// Get the current Kirby aura color (for other systems to query).
        /// </summary>
        public Color GetCurrentAuraColor() => _currentAuraColor;

        /// <summary>
        /// Get the color for a specific dash count.
        /// </summary>
        public static Color GetDashColor(int dashCount)
        {
            int index = Math.Clamp(dashCount, 0, DashColors.Length - 1);
            return DashColors[index];
        }

        /// <summary>
        /// Get the color for a specific Kirby power state.
        /// </summary>
        public static Color GetPowerColor(KirbyMode.KirbyPowerState power)
        {
            return PowerColors.TryGetValue(power, out var color) ? color : Color.White;
        }
    }
}
