using Celeste.Mod;
using System;
using System.Collections.Generic;
using Monocle;

namespace MaggyHelper
{
    public class MaggyHelperSaveData : EverestModuleSaveData
    {
        // ===== Save Data Version =====
        
        /// <summary>
        /// Version number for save data migration
        /// </summary>
        public int SaveDataVersion { get; set; } = 0;

        // ===== Mod Intro State =====
        
        /// <summary>
        /// Whether the player has already seen the mod selection screen.
        /// Once true, the custom title screen will not appear again.
        /// </summary>
        public bool HasSeenModIntro { get; set; } = false;

        // ===== Progression Tracking =====
        
        /// <summary>
        /// Chapters that have been completed (SIDs)
        /// </summary>
        public List<string> CompletedChapters { get; set; } = new List<string>();

        /// <summary>
        /// Chapters that have been unlocked (SIDs)
        /// </summary>
        public List<string> UnlockedChapters { get; set; } = new List<string>();

        /// <summary>
        /// Heart gems that have been collected (SIDs)
        /// </summary>
        public List<string> CollectedHeartGems { get; set; } = new List<string>();

        /// <summary>
        /// Cassettes that have been collected (SIDs)
        /// </summary>
        public List<string> CollectedCassettes { get; set; } = new List<string>();

        /// <summary>
        /// Per-chapter completion data
        /// </summary>
        public Dictionary<string, ChapterCompletionData> ChapterData { get; set; } = 
            new Dictionary<string, ChapterCompletionData>(StringComparer.OrdinalIgnoreCase);

        // ===== Boss & Enemy Stats =====
        
        /// <summary>
        /// Total bosses defeated across all playthroughs
        /// </summary>
        public int TotalBossesDefeated { get; set; } = 0;

        /// <summary>
        /// Total enemies defeated across all playthroughs
        /// </summary>
        public int TotalEnemiesDefeated { get; set; } = 0;

        /// <summary>
        /// List of boss names that have been defeated
        /// </summary>
        public HashSet<string> DefeatedBosses { get; set; } = new HashSet<string>();

        // ===== Unlockables =====
        
        /// <summary>
        /// Unlocked Kirby color palettes
        /// </summary>
        public HashSet<KirbyColorOption> UnlockedColors { get; set; } = new HashSet<KirbyColorOption>
        {
            KirbyColorOption.Pink // Pink is always unlocked
        };

        /// <summary>
        /// Unlocked copy abilities (some require boss defeats)
        /// </summary>
        public HashSet<CopyAbilityType> UnlockedAbilities { get; set; } = new HashSet<CopyAbilityType>();

        /// <summary>
        /// Whether the player has beaten the game as Kirby
        /// </summary>
        public bool CompletedAsKirby { get; set; } = false;

        /// <summary>
        /// Whether all bosses have been defeated
        /// </summary>
        public bool AllBossesDefeated { get; set; } = false;

        // ===== Statistics =====
        
        /// <summary>
        /// Total time spent floating (in seconds)
        /// </summary>
        public float TotalFloatTime { get; set; } = 0f;

        /// <summary>
        /// Total number of objects inhaled
        /// </summary>
        public int TotalObjectsInhaled { get; set; } = 0;

        /// <summary>
        /// Total ability stars collected
        /// </summary>
        public int TotalAbilityStarsCollected { get; set; } = 0;

        /// <summary>
        /// Number of times copy abilities were used
        /// </summary>
        public Dictionary<CopyAbilityType, int> AbilityUsageCount { get; set; } = new Dictionary<CopyAbilityType, int>();

        // ===== Achievements =====
        
        /// <summary>
        /// Achievement flags for various accomplishments
        /// </summary>
        public HashSet<string> Achievements { get; set; } = new HashSet<string>();

        // ===== Helper Methods =====

        /// <summary>
        /// Unlocks a Kirby color palette
        /// </summary>
        public void UnlockColor(KirbyColorOption color)
        {
            if (UnlockedColors.Add(color))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Unlocked color: {color}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Unlocks a copy ability
        /// </summary>
        public void UnlockAbility(CopyAbilityType ability)
        {
            if (UnlockedAbilities.Add(ability))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Unlocked ability: {ability}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Records a boss defeat
        /// </summary>
        {
            if (DefeatedBosses.Add(bossName))
            {
                TotalBossesDefeated++;
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Boss defeated: {bossName} (Total: {TotalBossesDefeated})");

                // Check if all main bosses are defeated
                if (DefeatedBosses.Count >= 3) // Assuming 3 main bosses
                {
                    AllBossesDefeated = true;
                }

                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if a boss has been defeated
        /// </summary>
        public bool HasDefeatedBoss(string bossName)
        {
            return DefeatedBosses.Contains(bossName);
        }

        /// <summary>
        /// Increments the usage count for a copy ability
        /// </summary>
        public void IncrementAbilityUsage(CopyAbilityType ability)
        {
            if (!AbilityUsageCount.ContainsKey(ability))
            {
                AbilityUsageCount[ability] = 0;
            }
            AbilityUsageCount[ability]++;
            OnSaveDataChanged();
        }

        /// <summary>
        /// Unlocks an achievement
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            if (Achievements.Add(achievementId))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Achievement unlocked: {achievementId}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if an achievement has been unlocked
        /// </summary>
        public bool HasAchievement(string achievementId)
        {
            return Achievements.Contains(achievementId);
        }

        /// <summary>
        /// Marks a chapter as completed
        /// </summary>
        public void CompleteChapter(string sid)
        {
            if (!CompletedChapters.Contains(sid))
            {
                CompletedChapters.Add(sid);
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Chapter completed: {sid}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if a chapter has been completed
        /// </summary>
        public bool HasCompletedChapter(string sid)
        {
            return CompletedChapters.Contains(sid);
        }

        /// <summary>
        /// Unlocks a chapter
        /// </summary>
        public void UnlockChapter(string sid)
        {
            if (!UnlockedChapters.Contains(sid))
            {
                UnlockedChapters.Add(sid);
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Chapter unlocked: {sid}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if a chapter has been unlocked
        /// </summary>
        public bool HasUnlockedChapter(string sid)
        {
            return UnlockedChapters.Contains(sid);
        }

        /// <summary>
        /// Collects a heart gem
        /// </summary>
        public void CollectHeartGem(string heartId)
        {
            if (!CollectedHeartGems.Contains(heartId))
            {
                CollectedHeartGems.Add(heartId);
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Heart gem collected: {heartId}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if a heart gem has been collected
        /// </summary>
        public bool HasCollectedHeartGem(string heartId)
        {
            return CollectedHeartGems.Contains(heartId);
        }

        /// <summary>
        /// Collects a cassette
        /// </summary>
        public void CollectCassette(string cassetteId)
        {
            if (!CollectedCassettes.Contains(cassetteId))
            {
                CollectedCassettes.Add(cassetteId);
                Logger.Log(LogLevel.Info, "MaggyHelper", $"Cassette collected: {cassetteId}");
                OnSaveDataChanged();
            }
        }

        /// <summary>
        /// Checks if a cassette has been collected
        /// </summary>
        public bool HasCollectedCassette(string cassetteId)
        {
            return CollectedCassettes.Contains(cassetteId);
        }

        /// <summary>
        /// Gets or creates chapter completion data for a given SID
        /// </summary>
        public ChapterCompletionData GetOrCreateChapterData(string sid)
        {
            if (!ChapterData.TryGetValue(sid, out var data))
            {
                data = new ChapterCompletionData();
                ChapterData[sid] = data;
            }
            return data;
        }

        /// <summary>
        /// Initializes default values for new save data
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Validate and repair any corrupted data
                ValidateAndRepair();
                
                // Ensure Pink is always unlocked
                if (UnlockedColors == null)
                {
                    UnlockedColors = new HashSet<KirbyColorOption>();
                }
                
                if (!UnlockedColors.Contains(KirbyColorOption.Pink))
                {
                    UnlockedColors.Add(KirbyColorOption.Pink);
                }

                Logger.Log(LogLevel.Info, "MaggyHelper", "SaveData initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error initializing SaveData: {ex.Message}");
                // Try to repair with defaults
                try
                {
                    ResetToDefaults();
                }
                catch (Exception ex2)
                {
                    Logger.Log(LogLevel.Error, "MaggyHelper", $"Failed to reset SaveData to defaults: {ex2.Message}");
                }
            }
        }

        /// <summary>
        /// Validates save data integrity and repairs any corrupted fields
        /// </summary>
        private void ValidateAndRepair()
        {
            bool needsRepair = false;

            // Validate collections are not null
            if (CompletedChapters == null)
            {
                CompletedChapters = new List<string>();
                needsRepair = true;
            }
            
            if (UnlockedChapters == null)
            {
                UnlockedChapters = new List<string>();
                needsRepair = true;
            }
            
            if (CollectedHeartGems == null)
            {
                CollectedHeartGems = new List<string>();
                needsRepair = true;
            }
            
            if (CollectedCassettes == null)
            {
                CollectedCassettes = new List<string>();
                needsRepair = true;
            }
            
            if (ChapterData == null)
            {
                ChapterData = new Dictionary<string, ChapterCompletionData>(StringComparer.OrdinalIgnoreCase);
                needsRepair = true;
            }
            
            if (DefeatedBosses == null)
            {
                DefeatedBosses = new HashSet<string>();
                needsRepair = true;
            }
            
            if (UnlockedColors == null)
            {
                UnlockedColors = new HashSet<KirbyColorOption> { KirbyColorOption.Pink };
                needsRepair = true;
            }
            
            if (UnlockedAbilities == null)
            {
                UnlockedAbilities = new HashSet<CopyAbilityType>();
                needsRepair = true;
            }
            
            if (Achievements == null)
            {
                Achievements = new HashSet<string>();
                needsRepair = true;
            }
            
            if (AbilityUsageCount == null)
            {
                AbilityUsageCount = new Dictionary<CopyAbilityType, int>();
                needsRepair = true;
            }

            // Validate numeric fields are within reasonable ranges
            if (TotalBossesDefeated < 0)
            {
                TotalBossesDefeated = 0;
                needsRepair = true;
            }
            
            if (TotalEnemiesDefeated < 0)
            {
                TotalEnemiesDefeated = 0;
                needsRepair = true;
            }
            
            if (TotalFloatTime < 0)
            {
                TotalFloatTime = 0f;
                needsRepair = true;
            }
            
            if (TotalObjectsInhaled < 0)
            {
                TotalObjectsInhaled = 0;
                needsRepair = true;
            }
            
            if (TotalAbilityStarsCollected < 0)
            {
                TotalAbilityStarsCollected = 0;
                needsRepair = true;
            }

            if (needsRepair)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", "SaveData had corrupted fields - repaired with defaults");
            }
        }

        /// <summary>
        /// Resets all mod progression for a genuine new-game start.
        /// Called automatically before the mod intro vignette so the player
        /// always begins with a clean slate.  Keeps <see cref="HasSeenModIntro"/>
        /// as <c>false</c> so it can be set to <c>true</c> once the vignette
        /// finishes.
        /// </summary>
        public void ResetForNewGame()
        {
            Logger.Log(LogLevel.Info, "MaggyHelper", "Resetting mod save data for new game start");

            // Progression
            CompletedChapters  = new List<string>();
            UnlockedChapters   = new List<string>();
            CollectedHeartGems = new List<string>();
            CollectedCassettes = new List<string>();
            ChapterData        = new Dictionary<string, ChapterCompletionData>(StringComparer.OrdinalIgnoreCase);

            // Combat stats
            TotalBossesDefeated  = 0;
            TotalEnemiesDefeated = 0;
            DefeatedBosses       = new HashSet<string>();
            AllBossesDefeated    = false;

            // Unlockables – reset to first-run state (Pink always available)
            UnlockedColors    = new HashSet<KirbyColorOption> { KirbyColorOption.Pink };
            UnlockedAbilities = new HashSet<CopyAbilityType>();
            CompletedAsKirby  = false;

            // Statistics
            TotalFloatTime             = 0f;
            TotalObjectsInhaled        = 0;
            TotalAbilityStarsCollected = 0;
            AbilityUsageCount          = new Dictionary<CopyAbilityType, int>();

            // Achievements
            Achievements = new HashSet<string>();

            // Keep HasSeenModIntro = false so it gets set to true once the
            // vignette completes – do NOT touch it here.
            HasSeenModIntro = false;

            // Bump the version so migration won't re-run on this file.
            SaveDataVersion = 1;

            // Persist immediately so the reset survives a crash.
            OnSaveDataChanged();

            Logger.Log(LogLevel.Info, "MaggyHelper", "Save data reset for new game complete");
        }

        /// <summary>
        /// Resets save data to safe defaults (nuclear option for corruption)
        /// </summary>
        private void ResetToDefaults()
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", "Resetting SaveData to defaults due to corruption");
            
            SaveDataVersion = 1;
            HasSeenModIntro = false;
            CompletedChapters = new List<string>();
            UnlockedChapters = new List<string>();
            CollectedHeartGems = new List<string>();
            CollectedCassettes = new List<string>();
            ChapterData = new Dictionary<string, ChapterCompletionData>(StringComparer.OrdinalIgnoreCase);
            TotalBossesDefeated = 0;
            TotalEnemiesDefeated = 0;
            DefeatedBosses = new HashSet<string>();
            UnlockedColors = new HashSet<KirbyColorOption> { KirbyColorOption.Pink };
            UnlockedAbilities = new HashSet<CopyAbilityType>();
            CompletedAsKirby = false;
            AllBossesDefeated = false;
            TotalFloatTime = 0f;
            TotalObjectsInhaled = 0;
            TotalAbilityStarsCollected = 0;
            AbilityUsageCount = new Dictionary<CopyAbilityType, int>();
            Achievements = new HashSet<string>();
        }

        /// <summary>
        /// Called when save data changes - triggers auto-save
        /// </summary>
        private void OnSaveDataChanged()
        {
            try
            {
                // Mark save data as dirty for Everest to auto-save
                UserIO.SaveHandler(file: true, settings: false);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Failed to trigger save: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Per-chapter completion tracking data
    /// </summary>
    public class ChapterCompletionData
    {
        public bool Completed { get; set; } = false;
        public bool HeartGemCollected { get; set; } = false;
        public bool CassetteCollected { get; set; } = false;
        public long BestTime { get; set; } = 0;
        public int DeathCount { get; set; } = 0;
    }
}
