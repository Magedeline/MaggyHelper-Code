using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Extensions for Spring entities providing custom bounce behaviors.
    /// </summary>
    public static class SpringExtensionsCore
    {
        public static void Initialize()
        {
            IngesteLogger.Debug("SpringExtensionsCore: Initializing...");
            
            On.Celeste.Spring.ctor_Vector2_Orientations_bool += Spring_Construct;
            
            IngesteLogger.Debug("SpringExtensionsCore: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("SpringExtensionsCore: Uninitializing...");
            
            On.Celeste.Spring.ctor_Vector2_Orientations_bool -= Spring_Construct;
            
            IngesteLogger.Debug("SpringExtensionsCore: Uninitialized");
        }

        private static void Spring_Construct(On.Celeste.Spring.orig_ctor_Vector2_Orientations_bool orig, Spring self, Vector2 position, Spring.Orientations orientation, bool playerCanUse)
        {
            orig(self, position, orientation, playerCanUse);
            
            // Make spring hookable
            self.SetHookable(true);
            
            // Add custom character interaction component
            self.Add(new CharacterInteractable(self.OnCharacterInteract));
        }

        private static void OnCharacterInteract(this Spring self, Entity character, Vector2 at)
        {
            if (character is KirbyPlayerExtension)
            {
                // Trigger spring animation
                Audio.Play("event:/game/general/spring", self.Position);
                return;
            }

            if (character is KirbyMode legacy)
            {
                // Trigger spring animation
                Audio.Play("event:/game/general/spring", self.Position);
            }
        }

        /// <summary>
        /// Get the direction this spring bounces entities
        /// </summary>
        public static Vector2 GetBounceDirection(this Spring self)
        {
            return self.Orientation switch
            {
                Spring.Orientations.Floor => -Vector2.UnitY,
                Spring.Orientations.WallLeft => Vector2.UnitX,
                Spring.Orientations.WallRight => -Vector2.UnitX,
                _ => -Vector2.UnitY
            };
        }

        /// <summary>
        /// Get the bounce speed for this spring
        /// </summary>
        public static float GetBounceSpeed(this Spring self)
        {
            // Standard spring bounce speed
            return 240f;
        }
    }

    /// <summary>
    /// Component for handling custom character interactions with entities
    /// </summary>
    public class CharacterInteractable : Component
    {
        public delegate void InteractCallback(Entity character, Vector2 at);
        
        private InteractCallback _callback;
        private float _cooldown;
        private float _cooldownTimer;

        public CharacterInteractable(InteractCallback callback, float cooldown = 0.1f) 
            : base(true, false)
        {
            _callback = callback;
            _cooldown = cooldown;
            _cooldownTimer = 0f;
        }

        public override void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Engine.DeltaTime;
            }
        }

        public bool TryInteract(Entity character, Vector2 at)
        {
            if (_cooldownTimer <= 0f && _callback != null)
            {
                _callback(character, at);
                _cooldownTimer = _cooldown;
                return true;
            }
            return false;
        }
    }
}
