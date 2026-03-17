using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.Utils;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for Solid entities providing custom platform behaviors.
    /// </summary>
    public static class SolidExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("SolidExtensionsCore: Initializing...");
            
            On.Celeste.Solid.Awake += Solid_Awake;
            On.Celeste.Solid.HasPlayerRider += Solid_HasPlayerRider;
            
            IngesteLogger.Debug("SolidExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("SolidExtensionsCore: Uninitializing...");
            
            On.Celeste.Solid.Awake -= Solid_Awake;
            On.Celeste.Solid.HasPlayerRider -= Solid_HasPlayerRider;
            
            IngesteLogger.Debug("SolidExtensionsCore: Uninitialized");
        }

        private static void Solid_Awake(On.Celeste.Solid.orig_Awake orig, Solid self, Scene scene)
        {
            orig(self, scene);
            
            // Make solid hookable by default
            self.SetHookable(true);
        }

        private static bool Solid_HasPlayerRider(On.Celeste.Solid.orig_HasPlayerRider orig, Solid self)
        {
            bool hasRider = orig(self);
            
            // Also consider hook attached as having a rider (for falling blocks, etc.)
            if (!hasRider && self.IsHookAttached())
            {
                return true;
            }
            
            return hasRider;
        }

        #region Extension Methods

        /// <summary>
        /// Check if a KirbyPlayerExtension is riding this solid
        /// </summary>
        public static bool HasKirbyRider(this Solid self)
        {
            if (self.Scene == null) return false;
            
            var kirby = self.Scene.Tracker.GetEntity<KirbyPlayer>();
            if (kirby != null && kirby.Active)
            {
                // Check if Kirby is on top of this solid
                return kirby.IsRiding(self);
            }
            
            return false;
        }

        /// <summary>
        /// Check if any custom character extension is riding this solid
        /// </summary>
        public static bool HasCharacterRider(this Solid self)
        {
            return self.HasPlayerRider() || self.HasKirbyRider();
        }

        #endregion
    }
}
