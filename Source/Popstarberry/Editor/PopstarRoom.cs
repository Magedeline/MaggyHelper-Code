using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace MaggyHelper.Popstarberry.Editor;

using Element = BinaryPacker.Element;

/// <summary>
/// Represents a room in the Popstarberry editor.
/// Based on Snowberry's Room class.
/// </summary>
public class PopstarRoom
{
    #region Properties
    
    public string Name { get; set; }
    public PopstarMap Map { get; }
    public Rectangle Bounds { get; set; }
    
    // Tile data
    public VirtualMap<char> FGTiles { get; private set; }
    public VirtualMap<char> BGTiles { get; private set; }
    public VirtualMap<char> ObjTiles { get; private set; }
    
    // Entities
    public List<Entities.PopstarEntity> Entities { get; } = new();
    public List<Entities.PopstarEntity> Triggers { get; } = new();
    public List<Entities.PopstarDecal> FGDecals { get; } = new();
    public List<Entities.PopstarDecal> BGDecals { get; } = new();
    
    public IEnumerable<Entities.PopstarEntity> AllEntities
    {
        get
        {
            foreach (var e in Entities) yield return e;
            foreach (var t in Triggers) yield return t;
        }
    }
    
    // Room properties
    public int ColorIndex { get; set; } = 0;
    public bool Dark { get; set; } = false;
    public bool Space { get; set; } = false;
    public bool Underwater { get; set; } = false;
    public string Music { get; set; } = "";
    public string MusicProgress { get; set; } = "";
    public string Ambience { get; set; } = "";
    public float WindPattern { get; set; } = 0f;
    
    // Rendering
    public Rectangle ScissorRect { get; private set; }
    
    #endregion

    #region Constructors
    
    public PopstarRoom(string name, PopstarMap map)
    {
        Name = name;
        Map = map;
        Bounds = new Rectangle(0, 0, 40, 23);
        
        // Initialize tile maps with default values
        int w = Bounds.Width;
        int h = Bounds.Height;
        
        FGTiles = new VirtualMap<char>(w, h, '0');
        BGTiles = new VirtualMap<char>(w, h, '0');
        ObjTiles = new VirtualMap<char>(w, h, '0');
    }
    
    public PopstarRoom(LevelData data, PopstarMap map) : this(data.Name, map)
    {
        Bounds = new Rectangle(data.Bounds.X / 8, data.Bounds.Y / 8, data.Bounds.Width / 8, data.Bounds.Height / 8);
        
        // Load room properties
        // Color is not directly on LevelData, use a default
        ColorIndex = 0;
        Dark = data.Dark;
        Space = data.Space;
        Underwater = data.Underwater;
        Music = data.Music ?? "";
        MusicProgress = data.MusicProgress.ToString();
        Ambience = data.Ambience ?? "";
        WindPattern = (float)data.WindPattern;
        
        // Load tiles
        LoadTiles(data);
        
        // Load entities
        LoadEntities(data);
        
        // Load decals
        LoadDecals(data);
    }
    
    #endregion

    #region Loading
    
    private void LoadTiles(LevelData data)
    {
        int w = Bounds.Width;
        int h = Bounds.Height;
        
        FGTiles = new VirtualMap<char>(w, h, '0');
        BGTiles = new VirtualMap<char>(w, h, '0');
        ObjTiles = new VirtualMap<char>(w, h, '-');
        
        // Parse FG tiles
        if (!string.IsNullOrEmpty(data.Solids))
        {
            ParseTileString(data.Solids, FGTiles);
        }
        
        // Parse BG tiles
        if (!string.IsNullOrEmpty(data.Bg))
        {
            ParseTileString(data.Bg, BGTiles);
        }
        
        // Parse object tiles
        if (!string.IsNullOrEmpty(data.ObjTiles))
        {
            ParseTileString(data.ObjTiles, ObjTiles);
        }
    }
    
    private void ParseTileString(string tileString, VirtualMap<char> map)
    {
        string[] rows = tileString.Split('\n');
        for (int y = 0; y < rows.Length && y < map.Rows; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length && x < map.Columns; x++)
            {
                map[x, y] = row[x];
            }
        }
    }
    
    private void LoadEntities(LevelData data)
    {
        foreach (var entityData in data.Entities)
        {
            var entity = Popstarberry.Entities.PluginRegistry.CreateEntity(entityData.Name, this, entityData);
            if (entity != null)
            {
                Entities.Add(entity);
            }
        }
        
        foreach (var triggerData in data.Triggers)
        {
            var trigger = Popstarberry.Entities.PluginRegistry.CreateEntity(triggerData.Name, this, triggerData);
            if (trigger != null)
            {
                Triggers.Add(trigger);
            }
        }
    }
    
    private void LoadDecals(LevelData data)
    {
        foreach (var decalData in data.FgDecals)
        {
            FGDecals.Add(new Entities.PopstarDecal(decalData, this, true));
        }
        
        foreach (var decalData in data.BgDecals)
        {
            BGDecals.Add(new Entities.PopstarDecal(decalData, this, false));
        }
    }
    
    #endregion

    #region Entity Management
    
    public void AddEntity(Entities.PopstarEntity entity)
    {
        entity.Room = this;
        Entities.Add(entity);
    }
    
    public void RemoveEntity(Entities.PopstarEntity entity)
    {
        Entities.Remove(entity);
        Triggers.Remove(entity);
        entity.Room = null;
    }
    
    #endregion

    #region Rendering
    
    public void CalculateScissorRect(PopstarEditor.BufferCamera camera)
    {
        Vector2 topLeft = camera.WorldToScreen(new Vector2(Bounds.X * 8, Bounds.Y * 8));
        Vector2 bottomRight = camera.WorldToScreen(new Vector2((Bounds.X + Bounds.Width) * 8, (Bounds.Y + Bounds.Height) * 8));
        
        ScissorRect = new Rectangle(
            (int)topLeft.X,
            (int)topLeft.Y,
            (int)(bottomRight.X - topLeft.X),
            (int)(bottomRight.Y - topLeft.Y)
        );
    }
    
    public void Render(Rectangle viewRect)
    {
        Vector2 offset = new Vector2(Bounds.X * 8, Bounds.Y * 8);
        
        // Get room color from index (use a default color palette)
        Color roomColor = GetRoomColor(ColorIndex);
        
        // Draw room background
        Draw.Rect(offset.X, offset.Y, Bounds.Width * 8, Bounds.Height * 8, roomColor * 0.3f);
        
        // Draw BG decals
        foreach (var decal in BGDecals)
        {
            decal.Render(offset);
        }
        
        // Draw BG tiles
        RenderTiles(BGTiles, offset, false);
        
        // Draw entities
        foreach (var entity in Entities)
        {
            entity.Render();
        }
        
        // Draw FG tiles
        RenderTiles(FGTiles, offset, true);
        
        // Draw FG decals
        foreach (var decal in FGDecals)
        {
            decal.Render(offset);
        }
        
        // Draw triggers (semi-transparent)
        foreach (var trigger in Triggers)
        {
            trigger.Render();
        }
        
        // Draw room outline
        Draw.HollowRect(offset.X, offset.Y, Bounds.Width * 8, Bounds.Height * 8, roomColor);
        
        // Draw room name
        if (PopstarberryModule.Settings.ShowGrid && ActiveFont.Font != null)
        {
            ActiveFont.Draw(Name ?? "", offset + new Vector2(4, 4), Color.White * 0.6f);
        }
    }
    
    private static Color GetRoomColor(int colorIndex)
    {
        // Default room color palette (can be extended)
        return colorIndex switch
        {
            0 => Color.Gray,
            1 => Color.CornflowerBlue,
            2 => Color.LimeGreen,
            3 => Color.Orange,
            4 => Color.Purple,
            5 => Color.Cyan,
            6 => Color.Magenta,
            _ => Color.Gray
        };
    }
    
    public void HQRender()
    {
        // High quality render pass for entities that need it
        foreach (var entity in Entities)
        {
            entity.HQRender();
        }
    }
    
    private void RenderTiles(VirtualMap<char> tiles, Vector2 offset, bool isForeground)
    {
        // Simple tile rendering - actual implementation would use autotiler
        for (int y = 0; y < tiles.Rows; y++)
        {
            for (int x = 0; x < tiles.Columns; x++)
            {
                char tile = tiles[x, y];
                if (tile != '0' && tile != '-')
                {
                    Color color = isForeground ? Color.Gray : Color.DarkGray;
                    Draw.Rect(offset.X + x * 8, offset.Y + y * 8, 8, 8, color);
                }
            }
        }
    }
    
    #endregion

    #region Export
    
    public Element CreateLevelData()
    {
        // Get color for export
        Color roomColor = GetRoomColor(ColorIndex);
        
        Element level = new Element
        {
            Name = "level",
            Attributes = new Dictionary<string, object>
            {
                ["name"] = Name,
                ["x"] = Bounds.X * 8,
                ["y"] = Bounds.Y * 8,
                ["width"] = Bounds.Width * 8,
                ["height"] = Bounds.Height * 8,
                ["c"] = (int)(roomColor.R << 16 | roomColor.G << 8 | roomColor.B),
                ["dark"] = Dark,
                ["space"] = Space,
                ["underwater"] = Underwater,
                ["music"] = Music,
                ["musicProgress"] = MusicProgress,
                ["ambience"] = Ambience,
                ["windPattern"] = (int)WindPattern
            },
            Children = new List<Element>()
        };
        
        // Solids (FG tiles)
        level.Attributes["solids"] = ExportTiles(FGTiles);
        level.Attributes["bg"] = ExportTiles(BGTiles);
        level.Attributes["objTiles"] = ExportTiles(ObjTiles);
        
        // Entities
        Element entities = new Element
        {
            Name = "entities",
            Children = new List<Element>()
        };
        foreach (var entity in Entities)
        {
            entities.Children.Add(entity.Export());
        }
        level.Children.Add(entities);
        
        // Triggers
        Element triggers = new Element
        {
            Name = "triggers",
            Children = new List<Element>()
        };
        foreach (var trigger in Triggers)
        {
            triggers.Children.Add(trigger.Export());
        }
        level.Children.Add(triggers);
        
        // FG Decals
        Element fgDecals = new Element
        {
            Name = "fgdecals",
            Children = new List<Element>()
        };
        foreach (var decal in FGDecals)
        {
            fgDecals.Children.Add(decal.Export());
        }
        level.Children.Add(fgDecals);
        
        // BG Decals
        Element bgDecals = new Element
        {
            Name = "bgdecals",
            Children = new List<Element>()
        };
        foreach (var decal in BGDecals)
        {
            bgDecals.Children.Add(decal.Export());
        }
        level.Children.Add(bgDecals);
        
        return level;
    }
    
    private string ExportTiles(VirtualMap<char> tiles)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < tiles.Rows; y++)
        {
            for (int x = 0; x < tiles.Columns; x++)
            {
                sb.Append(tiles[x, y]);
            }
            if (y < tiles.Rows - 1)
                sb.Append('\n');
        }
        return sb.ToString();
    }
    
    #endregion
}
