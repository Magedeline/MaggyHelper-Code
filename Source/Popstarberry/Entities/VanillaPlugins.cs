using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Popstarberry.Entities;

/// <summary>
/// Sample entity plugins demonstrating how to create Popstarberry entity definitions.
/// These follow the same pattern as Snowberry's entity plugins.
/// </summary>
/// 
// ============================================
// Vanilla Entity Plugins
// ============================================

[PopstarPlugin("strawberry", "Collectibles")]
public class Plugin_Strawberry : PopstarEntity
{
    [PopstarOption("winged")] public bool Winged { get; set; } = false;
    [PopstarOption("moon")] public bool Moon { get; set; } = false;
    [PopstarOption("order")] public int Order { get; set; } = -1;
    [PopstarOption("checkpointID")] public int CheckpointID { get; set; } = -1;
    
    public override int MaxNodes => -1;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = Moon ? Color.Purple : Color.Red;
        
        // Draw strawberry shape
        Draw.Circle(new Vector2(x + 8, y + 8), 6, color, 8);
        
        if (Winged)
        {
            // Draw wing hints
            Draw.Line(x + 2, y + 6, x - 2, y + 2, Color.White * 0.5f);
            Draw.Line(x + 14, y + 6, x + 18, y + 2, Color.White * 0.5f);
        }
        
        // Draw nodes (for golden/winged paths)
        Vector2 prev = new Vector2(x + 8, y + 8);
        foreach (var node in Nodes)
        {
            Vector2 nodePos = node + new Vector2(Room.Bounds.X * 8, Room.Bounds.Y * 8);
            Draw.Line(prev, nodePos, Color.Yellow * 0.5f);
            Draw.Circle(nodePos, 3, Color.Yellow * 0.5f, 6);
            prev = nodePos;
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Strawberry", "strawberry");
        PluginRegistry.AddPlacement("Strawberry (Winged)", "strawberry", 
            new() { ["winged"] = true });
        PluginRegistry.AddPlacement("Moon Berry", "strawberry", 
            new() { ["moon"] = true });
    }
}

[PopstarPlugin("refill", "Collectibles")]
public class Plugin_Refill : PopstarEntity
{
    [PopstarOption("twoDash")] public bool TwoDash { get; set; } = false;
    [PopstarOption("oneUse")] public bool OneUse { get; set; } = false;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = TwoDash ? Color.HotPink : Color.LimeGreen;
        
        Draw.Circle(new Vector2(x + 8, y + 8), 5, color, 8);
        
        if (OneUse)
        {
            Draw.Circle(new Vector2(x + 8, y + 8), 3, Color.DarkGray, 6);
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Refill", "refill");
        PluginRegistry.AddPlacement("Refill (Two Dash)", "refill", 
            new() { ["twoDash"] = true });
    }
}

[PopstarPlugin("spring", "Mechanics")]
public class Plugin_Spring : PopstarEntity
{
    [PopstarOption("orientation")] public string Orientation { get; set; } = "Floor";
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = Color.Yellow;
        
        switch (Orientation?.ToLower())
        {
            case "floor":
                Draw.Rect(x, y + 8, 16, 8, color);
                Draw.Line(x + 8, y + 8, x + 8, y, Color.Yellow);
                break;
            case "ceiling":
                Draw.Rect(x, y, 16, 8, color);
                Draw.Line(x + 8, y + 8, x + 8, y + 16, Color.Yellow);
                break;
            case "wallleft":
                Draw.Rect(x, y, 8, 16, color);
                Draw.Line(x + 8, y + 8, x + 16, y + 8, Color.Yellow);
                break;
            case "wallright":
                Draw.Rect(x + 8, y, 8, 16, color);
                Draw.Line(x + 8, y + 8, x, y + 8, Color.Yellow);
                break;
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Spring (Floor)", "spring", 
            new() { ["orientation"] = "Floor" });
        PluginRegistry.AddPlacement("Spring (Ceiling)", "spring", 
            new() { ["orientation"] = "Ceiling" });
        PluginRegistry.AddPlacement("Spring (Wall Left)", "spring", 
            new() { ["orientation"] = "WallLeft" });
        PluginRegistry.AddPlacement("Spring (Wall Right)", "spring", 
            new() { ["orientation"] = "WallRight" });
    }
}

[PopstarPlugin("spinner", "Hazards")]
public class Plugin_Spinner : PopstarEntity
{
    [PopstarOption("attachToSolid")] public bool AttachToSolid { get; set; } = false;
    [PopstarOption("color")] public string SpinnerColor { get; set; } = "Blue";
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = SpinnerColor?.ToLower() switch
        {
            "red" => Color.Red,
            "purple" => Color.Purple,
            "rainbow" => Color.Cyan,
            _ => Color.Blue
        };
        
        // Draw spinner shape
        Draw.Circle(new Vector2(x, y), 6, color, 8);
        Draw.Point(new Vector2(x, y), Color.White);
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Spinner (Blue)", "spinner", 
            new() { ["color"] = "Blue" });
        PluginRegistry.AddPlacement("Spinner (Red)", "spinner", 
            new() { ["color"] = "Red" });
        PluginRegistry.AddPlacement("Spinner (Purple)", "spinner", 
            new() { ["color"] = "Purple" });
    }
}

[PopstarPlugin("spikesUp", "Hazards")]
public class Plugin_SpikesUp : PopstarEntity
{
    [PopstarOption("type")] public string SpikeType { get; set; } = "default";
    
    public override int MinWidth => 8;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = Color.White;
        
        for (int i = 0; i < Width; i += 8)
        {
            Draw.Line(x + i + 4, y + 8, x + i, y, color);
            Draw.Line(x + i + 4, y + 8, x + i + 8, y, color);
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Spikes (Up)", "spikesUp");
    }
}

[PopstarPlugin("booster", "Mechanics")]
public class Plugin_Booster : PopstarEntity
{
    [PopstarOption("red")] public bool Red { get; set; } = false;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = Red ? Color.Red : Color.Green;
        
        Draw.Circle(new Vector2(x + 8, y + 8), 8, color, 12);
        Draw.Circle(new Vector2(x + 8, y + 8), 4, color * 1.5f, 8);
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Booster (Green)", "booster");
        PluginRegistry.AddPlacement("Booster (Red)", "booster", 
            new() { ["red"] = true });
    }
}

[PopstarPlugin("dreamBlock", "Mechanics")]
public class Plugin_DreamBlock : PopstarEntity
{
    [PopstarOption("fastMoving")] public bool FastMoving { get; set; } = false;
    
    public override int MinWidth => 8;
    public override int MinHeight => 8;
    public override int MaxNodes => -1;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Color color = Color.DarkBlue * 0.5f;
        
        Draw.Rect(x, y, Width, Height, color);
        Draw.HollowRect(x, y, Width, Height, Color.White * 0.3f);
        
        // Draw some "dream" particles inside
        var rand = new System.Random((int)(x + y));
        for (int i = 0; i < (Width * Height) / 64; i++)
        {
            float px = x + (float)rand.NextDouble() * Width;
            float py = y + (float)rand.NextDouble() * Height;
            Draw.Point(new Vector2(px, py), Color.White * 0.5f);
        }
        
        // Draw nodes for moving dream blocks
        if (Nodes.Length > 0)
        {
            Vector2 start = new Vector2(x + Width / 2, y + Height / 2);
            foreach (var node in Nodes)
            {
                Vector2 nodePos = node + new Vector2(Room.Bounds.X * 8 + Width / 2, Room.Bounds.Y * 8 + Height / 2);
                Draw.Line(start, nodePos, Color.Cyan * 0.5f);
                Draw.HollowRect(node.X + Room.Bounds.X * 8, node.Y + Room.Bounds.Y * 8, Width, Height, Color.Cyan * 0.3f);
            }
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Dream Block", "dreamBlock");
        PluginRegistry.AddPlacement("Dream Block (Moving)", "dreamBlock");
    }
}

// ============================================
// Trigger Plugins
// ============================================

[PopstarPlugin("cameraTargetTrigger", "Triggers")]
public class Plugin_CameraTargetTrigger : PopstarEntity
{
    [PopstarOption("lerpStrength")] public float LerpStrength { get; set; } = 0f;
    [PopstarOption("positionMode")] public string PositionMode { get; set; } = "NoEffect";
    [PopstarOption("xOnly")] public bool XOnly { get; set; } = false;
    [PopstarOption("yOnly")] public bool YOnly { get; set; } = false;
    
    public override int MinWidth => 8;
    public override int MinHeight => 8;
    public override int MaxNodes => 1;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Draw.Rect(x, y, Width, Height, Color.Cyan * 0.2f);
        Draw.HollowRect(x, y, Width, Height, Color.Cyan * 0.5f);
        
        if (Nodes.Length > 0)
        {
            Vector2 nodePos = Nodes[0] + new Vector2(Room.Bounds.X * 8, Room.Bounds.Y * 8);
            Draw.Line(new Vector2(x + Width / 2, y + Height / 2), nodePos, Color.Cyan);
            Draw.Circle(nodePos, 4, Color.Cyan, 6);
        }
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Camera Target Trigger", "cameraTargetTrigger", isTrigger: true);
    }
}

[PopstarPlugin("musicTrigger", "Triggers")]
public class Plugin_MusicTrigger : PopstarEntity
{
    [PopstarOption("track")] public string Track { get; set; } = "";
    [PopstarOption("resetOnLeave")] public bool ResetOnLeave { get; set; } = true;
    
    public override int MinWidth => 8;
    public override int MinHeight => 8;
    
    public override void Render()
    {
        float x = Position.X + Room.Bounds.X * 8;
        float y = Position.Y + Room.Bounds.Y * 8;
        
        Draw.Rect(x, y, Width, Height, Color.Purple * 0.2f);
        Draw.HollowRect(x, y, Width, Height, Color.Purple * 0.5f);
        
        // Music note symbol
        ActiveFont.Draw("♪", new Vector2(x + 4, y + 4), Vector2.Zero, Vector2.One * 0.4f, Color.Purple);
    }
    
    public static void AddPlacements()
    {
        PluginRegistry.AddPlacement("Music Trigger", "musicTrigger", isTrigger: true);
    }
}
