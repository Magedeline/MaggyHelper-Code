using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace MaggyHelper.Popstarberry.Entities;

using Element = BinaryPacker.Element;

/// <summary>
/// Registry for entity plugins in Popstarberry.
/// </summary>
public static class PluginRegistry
{
    private static Dictionary<string, PluginInfo> entityPlugins = new();
    private static Dictionary<string, PluginInfo> triggerPlugins = new();
    private static List<PlacementInfo> placements = new();
    
    public static void LoadPlugins(List<Type> pluginTypes)
    {
        foreach (var type in pluginTypes)
        {
            var attr = type.GetCustomAttribute<PopstarPluginAttribute>();
            if (attr == null) continue;
            
            if (typeof(PopstarEntity).IsAssignableFrom(type))
            {
                var info = new PluginInfo(type, attr);
                entityPlugins[attr.Name] = info;
                
                // Register placements
                RegisterPlacements(type, attr.Name);
            }
        }
        
        PopstarberryModule.Log(LogLevel.Info, $"Loaded {entityPlugins.Count} entity plugins.");
    }
    
    private static void RegisterPlacements(Type type, string entityName)
    {
        // Look for static AddPlacements method
        var method = type.GetMethod("AddPlacements", BindingFlags.Static | BindingFlags.Public);
        method?.Invoke(null, null);
    }
    
    public static void AddPlacement(string displayName, string entityName, Dictionary<string, object> defaultData = null, bool isTrigger = false)
    {
        placements.Add(new PlacementInfo
        {
            DisplayName = displayName,
            EntityName = entityName,
            DefaultData = defaultData ?? new(),
            IsTrigger = isTrigger
        });
    }
    
    public static IEnumerable<PlacementInfo> GetPlacements() => placements;
    
    public static PopstarEntity CreateEntity(string name, Editor.PopstarRoom room, EntityData entityData)
    {
        if (entityPlugins.TryGetValue(name, out var plugin))
        {
            var entity = plugin.Create<PopstarEntity>();
            entity.Name = name;
            entity.Room = room;
            entity.LoadFromEntityData(entityData);
            return entity;
        }
        
        // Create placeholder for unknown entities
        return new UnknownEntity(name, room, entityData);
    }
    
    public static PopstarEntity CreateEntity(string name, Editor.PopstarRoom room, Element element)
    {
        if (entityPlugins.TryGetValue(name, out var plugin))
        {
            var entity = plugin.Create<PopstarEntity>();
            entity.Name = name;
            entity.Room = room;
            entity.LoadFromElement(element);
            return entity;
        }
        
        return new UnknownEntity(name, room, element);
    }
}

/// <summary>
/// Information about a registered plugin.
/// </summary>
public class PluginInfo
{
    public Type Type { get; }
    public string Name { get; }
    public string Category { get; }
    public Dictionary<string, PropertyInfo> Options { get; } = new();
    
    public PluginInfo(Type type, PopstarPluginAttribute attr)
    {
        Type = type;
        Name = attr.Name;
        Category = attr.Category;
        
        // Find all [PopstarOption] properties
        foreach (var prop in type.GetProperties())
        {
            var optAttr = prop.GetCustomAttribute<PopstarOptionAttribute>();
            if (optAttr != null)
            {
                Options[optAttr.Name ?? prop.Name] = prop;
            }
        }
        
        foreach (var field in type.GetFields())
        {
            var optAttr = field.GetCustomAttribute<PopstarOptionAttribute>();
            if (optAttr != null)
            {
                // Wrap field in PropertyInfo-like access (simplified)
            }
        }
    }
    
    public T Create<T>() where T : class
    {
        return Activator.CreateInstance(Type) as T;
    }
}

/// <summary>
/// Information about an entity placement option.
/// </summary>
public class PlacementInfo
{
    public string DisplayName { get; set; }
    public string EntityName { get; set; }
    public bool IsTrigger { get; set; }
    public Dictionary<string, object> DefaultData { get; set; } = new();
}

/// <summary>
/// Base class for all entities in Popstarberry.
/// </summary>
public abstract class PopstarEntity
{
    #region Properties
    
    public string Name { get; set; }
    public int ID { get; set; }
    public Editor.PopstarRoom Room { get; set; }
    
    public Vector2 Position { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Vector2[] Nodes { get; set; } = Array.Empty<Vector2>();
    
    public Vector2 Center => Position + new Vector2(Width / 2f, Height / 2f);
    
    // Constraints
    public virtual int MinWidth => -1;
    public virtual int MinHeight => -1;
    public virtual int MinNodes => 0;
    public virtual int MaxNodes => 0;
    
    // Protected data dictionary
    protected Dictionary<string, object> data = new();
    
    #endregion

    #region Loading
    
    public virtual void LoadFromEntityData(EntityData entityData)
    {
        ID = entityData.ID;
        Position = entityData.Position;
        Width = entityData.Width;
        Height = entityData.Height;
        
        if (entityData.Nodes != null)
        {
            Nodes = entityData.Nodes;
        }
        
        // Load custom attributes
        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<PopstarOptionAttribute>();
            if (attr != null)
            {
                string key = attr.Name ?? prop.Name.ToLowerInvariant();
                if (entityData.Has(key))
                {
                    object value = GetTypedValue(entityData, key, prop.PropertyType);
                    prop.SetValue(this, value);
                }
            }
        }
    }
    
    public virtual void LoadFromElement(Element element)
    {
        if (element.Attributes == null) return;
        
        ID = element.AttrInt("id", 0);
        Position = new Vector2(element.AttrFloat("x", 0), element.AttrFloat("y", 0));
        Width = element.AttrInt("width", 8);
        Height = element.AttrInt("height", 8);
        
        // Store raw data
        foreach (var kvp in element.Attributes)
        {
            data[kvp.Key] = kvp.Value;
        }
        
        // Load nodes
        if (element.Children != null)
        {
            var nodeList = new List<Vector2>();
            foreach (var child in element.Children)
            {
                if (child.Name == "node")
                {
                    nodeList.Add(new Vector2(
                        child.AttrFloat("x", 0),
                        child.AttrFloat("y", 0)
                    ));
                }
            }
            Nodes = nodeList.ToArray();
        }
    }
    
    private object GetTypedValue(EntityData data, string key, Type targetType)
    {
        if (targetType == typeof(bool)) return data.Bool(key);
        if (targetType == typeof(int)) return data.Int(key);
        if (targetType == typeof(float)) return data.Float(key);
        if (targetType == typeof(string)) return data.Attr(key);
        if (targetType == typeof(char)) return data.Char(key);
        return data.Attr(key);
    }
    
    #endregion

    #region Initialization
    
    public virtual void Initialize() { }
    
    public virtual void InitializeAfter() { }
    
    #endregion

    #region Data Access
    
    public T Get<T>(string key, T defaultValue = default)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is T typed) return typed;
            
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
    
    public void Set(string key, object value)
    {
        data[key] = value;
    }
    
    #endregion

    #region Rendering
    
    public virtual void Render()
    {
        // Default render: simple rectangle
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Draw.HollowRect(x, y, Width, Height, Color.Red);
    }
    
    public virtual void HQRender() { }
    
    protected MTexture FromSprite(string spriteName, string animationId)
    {
        try
        {
            var sprite = GFX.SpriteBank.Create(spriteName);
            if (sprite != null)
            {
                return sprite.Animations[animationId]?.Frames[0];
            }
        }
        catch { }
        return null;
    }
    
    #endregion

    #region Selection
    
    public virtual Rectangle[] GetSelectionRects()
    {
        var rects = new List<Rectangle>
        {
            new Rectangle((int)Position.X, (int)Position.Y, Width, Height)
        };
        
        foreach (var node in Nodes)
        {
            rects.Add(new Rectangle((int)node.X - 4, (int)node.Y - 4, 8, 8));
        }
        
        return rects.ToArray();
    }
    
    #endregion

    #region Export
    
    public virtual Element Export()
    {
        var element = new Element
        {
            Name = Name,
            Attributes = new Dictionary<string, object>
            {
                ["id"] = ID,
                ["x"] = Position.X,
                ["y"] = Position.Y,
                ["width"] = Width,
                ["height"] = Height
            },
            Children = new List<Element>()
        };
        
        // Export custom options
        foreach (var kvp in data)
        {
            if (!element.Attributes.ContainsKey(kvp.Key))
            {
                element.Attributes[kvp.Key] = kvp.Value;
            }
        }
        
        // Export nodes
        foreach (var node in Nodes)
        {
            element.Children.Add(new Element
            {
                Name = "node",
                Attributes = new Dictionary<string, object>
                {
                    ["x"] = node.X,
                    ["y"] = node.Y
                }
            });
        }
        
        return element;
    }
    
    #endregion
}

/// <summary>
/// Placeholder entity for unknown/unregistered entity types.
/// </summary>
public class UnknownEntity : PopstarEntity
{
    public UnknownEntity(string name, Editor.PopstarRoom room, EntityData entityData)
    {
        Name = name;
        Room = room;
        LoadFromEntityData(entityData);
    }
    
    public UnknownEntity(string name, Editor.PopstarRoom room, Element element)
    {
        Name = name;
        Room = room;
        LoadFromElement(element);
    }
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        // Draw with warning color
        Draw.Rect(x, y, Width, Height, Color.Orange * 0.3f);
        Draw.HollowRect(x, y, Width, Height, Color.Orange);
        
        // Draw name
        ActiveFont.Draw(Name, new Vector2(x + 2, y + 2), Vector2.Zero, Vector2.One * 0.4f, Color.Orange);
    }
}

/// <summary>
/// Represents a decal in the editor.
/// </summary>
public class PopstarDecal
{
    public string Texture { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;
    public Editor.PopstarRoom Room { get; set; }
    public bool IsForeground { get; set; }
    
    private MTexture texture;
    
    public PopstarDecal(DecalData data, Editor.PopstarRoom room, bool isForeground)
    {
        Texture = data.Texture;
        Position = data.Position;
        Scale = data.Scale;
        Room = room;
        IsForeground = isForeground;
        
        // Load texture
        string path = "decals/" + Texture;
        if (GFX.Game.Has(path))
        {
            texture = GFX.Game[path];
        }
    }
    
    public void Render(Vector2 offset)
    {
        if (texture != null)
        {
            texture.DrawCentered(offset + Position, Color.White, Scale);
        }
        else
        {
            // Placeholder
            Draw.Rect(offset + Position - Vector2.One * 4, 8, 8, Color.Purple * 0.5f);
        }
    }
    
    public Element Export()
    {
        return new Element
        {
            Attributes = new Dictionary<string, object>
            {
                ["texture"] = Texture,
                ["x"] = Position.X,
                ["y"] = Position.Y,
                ["scaleX"] = Scale.X,
                ["scaleY"] = Scale.Y
            }
        };
    }
}
