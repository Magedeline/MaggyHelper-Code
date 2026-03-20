// =============================================================================
// KirbyModCompatManager.cs — Central mod compatibility manager for Kirby player
// =============================================================================
// Manages integration hooks between the Kirby player extension system and
// various open-source Celeste helper mods the game depends on.
//
// Credits & Attributions (all open-source, used with respect to their licenses):
//   - CommunalHelper    (MIT)    — catapillie & contributors  — Dream tunnel dash, custom booster states
//   - VivHelper         (AGPL-3) — vivianlonging (Viv)        — Custom speed/player state mechanics
//   - BossesHelper      (MIT)    — TheDavSmasher              — Health system, damage, fake death, i-frames
//   - MaxHelpingHand    (MIT)    — maddie480                  — Dash count modification, flag refills
//   - DJMapHelper       (MIT)    — DemoJameson                — Max dashes trigger, colorful refills
//   - HonlyHelper                — HollyMagala                — Player entity interactions
//   - MoreDasheline              — (community)                — Multi-dash hair color system
//   - DoonvHelper       (MIT)    — doonv                      — Custom enemy interaction, NPC systems
// =============================================================================

using System;
using System.Collections.Generic;
using MaggyHelper.Entities;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Central manager that detects loaded helper mods at runtime and activates
    /// the corresponding Kirby-player integration bridges.
    /// </summary>
    public static class KirbyModCompatManager
    {
        private static bool _initialized;
        private static readonly List<IKirbyModBridge> _bridges = new();

        /// <summary>Whether each helper mod is currently loaded.</summary>
        public static bool CommunalHelperLoaded { get; private set; }
        public static bool VivHelperLoaded { get; private set; }
        public static bool BossesHelperLoaded { get; private set; }
        public static bool MaxHelpingHandLoaded { get; private set; }
        public static bool DJMapHelperLoaded { get; private set; }
        public static bool HonlyHelperLoaded { get; private set; }
        public static bool MoreDashelineLoaded { get; private set; }
        public static bool DoonvHelperLoaded { get; private set; }
        public static bool AquaLoaded { get; private set; }

        /// <summary>
        /// Initialize mod compatibility detection and activate bridges.
        /// Call from the module's Load() or after Everest has loaded dependencies.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            DetectLoadedMods();
            RegisterBridges();

            foreach (var bridge in _bridges)
            {
                try
                {
                    bridge.Load();
                    Logger.Log(LogLevel.Info, "KirbyModCompat",
                        $"Loaded bridge: {bridge.ModName}");
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "KirbyModCompat",
                        $"Failed to load bridge {bridge.ModName}: {ex.Message}");
                }
            }

            _initialized = true;
            Logger.Log(LogLevel.Info, "KirbyModCompat",
                $"Mod compat initialized — {_bridges.Count} bridge(s) active");
        }

        /// <summary>
        /// Unload all bridges. Call from the module's Unload().
        /// </summary>
        public static void Uninitialize()
        {
            if (!_initialized) return;

            for (int i = _bridges.Count - 1; i >= 0; i--)
            {
                try
                {
                    _bridges[i].Unload();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "KirbyModCompat",
                        $"Failed to unload bridge {_bridges[i].ModName}: {ex.Message}");
                }
            }
            _bridges.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Called every frame to let bridges update Kirby-specific state.
        /// </summary>
        public static void Update(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!_initialized || kirby == null || player == null) return;

            foreach (var bridge in _bridges)
            {
                try
                {
                    bridge.UpdateKirby(kirby, player, level);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warn, "KirbyModCompat",
                        $"Bridge update error ({bridge.ModName}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called when Kirby takes damage, allowing bridges to intercept/modify.
        /// Returns true if the damage should be blocked.
        /// </summary>
        public static bool OnKirbyDamage(KirbyPlayerExtension kirby, int amount, Microsoft.Xna.Framework.Vector2 source)
        {
            foreach (var bridge in _bridges)
            {
                if (bridge.OnDamage(kirby, amount, source))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Called when Kirby dies, allowing bridges to intercept (fake death, etc.).
        /// Returns true if death should be canceled.
        /// </summary>
        public static bool OnKirbyDeath(KirbyPlayerExtension kirby, Player player)
        {
            foreach (var bridge in _bridges)
            {
                if (bridge.OnDeath(kirby, player))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Called when Kirby's dash count changes, so bridges can react.
        /// </summary>
        public static void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            foreach (var bridge in _bridges)
            {
                bridge.OnDashCountChanged(kirby, player, newCount);
            }
        }

        // ------------------------------------------------------------------
        // Internal helpers
        // ------------------------------------------------------------------

        private static void DetectLoadedMods()
        {
            CommunalHelperLoaded = IsModLoaded("CommunalHelper", "1.0.0");
            VivHelperLoaded = IsModLoaded("VivHelper", "1.0.0");
            BossesHelperLoaded = IsModLoaded("BossesHelper", "1.0.0");
            MaxHelpingHandLoaded = IsModLoaded("MaxHelpingHand", "1.0.0");
            DJMapHelperLoaded = IsModLoaded("DJMapHelper", "1.0.0");
            HonlyHelperLoaded = IsModLoaded("HonlyHelper", "1.0.0");
            MoreDashelineLoaded = IsModLoaded("MoreDasheline", "1.0.0");
            DoonvHelperLoaded = IsModLoaded("DoonvHelper", "1.0.0");
            AquaLoaded = IsModLoaded("Aqua", "0.0.0");

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                $"Detected: Communal={CommunalHelperLoaded}, Viv={VivHelperLoaded}, " +
                $"Bosses={BossesHelperLoaded}, MaxHH={MaxHelpingHandLoaded}, " +
                $"DJ={DJMapHelperLoaded}, Honly={HonlyHelperLoaded}, " +
                $"MoreDash={MoreDashelineLoaded}, Doonv={DoonvHelperLoaded}, Aqua={AquaLoaded}");
        }

        private static bool IsModLoaded(string modName, string minVersion)
        {
            return Everest.Loader.DependencyLoaded(new EverestModuleMetadata
            {
                Name = modName,
                Version = new Version(minVersion)
            });
        }

        private static void RegisterBridges()
        {
            // Always register the core bridges; each bridge checks its own mod's
            // loaded state and becomes a no-op if the mod is absent.
            _bridges.Add(new CommunalHelperBridge());
            _bridges.Add(new BossesHelperBridge());
            _bridges.Add(new MaxHelpingHandBridge());
            _bridges.Add(new DJMapHelperBridge());
            _bridges.Add(new DoonvHelperBridge());
            _bridges.Add(new VivHelperBridge());
            _bridges.Add(new MoreDashelineBridge());
            _bridges.Add(new HonlyHelperBridge());
            _bridges.Add(new AquaBridge());
        }
    }
}
