using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.Utils;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for Actor entities providing character-like physics support.
    /// </summary>
    public static class ActorExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("ActorExtensionsCore: Initializing...");
            
            On.Celeste.Actor.ctor += Actor_Construct;
            On.Celeste.Actor.Update += Actor_Update;
            
            IngesteLogger.Debug("ActorExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("ActorExtensionsCore: Uninitializing...");
            
            On.Celeste.Actor.ctor -= Actor_Construct;
            On.Celeste.Actor.Update -= Actor_Update;
            
            IngesteLogger.Debug("ActorExtensionsCore: Uninitialized");
        }

        private static void Actor_Construct(On.Celeste.Actor.orig_ctor orig, Actor self, Vector2 position)
        {
            orig(self, position);
            
            var data = DynamicData.For(self);
            data.Set("mass", 1.0f);
            data.Set("stamina_cost", 0f);
            data.Set("hookable", true);
            data.Set("inhalable", true); // Can be inhaled by Kirby
            data.Set("bounceable", true); // Can be bounced on
        }

        private static void Actor_Update(On.Celeste.Actor.orig_Update orig, Actor self)
        {
            DynamicData.For(self).Set("prev_position", self.Position);
            orig(self);
        }

        #region Extension Methods

        /// <summary>
        /// Get the mass of this actor (affects physics interactions)
        /// </summary>
        public static float GetMass(this Actor self)
        {
            return DynamicData.For(self).Get<float>("mass");
        }

        /// <summary>
        /// Set the mass of this actor
        /// </summary>
        public static void SetMass(this Actor self, float mass)
        {
            DynamicData.For(self).Set("mass", mass);
        }

        /// <summary>
        /// Get the stamina cost to interact with this actor
        /// </summary>
        public static float GetStaminaCost(this Actor self)
        {
            return DynamicData.For(self).Get<float>("stamina_cost");
        }

        /// <summary>
        /// Set the stamina cost to interact with this actor
        /// </summary>
        public static void SetStaminaCost(this Actor self, float cost)
        {
            DynamicData.For(self).Set("stamina_cost", cost);
        }

        /// <summary>
        /// Check if this actor can be inhaled by Kirby
        /// </summary>
        public static bool IsInhalable(this Actor self)
        {
            return DynamicData.For(self).Get<bool>("inhalable");
        }

        /// <summary>
        /// Set whether this actor can be inhaled by Kirby
        /// </summary>
        public static void SetInhalable(this Actor self, bool inhalable)
        {
            DynamicData.For(self).Set("inhalable", inhalable);
        }

        /// <summary>
        /// Check if this actor can be bounced on
        /// </summary>
        public static bool IsBounceable(this Actor self)
        {
            return DynamicData.For(self).Get<bool>("bounceable");
        }

        /// <summary>
        /// Set whether this actor can be bounced on
        /// </summary>
        public static void SetBounceable(this Actor self, bool bounceable)
        {
            DynamicData.For(self).Set("bounceable", bounceable);
        }

        /// <summary>
        /// Calculate momentum exchange between two actors
        /// </summary>
        public static (Vector2 selfSpeed, Vector2 otherSpeed) CalculateMomentumExchange(
            this Actor self, Actor other, Vector2 selfSpeed, Vector2 otherSpeed)
        {
            float selfMass = self.GetMass();
            float otherMass = other.GetMass();
            float totalMass = selfMass + otherMass;
            
            if (totalMass <= 0f) return (selfSpeed, otherSpeed);
            
            // Simple elastic collision
            Vector2 newSelfSpeed = ((selfMass - otherMass) * selfSpeed + 2f * otherMass * otherSpeed) / totalMass;
            Vector2 newOtherSpeed = ((otherMass - selfMass) * otherSpeed + 2f * selfMass * selfSpeed) / totalMass;
            
            return (newSelfSpeed, newOtherSpeed);
        }

        /// <summary>
        /// Apply a bounce effect to this actor
        /// </summary>
        public static void ApplyBounce(this Actor self, Vector2 direction, float speed)
        {
            if (direction != Vector2.Zero)
            {
                direction.Normalize();
            }
            
            // Get speed via reflection or dynamic data if available
            var data = DynamicData.For(self);
            
            // Try to set speed if the actor has a Speed field
            try
            {
                var speedField = self.GetType().GetField("Speed");
                if (speedField != null)
                {
                    speedField.SetValue(self, direction * speed);
                }
                else
                {
                    // Store in dynamic data for actors that use different speed systems
                    data.Set("bounce_velocity", direction * speed);
                }
            }
            catch
            {
                data.Set("bounce_velocity", direction * speed);
            }
        }

        #endregion
    }
}
