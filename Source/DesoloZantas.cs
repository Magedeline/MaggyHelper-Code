using MaggyHelper.Entities;
using Monocle;
using MaggyHelper;
using MaggyHelper.Cutscenes; // DesoloZantasVignette base class lives here

namespace MaggyHelper
{
    /// <summary>
    /// Central utility hub for DesoloZantas mod logic.
    /// All vanilla Celeste types are available directly via Celeste.exe reference —
    /// no reflection required for accessing vanilla APIs.
    /// </summary>
    public static class DesoloZantas
    {
        // ─── Vignette / Pause ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the current scene is a pauseable vignette-style scene
        /// (intro cutscenes and vessel creation), then opens the pause menu.
        /// Checks against <see cref="DesoloZantasVignette"/> — the shared base class
        /// all mod vignettes extend — for a compile-time safe match with no reflection.
        /// </summary>
        public static bool PauseAnywhere()
        {
            Scene scene = Engine.Scene;

            // All mod vignettes extend DesoloZantasVignette, so this covers every one
            // without needing string-based type checks or reflection.
            if (scene is not DesoloZantasVignette vignette)
                return false;

            if (!vignette.CanPause)
                return false;

            vignette.OpenMenu();
            return true;
        }

        // ─── Level helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current <see cref="Level"/>, or null if the active scene
        /// is not a gameplay level (e.g. overworld, vignette, title screen).
        /// </summary>
        public static Level CurrentLevel => Engine.Scene as Level;

        /// <summary>
        /// Returns the local player in the current level, or null if unavailable.
        /// Uses the vanilla <see cref="Level.Tracker"/> which is always available
        /// via the Celeste.exe reference.
        /// </summary>
        public static Player CurrentPlayer => CurrentLevel?.Tracker.GetEntity<Player>();

        // ─── Session helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the active gameplay <see cref="Session"/>, or null outside of levels.
        /// </summary>
        public static Session CurrentSession => CurrentLevel?.Session;

        /// <summary>
        /// Returns true when the player is inside one of this mod's own maps.
        /// Delegates to the AreaModeExtender so this check stays in one place.
        /// </summary>
        public static bool IsInOurMap()
        {
            Session session = CurrentSession;
            if (session == null)
                return false;

            AreaData area = AreaData.Get(session.Area);
            return area != null && AreaModeExtender.IsOurMap(area);
        }
    }
}