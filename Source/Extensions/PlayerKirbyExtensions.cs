using MaggyHelper.Entities;
using Monocle;
using MaggyHelper.Extensions.Core;
using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Extensions
{
    /// <summary>
    /// Player extension methods for Kirby mode integration.
    /// Provides backwards-compatible helpers used across triggers/entities.
    /// </summary>
    public static class PlayerKirbyExtensions
    {
        public static bool IsKirbyMode(this Player player)
        {
            if (player == null)
            {
                return false;
            }

            // Prefer character module state if available
            var module = PlayerExtensionCore.Instance.GetCharacterModule<KirbyCharacterModule>();
            if (module != null)
            {
                return module.IsActive(player);
            }

            if (player.Scene is Level level)
            {
                return level.Session?.GetFlag("kirby_mode") ?? false;
            }

            return LevelStateManager.IsKirbyModeEnabled();
        }

        public static void EnableKirbyMode(this Player player)
        {
            if (player == null)
            {
                return;
            }

            var module = PlayerExtensionCore.Instance.GetCharacterModule<KirbyCharacterModule>();
            if (module != null)
            {
                module.Enable(player);
                return;
            }

            if (player.Scene is Level level)
            {
                LevelStateManager.EnableKirbyMode(level);
                level.Session?.SetFlag("kirby_mode", true);
            }
        }

        public static void DisableKirbyMode(this Player player)
        {
            if (player == null)
            {
                return;
            }

            var module = PlayerExtensionCore.Instance.GetCharacterModule<KirbyCharacterModule>();
            if (module != null)
            {
                module.Disable(player);
                return;
            }

            if (player.Scene is Level level)
            {
                LevelStateManager.DisableKirbyMode(level);
                level.Session?.SetFlag("kirby_mode", false);
            }
        }

        public static void SetKirbyPowerState(this Player player, KirbyMode.KirbyPowerState power)
        {
            if (player?.Scene is not Level level)
            {
                return;
            }

            var module = PlayerExtensionCore.Instance.GetCharacterModule<KirbyCharacterModule>();
            if (module != null)
            {
                module.SetPowerState(level, power);
                return;
            }

            // Try new extension first
            var kirbyExt = level.Tracker.GetEntity<KirbyPlayerExtension>();
            kirbyExt?.SetPowerState(power);

            // Legacy fallback
            var kirby = level.Tracker.GetEntity<KirbyMode>();
            kirby?.SetPowerState(power);

            var state = LevelStateManager.GetState();
            if (state != null)
            {
                state.KirbyPower = power;
            }
        }

        // Legacy aliases used by older content
        public static bool IsKirbyPlayerMode(this Player player) => player.IsKirbyMode();

        public static void EnableKirbyPlayerMode(this Player player) => player.EnableKirbyMode();

        public static void DisableKirbyPlayerMode(this Player player) => player.DisableKirbyMode();
    }
}
