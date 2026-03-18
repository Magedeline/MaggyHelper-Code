using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace MaggyHelper.Popstarberry.Loenn;

/// <summary>
/// Bridge to integrate with Loenn's Lua-based plugin system.
/// Provides support for loading and executing Lua entity/trigger definitions.
/// </summary>
public static class LoennBridge
{
    #region State
    
    private static bool initialized;
    private static Dictionary<string, LuaEntityDefinition> entityDefinitions = new();
    private static Dictionary<string, LuaTriggerDefinition> triggerDefinitions = new();
    private static Dictionary<string, LuaStylegroundDefinition> stylegroundDefinitions = new();
    
    public static string LoennPluginPath { get; private set; }
    
    #endregion

    #region Initialization
    
    public static void Initialize()
    {
        if (initialized) return;
        
        // Find Loenn plugin path in the mod directory
        LoennPluginPath = Path.Combine(
            Path.GetDirectoryName(typeof(LoennBridge).Assembly.Location) ?? "",
            "..", "Loenn"
        );
        
        // Load any Lua definitions
        if (PopstarberryModule.Settings?.EnableLuaPlugins == true)
        {
            LoadLuaDefinitions();
        }
        
        initialized = true;
        PopstarberryModule.Log(LogLevel.Info, "LoennBridge initialized.");
    }
    
    public static void Cleanup()
    {
        entityDefinitions.Clear();
        triggerDefinitions.Clear();
        stylegroundDefinitions.Clear();
        initialized = false;
        
        PopstarberryModule.Log(LogLevel.Info, "LoennBridge cleaned up.");
    }
    
    #endregion

    #region Lua Loading
    
    private static void LoadLuaDefinitions()
    {
        if (!Directory.Exists(LoennPluginPath))
        {
            PopstarberryModule.Log(LogLevel.Warn, $"Loenn plugin path not found: {LoennPluginPath}");
            return;
        }
        
        // Load entity definitions
        string entitiesPath = Path.Combine(LoennPluginPath, "entities");
        if (Directory.Exists(entitiesPath))
        {
            foreach (string file in Directory.GetFiles(entitiesPath, "*.lua"))
            {
                LoadEntityDefinition(file);
            }
        }
        
        // Load trigger definitions
        string triggersPath = Path.Combine(LoennPluginPath, "triggers");
        if (Directory.Exists(triggersPath))
        {
            foreach (string file in Directory.GetFiles(triggersPath, "*.lua"))
            {
                LoadTriggerDefinition(file);
            }
        }
        
        // Load effect definitions
        string effectsPath = Path.Combine(LoennPluginPath, "effects");
        if (Directory.Exists(effectsPath))
        {
            foreach (string file in Directory.GetFiles(effectsPath, "*.lua"))
            {
                LoadStylegroundDefinition(file);
            }
        }
        
        PopstarberryModule.Log(LogLevel.Info, 
            $"Loaded {entityDefinitions.Count} entity, {triggerDefinitions.Count} trigger, {stylegroundDefinitions.Count} styleground definitions from Lua.");
    }
    
    private static void LoadEntityDefinition(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            var definition = ParseLuaEntityDefinition(content);
            
            if (definition != null && !string.IsNullOrEmpty(definition.Name))
            {
                entityDefinitions[definition.Name] = definition;
            }
        }
        catch (Exception ex)
        {
            PopstarberryModule.Log(LogLevel.Warn, $"Failed to load entity definition from {filePath}: {ex.Message}");
        }
    }
    
    private static void LoadTriggerDefinition(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            var definition = ParseLuaTriggerDefinition(content);
            
            if (definition != null && !string.IsNullOrEmpty(definition.Name))
            {
                triggerDefinitions[definition.Name] = definition;
            }
        }
        catch (Exception ex)
        {
            PopstarberryModule.Log(LogLevel.Warn, $"Failed to load trigger definition from {filePath}: {ex.Message}");
        }
    }
    
    private static void LoadStylegroundDefinition(string filePath)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            var definition = ParseLuaStylegroundDefinition(content);
            
            if (definition != null && !string.IsNullOrEmpty(definition.Name))
            {
                stylegroundDefinitions[definition.Name] = definition;
            }
        }
        catch (Exception ex)
        {
            PopstarberryModule.Log(LogLevel.Warn, $"Failed to load styleground definition from {filePath}: {ex.Message}");
        }
    }
    
    #endregion

    #region Lua Parsing (Simplified)
    
    // Note: This is a simplified parser. In production, you'd want to use a proper Lua interpreter
    // like NLua or MoonSharp for full Lua support.
    
    private static LuaEntityDefinition ParseLuaEntityDefinition(string luaContent)
    {
        var def = new LuaEntityDefinition();
        
        // Parse name
        def.Name = ExtractStringValue(luaContent, "name");
        
        // Parse placements
        def.Placements = ParsePlacements(luaContent);
        
        // Parse field info
        def.FieldInfo = ParseFieldInfo(luaContent);
        
        // Parse rendering hints
        def.Texture = ExtractStringValue(luaContent, "texture");
        def.NodeTexture = ExtractStringValue(luaContent, "nodeTexture");
        def.NodeLineRenderType = ExtractStringValue(luaContent, "nodeLineRenderType") ?? "line";
        
        // Parse sizing
        def.MinWidth = ExtractIntValue(luaContent, "minimumWidth", -1);
        def.MinHeight = ExtractIntValue(luaContent, "minimumHeight", -1);
        def.MaxNodes = ExtractIntValue(luaContent, "maximumNodes", 0);
        
        return def;
    }
    
    private static LuaTriggerDefinition ParseLuaTriggerDefinition(string luaContent)
    {
        var def = new LuaTriggerDefinition();
        
        def.Name = ExtractStringValue(luaContent, "name");
        def.Placements = ParsePlacements(luaContent);
        def.FieldInfo = ParseFieldInfo(luaContent);
        
        return def;
    }
    
    private static LuaStylegroundDefinition ParseLuaStylegroundDefinition(string luaContent)
    {
        var def = new LuaStylegroundDefinition();
        
        def.Name = ExtractStringValue(luaContent, "name");
        def.FieldInfo = ParseFieldInfo(luaContent);
        
        return def;
    }
    
    private static string ExtractStringValue(string content, string key)
    {
        // Simple pattern: key = "value"
        int idx = content.IndexOf(key + " =");
        if (idx < 0) idx = content.IndexOf(key + "=");
        if (idx < 0) return null;
        
        int quoteStart = content.IndexOf('"', idx);
        if (quoteStart < 0) return null;
        
        int quoteEnd = content.IndexOf('"', quoteStart + 1);
        if (quoteEnd < 0) return null;
        
        return content.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }
    
    private static int ExtractIntValue(string content, string key, int defaultValue)
    {
        int idx = content.IndexOf(key + " =");
        if (idx < 0) idx = content.IndexOf(key + "=");
        if (idx < 0) return defaultValue;
        
        int eqIdx = content.IndexOf('=', idx);
        int lineEnd = content.IndexOfAny(new[] { '\n', '\r', ',' }, eqIdx);
        if (lineEnd < 0) lineEnd = content.Length;
        
        string valueStr = content.Substring(eqIdx + 1, lineEnd - eqIdx - 1).Trim();
        
        if (int.TryParse(valueStr, out int value))
        {
            return value;
        }
        
        return defaultValue;
    }
    
    private static List<LuaPlacement> ParsePlacements(string content)
    {
        var placements = new List<LuaPlacement>();
        
        // Look for placements = { ... }
        int idx = content.IndexOf("placements");
        if (idx < 0) return placements;
        
        int braceStart = content.IndexOf('{', idx);
        if (braceStart < 0) return placements;
        
        // Find matching brace
        int depth = 1;
        int i = braceStart + 1;
        while (i < content.Length && depth > 0)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}') depth--;
            i++;
        }
        
        string placementsContent = content.Substring(braceStart + 1, i - braceStart - 2);
        
        // Parse individual placements (simplified)
        int placeIdx = 0;
        while ((placeIdx = placementsContent.IndexOf("name", placeIdx)) >= 0)
        {
            var placement = new LuaPlacement
            {
                Name = ExtractStringValue(placementsContent.Substring(placeIdx), "name")
            };
            
            if (!string.IsNullOrEmpty(placement.Name))
            {
                placements.Add(placement);
            }
            
            placeIdx++;
        }
        
        return placements;
    }
    
    private static Dictionary<string, LuaFieldInfo> ParseFieldInfo(string content)
    {
        var fields = new Dictionary<string, LuaFieldInfo>();
        
        // Look for fieldInformation = { ... }
        int idx = content.IndexOf("fieldInformation");
        if (idx < 0) return fields;
        
        int braceStart = content.IndexOf('{', idx);
        if (braceStart < 0) return fields;
        
        // Find matching brace
        int depth = 1;
        int i = braceStart + 1;
        while (i < content.Length && depth > 0)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}') depth--;
            i++;
        }
        
        // Parse field info (very simplified)
        string fieldsContent = content.Substring(braceStart + 1, i - braceStart - 2);
        
        // This is a very basic parser - a real implementation would need proper Lua parsing
        
        return fields;
    }
    
    #endregion

    #region Definition Access
    
    public static LuaEntityDefinition GetEntityDefinition(string name)
    {
        entityDefinitions.TryGetValue(name, out var def);
        return def;
    }
    
    public static LuaTriggerDefinition GetTriggerDefinition(string name)
    {
        triggerDefinitions.TryGetValue(name, out var def);
        return def;
    }
    
    public static LuaStylegroundDefinition GetStylegroundDefinition(string name)
    {
        stylegroundDefinitions.TryGetValue(name, out var def);
        return def;
    }
    
    public static IEnumerable<string> GetAllEntityNames() => entityDefinitions.Keys;
    public static IEnumerable<string> GetAllTriggerNames() => triggerDefinitions.Keys;
    public static IEnumerable<string> GetAllStylegroundNames() => stylegroundDefinitions.Keys;
    
    #endregion

    #region Placement Helper
    
    /// <summary>
    /// Get all available placements for the editor palette.
    /// </summary>
    public static IEnumerable<PlacementInfo> GetAllPlacements()
    {
        foreach (var kvp in entityDefinitions)
        {
            foreach (var placement in kvp.Value.Placements)
            {
                yield return new PlacementInfo
                {
                    Name = placement.Name,
                    EntityName = kvp.Key,
                    IsTrigger = false,
                    DefaultData = placement.Data
                };
            }
        }
        
        foreach (var kvp in triggerDefinitions)
        {
            foreach (var placement in kvp.Value.Placements)
            {
                yield return new PlacementInfo
                {
                    Name = placement.Name,
                    EntityName = kvp.Key,
                    IsTrigger = true,
                    DefaultData = placement.Data
                };
            }
        }
    }
    
    #endregion
}

#region Definition Types

public class LuaEntityDefinition
{
    public string Name { get; set; }
    public List<LuaPlacement> Placements { get; set; } = new();
    public Dictionary<string, LuaFieldInfo> FieldInfo { get; set; } = new();
    
    public string Texture { get; set; }
    public string NodeTexture { get; set; }
    public string NodeLineRenderType { get; set; } = "line";
    
    public int MinWidth { get; set; } = -1;
    public int MinHeight { get; set; } = -1;
    public int MaxNodes { get; set; } = 0;
}

public class LuaTriggerDefinition
{
    public string Name { get; set; }
    public List<LuaPlacement> Placements { get; set; } = new();
    public Dictionary<string, LuaFieldInfo> FieldInfo { get; set; } = new();
}

public class LuaStylegroundDefinition
{
    public string Name { get; set; }
    public Dictionary<string, LuaFieldInfo> FieldInfo { get; set; } = new();
}

public class LuaPlacement
{
    public string Name { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class LuaFieldInfo
{
    public string FieldType { get; set; } // "integer", "number", "string", "boolean", "color", etc.
    public List<object> Options { get; set; } // For dropdowns
    public object MinValue { get; set; }
    public object MaxValue { get; set; }
    public bool Editable { get; set; } = true;
}

public class PlacementInfo
{
    public string Name { get; set; }
    public string EntityName { get; set; }
    public bool IsTrigger { get; set; }
    public Dictionary<string, object> DefaultData { get; set; }
}

#endregion
