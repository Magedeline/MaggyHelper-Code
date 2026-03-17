using System;
using System.Collections.Generic;
using System.Reflection;
using MaggyHelper.Utils;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Popstarberry.Stylegrounds;

using Element = BinaryPacker.Element;

/// <summary>
/// Registry for styleground plugins in Popstarberry.
/// </summary>
public static class StylegroundRegistry
{
    private static Dictionary<string, Type> stylegroundTypes = new();
    
    public static void LoadPlugins(List<Type> pluginTypes)
    {
        foreach (var type in pluginTypes)
        {
            var attr = type.GetCustomAttribute<PopstarPluginAttribute>();
            if (attr == null) continue;
            
            if (typeof(PopstarStyleground).IsAssignableFrom(type))
            {
                stylegroundTypes[attr.Name.ToLowerInvariant()] = type;
            }
        }
        
        // Register built-in stylegrounds
        RegisterBuiltins();
        
        PopstarberryModule.Log(LogLevel.Info, $"Loaded {stylegroundTypes.Count} styleground plugins.");
    }
    
    private static void RegisterBuiltins()
    {
        // Common stylegrounds
        stylegroundTypes["parallax"] = typeof(ParallaxStyleground);
        stylegroundTypes["blackhole"] = typeof(BlackholeStyleground);
        stylegroundTypes["planets"] = typeof(PlanetsStyleground);
        stylegroundTypes["starfield"] = typeof(StarfieldStyleground);
        stylegroundTypes["stardust"] = typeof(StardustStyleground);
    }
    
    public static PopstarStyleground Create(string name, Editor.PopstarMap map, Element data, Element applyData = null)
    {
        PopstarStyleground styleground;
        
        if (stylegroundTypes.TryGetValue(name.ToLowerInvariant(), out var type))
        {
            styleground = (PopstarStyleground)Activator.CreateInstance(type);
        }
        else
        {
            // Unknown styleground - use placeholder
            styleground = new UnknownStyleground(name);
        }
        
        styleground.Name = name;
        styleground.Map = map;
        styleground.LoadFromElement(data, applyData);
        
        return styleground;
    }
}

/// <summary>
/// Base class for stylegrounds in Popstarberry.
/// </summary>
public abstract class PopstarStyleground
{
    #region Properties
    
    public string Name { get; set; }
    public Editor.PopstarMap Map { get; set; }
    
    // Common styleground properties
    public HashSet<string> Tags { get; } = new();
    public Vector2 Position { get; set; }
    public Vector2 Scroll { get; set; } = Vector2.One;
    public Vector2 Speed { get; set; }
    public float WindMultiplier { get; set; }
    public Color Color { get; set; } = Color.White;
    public bool LoopX { get; set; } = true;
    public bool LoopY { get; set; } = true;
    public bool? DreamingOnly { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public string OnlyIn { get; set; } = "*";
    public string ExcludeFrom { get; set; } = "";
    public string Flag { get; set; } = "";
    public string NotFlag { get; set; } = "";
    public bool InstantIn { get; set; } = true;
    public bool InstantOut { get; set; }
    
    public virtual bool Additive => false;
    
    protected Dictionary<string, object> data = new();
    
    #endregion

    #region Loading
    
    public virtual void LoadFromElement(Element element, Element applyData = null)
    {
        // Apply base data first
        if (applyData?.Attributes != null)
        {
            foreach (var kvp in applyData.Attributes)
            {
                ApplyAttribute(kvp.Key, kvp.Value);
            }
        }
        
        // Then override with specific data
        if (element?.Attributes != null)
        {
            foreach (var kvp in element.Attributes)
            {
                ApplyAttribute(kvp.Key, kvp.Value);
                data[kvp.Key] = kvp.Value;
            }
        }
    }
    
    private void ApplyAttribute(string key, object value)
    {
        switch (key.ToLowerInvariant())
        {
            case "tag":
                if (value is string tagStr)
                {
                    foreach (var tag in tagStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        Tags.Add(tag.Trim());
                    }
                }
                break;
                
            case "x":
                Position = new Vector2(Convert.ToSingle(value), Position.Y);
                break;
                
            case "y":
                Position = new Vector2(Position.X, Convert.ToSingle(value));
                break;
                
            case "scrollx":
                Scroll = new Vector2(Convert.ToSingle(value), Scroll.Y);
                break;
                
            case "scrolly":
                Scroll = new Vector2(Scroll.X, Convert.ToSingle(value));
                break;
                
            case "speedx":
                Speed = new Vector2(Convert.ToSingle(value), Speed.Y);
                break;
                
            case "speedy":
                Speed = new Vector2(Speed.X, Convert.ToSingle(value));
                break;
                
            case "color":
                Color = ParseColor(value);
                break;
                
            case "loopx":
                LoopX = Convert.ToBoolean(value);
                break;
                
            case "loopy":
                LoopY = Convert.ToBoolean(value);
                break;
                
            case "flipx":
                FlipX = Convert.ToBoolean(value);
                break;
                
            case "flipy":
                FlipY = Convert.ToBoolean(value);
                break;
                
            case "only":
                OnlyIn = value?.ToString() ?? "*";
                break;
                
            case "exclude":
                ExcludeFrom = value?.ToString() ?? "";
                break;
                
            case "flag":
                Flag = value?.ToString() ?? "";
                break;
                
            case "notflag":
                NotFlag = value?.ToString() ?? "";
                break;
                
            case "dreaming":
                DreamingOnly = Convert.ToBoolean(value);
                break;
                
            case "instantin":
                InstantIn = Convert.ToBoolean(value);
                break;
                
            case "instantout":
                InstantOut = Convert.ToBoolean(value);
                break;
                
            case "windmultiplier":
                WindMultiplier = Convert.ToSingle(value);
                break;
        }
    }
    
    private Color ParseColor(object value)
    {
        if (value is string colorStr)
        {
            if (colorStr.StartsWith("#"))
            {
                colorStr = colorStr.Substring(1);
            }
            
            if (colorStr.Length == 6)
            {
                int r = Convert.ToInt32(colorStr.Substring(0, 2), 16);
                int g = Convert.ToInt32(colorStr.Substring(2, 2), 16);
                int b = Convert.ToInt32(colorStr.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
        }
        return Color.White;
    }
    
    #endregion

    #region Visibility
    
    public bool IsVisibleIn(Editor.PopstarRoom room)
    {
        // Check room name filters
        if (!MatchRoomName(OnlyIn, room.Name))
            return false;
            
        if (!string.IsNullOrEmpty(ExcludeFrom) && MatchRoomName(ExcludeFrom, room.Name))
            return false;
        
        return true;
    }
    
    private static bool MatchRoomName(string pattern, string roomName)
    {
        if (pattern == "*") return true;
        
        foreach (string part in pattern.Split(','))
        {
            string trimmed = part.Trim();
            
            if (trimmed.EndsWith("*"))
            {
                string prefix = trimmed.Substring(0, trimmed.Length - 1);
                if (roomName.StartsWith(prefix))
                    return true;
            }
            else if (trimmed == roomName)
            {
                return true;
            }
        }
        
        return false;
    }
    
    #endregion

    #region Rendering
    
    public virtual void Render(Editor.PopstarRoom room) { }
    
    public virtual string Title()
    {
        return Name;
    }
    
    #endregion

    #region Export
    
    public virtual Element Export()
    {
        var element = new Element
        {
            Name = Name,
            Attributes = new Dictionary<string, object>()
        };
        
        // Export common properties
        if (Tags.Count > 0)
            element.Attributes["tag"] = string.Join(";", Tags);
        
        if (Position != Vector2.Zero)
        {
            element.Attributes["x"] = Position.X;
            element.Attributes["y"] = Position.Y;
        }
        
        if (Scroll != Vector2.One)
        {
            element.Attributes["scrollx"] = Scroll.X;
            element.Attributes["scrolly"] = Scroll.Y;
        }
        
        if (Speed != Vector2.Zero)
        {
            element.Attributes["speedx"] = Speed.X;
            element.Attributes["speedy"] = Speed.Y;
        }
        
        if (Color != Color.White)
        {
            element.Attributes["color"] = $"{Color.R:X2}{Color.G:X2}{Color.B:X2}";
        }
        
        if (!LoopX) element.Attributes["loopx"] = false;
        if (!LoopY) element.Attributes["loopy"] = false;
        if (FlipX) element.Attributes["flipx"] = true;
        if (FlipY) element.Attributes["flipy"] = true;
        
        if (OnlyIn != "*") element.Attributes["only"] = OnlyIn;
        if (!string.IsNullOrEmpty(ExcludeFrom)) element.Attributes["exclude"] = ExcludeFrom;
        if (!string.IsNullOrEmpty(Flag)) element.Attributes["flag"] = Flag;
        if (!string.IsNullOrEmpty(NotFlag)) element.Attributes["notflag"] = NotFlag;
        
        if (DreamingOnly.HasValue) element.Attributes["dreaming"] = DreamingOnly.Value;
        if (!InstantIn) element.Attributes["instantin"] = false;
        if (InstantOut) element.Attributes["instantout"] = true;
        if (WindMultiplier != 0) element.Attributes["windmultiplier"] = WindMultiplier;
        
        // Add custom data
        foreach (var kvp in data)
        {
            if (!element.Attributes.ContainsKey(kvp.Key))
            {
                element.Attributes[kvp.Key] = kvp.Value;
            }
        }
        
        return element;
    }
    
    #endregion
}

#region Built-in Stylegrounds

/// <summary>
/// Parallax/image styleground.
/// </summary>
public class ParallaxStyleground : PopstarStyleground
{
    public string Texture { get; set; }
    
    private MTexture texture;
    
    public override void LoadFromElement(Element element, Element applyData = null)
    {
        base.LoadFromElement(element, applyData);
        
        Texture = element?.Attr("texture") ?? applyData?.Attr("texture") ?? "";
        
        if (!string.IsNullOrEmpty(Texture))
        {
            string path = "bgs/" + Texture;
            if (GFX.Game.Has(path))
            {
                texture = GFX.Game[path];
            }
        }
    }
    
    public override void Render(Editor.PopstarRoom room)
    {
        if (texture == null) return;
        
        Vector2 roomOffset = new Vector2(room.Bounds.X * 8, room.Bounds.Y * 8);
        
        // Simplified parallax rendering
        texture.Draw(roomOffset + Position, Vector2.Zero, Color);
    }
    
    public override string Title()
    {
        return $"Parallax: {Texture}";
    }
}

/// <summary>
/// Blackhole effect styleground.
/// </summary>
public class BlackholeStyleground : PopstarStyleground
{
    public override void Render(Editor.PopstarRoom room)
    {
        Vector2 roomOffset = new Vector2(room.Bounds.X * 8, room.Bounds.Y * 8);
        Vector2 center = roomOffset + new Vector2(room.Bounds.Width * 4, room.Bounds.Height * 4);
        
        // Draw simple representation
        for (int i = 0; i < 3; i++)
        {
            float radius = 20 + i * 15;
            Draw.Circle(center, radius, Color.Purple * (0.3f - i * 0.1f), 32);
        }
    }
}

/// <summary>
/// Planets effect styleground.
/// </summary>
public class PlanetsStyleground : PopstarStyleground
{
    public int Count { get; set; } = 32;
    public string Size { get; set; } = "small";
    
    public override void LoadFromElement(Element element, Element applyData = null)
    {
        base.LoadFromElement(element, applyData);
        Count = element?.AttrInt("count", 32) ?? 32;
        Size = element?.Attr("size", "small") ?? "small";
    }
    
    public override void Render(Editor.PopstarRoom room)
    {
        // Draw placeholder circles for planets
        Vector2 roomOffset = new Vector2(room.Bounds.X * 8, room.Bounds.Y * 8);
        Random rand = new Pcg32Random(unchecked((uint)room.Name.GetHashCode()));
        
        for (int i = 0; i < Math.Min(Count, 10); i++)
        {
            Vector2 pos = roomOffset + new Vector2(
                (float)rand.NextDouble() * room.Bounds.Width * 8,
                (float)rand.NextDouble() * room.Bounds.Height * 8
            );
            float radius = Size == "big" ? 8 : 4;
            Draw.Circle(pos, radius, Color * 0.5f, 8);
        }
    }
}

/// <summary>
/// Starfield effect styleground.
/// </summary>
public class StarfieldStyleground : PopstarStyleground
{
    public override void Render(Editor.PopstarRoom room)
    {
        Vector2 roomOffset = new Vector2(room.Bounds.X * 8, room.Bounds.Y * 8);
        Random rand = new Pcg32Random(unchecked((uint)room.Name.GetHashCode()));
        
        for (int i = 0; i < 50; i++)
        {
            Vector2 pos = roomOffset + new Vector2(
                (float)rand.NextDouble() * room.Bounds.Width * 8,
                (float)rand.NextDouble() * room.Bounds.Height * 8
            );
            float brightness = (float)rand.NextDouble() * 0.5f + 0.2f;
            Draw.Point(pos, Color * brightness);
        }
    }
}

/// <summary>
/// Stardust effect styleground.
/// </summary>
public class StardustStyleground : PopstarStyleground
{
    public override void Render(Editor.PopstarRoom room)
    {
        // Similar to starfield but with particles
        Vector2 roomOffset = new Vector2(room.Bounds.X * 8, room.Bounds.Y * 8);
        Random rand = new Pcg32Random(unchecked((uint)(room.Name.GetHashCode() + 1)));
        
        for (int i = 0; i < 30; i++)
        {
            Vector2 pos = roomOffset + new Vector2(
                (float)rand.NextDouble() * room.Bounds.Width * 8,
                (float)rand.NextDouble() * room.Bounds.Height * 8
            );
            float size = (float)rand.NextDouble() * 2f + 1f;
            Draw.Rect(pos - Vector2.One * size / 2, size, size, Color * 0.4f);
        }
    }
}

/// <summary>
/// Placeholder for unknown stylegrounds.
/// </summary>
public class UnknownStyleground : PopstarStyleground
{
    public UnknownStyleground(string name)
    {
        Name = name;
    }
    
    public override void Render(Editor.PopstarRoom room)
    {
        // Don't render unknown stylegrounds, just log
    }
    
    public override string Title()
    {
        return $"Unknown: {Name}";
    }
}

#endregion
