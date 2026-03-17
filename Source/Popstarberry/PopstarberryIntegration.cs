using System;
using System.Collections.Generic;
using System.Reflection;
using MaggyHelper.Popstarberry.Editor;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Celeste.Editor;
using MapEditor = Celeste.Editor.MapEditor;
using LevelTemplate = Celeste.Editor.LevelTemplate;

namespace MaggyHelper.Popstarberry;

/// <summary>
/// Integration layer between the existing EnhancedMapEditor and Popstarberry.
/// Provides a bridge to use Popstarberry features from the vanilla map editor.
/// </summary>
public static class PopstarberryIntegration
{
    #region State
    
    private static bool _initialized;
    private static bool _showPopstarberryOverlay = false;
    private static bool _usePopstarberryUI = false;
    
    // Cached references
    private static Camera _vanillaCamera;
    private static MapEditor _currentEditor;
    
    #endregion

    #region Initialization
    
    /// <summary>
    /// Initialize the integration between EnhancedMapEditor and Popstarberry.
    /// Call this after both systems are loaded.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        // Hook into the vanilla map editor
        On.Celeste.Editor.MapEditor.ctor += OnMapEditorCtor;
        On.Celeste.Editor.MapEditor.Update += OnMapEditorUpdate;
        On.Celeste.Editor.MapEditor.Render += OnMapEditorRender;
        
        _initialized = true;
        PopstarberryModule.Log(LogLevel.Info, "PopstarberryIntegration initialized.");
    }
    
    /// <summary>
    /// Cleanup the integration.
    /// </summary>
    public static void Cleanup()
    {
        if (!_initialized) return;
        
        On.Celeste.Editor.MapEditor.ctor -= OnMapEditorCtor;
        On.Celeste.Editor.MapEditor.Update -= OnMapEditorUpdate;
        On.Celeste.Editor.MapEditor.Render -= OnMapEditorRender;
        
        _initialized = false;
        _currentEditor = null;
        _vanillaCamera = null;
        
        PopstarberryModule.Log(LogLevel.Info, "PopstarberryIntegration cleaned up.");
    }
    
    /// <summary>
    /// Unload alias for Cleanup for consistency with module patterns.
    /// </summary>
    public static void Unload() => Cleanup();
    
    #endregion

    #region Hooks
    
    private static void OnMapEditorCtor(On.Celeste.Editor.MapEditor.orig_ctor orig, MapEditor self, AreaKey area, bool reloadMapData)
    {
        orig(self, area, reloadMapData);
        
        _currentEditor = self;
        _vanillaCamera = GetVanillaCamera();
        
        // Sync with Popstarberry settings
        SyncSettings();
    }
    
    private static void OnMapEditorUpdate(On.Celeste.Editor.MapEditor.orig_Update orig, MapEditor self)
    {
        // Check for Popstarberry toggle (F10)
        if (MInput.Keyboard.Pressed(Keys.F10))
        {
            _showPopstarberryOverlay = !_showPopstarberryOverlay;
            ShowNotification($"Popstarberry Overlay: {(_showPopstarberryOverlay ? "ON" : "OFF")}");
        }
        
        // F11: Toggle Popstarberry UI mode
        if (MInput.Keyboard.Pressed(Keys.F11))
        {
            _usePopstarberryUI = !_usePopstarberryUI;
            ShowNotification($"Popstarberry UI: {(_usePopstarberryUI ? "ON" : "OFF")}");
        }
        
        // F12: Open full Popstarberry editor
        if (MInput.Keyboard.Pressed(Keys.F12))
        {
            var mapData = GetMapData(self);
            if (mapData != null)
            {
                Editor.PopstarEditor.Open(mapData);
                return; // Don't run vanilla update
            }
        }
        
        // Update ImGui system if overlay is active
        if (_showPopstarberryOverlay || _usePopstarberryUI)
        {
            ImGui.PopstarImGui.BeginFrame();
        }
        
        orig(self);
        
        // Process Popstarberry-specific input
        if (_showPopstarberryOverlay)
        {
            ProcessPopstarberryInput(self);
        }
        
        if (_showPopstarberryOverlay || _usePopstarberryUI)
        {
            ImGui.PopstarImGui.EndFrame();
        }
    }
    
    private static void OnMapEditorRender(On.Celeste.Editor.MapEditor.orig_Render orig, MapEditor self)
    {
        orig(self);
        
        if (_showPopstarberryOverlay)
        {
            RenderPopstarberryOverlay(self);
        }
        
        if (_usePopstarberryUI)
        {
            RenderPopstarberryUI(self);
        }
    }
    
    #endregion

    #region Popstarberry Integration
    
    private static void ProcessPopstarberryInput(MapEditor editor)
    {
        // Additional input processing when Popstarberry overlay is active
        
        // Number keys 1-9: Quick tool selection hints
        for (int i = 1; i <= 9; i++)
        {
            if (MInput.Keyboard.Pressed((Keys)((int)Keys.D0 + i)))
            {
                ShowNotification($"Tool {i} - Press F12 to open full editor");
            }
        }
    }
    
    private static void RenderPopstarberryOverlay(MapEditor editor)
    {
        var camera = GetVanillaCamera();
        if (camera == null) return;
        
        // Begin spritebatch for overlay rendering
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
            // Render grid with Popstarberry styling
            if (PopstarberryModule.Settings?.ShowGrid == true)
            {
                RenderStyledGrid(editor, camera);
            }
            
            // Render entity highlights with Popstarberry colors
            RenderEntityHighlights(editor, camera);
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
        
        // Render HUD elements
        RenderOverlayHUD(editor);
    }
    
    private static void RenderStyledGrid(MapEditor editor, Camera camera)
    {
        int gridSize = PopstarberryModule.Settings?.GridSize ?? 8;
        Color gridColor = PopstarberryModule.Settings?.AccentColor ?? new Color(255, 105, 180);
        gridColor *= 0.1f;
        
        // Calculate visible area
        Vector2 topLeft = camera.Position - new Vector2(Engine.ViewWidth, Engine.ViewHeight) / camera.Zoom / 2;
        Vector2 bottomRight = camera.Position + new Vector2(Engine.ViewWidth, Engine.ViewHeight) / camera.Zoom / 2;
        
        int startX = (int)Math.Floor(topLeft.X / gridSize) * gridSize;
        int startY = (int)Math.Floor(topLeft.Y / gridSize) * gridSize;
        int endX = (int)Math.Ceiling(bottomRight.X / gridSize) * gridSize;
        int endY = (int)Math.Ceiling(bottomRight.Y / gridSize) * gridSize;
        
        float lineThickness = 1f / camera.Zoom;
        
        // Draw vertical lines
        for (int x = startX; x <= endX; x += gridSize)
        {
            Color c = (x % (gridSize * 5) == 0) ? gridColor * 2 : gridColor;
            Draw.Line(x, startY, x, endY, c, lineThickness);
        }
        
        // Draw horizontal lines
        for (int y = startY; y <= endY; y += gridSize)
        {
            Color c = (y % (gridSize * 5) == 0) ? gridColor * 2 : gridColor;
            Draw.Line(startX, y, endX, y, c, lineThickness);
        }
    }
    
    private static void RenderEntityHighlights(MapEditor editor, Camera camera)
    {
        var mapData = GetMapData(editor);
        if (mapData == null) return;
        
        Color accentColor = PopstarberryModule.Settings?.AccentColor ?? new Color(255, 105, 180);
        
        // Highlight special entities
        foreach (var level in mapData.Levels)
        {
            foreach (var entity in level.Entities)
            {
                string name = entity.Name?.ToLower() ?? "";
                
                // Highlight collectibles
                if (name.Contains("strawberry") || name.Contains("heart") || name.Contains("cassette"))
                {
                    Vector2 pos = new Vector2(entity.Position.X + level.Bounds.X, entity.Position.Y + level.Bounds.Y) / 8f;
                    Draw.Circle(pos, 2f / camera.Zoom, accentColor * 0.7f, 8);
                }
                
                // Highlight spawn points
                if (name.Contains("player"))
                {
                    Vector2 pos = new Vector2(entity.Position.X + level.Bounds.X, entity.Position.Y + level.Bounds.Y) / 8f;
                    Draw.HollowRect(pos.X - 1, pos.Y - 2, 2, 4, Color.Lime);
                }
            }
        }
    }
    
    private static void RenderOverlayHUD(MapEditor editor)
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
            Color accentColor = PopstarberryModule.Settings?.AccentColor ?? new Color(255, 105, 180);
            
            // Draw Popstarberry branding
            string title = "★ Popstarberry";
            Vector2 titleSize = Draw.DefaultFont.MeasureString(title);
            
            float x = Engine.ViewWidth - titleSize.X - 20;
            float y = 20;
            
            Draw.Rect(x - 5, y - 5, titleSize.X + 10, titleSize.Y + 10, Color.Black * 0.8f);
            Draw.Rect(x - 5, y - 5, 3, titleSize.Y + 10, accentColor);
            Draw.SpriteBatch.DrawString(Draw.DefaultFont, title, new Vector2(x, y), accentColor);
            
            // Help text
            string helpText = "F10: Toggle Overlay | F11: UI Mode | F12: Full Editor";
            Vector2 helpSize = Draw.DefaultFont.MeasureString(helpText);
            
            Draw.Rect(x - 5, y + titleSize.Y + 15, helpSize.X + 10, helpSize.Y + 10, Color.Black * 0.7f);
            Draw.SpriteBatch.DrawString(Draw.DefaultFont, helpText, new Vector2(x, y + titleSize.Y + 20), Color.Gray);
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
    }
    
    private static void RenderPopstarberryUI(MapEditor editor)
    {
        // Render ImGui-style UI overlay
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
            bool showToolWindow = true;
            
            // Tool window using ImGui
            if (ImGui.PopstarImGui.BeginWindow("Popstarberry Tools", ref showToolWindow, 20, 100, 250, 400))
            {
                ImGui.PopstarImGui.Text("Quick Tools");
                ImGui.PopstarImGui.Separator();
                
                if (ImGui.PopstarImGui.Button("Open Full Editor (F12)"))
                {
                    var mapData = GetMapData(editor);
                    if (mapData != null)
                    {
                        Editor.PopstarEditor.Open(mapData);
                    }
                }
                
                ImGui.PopstarImGui.Spacing();
                
                if (ImGui.PopstarImGui.CollapsingHeader("View Options", true))
                {
                    bool showGrid = PopstarberryModule.Settings?.ShowGrid ?? true;
                    if (ImGui.PopstarImGui.Checkbox("Show Grid", ref showGrid))
                    {
                        if (PopstarberryModule.Settings != null)
                            PopstarberryModule.Settings.ShowGrid = showGrid;
                    }
                    
                    bool showEntities = PopstarberryModule.Settings?.ShowEntityOverlay ?? true;
                    if (ImGui.PopstarImGui.Checkbox("Entity Overlay", ref showEntities))
                    {
                        if (PopstarberryModule.Settings != null)
                            PopstarberryModule.Settings.ShowEntityOverlay = showEntities;
                    }
                    
                    ImGui.PopstarImGui.EndCollapsingHeader();
                }
                
                if (ImGui.PopstarImGui.CollapsingHeader("Map Info"))
                {
                    var mapData = GetMapData(editor);
                    if (mapData != null)
                    {
                        ImGui.PopstarImGui.Text($"Name: {mapData.Filename}");
                        ImGui.PopstarImGui.Text($"Rooms: {mapData.Levels.Count}");
                        ImGui.PopstarImGui.Text($"Area: {mapData.Area.GetSID()}");
                    }
                    ImGui.PopstarImGui.EndCollapsingHeader();
                }
                
                ImGui.PopstarImGui.EndWindow();
            }
        }
        finally
        {
            Draw.SpriteBatch.End();
        }
    }
    
    #endregion

    #region Utilities
    
    private static void SyncSettings()
    {
        // Sync EnhancedMapEditor settings with Popstarberry
        // This allows both systems to share configuration
    }
    
    private static Camera GetVanillaCamera()
    {
        var cameraField = typeof(MapEditor).GetField("Camera", BindingFlags.NonPublic | BindingFlags.Static);
        return cameraField?.GetValue(null) as Camera;
    }
    
    private static MapData GetMapData(MapEditor editor)
    {
        var mapDataField = typeof(MapEditor).GetField("mapData", BindingFlags.NonPublic | BindingFlags.Instance);
        return mapDataField?.GetValue(editor) as MapData;
    }
    
    private static void ShowNotification(string message)
    {
        // Use EnhancedMapEditor's notification system if available
        // or fall back to logging
        PopstarberryModule.Log(LogLevel.Info, message);
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// Check if Popstarberry overlay is currently active.
    /// </summary>
    public static bool IsOverlayActive => _showPopstarberryOverlay;
    
    /// <summary>
    /// Check if Popstarberry UI mode is active.
    /// </summary>
    public static bool IsUIActive => _usePopstarberryUI;
    
    /// <summary>
    /// Get the current vanilla map editor instance.
    /// </summary>
    public static MapEditor CurrentEditor => _currentEditor;
    
    /// <summary>
    /// Open Popstarberry editor with the current map.
    /// </summary>
    public static void OpenPopstarberryEditor()
    {
        if (_currentEditor != null)
        {
            var mapData = GetMapData(_currentEditor);
            if (mapData != null)
            {
                Editor.PopstarEditor.Open(mapData);
            }
        }
    }
    
    #endregion
}
