using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.Utils;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Core entity extensions providing common functionality for all entities.
    /// Similar to Aqua's EntityExtensions, provides hookable tracking, previous position,
    /// and unique ID generation.
    /// </summary>
    public static class EntityExtensionsCore
    {
        private static ulong _nextUniqueId = 1;

        public static void Initialize()
        {
            IngesteLogger.Debug("EntityExtensionsCore: Initializing...");
            
            On.Monocle.Entity.ctor_Vector2 += Entity_Construct;
            On.Monocle.Entity.Update += Entity_Update;
            
            IngesteLogger.Debug("EntityExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("EntityExtensionsCore: Uninitializing...");
            
            On.Monocle.Entity.ctor_Vector2 -= Entity_Construct;
            On.Monocle.Entity.Update -= Entity_Update;
            
            IngesteLogger.Debug("EntityExtensionsCore: Uninitialized");
        }

        private static void Entity_Construct(On.Monocle.Entity.orig_ctor_Vector2 orig, Entity self, Vector2 position)
        {
            orig(self, position);
            
            var data = DynamicData.For(self);
            data.Set("unique_id", _nextUniqueId++);
            data.Set("hookable", false);
            data.Set("hook_attached", false);
            data.Set("prev_position", position);
            data.Set("character_interactable", true);
        }

        private static void Entity_Update(On.Monocle.Entity.orig_Update orig, Entity self)
        {
            // Store previous position before update
            DynamicData.For(self).Set("prev_position", self.Position);
            orig(self);
        }

        #region Extension Methods

        /// <summary>
        /// Get a unique ID for this entity (session-unique, not persistent)
        /// </summary>
        public static ulong GetUniqueID(this Entity self)
        {
            return DynamicData.For(self).Get<ulong>("unique_id");
        }

        /// <summary>
        /// Get the entity's position from the previous frame
        /// </summary>
        public static Vector2 GetPreviousPosition(this Entity self)
        {
            return DynamicData.For(self).Get<Vector2>("prev_position");
        }

        /// <summary>
        /// Check if this entity can be hooked/grappled
        /// </summary>
        public static bool IsHookable(this Entity self)
        {
            return DynamicData.For(self).Get<bool>("hookable");
        }

        /// <summary>
        /// Set whether this entity can be hooked/grappled
        /// </summary>
        public static void SetHookable(this Entity self, bool hookable)
        {
            DynamicData.For(self).Set("hookable", hookable);
        }

        /// <summary>
        /// Check if a hook is currently attached to this entity
        /// </summary>
        public static bool IsHookAttached(this Entity self)
        {
            return DynamicData.For(self).Get<bool>("hook_attached");
        }

        /// <summary>
        /// Set whether a hook is attached to this entity
        /// </summary>
        public static void SetHookAttached(this Entity self, bool attached)
        {
            DynamicData.For(self).Set("hook_attached", attached);
        }

        /// <summary>
        /// Check if characters can interact with this entity
        /// </summary>
        public static bool IsCharacterInteractable(this Entity self)
        {
            return DynamicData.For(self).Get<bool>("character_interactable");
        }

        /// <summary>
        /// Set whether characters can interact with this entity
        /// </summary>
        public static void SetCharacterInteractable(this Entity self, bool interactable)
        {
            DynamicData.For(self).Set("character_interactable", interactable);
        }

        /// <summary>
        /// Calculate velocity from position change
        /// </summary>
        public static Vector2 GetVelocity(this Entity self)
        {
            Vector2 prevPos = self.GetPreviousPosition();
            return (self.Position - prevPos) / Engine.DeltaTime;
        }

        /// <summary>
        /// Check if entity moved this frame
        /// </summary>
        public static bool HasMoved(this Entity self)
        {
            return self.Position != self.GetPreviousPosition();
        }

        #endregion
    }
}
