using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.PlayerPatch;

/// <summary>
/// Physics hook layer for Kirby player, aligned with Player.cs method boundaries.
///
/// This class hooks into specific Player.cs methods to apply Kirby-specific physics
/// modifications at the correct phase of the player update cycle — inside the method
/// that performs the work, rather than as post-Update side effects.
///
/// Current active hooks:
///   On.Celeste.Player.NormalUpdate
///     — Detects hover activation and transitions player to StKirbyHover.
///     — When hover is active but the state hasn't transitioned yet (1-frame gap),
///       this hook bridges the transition within NormalUpdate's return value.
///
/// Future IL hook entry points (see Docs/PLAYER_PATCH_ARCHITECTURE.md §Future IL Patches):
///   IL.Celeste.Player.NormalUpdate — gravity constant injection (MaxFall, Gravity)
///   IL.Celeste.Player.DashUpdate   — Kirby dash speed multiplier
///   IL.Celeste.Player.ClimbUpdate  — disable/modify climb in Kirby mode
///
/// [UPSTREAM-REF] Hook points correspond to methods in:
///   https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
/// </summary>
internal sealed class KirbyPhysicsPatch
{
    private bool _hooked;

    public void Hook()
    {
        if (_hooked)
            return;

        // [UPSTREAM-REF] On.Celeste.Player.NormalUpdate wraps the vanilla NormalUpdate()
        // method. We use the return value to trigger the hover state transition, which
        // is identical to how vanilla NormalUpdate returns StDash, StClimb, etc.
        global::On.Celeste.Player.NormalUpdate += OnNormalUpdate;

        _hooked = true;
    }

    public void Unhook()
    {
        if (!_hooked)
            return;

        global::On.Celeste.Player.NormalUpdate -= OnNormalUpdate;

        _hooked = false;
    }

    // ── NormalUpdate hook ────────────────────────────────────────────────────────
    //
    // [UPSTREAM-REF] This wraps Player.NormalUpdate(), called when StateMachine.State
    // == StNormal (0). Our hook checks whether Kirby hover is beginning and redirects
    // the return value to StKirbyHover, triggering the real state machine transition.
    //
    // This is the same mechanism vanilla uses — NormalUpdate returns StDash (2) when
    // the player presses dash, and the state machine transitions. Our hook does the
    // same for hover.
    //
    // Note: When the player is already in StKirbyHover, NormalUpdate is NOT called
    // (the state machine calls KirbyHoverUpdate instead). This hook only runs for
    // StNormal → StKirbyHover transitions.

    private static int OnNormalUpdate(global::On.Celeste.Player.orig_NormalUpdate orig, CelestePlayer self)
    {
        // Guard: if custom states aren't registered yet, pass through.
        if (PlayerCharacterStates.StKirbyHover < 0)
            return orig(self);

        var ext = GetKirbyExtension(self);
        var hover = ext?.Hover;

        // [MOD-SPECIFIC] If KirbyHoverAbility has set IsHovering = true but the state
        // machine is still StNormal (1-frame transition gap), redirect to StKirbyHover
        // here via the NormalUpdate return value — same pattern vanilla uses for StDash.
        if (hover?.IsHovering == true && ext?.IsDead == false)
        {
            // Run orig() first to let vanilla input/physics run once, then transition.
            // This prevents a single-frame physics glitch on hover entry.
            orig(self);
            return PlayerCharacterStates.StKirbyHover;
        }

        // [MOD-SPECIFIC] If KirbyInhaleAbility is inhaling, redirect to StKirbyInhale.
        if (PlayerCharacterStates.StKirbyInhale >= 0
            && ext?.Inhale?.IsInhaling == true
            && ext?.IsDead == false)
        {
            orig(self);
            return PlayerCharacterStates.StKirbyInhale;
        }

        // Default: run vanilla NormalUpdate.
        return orig(self);
    }

    // ── IL hook stubs (future migration) ────────────────────────────────────────
    //
    // These are documented entry points for future IL-level physics modifications.
    // When implemented, they inject Kirby-specific gravity/speed constants directly
    // into NormalUpdate's IL stream, avoiding the need to fight vanilla physics in
    // a post-Update pass. Prefer IL hooks over On hooks for physics constants because
    // they run at exactly the right instruction, not around the full method.
    //
    // Example (not yet implemented):
    //
    //   private static void ILNormalUpdate_KirbyGravity(ILContext il)
    //   {
    //       // Locate the Gravity constant load in the gravity section:
    //       //   ldc.r4 900  (Gravity)
    //       //   ...
    //       //   call Calc.Approach
    //       // Replace with a conditional that substitutes HoverGravity when active.
    //   }
    //
    // See Docs/PLAYER_PATCH_ARCHITECTURE.md §Future IL Patches for step-by-step guide.

    private static KirbyPlayerExtension GetKirbyExtension(CelestePlayer player)
        => (player?.Scene as Level)?.Tracker.GetEntity<KirbyPlayerExtension>();
}
