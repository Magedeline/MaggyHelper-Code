using Celeste.Mod.MaggyHelper.Patches.Player;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Custom player state IDs for character extension systems.
    ///
    /// State IDs are assigned at runtime by StateMachine.AddState() during
    /// Player.Added (via PlayerPatchCore.OnPlayerAdded → KirbyPlayerStatePatch.RegisterStates).
    /// All IDs default to -1 until the first Player entity is added to a scene.
    ///
    /// [UPSTREAM-REF] Vanilla Celeste Player states (Player.cs constructor):
    ///   StateMachine = new StateMachine(23);   ← 23 vanilla states, IDs 0–22
    ///   StNormal=0, StClimb=1, StDash=2, StSwim=3, StBoost=4, StRedDash=5,
    ///   StHitSquash=6, StLaunch=7, StPickup=8, StDreamDash=9, StSummitLaunch=10,
    ///   StDummy=11, StIntroWalk=12, StIntroJump=13, StIntroRespawn=14,
    ///   StIntroWakeUp=15, StBirdDashTutorial=16, StFrozen=17, StReflectionFall=18,
    ///   StStarFly=19, StTempleFall=20, StCassetteFly=21, StAttract=22
    ///   https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
    ///
    /// Our states are registered via AddState() and start at ID 23:
    ///   StKirbyHover  = 23  (first AddState call)
    ///   StKirbyInhale = 24
    ///   StKirbySlide  = 25  (was placeholder const 100 — now a real registered state)
    /// </summary>
    public static class PlayerCharacterStates
    {
        // ── Kirby state IDs ────────────────────────────────────────────────────
        // Set at runtime by KirbyPlayerStatePatch.RegisterStates() via AddState().
        // -1 = not yet registered (PlayerPatchCore not initialized or no Player spawned yet).

        /// <summary>
        /// Kirby hover / float state.
        /// Registered as the 1st AddState call → ID 23 (after vanilla states 0–22).
        /// Physics: reduced gravity (HoverGravity), capped fall speed (HoverFallSpeed).
        /// Horizontal movement mirrors NormalUpdate. Exits to StNormal on hover end.
        /// </summary>
        public static int StKirbyHover { get; private set; } = -1;

        /// <summary>
        /// Kirby inhale state.
        /// Registered as the 2nd AddState call → ID 24.
        /// Movement: walk-only (50% MaxRun). Pull cone managed by KirbyInhaleAbility.
        /// Exits to StNormal when inhale ends.
        /// </summary>
        public static int StKirbyInhale { get; private set; } = -1;

        /// <summary>
        /// Kirby slide / dash-burst state.
        /// Registered as the 3rd AddState call → ID 25.
        /// Previously a placeholder const = 100 (never reached). Now a real registered
        /// state — KirbyPlayerExtensionCore.PlayerOnCollideH correctly detects it.
        /// Speed: directional burst (SlideSpeed) for SlideDuration seconds.
        /// </summary>
        public static int StKirbySlide { get; private set; } = -1;

        /// <summary>
        /// Called by KirbyPlayerStatePatch.RegisterStates() after each Player.Added.
        /// All Player instances receive the same IDs since vanilla provides 23 states
        /// in a fixed order, making AddState positions deterministic.
        /// </summary>
        internal static void SetKirbyStateIds(int hover, int inhale, int slide)
        {
            StKirbyHover  = hover;
            StKirbyInhale = inhale;
            StKirbySlide  = slide;
        }

        public static void Initialize()
        {
            // State IDs are now set dynamically via SetKirbyStateIds().
            // No pre-initialization needed here.
        }

        public static void Uninitialize()
        {
            // Reset to -1 on module unload so stale IDs don't persist across reloads.
            StKirbyHover  = -1;
            StKirbyInhale = -1;
            StKirbySlide  = -1;
        }
    }
}
