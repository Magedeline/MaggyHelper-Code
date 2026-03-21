using Celeste.Mod.MaggyHelper.Patches.Player;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Player state IDs for character extension systems.
    ///
    /// Vanilla state IDs are forwarded from ForkedPlayerCore, which mirrors the
    /// authoritative list from NoelFB/Celeste Source/Player/Player.cs.
    /// Custom mod-specific state IDs start at 100 to avoid collisions.
    /// </summary>
    public static class PlayerCharacterStates
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Vanilla state IDs — forwarded from ForkedPlayerCore (real Player.cs reference)
        // ─────────────────────────────────────────────────────────────────────────

        /// <inheritdoc cref="ForkedPlayerCore.StNormal"/>
        public const int StNormal        = ForkedPlayerCore.StNormal;

        /// <inheritdoc cref="ForkedPlayerCore.StClimb"/>
        public const int StClimb         = ForkedPlayerCore.StClimb;

        /// <inheritdoc cref="ForkedPlayerCore.StDash"/>
        public const int StDash          = ForkedPlayerCore.StDash;

        /// <inheritdoc cref="ForkedPlayerCore.StSwim"/>
        public const int StSwim          = ForkedPlayerCore.StSwim;

        /// <inheritdoc cref="ForkedPlayerCore.StBoost"/>
        public const int StBoost         = ForkedPlayerCore.StBoost;

        /// <inheritdoc cref="ForkedPlayerCore.StDreamDash"/>
        public const int StDreamDash     = ForkedPlayerCore.StDreamDash;

        /// <inheritdoc cref="ForkedPlayerCore.StStarFly"/>
        public const int StStarFly       = ForkedPlayerCore.StStarFly;

        /// <inheritdoc cref="ForkedPlayerCore.StDummy"/>
        public const int StDummy         = ForkedPlayerCore.StDummy;

        /// <inheritdoc cref="ForkedPlayerCore.StIntroRespawn"/>
        public const int StIntroRespawn  = ForkedPlayerCore.StIntroRespawn;

        // ─────────────────────────────────────────────────────────────────────────
        // Custom mod state IDs — must not collide with vanilla states (0–22).
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Kirby ground-slide state (custom, not in vanilla Player.cs).</summary>
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
