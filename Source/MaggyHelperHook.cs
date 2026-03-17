using System.Collections;
using MaggyHelper.Entities;
using MaggyHelper;
using MaggyHelper.Cutscenes;
using Monocle;

namespace MaggyHelper {
    public static class MaggyHelperHooks {
        public static void Load() {
            On.Celeste.LevelEnter.Go += LevelEnterGo;
            On.Celeste.Player.Added += OnPlayerAdded_SwapSprite;

            // Overworld: void moon rendering
            VoidMoonManager.Load();

            // Tape: IL hooks for custom preview audio / sprites
            DesoloZantasTape.Load();
        }

        public static void Unload() {
            On.Celeste.LevelEnter.Go -= LevelEnterGo;
            On.Celeste.Player.Added -= OnPlayerAdded_SwapSprite;

            // Overworld: void moon cleanup
            VoidMoonManager.Unload();

            // Tape: dispose IL hooks
            DesoloZantasTape.Unload();
        }

        /// <summary>
        /// When the player is added to a MaggyHelper level, swap the vanilla
        /// sprite bank entry for our namespaced version so custom art is used
        /// only inside mod levels without overwriting vanilla.
        /// </summary>
        private static void OnPlayerAdded_SwapSprite(
            On.Celeste.Player.orig_Added orig,
            Player self,
            Scene scene)
        {
            orig(self, scene);

            if (scene is Level level)
            {
                var areaData = AreaData.Get(level.Session.Area);
                if (areaData != null && AreaModeExtender.IsOurMap(areaData))
                {
                    string maggyId = self.Sprite.Mode switch
                    {
                        PlayerSpriteMode.Madeline          => "maggy_player",
                        PlayerSpriteMode.MadelineNoBackpack => "maggy_player",
                        PlayerSpriteMode.Playback           => "maggy_player_playback",
                        _ => null
                    };

                    if (maggyId != null && GFX.SpriteBank.Has(maggyId))
                    {
                        GFX.SpriteBank.CreateOn(self.Sprite, maggyId);
                    }
                }
            }
        }

        // ── Shared helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when <paramref name="session"/> is a fresh start of one of
        /// our mod's maps on A-Side (not restored from a save file, started from the
        /// beginning, A-Side only, and belonging to this mod's SID namespace).
        /// SaveData IS valid here — a save slot was chosen before LevelEnter fires.
        /// </summary>
        private static bool IsFreshModStart(Session session, bool fromSaveData)
        {
            if (fromSaveData || !session.StartedFromBeginning) return false;
            if (session.Area.Mode != AreaMode.Normal) return false;
            var area = AreaData.Get(session.Area);
            return area != null && AreaModeExtender.IsOurMap(area);
        }

        /// <summary>
        /// Returns true when <paramref name="session"/> is a fresh start of one of
        /// our mod's maps on the specified mode/side (not restored from a save file,
        /// started from the beginning).
        /// </summary>
        private static bool IsFreshModStartForMode(Session session, bool fromSaveData, int mode)
        {
            if (fromSaveData || !session.StartedFromBeginning) return false;
            if ((int)session.Area.Mode != mode) return false;
            var area = AreaData.Get(session.Area);
            return area != null && AreaModeExtender.IsOurMap(area);
        }

        /// <summary>
        /// Returns true when the session's area SID contains <paramref name="chapterFragment"/>.
        /// Use a unique fragment like "/00_Prologue", "/03_Stars", "/10_Ruins", etc.
        /// </summary>
        private static bool SidContains(Session session, string chapterFragment)
        {
            var area = AreaData.Get(session.Area);
            return area?.SID?.Contains(chapterFragment, StringComparison.OrdinalIgnoreCase) == true;
        }

        // ── Per-vignette predicates ─────────────────────────────────────────────────

        /// <summary>
        /// Show VesselCreationVignette when starting the prologue fresh for the very
        /// first time (HasSeenModIntro == false).  On replay or save-restore, skip it.
        /// </summary>
        private static bool ShouldShowVesselCreationVignette(Session session, bool fromSaveData)
        {
            if (!IsFreshModStart(session, fromSaveData)) return false;
            if (!SidContains(session, "/00_Prologue")) return false;
            // SaveData is valid here – check whether the intro has already been seen.
            if (MaggyHelperModule.SaveData?.HasSeenModIntro == true) return false;
            if (MaggyHelperModule.Settings?.SkipModIntro == true) return false;
            return true;
        }

        /// <summary>
        /// Cs00IntroVignette is launched directly by VesselCreationVignette, so this
        /// LevelEnter path only fires as a fallback (e.g. debug room-warp into prologue).
        /// </summary>
        private static bool ShouldShowPrologueIntroVignette(Session session, bool fromSaveData)
        {
            // Avoid double-trigger: vessel creation already transitions to the intro vignette.
            // Only show if vessel creation was already seen but we somehow re-entered prologue fresh.
            if (!IsFreshModStart(session, fromSaveData)) return false;
            if (!SidContains(session, "/00_Prologue")) return false;
            // If vessel creation wasn't shown (intro already seen), don't show intro again.
            return false;
        }

        private static bool ShouldShowPrologueOutroVignette(Session session, bool fromSaveData)
        {
            // Bridge-ending vignette: triggered by a specific room flag inside the level,
            // not by LevelEnter. Return false here to avoid premature trigger.
            return false;
        }

        private static bool ShouldShowchapter3IntroVignette(Session session, bool fromSaveData)
        {
            return IsFreshModStart(session, fromSaveData)
                && SidContains(session, "/03_Stars");
        }

        private static bool ShouldShowchapter3OutroVignette(Session session, bool fromSaveData)
        {
            // Outro fires from a room trigger inside the map, not from LevelEnter.
            return false;
        }

        private static bool ShouldShowchapter9IntroVignette(Session session, bool fromSaveData)
        {
            if (MaggyHelperModule.SaveData?.HasSeenChapter9IntroVignette == true) return false;

            return IsFreshModStart(session, fromSaveData)
                && SidContains(session, "/09_Summit");
        }

        private static bool ShouldShowchapter10IntroVignette(Session session, bool fromSaveData)
        {
            return IsFreshModStart(session, fromSaveData)
                && SidContains(session, "/10_Ruins");
        }

        private static bool ShouldShowchapterEpilogueIntroVignette(Session session, bool fromSaveData)
        {
            return IsFreshModStart(session, fromSaveData)
                && SidContains(session, "/17_Epilogue");
        }

        private static bool ShouldShowchapterChapter18IntroVignette(Session session, bool fromSaveData)
        {
            return IsFreshModStart(session, fromSaveData)
                && SidContains(session, "/18_Heart");
        }

        private static bool ShouldShowchapterChapter18OutroVignette(Session session, bool fromSaveData)
        {
            // Outro fires from a room trigger inside the map, not from LevelEnter.
            return false;
        }

        private static void LevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData)
        {
            // ── A-Side: custom vignettes ────────────────────────────────────────
            if (ShouldShowVesselCreationVignette(session, fromSaveData))
            {
                // SaveData is valid here (save slot was chosen before LevelEnter fires).
                // Reset all mod progression so the player starts completely fresh.
                MaggyHelperModule.ResetModSaveData();

                Logger.Log(LogLevel.Info, "MaggyHelper",
                    $"[LevelEnterGo] Launching VesselCreationVignette for '{AreaData.Get(session.Area)?.SID}'");
                Engine.Scene = new VesselCreationVignette(session);
            }
            else if (ShouldShowchapter3IntroVignette(session, fromSaveData))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "[LevelEnterGo] Launching Cs03IntroVignette");
                Engine.Scene = new Cs03IntroVignette(session);
            }
            else if (ShouldShowchapter9IntroVignette(session, fromSaveData))
            {
                if (MaggyHelperModule.SaveData != null)
                    MaggyHelperModule.SaveData.HasSeenChapter9IntroVignette = true;

                Logger.Log(LogLevel.Info, "MaggyHelper", "[LevelEnterGo] Launching BeyondSummitVignette");
                Engine.Scene = new BeyondSummitVignette(session);
            }
            else if (ShouldShowchapter10IntroVignette(session, fromSaveData))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "[LevelEnterGo] Launching Cs10IntroVignetteAlt");
                Engine.Scene = new Cs10IntroVignetteAlt(session);
            }
            else if (ShouldShowchapterEpilogueIntroVignette(session, fromSaveData))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "[LevelEnterGo] Launching Cs17EpilogueEndingVignette");
                Engine.Scene = new Cs17EpilogueEndingVignette(session);
            }
            else if (ShouldShowchapterChapter18IntroVignette(session, fromSaveData))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "[LevelEnterGo] Launching Cs18IntroVignette");
                Engine.Scene = new Cs18IntroVignette(session);
            }
            // ── B-Side: VHS intro remix ─────────────────────────────────────────
            else if (IsFreshModStartForMode(session, fromSaveData, AreaModeExtender.MODE_BSIDE))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper",
                    $"[LevelEnterGo] Launching CS_Gen_IntroRemix_BSide for '{AreaData.Get(session.Area)?.SID}'");
                Engine.Scene = new CS_Gen_IntroRemix_BSide(session);
            }
            // ── C-Side: VHS damaged-tape intro remix ────────────────────────────
            else if (IsFreshModStartForMode(session, fromSaveData, AreaModeExtender.MODE_CSIDE))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper",
                    $"[LevelEnterGo] Launching CS_Gen_IntroRemix_CSide for '{AreaData.Get(session.Area)?.SID}'");
                Engine.Scene = new CS_Gen_IntroRemix_CSide(session);
            }
            // ── D-Side: no intro, skip directly to level ────────────────────────
            else if (IsFreshModStartForMode(session, fromSaveData, AreaModeExtender.MODE_DSIDE))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper",
                    $"[LevelEnterGo] D-Side — skipping intro for '{AreaData.Get(session.Area)?.SID}'");
                Engine.Scene = new LevelLoader(session);
            }
            else
            {
                orig(session, fromSaveData);
            }
        }
    }
}