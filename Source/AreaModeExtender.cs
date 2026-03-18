#pragma warning disable CS0436 // Local patch types intentionally shadow imported Celeste runtime types.

using System.Collections;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
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
    private static readonly Type RuntimeAreaStatsType = typeof(Session).Assembly.GetType("Celeste.AreaStats", throwOnError: false);
    private static readonly HashSet<string> EarlyMapMetaSkipLog = new(StringComparer.OrdinalIgnoreCase);
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
    private static On.Celeste.Session.hook_ctor_AreaKey_string_AreaStats _sessionCtorHook;
    private static Hook _mapMetaApplyHook;

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

            // Everest applies map meta before our post-load mode extension runs.
            // Skip any Maggy mode metadata targeting slots that do not exist yet.
            InstallMapMetaApplyHook();

            // Hook into OuiChapterPanel to show D/DX side tabs
            On.Celeste.OuiChapterPanel.Reset += OnChapterPanelReset;
            On.Celeste.OuiChapterPanel.UpdateStats += OnChapterPanelUpdateStats;

            // Hook HeartGem collection to work with extended modes
            On.Celeste.HeartGem.Collect += OnHeartGemCollect;

            // Hook AreaComplete to handle extended modes
            On.Celeste.LevelExit.ctor += OnLevelExitCtor;

            // Hook Session ctor to guarantee checkpoint starts always receive valid AreaStats.
            _sessionCtorHook ??= (orig, self, area, checkpoint, oldStats) => OnSessionCtor(orig, self, area, checkpoint, oldStats);
            On.Celeste.Session.ctor_AreaKey_string_AreaStats += _sessionCtorHook;

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
        if (_sessionCtorHook != null)
            On.Celeste.Session.ctor_AreaKey_string_AreaStats -= _sessionCtorHook;
        On.Celeste.SaveData.AfterInitialize -= OnSaveDataAfterInitialize;
        On.Celeste.UserIO.SaveThread -= OnSaveThread;
        _mapMetaApplyHook?.Dispose();
        _mapMetaApplyHook = null;
        EarlyMapMetaSkipLog.Clear();

        Logger.Log(LogLevel.Info, "MaggyHelper", "AreaModeExtender unloaded");
    }

    // ── AreaData Extension ───────────────────────────────────────────────

    private delegate void orig_MapMetaModeProperties_ApplyTo(MapMetaModeProperties self, AreaData area, AreaMode mode);

    private static void InstallMapMetaApplyHook()
    {
        if (_mapMetaApplyHook != null)
            return;

        MethodInfo target = typeof(MapMetaModeProperties).GetMethod(
            "ApplyTo",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(AreaData), typeof(AreaMode) },
            modifiers: null);

        MethodInfo detour = typeof(AreaModeExtender).GetMethod(
            nameof(Hook_MapMetaModeProperties_ApplyTo),
            BindingFlags.Static | BindingFlags.NonPublic);

        if (target == null || detour == null)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper",
                "Failed to locate MapMetaModeProperties.ApplyTo detour target; early mode metadata guard disabled.");
            return;
        }

        _mapMetaApplyHook = new Hook(target, detour);
    }

    private static void Hook_MapMetaModeProperties_ApplyTo(orig_MapMetaModeProperties_ApplyTo orig,
        MapMetaModeProperties self, AreaData area, AreaMode mode)
    {
        if (area != null && IsOurMap(area))
        {
            int modeIndex = (int) mode;
            int availableModes = area.Mode?.Length ?? 0;

            if (modeIndex >= availableModes)
            {
                string key = $"{area.SID}|{modeIndex}|{availableModes}";
                if (EarlyMapMetaSkipLog.Add(key))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper",
                        $"Skipping early MapMeta apply for '{area.SID}' mode {modeIndex}; Mode[] length is still {availableModes} during AreaData.Load.");
                }

                return;
            }

            try
            {
                orig(self, area, mode);
                return;
            }
            catch (IndexOutOfRangeException) when (availableModes < TOTAL_MODES)
            {
                string key = $"{area.SID}|orig-throw|{modeIndex}|{availableModes}";
                if (EarlyMapMetaSkipLog.Add(key))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper",
                        $"Skipping early MapMeta apply for '{area.SID}' mode {modeIndex} after Everest hit IndexOutOfRangeException with Mode[] length {availableModes}.");
                }

                return;
            }
        }

        orig(self, area, mode);
    }

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

        // Try common string SID-based parent members.
        if (!string.IsNullOrEmpty(parentSid))
        {
            TrySetMember(target, dyn, "ParentSID", parentSid);
            TrySetMember(target, dyn, "ParentSid", parentSid);
            TrySetMember(target, dyn, "parentSID", parentSid);
            TrySetMember(target, dyn, "parentSid", parentSid);
            TrySetMember(target, dyn, "Parent", parentSid);
            TrySetMember(target, dyn, "parent", parentSid);
        }

        // Try common int-based parent members.
        TrySetMember(target, dyn, "ParentID", parentId);
        TrySetMember(target, dyn, "ParentId", parentId);
        TrySetMember(target, dyn, "parentID", parentId);
        TrySetMember(target, dyn, "parentId", parentId);
        TrySetMember(target, dyn, "Parent", parentId);
        TrySetMember(target, dyn, "parent", parentId);
    }

    private static bool TrySetMember(object target, DynamicData dyn, string name, object value)
    {
        if (!TryResolveWritableMember(target.GetType(), name, value?.GetType(), out string resolvedName))
            return false;

        try
        {
            dyn.Set(resolvedName, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveWritableMember(Type targetType, string name, Type valueType, out string resolvedName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        resolvedName = name;

        var property = targetType.GetProperty(name, flags);
        if (property != null && property.CanWrite && IsValueCompatible(property.PropertyType, valueType))
        {
            resolvedName = property.Name;
            return true;
        }

        var field = targetType.GetField(name, flags);
        if (field != null && IsValueCompatible(field.FieldType, valueType))
        {
            resolvedName = field.Name;
            return true;
        }

        return false;
    }

    private static bool IsValueCompatible(Type targetType, Type valueType)
    {
        if (valueType == null)
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

        Type effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveTargetType.IsAssignableFrom(valueType))
            return true;

        if (effectiveTargetType.IsEnum)
            return IsValueCompatible(Enum.GetUnderlyingType(effectiveTargetType), valueType);

        return effectiveTargetType == typeof(int) && valueType == typeof(int)
            || effectiveTargetType == typeof(string) && valueType == typeof(string);
    }

    private static T TryGetMember<T>(DynamicData dyn, string name, T fallback = default)
    {
        if (dyn == null)
            return fallback;

        try
        {
            return dyn.Get<T>(name);
        }
        catch
        {
            return fallback;
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
        {
            Logger.Log(LogLevel.Verbose, "MaggyHelper",
                $"ExtendAreaModes: skipping '{area.SID}' (chapter has no D/DX sides)");
            return;
        }

        // Only extend chapters that have alt-sides (not prologue, epilogue, etc.)
        string sid = area.SID ?? "";
        string chapterKey = ExtractChapterKey(sid);
        if (string.IsNullOrEmpty(chapterKey))
        {
            Logger.Log(LogLevel.Verbose, "MaggyHelper",
                $"ExtendAreaModes: could not extract chapter key from '{area.SID}'");
            return;
        }

        bool hasDSide  = chapterDef == null || chapterDef.HasDSide;
        bool hasDXSide = chapterDef == null || chapterDef.HasDXSide;

        Logger.Log(LogLevel.Verbose, "MaggyHelper",
            $"ExtendAreaModes: '{area.SID}' - chapterDefFound={chapterDef != null}, hasDSide={hasDSide}, hasDXSide={hasDXSide}");

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
                Logger.Log(LogLevel.Verbose, "MaggyHelper",
                    $"ExtendAreaModes: loaded MapData for '{area.SID}' mode {mi} (path: {newModes[mi].Path})");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper",
                    $"ExtendAreaModes: failed to load MapData for '{area.SID}' mode {mi} (path: {newModes[mi].Path}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
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
        AreaKey area, string checkpoint, object oldStats)
    {
        try
        {
            object safeStats = EnsureSafeAreaStats(area, oldStats);

            // Never pass a null AreaStats to Session ctor.
            if (safeStats == null)
                safeStats = CreateFallbackAreaStats(area);

            // Absolute last resort: pass through original object if all fallbacks failed.
            safeStats ??= oldStats;

            int modeIndex = (int)area.Mode;
            Array modes = GetModesArray(safeStats);
            int modesLength = modes?.Length ?? -1;
            bool selectedModeNull = modeIndex < 0 || modeIndex >= modesLength || modes?.GetValue(modeIndex) == null;
            Logger.Log(LogLevel.Verbose, "MaggyHelper",
                $"SessionCtor sanitize: sid={area.SID} mode={modeIndex} checkpoint={(checkpoint ?? "<none>")} statsNull={safeStats == null} modesLen={modesLength} selectedModeNull={selectedModeNull}");

            InvokeSessionCtor(orig, self, area, checkpoint, safeStats);
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
                    InvokeSessionCtor(orig, self, area, checkpoint, fallback);
                    return;
                }
            }
            catch { }
            
            // Re-throw if we can't recover - better to fail cleanly than corrupt saves
            throw;
        }
    }

    private static void InvokeSessionCtor(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self,
        AreaKey area, string checkpoint, object stats)
    {
        orig.DynamicInvoke(self, area, checkpoint, stats);
    }

    private static void OnSaveDataAfterInitialize(On.Celeste.SaveData.orig_AfterInitialize orig, SaveData self)
    {
        string stage = "before-first-orig";
        try
        {
            try
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "SaveData.AfterInitialize wrapper entering first orig(self) call.");
                orig(self);
                Logger.Log(LogLevel.Info, "MaggyHelper", "SaveData.AfterInitialize wrapper completed first orig(self) call.");
            }
            catch (Exception ex) when (TryRecoverAfterInitialize(self, ex))
            {
                stage = "retry-orig";
                Logger.Log(LogLevel.Warn, "MaggyHelper",
                    $"SaveData.AfterInitialize wrapper retrying orig(self) after {ex.GetType().Name}.");
                try
                {
                    orig(self);
                    Logger.Log(LogLevel.Info, "MaggyHelper", "SaveData.AfterInitialize wrapper completed retry orig(self) call.");
                }
                catch (Exception retryEx) when (TryFallbackAfterInitialize(self, retryEx))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper",
                        $"SaveData.AfterInitialize wrapper suppressed second failure after fallback repair: {retryEx.GetType().Name}.");
                }
            }

            try
            {
                stage = "post-sanitize";
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
                stage = "post-ensure-extended-save-areas";
                EnsureExtendedSaveAreaStats(self);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper",
                    $"Post-initialize Maggy AreaStats repair skipped: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in SaveData.AfterInitialize sanitizer during wrapper stage '{stage}': {ex}");
            throw;
        }
    }

    private static bool TryFallbackAfterInitialize(SaveData save, Exception exception)
    {
        if (save == null)
            return false;

        if (exception is not NullReferenceException && exception is not IndexOutOfRangeException)
            return false;

        try
        {
            int repaired = 0;
            DynamicData saveDyn = DynamicData.For(save);

            repaired += EnsureSaveDataStructure(save);
            repaired += EnsureExtendedSaveAreaStats(save);

            if (TryGetMember<string>(saveDyn, "Name") == null && TrySetMember(save, saveDyn, "Name", string.Empty))
                repaired++;
            if (TryGetMember<string>(saveDyn, "TheoSisterName") == null && TrySetMember(save, saveDyn, "TheoSisterName", string.Empty))
                repaired++;

            if (TryGetMember<IList>(saveDyn, "Areas_Unsafe") == null
                && TrySetMember(save, saveDyn, "Areas_Unsafe", new List<AreaStats>()))
            {
                repaired++;
            }

            if (TryGetMember<IList>(saveDyn, "LevelSets") == null
                && TrySetMember(save, saveDyn, "LevelSets", new List<object>()))
            {
                repaired++;
            }

            if (TryGetMember<IList>(saveDyn, "LevelSetRecycleBin") == null
                && TrySetMember(save, saveDyn, "LevelSetRecycleBin", new List<object>()))
            {
                repaired++;
            }

            AreaKey defaultKey = AreaKey.Default;
            if (TrySetMember(save, saveDyn, "LastArea", defaultKey))
                repaired++;
            if (TrySetMember(save, saveDyn, "LastArea_Safe", defaultKey))
                repaired++;
            if (TrySetMember(save, saveDyn, "LastArea_Unsafe", defaultKey))
                repaired++;

            if (TrySetMember(save, saveDyn, "CurrentSession", null))
                repaired++;
            if (TrySetMember(save, saveDyn, "CurrentSession_Safe", null))
                repaired++;
            if (TrySetMember(save, saveDyn, "CurrentSession_Unsafe", null))
                repaired++;

            TrySetMember(save, saveDyn, "_cached_Areas_Safe", null);
            TrySetMember(save, saveDyn, "_cached_LevelSetStats", null);
            TrySetMember(save, saveDyn, "_cached_LevelSetStats_LevelSet", null);

            try
            {
                SanitizeVanillaSaveTargets(save, temporary: false);
            }
            catch
            {
            }

            Logger.Log(LogLevel.Warn, "MaggyHelper",
                $"Forced minimal SaveData fallback after repeated {exception.GetType().Name}; repaired {repaired} field(s) and skipped the failing vanilla tail.");
            return true;
        }
        catch (Exception fallbackEx)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper",
                $"SaveData.AfterInitialize fallback failed: {fallbackEx}");
            return false;
        }
    }

    private static bool TryRecoverAfterInitialize(SaveData save, Exception exception)
    {
        if (save == null)
            return false;

        if (exception is not NullReferenceException && exception is not IndexOutOfRangeException)
            return false;

        int repaired;
        try
        {
            repaired = EnsureSaveDataStructure(save);
            repaired += EnsureExtendedSaveAreaStats(save);
        }
        catch (Exception repairException)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper",
                $"SaveData.AfterInitialize recovery failed before retry: {repairException.Message}");
            return false;
        }

        if (repaired <= 0)
            return false;

        Logger.Log(LogLevel.Warn, "MaggyHelper",
            $"SaveData.AfterInitialize hit {exception.GetType().Name}; repaired {repaired} AreaStats entr{(repaired == 1 ? "y" : "ies")} and retrying once.");
        return true;
    }

    private static int EnsureSaveDataStructure(SaveData save)
    {
        if (save == null)
            return 0;

        int repaired = 0;
        DynamicData saveDyn = DynamicData.For(save);

        repaired += EnsureLevelSetCollection(TryGetMember<IList>(saveDyn, "LevelSets"));
        repaired += EnsureLevelSetCollection(TryGetMember<IList>(saveDyn, "LevelSetRecycleBin"));

        if (repaired > 0)
        {
            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Repaired {repaired} save structure entr{(repaired == 1 ? "y" : "ies")} before save load retry");
        }

        return repaired;
    }

    private static int EnsureLevelSetCollection(IList levelSets)
    {
        if (levelSets == null)
            return 0;

        int repaired = 0;

        for (int i = levelSets.Count - 1; i >= 0; i--)
        {
            object levelSet = levelSets[i];
            if (levelSet == null)
            {
                levelSets.RemoveAt(i);
                repaired++;
                continue;
            }

            DynamicData levelSetDyn = DynamicData.For(levelSet);

            if (TryGetMember<string>(levelSetDyn, "Name") == null
                && TrySetMember(levelSet, levelSetDyn, "Name", string.Empty))
            {
                repaired++;
            }

            repaired += EnsureCollectionMember(levelSet, levelSetDyn, "Areas");
            repaired += EnsureCollectionMember(levelSet, levelSetDyn, "Poem");
        }

        return repaired;
    }

    private static int EnsureCollectionMember(object target, DynamicData dyn, string memberName)
    {
        if (target == null || dyn == null)
            return 0;

        if (TryGetMember<IList>(dyn, memberName) != null)
            return 0;

        if (!TryCreateMemberInstance(target.GetType(), memberName, out object instance))
            return 0;

        return TrySetMember(target, dyn, memberName, instance) ? 1 : 0;
    }

    private static bool TryCreateMemberInstance(Type targetType, string memberName, out object instance)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        instance = null;

        Type memberType = targetType.GetProperty(memberName, flags)?.PropertyType
            ?? targetType.GetField(memberName, flags)?.FieldType;

        if (memberType == null)
            return false;

        try
        {
            instance = Activator.CreateInstance(memberType);
            return instance != null;
        }
        catch
        {
            return false;
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

    private static object EnsureSafeAreaStats(AreaKey area, object oldStats)
    {
        try
        {
            object safeStats = oldStats;

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

    private static object CreateFallbackAreaStats(AreaKey area, string sid = null)
    {
        Type areaStatsType = RuntimeAreaStatsType;
        if (areaStatsType == null)
            return null;

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
                    culture: null) is object created)
                {
                    EnsureAreaStatsIdentity(created, area.ID, sid);
                    return created;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static int EnsureStoredAreaStatsList(IList storedAreas, string levelSetName)
    {
        if (storedAreas == null)
            return 0;

        List<int> rootAreaIds = GetRootAreaIdsForLevelSet(levelSetName);
        int repaired = 0;

        while (storedAreas.Count < rootAreaIds.Count)
        {
            storedAreas.Add(null);
            repaired++;
        }

        for (int i = storedAreas.Count - 1; i >= rootAreaIds.Count; i--)
        {
            if (storedAreas[i] == null)
            {
                storedAreas.RemoveAt(i);
                repaired++;
            }
        }

        for (int i = 0; i < rootAreaIds.Count; i++)
        {
            int areaId = rootAreaIds[i];
            AreaData areaData = AreaData.Areas[areaId];
            string sid = areaData?.SID;

            object stats = storedAreas[i];
            if (stats == null)
            {
                stats = CreateFallbackAreaStats(new AreaKey(areaId, global::Celeste.AreaMode.Normal), sid);
                if (stats == null)
                    continue;

                storedAreas[i] = stats;
                repaired++;
            }

            repaired += EnsureAreaStatsIdentity(stats, areaId, sid);

            int existingModes = GetModesArray(stats)?.Length ?? 0;
            int requiredModes = Math.Max(areaData?.Mode?.Length ?? 0, 3);
            EnsureAreaModeStatsArray(stats, requiredModes);
            if (existingModes < requiredModes)
                repaired++;
        }

        return repaired;
    }

    private static int EnsureAreaStatsIdentity(object stats, int areaId, string sid)
    {
        if (stats == null)
            return 0;

        int repaired = 0;
        DynamicData dyn = DynamicData.For(stats);

        int currentId = TryGetMember(dyn, "ID_Safe", int.MinValue);
        if (currentId == int.MinValue)
            currentId = TryGetMember(dyn, "ID_Unsafe", int.MinValue);
        if (currentId == int.MinValue)
            currentId = TryGetMember(dyn, "ID", int.MinValue);

        if (currentId < 0 || currentId >= AreaData.Areas.Count)
        {
            if (TrySetMember(stats, dyn, "ID_Safe", areaId)
                || TrySetMember(stats, dyn, "ID_Unsafe", areaId)
                || TrySetMember(stats, dyn, "ID", areaId))
            {
                repaired++;
            }
        }

        string currentSid = TryGetMember<string>(dyn, "SID");
        if (string.IsNullOrEmpty(currentSid) && !string.IsNullOrEmpty(sid))
        {
            if (TrySetMember(stats, dyn, "SID", sid))
                repaired++;
        }

        return repaired;
    }

    private static List<int> GetRootAreaIdsForLevelSet(string levelSetName)
    {
        List<int> ids = new();
        if (AreaData.Areas == null)
            return ids;

        for (int areaId = 0; areaId < AreaData.Areas.Count; areaId++)
        {
            AreaData area = AreaData.Areas[areaId];
            if (area == null)
                continue;

            if (!string.Equals(GetLevelSetName(area), levelSetName, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrEmpty(GetParentSid(area)))
                continue;

            ids.Add(areaId);
        }

        return ids;
    }

    private static string GetLevelSetName(AreaData area)
    {
        if (area == null)
            return null;

        string levelSet = TryGetMember<string>(DynamicData.For(area), "LevelSet");
        if (!string.IsNullOrEmpty(levelSet))
            return levelSet;

        string sid = area.SID;
        int slash = sid?.LastIndexOf('/') ?? -1;
        return slash > 0 ? sid[..slash] : "Celeste";
    }

    private static string GetParentSid(AreaData area)
    {
        if (area == null)
            return null;

        DynamicData areaDyn = DynamicData.For(area);
        object meta = TryGetMember<object>(areaDyn, "Meta");
        if (meta == null)
            return null;

        DynamicData metaDyn = DynamicData.For(meta);
        return TryGetMember<string>(metaDyn, "ParentSID")
            ?? TryGetMember<string>(metaDyn, "ParentSid")
            ?? TryGetMember<string>(metaDyn, "Parent");
    }

    private static int EnsureExtendedSaveAreaStats(SaveData save)
    {
        if (save?.Areas_Safe == null || AreaData.Areas == null || AreaData.Areas.Count == 0)
            return 0;

        int repaired = 0;

        DynamicData saveDyn = DynamicData.For(save);
        repaired += EnsureStoredAreaStatsList(TryGetMember<IList>(saveDyn, "Areas_Unsafe"), "Celeste");

        IList levelSets = TryGetMember<IList>(saveDyn, "LevelSets");
        if (levelSets != null)
        {
            for (int i = 0; i < levelSets.Count; i++)
            {
                object levelSet = levelSets[i];
                if (levelSet == null)
                    continue;

                DynamicData levelSetDyn = DynamicData.For(levelSet);
                string name = TryGetMember<string>(levelSetDyn, "Name");
                if (string.IsNullOrEmpty(name))
                    continue;

                repaired += EnsureStoredAreaStatsList(TryGetMember<IList>(levelSetDyn, "Areas"), name);
            }
        }

        if (repaired > 0)
        {
            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Repaired {repaired} stored AreaStats entry/entries before save load retry");
        }

        return repaired;
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

    private static Array GetModesArray(object stats)
    {
        if (stats == null)
            return null;

        DynamicData dyn = DynamicData.For(stats);
        return TryGetMember<Array>(dyn, "Modes") ?? TryGetMember<Array>(dyn, "modes");
    }

    private static Type GetModesArrayType(object stats)
    {
        if (stats == null)
            return null;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return stats.GetType().GetProperty("Modes", flags)?.PropertyType
            ?? stats.GetType().GetProperty("modes", flags)?.PropertyType
            ?? stats.GetType().GetField("Modes", flags)?.FieldType
            ?? stats.GetType().GetField("modes", flags)?.FieldType;
    }

    private static void EnsureAreaModeStatsArray(object stats, int requiredModes)
    {
        var dyn = new DynamicData(stats);
        Array modes = GetModesArray(stats);
        Type arrayType = GetModesArrayType(stats);
        Type modeType = arrayType?.GetElementType();

        if (modeType == null)
            return;

        if (modes == null)
        {
            modes = Array.CreateInstance(modeType, requiredModes);
            TrySetMember(stats, dyn, "Modes", modes);
            TrySetMember(stats, dyn, "modes", modes);
        }
        else if (modes.Length < requiredModes)
        {
            Array resized = Array.CreateInstance(modeType, requiredModes);
            Array.Copy(modes, resized, modes.Length);
            modes = resized;
            TrySetMember(stats, dyn, "Modes", modes);
            TrySetMember(stats, dyn, "modes", modes);
        }

        for (int i = 0; i < requiredModes; i++)
        {
            if (modes.GetValue(i) == null)
            {
                object modeValue = Activator.CreateInstance(modeType, nonPublic: true);
                modes.SetValue(modeValue, i);
            }

            EnsureAreaModeStatsSafety(modes.GetValue(i));
        }
    }

    private static void EnsureAreaModeStatsSafety(object modeStats)
    {
        if (modeStats == null)
            return;

        // Some external hooks expect reference members inside AreaModeStats to exist.
        // Initialize any null reference-type fields/properties with safe defaults.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Type modeStatsType = modeStats.GetType();

        foreach (var field in modeStatsType.GetFields(flags))
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

        foreach (var prop in modeStatsType.GetProperties(flags))
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
    internal static object TryGetSaveAreaStats(int areaId)
    {
        var saveData = SaveData.Instance;
        if (saveData?.Areas_Safe == null || areaId < 0 || areaId >= saveData.Areas_Safe.Count)
            return null;

        return saveData.Areas_Safe[areaId];
    }

    internal static object TryGetSaveAreaStats(AreaKey area)
    {
        return TryGetSaveAreaStats(area.ID);
    }

    internal static int GetSaveAreaModeCount(int areaId)
    {
        Array modes = GetModesArray(TryGetSaveAreaStats(areaId));
        return modes?.Length ?? 0;
    }

    internal static bool GetSaveAreaModeHeartGem(int areaId, int modeIndex)
    {
        return GetSaveAreaModeBool(areaId, modeIndex, "HeartGem");
    }

    internal static bool GetSaveAreaModeCompleted(int areaId, int modeIndex)
    {
        return GetSaveAreaModeBool(areaId, modeIndex, "Completed");
    }

    internal static bool SetSaveAreaModeHeartGem(int areaId, int modeIndex, bool value)
    {
        return SetSaveAreaModeBool(areaId, modeIndex, "HeartGem", value);
    }

    private static bool GetSaveAreaModeBool(int areaId, int modeIndex, string memberName)
    {
        object modeStats = GetSaveAreaModeStats(areaId, modeIndex);
        if (modeStats == null)
            return false;

        DynamicData dyn = DynamicData.For(modeStats);
        return TryGetMember(dyn, memberName, false);
    }

    private static bool SetSaveAreaModeBool(int areaId, int modeIndex, string memberName, bool value)
    {
        object modeStats = GetSaveAreaModeStats(areaId, modeIndex);
        if (modeStats == null)
            return false;

        DynamicData dyn = DynamicData.For(modeStats);
        return TrySetMember(modeStats, dyn, memberName, value);
    }

    private static object GetSaveAreaModeStats(int areaId, int modeIndex)
    {
        if (modeIndex < 0)
            return null;

        Array modes = GetModesArray(TryGetSaveAreaStats(areaId));
        if (modes == null || modeIndex >= modes.Length)
            return null;

        return modes.GetValue(modeIndex);
    }

    public static bool IsSideUnlocked(AreaKey area, int modeIndex)
    {
        if (modeIndex == MODE_NORMAL) return true;

        var saveData = SaveData.Instance;
        if (saveData == null) return false;

        // Cheat mode bypasses all side unlock requirements
        if (saveData.CheatMode) return true;

        if (TryGetSaveAreaStats(area) == null) return false;

        // Each side requires the previous side to be completed
        int previousMode = modeIndex - 1;
        if (previousMode < 0) return true;

        // Check if the previous mode's area stats indicate completion
        if (previousMode < GetSaveAreaModeCount(area.ID))
        {
            return GetSaveAreaModeHeartGem(area.ID, previousMode)
                || GetSaveAreaModeCompleted(area.ID, previousMode);
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
