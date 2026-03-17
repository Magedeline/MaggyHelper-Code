# SaveData Update Summary

## Overview
Updated the MaggyHelper SaveData system to use proper hooks pattern as described in the "Hooks for Idiots by Idiots" article. The SaveData now includes proper initialization, migration, and auto-saving capabilities.

## Key Changes

### 1. Enhanced SaveData Class ([MaggyHelperSaveData.cs](MaggyHelperSaveData.cs))

#### New Features:
- **SaveDataVersion** property for tracking save data migrations
- **Chapter Progression Tracking**:
  - `CompletedChapters` - List of completed chapter SIDs
  - `UnlockedChapters` - List of unlocked chapter SIDs  
  - `CollectedHeartGems` - List of collected heart gem IDs
  - `CollectedCassettes` - List of collected cassette IDs
  - `ChapterData` - Dictionary for per-chapter completion data

- **Enhanced Helper Methods**:
  - All modification methods now log changes and trigger auto-save
  - Added collection check to prevent duplicate entries
  - New methods for chapter/collectible management

#### New Methods:
```csharp
// Chapter Management
void CompleteChapter(string sid)
bool HasCompletedChapter(string sid)
void UnlockChapter(string sid)
bool HasUnlockedChapter(string sid)

// Collectibles
void CollectHeartGem(string heartId)
bool HasCollectedHeartGem(string heartId)
void CollectCassette(string cassetteId)
bool HasCollectedCassette(string cassetteId)

// Initialization
void Initialize() - Sets up default values
ChapterCompletionData GetOrCreateChapterData(string sid)
```

### 2. Module Hooks ([MaggyHelperModule.cs](MaggyHelperModule.cs))

#### New Hooks Added:
```csharp
// SaveData Initialization Hook
On.Celeste.UserIO.Load += OnSaveDataLoad;

// Main Menu Hook for SaveData verification
Everest.Events.MainMenu.OnCreateButtons += OnMainMenuCreate;
```

#### Hook Behavior:
- **OnSaveDataLoad**: Automatically called when save data is loaded
  - Initializes SaveData defaults
  - Runs migration if needed (version < 1)
  - Logs initialization status

- **OnMainMenuCreate**: Ensures SaveData is properly initialized when main menu is created
  - Verifies Pink Kirby color is unlocked (default)

### 3. SaveData Migration ([MaggySaveDataMigration.cs](Source/MaggyHelper/MaggySaveDataMigration.cs))

#### Migration Features:
- Migrates old SID prefixes (`Maggy/Main/` → new format)
- Updates all chapter-related collections
- Updates ChapterData dictionary keys
- Version tracking to prevent re-running migrations

### 4. ChapterCompletionData Class

New helper class for tracking per-chapter stats:
```csharp
public class ChapterCompletionData
{
    public bool Completed { get; set; }
    public bool HeartGemCollected { get; set; }
    public bool CassetteCollected { get; set; }
    public long BestTime { get; set; }
    public int DeathCount { get; set; }
}
```

## Hooks Pattern Implementation

Following the article's guidelines:

### Load() Method:
```csharp
public override void Load()
{
    // Register hooks with +=
    On.Celeste.UserIO.Load += OnSaveDataLoad;
    Everest.Events.MainMenu.OnCreateButtons += OnMainMenuCreate;
    // ... other hooks
}
```

### Unload() Method:
```csharp
public override void Unload()
{
    // Unregister hooks with -=
    On.Celeste.UserIO.Load -= OnSaveDataLoad;
    Everest.Events.MainMenu.OnCreateButtons -= OnMainMenuCreate;
    // ... other hooks
}
```

### Hook Methods:
All hook methods are:
- **Static** (as recommended)
- **Private** (module-level hooks)
- Include the `orig` method to call the original implementation
- Include proper logging for debugging

## Auto-Save Implementation

The `OnSaveDataChanged()` private method triggers Everest's auto-save:
```csharp
private void OnSaveDataChanged()
{
    UserIO.SaveHandler(file: true, settings: false);
}
```

Called automatically when:
- Colors are unlocked
- Abilities are unlocked
- Bosses are defeated
- Achievements are unlocked
- Chapters are completed/unlocked
- Collectibles are obtained

## Benefits

1. **Automatic Initialization**: SaveData is properly initialized on load
2. **Automatic Migration**: Old save data is migrated on first run
3. **Auto-Saving**: Changes are automatically persisted
4. **Better Logging**: All important operations are logged
5. **Duplicate Prevention**: Helper methods prevent duplicate entries
6. **Extensible**: Easy to add new properties and migration steps

## Testing Recommendations

1. Test with a fresh save file
2. Test with an existing save file (migration)
3. Verify auto-save works when unlocking items
4. Check logs for proper initialization messages
5. Verify Pink Kirby color is always unlocked by default

## Notes

- SaveData follows Everest's standard `EverestModuleSaveData` pattern
- Compatible with Everest's save/load system
- Hooks are properly registered/unregistered to prevent memory leaks
- All warnings during build are pre-existing and unrelated to these changes
