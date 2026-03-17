#nullable enable

namespace MaggyHelper.Helpers;

/// <summary>
/// Helper class for texture operations
/// </summary>
public static class TextureUtil
{
    #region Path Constants
    
    /// <summary>
    /// Base path to MaggyHelper's Graphics folder (relative to mod root)
    /// </summary>
    public const string GraphicsRoot = "Graphics";
    
    /// <summary>
    /// Path to Sprites.xml
    /// </summary>
    public const string SpritesXmlPath = "Graphics/Sprites.xml";
    
    /// <summary>
    /// Path to SpritesGui.xml
    /// </summary>
    public const string SpritesGuiXmlPath = "Graphics/SpritesGui.xml";
    
    /// <summary>
    /// Path to Portraits.xml
    /// </summary>
    public const string PortraitsXmlPath = "Graphics/Portraits.xml";
    
    /// <summary>
    /// Path to KirbySprites.xml
    /// </summary>
    public const string KirbySpritesXmlPath = "Graphics/KirbySprites.xml";
    
    /// <summary>
    /// Path to KirbyActorSprites.xml
    /// </summary>
    public const string KirbyActorSpritesXmlPath = "Graphics/KirbyActorSprites.xml";
    
    /// <summary>
    /// Path to AnimatedTiles.xml
    /// </summary>
    public const string AnimatedTilesXmlPath = "Graphics/AnimatedTiles.xml";
    
    /// <summary>
    /// Path to BackgroundTiles.xml
    /// </summary>
    public const string BackgroundTilesXmlPath = "Graphics/BackgroundTiles.xml";
    
    /// <summary>
    /// Path to ForegroundTiles.xml
    /// </summary>
    public const string ForegroundTilesXmlPath = "Graphics/ForegroundTiles.xml";
    
    #endregion
    
    #region Atlas Paths
    
    /// <summary>
    /// Base path for Gameplay atlas textures
    /// </summary>
    public const string GameplayAtlasPath = "Graphics/Atlases/Gameplay";
    
    /// <summary>
    /// Base path for GUI atlas textures
    /// </summary>
    public const string GuiAtlasPath = "Graphics/Atlases/Gui";
    
    /// <summary>
    /// Base path for Portraits atlas textures
    /// </summary>
    public const string PortraitsAtlasPath = "Graphics/Atlases/Portraits";
    
    /// <summary>
    /// Base path for Misc atlas textures
    /// </summary>
    public const string MiscAtlasPath = "Graphics/Atlases/Misc";
    
    /// <summary>
    /// Base path for Overworld atlas textures
    /// </summary>
    public const string OverworldAtlasPath = "Graphics/Atlases/Overworld";
    
    /// <summary>
    /// Base path for Mountain atlas textures
    /// </summary>
    public const string MountainAtlasPath = "Graphics/Atlases/Mountain";
    
    /// <summary>
    /// Base path for Effects folder
    /// </summary>
    public const string EffectsPath = "Graphics/Effects";
    
    /// <summary>
    /// Base path for ColorGrading folder
    /// </summary>
    public const string ColorGradingPath = "Graphics/ColorGrading";
    
    /// <summary>
    /// Base path for BossesHelper folder
    /// </summary>
    public const string BossesHelperPath = "Graphics/BossesHelper";
    
    #endregion
    
    #region Path Helpers
    
    /// <summary>
    /// Builds a path to a gameplay texture
    /// </summary>
    /// <param name="relativePath">Path relative to Gameplay atlas (e.g., "objects/myentity/sprite")</param>
    public static string GetGameplayPath(string relativePath)
    {
        return $"{GameplayAtlasPath}/{relativePath}";
    }
    
    /// <summary>
    /// Builds a path to a GUI texture
    /// </summary>
    /// <param name="relativePath">Path relative to GUI atlas</param>
    public static string GetGuiPath(string relativePath)
    {
        return $"{GuiAtlasPath}/{relativePath}";
    }
    
    /// <summary>
    /// Builds a path to a portrait texture
    /// </summary>
    /// <param name="relativePath">Path relative to Portraits atlas</param>
    public static string GetPortraitPath(string relativePath)
    {
        return $"{PortraitsAtlasPath}/{relativePath}";
    }
    
    /// <summary>
    /// Builds a path to an effect shader
    /// </summary>
    /// <param name="effectName">Name of the effect file (without extension)</param>
    public static string GetEffectPath(string effectName)
    {
        return $"{EffectsPath}/{effectName}";
    }
    
    /// <summary>
    /// Builds a path to a color grading texture
    /// </summary>
    /// <param name="gradingName">Name of the color grading file</param>
    public static string GetColorGradingPath(string gradingName)
    {
        return $"{ColorGradingPath}/{gradingName}";
    }
    
    #endregion

    /// <summary>
    /// Gets a texture from the Gameplay atlas
    /// </summary>
    public static MTexture GetTexture(string path)
    {
        return GFX.Game[path];
    }
    
    /// <summary>
    /// Gets a texture from the Gameplay atlas, or null if not found
    /// </summary>
    public static MTexture? GetTextureOrNull(string path)
    {
        return GFX.Game.Has(path) ? GFX.Game[path] : null;
    }
    
    /// <summary>
    /// Checks if a texture exists in the Gameplay atlas
    /// </summary>
    public static bool TextureExists(string path)
    {
        return GFX.Game.Has(path);
    }
    
    /// <summary>
    /// Gets all textures matching a path prefix
    /// </summary>
    public static List<MTexture> GetTextures(string pathPrefix)
    {
        return GFX.Game.GetAtlasSubtextures(pathPrefix);
    }
    
    /// <summary>
    /// Gets a texture from the GUI atlas
    /// </summary>
    public static MTexture GetGuiTexture(string path)
    {
        return GFX.Gui[path];
    }
    
    /// <summary>
    /// Checks if a texture exists in the GUI atlas
    /// </summary>
    public static bool GuiTextureExists(string path)
    {
        return GFX.Gui.Has(path);
    }
    
    /// <summary>
    /// Gets a texture from the Portraits atlas
    /// </summary>
    public static MTexture GetPortraitTexture(string path)
    {
        return GFX.Portraits[path];
    }
    
    /// <summary>
    /// Creates a subtexture from a texture
    /// </summary>
    public static MTexture GetSubtexture(MTexture texture, int x, int y, int width, int height)
    {
        return texture.GetSubtexture(x, y, width, height);
    }
    
    /// <summary>
    /// Creates a tiled sprite from a texture
    /// </summary>
    public static Sprite CreateSprite(string spriteBankId)
    {
        return GFX.SpriteBank.Create(spriteBankId);
    }
    
    /// <summary>
    /// Creates a sprite from Sprites.xml
    /// </summary>
    public static Sprite? CreateSpriteIfExists(string spriteBankId)
    {
        try
        {
            return GFX.SpriteBank.Create(spriteBankId);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Tries to get an overworld texture by path
    /// </summary>
    public static MTexture? TryGetOverworldTexture(string path)
    {
        try
        {
            if (GFX.Gui.Has(path))
            {
                return GFX.Gui[path];
            }
            if (GFX.Game.Has(path))
            {
                return GFX.Game[path];
            }
        }
        catch
        {
            // Fall through to return null
        }
        return null;
    }
}
