using System;
using System.Collections.Generic;
using MaggyHelper.Popstarberry.Editor;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace MaggyHelper.Popstarberry.Tools;

/// <summary>
/// Registry for editor tools.
/// </summary>
public static class ToolRegistry
{
    private static List<Tool> tools = new();
    
    public static void LoadPlugins(List<Type> pluginTypes) { }
    
    public static void RegisterTool(Tool tool) => tools.Add(tool);
    
    public static Tool GetTool(int index) => index >= 0 && index < tools.Count ? tools[index] : null;
    
    public static IEnumerable<Tool> GetAllTools() => tools;
    
    public static int ToolCount => tools.Count;
}

/// <summary>
/// Base class for editor tools.
/// </summary>
public abstract class Tool : PopstarModule
{
    protected PopstarEditor EditorRef => PopstarEditor.Instance;
    protected Vector2 WorldMouse => PopstarEditor.Mouse.World;
    protected Vector2 ScreenMouse => PopstarEditor.Mouse.Screen;
    
    public virtual void Selected() { }
    public virtual void Unselected() { }
    public virtual void Update() { }
    public virtual void Render() { }
    public virtual UI.UIElement CreatePanel() => null;
}

/// <summary>
/// Selection tool for selecting and moving entities.
/// </summary>
public class SelectionTool : Tool
{
    public override string Name => "Selection";
    
    private Vector2 dragStart;
    private bool isDragging;
    private bool isMoving;
    private Vector2 moveOffset;
    
    public override void Load() => ToolRegistry.RegisterTool(this);
    public override void Unload() { }
    
    public override void Update()
    {
        if (EditorRef == null) return;
        
        Vector2 mouseWorld = WorldMouse;
        
        if (MInput.Mouse.PressedLeftButton)
        {
            bool clickedOnSelection = false;
            foreach (var sel in EditorRef.SelectedEntities)
            {
                if (sel.Bounds.Contains(new Point((int)mouseWorld.X, (int)mouseWorld.Y)))
                {
                    clickedOnSelection = true;
                    isMoving = true;
                    moveOffset = mouseWorld;
                    break;
                }
            }
            
            if (!clickedOnSelection)
            {
                EditorRef.SelectedEntities.Clear();
                dragStart = mouseWorld;
                isDragging = true;
            }
        }
        
        if (isDragging)
        {
            Vector2 min = new Vector2(Math.Min(dragStart.X, mouseWorld.X), Math.Min(dragStart.Y, mouseWorld.Y));
            Vector2 max = new Vector2(Math.Max(dragStart.X, mouseWorld.X), Math.Max(dragStart.Y, mouseWorld.Y));
            
            EditorRef.Selection = new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));
        }
        
        if (isMoving)
        {
            Vector2 delta = mouseWorld - moveOffset;
            foreach (var sel in EditorRef.SelectedEntities)
                sel.Entity.Position += delta;
            moveOffset = mouseWorld;
        }
        
        if (MInput.Mouse.ReleasedLeftButton)
        {
            if (isDragging && EditorRef.Selection.HasValue)
            {
                SelectEntitiesInRect(EditorRef.Selection.Value);
                EditorRef.Selection = null;
            }
            isDragging = false;
            isMoving = false;
        }
    }
    
    private void SelectEntitiesInRect(Rectangle rect)
    {
        if (EditorRef?.SelectedRoom == null) return;
        
        foreach (var entity in EditorRef.SelectedRoom.AllEntities)
        {
            var bounds = entity.GetSelectionRects();
            foreach (var bound in bounds)
            {
                if (rect.Intersects(bound))
                {
                    EditorRef.SelectedEntities.Add(new EntitySelection(entity, bound));
                    break;
                }
            }
        }
    }
    
    public override void Render() { }
    
    public override UI.UIElement CreatePanel()
    {
        var panel = new UI.UIElement { Width = 200, Height = 300 };
        panel.Add(new UI.UILabel("Selection Tool") { Position = new Vector2(10, 10) });
        panel.Add(new UI.UILabel(() => $"Selected: {EditorRef?.SelectedEntities?.Count ?? 0}") { Position = new Vector2(10, 30) });
        return panel;
    }
}

/// <summary>
/// Placement tool for adding new entities.
/// </summary>
public class PlacementTool : Tool
{
    public override string Name => "Placement";
    
    private Entities.PlacementInfo currentPlacement;
    
    public override void Load() => ToolRegistry.RegisterTool(this);
    public override void Unload() { }
    
    public override void Update()
    {
        if (currentPlacement == null || EditorRef?.SelectedRoom == null) return;
        
        Vector2 mouseWorld = WorldMouse;
        
        if (MInput.Mouse.PressedLeftButton)
        {
            var entityData = new EntityData
            {
                Name = currentPlacement.EntityName,
                ID = (int)(DateTime.Now.Ticks % int.MaxValue),
                Position = mouseWorld - new Vector2(EditorRef.SelectedRoom.Bounds.X * 8, EditorRef.SelectedRoom.Bounds.Y * 8),
                Width = 16,
                Height = 16
            };
            
            var entity = Entities.PluginRegistry.CreateEntity(currentPlacement.EntityName, EditorRef.SelectedRoom, entityData);
            if (entity != null)
                EditorRef.SelectedRoom.AddEntity(entity);
        }
    }
    
    public override void Render()
    {
        if (currentPlacement == null) return;
        Vector2 mouseWorld = WorldMouse;
        Draw.HollowRect(mouseWorld.X - 8, mouseWorld.Y - 8, 16, 16, Color.LimeGreen * 0.7f);
    }
    
    public override UI.UIElement CreatePanel()
    {
        var panel = new UI.UIElement { Width = 200, Height = 400 };
        panel.Add(new UI.UILabel("Placement Tool") { Position = new Vector2(10, 10) });
        
        int y = 40;
        foreach (var placement in Entities.PluginRegistry.GetPlacements())
        {
            var p = placement;
            panel.Add(new UI.UIButton(placement.DisplayName, 4, 2)
            {
                Position = new Vector2(10, y),
                OnPress = () => currentPlacement = p
            });
            y += 20;
        }
        
        return panel;
    }
}

/// <summary>
/// Tile brush tool for painting tiles.
/// </summary>
public class TileBrushTool : Tool
{
    public override string Name => "Tiles";
    
    private char currentTile = '1';
    private bool isForeground = true;
    private int brushSize = 1;
    
    public override void Load() => ToolRegistry.RegisterTool(this);
    public override void Unload() { }
    
    public override void Update()
    {
        if (EditorRef?.SelectedRoom == null) return;
        
        Vector2 mouseWorld = WorldMouse;
        
        if (MInput.Mouse.CheckLeftButton)
        {
            Vector2 localPos = mouseWorld - new Vector2(EditorRef.SelectedRoom.Bounds.X * 8, EditorRef.SelectedRoom.Bounds.Y * 8);
            int tileX = (int)(localPos.X / 8);
            int tileY = (int)(localPos.Y / 8);
            
            var tiles = isForeground ? EditorRef.SelectedRoom.FGTiles : EditorRef.SelectedRoom.BGTiles;
            
            for (int dx = -brushSize + 1; dx < brushSize; dx++)
            {
                for (int dy = -brushSize + 1; dy < brushSize; dy++)
                {
                    int x = tileX + dx;
                    int y = tileY + dy;
                    if (x >= 0 && x < tiles.Columns && y >= 0 && y < tiles.Rows)
                        tiles[x, y] = currentTile;
                }
            }
        }
        
        if (MInput.Mouse.CheckRightButton)
        {
            Vector2 localPos = mouseWorld - new Vector2(EditorRef.SelectedRoom.Bounds.X * 8, EditorRef.SelectedRoom.Bounds.Y * 8);
            int tileX = (int)(localPos.X / 8);
            int tileY = (int)(localPos.Y / 8);
            
            var tiles = isForeground ? EditorRef.SelectedRoom.FGTiles : EditorRef.SelectedRoom.BGTiles;
            
            for (int dx = -brushSize + 1; dx < brushSize; dx++)
            {
                for (int dy = -brushSize + 1; dy < brushSize; dy++)
                {
                    int x = tileX + dx;
                    int y = tileY + dy;
                    if (x >= 0 && x < tiles.Columns && y >= 0 && y < tiles.Rows)
                        tiles[x, y] = '0';
                }
            }
        }
    }
    
    public override void Render()
    {
        Vector2 mouseWorld = WorldMouse;
        float size = brushSize * 8;
        Draw.HollowRect(
            (float)Math.Floor(mouseWorld.X / 8) * 8 - (brushSize - 1) * 4,
            (float)Math.Floor(mouseWorld.Y / 8) * 8 - (brushSize - 1) * 4,
            size, size,
            isForeground ? Color.Cyan : Color.Magenta);
    }
    
    public override UI.UIElement CreatePanel()
    {
        var panel = new UI.UIElement { Width = 200, Height = 400 };
        panel.Add(new UI.UILabel("Tile Brush") { Position = new Vector2(10, 10) });
        
        panel.Add(new UI.UIButton("FG", 4, 2) { Position = new Vector2(10, 40), OnPress = () => isForeground = true });
        panel.Add(new UI.UIButton("BG", 4, 2) { Position = new Vector2(60, 40), OnPress = () => isForeground = false });
        
        return panel;
    }
}

/// <summary>
/// Stylegrounds tool for managing stylegrounds.
/// </summary>
public class StylegroundsTool : Tool
{
    public override string Name => "Stylegrounds";
    
    private bool isBackground = false;
    
    public override void Load() => ToolRegistry.RegisterTool(this);
    public override void Unload() { }
    
    public override UI.UIElement CreatePanel()
    {
        var panel = new UI.UIElement { Width = 250, Height = 400 };
        panel.Add(new UI.UILabel("Stylegrounds") { Position = new Vector2(10, 10) });
        
        panel.Add(new UI.UIButton("Foreground", 6, 4) { Position = new Vector2(10, 30), OnPress = () => isBackground = false });
        panel.Add(new UI.UIButton("Background", 6, 4) { Position = new Vector2(100, 30), OnPress = () => isBackground = true });
        
        panel.Add(new UI.UILabel(() =>
        {
            var map = EditorRef?.Map;
            if (map == null) return "No map loaded";
            var list = isBackground ? map.BGStylegrounds : map.FGStylegrounds;
            return $"{list.Count} stylegrounds";
        }) { Position = new Vector2(10, 70) });
        
        return panel;
    }
}
