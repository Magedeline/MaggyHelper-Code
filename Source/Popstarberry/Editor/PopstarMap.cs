using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace MaggyHelper.Popstarberry.Editor;

using Element = BinaryPacker.Element;

/// <summary>
/// Represents a map being edited in Popstarberry.
/// Based on Snowberry's Map class.
/// </summary>
public class PopstarMap
{
    #region Properties
    
    public string Name { get; set; }
    public AreaKey From { get; set; }
    
    public List<PopstarRoom> Rooms { get; } = new();
    public List<Rectangle> Fillers { get; } = new();
    
    public List<Stylegrounds.PopstarStyleground> FGStylegrounds { get; } = new();
    public List<Stylegrounds.PopstarStyleground> BGStylegrounds { get; } = new();
    
    #endregion

    #region Constructors
    
    public PopstarMap(string name)
    {
        Name = name;
        From = new AreaKey(-1);
        
        // Create default room
        var defaultRoom = new PopstarRoom("main", this)
        {
            Bounds = new Rectangle(0, 0, 40, 23)
        };
        Rooms.Add(defaultRoom);
    }
    
    public PopstarMap(MapData data) : this(data.Filename)
    {
        From = data.Area;
        
        // Clear default room
        Rooms.Clear();
        
        // Load rooms
        foreach (var levelData in data.Levels)
        {
            Rooms.Add(new PopstarRoom(levelData, this));
        }
        
        // Load fillers
        foreach (var filler in data.Filler)
        {
            Fillers.Add(filler);
        }
        
        // Load stylegrounds
        LoadStylegrounds(data);
        
        PopstarberryModule.Log(LogLevel.Info, 
            $"Loaded map with {Rooms.Count} rooms, {FGStylegrounds.Count} FG and {BGStylegrounds.Count} BG stylegrounds.");
    }
    
    #endregion

    #region Initialization
    
    public void InitializeAllEntities()
    {
        foreach (var room in Rooms)
        {
            foreach (var entity in room.AllEntities)
            {
                entity.InitializeAfter();
            }
        }
    }
    
    private void LoadStylegrounds(MapData data)
    {
        // Load foreground stylegrounds
        if (data.Foreground?.Children != null)
        {
            foreach (var item in data.Foreground.Children)
            {
                LoadStylegroundElement(item, false);
            }
        }
        
        // Load background stylegrounds
        if (data.Background?.Children != null)
        {
            foreach (var item in data.Background.Children)
            {
                LoadStylegroundElement(item, true);
            }
        }
    }
    
    private void LoadStylegroundElement(Element item, bool isBackground, Element applyData = null)
    {
        string name = item.Name?.ToLowerInvariant() ?? "";
        
        if (name.Equals("apply"))
        {
            // Handle apply groups
            if (item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    LoadStylegroundElement(child, isBackground, item);
                }
            }
        }
        else
        {
            // Create styleground
            var styleground = Stylegrounds.StylegroundRegistry.Create(name, this, item, applyData);
            if (styleground != null)
            {
                if (isBackground)
                    BGStylegrounds.Add(styleground);
                else
                    FGStylegrounds.Add(styleground);
            }
        }
    }
    
    #endregion

    #region Room Access
    
    public PopstarRoom GetRoomAt(Point worldPos)
    {
        foreach (var room in Rooms)
        {
            if (room.Bounds.Contains(worldPos))
            {
                return room;
            }
        }
        return null;
    }
    
    public int GetFillerIndexAt(Point worldPos)
    {
        for (int i = 0; i < Fillers.Count; i++)
        {
            if (Fillers[i].Contains(worldPos))
            {
                return i;
            }
        }
        return -1;
    }
    
    #endregion

    #region Rendering
    
    public void Render(PopstarEditor.BufferCamera camera)
    {
        Rectangle viewRect = camera.ViewRect;
        var visibleRooms = new List<PopstarRoom>();
        
        // Find visible rooms
        foreach (var room in Rooms)
        {
            Rectangle rect = new Rectangle(
                room.Bounds.X * 8, 
                room.Bounds.Y * 8, 
                room.Bounds.Width * 8, 
                room.Bounds.Height * 8
            );
            
            if (viewRect.Intersects(rect))
            {
                room.CalculateScissorRect(camera);
                visibleRooms.Add(room);
            }
        }
        
        // Render BG stylegrounds
        foreach (var styleground in BGStylegrounds)
        {
            foreach (var room in visibleRooms)
            {
                if (styleground.IsVisibleIn(room))
                {
                    styleground.Render(room);
                }
            }
        }
        
        // Render rooms
        foreach (var room in visibleRooms)
        {
            RenderRoom(room, camera, viewRect);
        }
        
        // Render FG stylegrounds
        foreach (var styleground in FGStylegrounds)
        {
            foreach (var room in visibleRooms)
            {
                if (styleground.IsVisibleIn(room))
                {
                    styleground.Render(room);
                }
            }
        }
        
        // Render fillers
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, camera.Matrix);
        for (int i = 0; i < Fillers.Count; i++)
        {
            Rectangle filler = Fillers[i];
            Rectangle rect = new Rectangle(filler.X * 8, filler.Y * 8, filler.Width * 8, filler.Height * 8);
            Draw.Rect(rect, Color.White * (PopstarEditor.Instance?.SelectedFillerIndex == i ? 0.14f : 0.1f));
        }
        Draw.SpriteBatch.End();
    }
    
    private void RenderRoom(PopstarRoom room, PopstarEditor.BufferCamera camera, Rectangle viewRect)
    {
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, camera.Matrix);
        room.Render(viewRect);
        Draw.SpriteBatch.End();
    }
    
    public void HQRender(PopstarEditor.BufferCamera camera)
    {
        Rectangle viewRect = camera.ViewRect;
        
        foreach (var room in Rooms)
        {
            Rectangle rect = new Rectangle(
                room.Bounds.X * 8, 
                room.Bounds.Y * 8, 
                room.Bounds.Width * 8, 
                room.Bounds.Height * 8
            );
            
            if (viewRect.Intersects(rect))
            {
                room.HQRender();
            }
        }
    }
    
    #endregion

    #region Map Data Generation
    
    public void GenerateMapData(MapData data)
    {
        // Generate rooms
        foreach (var room in Rooms)
        {
            data.Levels.Add(new LevelData(room.CreateLevelData()));
        }
        
        // Generate fillers
        foreach (var filler in Fillers)
        {
            data.Filler.Add(filler);
        }
        
        // Generate stylegrounds
        data.Foreground = GenerateStylegroundsElement(false);
        data.Background = GenerateStylegroundsElement(true);
        
        // Calculate bounds
        CalculateMapBounds(data);
    }
    
    private void CalculateMapBounds(MapData data)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var level in data.Levels)
        {
            minX = Math.Min(minX, level.Bounds.Left);
            minY = Math.Min(minY, level.Bounds.Top);
            maxX = Math.Max(maxX, level.Bounds.Right);
            maxY = Math.Max(maxY, level.Bounds.Bottom);
        }
        
        foreach (var filler in data.Filler)
        {
            minX = Math.Min(minX, filler.Left);
            minY = Math.Min(minY, filler.Top);
            maxX = Math.Max(maxX, filler.Right);
            maxY = Math.Max(maxY, filler.Bottom);
        }
        
        int padding = 64;
        data.Bounds = new Rectangle(
            minX - padding, 
            minY - padding, 
            maxX - minX + padding * 2, 
            maxY - minY + padding * 2
        );
    }
    
    #endregion

    #region Export
    
    public Element Export()
    {
        Element map = new Element
        {
            Children = new List<Element>()
        };
        
        // Levels
        Element levels = new Element
        {
            Name = "levels",
            Children = new List<Element>()
        };
        foreach (var room in Rooms)
        {
            levels.Children.Add(room.CreateLevelData());
        }
        map.Children.Add(levels);
        
        // Fillers
        Element fillers = new Element
        {
            Name = "Filler",
            Children = new List<Element>()
        };
        foreach (var filler in Fillers)
        {
            Element fill = new Element
            {
                Attributes = new Dictionary<string, object>
                {
                    ["x"] = filler.X,
                    ["y"] = filler.Y,
                    ["w"] = filler.Width,
                    ["h"] = filler.Height
                }
            };
            fillers.Children.Add(fill);
        }
        map.Children.Add(fillers);
        
        // Style
        Element style = new Element
        {
            Name = "Style",
            Attributes = new Dictionary<string, object>(),
            Children = new List<Element>
            {
                GenerateStylegroundsElement(false),
                GenerateStylegroundsElement(true)
            }
        };
        map.Children.Add(style);
        
        return map;
    }
    
    private Element GenerateStylegroundsElement(bool isBackground)
    {
        Element styles = new Element
        {
            Name = isBackground ? "Backgrounds" : "Foregrounds",
            Children = new List<Element>()
        };
        
        var stylegrounds = isBackground ? BGStylegrounds : FGStylegrounds;
        
        foreach (var styleground in stylegrounds)
        {
            styles.Children.Add(styleground.Export());
        }
        
        return styles;
    }
    
    #endregion
}
