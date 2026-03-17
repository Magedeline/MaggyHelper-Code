using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.Utils;
using JumpThru = Celeste.JumpThru; // Celeste JumpThru for hooks and extensions

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for JumpThru (pass-through platforms) entities.
    /// </summary>
    public static class JumpThruExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("JumpThruExtensionsCore: Initializing...");
            
            On.Celeste.JumpThru.ctor += JumpThru_Construct;
            On.Celeste.JumpThru.HasPlayerRider += JumpThru_HasPlayerRider;
            
            IngesteLogger.Debug("JumpThruExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("JumpThruExtensionsCore: Uninitializing...");
            
            On.Celeste.JumpThru.ctor -= JumpThru_Construct;
            On.Celeste.JumpThru.HasPlayerRider -= JumpThru_HasPlayerRider;
            
            IngesteLogger.Debug("JumpThruExtensionsCore: Uninitialized");
        }

        private static void JumpThru_Construct(On.Celeste.JumpThru.orig_ctor orig, JumpThru self, Vector2 position, int width, bool safe)
        {
            orig(self, position, width, safe);
            self.SetHookable(true);
        }

        private static bool JumpThru_HasPlayerRider(On.Celeste.JumpThru.orig_HasPlayerRider orig, JumpThru self)
        {
            if (orig(self))
                return true;
            
            // Also check for hook attachment
            if (self.IsHookAttached())
                return true;
            
            // Check for custom character riders
            if (self.HasKirbyRider())
                return true;
            
            return false;
        }

        #region Extension Methods

        /// <summary>
        /// Check if a KirbyPlayerExtension is riding this jumpthru
        /// </summary>
        public static bool HasKirbyRider(this JumpThru self)
        {
            if (self.Scene == null) return false;
            
            var kirby = self.Scene.Tracker.GetEntity<KirbyPlayer>();
            if (kirby != null && kirby.Active)
            {
                return kirby.IsRiding(self);
            }
            
            return false;
        }

        #endregion
    }
}
