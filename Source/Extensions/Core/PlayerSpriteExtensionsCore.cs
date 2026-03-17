using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for PlayerSprite providing custom sprite handling for different characters.
    /// </summary>
    public static class PlayerSpriteExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("PlayerSpriteExtensionsCore: Initializing...");
            // Add hooks as needed
            IngesteLogger.Debug("PlayerSpriteExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("PlayerSpriteExtensionsCore: Uninitializing...");
            // Remove hooks
            IngesteLogger.Debug("PlayerSpriteExtensionsCore: Uninitialized");
        }

        #region Extension Methods

        /// <summary>
        /// Check if the sprite has a specific animation
        /// </summary>
        public static bool Has(this Sprite sprite, string animationId)
        {
            try
            {
                return sprite.Animations != null && sprite.Animations.ContainsKey(animationId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Play an animation if it exists, otherwise play fallback
        /// </summary>
        public static void PlaySafe(this Sprite sprite, string animationId, string fallback = null)
        {
            if (sprite.Has(animationId))
            {
                sprite.Play(animationId);
            }
            else if (fallback != null && sprite.Has(fallback))
            {
                sprite.Play(fallback);
            }
        }

        #endregion
    }
}
