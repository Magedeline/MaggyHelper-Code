namespace MaggyHelper;

using System.Reflection;

/// <summary>
/// Lightweight validation panel for the major custom systems in this mod.
/// Run in Celeste console: maggy_validate [quick|full]
/// </summary>
public static class FeatureValidationCommands
{
    private sealed class CheckResult
    {
        public string Name { get; }
        public bool Passed { get; }
        public string Detail { get; }

        public CheckResult(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
        }
    }

    [Command("maggy_validate", "Validates core mod systems. Usage: maggy_validate [quick|full]")]
    private static void CmdValidate(string mode = "quick")
    {
        bool full = string.Equals(mode?.Trim(), "full", StringComparison.OrdinalIgnoreCase);
        List<CheckResult> checks = BuildChecks(full);

        int passCount = checks.Count(c => c.Passed);
        int failCount = checks.Count - passCount;

        Log("[MaggyHelper] ========================================");
        Log($"[MaggyHelper] Feature Validation Panel ({(full ? "full" : "quick")})");
        Log($"[MaggyHelper] Result: {passCount} passed / {failCount} failed");

        foreach (CheckResult c in checks)
        {
            string status = c.Passed ? "PASS" : "FAIL";
            Log($"[MaggyHelper] [{status}] {c.Name} - {c.Detail}");
        }

        if (failCount > 0)
        {
            Log("[MaggyHelper] Some checks failed. Run maggy_validate full for deeper diagnostics.");
        }

        Log("[MaggyHelper] ========================================");
    }

    [Command("maggy_validate_state", "Prints current runtime state for fast triage")]
    private static void CmdValidateState()
    {
        Level level = Engine.Scene as Level;
        Session session = level?.Session;

        string sid = session?.Area.GetSID() ?? "<none>";
        string mode = session != null ? session.Area.Mode.ToString() : "<none>";

        Log("[MaggyHelper] -------- Runtime State --------");
        Log($"[MaggyHelper] Scene={Engine.Scene?.GetType().Name ?? "<null>"}");
        Log($"[MaggyHelper] AreaSID={sid}");
        Log($"[MaggyHelper] AreaMode={mode}");
        Log($"[MaggyHelper] InMaggyMap={MaggyHelperModule.IsInMaggyHelperMap()}");

        MaggyHelperModuleSession modSession = MaggyHelperModule.Session;
        MaggyHelperModuleSaveData save = MaggyHelperModule.SaveData;

        Log($"[MaggyHelper] BossFightActive={modSession?.BossFightActive}");
        Log($"[MaggyHelper] CurrentBossName={modSession?.CurrentBossName ?? "<none>"}");
        Log($"[MaggyHelper] CurrentCopyAbility={modSession?.CurrentCopyAbility?.ToString() ?? "<none>"}");
        Log($"[MaggyHelper] HeartsTracked={save?.CollectedHeartGems?.Count ?? 0}");
        Log($"[MaggyHelper] ChaptersUnlocked={save?.UnlockedChapters?.Count ?? 0}");
        Log("[MaggyHelper] -------------------------------");
    }

    [Command("maggy_validate_room", "Validates the currently loaded room/side flow while in-level")]
    private static void CmdValidateRoom()
    {
        Level level = Engine.Scene as Level;
        if (level?.Session == null)
        {
            Log("[MaggyHelper] [FAIL] Room Validation - Not currently in a Level scene.");
            return;
        }

        Session session = level.Session;
        AreaData areaData = AreaData.Get(session.Area);
        string sid = session.Area.GetSID() ?? "<none>";
        int mode = (int) session.Area.Mode;

        Log("[MaggyHelper] -------- Room Validation --------");
        Log($"[MaggyHelper] SID={sid}");
        Log($"[MaggyHelper] Mode={mode} ({AreaModeExtender.GetModeName(mode)})");
        Log($"[MaggyHelper] IsOurMap={AreaModeExtender.IsOurMap(areaData)}");

        AreaMapData.ChapterDef chapterDef = AreaMapData.FindByAnySID(sid);
        bool chapterRegistered = chapterDef != null;
        Log($"[MaggyHelper] ChapterRegistered={chapterRegistered}");
        if (chapterRegistered)
        {
            Log($"[MaggyHelper] ChapterNumber={chapterDef.Number}, Name={chapterDef.Name}");
        }

        bool heartCollected = HeartGemManager.IsHeartGemCollected(session);
        Log($"[MaggyHelper] HeartCollectedForThisSide={heartCollected}");

        if (AreaModeExtender.IsOurMap(areaData) && mode >= AreaModeExtender.MODE_BSIDE && mode < AreaModeExtender.TOTAL_MODES)
        {
            var unlockConfig = PostcardUnlockSystem.GetUnlockConfig(mode);
            bool expectsPostcard = unlockConfig != null;
            Log($"[MaggyHelper] ExpectsUnlockPostcardAfterClear={expectsPostcard}");

            if (unlockConfig != null)
            {
                bool hasTexture = string.IsNullOrWhiteSpace(unlockConfig.TexturePath) || GFX.Gui.Has(unlockConfig.TexturePath);
                bool hasSfxIn = !string.IsNullOrWhiteSpace(unlockConfig.SfxIn);
                bool hasSfxOut = !string.IsNullOrWhiteSpace(unlockConfig.SfxOut);

                string status = hasTexture && hasSfxIn && hasSfxOut ? "PASS" : "WARN";
                Log($"[MaggyHelper] [{status}] PostcardConfig texture={hasTexture} sfxIn={hasSfxIn} sfxOut={hasSfxOut}");
            }
        }

        MaggyHelperModuleSession modSession = MaggyHelperModule.Session;
        MaggyHelperModuleSaveData save = MaggyHelperModule.SaveData;

        if (modSession != null)
        {
            Log($"[MaggyHelper] BossFightActive={modSession.BossFightActive}");
            Log($"[MaggyHelper] CurrentBossName={modSession.CurrentBossName ?? "<none>"}");
            Log($"[MaggyHelper] CurrentCopyAbility={modSession.CurrentCopyAbility?.ToString() ?? "<none>"}");
        }

        if (save != null)
        {
            Log($"[MaggyHelper] HeartsTrackedTotal={save.CollectedHeartGems?.Count ?? 0}");
            Log($"[MaggyHelper] ChaptersUnlockedTotal={save.UnlockedChapters?.Count ?? 0}");

            if (!string.IsNullOrWhiteSpace(sid) && mode + 1 < AreaModeExtender.TOTAL_MODES)
            {
                string nextUnlockKey = $"{sid}_{AreaModeExtender.GetModeName(mode + 1)}_unlocked";
                Log($"[MaggyHelper] NextSideUnlockAchievement={save.HasAchievement(nextUnlockKey)} ({nextUnlockKey})");
            }
        }

        bool hasRemixHookTypes = typeof(IntroRemixHooks) != null &&
                     typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_BSide) != null &&
                     typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_CSide) != null;
        Log($"[MaggyHelper] IntroRemixTypesPresent={hasRemixHookTypes}");
        Log("[MaggyHelper] -------------------------------");
    }

    private static List<CheckResult> BuildChecks(bool full)
    {
        List<CheckResult> checks = new();

        checks.Add(CheckAreaMapData(full));
        checks.Add(CheckHeartGemCustomization(full));
        checks.Add(CheckAreaCompleteCredits(full));
        checks.Add(CheckBossIntegration(full));
        checks.Add(CheckInventoryExtension(full));
        checks.Add(CheckIntroRemixVignette(full));
        checks.Add(CheckPostcardExtension(full));

        return checks;
    }

    private static CheckResult CheckAreaMapData(bool full)
    {
        bool hasChapters = AreaMapData.Chapters != null && AreaMapData.Chapters.Count > 0;
        if (!hasChapters)
        {
            return new CheckResult("AreaMapDataExt", false, "No registered chapters.");
        }

        if (!full)
        {
            return new CheckResult("AreaMapDataExt", true, $"Chapters={AreaMapData.Chapters.Count}");
        }

        bool metadataOk = AreaMapData.Chapters.All(c =>
            !string.IsNullOrWhiteSpace(c.SID) &&
            c.MusicEvents != null && c.MusicEvents.Length > 0 &&
            c.MountainData != null);

        return new CheckResult(
            "AreaMapDataExt",
            metadataOk,
            metadataOk ? "All chapters have SID/music/mountain metadata." : "Some chapters are missing SID/music/mountain metadata.");
    }

    private static CheckResult CheckHeartGemCustomization(bool full)
    {
        bool arraysOk = HeartGemManager.HeartColors.Length >= 5 && HeartGemManager.HeartSpriteIds.Length >= 5;
        if (!arraysOk)
        {
            return new CheckResult("HeartGem Custom Sprite+SFX", false, "Heart color/sprite arrays are incomplete.");
        }

        if (!full)
        {
            return new CheckResult("HeartGem Custom Sprite+SFX", true, "Heart arrays are configured for extended sides.");
        }

        bool soundsOk = AreaModeExtender.HeartGemGetSounds != null &&
                        AreaModeExtender.HeartGemGetSounds.Length >= AreaModeExtender.TOTAL_MODES;

        return new CheckResult(
            "HeartGem Custom Sprite+SFX",
            soundsOk,
            soundsOk ? "Heart collection SFX table covers all modes." : "Heart collection SFX table is shorter than TOTAL_MODES.");
    }

    private static CheckResult CheckAreaCompleteCredits(bool full)
    {
        Type t = typeof(AreaComplete);
        bool hasInit = t.GetMethod("InitAreaCompleteInfoForEverest2", BindingFlags.Public | BindingFlags.Static) != null;
        bool hasUpdate = t.GetMethod("Orig_Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        bool ok = hasInit && hasUpdate;

        if (!full)
        {
            return new CheckResult("Credits After AreaComplete", ok, ok ? "AreaComplete extension methods found." : "Expected AreaComplete extension methods missing.");
        }

        bool hasCreditsSid = AreaMapData.Chapters.Any(c => c.Number == 17 || c.Number == 20);
        return new CheckResult(
            "Credits After AreaComplete",
            ok && hasCreditsSid,
            ok && hasCreditsSid ? "AreaComplete hooks and late-game chapter definitions are present." : "Missing AreaComplete hooks or expected late-game chapters.");
    }

    private static CheckResult CheckBossIntegration(bool full)
    {
        MaggyHelperModuleSession session = MaggyHelperModule.Session;
        MaggyHelperModuleSaveData save = MaggyHelperModule.SaveData;

        bool hasState = session != null && save != null;
        if (!hasState)
        {
            return new CheckResult("Integrated Boss Fight", false, "Module session/save data unavailable.");
        }

        if (!full)
        {
            return new CheckResult("Integrated Boss Fight", true, "Session/save boss state containers are available.");
        }

        bool fieldsOk = save.DefeatedBosses != null && save.TotalBossesDefeated >= 0 && session.BossesDefeated >= 0;
        return new CheckResult(
            "Integrated Boss Fight",
            fieldsOk,
            fieldsOk ? "Boss persistence and runtime counters are valid." : "Boss counters/dictionaries are not initialized.");
    }

    private static CheckResult CheckInventoryExtension(bool full)
    {
        MaggyHelperModuleSession session = MaggyHelperModule.Session;
        MaggyHelperModuleSaveData save = MaggyHelperModule.SaveData;

        bool hasContainers = session != null && save != null;
        if (!hasContainers)
        {
            return new CheckResult("Player Inventory Ext", false, "Module session/save data unavailable.");
        }

        if (!full)
        {
            return new CheckResult("Player Inventory Ext", true, "Session + save inventory containers are available.");
        }

        bool runtimeOk = session.CustomFlags != null && save.UnlockedKirbyPowers != null;
        return new CheckResult(
            "Player Inventory Ext",
            runtimeOk,
            runtimeOk ? "Runtime flags and persistent unlock collections are initialized." : "Inventory-related collections are not initialized.");
    }

    private static CheckResult CheckIntroRemixVignette(bool full)
    {
        bool hasB = typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_BSide) != null;
        bool hasC = typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_CSide) != null;
        bool hasHooks = typeof(IntroRemixHooks) != null;

        bool ok = hasB && hasC && hasHooks;
        if (!full)
        {
            return new CheckResult("Intro Remix Vignette", ok, ok ? "B/C-side intro vignette scenes and hooks are present." : "Missing B/C-side intro vignette scene or hook type.");
        }

        bool routinesOk = typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_BSide)
            .GetMethod("VHSRemixRoutine", BindingFlags.NonPublic | BindingFlags.Instance) != null
            && typeof(global::MaggyHelper.Cutscenes.CS_Gen_IntroRemix_CSide)
            .GetMethod("VHSDamagedTapeRoutine", BindingFlags.NonPublic | BindingFlags.Instance) != null;

        return new CheckResult(
            "Intro Remix Vignette",
            ok && routinesOk,
            ok && routinesOk ? "Intro routines are discoverable by reflection." : "Intro routines were not found (method rename/regression suspected).");
    }

    private static CheckResult CheckPostcardExtension(bool full)
    {
        bool configsOk = PostcardUnlockSystem.CSideConfig != null
            && PostcardUnlockSystem.DSideConfig != null
            && PostcardUnlockSystem.DXSideConfig != null;

        if (!configsOk)
        {
            return new CheckResult("PostcardExt", false, "Unlock postcard configs are missing.");
        }

        if (!full)
        {
            return new CheckResult("PostcardExt", true, "Unlock postcard configs are present.");
        }

        bool parserOk = typeof(PostcardMaggy)
            .GetMethod("GetSoundEventBase", BindingFlags.NonPublic | BindingFlags.Static) != null;

        return new CheckResult(
            "PostcardExt",
            parserOk,
            parserOk ? "Custom postcard sound parser is present." : "Custom postcard sound parser method not found.");
    }

    private static void Log(string message)
    {
        if (Engine.Commands != null)
        {
            Engine.Commands.Log(message);
            return;
        }

        Logger.Log(LogLevel.Info, "MaggyHelper", message);
    }
}