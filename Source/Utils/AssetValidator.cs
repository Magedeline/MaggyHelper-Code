using System;
using System.Collections.Generic;
using Monocle;
using Microsoft.Xna.Framework.Graphics;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Validates that required assets (sprites, audio, dialog) exist before attempting to use them.
    /// Provides fallback mechanisms when assets are missing to prevent crashes.
    /// </summary>
    public static class AssetValidator
    {
        private static readonly HashSet<string> MissingAssets = new HashSet<string>();
        private static readonly HashSet<string> ValidatedAssets = new HashSet<string>();

        /// <summary>
        /// Checks if a sprite atlas XML exists
        /// </summary>
        public static bool ValidateSpriteAtlas(string atlasPath)
        {
            if (ValidatedAssets.Contains(atlasPath))
                return true;

            if (MissingAssets.Contains(atlasPath))
                return false;

            try
            {
                var atlas = GFX.Game;
                if (atlas == null)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", $"GFX.Game atlas is null when validating {atlasPath}");
                    return false;
                }

                // Try to check if the sprite exists
                if (atlas.Has(atlasPath))
                {
                    ValidatedAssets.Add(atlasPath);
                    return true;
                }

                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Sprite atlas missing: {atlasPath}");
                MissingAssets.Add(atlasPath);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error validating sprite atlas {atlasPath}: {ex.Message}");
                MissingAssets.Add(atlasPath);
                return false;
            }
        }

        /// <summary>
        /// Safely gets a texture or returns a fallback
        /// </summary>
        public static MTexture GetTextureSafe(string path, MTexture fallback = null)
        {
            try
            {
                if (GFX.Game == null)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", "GFX.Game is null");
                    return fallback;
                }

                if (GFX.Game.Has(path))
                {
                    return GFX.Game[path];
                }

                if (!MissingAssets.Contains(path))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", $"Texture not found: {path}");
                    MissingAssets.Add(path);
                }

                return fallback;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error loading texture {path}: {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// Safely gets a sprite or returns null
        /// </summary>
        public static Sprite GetSpriteSafe(string spriteId, string defaultAnimation = null)
        {
            try
            {
                if (!ValidateSpriteAtlas(spriteId))
                {
                    return null;
                }

                var sprite = new Sprite(GFX.Game, spriteId);
                
                if (!string.IsNullOrEmpty(defaultAnimation))
                {
                    try
                    {
                        sprite.Play(defaultAnimation);
                    }
                    catch
                    {
                        Logger.Log(LogLevel.Warn, "MaggyHelper", 
                            $"Animation '{defaultAnimation}' not found in sprite '{spriteId}'");
                    }
                }

                return sprite;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error creating sprite {spriteId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates that an audio event exists
        /// </summary>
        public static bool ValidateAudioEvent(string audioPath)
        {
            if (string.IsNullOrEmpty(audioPath))
                return false;

            if (ValidatedAssets.Contains(audioPath))
                return true;

            if (MissingAssets.Contains(audioPath))
                return false;

            try
            {
                // Try to check if the audio bank has this event
                // Note: FMOD doesn't provide an easy way to check without trying to play
                // So we'll just validate the format for now
                if (audioPath.StartsWith("event:/"))
                {
                    ValidatedAssets.Add(audioPath);
                    return true;
                }

                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Invalid audio event format: {audioPath}");
                MissingAssets.Add(audioPath);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error validating audio {audioPath}: {ex.Message}");
                MissingAssets.Add(audioPath);
                return false;
            }
        }

        /// <summary>
        /// Safely plays an audio event, returns whether it succeeded
        /// </summary>
        public static bool PlayAudioSafe(string audioPath)
        {
            try
            {
                if (!ValidateAudioEvent(audioPath))
                    return false;

                Audio.Play(audioPath);
                return true;
            }
            catch (Exception ex)
            {
                if (!MissingAssets.Contains(audioPath))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to play audio {audioPath}: {ex.Message}");
                    MissingAssets.Add(audioPath);
                }
                return false;
            }
        }

        /// <summary>
        /// Validates that a dialog key exists
        /// </summary>
        public static bool ValidateDialogKey(string dialogKey)
        {
            if (string.IsNullOrEmpty(dialogKey))
                return false;

            if (ValidatedAssets.Contains(dialogKey))
                return true;

            if (MissingAssets.Contains(dialogKey))
                return false;

            try
            {
                if (Dialog.Has(dialogKey))
                {
                    ValidatedAssets.Add(dialogKey);
                    return true;
                }

                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Dialog key missing: {dialogKey}");
                MissingAssets.Add(dialogKey);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error validating dialog key {dialogKey}: {ex.Message}");
                MissingAssets.Add(dialogKey);
                return false;
            }
        }

        /// <summary>
        /// Safely gets dialog text or returns a fallback
        /// </summary>
        public static string GetDialogSafe(string dialogKey, string fallback = null)
        {
            try
            {
                if (ValidateDialogKey(dialogKey))
                {
                    return Dialog.Get(dialogKey);
                }

                return fallback ?? $"[Missing: {dialogKey}]";
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error getting dialog {dialogKey}: {ex.Message}");
                return fallback ?? $"[Error: {dialogKey}]";
            }
        }

        /// <summary>
        /// Clears the asset validation cache
        /// </summary>
        public static void ClearCache()
        {
            ValidatedAssets.Clear();
            MissingAssets.Clear();
            Logger.Log(LogLevel.Info, "MaggyHelper", "Asset validation cache cleared");
        }

        /// <summary>
        /// Logs a summary of missing assets
        /// </summary>
        public static void LogMissingSummary()
        {
            if (MissingAssets.Count > 0)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", 
                    $"Total missing assets: {MissingAssets.Count}");
                
                foreach (var asset in MissingAssets)
                {
                    Logger.Log(LogLevel.Debug, "MaggyHelper", $"  - {asset}");
                }
            }
            else
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "No missing assets detected");
            }
        }
    }
}
