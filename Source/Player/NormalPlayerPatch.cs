namespace MaggyHelper.PlayerPatch;

/// <summary>
/// Patch layer for the normal (Madeline) player behavior.
///
/// This class is the intended location for all modifications to normal player
/// behavior that are not part of a specific character system (Kirby, Ralsei, etc.).
/// It follows the real-Player.cs architecture established by PlayerPatchCore.
///
/// Current contents: scaffold only — no normal-player overrides active yet.
///
/// Intended future patches (see Docs/PLAYER_PATCH_ARCHITECTURE.md §Next Steps):
///   - Madeline combat system integration points (currently in MadelineCombatSystem.cs)
///   - Custom stamina / dash refill hooks
///   - Extended normal player move-sets (e.g., wall kick variants)
///
/// [UPSTREAM-REF] Upstream Player.cs:
///   https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
/// </summary>
internal sealed class NormalPlayerPatch
{
    private bool _hooked;

    public void Hook()
    {
        if (_hooked)
            return;

        // Future hook points for normal player:
        //   On.Celeste.Player.NormalUpdate += OnNormalUpdate_Madeline;
        //   On.Celeste.Player.Die          += OnDie_Madeline;
        //   On.Celeste.Player.Jump         += OnJump_Madeline;
        //   IL.Celeste.Player.NormalUpdate += ILNormalUpdate_Madeline;

        _hooked = true;
    }

    public void Unhook()
    {
        if (!_hooked)
            return;

        // Future: remove hooks added above.

        _hooked = false;
    }
}
