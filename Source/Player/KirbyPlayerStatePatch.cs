using MaggyHelper.Extensions.Kirby;
using MaggyHelper.Extensions.Kirby.Core;

namespace MaggyHelper.PlayerPatch;

/// <summary>
/// Registers Kirby-specific states into the real Celeste Player.StateMachine.
///
/// Each state is registered via StateMachine.AddState(), which is the same mechanism
/// Celeste itself uses for all its built-in states (StNormal, StDash, StClimb, etc.).
/// This means our states are first-class citizens of the real Player.cs state flow —
/// correct entry/exit semantics, correct priority, and correct MoveH/MoveV dispatch.
///
/// Registered states and their purposes:
///   StKirbyHover  — Kirby float/hover: reduced gravity, flap jumps, full horizontal movement.
///   StKirbyInhale — Kirby inhale: restricted movement (walk-only), pull cone active.
///   StKirbySlide  — Kirby dash-slide: directional speed burst, can deal contact damage.
///
/// State IDs are assigned at runtime by StateMachine.AddState() and persisted in
/// PlayerCharacterStates (accessible across the mod) after the first registration.
///
/// How state updates interact with vanilla Player.Update():
///   Player.Update() calls StateMachine.Update(), which dispatches to our state's
///   onUpdate delegate. The onUpdate sets player.Speed and returns the next state ID.
///   Player.Update() then calls MoveH(Speed.X * dt) and MoveY(Speed.Y * dt) — so our
///   states only need to set Speed; movement and collision are handled by vanilla.
///
/// [UPSTREAM-REF] State machine structure based on Celeste's real Player.cs:
///   https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
///   Vanilla states: StNormal=0 ... StIntroThinkForABit=24
///   Our states start at 25 in registration order.
/// </summary>
internal sealed class KirbyPlayerStatePatch
{
    // ── Physics constants mirrored from upstream Player.cs ─────────────────────
    // [UPSTREAM-REF] https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
    // Keep these in sync if Celeste updates Player.cs physics constants.
    private const float UpstreamMaxRun          = 90f;    // Player.MaxRun (public)
    private const float UpstreamRunAccel        = 1000f;  // Player.RunAccel (public)
    private const float UpstreamAirMult         = 0.65f;  // Player.AirMult (private)
    private const float UpstreamGravity         = 900f;   // Player.Gravity (private)
    private const float UpstreamHalfGravThresh  = 40f;    // Player.HalfGravThreshold (private)
    private const float UpstreamMaxFall         = 160f;   // Player.MaxFall (public)

    // ── Kirby slide state tuning ───────────────────────────────────────────────
    // [MOD-SPECIFIC] These are not in upstream Player.cs.
    private const float SlideDuration = 0.25f;  // Slide state duration in seconds
    private const float SlideSpeed    = 200f;   // Slide horizontal speed (< Dash 240f)

    // ── State ID tracking ──────────────────────────────────────────────────────
    // All Player instances register states in the same order, so the IDs are identical.
    // We record them once from the first registration.
    private static bool _stateIdsRecorded;

    // Per-state instance data (needed for slide direction/timer across Begin/Update).
    // Each Player instance gets its own StatePatch closures, so these fields are
    // safe to use even if two Player instances coexist (e.g., multiplayer mods).
    private float _slideTimer;
    private Vector2 _slideDir;

    // ──────────────────────────────────────────────────────────────────────────
    // Registration
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called from PlayerPatchCore.OnPlayerAdded for each new Player instance.
    /// Registers Kirby custom states into the Player's StateMachine and stores
    /// the assigned IDs in PlayerCharacterStates.
    /// </summary>
    public void RegisterStates(CelestePlayer player)
    {
        // ── StKirbyHover ──────────────────────────────────────────────────────
        // [MOD-SPECIFIC] Float state: reduced gravity, flap jumps.
        // Horizontal movement mirrors NormalUpdate (UpstreamMaxRun, UpstreamRunAccel).
        // Gravity replaced with HoverGravity / HoverFallSpeed from KirbySettings.
        int hoverId = player.StateMachine.AddState(
            "MaggyHelper_KirbyHover",
            () => KirbyHoverUpdate(player),
            null,
            () => KirbyHoverBegin(player),
            () => KirbyHoverEnd(player)
        );

        // ── StKirbyInhale ─────────────────────────────────────────────────────
        // [MOD-SPECIFIC] Inhale state: restricted walk-only movement.
        // Pull cone logic lives in KirbyInhaleAbility; this state manages movement.
        int inhaleId = player.StateMachine.AddState(
            "MaggyHelper_KirbyInhale",
            () => KirbyInhaleUpdate(player),
            null,
            () => KirbyInhaleBegin(player),
            () => KirbyInhaleEnd(player)
        );

        // ── StKirbySlide ──────────────────────────────────────────────────────
        // [MOD-SPECIFIC] Directional burst / slide state.
        // Previously referenced as a placeholder const StKirbySlide = 100 (never reached).
        // Now a real registered state — OnCollideH in KirbyPlayerExtensionCore
        // correctly detects it for enemy contact damage logic.
        // Pattern mirrors vanilla DashUpdate: set Speed = dir * speed each frame.
        int slideId = player.StateMachine.AddState(
            "MaggyHelper_KirbySlide",
            () => KirbySlideUpdate(player),
            null,
            () => KirbySlideBegin(player),
            () => KirbySlideEnd(player)
        );

        // Record state IDs on first registration only.
        // All Player instances start with the same vanilla states 0-24, so the
        // IDs assigned to our states are identical across all instances.
        if (!_stateIdsRecorded)
        {
            PlayerCharacterStates.SetKirbyStateIds(hoverId, inhaleId, slideId);
            _stateIdsRecorded = true;
            IngesteLogger.Info(
                $"PlayerPatchCore: Kirby states registered in real StateMachine — " +
                $"Hover={hoverId}, Inhale={inhaleId}, Slide={slideId}.");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static KirbyPlayerExtension GetKirbyExtension(CelestePlayer player)
        => (player?.Scene as Level)?.Tracker.GetEntity<KirbyPlayerExtension>();

    private static bool IsKirbyActive(CelestePlayer player)
    {
        var ext = GetKirbyExtension(player);
        return ext != null && !ext.IsDead;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StKirbyHover — Float / hover state
    // ──────────────────────────────────────────────────────────────────────────
    //
    // [MOD-SPECIFIC] Replaces the normal gravity path while Kirby is floating.
    //
    // [UPSTREAM-REF] Horizontal movement structure mirrors Player.NormalUpdate():
    //   float targetX = Input.MoveX.Value * MaxRun;
    //   Speed.X = Calc.Approach(Speed.X, targetX, accel * Engine.DeltaTime);
    //
    // [UPSTREAM-REF] Gravity section replaced:
    //   Vanilla: Speed.Y = Calc.Approach(Speed.Y, MaxFall, Gravity * mult * dt)
    //   Hover:   Speed.Y = Calc.Approach(Speed.Y, HoverFallSpeed, HoverGravity * mult * dt)
    //
    // Player.Update() will call MoveH/MoveY after this returns — we only set Speed.

    private static int KirbyHoverUpdate(CelestePlayer player)
    {
        var ext = GetKirbyExtension(player);

        // Exit hover state if the ability ended or mode disabled.
        if (ext?.Hover == null || !ext.Hover.IsHovering || ext.IsDead)
            return CelestePlayer.StNormal;

        // [UPSTREAM-REF] Horizontal movement (mirrors NormalUpdate horizontal section).
        float targetSpeedX = Input.MoveX.Value * UpstreamMaxRun;
        float accel = UpstreamRunAccel * (player.OnGround() ? 1f : UpstreamAirMult);
        player.Speed.X = Calc.Approach(player.Speed.X, targetSpeedX, accel * Engine.DeltaTime);

        // [MOD-SPECIFIC] Hover gravity — approaches HoverFallSpeed instead of MaxFall.
        // HoverFallSpeed (60f default) << MaxFall (160f), creating the floaty effect.
        var settings = ext.Settings;
        float gravMult = Math.Abs(player.Speed.Y) < UpstreamHalfGravThresh ? 0.5f : 1f;
        player.Speed.Y = Calc.Approach(
            player.Speed.Y,
            settings.HoverFallSpeed,
            settings.HoverGravity * gravMult * Engine.DeltaTime
        );

        // Stay in hover state.
        return PlayerCharacterStates.StKirbyHover;
    }

    private static void KirbyHoverBegin(CelestePlayer player)
    {
        // [MOD-SPECIFIC] State entry point.
        // KirbyHoverAbility manages its own IsHovering flag, audio, and FX.
        // The state machine entry here is a structural acknowledgment only.
        IngesteLogger.Debug("PlayerPatchCore: Entered StKirbyHover.");
    }

    private static void KirbyHoverEnd(CelestePlayer player)
    {
        // [MOD-SPECIFIC] Ensure ability is cancelled if state exits from an external
        // source (e.g., a dash cancels hover, or player enters climb state).
        var ext = GetKirbyExtension(player);
        if (ext?.Hover?.IsHovering == true)
            ext.Hover.Cancel();

        IngesteLogger.Debug("PlayerPatchCore: Exited StKirbyHover.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StKirbyInhale — Inhale state
    // ──────────────────────────────────────────────────────────────────────────
    //
    // [MOD-SPECIFIC] Restricted movement during inhale. Walk-only, no jump or dash.
    // Pull cone and swallow logic delegated to KirbyInhaleAbility (unchanged).
    //
    // [UPSTREAM-REF] Horizontal movement uses a subset of NormalUpdate, at 50% speed.
    //   Normal MaxRun = 90f; inhale walk = 45f.
    //   Gravity is vanilla (900f toward MaxFall 160f) — Kirby stays grounded while inhaling.

    private static int KirbyInhaleUpdate(CelestePlayer player)
    {
        var ext = GetKirbyExtension(player);

        // Exit if inhale ended or mode disabled.
        if (ext?.Inhale == null || !ext.Inhale.IsInhaling || ext.IsDead)
            return CelestePlayer.StNormal;

        // [MOD-SPECIFIC] Walk-only movement at 50% MaxRun during inhale.
        // Prevents dashing while inhaling; ability already blocks Jump input.
        float walkSpeed = UpstreamMaxRun * 0.5f;
        float targetX = Input.MoveX.Value * walkSpeed;
        player.Speed.X = Calc.Approach(player.Speed.X, targetX, UpstreamRunAccel * Engine.DeltaTime);

        // [UPSTREAM-REF] Vanilla gravity during inhale (NormalUpdate gravity section).
        float gravMult = Math.Abs(player.Speed.Y) < UpstreamHalfGravThresh ? 0.5f : 1f;
        player.Speed.Y = Calc.Approach(
            player.Speed.Y,
            UpstreamMaxFall,
            UpstreamGravity * gravMult * Engine.DeltaTime
        );

        return PlayerCharacterStates.StKirbyInhale;
    }

    private static void KirbyInhaleBegin(CelestePlayer player)
    {
        IngesteLogger.Debug("PlayerPatchCore: Entered StKirbyInhale.");
    }

    private static void KirbyInhaleEnd(CelestePlayer player)
    {
        // Ensure the ability is stopped if the state exits externally.
        var ext = GetKirbyExtension(player);
        if (ext?.Inhale?.IsInhaling == true)
            ext.Inhale.StopInhale();

        IngesteLogger.Debug("PlayerPatchCore: Exited StKirbyInhale.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StKirbySlide — Directional burst / slide state
    // ──────────────────────────────────────────────────────────────────────────
    //
    // [MOD-SPECIFIC] Short directional burst (200 px/s for 0.25 s).
    //
    // [UPSTREAM-REF] Pattern mirrors Player.cs DashUpdate():
    //   Speed = DashDir * DashSpeed each frame; Player.Update handles MoveH/MoveY.
    //
    // Previously registered as const StKirbySlide = 100, which was never a real state
    // (state 100 doesn't exist in vanilla and was never set). The collision handler in
    // KirbyPlayerExtensionCore.PlayerOnCollideH checked for this state — that check now
    // works correctly because this is a real registered state.

    private int KirbySlideUpdate(CelestePlayer player)
    {
        var ext = GetKirbyExtension(player);
        if (ext == null || ext.IsDead)
            return CelestePlayer.StNormal;

        _slideTimer -= Engine.DeltaTime;
        if (_slideTimer <= 0f)
            return CelestePlayer.StNormal;

        // [UPSTREAM-REF] Directional burst, pattern from DashUpdate: Speed = dir * speed.
        // Horizontal speed only (no vertical burst for Kirby slide, unlike Celeste dash).
        player.Speed.X = _slideDir.X * SlideSpeed * ext.Settings.SlideSpeedMultiplier;
        // Gravity still applies to Y (no zero-gravity during slide, unlike dash).

        return PlayerCharacterStates.StKirbySlide;
    }

    private void KirbySlideBegin(CelestePlayer player)
    {
        // [MOD-SPECIFIC] Capture facing direction at slide start.
        _slideDir = player.Facing == Facings.Left ? -Vector2.UnitX : Vector2.UnitX;
        _slideTimer = SlideDuration;
        IngesteLogger.Debug($"PlayerPatchCore: Entered StKirbySlide dir={_slideDir}.");
    }

    private static void KirbySlideEnd(CelestePlayer player)
    {
        IngesteLogger.Debug("PlayerPatchCore: Exited StKirbySlide.");
    }
}
