using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MaggyHelper.Extensions.Kirby;
using MonoMod.Utils;
using JumpThru = Celeste.JumpThru; // Use Celeste.JumpThru for MMHOOK hooks

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for Platform entities.
    /// </summary>
    public static class PlatformExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("PlatformExtensionsCore: Initializing...");
            
            On.Celeste.Platform.Update += Platform_Update;
            On.Celeste.Platform.MoveH_float += Platform_MoveH;
            On.Celeste.Platform.MoveV_float += Platform_MoveV;
            
            IngesteLogger.Debug("PlatformExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("PlatformExtensionsCore: Uninitializing...");
            
            On.Celeste.Platform.Update -= Platform_Update;
            On.Celeste.Platform.MoveH_float -= Platform_MoveH;
            On.Celeste.Platform.MoveV_float -= Platform_MoveV;
            
            IngesteLogger.Debug("PlatformExtensionsCore: Uninitialized");
        }

        private static void Platform_Update(On.Celeste.Platform.orig_Update orig, Celeste.Platform self)
        {
            // Store previous position before update
            var data = DynamicData.For(self);
            data.Set("prev_position", self.Position);
            
            // Ensure platform is marked as hookable by default
            if (!data.TryGet<bool>("hookable", out _))
            {
                data.Set("hookable", true);
            }
            
            orig(self);
        }

        private static void Platform_MoveH(On.Celeste.Platform.orig_MoveH_float orig, Celeste.Platform self, float moveH)
        {
            Vector2 oldPos = self.Position;
            Vector2 oldExactPos = self.ExactPosition;
            
            orig(self, moveH);
            
            Vector2 newExactPos = self.ExactPosition;
            Vector2 exactMovement = newExactPos - oldExactPos;
            Vector2 pixels = self.Position - oldPos;
            
            // Move any attached characters
            self.MoveAttachedCharacters(exactMovement, pixels);
        }

        private static void Platform_MoveV(On.Celeste.Platform.orig_MoveV_float orig, Celeste.Platform self, float moveV)
        {
            Vector2 oldPos = self.Position;
            Vector2 oldExactPos = self.ExactPosition;
            
            orig(self, moveV);
            
            Vector2 newExactPos = self.ExactPosition;
            Vector2 exactMovement = newExactPos - oldExactPos;
            Vector2 pixels = self.Position - oldPos;
            
            // Move any attached characters
            self.MoveAttachedCharacters(exactMovement, pixels);
        }

        #region Extension Methods

        /// <summary>
        /// Move any attached custom characters with the platform
        /// </summary>
        public static void MoveAttachedCharacters(this Celeste.Platform self, Vector2 exactMovement, Vector2 pixels)
        {
            if (self.Scene == null || exactMovement == Vector2.Zero) return;

            // Legacy compatibility: move compatibility entities only.
            // The new KirbyPlayerExtension mirrors the vanilla Player and does not need
            // manual carry motion because vanilla platform movement already handles Player.
            var legacy = self.Scene.Tracker.GetEntity<KirbyMode>();
            if (legacy != null && legacy.Active)
            {
                bool isRiding = false;
                if (self is Solid solid)
                {
                    isRiding = legacy.IsRiding(solid);
                }
                else if (self is JumpThru jumpThru)
                {
                    isRiding = legacy.IsRiding(jumpThru);
                }

                if (isRiding)
                {
                    legacy.Position += pixels;
                }
            }

            var shim = self.Scene.Tracker.GetEntity<KirbyPlayer>();
            if (shim != null && shim.Active)
            {
                bool isRiding = false;
                if (self is Solid solid)
                {
                    isRiding = shim.IsRiding(solid);
                }
                else if (self is JumpThru jumpThru)
                {
                    isRiding = shim.IsRiding(jumpThru);
                }

                if (isRiding)
                {
                    shim.Position += pixels;
                }
            }
            
            // Add other character extensions as needed
        }

        #endregion
    }
}

