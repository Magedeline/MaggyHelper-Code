using System.Runtime.CompilerServices;
using MaggyHelper.Popstarberry.Editor;
using Microsoft.Xna.Framework.Input;
using Celeste.Editor;
using MapEditor = Celeste.Editor.MapEditor;
using LevelTemplate = Celeste.Editor.LevelTemplate;

namespace MaggyHelper;

/// <summary>
/// Enhanced Map Editor features for MaggyHelper mod.
/// Provides additional visualization, navigation, and debugging tools.
/// </summary>
public static class EnhancedMapEditor
{
    #region Configuration
    
    private const string MOD_SID_PREFIX = "DesoloZantas/";
    
    // Additional entity types to track and visualize
    private static readonly string[] TrackedEntityTypes = new[]
    {
        "key", "lockBlock", "heartGem", "cassette", "summitGem",
        "badelineBoost", "dreamBlock", "moveBlock", "swapBlock",
        "spinner", "refill", "spring", "booster", "zipMover"
    };
    
    // Custom entity visualization colors
    private static readonly Dictionary<string, Color> EntityColors = new()
    {
        { "key", Color.Gold },
        { "lockBlock", Color.DarkGoldenrod },
        { "heartGem", Color.DeepPink },
        { "cassette", Color.Purple },
        { "summitGem", Color.Cyan },
        { "badelineBoost", Color.MediumPurple },
        { "dreamBlock", Color.DarkBlue },
        { "moveBlock", Color.Orange },
        { "swapBlock", Color.YellowGreen },
        { "spinner", Color.White },
        { "refill", Color.LimeGreen },
        { "spring", Color.Yellow },
        { "booster", Color.Red },
        { "zipMover", Color.Gray }
    };
    
    #endregion

    #region State
    
    private static bool _hooksLoaded = false;
    private static bool _showEntityOverlay = false;
    private static bool _showRoomConnections = false;
    private static bool _showCheckpointPaths = false;
    private static int _selectedEntityFilter = -1; // -1 = all, 0+ = specific type index
    
    // Cached entity positions for rendering
    private static Dictionary<string, List<EntityRenderInfo>> _cachedEntities = new();
    private static string _cachedMapSID = null;
    
    // Camera smoothing
    private static Vector2 _targetCameraPosition;
    private static bool _smoothCameraEnabled = true;
    private static float _cameraLerpSpeed = 8f;
    
    // Quick teleport history
    private static List<QuickTeleportEntry> _teleportHistory = new();
    private static int _maxHistoryEntries = 10;
    
    // Cached reflection fields — resolved once to avoid per-frame GetField calls
    private static readonly FieldInfo CameraField = typeof(MapEditor).GetField("Camera", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo LevelsField = typeof(MapEditor).GetField("levels", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MapDataField = typeof(MapEditor).GetField("mapData", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MousePositionField = typeof(MapEditor).GetField("mousePosition", BindingFlags.NonPublic | BindingFlags.Instance);
    
    // Cached help text — rebuilt only when toggle state changes
    private static string _cachedHelpText;
    private static bool _helpTextDirty = true;
    
    #endregion

    #region Data Structures
    
    private struct EntityRenderInfo
    {
        public Vector2 Position;
        public string EntityType;
        public string RoomName;
        public int Width;
        public int Height;
        public Dictionary<string, object> CustomData;
    }
    
    private struct QuickTeleportEntry
    {
        public string RoomName;
        public Vector2 Position;
        public DateTime Timestamp;
    }
    
    #endregion

    #region Initialization
    
    /// <summary>
    /// Register all MapEditor hooks. Call this from MaggyHelperModule.Load()
    /// </summary>
    public static void Load()
    {
        if (_hooksLoaded) return;
        
        On.Celeste.Editor.MapEditor.ctor += MapEditor_ctor;
        On.Celeste.Editor.MapEditor.Update += MapEditor_Update;
        On.Celeste.Editor.MapEditor.Render += MapEditor_Render;
        
        _hooksLoaded = true;
        Logger.Log(LogLevel.Info, "MaggyHelper", "Enhanced MapEditor hooks loaded.");
    }
    
    /// <summary>
    /// Unregister all MapEditor hooks. Call this from MaggyHelperModule.Unload()
    /// </summary>
    public static void Unload()
    {
        if (!_hooksLoaded) return;
        
        On.Celeste.Editor.MapEditor.ctor -= MapEditor_ctor;
        On.Celeste.Editor.MapEditor.Update -= MapEditor_Update;
        On.Celeste.Editor.MapEditor.Render -= MapEditor_Render;
        
        _hooksLoaded = false;
        _cachedEntities.Clear();
        _teleportHistory.Clear();
        
        Logger.Log(LogLevel.Info, "MaggyHelper", "Enhanced MapEditor hooks unloaded.");
    }
    
    #endregion

    #region Hooks
    
    private static void MapEditor_ctor(On.Celeste.Editor.MapEditor.orig_ctor orig, MapEditor self, AreaKey area, bool reloadMapData)
    {
        orig(self, area, reloadMapData);
        
        // Cache entities for the new map
        CacheMapEntities(area);
        
        // Reset camera smoothing target
        _targetCameraPosition = GetCameraPosition(self);
    }
    
    private static void MapEditor_Update(On.Celeste.Editor.MapEditor.orig_Update orig, MapEditor self)
    {
        // Process enhanced input before original update
        ProcessEnhancedInput(self);
        
        orig(self);
        
        // Apply smooth camera movement if enabled
        if (_smoothCameraEnabled)
        {
            ApplySmoothCamera(self);
        }
    }
    
    private static void MapEditor_Render(On.Celeste.Editor.MapEditor.orig_Render orig, MapEditor self)
    {
        orig(self);
        
        // Render additional overlays
        RenderEnhancedOverlays(self);
        RenderEnhancedHUD(self);
    }
    
    #endregion

    #region Input Processing
    
    private static void ProcessEnhancedInput(MapEditor editor)
    {
        // F3: Toggle entity overlay
        if (MInput.Keyboard.Pressed(Keys.F3))
        {
            _showEntityOverlay = !_showEntityOverlay;
            _helpTextDirty = true;
            ShowNotification($"Entity Overlay: {(_showEntityOverlay ? "ON" : "OFF")}");
        }
        
        // F4: Toggle room connections visualization
        if (MInput.Keyboard.Pressed(Keys.F4))
        {
            _showRoomConnections = !_showRoomConnections;
            _helpTextDirty = true;
            ShowNotification($"Room Connections: {(_showRoomConnections ? "ON" : "OFF")}");
        }
        
        // F6: Toggle checkpoint path visualization
        if (MInput.Keyboard.Pressed(Keys.F6))
        {
            _showCheckpointPaths = !_showCheckpointPaths;
            _helpTextDirty = true;
            ShowNotification($"Checkpoint Paths: {(_showCheckpointPaths ? "ON" : "OFF")}");
        }
        
        // F7: Toggle smooth camera
        if (MInput.Keyboard.Pressed(Keys.F7))
        {
            _smoothCameraEnabled = !_smoothCameraEnabled;
            _helpTextDirty = true;
            ShowNotification($"Smooth Camera: {(_smoothCameraEnabled ? "ON" : "OFF")}");
        }
        
        // Tab: Cycle entity filter when overlay is active
        if (_showEntityOverlay && MInput.Keyboard.Pressed(Keys.Tab))
        {
            _selectedEntityFilter++;
            if (_selectedEntityFilter >= TrackedEntityTypes.Length)
                _selectedEntityFilter = -1;
            
            string filterName = _selectedEntityFilter < 0 ? "All" : TrackedEntityTypes[_selectedEntityFilter];
            ShowNotification($"Entity Filter: {filterName}");
        }
        
        // Ctrl+G: Go to room by name (would need text input - simplified version)
        if (MInput.Keyboard.Check(Keys.LeftControl) && MInput.Keyboard.Pressed(Keys.G))
        {
            // Jump to first checkpoint room as a simple implementation
            JumpToNextCheckpoint(editor);
        }
        
        // Ctrl+B: Add bookmark at current mouse position
        if (MInput.Keyboard.Check(Keys.LeftControl) && MInput.Keyboard.Pressed(Keys.B))
        {
            AddTeleportBookmark(editor);
        }
        
        // Ctrl+Shift+B: Go to last bookmark
        if (MInput.Keyboard.Check(Keys.LeftControl) && MInput.Keyboard.Check(Keys.LeftShift) && MInput.Keyboard.Pressed(Keys.B))
        {
            GoToLastBookmark(editor);
        }
        
        // Home: Reset camera to origin
        if (MInput.Keyboard.Pressed(Keys.Home))
        {
            SetCameraPosition(editor, Vector2.Zero);
            ShowNotification("Camera reset to origin");
        }
        
        // Page Up/Down: Zoom presets
        if (MInput.Keyboard.Pressed(Keys.PageUp))
        {
            SetCameraZoom(editor, Math.Min(GetCameraZoom(editor) * 2f, 24f));
        }
        if (MInput.Keyboard.Pressed(Keys.PageDown))
        {
            SetCameraZoom(editor, Math.Max(GetCameraZoom(editor) / 2f, 0.5f));
        }
    }
    
    #endregion

    #region Rendering
    
    private static void RenderEnhancedOverlays(MapEditor editor)
    {
        var camera = GetCamera(editor);
        if (camera == null) return;
        
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            camera.Matrix * Engine.ScreenMatrix
        );
        
        try
        {
            if (_showEntityOverlay)
            {
                RenderEntityOverlay(editor, camera);
            }
            
            if (_showRoomConnections)
            {
                RenderRoomConnections(editor, camera);
            }
            
            if (_showCheckpointPaths)
            {
                RenderCheckpointPaths(editor, camera);
            }
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
    }
    
    private static void RenderEntityOverlay(MapEditor editor, Camera camera)
    {
        foreach (var kvp in _cachedEntities)
        {
            string entityType = kvp.Key;
            
            // Apply filter if active
            if (_selectedEntityFilter >= 0 && TrackedEntityTypes[_selectedEntityFilter] != entityType)
                continue;
            
            Color color = EntityColors.TryGetValue(entityType, out var c) ? c : Color.White;
            
            foreach (var entity in kvp.Value)
            {
                Vector2 pos = entity.Position / 8f;
                
                // Draw entity marker
                if (entity.Width > 0 && entity.Height > 0)
                {
                    Draw.HollowRect(pos.X, pos.Y, entity.Width / 8f, entity.Height / 8f, color);
                }
                else
                {
                    Draw.HollowRect(pos.X - 1f, pos.Y - 1f, 3f, 3f, color);
                }
                
                // Draw center dot
                Draw.Rect(pos.X, pos.Y, 1f, 1f, color);
            }
        }
    }
    
    private static void RenderRoomConnections(MapEditor editor, Camera camera)
    {
        var levels = GetLevels(editor);
        if (levels == null) return;
        
        // Draw connections between adjacent rooms
        for (int i = 0; i < levels.Count; i++)
        {
            var level1 = levels[i];
            var rect1 = new Rectangle(level1.X, level1.Y, level1.Width, level1.Height);
            
            for (int j = i + 1; j < levels.Count; j++)
            {
                var level2 = levels[j];
                var rect2 = new Rectangle(level2.X, level2.Y, level2.Width, level2.Height);
                
                // Check for adjacency (within 1 tile)
                if (AreRoomsAdjacent(rect1, rect2))
                {
                    Vector2 center1 = new Vector2(level1.X + level1.Width / 2f, level1.Y + level1.Height / 2f);
                    Vector2 center2 = new Vector2(level2.X + level2.Width / 2f, level2.Y + level2.Height / 2f);
                    
                    Draw.Line(center1, center2, Color.Cyan * 0.5f, 1f / camera.Zoom);
                }
            }
        }
    }
    
    private static void RenderCheckpointPaths(MapEditor editor, Camera camera)
    {
        var levels = GetLevels(editor);
        if (levels == null) return;
        
        // Find all rooms with checkpoints and draw them prominently
        List<LevelTemplate> checkpointRooms = new();
        
        foreach (var level in levels)
        {
            if (level.Checkpoints != null && level.Checkpoints.Count > 0)
            {
                checkpointRooms.Add(level);
                
                // Draw checkpoint indicators
                foreach (var cp in level.Checkpoints)
                {
                    Vector2 pos = new Vector2(level.X, level.Y) + cp;
                    Draw.Rect(pos.X - 2f, pos.Y - 2f, 5f, 5f, Color.Lime);
                    Draw.HollowRect(pos.X - 3f, pos.Y - 3f, 7f, 7f, Color.DarkGreen);
                }
            }
        }
        
        // Draw lines connecting checkpoint rooms in order
        for (int i = 0; i < checkpointRooms.Count - 1; i++)
        {
            var room1 = checkpointRooms[i];
            var room2 = checkpointRooms[i + 1];
            
            Vector2 center1 = new Vector2(room1.X + room1.Width / 2f, room1.Y + room1.Height / 2f);
            Vector2 center2 = new Vector2(room2.X + room2.Width / 2f, room2.Y + room2.Height / 2f);
            
            Draw.Line(center1, center2, Color.Lime * 0.7f, 2f / camera.Zoom);
        }
    }
    
    private static void RenderEnhancedHUD(MapEditor editor)
    {
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Engine.ScreenMatrix
        );
        
        try
        {
            // Render enhanced manual text in bottom-left
            string enhancedText = BuildEnhancedHelpText();
            Vector2 textSize = Draw.DefaultFont.MeasureString(enhancedText);
            
            float padding = 10f;
            float x = padding;
            float y = Engine.ViewHeight - textSize.Y - padding;
            
            // Background
            Draw.Rect(x - 5f, y - 5f, textSize.X + 10f, textSize.Y + 10f, Color.Black * 0.7f);
            
            // Text
            Draw.SpriteBatch.DrawString(Draw.DefaultFont, enhancedText, new Vector2(x, y), Color.LightGray);
            
            // Show current filter status if entity overlay is active
            if (_showEntityOverlay)
            {
                string filterStatus = _selectedEntityFilter < 0 
                    ? "Showing: All Entities" 
                    : $"Showing: {TrackedEntityTypes[_selectedEntityFilter]}";
                    
                Vector2 filterSize = Draw.DefaultFont.MeasureString(filterStatus);
                Draw.Rect(padding - 5f, 80f, filterSize.X + 10f, filterSize.Y + 10f, Color.Black * 0.7f);
                Draw.SpriteBatch.DrawString(Draw.DefaultFont, filterStatus, new Vector2(padding, 85f), Color.Cyan);
            }
            
            // Render notification if active
            RenderNotification();
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
    }
    
    private static string BuildEnhancedHelpText()
    {
        if (!_helpTextDirty && _cachedHelpText != null)
            return _cachedHelpText;
        
        _helpTextDirty = false;
        
        if (!MaggyHelperModuleSettings.ShowEnhancedMapEditorHelp)
        {
            _cachedHelpText = "F8: Show enhanced controls";
            return _cachedHelpText;
        }
        
        _cachedHelpText = @"=== MaggyHelper Enhanced ===
F3: Entity Overlay [" + (_showEntityOverlay ? "ON" : "OFF") + @"]
F4: Room Connections [" + (_showRoomConnections ? "ON" : "OFF") + @"]
F6: Checkpoint Paths [" + (_showCheckpointPaths ? "ON" : "OFF") + @"]
F7: Smooth Camera [" + (_smoothCameraEnabled ? "ON" : "OFF") + @"]
Tab: Cycle Entity Filter
Ctrl+G: Jump to Checkpoint
Ctrl+B: Add Bookmark
Ctrl+Shift+B: Last Bookmark
Home: Reset Camera
PgUp/PgDn: Zoom Presets
F8: Hide this help";
        return _cachedHelpText;
    }
    
    #endregion

    #region Camera Utilities
    
    private static Camera GetCamera(MapEditor editor)
    {
        // Use cached FieldInfo
        return CameraField?.GetValue(null) as Camera;
    }
    
    private static Vector2 GetCameraPosition(MapEditor editor)
    {
        var camera = GetCamera(editor);
        return camera?.Position ?? Vector2.Zero;
    }
    
    private static void SetCameraPosition(MapEditor editor, Vector2 position)
    {
        var camera = GetCamera(editor);
        if (camera != null)
        {
            if (_smoothCameraEnabled)
            {
                _targetCameraPosition = position;
            }
            else
            {
                camera.Position = position;
            }
        }
    }
    
    private static float GetCameraZoom(MapEditor editor)
    {
        var camera = GetCamera(editor);
        return camera?.Zoom ?? 6f;
    }
    
    private static void SetCameraZoom(MapEditor editor, float zoom)
    {
        var camera = GetCamera(editor);
        if (camera != null)
        {
            camera.Zoom = Math.Clamp(zoom, 0.25f, 24f);
        }
    }
    
    private static void ApplySmoothCamera(MapEditor editor)
    {
        var camera = GetCamera(editor);
        if (camera == null) return;
        
        camera.Position = Vector2.Lerp(camera.Position, _targetCameraPosition, _cameraLerpSpeed * Engine.DeltaTime);
    }
    
    #endregion

    #region Level Utilities
    
    private static List<LevelTemplate> GetLevels(MapEditor editor)
    {
        return LevelsField?.GetValue(editor) as List<LevelTemplate>;
    }
    
    private static MapData GetMapData(MapEditor editor)
    {
        return MapDataField?.GetValue(editor) as MapData;
    }
    
    private static Vector2 GetMousePosition(MapEditor editor)
    {
        return MousePositionField != null ? (Vector2)MousePositionField.GetValue(editor) : Vector2.Zero;
    }
    
    private static bool AreRoomsAdjacent(Rectangle r1, Rectangle r2)
    {
        // Expand r1 by 1 tile and check intersection
        Rectangle expanded = new Rectangle(r1.X - 1, r1.Y - 1, r1.Width + 2, r1.Height + 2);
        return expanded.Intersects(r2);
    }
    
    #endregion

    #region Entity Caching
    
    private static void CacheMapEntities(AreaKey area)
    {
        string sid = area.GetSID();
        if (sid == _cachedMapSID && _cachedEntities.Count > 0)
            return;
        
        _cachedEntities.Clear();
        _cachedMapSID = sid;
        
        try
        {
            var mapData = AreaData.Get(area)?.Mode[(int)area.Mode]?.MapData;
            if (mapData?.Levels == null) return;
            
            foreach (var level in mapData.Levels)
            {
                Vector2 offset = new Vector2(level.Bounds.X, level.Bounds.Y);
                
                foreach (var entity in level.Entities)
                {
                    if (Array.IndexOf(TrackedEntityTypes, entity.Name) >= 0)
                    {
                        if (!_cachedEntities.ContainsKey(entity.Name))
                            _cachedEntities[entity.Name] = new List<EntityRenderInfo>();
                        
                        _cachedEntities[entity.Name].Add(new EntityRenderInfo
                        {
                            Position = offset + entity.Position,
                            EntityType = entity.Name,
                            RoomName = level.Name,
                            Width = entity.Width,
                            Height = entity.Height,
                            CustomData = new Dictionary<string, object>()
                        });
                    }
                }
            }
            
            Logger.Log(LogLevel.Debug, "MaggyHelper", $"Cached {_cachedEntities.Sum(kvp => kvp.Value.Count)} entities for map {sid}");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Error caching map entities: {ex.Message}");
        }
    }
    
    #endregion

    #region Navigation Helpers
    
    private static void JumpToNextCheckpoint(MapEditor editor)
    {
        var levels = GetLevels(editor);
        if (levels == null) return;
        
        var currentPos = GetCameraPosition(editor);
        LevelTemplate closest = null;
        float closestDist = float.MaxValue;
        
        foreach (var level in levels)
        {
            if (level.Checkpoints == null || level.Checkpoints.Count == 0)
                continue;
            
            Vector2 cpPos = new Vector2(level.X, level.Y) + level.Checkpoints[0];
            float dist = Vector2.Distance(currentPos, cpPos);
            
            // Only consider checkpoints ahead of current position (or wrap around)
            if (dist > 10f && dist < closestDist)
            {
                closestDist = dist;
                closest = level;
            }
        }
        
        if (closest != null)
        {
            Vector2 targetPos = new Vector2(closest.X, closest.Y) + closest.Checkpoints[0];
            SetCameraPosition(editor, targetPos);
            ShowNotification($"Jumped to: {closest.Name}");
        }
        else
        {
            ShowNotification("No checkpoints found");
        }
    }
    
    private static void AddTeleportBookmark(MapEditor editor)
    {
        var mousePos = GetMousePosition(editor);
        var levels = GetLevels(editor);
        
        string roomName = "Unknown";
        foreach (var level in levels ?? Enumerable.Empty<LevelTemplate>())
        {
            if (level.Check(mousePos))
            {
                roomName = level.Name;
                break;
            }
        }
        
        _teleportHistory.Add(new QuickTeleportEntry
        {
            RoomName = roomName,
            Position = mousePos,
            Timestamp = DateTime.Now
        });
        
        while (_teleportHistory.Count > _maxHistoryEntries)
            _teleportHistory.RemoveAt(0);
        
        ShowNotification($"Bookmark added: {roomName}");
    }
    
    private static void GoToLastBookmark(MapEditor editor)
    {
        if (_teleportHistory.Count == 0)
        {
            ShowNotification("No bookmarks");
            return;
        }
        
        var bookmark = _teleportHistory[^1];
        SetCameraPosition(editor, bookmark.Position);
        ShowNotification($"Jumped to: {bookmark.RoomName}");
    }
    
    #endregion

    #region Notification System
    
    private static string _currentNotification = "";
    private static float _notificationTimer = 0f;
    private const float NotificationDuration = 2f;
    
    private static void ShowNotification(string message)
    {
        _currentNotification = message;
        _notificationTimer = NotificationDuration;
    }
    
    private static void RenderNotification()
    {
        if (_notificationTimer <= 0f) return;
        
        _notificationTimer -= Engine.DeltaTime;
        float alpha = Math.Min(1f, _notificationTimer);
        
        Vector2 textSize = Draw.DefaultFont.MeasureString(_currentNotification);
        float x = (Engine.ViewWidth - textSize.X) / 2f;
        float y = 100f;
        
        Draw.Rect(x - 10f, y - 5f, textSize.X + 20f, textSize.Y + 10f, Color.Black * (0.8f * alpha));
        Draw.SpriteBatch.DrawString(Draw.DefaultFont, _currentNotification, new Vector2(x, y), Color.White * alpha);
    }
    
    #endregion
}

/// <summary>
/// Extension methods for MapEditor settings
/// </summary>
public partial class MaggyHelperModuleSettings
{
    // Add this to your existing MaggyHelperModuleSettings class
    public static bool ShowEnhancedMapEditorHelp { get; set; } = true;
}