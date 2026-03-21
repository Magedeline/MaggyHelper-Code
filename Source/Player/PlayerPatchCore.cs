using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.PlayerPatch;

/// <summary>
/// Central coordinator for real-Player.cs-based patches and state machine integration.
///
/// Architecture intent:
/// ─────────────────────────────────────────────────────────────────────────────────
/// This class is the authoritative entry point for all modifications to Celeste's
/// vanilla Player.cs behavior in this mod. It sits parallel to (not inside) the
/// extension/actor overlay system, integrating directly with Player's real state
/// machine and physics methods.
///
/// Unlike the existing KirbyPlayerExtensionCore (which attaches side-effects via
/// On.Celeste.Player.* hooks that run around orig()), this layer:
///   1. Registers custom states in the actual Player.StateMachine via AddState(),
///      so Kirby/custom states are first-class citizens of the real state machine.
///   2. Provides physics hook points aligned with Player.cs method boundaries
///      (NormalUpdate, DashUpdate, etc.) rather than post-Update patches.
///   3. Acts as the single registry for all patches that modify player core behavior.
///
/// Upstream reference:
///   https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs
///
/// Migration status: See Docs/PLAYER_PATCH_ARCHITECTURE.md
/// ─────────────────────────────────────────────────────────────────────────────────
/// </summary>
public static class PlayerPatchCore
{
    private static bool _initialized;

    // Sub-patch objects owned by this core.
    // [MOD-SPECIFIC] These are not part of upstream Player.cs — they are our additions.
    private static KirbyPlayerStatePatch _kirbyStatePatch;
    private static KirbyPhysicsPatch _kirbyPhysicsPatch;
    private static NormalPlayerPatch _normalPlayerPatch;

    /// <summary>
    /// Whether the patch layer has been initialized.
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Initialize the real-Player.cs patch layer.
    /// Called from PlayerExtensionCore.Hook() during module load.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        IngesteLogger.Info("PlayerPatchCore: Initializing real-Player.cs patch layer.");

        _kirbyStatePatch = new KirbyPlayerStatePatch();
        _kirbyPhysicsPatch = new KirbyPhysicsPatch();
        _normalPlayerPatch = new NormalPlayerPatch();

        // Primary integration point with real Player.cs:
        // Hook Player.Added to inject our custom states into each new Player instance's
        // StateMachine. This must happen AFTER orig() so vanilla states 0-24 are present.
        global::On.Celeste.Player.Added += OnPlayerAdded;

        _kirbyPhysicsPatch.Hook();
        _normalPlayerPatch.Hook();

        _initialized = true;
        IngesteLogger.Info("PlayerPatchCore: Initialized. Custom states registered on Player.Added.");
    }

    /// <summary>
    /// Uninitialize the patch layer and remove all hooks.
    /// Called from PlayerExtensionCore.Unhook() during module unload.
    /// </summary>
    public static void Uninitialize()
    {
        if (!_initialized)
            return;

        global::On.Celeste.Player.Added -= OnPlayerAdded;

        _kirbyPhysicsPatch?.Unhook();
        _normalPlayerPatch?.Unhook();

        _kirbyStatePatch = null;
        _kirbyPhysicsPatch = null;
        _normalPlayerPatch = null;

        _initialized = false;
        IngesteLogger.Info("PlayerPatchCore: Uninitialized.");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Player.Added hook — primary real-Player.cs integration point
    // ──────────────────────────────────────────────────────────────────────────────

    private static void OnPlayerAdded(global::On.Celeste.Player.orig_Added orig, CelestePlayer self, Scene scene)
    {
        // Call orig() first so vanilla states (0–24) are present in the StateMachine.
        orig(self, scene);

        // Register all mod-specific custom states into the real Player StateMachine.
        // Each registration returns the actual state ID, stored in PlayerCharacterStates.
        _kirbyStatePatch?.RegisterStates(self);

        IngesteLogger.Debug($"PlayerPatchCore: Custom states registered for Player @ {self.Position}.");
    }
}
