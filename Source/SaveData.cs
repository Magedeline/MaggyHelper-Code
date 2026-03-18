#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0436 // Local patch types intentionally shadow imported Celeste runtime types.

using Celeste.Mod;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    public class patch_SaveData : SaveData {

        public static new patch_SaveData Instance;

        private static readonly int VanillaModeCount = Enum.GetValues(typeof(AreaMode)).Length;

        public new List<LevelSetStats> LevelSets = new List<LevelSetStats>();

        public new List<LevelSetStats> LevelSetRecycleBin = new List<LevelSetStats>();

        public new bool HasModdedSaveData = false;

        [XmlIgnore]
        public new string LevelSet => LevelSetStats?.Name ?? "Celeste";

        [XmlIgnore]
        public new static int LoadedModSaveDataIndex = int.MinValue;

        private LevelSetStats _cached_LevelSetStats = null;
        private string _cached_LevelSetStats_LevelSet = null;

        private string GetCurrentLevelSetNameOrDefault() {
            return TryGetAreaKeyLevelSet(LastArea, out string levelSet)
                ? levelSet
                : "Celeste";
        }

        private LevelSetStats computeLevelSetStats() {
            Logger.Verbose("SaveData", "Recomputing SaveData.LevelSetStats");
            string name = GetCurrentLevelSetNameOrDefault();
            LevelSets ??= new List<LevelSetStats>();
            LevelSetStats set = LevelSets.Find(other => other?.Name == name);

            if (set == null) {
                // Just silently add the missing levelset.
                LevelSets.Add(set = new LevelSetStats {
                    SaveData = this,
                    Name = name,
                    UnlockedAreas = 0
                });
                _cached_Areas_Safe = null;
            }

            // If the levelset doesn't exist in AreaData.Areas anymore (offset == -1), fall back.
            if (name != "Celeste" && set.AreaOffset == -1) {
                LastArea = AreaKey.Default;
                // Recurse - get the new, proper level set.
                return LevelSetStats;
            }

            return set;
        }

        [XmlIgnore]
        public new LevelSetStats LevelSetStats {
            get {
                string name = GetCurrentLevelSetNameOrDefault();
                if (_cached_LevelSetStats == null || _cached_LevelSetStats_LevelSet != name) {
                    _cached_LevelSetStats = computeLevelSetStats();
                    _cached_LevelSetStats_LevelSet = name;
                }
                return _cached_LevelSetStats ??= new LevelSetStats {
                    SaveData = this,
                    Name = name,
                    UnlockedAreas = 0
                };
            }
        }

        // We want use LastArea_Safe instead of LastArea to avoid breaking vanilla Celeste.

        [MonoModLinkFrom("Celeste.AreaKey Celeste.SaveData::LastArea_Unsafe")]
        public new AreaKey LastArea;

        [MonoModRemove]
        public AreaKey LastArea_Unsafe;

        [MonoModLinkFrom("Celeste.AreaKey Celeste.SaveData::LastArea")]
        public new AreaKey LastArea_Safe;

        // We want use CurrentSession_Safe instead of CurrentSession to avoid breaking vanilla Celeste.

        [MonoModLinkFrom("Celeste.Session Celeste.SaveData::CurrentSession_Unsafe")]
        public new Session CurrentSession;

        [MonoModRemove]
        public Session CurrentSession_Unsafe;

        [MonoModLinkFrom("Celeste.Session Celeste.SaveData::CurrentSession")]
        public new Session CurrentSession_Safe;

        // Legacy code should benefit from the new LevelSetStats.

        [MonoModLinkFrom("System.Int32 Celeste.SaveData::UnlockedAreas_Unsafe")]
        public new int UnlockedAreas;

        [MonoModRemove]
        public int UnlockedAreas_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::UnlockedAreas")]
        public new int UnlockedAreas_Safe {
            get {
                if (LevelSet == "Celeste")
                    return UnlockedAreas_Unsafe;
                LevelSetStats stats = LevelSetStats;
                return stats.AreaOffset + stats.UnlockedAreas;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    UnlockedAreas_Unsafe = value;
                    return;
                }
                LevelSetStats stats = LevelSetStats;
                stats.UnlockedAreas = value - stats.AreaOffset;
            }
        }


        [MonoModLinkFrom("System.Int32 Celeste.SaveData::TotalStrawberries_Unsafe")]
        public new int TotalStrawberries;

        [MonoModRemove]
        public int TotalStrawberries_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Int32 Celeste.SaveData::TotalStrawberries")]
        public new int TotalStrawberries_Safe {
            get {
                if (LevelSet == "Celeste")
                    return TotalStrawberries_Unsafe;
                return LevelSetStats.TotalStrawberries;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    TotalStrawberries_Unsafe = value;
                    return;
                }
                LevelSetStats.TotalStrawberries = value;
            }
        }

        // Make TotalHeartGems return the crystal heart count for the current level set, like TotalStrawberries does.
        public new int TotalHeartGems {
            [MonoModReplace]
            get {
                return LevelSetStats.TotalHeartGems;
            }
        }

        public new int TotalHeartGemsInVanilla => GetLevelSetStatsFor("Celeste")?.TotalHeartGems ?? 0;

        [MonoModLinkFrom("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas_Unsafe")]
        public new List<AreaStats> Areas;

        [MonoModRemove]
        public List<patch_AreaStats> Areas_Unsafe;

        private List<patch_AreaStats> _cached_Areas_Safe = null;
        private static patch_AreaStats CreateAreaStats(int areaId) {
            patch_AreaStats stats = new patch_AreaStats(areaId);
            EnsureAreaStatsModes(stats);
            return stats;
        }

        internal static void EnsureAreaStatsModes(AreaStats area) {
            if (area == null)
                return;

            if (area.Modes == null || area.Modes.Length != VanillaModeCount) {
                AreaModeStats[] modes = new AreaModeStats[VanillaModeCount];
                if (area.Modes != null)
                    Array.Copy(area.Modes, modes, Math.Min(area.Modes.Length, modes.Length));
                area.Modes = modes;
            }

            for (int index = 0; index < area.Modes.Length; index++)
                area.Modes[index] ??= new AreaModeStats();
        }

        private static patch_AreaStats EnsureAreaStatsEntry(List<patch_AreaStats> areas, int index, int areaId) {
            while (areas.Count <= index)
                areas.Add(CreateAreaStats(areaId));

            patch_AreaStats stats = areas[index];
            if (stats == null) {
                stats = CreateAreaStats(areaId);
                areas[index] = stats;
            } else {
                EnsureAreaStatsModes(stats);
            }

            return stats;
        }

        internal static bool IsModeCompleted(AreaStats area, int modeIndex) {
            return area?.Modes != null
                && modeIndex >= 0
                && modeIndex < area.Modes.Length
                && area.Modes[modeIndex]?.Completed == true;
        }

        internal static bool HasHeartGem(AreaStats area, int modeIndex) {
            return area?.Modes != null
                && modeIndex >= 0
                && modeIndex < area.Modes.Length
                && area.Modes[modeIndex]?.HeartGem == true;
        }

        internal static T TryGetMember<T>(DynamicData dyn, string name, T fallback = default) {
            if (dyn == null)
                return fallback;

            try {
                return dyn.Get<T>(name);
            } catch {
                return fallback;
            }
        }

        internal static string GetAreaLevelSet(AreaData area) {
            if (area == null)
                return null;

            string levelSet = TryGetMember<string>(DynamicData.For(area), "LevelSet");
            if (!string.IsNullOrEmpty(levelSet))
                return levelSet;

            string sid = area.SID;
            int slash = sid?.LastIndexOf('/') ?? -1;
            return slash > 0 ? sid.Substring(0, slash) : "Celeste";
        }

        internal static string GetAreaParentSid(AreaData area) {
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

        internal static AreaData FindAreaBySid(string sid) {
            if (string.IsNullOrEmpty(sid) || AreaData.Areas == null)
                return null;

            for (int index = 0; index < AreaData.Areas.Count; index++) {
                AreaData area = AreaData.Areas[index];
                if (area != null && string.Equals(area.SID, sid, StringComparison.Ordinal))
                    return area;
            }

            return null;
        }

        internal static AreaData FindAreaByStats(AreaStats stats) {
            if (stats == null)
                return null;

            DynamicData dyn = DynamicData.For(stats);
            string sid = TryGetMember<string>(dyn, "SID");
            AreaData area = FindAreaBySid(sid);
            if (area != null)
                return area;

            int id = TryGetMember(dyn, "ID_Safe", int.MinValue);
            if (id == int.MinValue)
                id = TryGetMember(dyn, "ID_Unsafe", int.MinValue);
            if (id == int.MinValue)
                id = TryGetMember(dyn, "ID", int.MinValue);

            return id >= 0 && AreaData.Areas != null && id < AreaData.Areas.Count
                ? AreaData.Areas[id]
                : null;
        }

        internal static int GetAreaOffsetForLevelSet(string levelSetName) {
            if (AreaData.Areas == null)
                return -1;

            for (int index = 0; index < AreaData.Areas.Count; index++) {
                if (string.Equals(GetAreaLevelSet(AreaData.Areas[index]), levelSetName, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        internal static int CountRootAreasForLevelSet(string levelSetName) {
            if (AreaData.Areas == null)
                return 0;

            int count = 0;
            for (int index = 0; index < AreaData.Areas.Count; index++) {
                AreaData area = AreaData.Areas[index];
                if (!string.Equals(GetAreaLevelSet(area), levelSetName, StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(GetAreaParentSid(area)))
                    continue;
                count++;
            }
            return count;
        }

        internal static int CountAllAreasForLevelSet(string levelSetName) {
            if (AreaData.Areas == null)
                return 0;

            int count = 0;
            for (int index = 0; index < AreaData.Areas.Count; index++) {
                if (string.Equals(GetAreaLevelSet(AreaData.Areas[index]), levelSetName, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static bool IsMissingOrDefaultAreaKey(AreaKey key) {
            object boxed = key;
            if (boxed == null)
                return true;

            try {
                return key.ID == 0;
            } catch {
                return true;
            }
        }

        private static bool TryGetAreaKeyLevelSet(AreaKey key, out string levelSet) {
            object boxed = key;
            if (boxed == null) {
                levelSet = null;
                return false;
            }

            try {
                levelSet = key.GetLevelSet();
                return !string.IsNullOrEmpty(levelSet);
            } catch {
                levelSet = null;
                return false;
            }
        }

        private static int CompareLevelSets(LevelSetStats set1, LevelSetStats set2) {
            if (ReferenceEquals(set1, set2))
                return 0;
            if (set1 == null)
                return 1;
            if (set2 == null)
                return -1;

            try {
                return set1.AreaOffset.CompareTo(set2.AreaOffset);
            } catch {
                return string.Compare(set1.Name, set2.Name, StringComparison.Ordinal);
            }
        }

        private static string GetSaveFilePath() {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try {
                MethodInfo method = typeof(UserIO).GetMethod("GetSaveFilePath", Flags, null, Type.EmptyTypes, null);
                if (method?.Invoke(null, null) is string path && !string.IsNullOrWhiteSpace(path))
                    return path;
            } catch {
            }

            try {
                PropertyInfo property = typeof(UserIO).GetProperty("SaveFilePath", Flags)
                    ?? typeof(UserIO).GetProperty("SavePath", Flags);
                if (property?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
                    return path;
            } catch {
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Celeste", "Saves");
        }

        private List<patch_AreaStats> computeAreasSafe() {
            Logger.Verbose("SaveData", "Recomputing SaveData.Areas_Safe");
            List<patch_AreaStats> areasAll = new List<patch_AreaStats>(Areas_Unsafe);
            foreach (LevelSetStats set in LevelSets) {
                if (set?.Areas != null)
                    areasAll.AddRange(set.Areas);
            }
            return areasAll;
        }

        private static int NormalizeLevelSetList(List<LevelSetStats> levelSets) {
            if (levelSets == null)
                return 0;

            int repaired = 0;

            for (int i = levelSets.Count - 1; i >= 0; i--) {
                LevelSetStats set = levelSets[i];
                if (set == null) {
                    levelSets.RemoveAt(i);
                    repaired++;
                    continue;
                }

                set.Name ??= string.Empty;
                set.Areas ??= new List<patch_AreaStats>();
                set.Poem ??= new List<string>();
            }

            return repaired;
        }

        [XmlIgnore]
        [MonoModLinkFrom("System.Collections.Generic.List`1<Celeste.AreaStats> Celeste.SaveData::Areas")]
        public new List<patch_AreaStats> Areas_Safe {
            get {
                _cached_Areas_Safe ??= computeAreasSafe();
                return _cached_Areas_Safe;
            }
            set {
                _cached_Areas_Safe = null;
                _cached_LevelSetStats = null;

                if (LevelSets == null && value.Count == 0) {
                    Areas_Unsafe = value;
                    return;
                }

                int i = 0;
                Areas_Unsafe = value.GetRange(i, Areas_Unsafe.Count);
                i += Areas_Unsafe.Count;
                foreach (LevelSetStats set in LevelSets) {
                    set.Areas = value.GetRange(i, set.Areas.Count);
                    i += set.Areas.Count;
                }
            }
        }

        public new int UnlockedModes {
            [MonoModReplace]
            get {
                if (DebugMode || CheatMode) {
                    return 3;
                }

                return LevelSetStats.UnlockedModes;
            }
        }

        public new int MaxArea {
            [MonoModReplace]
            get {
                LevelSetStats stats = LevelSetStats;
                return stats.AreaOffset + stats.MaxArea;
            }
        }

        public new int MaxAssistArea {
            [MonoModReplace]
            get {
                LevelSetStats stats = LevelSetStats;
                return stats.AreaOffset + stats.MaxAssistArea;
            }
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<System.String> Celeste.SaveData::Poem_Unsafe")]
        public new List<string> Poem;

        [MonoModRemove]
        public List<string> Poem_Unsafe;

        [XmlIgnore]
        [MonoModLinkFrom("System.Collections.Generic.List`1<System.String> Celeste.SaveData::Poem")]
        public new List<string> Poem_Safe {
            get {
                if (LevelSet == "Celeste")
                    return Poem_Unsafe;
                return LevelSetStats.Poem;
            }
            set {
                if (LevelSets == null || LevelSet == "Celeste") {
                    Poem_Unsafe = value;
                    return;
                }
                LevelSetStats.Poem = value;
            }
        }

        public new int TotalCassettes {
            [MonoModReplace] // optimise the method
            get {
                int totalCassettes = 0;
                List<patch_AreaStats> areas = Areas_Safe; // this getter hides extremely expensive calculations. Evil!
                int maxArea = MaxArea;

                for (int i = 0; i <= maxArea; ++i) {
                    AreaData areaData = AreaData.Get(i);
                    if (i < areas.Count && areas[i]?.Cassette == true && areaData != null && !areaData.Interlude)
                        totalCassettes++;
                }
                return totalCassettes;
            }
        }

        public static new extern void orig_Start(SaveData data, int slot);
        public static new void Start(SaveData data, int slot) {
            orig_Start(data, slot);

            LoadModSaveData(slot);

            // load session data.
            foreach (EverestModule mod in Everest.Modules) {
                if (mod.SaveDataAsync) {
                    mod.DeserializeSession(slot, mod.ReadSession(slot));
                } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                    mod.LoadSession(slot, false);
#pragma warning restore CS0618
                }
            }
        }

        /// <summary>
        /// Load mod saves only when the given slot is not the currently loaded slot.
        /// This does NOT load mod sessions, which are loaded on Start, making this ideal for f.e. UI purposes.
        /// </summary>
        /// <param name="slot">The slot to load</param>
        public static new void LoadModSaveData(int slot) {
            if (LoadedModSaveDataIndex != slot) {
                foreach (EverestModule mod in Everest.Modules) {
                    if (mod.SaveDataAsync) {
                        mod.DeserializeSaveData(slot, mod.ReadSaveData(slot));
                    } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                        mod.LoadSaveData(slot);
#pragma warning restore CS0618
                    }
                }
                LoadedModSaveDataIndex = slot;
            }
        }

        public static new extern bool orig_TryDelete(int slot);
        public static new bool TryDelete(int slot) {
            bool vanillaExists = false;
            string saveFilePath = GetSaveFilePath();
            string saveFileName = GetFilename(slot);
            if (Directory.Exists(saveFilePath))
                vanillaExists = File.Exists(Path.Combine(saveFilePath, $"{saveFileName}.celeste"));

            if (vanillaExists) {
                if (!orig_TryDelete(slot)) {
                    return false;
                }
            }
            else
                Logger.Warn("SaveData", $"Deleting save data for slot {saveFileName} which has no vanilla data");

            return TryDeleteModSaveData(slot);
        }

        public static new bool TryDeleteModSaveData(int slot) {
            foreach (EverestModule mod in Everest.Modules) {
                if (mod.SaveDataAsync) {
                    mod.WriteSaveData(slot, null);
                    mod.WriteSession(slot, null);
                } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                    mod.DeleteSaveData(slot);
                    mod.DeleteSession(slot);
#pragma warning restore CS0618
                }
            }

            // clean modsave and modsession files which are not deleted if their module is not loaded
            string saveFilePath = GetSaveFilePath();
            string saveFileName = GetFilename(slot);
            if (Directory.Exists(saveFilePath)) {
                foreach (string modSaveFile in Directory.GetFiles(saveFilePath, $"{saveFileName}-modsave-*.celeste")) {
                    string file = Path.GetFileNameWithoutExtension(modSaveFile);
                    Logger.Info("SaveData", $"Save slot {saveFileName} has modsave {file} which was not cleaned, deleting file");
                    UserIO.Delete(file);
                }

                foreach (string modSessionFile in Directory.GetFiles(saveFilePath, $"{saveFileName}-modsession-*.celeste")) {
                    string file = Path.GetFileNameWithoutExtension(modSessionFile);
                    Logger.Info("SaveData", $"Save slot {saveFileName} has modsession {file} which was not cleaned, deleting file");
                    UserIO.Delete(file);
                }
            }

            LoadedModSaveDataIndex = int.MinValue;

            // delete the modsavedata file if it exists.
            string modSaveDataName = $"{saveFileName}-modsavedata";
            if (UserIO.Exists(modSaveDataName)) {
                return UserIO.Delete(modSaveDataName);
            } else {
                return true;
            }
        }

        public new extern void orig_StartSession(Session session);
        public new void StartSession(Session session) {
            Session sessionPrev = CurrentSession;

            orig_StartSession(session);

            if (sessionPrev != session) {
                foreach (EverestModule mod in Everest.Modules) {
                    if (mod.SaveDataAsync) {
                        mod.DeserializeSession(FileSlot, null);
                    } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                        mod.LoadSession(FileSlot, true);
#pragma warning restore CS0618
                    }
                }
            }
        }

        [MonoModReplace]
        [MonoModPublic]
        public static new string GetFilename(int slot) {
            if (slot == -1)
                return "debug";
            return slot.ToString();
        }

        [MonoModReplace]
        public static new void InitializeDebugMode(bool loadExisting = true) {
            SaveData save = null;
            if (loadExisting && UserIO.Open(UserIO.Mode.Read)) {
                save = UserIO.Load<SaveData>(GetFilename(-1));
                UserIO.Close();
            }

            save = save ?? new SaveData();
            NormalizeDebugSaveState(save);
            save.DebugMode = true;

            Start(save, -1);
        }

        private static void NormalizeDebugSaveState(SaveData save) {
            if (save is not patch_SaveData patch)
                return;

            patch.Name ??= "DEBUG";
            patch.LevelSets ??= new List<LevelSetStats>();
            patch.LevelSetRecycleBin ??= new List<LevelSetStats>();
            patch.Areas_Unsafe ??= new List<patch_AreaStats>();

            try {
                patch.LastArea = AreaKey.Default;
                patch.LastArea_Safe = AreaKey.Default;
                patch.LastArea_Unsafe = AreaKey.Default;
            } catch {
            }

            patch.CurrentSession = null;
            patch.CurrentSession_Safe = null;
            patch.CurrentSession_Unsafe = null;

            patch.TheoSisterName ??= string.Empty;
        }

        [MonoModReplace]
        public new void AfterInitialize() {
            string stage = "start";

            try {
            // Vanilla / new saves don't have the LevelSets list.
            stage = "init-levelset-containers";
            if (LevelSets == null)
                LevelSets = new List<LevelSetStats>();

            if (LevelSetRecycleBin == null)
                LevelSetRecycleBin = new List<LevelSetStats>();

            Name ??= string.Empty;

            stage = "normalize-levelset-lists";
            NormalizeLevelSetList(LevelSets);
            NormalizeLevelSetList(LevelSetRecycleBin);

            stage = "restore-mod-backup-if-needed";
            if (LevelSets.Count <= 1 && LevelSetRecycleBin.Count == 0 && !HasModdedSaveData) {
                // the save file doesn't have any mod save data (just created, overwritten by vanilla, or Everest just updated).
                // we want to carry mod save data that was backed up in the mod save file, if any.
                string saveFileName = GetFilename(FileSlot);
                ModSaveData modSaveData = UserIO.Load<ModSaveData>($"{saveFileName}-modsavedata");
                if (modSaveData != null) {
                    modSaveData.CopyToCelesteSaveData(this);
                    Logger.Warn("SaveData", $"{LevelSets.Count} level set(s) were restored from mod backup for save slot {saveFileName}");
                }
            }

            HasModdedSaveData = true;

            stage = "init-area-list";
            if (Areas_Unsafe == null)
                Areas_Unsafe = new List<patch_AreaStats>();

            // Add missing LevelSetStats.
            stage = "ensure-levelsets-from-areadata";
            foreach (AreaData area in AreaData.Areas) {
                string set = GetAreaLevelSet(area);
                if (!LevelSets.Exists(other => other != null && other.Name == set)) {
                    LevelSetStats recycleBinLevelSet = LevelSetRecycleBin.FirstOrDefault(other => other != null && other.Name == set);
                    if (recycleBinLevelSet != null) {
                        // the level set is actually in the recycle bin - restore it.
                        LevelSets.Add(recycleBinLevelSet);
                        LevelSetRecycleBin.Remove(recycleBinLevelSet);
                    } else {
                        // create a new LevelSetStats entry.
                        LevelSets.Add(new LevelSetStats {
                            Name = set,
                            UnlockedAreas = set == "Celeste" ? UnlockedAreas_Unsafe : 0
                        });
                    }
                }
            }

            // Fill each LevelSetStats with its areas.
            stage = "populate-levelset-areas";
            for (int lsi = 0; lsi < LevelSets.Count; lsi++) {
                LevelSetStats set = LevelSets[lsi];
                if (set == null) {
                    LevelSets.RemoveAt(lsi);
                    lsi--;
                    continue;
                }

                set.Name ??= string.Empty;
                set.Areas ??= new List<patch_AreaStats>();
                set.Poem ??= new List<string>();
                set.SaveData = this;
                List<patch_AreaStats> areas = set.Areas;
                if (set.Name == "Celeste")
                    areas = Areas_Unsafe;

                // compute the level set's AreaOffset and MaxArea.
                stage = $"compute-bounds:{set.Name}";
                set.ComputeBounds();

                int offset = set.AreaOffset;
                if (offset == -1) {
                    // LevelSet gone - let's move it to the recycle bin.
                    LevelSetStats levelSetAlreadyInRecycleBin = LevelSetRecycleBin.FirstOrDefault(other => other != null && other.Name == set.Name);
                    if (levelSetAlreadyInRecycleBin != null) {
                        // a level set with the same name already exists in the recycle bin - replace it.
                        LevelSetRecycleBin.Remove(levelSetAlreadyInRecycleBin);
                    }
                    LevelSetRecycleBin.Add(set);

                    // now, remove it to prevent any unwanted access.
                    LevelSets.RemoveAt(lsi);
                    lsi--;
                    continue;
                }

                // Refresh all stat IDs based on their SIDs, sort, fill and remove leftovers.
                // Temporarily use ID_Unsafe; later ID_Safe to ID_Unsafe to resync the SIDs.
                // This keeps the stats bound to their SIDs, not their indices, while removing non-existent areas.
                int countRoots = CountRootAreasForLevelSet(set.Name);
                int countAll = CountAllAreasForLevelSet(set.Name);

                // Fix IDs
                stage = $"fix-ids:{set.Name}";
                for (int i = 0; i < areas.Count; i++) {
                    patch_AreaStats stats = EnsureAreaStatsEntry(areas, i, offset + i);
                    AreaData area = null;
                    try {
                        area = FindAreaByStats(stats);
                    } catch {
                        area = null;
                    }
                    if (!string.IsNullOrEmpty(GetAreaParentSid(area)))
                        area = null;
                    stats.ID_Unsafe = area?.ID ?? int.MaxValue;
                }

                // Sort
                stage = $"sort-areas:{set.Name}";
                areas.Sort((a, b) => ((patch_AreaStats) a).ID_Unsafe - ((patch_AreaStats) b).ID_Unsafe);

                // Remove leftovers
                stage = $"trim-leftovers:{set.Name}";
                while (areas.Count > 0 && ((patch_AreaStats) areas[areas.Count - 1]).ID_Unsafe == int.MaxValue)
                    areas.RemoveAt(areas.Count - 1);

                // Fill gaps
                stage = $"fill-gaps:{set.Name}";
                for (int i = 0; i < countRoots; i++)
                    if (i >= areas.Count || ((patch_AreaStats) areas[i]).ID_Unsafe != offset + i)
                        areas.Insert(i, CreateAreaStats(offset + i));

                // Duplicate parent stat refs into their respective children slots.
                stage = $"duplicate-parents:{set.Name}";
                for (int i = countRoots; i < countAll; i++) {
                    if (i >= areas.Count) {
                        AreaData childArea = offset + i >= 0 && offset + i < AreaData.Areas.Count
                            ? AreaData.Areas[offset + i]
                            : null;
                        AreaData parentArea = string.IsNullOrEmpty(GetAreaParentSid(childArea))
                            ? null
                            : FindAreaBySid(GetAreaParentSid(childArea));
                        int parentIndex = parentArea == null ? -1 : parentArea.ID - offset;
                        patch_AreaStats parentStats = parentIndex >= 0 && parentIndex < areas.Count
                            ? areas[parentIndex]
                            : null;
                        areas.Insert(i, parentStats ?? CreateAreaStats(offset + i));
                    }
                }

                // Resync SIDs
                stage = $"resync-sids:{set.Name}";
                for (int i = 0; i < areas.Count; i++) {
                    patch_AreaStats stats = EnsureAreaStatsEntry(areas, i, offset + i);
                    if (stats.ID_Unsafe != int.MaxValue)
                        stats.ID_Safe = stats.ID_Unsafe;
                }

                int lastCompleted = -1;
                stage = $"scan-completions:{set.Name}";
                for (int i = 0; i < countRoots; i++) {
                    if (IsModeCompleted(areas[i], 0)) {
                        lastCompleted = i;
                    }
                }

                if (set.Name == "Celeste") {
                    if (UnlockedAreas_Unsafe < lastCompleted + 1 && set.MaxArea >= lastCompleted + 1) {
                        UnlockedAreas_Unsafe = lastCompleted + 1;
                    }
                    if (DebugMode || CheatMode) {
                        UnlockedAreas_Unsafe = set.MaxArea;
                    }

                } else {
                    if (set.UnlockedAreas < lastCompleted + 1 && set.MaxArea >= lastCompleted + 1) {
                        set.UnlockedAreas = lastCompleted + 1;
                    }
                    if (DebugMode || CheatMode) {
                        set.UnlockedAreas = set.MaxArea;
                    }
                }

                stage = $"clean-checkpoints:{set.Name}";
                foreach (AreaStats area in areas) {
                    if (area == null)
                        continue;
                    EnsureAreaStatsModes(area);
                    area.CleanCheckpoints();
                }
            }

            // Assign SaveData for the level sets in the recycle bin to prevent crashes.
            stage = "prepare-recycle-bin";
            foreach (LevelSetStats set in LevelSetRecycleBin) {
                if (set == null)
                    continue;

                set.Areas ??= new List<patch_AreaStats>();
                set.Poem ??= new List<string>();
                set.Name ??= string.Empty;
                set.SaveData = this;
            }

            // Order the levelsets to appear just as their areas appear in AreaData.Areas
            stage = "sort-levelsets";
            LevelSets.Sort(CompareLevelSets);

            // If there is no mod progress, carry over any progress from vanilla saves.
            stage = "restore-legacy-state";
            if (IsMissingOrDefaultAreaKey(LastArea_Safe))
                LastArea_Safe = LastArea_Unsafe;
            if (CurrentSession_Safe == null)
                CurrentSession_Safe = CurrentSession_Unsafe;

            // Trick unmodded instances of Celeste to thinking that we last selected prologue / played no level.
            LastArea_Unsafe = AreaKey.Default;
            CurrentSession_Unsafe = null;

            // Fix areas with missing SID (f.e. deleted or renamed maps).
            if (AreaData.Get(LastArea) == null)
                LastArea = AreaKey.Default;

            // Fix out of bounds areas.
            if (LastArea.ID < 0 || LastArea.ID >= AreaData.Areas.Count)
                LastArea = AreaKey.Default;

            stage = "init-theo-name";
            if (string.IsNullOrEmpty(TheoSisterName)) {
                TheoSisterName = Dialog.Clean("THEO_SISTER_NAME", null) ?? string.Empty;
                if (!string.IsNullOrEmpty(Name)
                    && !string.IsNullOrEmpty(TheoSisterName)
                    && Name.IndexOf(TheoSisterName, StringComparison.InvariantCultureIgnoreCase) >= 0) {
                    TheoSisterName = Dialog.Clean("THEO_SISTER_ALT_NAME", null) ?? TheoSisterName;
                }
            }

            stage = "assist-mode-checks";
            AssistModeChecks();

            stage = "legacy-version-migration";
            if (Version != null) {
                Version v = new Version(Version);

                if (v < new Version(1, 2, 1, 1)) {
                    for (int id = 0; id < Areas_Unsafe.Count; id++) {
                        AreaStats area = Areas_Unsafe[id];
                        if (area == null)
                            continue;
                        for (int modei = 0; modei < area.Modes.Length; modei++) {
                            AreaModeStats mode = area.Modes[modei];
                            if (mode == null)
                                continue;
                            if (mode.BestTime > 0L) {
                                mode.SingleRunCompleted = true;
                            }
                            mode.BestTime = 0L;
                            mode.BestFullClearTime = 0L;
                        }
                    }
                }
            }

            stage = "clear-caches";
            _cached_Areas_Safe = null;
            _cached_LevelSetStats = null;
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, "MaggyHelper",
                    $"SaveData.AfterInitialize failed during stage '{stage}': {ex}");
                throw;
            }
        }

        public new extern void orig_BeforeSave();
        public new void BeforeSave() {
            // If we're in a Vanilla-compatible area, copy from _Safe (new) to _Unsafe (legacy).
            if (TryGetAreaKeyLevelSet(LastArea_Safe, out string lastAreaLevelSet) && lastAreaLevelSet == "Celeste")
                LastArea_Unsafe = LastArea_Safe;
            if (CurrentSession_Safe != null
                && TryGetAreaKeyLevelSet(CurrentSession_Safe.Area, out string currentSessionLevelSet)
                && currentSessionLevelSet == "Celeste")
                CurrentSession_Unsafe = CurrentSession_Safe;

            // Make sure that subchapter references to parent chapters aren't stored.
            // They'll be reverted afterwards with AfterInitialize.
            // Fill each LevelSetStats with its areas.
            foreach (LevelSetStats set in LevelSets) {
                if (set.Name == "Celeste")
                    continue;
                int countRoots = CountRootAreasForLevelSet(set.Name);
                List<patch_AreaStats> areas = set.Areas;
                while (areas.Count > countRoots)
                    areas.RemoveAt(areas.Count - 1);
            }


            orig_BeforeSave();
        }

        /// <summary>
        /// Get the statistics for a given level set.
        /// </summary>
        public new LevelSetStats GetLevelSetStatsFor(string name)
            => LevelSets?.Find(set => set?.Name == name);

        public new AreaStats GetAreaStatsFor(AreaKey key)
            => LevelSets.Find(set => set.Name == key.GetLevelSet())?.Areas?.Find(area => area?.SID == key.GetSID());

        public new extern HashSet<string> orig_GetCheckpoints(AreaKey area);
        public new HashSet<string> GetCheckpoints(AreaKey area) {
            HashSet<string> checkpoints = orig_GetCheckpoints(area);

            if (Celeste.PlayMode == Celeste.PlayModes.Event ||
                DebugMode || CheatMode) {
                return checkpoints;
            }

            // Remove any checkpoints which don't exist in the level.
            AreaData areaData = AreaData.Get(area);
            ModeProperties mode = areaData?.Mode != null && (int) area.Mode >= 0 && (int) area.Mode < areaData.Mode.Length
                ? areaData.Mode[(int) area.Mode]
                : null;
            if (mode?.Checkpoints == null) {
                checkpoints.Clear();
            } else {
                checkpoints.RemoveWhere(a => !mode.Checkpoints.Any(b => b.Level == a));
            }
            return checkpoints;
        }

    }
    [Serializable]
    public class LevelSetStats {

        internal patch_SaveData SaveData;

        [XmlAttribute]
        public string Name;

        [XmlIgnore]
        [NonSerialized]
        private int _UnlockedAreas;
        public int UnlockedAreas {
            get {
                if (Name == "Celeste" && SaveData != null)
                    return SaveData.UnlockedAreas_Unsafe;
                if (string.IsNullOrEmpty(Name))
                    return MaxArea;
                return Calc.Clamp(_UnlockedAreas, 0, AreasIncludingCeleste?.Count ?? 0);
            }
            set {
                if (Name == "Celeste" && SaveData != null) {
                    SaveData.UnlockedAreas_Unsafe = value;
                    return;
                }
                _UnlockedAreas = value;
            }
        }

        public List<patch_AreaStats> Areas = new List<patch_AreaStats>();
        [XmlIgnore]
        public List<patch_AreaStats> AreasIncludingCeleste => Name == "Celeste" ? SaveData?.Areas_Unsafe ?? new List<patch_AreaStats>() : Areas ?? new List<patch_AreaStats>();

        public List<string> Poem = new List<string>();

        [XmlIgnore]
        [NonSerialized]
        private int _TotalStrawberries;
        public int TotalStrawberries {
            get {
                // TODO: Dynamically calculate?
                if (Name == "Celeste" && SaveData != null)
                    return SaveData.TotalStrawberries_Unsafe;
                return _TotalStrawberries;
            }
            set {
                if (Name == "Celeste" && SaveData != null) {
                    SaveData.TotalStrawberries_Unsafe = value;
                    return;
                }
                _TotalStrawberries = value;
            }
        }

        [XmlIgnore]
        public int TotalGoldenStrawberries {
            get {
                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    AreaStats areaSave;
                    if (Name == "Celeste")
                        areaSave = SaveData?.Areas_Unsafe != null && i < SaveData.Areas_Unsafe.Count ? SaveData.Areas_Unsafe[i] : null;
                    else
                        areaSave = i < Areas.Count ? Areas[i] : null;

                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null || areaSave?.Modes == null)
                        continue;

                    for (int j = 0; j < areaData.Mode.Length && j < areaSave.Modes.Length; j++) {
                        AreaModeStats modeSave = areaSave.Modes[j];
                        ModeProperties modeData = areaData.Mode[j];

                        if (modeSave?.Strawberries == null || modeData?.MapData == null)
                            continue;

                        foreach (EntityID strawb in modeSave.Strawberries) {
                            if (modeData.MapData.Goldenberries.Any(berry => berry.ID == strawb.ID && berry.Level.Name == strawb.Level))
                                count++;
                            if (modeData.MapData.DashlessGoldenberries.Any(berry => berry.ID == strawb.ID && berry.Level.Name == strawb.Level))
                                count++;
                        }
                    }
                }

                return count;
            }
        }

        [XmlIgnore]
        public int MaxStrawberries {
            get {
                if (Name == "Celeste")
                    return 175;

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties mode in areaData.Mode) {
                        if (mode?.MapData?.Area == null || mode.MapData.Area.Mode > AreaMode.CSide)
                            continue;
                        count += mode.MapData.DetectedStrawberries;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int MaxStrawberriesIncludingUntracked {
            get {
                if (Name == "Celeste")
                    return 202;

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties mode in areaData.Mode) {
                        if (mode?.MapData == null)
                            continue;
                        count += mode.MapData.DetectedStrawberriesIncludingUntracked;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int MaxGoldenStrawberries {
            get {
                if (Name == "Celeste")
                    return 25; // vanilla is wrong (there are 26 including dashless), but don't mess with vanilla.

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties mode in areaData.Mode) {
                        if (mode?.MapData == null)
                            continue;
                        count += mode.MapData.Goldenberries.Count;
                        count += mode.MapData.DashlessGoldenberries.Count;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int MaxCassettes {
            get {
                if (Name == "Celeste")
                    return 8;

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties mode in areaData.Mode) {
                        if (mode?.MapData?.DetectedCassette == true)
                            count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int UnlockedModes {
            get {
                int offset = AreaOffset;

                bool completedAllBSides = true;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    if (areaIndex < 0 || areaIndex >= AreaData.Areas.Count) {
                        completedAllBSides = false;
                        continue;
                    }

                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData == null || !areaData.HasMode(AreaMode.BSide)) {
                        continue;
                    }

                    AreaStats areaStats = i < AreasIncludingCeleste.Count ? AreasIncludingCeleste[i] : null;
                    patch_SaveData.EnsureAreaStatsModes(areaStats);
                    AreaModeStats modeStats = areaStats?.Modes[(int) AreaMode.BSide];
                    bool interlude = areaData.Interlude;
                    ModeProperties bsideMode = areaData.Mode != null && areaData.Mode.Length > (int) AreaMode.BSide
                        ? areaData.Mode[(int) AreaMode.BSide]
                        : null;
                    bool mapHasHeartGem = bsideMode?.MapData?.DetectedHeartGem == true;
                    bool heartGemCollected = modeStats?.HeartGem == true;
                    bool completed = modeStats?.Completed == true;

                    if (!interlude && bsideMode?.MapData == null) {
                        completedAllBSides = false;
                        continue;
                    }

                    if (!interlude && !(mapHasHeartGem ? heartGemCollected : completed)) {
                        completedAllBSides = false;
                    }
                }

                if (completedAllBSides) {
                    return 3;
                }

                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData != null && !areaData.Interlude && i < AreasIncludingCeleste.Count && AreasIncludingCeleste[i]?.Cassette == true) {
                        return 2;
                    }
                }

                return 1;
            }
        }

        [XmlIgnore]
        public int AreaOffset {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get;
            private set;
        }

        [XmlIgnore]
        public int MaxArea {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get;
            private set;
        }

        internal void ComputeBounds() {
            AreaOffset = patch_SaveData.GetAreaOffsetForLevelSet(Name);

            int count = patch_SaveData.CountRootAreasForLevelSet(Name) - 1;
            if (Celeste.PlayMode == Celeste.PlayModes.Event)
                MaxArea = Math.Min(count, AreaOffset + 2);
            else
                MaxArea = count;
        }

        [XmlIgnore]
        public int MaxAssistArea {
            get {
                return MaxArea;
            }
        }

        [XmlIgnore]
        public int TotalHeartGems {
            get {
                int total = 0;
                foreach (AreaStats area in AreasIncludingCeleste) {
                    patch_SaveData.EnsureAreaStatsModes(area);
                    if (area?.Modes == null)
                        continue;
                    total += area.Modes.Count(mode => mode?.HeartGem ?? false);
                }
                return total;
            }
        }

        [XmlIgnore]
        public int MaxHeartGems {
            get {
                if (Name == "Celeste")
                    return 24;

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties mode in areaData.Mode) {
                        if (mode?.MapData?.Area == null || mode.MapData.Area.Mode > AreaMode.CSide)
                            continue;
                        count += mode.MapData.DetectedHeartGem ? 1 : 0;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int MaxHeartGemsExcludingCSides {
            get {
                if (Name == "Celeste")
                    return 16;

                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    for (int j = 0; j < 2 && j < areaData.Mode.Length; j++) {
                        if (areaData.Mode[j]?.MapData?.DetectedHeartGem == true)
                            count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int MaxCompletions {
            get {
                if (Name == "Celeste")
                    return 9;

                int count = 0;
                if (AreaData.Areas == null)
                    return count;

                foreach (AreaData area in AreaData.Areas) {
                    if (area != null
                        && string.Equals(patch_SaveData.GetAreaLevelSet(area), Name, StringComparison.Ordinal)
                        && !area.Interlude) {
                        count++;
                    }
                }

                return count;
            }
        }

        [XmlIgnore]
        public int TotalCassettes {
            get {
                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData != null && !areaData.Interlude && i < AreasIncludingCeleste.Count && AreasIncludingCeleste[i]?.Cassette == true) {
                        count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int TotalCompletions {
            get {
                int offset = AreaOffset;
                int count = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = offset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData != null && !areaData.Interlude && i < AreasIncludingCeleste.Count && patch_SaveData.IsModeCompleted(AreasIncludingCeleste[i], 0)) {
                        count++;
                    }
                }
                return count;
            }
        }

        [XmlIgnore]
        public int CompletionPercent {
            get {
                // TODO: Get max counts on the fly.
                float value = 0f;
                value += (MaxHeartGems == 0 ? 1 : (float) TotalHeartGems / MaxHeartGems) * 24f;
                value += (MaxStrawberries == 0 ? 1 : (float) TotalStrawberries / MaxStrawberries) * 55f;
                value += (MaxCassettes == 0 ? 1 : (float) TotalCassettes / MaxCassettes) * 7f;
                value += (MaxCompletions == 0 ? 1 : (float) TotalCompletions / MaxCompletions) * 14f;

                return (int) value;
            }
        }

        public int MaxAreaMode {
            get {
                if (Name == "Celeste") {
                    return (int) AreaMode.CSide;
                }
                int areaOffset = AreaOffset;
                int maxAreaMode = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    int areaIndex = areaOffset + i;
                    AreaData areaData = areaIndex >= 0 && areaIndex < AreaData.Areas.Count ? AreaData.Areas[areaIndex] : null;
                    if (areaData?.Mode == null)
                        continue;

                    foreach (ModeProperties modeProperties in areaData.Mode) {
                        if (modeProperties?.MapData?.Area != null && (int) modeProperties.MapData.Area.Mode > maxAreaMode) {
                            maxAreaMode = (int) modeProperties.MapData.Area.Mode;
                        }
                    }
                }
                return maxAreaMode;
            }
        }

    }
    public static class SaveDataExt {

        /// <summary>
        /// Get the statistics for all level sets.
        /// </summary>
        [Obsolete("Use SaveData.LevelSets instead.")]
        public static List<LevelSetStats> GetLevelSets(this SaveData self)
            => ((patch_SaveData) self).LevelSets;
        /// <summary>
        /// Set the statistics for all level sets.
        /// </summary>
        [Obsolete("Use SaveData.LevelSets instead.")]
        public static SaveData SetLevelSets(this SaveData self, List<LevelSetStats> value) {
            ((patch_SaveData) self).LevelSets = value;
            return self;
        }

        /// <summary>
        /// Get the last played level set.
        /// </summary>
        [Obsolete("Use SaveData.LevelSet instead.")]
        public static string GetLevelSet(this SaveData self)
            => ((patch_SaveData) self).LevelSet;

        /// <summary>
        /// Get the statistics for the last played level set.
        /// </summary>
        [Obsolete("Use SaveData.LevelSetStats instead.")]
        public static LevelSetStats GetLevelSetStats(this SaveData self)
            => ((patch_SaveData) self).LevelSetStats;

        /// <inheritdoc cref="patch_SaveData.GetLevelSetStatsFor(string)"/>
        [Obsolete("Use SaveData.GetLevelSetStatsFor instead.")]
        public static LevelSetStats GetLevelSetStatsFor(this SaveData self, string name)
            => ((patch_SaveData) self).GetLevelSetStatsFor(name);

    }
}
