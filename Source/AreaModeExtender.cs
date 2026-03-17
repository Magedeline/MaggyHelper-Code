using System.Collections;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace MaggyHelper;

/// <summary>
/// Extends Celeste's vanilla area mode system to support A-Side, B-Side, C-Side, D-Side, and DX-Side
/// without requiring AltSideHelper or AltSideHelper_Extra.
/// 
/// Vanilla Celeste has 3 modes: Normal (0), BSide (1), CSide (2).
/// We extend to 5 modes: Normal (0), BSide (1), CSide (2), DSide (3), DXSide (4).
/// </summary>
public static class AreaModeExtender
{
    // Extended mode indices (matching Celeste's AreaMode enum values cast to int)
    public const int MODE_NORMAL = 0;
    public const int MODE_BSIDE = 1;
    public const int MODE_CSIDE = 2;
    public const int MODE_DSIDE = 3;
    public const int MODE_DXSIDE = 4;

    public const int TOTAL_MODES = 5;

    /// <summary>Map SID prefix for our mod's chapters</summary>
    public const string MAP_PREFIX = "Maggy";

    /// <summary>Main map folder under the Maggy map root.</summary>
    public const string MAP_MAIN_FOLDER = "Main";

    /// <summary>Effective SID root for chapter maps.</summary>
    public static readonly string MAP_ROOT = $"{MAP_PREFIX}/{MAP_MAIN_FOLDER}";

    /// <summary>Mode folder mapping (all sides live under Maggy/Main)</summary>
    public static readonly string[] SideFolders = { MAP_MAIN_FOLDER, MAP_MAIN_FOLDER, MAP_MAIN_FOLDER, MAP_MAIN_FOLDER, MAP_MAIN_FOLDER };

    /// <summary>Side display suffixes</summary>
    public static readonly string[] SideSuffixes = { "", " B-Side", " C-Side", " D-Side", " DX-Side" };

    /// <summary>Heart gem color per side (Blue=A, Red=B, Gold=C, Rainbow=D, Void=DX)</summary>
    public static readonly string[] HeartGemColors = { "blue", "red", "gold", "rainbow", "void" };

    /// <summary>Heart gem get sound events per side</summary>
    public static readonly string[] HeartGemGetSounds =
    {
        "event:/game/general/crystalheart_blue_get",
        "event:/game/general/crystalheart_red_get",
        "event:/game/general/crystalheart_gold_get",
        "event:/desolozantas/game/general/crystalheart_rainbow_get",
        "event:/desolozantas/game/general/crystalheart_void_get"
    };

    private static bool _hooked = false;

    /// <summary>Gets the side folder name for a mode index.</summary>
    public static string GetSideFolder(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex >= SideFolders.Length)
            return SideFolders[MODE_NORMAL];

        return SideFolders[modeIndex];
    }

    /// <summary>Builds a full SID path under the mod's main map folder for a given side.</summary>
    public static string BuildSideSID(int modeIndex, string mapName)
    {
        return BuildSideSID(GetSideFolder(modeIndex), mapName);
    }

    /// <summary>Builds a full SID path under the mod's main map folder for a given side folder name.</summary>
    public static string BuildSideSID(string sideFolder, string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
            return MAP_ROOT;

        return $"{MAP_ROOT}/{mapName}";
    }

    /// <summary>Builds an A-Side SID under the mod's main map folder.</summary>
    public static string BuildASideSID(string mapName)
    {
        return BuildSideSID(MODE_NORMAL, mapName);
    }

    // ── Hook Management ──────────────────────────────────────────────────

    public static void Load()
    {
        if (_hooked) return;
        _hooked = true;

        try
        {
            // Hook into AreaData loading to extend mode arrays
            On.Celeste.AreaData.Load += OnAreaDataLoad;

            // Hook into OuiChapterPanel to show D/DX side tabs
            On.Celeste.OuiChapterPanel.Reset += OnChapterPanelReset;
            On.Celeste.OuiChapterPanel.UpdateStats += OnChapterPanelUpdateStats;

            // Hook HeartGem collection to work with extended modes
            On.Celeste.HeartGem.Collect += OnHeartGemCollect;

            // Hook AreaComplete to handle extended modes
            On.Celeste.LevelExit.ctor += OnLevelExitCtor;

            // Hook Session ctor to guarantee checkpoint starts always receive valid AreaStats.
            On.Celeste.Session.ctor_AreaKey_string_AreaStats += OnSessionCtor;

            // Vanilla SaveData XML only supports AreaMode values 0-2.
            // Clamp D/DX modes at save/load boundaries so runtime can use them
            // without corrupting the underlying file-select save data.
            On.Celeste.SaveData.AfterInitialize += OnSaveDataAfterInitialize;
            On.Celeste.UserIO.SaveThread += OnSaveThread;

            Logger.Log(LogLevel.Info, "MaggyHelper", "AreaModeExtender loaded (A/B/C/D sides — DX-Side pending)");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Failed to load AreaModeExtender: {ex}");
            _hooked = false;
            throw;
        }
    }

    public static void Unload()
    {
        if (!_hooked) return;
        _hooked = false;

        On.Celeste.AreaData.Load -= OnAreaDataLoad;
        On.Celeste.OuiChapterPanel.Reset -= OnChapterPanelReset;
        On.Celeste.OuiChapterPanel.UpdateStats -= OnChapterPanelUpdateStats;
        On.Celeste.HeartGem.Collect -= OnHeartGemCollect;
        On.Celeste.LevelExit.ctor -= OnLevelExitCtor;
        On.Celeste.Session.ctor_AreaKey_string_AreaStats -= OnSessionCtor;
        On.Celeste.SaveData.AfterInitialize -= OnSaveDataAfterInitialize;
        On.Celeste.UserIO.SaveThread -= OnSaveThread;

        Logger.Log(LogLevel.Info, "MaggyHelper", "AreaModeExtender unloaded");
    }

    // ── AreaData Extension ───────────────────────────────────────────────

    /// <summary>
    /// After Celeste loads all area data, extend our chapters' Mode arrays 
    /// from 3 (vanilla) to 5 (with D-Side and DX-Side).
    /// </summary>
    private static void OnAreaDataLoad(On.Celeste.AreaData.orig_Load orig)
    {
        try
        {
            orig();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in vanilla AreaData.Load: {ex}");
            throw; // Re-throw as this is critical
        }

        int extendedCount = 0;
        int failedCount = 0;
        
        try
        {
            foreach (var area in AreaData.Areas)
            {
                if (!IsOurMap(area))
                    continue;

                try
                {
                    ExtendAreaModes(area);
                    extendedCount++;
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "MaggyHelper",
                        $"Failed to extend modes for area '{area?.SID ?? "<null>"}': {ex.Message}");
                    failedCount++;
                }
            }

            try
            {
                AreaMapData.ApplyHardcodedRuntimeData();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to apply hardcoded runtime data: {ex.Message}");
            }

            try
            {
                // Mirror vanilla-style map metadata parenting so sidecar maps
                // (B/C/D/DX) are grouped under their A-side chapter entry.
                ConfigureMainSideHierarchy();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to configure main side hierarchy: {ex.Message}");
            }

            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Extended {extendedCount} area(s) to {TOTAL_MODES} modes (failed: {failedCount})");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error extending area modes: {ex}");
            // Don't re-throw - allow game to continue with vanilla modes
        }
    }

    /// <summary>
    /// In Maggy/Main, B/C/D/DX bins exist as standalone map files.
    /// Mark those entries as children of their A-side/base chapter so chapter select
    /// shows one chapter entry instead of one entry per side map file.
    /// </summary>
    private static void ConfigureMainSideHierarchy()
    {
        var parentByBaseKey = new Dictionary<string, (int id, string sid)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < AreaData.Areas.Count; i++)
        {
            var area = AreaData.Areas[i];
            if (!TryParseMainSideSID(area?.SID, out string baseKey, out string suffix))
                continue;

            if (string.IsNullOrEmpty(suffix) || suffix.Equals("_A", StringComparison.OrdinalIgnoreCase))
            {
                if (!parentByBaseKey.ContainsKey(baseKey))
                    parentByBaseKey[baseKey] = (i, area.SID);
            }
        }

        int linked = 0;
        for (int i = 0; i < AreaData.Areas.Count; i++)
        {
            var area = AreaData.Areas[i];
            if (!TryParseMainSideSID(area?.SID, out string baseKey, out string suffix))
                continue;

            if (string.IsNullOrEmpty(suffix) || suffix.Equals("_A", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!parentByBaseKey.TryGetValue(baseKey, out var parent) || parent.id == i)
                continue;

            var dyn = DynamicData.For(area);
            SetParentOnObject(area, parent.id, parent.sid);
            object meta = null;
            try
            {
                meta = dyn.Get<object>("Meta");
            }
            catch
            {
            }
            if (meta != null)
                SetParentOnObject(meta, parent.id, parent.sid);

            linked++;
        }

        if (linked > 0)
        {
            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Configured Maggy/Main side hierarchy for {linked} side map entries");
        }
    }

    private static void SetParentOnObject(object target, int parentId, string parentSid)
    {
        if (target == null)
            return;

        var dyn = DynamicData.For(target);

        // Try common int-based parent members.
        TrySetMember(dyn, "Parent", parentId);
        TrySetMember(dyn, "parent", parentId);
        TrySetMember(dyn, "ParentID", parentId);
        TrySetMember(dyn, "ParentId", parentId);
        TrySetMember(dyn, "parentID", parentId);
        TrySetMember(dyn, "parentId", parentId);

        // Try common string SID-based parent members.
        if (!string.IsNullOrEmpty(parentSid))
        {
            TrySetMember(dyn, "ParentSID", parentSid);
            TrySetMember(dyn, "ParentSid", parentSid);
            TrySetMember(dyn, "parentSID", parentSid);
            TrySetMember(dyn, "parentSid", parentSid);
            TrySetMember(dyn, "Parent", parentSid);
            TrySetMember(dyn, "parent", parentSid);
        }
    }

    private static bool TrySetMember(DynamicData dyn, string name, object value)
    {
        try
        {
            dyn.Set(name, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseMainSideSID(string sid, out string baseKey, out string suffix)
    {
        baseKey = null;
        suffix = null;

        if (string.IsNullOrWhiteSpace(sid) || !sid.StartsWith(MAP_ROOT + "/", StringComparison.OrdinalIgnoreCase))
            return false;

        string mapName = sid[(MAP_ROOT.Length + 1)..];
        if (string.IsNullOrWhiteSpace(mapName))
            return false;

        foreach (var s in new[] { "_DX", "_D", "_C", "_B", "_A" })
        {
            if (mapName.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                suffix = s;
                baseKey = mapName[..^s.Length];
                return !string.IsNullOrWhiteSpace(baseKey);
            }
        }

        suffix = string.Empty;
        baseKey = mapName;
        return true;
    }

    /// <summary>
    /// Extends an AreaData's Mode array from 3 to 5 entries, populating
    /// D-Side (index 3) and DX-Side (index 4) with appropriate metadata.
    /// Only runs on A-side (parent) map entries; B/C/D/DX standalone entries
    /// must not be extended or they will gain Mode[1..] slots with null MapData,
    /// which crashes Everest's LevelSetStats.get_UnlockedModes().
    /// </summary>
    private static void ExtendAreaModes(AreaData area)
    {
        if (area.Mode == null || area.Mode.Length >= TOTAL_MODES)
            return;

        // Only extend the A-side (parent) chapter entry.
        // Side maps named *_B, *_C, *_D, *_DX are registered as their own
        // AreaData by Everest.  Extending them would leave Mode[1] etc. as
        // null-MapData stubs that crash LevelSetStats.get_UnlockedModes().
        if (TryParseMainSideSID(area.SID, out _, out string sidSuffix))
        {
            bool isNonASide = !string.IsNullOrEmpty(sidSuffix)
                && !sidSuffix.Equals("_A", StringComparison.OrdinalIgnoreCase);
            if (isNonASide)
            {
                Logger.Log(LogLevel.Verbose, "MaggyHelper",
                    $"ExtendAreaModes: skipping non-A-side entry '{area.SID}'");
                return;
            }
        }

        var chapterDef = AreaMapData.FindByAnySID(area.SID);
        if (chapterDef != null && !chapterDef.HasDSide && !chapterDef.HasDXSide)
            return;

        // Only extend chapters that have alt-sides (not prologue, epilogue, etc.)
        string sid = area.SID ?? "";
        string chapterKey = ExtractChapterKey(sid);
        if (string.IsNullOrEmpty(chapterKey))
            return;

        bool hasDSide  = chapterDef == null || chapterDef.HasDSide;
        bool hasDXSide = chapterDef == null || chapterDef.HasDXSide;

        var oldModes = area.Mode;

        // Size the array only to the highest populated index so that Everest's
        // LevelSetStats iterators never encounter a null Mode entry.
        int newLength = Math.Min(oldModes.Length, 3); // start at however many vanilla sides exist
        if (hasDSide)  newLength = Math.Max(newLength, MODE_DSIDE  + 1);
        if (hasDXSide) newLength = Math.Max(newLength, MODE_DXSIDE + 1);

        var newModes = new ModeProperties[newLength];

        // Copy existing A/B/C sides
        for (int i = 0; i < Math.Min(oldModes.Length, 3); i++)
            newModes[i] = oldModes[i];

        // Create D-Side mode (index 3)
        if (hasDSide)
        {
            string dSidePath = BuildSideSID(MODE_DSIDE, $"{chapterKey}_D");
            newModes[MODE_DSIDE] = CreateModeProperties(area, dSidePath, MODE_DSIDE, chapterKey);
        }

        // Create DX-Side mode (index 4)
        if (hasDXSide)
        {
            string dxSidePath = BuildSideSID(MODE_DXSIDE, $"{chapterKey}_DX");
            newModes[MODE_DXSIDE] = CreateModeProperties(area, dxSidePath, MODE_DXSIDE, chapterKey);
        }

        // Persist the extended array first so MapData construction can resolve
        // the mode's Path through AreaData.Get(key).Mode[modeIndex].Path.
        area.Mode = newModes;

        // Load MapData for every new extended mode.  Without this, Everest's
        // LevelSetStats.get_UnlockedModes() crashes on
        //   areaData.Mode[1].MapData.DetectedHeartGem
        // because HasMode() only checks Mode[i] != null, not MapData != null.
        for (int mi = oldModes.Length; mi < newModes.Length; mi++)
        {
            if (newModes[mi] == null) continue;
            try
            {
                var key = new AreaKey(area.ID, (global::Celeste.AreaMode)mi);
                newModes[mi].MapData = new MapData(key);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper",
                    $"Could not load MapData for '{area.SID}' mode {mi}: {ex.Message}");
                // Leave MapData null — mode will be hidden via a null guard below.
                // Clear the slot so HasMode() returns false and the Everest
                // iterator skips it rather than crashing on .MapData access.
                newModes[mi] = null;
            }
        }

        // Trim trailing null slots introduced by MapData load failures so that
        // area.Mode.Length accurately reflects the number of valid modes.
        int validLength = newModes.Length;
        while (validLength > oldModes.Length && newModes[validLength - 1] == null)
            validLength--;
        if (validLength != newModes.Length)
        {
            var trimmed = new ModeProperties[validLength];
            Array.Copy(newModes, trimmed, validLength);
            area.Mode = trimmed;
        }

        Logger.Log(LogLevel.Verbose, "MaggyHelper",
            $"Extended '{area.SID}' to {newLength} modes (D={hasDSide}, DX={hasDXSide})");
    }

    /// <summary>
    /// Creates a ModeProperties for an extended side based on the chapter's A-Side data.
    /// </summary>
    private static ModeProperties CreateModeProperties(AreaData area, string mapPath, int modeIndex, string chapterKey)
    {
        // Base the audio state on C-Side if available, otherwise A-Side
        var baseMode = area.Mode.Length > MODE_CSIDE && area.Mode[MODE_CSIDE] != null
            ? area.Mode[MODE_CSIDE]
            : area.Mode[MODE_NORMAL];

        // Get chapter number for music lookup
        int chapterNum = ExtractChapterNumber(chapterKey);
        string musicFolder = modeIndex == MODE_DSIDE ? "CSide" : "DXSide";

        var mode = new ModeProperties
        {
            Path = mapPath,
            Inventory = baseMode?.Inventory ?? PlayerInventory.Default,
            AudioState = new AudioState(
                $"event:/desolozantas/music/{musicFolder}/{FormatChapterNum(chapterNum)}_{GetChapterMusicSuffix(chapterKey)}",
                baseMode?.AudioState?.Ambience?.Event ?? $"event:/desolozantas/env/{FormatChapterNum(chapterNum)}_main"
            ),
            Checkpoints = null,
        };

        return mode;
    }

    // ── Chapter Panel Extension ──────────────────────────────────────────

    /// <summary>
    /// When the chapter panel resets, ensure our extended modes are accessible.
    /// </summary>
    private static void OnChapterPanelReset(On.Celeste.OuiChapterPanel.orig_Reset orig, OuiChapterPanel self)
    {
        try
        {
            var area = AreaData.Get(self.Area);
            if (!IsOurMap(area))
            {
                orig(self);
                return;
            }

            // Vanilla chapter panel option generation is gated by SaveData.UnlockedModes.
            // For custom sides, bump that gate so D/DX tabs can be created when unlocked.
            EnsureUnlockedModesForChapterPanel(self.Area, area);

            // Re-apply the chapter's runtime UI data before the panel rebuilds itself.
            AreaMapData.ApplyHardcodedRuntimeData(area);

            orig(self);

            // Store extended mode availability for UI navigation
            if (area?.Mode == null || area.Mode.Length < TOTAL_MODES)
                return;

            // The panel will be extended via the side button press hooks
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnChapterPanelReset: {ex.Message}");
            // Try to continue with vanilla behavior
            try { orig(self); } catch { }
        }
    }

    private static void EnsureUnlockedModesForChapterPanel(AreaKey key, AreaData area)
    {
        var save = SaveData.Instance;
        if (save == null || area?.Mode == null)
            return;

        int required = 3; // Keep vanilla behavior by default (A/B/C).

        bool hasD = area.Mode.Length > MODE_DSIDE && area.Mode[MODE_DSIDE] != null;
        bool hasDX = area.Mode.Length > MODE_DXSIDE && area.Mode[MODE_DXSIDE] != null;

        if (hasD && IsSideUnlocked(key, MODE_DSIDE))
            required = MODE_DSIDE + 1;

        if (hasDX && IsSideUnlocked(key, MODE_DXSIDE))
            required = MODE_DXSIDE + 1;

        if (required <= save.UnlockedModes)
            return;

        // SaveData.UnlockedModes is read-only on current Everest; write backing fields.
        var dyn = DynamicData.For(save);
        try { dyn.Set("unlockedModes", required); } catch { }
        try { dyn.Set("UnlockedModes", required); } catch { }
    }

    /// <summary>
    /// Update stats display for extended modes.
    /// </summary>
    private static void OnChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig,
        OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle)
    {
        try
        {
            var area = AreaData.Get(self.Area);
            if (IsOurMap(area))
                AreaMapData.ApplyHardcodedRuntimeData(area);

            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Error in OnChapterPanelUpdateStats: {ex.Message}");
        }
    }

    // ── HeartGem Collection ──────────────────────────────────────────────

    /// <summary>
    /// Hook HeartGem collection to properly track gems for D-Side and DX-Side.
    /// </summary>
    private static void OnHeartGemCollect(On.Celeste.HeartGem.orig_Collect orig, HeartGem self, Player player)
    {
        try
        {
            var level = self.Scene as Level;
            if (level == null)
            {
                orig(self, player);
                return;
            }

            int modeIndex = (int)level.Session.Area.Mode;
            var area = AreaData.Get(level.Session.Area);

            // For vanilla modes (0-2), use normal collection
            if (modeIndex < MODE_DSIDE || !IsOurMap(area))
            {
                orig(self, player);
                return;
            }

            // For D-Side and DX-Side, handle collection ourselves
            orig(self, player);

            // Track in our save data
            string heartId = $"{area.SID}_{GetModeName(modeIndex)}";
            MaggyHelperModule.SaveData?.CollectHeartGem(heartId);

            // Play the appropriate collection sound
            if (modeIndex < HeartGemGetSounds.Length)
            {
                Audio.Play(HeartGemGetSounds[modeIndex]);
            }

            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Heart gem collected: {heartId} (mode {modeIndex})");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnHeartGemCollect: {ex.Message}");
            // Fall back to vanilla behavior
            try { orig(self, player); } catch { }
        }
    }

    // ── LevelExit Extension ─────────────────────────────────────────────

    /// <summary>
    /// When exiting a level, handle postcard display and side unlocking for D/DX.
    /// </summary>
    private static void OnLevelExitCtor(On.Celeste.LevelExit.orig_ctor orig, LevelExit self,
        LevelExit.Mode mode, Session session, HiresSnow snow)
    {
        try
        {
            orig(self, mode, session, snow);

            if (mode != LevelExit.Mode.Completed || session == null)
                return;

            var area = AreaData.Get(session.Area);
            if (!IsOurMap(area))
                return;

            int currentMode = (int)session.Area.Mode;

            // After completing B-Side, unlock C-Side (vanilla handles A→B)
            // After completing C-Side, show D-Side postcard
            // After completing D-Side, show DX-Side postcard
            switch (currentMode)
            {
                case MODE_BSIDE:
                    // C-Side should now be available (vanilla handles this)
                    break;

                case MODE_CSIDE:
                    try
                    {
                        // Show D-Side unlock postcard
                        ShowPostcard(self, session, "POSTCARD_DSIDE_UNLOCK", "dsides");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to show D-Side postcard: {ex.Message}");
                    }
                    break;

                case MODE_DSIDE:
                    // DX-Side postcard is deferred until the DX map is ready.
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnLevelExitCtor: {ex.Message}");
            // Try to continue with vanilla behavior
            try { orig(self, mode, session, snow); } catch { }
        }
    }

    /// <summary>
    /// Defensive Session construction for custom sides. Checkpoint starts on extended side tabs can
    /// pass null or undersized AreaStats, which crashes vanilla Session ctor when indexing mode stats.
    /// </summary>
    private static void OnSessionCtor(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self,
        AreaKey area, string checkpoint, AreaStats oldStats)
    {
        try
        {
            AreaStats safeStats = EnsureSafeAreaStats(area, oldStats);

            // Never pass a null AreaStats to Session ctor.
            if (safeStats == null)
                safeStats = CreateFallbackAreaStats(area);

            // Absolute last resort: pass through original object if all fallbacks failed.
            safeStats ??= oldStats;

            int modeIndex = (int)area.Mode;
            int modesLength = safeStats?.Modes?.Length ?? -1;
            bool selectedModeNull = modeIndex < 0 || modeIndex >= modesLength || safeStats?.Modes?[modeIndex] == null;
            Logger.Log(LogLevel.Verbose, "MaggyHelper",
                $"SessionCtor sanitize: sid={area.SID} mode={modeIndex} checkpoint={(checkpoint ?? "<none>")} statsNull={safeStats == null} modesLen={modesLength} selectedModeNull={selectedModeNull}");

            orig(self, area, checkpoint, safeStats);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", 
                $"Critical error in OnSessionCtor for {area.SID} mode {(int)area.Mode}: {ex.Message}");
            
            // Last resort: try with fallback stats
            try
            {
                var fallback = CreateFallbackAreaStats(area);
                if (fallback != null)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", "Attempting Session creation with fallback AreaStats");
                    orig(self, area, checkpoint, fallback);
                    return;
                }
            }
            catch { }
            
            // Re-throw if we can't recover - better to fail cleanly than corrupt saves
            throw;
        }
    }

    private static void OnSaveDataAfterInitialize(On.Celeste.SaveData.orig_AfterInitialize orig, SaveData self)
    {
        try
        {
            orig(self);

            try
            {
                // SaveData.AfterInitialize also runs during brand-new slot creation.
                // Avoid touching partially-constructed SaveData before vanilla has finished
                // building its internal state, then clamp any extended modes afterwards.
                SanitizeVanillaSaveTargets(self, temporary: false);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper",
                    $"Post-initialize save sanitizer skipped: {ex.Message}");
            }

            try
            {
                EnsureMaggySaveAreaStats(self);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper",
                    $"Post-initialize Maggy AreaStats repair skipped: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in SaveData.AfterInitialize sanitizer: {ex}");
            throw;
        }
    }

    private static void OnSaveThread(On.Celeste.UserIO.orig_SaveThread orig)
    {
        SaveDataSanitizationSnapshot snapshot = null;

        try
        {
            snapshot = SanitizeVanillaSaveTargets(SaveData.Instance, temporary: true);
            orig();
        }
        finally
        {
            snapshot?.Restore();
        }
    }

    private static AreaStats EnsureSafeAreaStats(AreaKey area, AreaStats oldStats)
    {
        try
        {
            AreaStats safeStats = oldStats;

            // Prefer game-provided stats; checkpoint flow can provide null depending on panel state.
            if (safeStats == null)
            {
                var save = SaveData.Instance;
                if (save?.Areas_Safe != null && area.ID >= 0 && area.ID < save.Areas_Safe.Count)
                    safeStats = save.Areas_Safe[area.ID];
            }

            // Last-resort fallback: construct an empty AreaStats instance if possible.
            if (safeStats == null)
                safeStats = CreateFallbackAreaStats(area);

            if (safeStats != null)
            {
                int requiredModes = Math.Max(TOTAL_MODES, (int)area.Mode + 1);
                EnsureAreaModeStatsArray(safeStats, requiredModes);
            }

            return safeStats;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper",
                $"Failed to sanitize AreaStats for {area.SID} mode {(int)area.Mode}: {ex.Message}");
            return CreateFallbackAreaStats(area);
        }
    }

    private static AreaStats CreateFallbackAreaStats(AreaKey area)
    {
        Type areaStatsType = typeof(AreaStats);

        // Try common constructor signatures first.
        foreach (var args in new object[][]
        {
            new object[] { area.ID },
            Array.Empty<object>()
        })
        {
            try
            {
                if (Activator.CreateInstance(areaStatsType,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    args: args,
                    culture: null) is AreaStats created)
                {
                    return created;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static void EnsureMaggySaveAreaStats(SaveData save)
    {
        if (save?.Areas_Safe == null || AreaData.Areas == null || AreaData.Areas.Count == 0)
            return;

        int repaired = 0;

        for (int areaId = 0; areaId < AreaData.Areas.Count; areaId++)
        {
            AreaData area = AreaData.Areas[areaId];
            if (!IsOurMap(area))
                continue;

            if (areaId < 0 || areaId >= save.Areas_Safe.Count)
                continue;

            AreaStats stats = save.Areas_Safe[areaId];
            if (stats == null)
            {
                stats = CreateFallbackAreaStats(new AreaKey(areaId, global::Celeste.AreaMode.Normal));
                if (stats == null)
                    continue;

                save.Areas_Safe[areaId] = stats;
                repaired++;
            }

            int requiredModes = Math.Max(area.Mode?.Length ?? 0, 3);
            EnsureAreaModeStatsArray(stats, requiredModes);
        }

        if (repaired > 0)
        {
            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Rebuilt {repaired} missing Maggy AreaStats entries after save load");
        }
    }

    private static SaveDataSanitizationSnapshot SanitizeVanillaSaveTargets(SaveData save, bool temporary)
    {
        if (save == null)
            return null;

        SaveDataSanitizationSnapshot snapshot = temporary ? new SaveDataSanitizationSnapshot(save) : null;
        int changes = 0;

        if (TrySanitizeAreaKey(save.LastArea, out AreaKey sanitizedLastArea))
        {
            int originalMode = (int)save.LastArea.Mode;

            if (temporary)
                snapshot.LastArea = save.LastArea;

            save.LastArea = sanitizedLastArea;
            changes++;

            Logger.Log(LogLevel.Warn, "MaggyHelper",
                $"Clamped save LastArea from mode {originalMode} to {(int)sanitizedLastArea.Mode} for vanilla serialization");
        }

        Session currentSession = GetCurrentSession(save);
        if (currentSession != null && TrySanitizeAreaKey(currentSession.Area, out AreaKey sanitizedSessionArea))
        {
            int originalMode = (int)currentSession.Area.Mode;

            if (temporary)
            {
                snapshot.CurrentSession = currentSession;
                snapshot.CurrentSessionArea = currentSession.Area;
            }

            SetSessionArea(currentSession, sanitizedSessionArea);
            changes++;

            Logger.Log(LogLevel.Warn, "MaggyHelper",
                $"Clamped CurrentSession area from mode {originalMode} to {(int)sanitizedSessionArea.Mode} for vanilla serialization");
        }

        if (!temporary)
            return null;

        return changes > 0 ? snapshot : null;
    }

    private static Session GetCurrentSession(SaveData save)
    {
        if (save == null)
            return null;

        try
        {
            return save.CurrentSession_Safe ?? save.CurrentSession;
        }
        catch
        {
            return null;
        }
    }

    private static void SetSessionArea(Session session, AreaKey sanitizedArea)
    {
        if (session == null)
            return;

        DynamicData dyn = DynamicData.For(session);

        try { dyn.Set("area", sanitizedArea); } catch { }
        try { dyn.Set("Area", sanitizedArea); } catch { }
    }

    private static bool TrySanitizeAreaKey(AreaKey key, out AreaKey sanitized)
    {
        sanitized = key;

        int modeIndex = (int)key.Mode;
        if (modeIndex >= MODE_NORMAL && modeIndex <= MODE_CSIDE)
            return false;

        int clampedMode = Math.Clamp(modeIndex, MODE_NORMAL, MODE_CSIDE);
        sanitized = new AreaKey(key.ID, (global::Celeste.AreaMode)clampedMode);
        return true;
    }

    private sealed class SaveDataSanitizationSnapshot
    {
        private readonly SaveData _save;

        public SaveDataSanitizationSnapshot(SaveData save)
        {
            _save = save;
        }

        public AreaKey? LastArea { get; set; }

        public Session CurrentSession { get; set; }

        public AreaKey? CurrentSessionArea { get; set; }

        public void Restore()
        {
            if (_save == null)
                return;

            if (LastArea.HasValue)
                _save.LastArea = LastArea.Value;

            if (CurrentSession != null && CurrentSessionArea.HasValue)
                SetSessionArea(CurrentSession, CurrentSessionArea.Value);
        }
    }

    private static void EnsureAreaModeStatsArray(AreaStats stats, int requiredModes)
    {
        var dyn = new DynamicData(stats);
        var modes = stats.Modes;

        if (modes == null)
        {
            modes = new AreaModeStats[requiredModes];
            dyn.Set("Modes", modes);
            dyn.Set("modes", modes);
        }
        else if (modes.Length < requiredModes)
        {
            var resized = new AreaModeStats[requiredModes];
            Array.Copy(modes, resized, modes.Length);
            modes = resized;
            dyn.Set("Modes", modes);
            dyn.Set("modes", modes);
        }

        for (int i = 0; i < requiredModes; i++)
        {
            if (modes[i] == null)
                modes[i] = new AreaModeStats();

            EnsureAreaModeStatsSafety(modes[i]);
        }
    }

    private static void EnsureAreaModeStatsSafety(AreaModeStats modeStats)
    {
        if (modeStats == null)
            return;

        // Some external hooks expect reference members inside AreaModeStats to exist.
        // Initialize any null reference-type fields/properties with safe defaults.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var field in typeof(AreaModeStats).GetFields(flags))
        {
            if (field.FieldType.IsValueType || field.FieldType == typeof(string))
                continue;

            if (field.GetValue(modeStats) != null)
                continue;

            object value = CreateDefaultReferenceValue(field.FieldType);
            if (value != null)
            {
                try { field.SetValue(modeStats, value); } catch { }
            }
        }

        foreach (var prop in typeof(AreaModeStats).GetProperties(flags))
        {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                continue;

            object current;
            try { current = prop.GetValue(modeStats); }
            catch { continue; }

            if (current != null)
                continue;

            object value = CreateDefaultReferenceValue(prop.PropertyType);
            if (value != null)
            {
                try { prop.SetValue(modeStats, value); } catch { }
            }
        }
    }

    private static object CreateDefaultReferenceValue(Type type)
    {
        try
        {
            if (type.IsArray)
                return Array.CreateInstance(type.GetElementType()!, 0);

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
                return Activator.CreateInstance(type);

            if (type.GetConstructor(Type.EmptyTypes) != null)
                return Activator.CreateInstance(type);
        }
        catch
        {
        }

        return null;
    }

    private static void ShowPostcard(LevelExit exit, Session session, string dialogKey, string soundId)
    {
        // Postcard display is handled by the PostcardMaggy system
        // This will be triggered in the LevelExit flow
        Logger.Log(LogLevel.Info, "MaggyHelper",
            $"Showing postcard: {dialogKey} for area {session.Area.SID}");
    }

    // ── Utility Methods ──────────────────────────────────────────────────

    /// <summary>Checks if an AreaData belongs to our mod</summary>
    public static bool IsOurMap(AreaData area)
    {
        if (area?.SID == null) return false;
        return area.SID.StartsWith(MAP_ROOT + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Gets the mode name for a given mode index</summary>
    public static string GetModeName(int modeIndex)
    {
        return modeIndex switch
        {
            MODE_NORMAL => "Normal",
            MODE_BSIDE => "BSide",
            MODE_CSIDE => "CSide",
            MODE_DSIDE => "DSide",
            MODE_DXSIDE => "DXSide",
            _ => $"Mode{modeIndex}"
        };
    }

    /// <summary>Gets the side label letter(s) for UI display</summary>
    public static string GetSideLabel(int modeIndex)
    {
        return modeIndex switch
        {
            MODE_NORMAL => "A",
            MODE_BSIDE => "B",
            MODE_CSIDE => "C",
            MODE_DSIDE => "D",
            MODE_DXSIDE => "DX",
            _ => "?"
        };
    }

    /// <summary>
    /// Checks whether a specific side is unlocked for a given area.
    /// A-Side: always available
    /// B-Side: after completing A-Side
    /// C-Side: after completing B-Side + postcard shown
    /// D-Side: after completing C-Side + postcard shown
    /// DX-Side: after completing D-Side + postcard shown
    /// </summary>
    public static bool IsSideUnlocked(AreaKey area, int modeIndex)
    {
        if (modeIndex == MODE_NORMAL) return true;

        var saveData = SaveData.Instance;
        if (saveData == null) return false;

        // Cheat mode bypasses all side unlock requirements
        if (saveData.CheatMode) return true;

        var areaStats = saveData.Areas_Safe[area.ID];
        if (areaStats == null) return false;

        // Each side requires the previous side to be completed
        int previousMode = modeIndex - 1;
        if (previousMode < 0) return true;

        // Check if the previous mode's area stats indicate completion
        if (previousMode < areaStats.Modes.Length)
        {
            return areaStats.Modes[previousMode]?.HeartGem == true
                || areaStats.Modes[previousMode]?.Completed == true;
        }

        // For extended modes beyond vanilla tracking, check our custom save data
        string heartId = $"{AreaData.Get(area)?.SID}_{GetModeName(previousMode)}";
        return MaggyHelperModule.SaveData?.HasCollectedHeartGem(heartId) == true;
    }

    /// <summary>Extracts the chapter key from a SID (e.g. "Maggy/Main/01_City_A" → "01_City")</summary>
    private static string ExtractChapterKey(string sid)
    {
        if (string.IsNullOrEmpty(sid)) return null;

        // Parse: Maggy/Main/XX_Name_A → XX_Name
        var parts = sid.Split('/');
        if (parts.Length < 3) return null;

        string mapName = parts[^1]; // last segment
        // Remove side suffix (_A, _B, _C, _D, _DX)
        foreach (var suffix in new[] { "_A", "_B", "_C", "_D", "_DX" })
        {
            if (mapName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                mapName = mapName[..^suffix.Length];
                break;
            }
        }

        return mapName;
    }

    /// <summary>Extracts chapter number from key (e.g. "01_City" → 1)</summary>
    private static int ExtractChapterNumber(string chapterKey)
    {
        if (string.IsNullOrEmpty(chapterKey)) return 0;
        var numStr = "";
        foreach (char c in chapterKey)
        {
            if (char.IsDigit(c)) numStr += c;
            else break;
        }
        return int.TryParse(numStr, out int n) ? n : 0;
    }

    /// <summary>Formats chapter number for music paths (e.g. 1 → "01")</summary>
    private static string FormatChapterNum(int num)
    {
        return num.ToString("D2");
    }

    /// <summary>Gets the music suffix for a chapter key (e.g. "01_City" → "city")</summary>
    private static string GetChapterMusicSuffix(string chapterKey)
    {
        if (string.IsNullOrEmpty(chapterKey)) return "Main";
        int underscoreIdx = chapterKey.IndexOf('_');
        if (underscoreIdx < 0 || underscoreIdx >= chapterKey.Length - 1) return "Main";
        return chapterKey[(underscoreIdx + 1)..].ToLowerInvariant();
    }
}
