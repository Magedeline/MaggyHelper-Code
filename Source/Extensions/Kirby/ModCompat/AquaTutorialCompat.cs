namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Reflection-friendly tutorial condition helpers for Aqua and Kirby integration.
    /// These methods are intentionally static and take only a Level so they can be
    /// called from mods that support condition functions by reflection.
    /// </summary>
    public static class AquaTutorialCompat
    {
        private const int HookStateFixed = 4;
        private const int HookStateAttracted = 5;

        private static readonly AquaInterop Interop = new();
        private static bool _resolved;

        public static bool IsAquaHookFixed(Level level)
        {
            return GetHookState(level) == HookStateFixed;
        }

        public static bool IsAquaHookActive(Level level)
        {
            return GetHookState(level) > 0;
        }

        public static bool IsKirbyAquaSwinging(Level level)
        {
            if (level == null)
            {
                return false;
            }

            Player player = level.Tracker.GetEntity<Player>();
            if (player == null || !player.IsKirbyMode())
            {
                return false;
            }

            return GetHookState(level) == HookStateFixed && !player.OnGround();
        }

        public static bool IsKirbyAquaAttracted(Level level)
        {
            if (level == null)
            {
                return false;
            }

            Player player = level.Tracker.GetEntity<Player>();
            return player != null && player.IsKirbyMode() && GetHookState(level) == HookStateAttracted;
        }

        private static int GetHookState(Level level)
        {
            if (level == null || !EnsureInterop())
            {
                return 0;
            }

            Player player = level.Tracker.GetEntity<Player>();
            if (player == null)
            {
                return 0;
            }

            Entity hook = Interop.GetGrapplingHook(player);
            return Interop.GetGrapplingHookState(hook);
        }

        private static bool EnsureInterop()
        {
            if (!_resolved)
            {
                _resolved = true;
                Interop.Resolve();
            }

            return Interop.IsAvailable;
        }
    }
}