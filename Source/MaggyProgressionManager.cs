using global::MaggyHelper.Extensions.Core;
using MonoMod.Utils;

﻿#pragma warning disable CS0436 // Local patch save types intentionally shadow imported Celeste runtime types.

namespace MaggyHelper;

/// <summary>
/// Central save-driven progression manager for chapter save points, character restore,
/// collectible totals, and unlock recalculation.
/// </summary>
public static class MaggyProgressionManager
{
    private const string UltraCompletionDialog = "POSTCARD_ULTRA_VARIANT_UNLOCK";
    private static bool _hooked;

    public static void Load()
    {
        if (_hooked)
            return;

        _hooked = true;

        On.Celeste.LevelEnter.Go += OnLevelEnterGo;
        On.Celeste.Level.LoadLevel += OnLevelLoadLevel;
        On.Celeste.Player.Die += OnPlayerDie;
        On.Celeste.Overworld.Begin += OnOverworldBegin;

        RefreshProgression();
    }

    public static void Unload()
    {
        if (!_hooked)
            return;

        _hooked = false;

        On.Celeste.LevelEnter.Go -= OnLevelEnterGo;
        On.Celeste.Level.LoadLevel -= OnLevelLoadLevel;
        On.Celeste.Player.Die -= OnPlayerDie;
        On.Celeste.Overworld.Begin -= OnOverworldBegin;
    }

    public static void RecordCheckpoint(Level level, Vector2 position, string checkpointId = null)
    {
        if (!TryGetTrackedChapter(level, out string sid))
            return;

        PlayerCharacter character = ResolveCharacter(level);
        var session = MaggyHelperModule.Session;
        var save = MaggyHelperModule.SaveData;
        if (session == null || save == null)
            return;

        level.Session.RespawnPoint = position;
        session.SetActivePlayerCharacter(character);
        session.HasRegisteredChapterSavePoint = true;
        session.LastCheckpointId = checkpointId ?? string.Empty;

        save.SetPreferredCharacter(sid, character.Id);
        save.SaveChapterRespawn(sid, new SavedChapterRespawnState
        {
            LevelName = level.Session.Level ?? string.Empty,
            RespawnX = position.X,
            RespawnY = position.Y,
            CharacterId = character.Id,
            CheckpointId = checkpointId ?? string.Empty,
            KirbyModeActive = character.IsKirby
        });

        RefreshProgression();
    }

    public static void RecordPreferredCharacter(Level level, string characterId)
    {
        if (!TryGetTrackedChapter(level, out string sid))
            return;

        PlayerCharacter character = PlayerCharacter.FromId(characterId);

        var session = MaggyHelperModule.Session;
        var save = MaggyHelperModule.SaveData;
        if (session == null || save == null)
            return;

        session.SetActivePlayerCharacter(character);
        save.SetPreferredCharacter(sid, character.Id);

        level.Session.SetFlag("kirby_mode", character.IsKirby);
        level.Session.SetFlag("character_kirby", character.IsKirby);
        level.Session.SetFlag("character_madeline", !character.IsKirby);

        RefreshProgression();
    }

    public static void RecordCassette(Level level)
    {
        if (!TryGetTrackedChapter(level, out string sid))
            return;

        MaggyHelperModule.SaveData?.CollectCassette(sid);
        RefreshProgression();
    }

    public static void RecordMiniHeart(Level level, string miniHeartId)
    {
        if (!TryGetTrackedChapter(level, out string sid))
            return;

        string collectibleId = string.IsNullOrWhiteSpace(miniHeartId)
            ? $"{sid}_{level.Session.Level}_miniheart"
            : $"{sid}_{miniHeartId}";

        MaggyHelperModule.SaveData?.CollectMiniHeartGem(collectibleId);
        RefreshProgression();
    }

    public static void RecordPinkPlatinumBerry(Level level, string berryId)
    {
        if (!TryGetTrackedChapter(level, out string sid))
            return;

        string collectibleId = string.IsNullOrWhiteSpace(berryId)
            ? $"{sid}_pink_platinum"
            : berryId;

        MaggyHelperModule.SaveData?.CollectPinkPlatinumBerry(collectibleId);
        RefreshProgression();
    }

    public static void RefreshProgression()
    {
        var modSave = MaggyHelperModule.SaveData;
        if (modSave == null)
            return;

        modSave.TotalTrackedStrawberries = CountTrackedStrawberries(modSave);
        modSave.TotalTrackedHeartGems = HeartGemManager.GetTotalHeartsOverall();
        modSave.TotalTrackedCassettes = CountTrackedCassettes(modSave);
        modSave.TotalTrackedMiniHeartGems = modSave.CollectedMiniHeartGems?.Count ?? 0;
        modSave.TotalTrackedDSideHeartGems = CountDSideHeartGems(modSave);
        modSave.TotalTrackedCollectibles = modSave.TotalTrackedStrawberries
            + modSave.TotalTrackedHeartGems
            + modSave.TotalTrackedCassettes
            + modSave.TotalTrackedMiniHeartGems;

        modSave.AllABCSideHeartsCollected = HaveAllABCSideHearts();
        modSave.DSideHeartGemUnlocked = modSave.AllABCSideHeartsCollected;
        modSave.PinkPlatinumBerryUnlocked = modSave.AllABCSideHeartsCollected;
        modSave.BossRushUnlocked = modSave.TotalBossesDefeated >= 3 || (modSave.DefeatedBosses?.Count ?? 0) >= 3;
        modSave.OneHundredPercentComplete = (SaveData.Instance?.CompletionPercent ?? 0) >= 100;
        modSave.UltraPostcardUnlocked = modSave.OneHundredPercentComplete;
        modSave.FinalDlcContentUnlocked = modSave.VoidMoonUnlocked
            || modSave.Chapter19Complete
            || modSave.PinkPlatinumBerryUnlocked
            || modSave.OneHundredPercentComplete;
    }

    private static void OnLevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData)
    {
        ApplySavedChapterEntryState(session, fromSaveData);

        // Reset lives on a fresh chapter entry so game over music only fires after all lives are spent.
        if (!fromSaveData)
        {
            var modSession = MaggyHelperModule.Session;
            if (modSession != null)
                modSession.LivesRemaining = MaggyHelperModuleSession.MaxLives;
        }

        orig(session, fromSaveData);
    }

    private static void OnLevelLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        orig(self, playerIntro, isFromLoader);

        if (!TryGetTrackedChapter(self, out string sid))
            return;

        string characterId = ResolveRequestedCharacter(sid, self.Session);
        ApplyCharacterState(self, characterId);
        RefreshProgression();
    }

    private static PlayerDeadBody OnPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        Level level = self.Scene as Level;
        if (level?.Session != null)
        {
            RestoreSavedRespawn(level.Session);
        }

        PlayerDeadBody deadBody = orig(self, direction, evenIfInvincible, registerDeathInStats);

        if (deadBody != null)
        {
            var modSession = MaggyHelperModule.Session;
            if (modSession != null)
            {
                modSession.LivesRemaining = Math.Max(0, modSession.LivesRemaining - 1);
                if (modSession.LivesRemaining <= 0)
                {
                    Audio.SetMusic(OverworldMusicManager.MUSIC_GAMEOVER);
                }
            }
            RefreshProgression();
        }

        return deadBody;
    }

    private static void OnOverworldBegin(On.Celeste.Overworld.orig_Begin orig, Overworld self)
    {
        orig(self);

        RefreshProgression();

        var save = MaggyHelperModule.SaveData;
        if (save?.UltraPostcardUnlocked != true || save.HasSeenUltraCompletionPostcard)
            return;

        save.HasSeenUltraCompletionPostcard = true;

        Entity runner = new Entity();
        runner.Add(new Coroutine(ShowUltraCompletionPostcard(self, runner)));
        self.Add(runner);
    }

    private static IEnumerator ShowUltraCompletionPostcard(Scene scene, Entity runner)
    {
        yield return 0.2f;
        yield return PostcardUnlockSystem.ShowUltraCompletionPostcard(scene);
        runner.RemoveSelf();
    }

    private static void ApplySavedChapterEntryState(Session session, bool fromSaveData)
    {
        if (fromSaveData || session == null)
            return;

        AreaData area = AreaData.Get(session.Area);
        string sid = area?.SID;
        if (!AreaModeExtender.IsOurMap(area) || string.IsNullOrWhiteSpace(sid))
            return;

        var modSave = MaggyHelperModule.SaveData;
        var modSession = MaggyHelperModule.Session;
        if (modSave == null || modSession == null)
            return;

        if (modSave.TryGetChapterRespawn(sid, out SavedChapterRespawnState respawnState))
        {
            if (!string.IsNullOrWhiteSpace(respawnState.LevelName))
                session.Level = respawnState.LevelName;

            session.RespawnPoint = respawnState.RespawnPoint;
            modSession.UsedSavedChapterRespawn = true;
            modSession.HasRegisteredChapterSavePoint = true;
            modSession.LastCheckpointId = respawnState.CheckpointId ?? string.Empty;
            ApplyCharacterToSession(modSession, session, respawnState.CharacterId);
            return;
        }

        modSession.UsedSavedChapterRespawn = false;

        if (modSave.TryGetPreferredCharacter(sid, out string characterId))
        {
            ApplyCharacterToSession(modSession, session, characterId);
        }
    }

    private static void RestoreSavedRespawn(Session session)
    {
        AreaData area = AreaData.Get(session.Area);
        string sid = area?.SID;
        if (!AreaModeExtender.IsOurMap(area) || string.IsNullOrWhiteSpace(sid))
            return;

        var modSave = MaggyHelperModule.SaveData;
        if (modSave?.TryGetChapterRespawn(sid, out SavedChapterRespawnState respawnState) != true)
            return;

        if (!string.IsNullOrWhiteSpace(respawnState.LevelName))
            session.Level = respawnState.LevelName;

        session.RespawnPoint = respawnState.RespawnPoint;
    }

    private static void ApplyCharacterState(Level level, string characterId)
    {
        var modSession = MaggyHelperModule.Session;
        if (modSession == null)
            return;

        PlayerCharacter character = PlayerCharacter.FromId(characterId);
        modSession.SetActivePlayerCharacter(character);

        level.Session.SetFlag("kirby_mode", character.IsKirby);
        level.Session.SetFlag("character_kirby", character.IsKirby);
        level.Session.SetFlag("character_madeline", !character.IsKirby);

        LevelStateManager.SetActiveCharacter(character, level);
    }

    private static void ApplyCharacterToSession(MaggyHelperModuleSession modSession, Session session, string characterId)
    {
        PlayerCharacter character = PlayerCharacter.FromId(characterId);
        modSession.SetActivePlayerCharacter(character);

        session.SetFlag("kirby_mode", character.IsKirby);
        session.SetFlag("character_kirby", character.IsKirby);
        session.SetFlag("character_madeline", !character.IsKirby);
    }

    private static string ResolveRequestedCharacter(string sid, Session session)
    {
        return ResolveRequestedPlayerCharacter(sid, session).Id;
    }

    private static PlayerCharacter ResolveRequestedPlayerCharacter(string sid, Session session)
    {
        var modSave = MaggyHelperModule.SaveData;
        if (modSave?.TryGetChapterRespawn(sid, out SavedChapterRespawnState respawnState) == true)
            return PlayerCharacter.FromId(respawnState.CharacterId);

        if (modSave?.TryGetPreferredCharacter(sid, out string preferredCharacter) == true)
            return PlayerCharacter.FromId(preferredCharacter);

        if (MaggyHelperModule.Session?.IsKirbyModeActive == true || session.GetFlag("kirby_mode"))
            return PlayerCharacter.KirbyCharacter;

        return PlayerCharacter.MadelineCharacter;
    }

    private static bool TryGetTrackedChapter(Level level, out string sid)
    {
        sid = null;

        if (level?.Session == null)
            return false;

        sid = AreaData.Get(level.Session.Area)?.SID;
        return !string.IsNullOrWhiteSpace(sid)
            && AreaModeExtender.IsOurMap(AreaData.Get(level.Session.Area));
    }

    private static PlayerCharacter ResolveCharacter(Level level)
    {
        PlayerCharacter? sessionCharacter = MaggyHelperModule.Session?.GetActivePlayerCharacter();
        if (sessionCharacter.HasValue)
            return sessionCharacter.Value;

        if (MaggyHelperModule.Session?.IsKirbyModeActive == true || level.Session.GetFlag("kirby_mode"))
            return PlayerCharacter.KirbyCharacter;

        PlayerCharacter levelStateCharacter = LevelStateManager.GetActivePlayerCharacter();
        if (!string.IsNullOrWhiteSpace(levelStateCharacter.Id))
            return levelStateCharacter;

        return PlayerCharacter.MadelineCharacter;
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return PlayerCharacter.NormalizeId(characterId);
    }

    private static bool IsKirbyCharacter(string characterId)
    {
        return PlayerCharacter.FromId(characterId).IsKirby;
    }

    private static int CountTrackedStrawberries(MaggyHelperModuleSaveData modSave)
    {
        int vanillaTracked = SaveData.Instance?.TotalStrawberries ?? 0;
        int customTracked = (modSave.CollectedBerries?.Count ?? 0)
            + (modSave.CollectedDeltaBerries?.Count ?? 0)
            + (modSave.CollectedPinkPlatinumBerries?.Count ?? 0);

        return Math.Max(vanillaTracked, customTracked);
    }

    private static int CountTrackedCassettes(MaggyHelperModuleSaveData modSave)
    {
        int vanillaTracked = SaveData.Instance?.TotalCassettes ?? 0;
        int customTracked = modSave.CollectedCassettes?.Count ?? 0;
        return Math.Max(vanillaTracked, customTracked);
    }

    private static int CountDSideHeartGems(MaggyHelperModuleSaveData modSave)
    {
        if (modSave.CollectedHeartGems == null)
            return 0;

        string dSideSuffix = "_" + AreaModeExtender.GetModeName(AreaModeExtender.MODE_DSIDE);
        int count = 0;

        foreach (string heartId in modSave.CollectedHeartGems)
        {
            if (!string.IsNullOrWhiteSpace(heartId)
                && heartId.EndsWith(dSideSuffix, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HaveAllABCSideHearts()
    {
        bool foundTrackedArea = false;

        for (int areaId = 0; areaId < AreaData.Areas.Count; areaId++)
        {
            AreaData area = AreaData.Get(areaId);
            if (!AreaModeExtender.IsOurMap(area) || !string.IsNullOrEmpty(GetParentSid(area)))
                continue;

            foundTrackedArea = true;
            int modeCount = area.Mode?.Length ?? 0;

            for (int mode = (int) AreaMode.Normal; mode <= (int) AreaMode.CSide && mode < modeCount; mode++)
            {
                if (!AreaModeExtender.GetSaveAreaModeHeartGem(areaId, mode))
                    return false;
            }
        }

        return foundTrackedArea;
    }

    private static string GetParentSid(AreaData area)
    {
        if (area == null)
            return null;

        DynamicData areaDyn = DynamicData.For(area);
        object meta = null;

        try
        {
            meta = areaDyn.Get<object>("Meta");
        }
        catch
        {
            meta = null;
        }

        if (meta == null)
            return null;

        DynamicData metaDyn = DynamicData.For(meta);

        try
        {
            return metaDyn.Get<string>("ParentSID")
                ?? metaDyn.Get<string>("ParentSid")
                ?? metaDyn.Get<string>("Parent");
        }
        catch
        {
            return null;
        }
    }
}