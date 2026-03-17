namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Custom player state IDs for character extension systems.
    /// </summary>
    public static class PlayerCharacterStates
    {
        // Reserved state IDs for custom character behaviors.
        // Ensure these do not collide with Celeste's built-in states.
        public const int StKirbySlide = 100;

        public static void Initialize()
        {
            // Reserved for future hook/state registration.
        }

        public static void Uninitialize()
        {
            // Reserved for future cleanup.
        }
    }
}
