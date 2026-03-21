using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MaggyHelper.Patches.Player
{
    /// <summary>
    /// Migration core for a patched/forked real Player.cs-based architecture.
    ///
    /// This is the primary behavioral routing layer for both normal-player and Kirby-player paths.
    /// It owns the MonoMod On.Celeste hooks and dispatches per-state logic using the same state
    /// identifiers as the upstream NoelFB/Celeste Source/Player/Player.cs implementation.
    ///
    /// Upstream reference:
    ///   NoelFB/Celeste @ 1b0ce45c75e05649ae91b44a8bb6b196684e4352
    ///   Source/Player/Player.cs
    ///
    /// Important:
    ///   This is not a full drop-in replacement of Celeste.Player. It is an Everest-compatible
    ///   patch/fork layer that intercepts and extends vanilla player behavior while staying
    ///   buildable against the standard Celeste + Everest assembly stack.
    /// </summary>
    public sealed class ForkedPlayerCore
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Upstream state constants — mirrored from NoelFB/Celeste Source/Player/Player.cs
        // These match the vanilla StateMachine state IDs exactly and are used to route
        // behavior without re-defining what each state does at the physics level.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Normal grounded/airborne movement (run, jump, duck).</summary>
        public const int StNormal = 0;

        /// <summary>Wall-climbing state.</summary>
        public const int StClimb = 1;

        /// <summary>Dash / dash-attack state.</summary>
        public const int StDash = 2;

        /// <summary>Swimming state.</summary>
        public const int StSwim = 3;

        /// <summary>Bubble/boost state.</summary>
        public const int StBoost = 4;

        /// <summary>Red-boost dash state.</summary>
        public const int StRedDash = 5;

        /// <summary>Hit-squash state (bounced into ceiling/wall).</summary>
        public const int StHitSquash = 6;

        /// <summary>Launch state (bumpers, launch blocks).</summary>
        public const int StLaunch = 7;

        /// <summary>Picking-up holdable state.</summary>
        public const int StPickup = 8;

        /// <summary>Dream-block dash state.</summary>
        public const int StDreamDash = 9;

        /// <summary>Summit-launch sequence state.</summary>
        public const int StSummitLaunch = 10;

        /// <summary>Dummy/cutscene-controlled state.</summary>
        public const int StDummy = 11;

        /// <summary>Intro walk state.</summary>
        public const int StIntroWalk = 12;

        /// <summary>Intro jump state.</summary>
        public const int StIntroJump = 13;

        /// <summary>Respawn intro state.</summary>
        public const int StIntroRespawn = 14;

        /// <summary>Wake-up intro state.</summary>
        public const int StIntroWakeUp = 15;

        /// <summary>Bird-dash tutorial state.</summary>
        public const int StBirdDashTutorial = 16;

        /// <summary>Frozen (Oshiro etc.) state.</summary>
        public const int StFrozen = 17;

        /// <summary>Reflection chapter fall state.</summary>
        public const int StReflectionFall = 18;

        /// <summary>Feather / star-fly state.</summary>
        public const int StStarFly = 19;

        /// <summary>Temple fall state.</summary>
        public const int StTempleFall = 20;

        /// <summary>Cassette-fly state.</summary>
        public const int StCassetteFly = 21;

        /// <summary>Attract (Core crystal) state.</summary>
        public const int StAttract = 22;

        // ─────────────────────────────────────────────────────────────────────────
        // Core upstream movement constants — from NoelFB/Celeste Player.cs
        // Referenced here so ForkedPlayerCore modifications can stay consistent with
        // vanilla tuning without having to reach back into Player internals.
        // ─────────────────────────────────────────────────────────────────────────

        public const float MaxFall = 160f;
        public const float MaxRun = 90f;
        public const float RunAccel = 1000f;
        public const float JumpSpeed = -105f;
        public const float JumpHBoost = 40f;
        public const float DashSpeed = 240f;
        public const float ClimbMaxStamina = 110f;
        public const float WallSlideStartMax = 20f;

        // ─────────────────────────────────────────────────────────────────────────
        // Hook management
        // ─────────────────────────────────────────────────────────────────────────

        private bool _hooked;
        private readonly PlayerModeRouter _router;

        public ForkedPlayerCore(PlayerModeRouter router)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
        }

        /// <summary>
        /// Install MonoMod On.Celeste hooks.
        /// Called by PlayerExtensionCore.Hook() as part of the module lifecycle.
        /// </summary>
        public void Hook()
        {
            if (_hooked)
                return;

            On.Celeste.Player.Update += OnPlayerUpdate;
            On.Celeste.Player.Die    += OnPlayerDie;

            _hooked = true;
            IngesteLogger.Info("ForkedPlayerCore: Hooks installed.");
        }

        /// <summary>
        /// Remove MonoMod On.Celeste hooks.
        /// Called by PlayerExtensionCore.Unhook() as part of the module lifecycle.
        /// </summary>
        public void Unhook()
        {
            if (!_hooked)
                return;

            On.Celeste.Player.Update -= OnPlayerUpdate;
            On.Celeste.Player.Die    -= OnPlayerDie;

            _hooked = false;
            IngesteLogger.Info("ForkedPlayerCore: Hooks removed.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Hook implementations
        // ─────────────────────────────────────────────────────────────────────────

        private void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Celeste.Player self)
        {
            orig(self);

            if (self?.Scene is not Level level)
                return;

            try
            {
                RouteUpdate(self, level);
            }
            catch (Exception ex)
            {
                IngesteLogger.Warn($"ForkedPlayerCore.OnPlayerUpdate: {ex.Message}");
            }
        }

        private PlayerDeadBody OnPlayerDie(
            On.Celeste.Player.orig_Die orig,
            Celeste.Player self,
            Vector2 direction,
            bool evenIfInvincible,
            bool registerDeathInStats)
        {
            if (_router.GetMode(self) == PlayerRewriteMode.Kirby)
            {
                var result = _router.KirbyPatch.OnPlayerDie(orig, self, direction, evenIfInvincible, registerDeathInStats);
                return result;
            }

            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Per-state routing
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Route per-frame update logic based on the active player mode and current state machine state.
        /// This mirrors the behavioral dispatch ordering of the real NoelFB Player.cs Update method:
        ///   1. Determine which rewrite path owns this frame (Normal or Kirby).
        ///   2. Within that path, dispatch to the correct state handler.
        ///   3. Vanilla Player.Update has already run; this is for mod-specific extensions only.
        /// </summary>
        private void RouteUpdate(Celeste.Player player, Level level)
        {
            var mode = _router.GetMode(player);

            switch (mode)
            {
                case PlayerRewriteMode.Kirby:
                    _router.KirbyPatch.OnUpdate(player, level);
                    break;

                case PlayerRewriteMode.Normal:
                default:
                    RouteNormalUpdate(player, level);
                    break;
            }
        }

        /// <summary>
        /// Normal-mode per-state dispatch.
        /// Follows the same state-ID ordering as upstream Player.cs.
        /// Add mod-specific extensions to each state arm as migration progresses.
        /// </summary>
        private void RouteNormalUpdate(Celeste.Player player, Level level)
        {
            int state = player.StateMachine.State;

            switch (state)
            {
                case StNormal:
                    OnNormalStateUpdate(player, level);
                    break;

                case StDash:
                    OnDashStateUpdate(player, level);
                    break;

                case StClimb:
                    OnClimbStateUpdate(player, level);
                    break;

                case StSwim:
                    OnSwimStateUpdate(player, level);
                    break;

                // All other vanilla states (boost, dream dash, star fly, etc.) pass through
                // unchanged for now. Add migration arms incrementally in later phases.
                default:
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Per-state extension points (normal mode)
        // These methods mirror the naming and intent of upstream Player.cs methods
        // and are the forward-facing extension points for Phase 2 migration work.
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Extension point for normal movement state (StNormal = 0).
        /// Corresponds to upstream Player.NormalUpdate().
        /// Vanilla behavior runs first; add mod-specific overrides here.
        /// </summary>
        private void OnNormalStateUpdate(Celeste.Player player, Level level)
        {
            // Phase 2: port normal-movement modifiers (custom wind, gravity modifiers,
            // extended jump buffering, etc.) here without touching NormalUpdate internals.
        }

        /// <summary>
        /// Extension point for dash state (StDash = 2).
        /// Corresponds to upstream Player.DashUpdate() / DashCoroutine().
        /// Vanilla behavior runs first; add mod-specific overrides here.
        /// </summary>
        private void OnDashStateUpdate(Celeste.Player player, Level level)
        {
            // Phase 2: port custom dash modifiers (directional bias, Kirby pre-dash
            // inhale cancellation logic, etc.) here.
        }

        /// <summary>
        /// Extension point for climb state (StClimb = 1).
        /// Corresponds to upstream Player.ClimbUpdate().
        /// </summary>
        private void OnClimbStateUpdate(Celeste.Player player, Level level)
        {
            // Phase 2: port climb modifiers (stamina overrides for Kirby, etc.) here.
        }

        /// <summary>
        /// Extension point for swim state (StSwim = 3).
        /// Corresponds to upstream Player.SwimUpdate().
        /// </summary>
        private void OnSwimStateUpdate(Celeste.Player player, Level level)
        {
            // Phase 2: port swim modifiers here.
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helper utilities
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True while the player is in the normal movement state.
        /// </summary>
        public static bool IsNormalState(Celeste.Player player) =>
            player?.StateMachine?.State == StNormal;

        /// <summary>
        /// True while the player is in the dash or dash-attack state.
        /// </summary>
        public static bool IsDashState(Celeste.Player player)
        {
            int state = player?.StateMachine?.State ?? -1;
            return state == StDash || player?.DashAttacking == true;
        }

        /// <summary>
        /// True while the player is in a state where Kirby abilities can activate.
        /// (Normal, Dash, Climb — not during cutscene/intro states.)
        /// </summary>
        public static bool IsAbilityEligibleState(Celeste.Player player)
        {
            int state = player?.StateMachine?.State ?? -1;
            return state == StNormal || state == StDash || state == StClimb || state == StSwim;
        }

        /// <summary>
        /// True while the player is in any intro/respawn state that should suppress gameplay behavior.
        /// </summary>
        public static bool IsIntroState(Celeste.Player player)
        {
            int state = player?.StateMachine?.State ?? -1;
            return state == StIntroWalk
                || state == StIntroJump
                || state == StIntroRespawn
                || state == StIntroWakeUp;
        }
    }
}
