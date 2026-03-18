#pragma warning disable CS0436 // Local patch save types intentionally shadow imported Celeste runtime types.

namespace MaggyHelper;

/// <summary>
/// Small adapter over vanilla <see cref="SaveData"/> plus Maggy's module save data.
/// Keeps mixed save operations in one place so feature code does not need to know
/// which backend owns a given piece of persistence.
/// </summary>
public static class MaggySaveFacade
{
    public static SaveData Vanilla => SaveData.Instance;

    public static MaggyHelperModuleSaveData Mod => MaggyHelperModule.SaveData;

    public static bool HasVanillaSave => Vanilla != null;

    public static bool HasModSave => Mod != null;

    public static bool IsLoaded => HasVanillaSave && HasModSave;

    public static int SelectedAreaId => Vanilla?.LastArea_Safe.ID ?? -1;

    public static bool TrySelectArea(int areaId)
    {
        if (Vanilla == null || areaId < 0)
            return false;

        Vanilla.LastArea = new AreaKey(areaId, AreaMode.Normal);
        return true;
    }

    public static bool IsChapterUnlocked(string sid)
    {
        return !string.IsNullOrWhiteSpace(sid) && Mod?.IsChapterUnlocked(sid) == true;
    }

    public static bool UnlockChapter(string sid)
    {
        if (string.IsNullOrWhiteSpace(sid) || Mod == null)
            return false;

        Mod.UnlockChapter(sid);
        AreaMapData.RefreshChapterIcon(sid);

        AreaData area = AreaData.Get(sid);
        if (area != null && Vanilla != null)
            Vanilla.UnlockedAreas = Math.Max(Vanilla.UnlockedAreas, area.ID + 1);

        return true;
    }

    public static bool TryRecordExtendedHeartGem(Session session)
    {
        if (!TryGetExtendedHeartId(session, out string heartId))
            return false;

        Mod?.CollectHeartGem(heartId);
        MaggyProgressionManager.RefreshProgression();
        return true;
    }

    public static bool TryGetPreferredCharacter(string sid, out string characterId)
    {
        characterId = string.Empty;
        return !string.IsNullOrWhiteSpace(sid) && Mod?.TryGetPreferredCharacter(sid, out characterId) == true;
    }

    public static bool HasHeartGem(Session session)
    {
        if (session == null)
            return false;

        int mode = (int) session.Area.Mode;
        if (mode < AreaModeExtender.MODE_DSIDE)
        {
            return AreaModeExtender.GetSaveAreaModeHeartGem(session.Area.ID, mode);
        }

        return TryGetExtendedHeartId(session, out string heartId) && Mod?.HasCollectedHeartGem(heartId) == true;
    }

    public static int CountHeartsForChapter(int areaId)
    {
        int count = 0;
        int vanillaModeCount = Math.Min(AreaModeExtender.GetSaveAreaModeCount(areaId), 3);

        for (int mode = 0; mode < vanillaModeCount; mode++)
        {
            if (AreaModeExtender.GetSaveAreaModeHeartGem(areaId, mode))
                count++;
        }

        AreaData area = AreaData.Get(areaId);
        if (area == null || !AreaModeExtender.IsOurMap(area) || Mod == null)
            return count;

        for (int mode = AreaModeExtender.MODE_DSIDE; mode < AreaModeExtender.TOTAL_MODES; mode++)
        {
            string heartId = BuildExtendedHeartId(area.SID, mode);
            if (Mod.HasCollectedHeartGem(heartId))
                count++;
        }

        return count;
    }

    public static string BuildExtendedHeartId(string sid, int mode)
    {
        return $"{sid}_{AreaModeExtender.GetModeName(mode)}";
    }

    public static bool TryGetExtendedHeartId(Session session, out string heartId)
    {
        heartId = null;
        if (session == null)
            return false;

        int mode = (int) session.Area.Mode;
        if (mode < AreaModeExtender.MODE_DSIDE)
            return false;

        AreaData area = AreaData.Get(session.Area);
        if (area?.SID == null)
            return false;

        heartId = BuildExtendedHeartId(area.SID, mode);
        return true;
    }
}