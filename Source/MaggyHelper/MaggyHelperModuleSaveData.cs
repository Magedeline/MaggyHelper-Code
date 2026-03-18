
namespace MaggyHelper.MaggyHelper;

/// <summary>
/// Save data for MaggyHelper that persists across game sessions.
/// This data is saved to the player's save file.
/// </summary>
public class MaggyHelperModuleSaveData : EverestModuleSaveData
{
    /// <summary>
    /// Version number for save data migrations.
    /// </summary>
    public int SaveDataVersion { get; set; } = 0;

    #region Collectibles
    
    /// <summary>
    /// List of collected custom berry IDs.
    /// </summary>
    public List<string> CollectedBerries { get; set; } = new List<string>();
    
    /// <summary>
    /// List of collected delta berry IDs.
    /// </summary>
    public List<string> CollectedDeltaBerries { get; set; } = new List<string>();
    
    /// <summary>
    /// Combined delta berries identifier (for multi-part collectibles).
    /// </summary>
    public string CombinedDeltaBerries { get; set; } = "";
    
    /// <summary>
    /// List of collected heart gem IDs.
    /// </summary>
    public List<string> CollectedHeartGems { get; set; } = new List<string>();
    
    /// <summary>
    /// List of collected cassette tape IDs.
    /// </summary>
    public List<string> CollectedCassettes { get; set; } = new List<string>();

    /// <summary>
    /// List of collected mini heart gem IDs.
    /// </summary>
    public List<string> CollectedMiniHeartGems { get; set; } = new List<string>();

    /// <summary>
    /// List of collected pink platinum berry IDs.
    /// </summary>
    public List<string> CollectedPinkPlatinumBerries { get; set; } = new List<string>();
    
    /// <summary>
    /// Dictionary of custom collectibles (type -> list of IDs).
    /// </summary>
    public Dictionary<string, List<string>> CustomCollectibles { get; set; } = new Dictionary<string, List<string>>();
    
    #endregion

    #region Progression
    
    /// <summary>
    /// List of completed chapter SIDs.
    /// </summary>
    public List<string> CompletedChapters { get; set; } = new List<string>();
    
    /// <summary>
    /// List of unlocked chapter SIDs.
    /// </summary>
    public List<string> UnlockedChapters { get; set; } = new List<string>();
    
    /// <summary>
    /// List of completed cutscene IDs.
    /// </summary>
    public List<string> CompletedCutscenes { get; set; } = new List<string>();
    
    /// <summary>
    /// Dictionary of chapter completion data (SID -> completion info).
    /// </summary>
    public Dictionary<string, ChapterCompletionData> ChapterData { get; set; } = new Dictionary<string, ChapterCompletionData>();

    /// <summary>
    /// Preferred playable character for each chapter SID.
    /// </summary>
    public Dictionary<string, string> PreferredChapterCharacters { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Saved respawn state per chapter SID.
    /// </summary>
    public Dictionary<string, SavedChapterRespawnState> ChapterRespawnStates { get; set; } = new Dictionary<string, SavedChapterRespawnState>(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Whether the player has seen the mod intro / selection screen.
    /// Once true, the ModSelectionScreen will not appear again.
    /// </summary>
    public bool HasSeenModIntro { get; set; } = false;

    /// <summary>
    /// Whether the player has already seen the Chapter 9 Beyond Summit intro vignette.
    /// </summary>
    public bool HasSeenChapter9IntroVignette { get; set; } = false;
    
    #endregion

    #region Kirby Progress
    
    /// <summary>
    /// List of unlocked Kirby powers.
    /// </summary>
    public List<string> UnlockedKirbyPowers { get; set; } = new List<string>();
    
    /// <summary>
    /// Total enemies defeated across all sessions.
    /// </summary>
    public int TotalEnemiesDefeated { get; set; } = 0;
    
    /// <summary>
    /// Total bosses defeated.
    /// </summary>
    public int TotalBossesDefeated { get; set; } = 0;
    
    /// <summary>
    /// Dictionary of defeated boss names and their defeat count.
    /// </summary>
    public Dictionary<string, int> DefeatedBosses { get; set; } = new Dictionary<string, int>();
    
    /// <summary>
    /// Whether Knight mode has been unlocked.
    /// </summary>
    public bool KnightModeUnlocked { get; set; } = false;

    /// <summary>
    /// Whether the Void Moon has been unlocked on the overworld mountain.
    /// Set to true when Chapter 19 is completed and Chapter 20 (The End) is reached.
    /// </summary>
    public bool VoidMoonUnlocked { get; set; } = false;
    
    #endregion

    #region Achievements
    
    /// <summary>
    /// List of unlocked achievement IDs.
    /// </summary>
    public List<string> UnlockedAchievements { get; set; } = new List<string>();
    
    /// <summary>
    /// Dictionary of achievement progress (ID -> progress value).
    /// </summary>
    public Dictionary<string, int> AchievementProgress { get; set; } = new Dictionary<string, int>();
    
    #endregion

    #region Statistics
    
    /// <summary>
    /// Total play time in seconds.
    /// </summary>
    public long TotalPlayTimeSeconds { get; set; } = 0;
    
    /// <summary>
    /// Total death count.
    /// </summary>
    public int TotalDeaths { get; set; } = 0;
    
    /// <summary>
    /// Total dash count.
    /// </summary>
    public long TotalDashes { get; set; } = 0;
    
    /// <summary>
    /// Total jump count.
    /// </summary>
    public long TotalJumps { get; set; } = 0;
    
    /// <summary>
    /// Total wall jumps.
    /// </summary>
    public long TotalWallJumps { get; set; } = 0;
    
    #endregion

    #region Settings Persistence
    
    /// <summary>
    /// Last selected character skin.
    /// </summary>
    public string LastSelectedSkin { get; set; } = "default";
    
    /// <summary>
    /// Custom persistent flags.
    /// </summary>
    public Dictionary<string, bool> PersistentFlags { get; set; } = new Dictionary<string, bool>();
    
    /// <summary>
    /// Custom persistent values.
    /// </summary>
    public Dictionary<string, string> PersistentValues { get; set; } = new Dictionary<string, string>();
    
    #endregion

    #region Unlock Data
    
    /// <summary>
    /// List of unlocked remix extra IDs (for Cartridge collectibles).
    /// </summary>
    public List<string> UnlockedRemixExtraIDs { get; set; } = new List<string>();
    
    /// <summary>
    /// List of unlocked B-Side IDs (for Cassette collectibles).
    /// </summary>
    public List<string> UnlockedBSideIDs { get; set; } = new List<string>();
    
    /// <summary>
    /// List of unlocked C-Side IDs (for Tape collectibles).
    /// </summary>
    public List<string> UnlockedCSideIDs { get; set; } = new List<string>();
    
    /// <summary>
    /// Whether Chapter 19 has been completed.
    /// </summary>
    public bool Chapter19Complete { get; set; } = false;

    /// <summary>
    /// Whether all A/B/C side hearts required for D-side progression have been collected.
    /// </summary>
    public bool AllABCSideHeartsCollected { get; set; } = false;

    /// <summary>
    /// Whether D-side heart gem progression is globally unlocked.
    /// </summary>
    public bool DSideHeartGemUnlocked { get; set; } = false;

    /// <summary>
    /// Whether the pink platinum berry unlock requirement has been met.
    /// </summary>
    public bool PinkPlatinumBerryUnlocked { get; set; } = false;

    /// <summary>
    /// Whether boss rush content is unlocked.
    /// </summary>
    public bool BossRushUnlocked { get; set; } = false;

    /// <summary>
    /// Whether final DLC content is unlocked.
    /// </summary>
    public bool FinalDlcContentUnlocked { get; set; } = false;

    /// <summary>
    /// Whether the account has reached 100% completion.
    /// </summary>
    public bool OneHundredPercentComplete { get; set; } = false;

    /// <summary>
    /// Whether the ultra completion postcard is available.
    /// </summary>
    public bool UltraPostcardUnlocked { get; set; } = false;

    /// <summary>
    /// Whether the ultra completion postcard has already been shown.
    /// </summary>
    public bool HasSeenUltraCompletionPostcard { get; set; } = false;

    /// <summary>
    /// Total tracked strawberries across vanilla and custom Maggy progression.
    /// </summary>
    public int TotalTrackedStrawberries { get; set; } = 0;

    /// <summary>
    /// Total tracked heart gems across vanilla, mini hearts, and extended sides.
    /// </summary>
    public int TotalTrackedHeartGems { get; set; } = 0;

    /// <summary>
    /// Total tracked cassette/tape collectibles.
    /// </summary>
    public int TotalTrackedCassettes { get; set; } = 0;

    /// <summary>
    /// Total tracked mini heart gems.
    /// </summary>
    public int TotalTrackedMiniHeartGems { get; set; } = 0;

    /// <summary>
    /// Total tracked D-side heart gems.
    /// </summary>
    public int TotalTrackedDSideHeartGems { get; set; } = 0;

    /// <summary>
    /// Total tracked collectibles used by Maggy progression.
    /// </summary>
    public int TotalTrackedCollectibles { get; set; } = 0;
    
    /// <summary>
    /// Whether Chapter 19 has been unlocked.
    /// </summary>
    public bool UnlockedChapter19 { get; set; } = false;

    /// <summary>
    /// Pending unlock for Chapter 16 (processed on next game launch).
    /// </summary>
    public bool PendingUnlockChapter16OnRestart { get; set; } = false;

    /// <summary>
    /// Pending unlock for Chapter 19 (processed on next game launch).
    /// </summary>
    public bool PendingUnlockChapter19OnRestart { get; set; } = false;

    /// <summary>
    /// Pending unlock for Chapter 20 (processed on next game launch).
    /// </summary>
    public bool PendingUnlockChapter20OnRestart { get; set; } = false;
    
    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Collect a heart gem and track it.
    /// </summary>
    public void CollectHeartGem(string heartId)
    {
        if (!CollectedHeartGems.Contains(heartId))
        {
            CollectedHeartGems.Add(heartId);
        }
    }
    
    /// <summary>
    /// Check if a heart gem has been collected.
    /// </summary>
    public bool HasCollectedHeartGem(string heartId)
    {
        return CollectedHeartGems.Contains(heartId);
    }

    /// <summary>
    /// Mark a cassette as collected.
    /// </summary>
    public void CollectCassette(string cassetteId)
    {
        if (!string.IsNullOrWhiteSpace(cassetteId) && !CollectedCassettes.Contains(cassetteId))
        {
            CollectedCassettes.Add(cassetteId);
        }
    }

    /// <summary>
    /// Mark a mini heart gem as collected.
    /// </summary>
    public void CollectMiniHeartGem(string miniHeartId)
    {
        if (!string.IsNullOrWhiteSpace(miniHeartId) && !CollectedMiniHeartGems.Contains(miniHeartId))
        {
            CollectedMiniHeartGems.Add(miniHeartId);
        }
    }

    /// <summary>
    /// Mark a pink platinum berry as collected.
    /// </summary>
    public void CollectPinkPlatinumBerry(string berryId)
    {
        if (!string.IsNullOrWhiteSpace(berryId) && !CollectedPinkPlatinumBerries.Contains(berryId))
        {
            CollectedPinkPlatinumBerries.Add(berryId);
        }
    }
    
    /// <summary>
    /// Check if an achievement/flag has been earned (alias for IsAchievementUnlocked).
    /// </summary>
    public bool HasAchievement(string achievementId)
    {
        return UnlockedAchievements.Contains(achievementId);
    }
    
    /// <summary>
    /// Check if a berry has been collected.
    /// </summary>
    public bool HasCollectedBerry(string berryId)
    {
        return CollectedBerries.Contains(berryId);
    }
    
    /// <summary>
    /// Mark a berry as collected.
    /// </summary>
    public void CollectBerry(string berryId)
    {
        if (!CollectedBerries.Contains(berryId))
        {
            CollectedBerries.Add(berryId);
        }
    }
    
    /// <summary>
    /// Check if a chapter has been completed.
    /// </summary>
    public bool HasCompletedChapter(string sid)
    {
        return CompletedChapters.Contains(sid);
    }
    
    /// <summary>
    /// Mark a chapter as completed.
    /// </summary>
    public void CompleteChapter(string sid)
    {
        if (!CompletedChapters.Contains(sid))
        {
            CompletedChapters.Add(sid);
        }
    }
    
    /// <summary>
    /// Check if a chapter is unlocked.
    /// </summary>
    public bool IsChapterUnlocked(string sid)
    {
        return UnlockedChapters.Contains(sid);
    }
    
    /// <summary>
    /// Unlock a chapter.
    /// </summary>
    public void UnlockChapter(string sid)
    {
        if (!UnlockedChapters.Contains(sid))
        {
            UnlockedChapters.Add(sid);
        }
    }

    /// <summary>
    /// Store the preferred playable character for a chapter.
    /// </summary>
    public void SetPreferredCharacter(string sid, string characterId)
    {
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(characterId))
            return;

        PreferredChapterCharacters[sid] = characterId;
    }

    /// <summary>
    /// Try to get the preferred playable character for a chapter.
    /// </summary>
    public bool TryGetPreferredCharacter(string sid, out string characterId)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            characterId = string.Empty;
            return false;
        }

        return PreferredChapterCharacters.TryGetValue(sid, out characterId) && !string.IsNullOrWhiteSpace(characterId);
    }

    /// <summary>
    /// Store the current chapter respawn state.
    /// </summary>
    public void SaveChapterRespawn(string sid, SavedChapterRespawnState state)
    {
        if (string.IsNullOrWhiteSpace(sid) || state == null)
            return;

        ChapterRespawnStates[sid] = state;
    }

    /// <summary>
    /// Try to get a saved respawn state for a chapter.
    /// </summary>
    public bool TryGetChapterRespawn(string sid, out SavedChapterRespawnState state)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            state = null;
            return false;
        }

        return ChapterRespawnStates.TryGetValue(sid, out state) && state != null;
    }
    
    /// <summary>
    /// Check if a Kirby power is unlocked.
    /// </summary>
    public bool IsKirbyPowerUnlocked(string powerName)
    {
        return UnlockedKirbyPowers.Contains(powerName);
    }
    
    /// <summary>
    /// Unlock a Kirby power.
    /// </summary>
    public void UnlockKirbyPower(string powerName)
    {
        if (!UnlockedKirbyPowers.Contains(powerName))
        {
            UnlockedKirbyPowers.Add(powerName);
        }
    }
    
    /// <summary>
    /// Record a boss defeat.
    /// </summary>
    public void RecordBossDefeat(string bossName)
    {
        TotalBossesDefeated++;
        
        if (DefeatedBosses.ContainsKey(bossName))
        {
            DefeatedBosses[bossName]++;
        }
        else
        {
            DefeatedBosses[bossName] = 1;
        }
    }
    
    /// <summary>
    /// Check if an achievement is unlocked.
    /// </summary>
    public bool IsAchievementUnlocked(string achievementId)
    {
        return UnlockedAchievements.Contains(achievementId);
    }
    
    /// <summary>
    /// Unlock an achievement.
    /// </summary>
    public void UnlockAchievement(string achievementId)
    {
        if (!UnlockedAchievements.Contains(achievementId))
        {
            UnlockedAchievements.Add(achievementId);
        }
    }
    
    /// <summary>
    /// Get achievement progress.
    /// </summary>
    public int GetAchievementProgress(string achievementId)
    {
        return AchievementProgress.TryGetValue(achievementId, out int progress) ? progress : 0;
    }
    
    /// <summary>
    /// Set achievement progress.
    /// </summary>
    public void SetAchievementProgress(string achievementId, int progress)
    {
        AchievementProgress[achievementId] = progress;
    }
    
    /// <summary>
    /// Get a persistent flag.
    /// </summary>
    public bool GetPersistentFlag(string flagName)
    {
        return PersistentFlags.TryGetValue(flagName, out bool value) && value;
    }
    
    /// <summary>
    /// Set a persistent flag.
    /// </summary>
    public void SetPersistentFlag(string flagName, bool value)
    {
        PersistentFlags[flagName] = value;
    }
    
    /// <summary>
    /// Get a persistent value.
    /// </summary>
    public string GetPersistentValue(string key)
    {
        return PersistentValues.TryGetValue(key, out string value) ? value : "";
    }
    
    /// <summary>
    /// Set a persistent value.
    /// </summary>
    public void SetPersistentValue(string key, string value)
    {
        PersistentValues[key] = value;
    }
    
    /// <summary>
    /// Add a custom collectible.
    /// </summary>
    public void AddCustomCollectible(string type, string id)
    {
        if (!CustomCollectibles.ContainsKey(type))
        {
            CustomCollectibles[type] = new List<string>();
        }
        
        if (!CustomCollectibles[type].Contains(id))
        {
            CustomCollectibles[type].Add(id);
        }
    }
    
    /// <summary>
    /// Check if a custom collectible has been collected.
    /// </summary>
    public bool HasCustomCollectible(string type, string id)
    {
        return CustomCollectibles.ContainsKey(type) && CustomCollectibles[type].Contains(id);
    }
    
    #endregion

    #region New Game Reset

    /// <summary>
    /// Resets all mod progression for a genuine new-game start while keeping
    /// account-level flags (e.g. <see cref="HasSeenModIntro"/> is reset so the
    /// vessel-creation intro plays, then set to <c>true</c> once it completes).
    /// Called by <see cref="MaggyHelperModule.ResetModSaveData"/> from
    /// <c>MaggyHelperHooks.LevelEnterGo</c> before the intro vignette opens.
    /// </summary>
    public void ResetForNewGame()
    {
        Logger.Log(LogLevel.Info, "MaggyHelper", "Resetting mod save data for new game start");

        // Collectibles
        CollectedBerries       = new List<string>();
        CollectedDeltaBerries  = new List<string>();
        CombinedDeltaBerries   = "";
        CollectedHeartGems     = new List<string>();
        CollectedCassettes     = new List<string>();
        CollectedMiniHeartGems = new List<string>();
        CollectedPinkPlatinumBerries = new List<string>();
        CustomCollectibles     = new Dictionary<string, List<string>>();

        // Progression
        CompletedChapters  = new List<string>();
        UnlockedChapters   = new List<string>();
        CompletedCutscenes = new List<string>();
        ChapterData        = new Dictionary<string, ChapterCompletionData>();
        PreferredChapterCharacters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ChapterRespawnStates = new Dictionary<string, SavedChapterRespawnState>(StringComparer.OrdinalIgnoreCase);

        // Kirby / combat stats
        UnlockedKirbyPowers  = new List<string>();
        TotalEnemiesDefeated = 0;

        // HasSeenModIntro is reset to false so the vessel-creation vignette
        // fires. VesselCreationVignette sets it back to true on completion.
        HasSeenModIntro = false;

        // Chapter intros should replay on a true new game.
        HasSeenChapter9IntroVignette = false;

        AllABCSideHeartsCollected = false;
        DSideHeartGemUnlocked = false;
        PinkPlatinumBerryUnlocked = false;
        BossRushUnlocked = false;
        FinalDlcContentUnlocked = false;
        OneHundredPercentComplete = false;
        UltraPostcardUnlocked = false;
        HasSeenUltraCompletionPostcard = false;
        TotalTrackedStrawberries = 0;
        TotalTrackedHeartGems = 0;
        TotalTrackedCassettes = 0;
        TotalTrackedMiniHeartGems = 0;
        TotalTrackedDSideHeartGems = 0;
        TotalTrackedCollectibles = 0;

        // Bump schema version so migration won't re-run on this file.
        SaveDataVersion = 1;

        Logger.Log(LogLevel.Info, "MaggyHelper", "Save data reset for new game complete");
    }

    #endregion
}

/// <summary>
/// Data structure for chapter completion information.
/// </summary>
public class ChapterCompletionData
{
    /// <summary>
    /// Best time in ticks.
    /// </summary>
    public long BestTime { get; set; } = 0;
    
    /// <summary>
    /// Death count for best run.
    /// </summary>
    public int BestDeaths { get; set; } = 0;
    
    /// <summary>
    /// Total completions.
    /// </summary>
    public int CompletionCount { get; set; } = 0;
    
    /// <summary>
    /// Whether completed in full clear mode.
    /// </summary>
    public bool FullClear { get; set; } = false;
    
    /// <summary>
    /// Whether completed with golden strawberry.
    /// </summary>
    public bool GoldenComplete { get; set; } = false;
    
    /// <summary>
    /// Number of berries collected.
    /// </summary>
    public int BerriesCollected { get; set; } = 0;
    
    /// <summary>
    /// Total berries in chapter.
    /// </summary>
    public int TotalBerries { get; set; } = 0;
}

