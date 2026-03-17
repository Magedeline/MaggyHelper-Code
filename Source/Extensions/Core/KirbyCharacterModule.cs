using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MaggyHelper.Extensions.Kirby;
using MonoMod.Utils;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Kirby-specific character module implementing the full Kirby gameplay system.
    /// Integrates with the player extension core to provide Kirby mode.
    /// Manages the KirbyPlayerExtension entity which provides modular abilities:
    ///   Inhale, Hover, Spit, Melee, Range, and Copy Ability.
    /// </summary>
    public class KirbyCharacterModule : CharacterModuleBase
    {
        public override string CharacterId => "kirby";
        public override string CharacterName => "Kirby";

        // Constants
        private const string SFX_PATH = "event:/desolozantas/char/kirby/";
        private const string SFX_TRANSFORM = SFX_PATH + "transform";
        private const string SFX_LETS_GO = SFX_PATH + "lets_go";
        private const string SFX_HURT = SFX_PATH + "hurt";
        private const string SFX_DIE = SFX_PATH + "die";

        protected override void OnInitialize()
        {
            // Add Kirby-specific hooks
            On.Celeste.Player.Die += Player_Die;
            On.Celeste.Player.OnCollideH += Player_OnCollideH;
            On.Celeste.Player.OnCollideV += Player_OnCollideV;
        }

        protected override void OnUninitialize()
        {
            // Remove Kirby-specific hooks
            On.Celeste.Player.Die -= Player_Die;
            On.Celeste.Player.OnCollideH -= Player_OnCollideH;
            On.Celeste.Player.OnCollideV -= Player_OnCollideV;
        }

        protected override void OnEnable(Player player, Level level)
        {
            // Play transformation effect
            Audio.Play(SFX_TRANSFORM, player.Position);
            level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 20, player.Position, Vector2.One * 16f);
            
            // Spawn or activate KirbyPlayerExtension (new modular system)
            SpawnKirbyPlayerExtension(player, level);
            
            // Legacy: also spawn KirbyMode for backward compat with entities
            SpawnKirbyExtension(player, level);
            
            // Set inventory if available
            var state = LevelStateManager.GetState();
            if (state != null)
            {
                state.KirbyModeEnabled = true;
            }
        }

        protected override void OnDisable(Player player, Level level)
        {
            // Play revert effect
            Audio.Play("event:/game/general/seed_touch", player.Position);
            level.ParticlesFG?.Emit(ParticleTypes.Dust, 10, player.Position, Vector2.One * 12f);
            
            // Deactivate new extension
            DeactivateKirbyPlayerExtension(level);
            
            // Legacy compat
            DeactivateKirbyExtension(level);
            
            // Update state
            var state = LevelStateManager.GetState();
            if (state != null)
            {
                state.KirbyModeEnabled = false;
            }
        }

        protected override void OnLevelLoaded(Level level)
        {
            // Check if Kirby mode should be active
            var state = LevelStateManager.GetState();
            if (state != null && state.KirbyModeEnabled)
            {
                var player = level.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    SpawnKirbyPlayerExtension(player, level);
                    SpawnKirbyExtension(player, level);
                }
            }
        }

        protected override void OnLevelUnloaded(Level level)
        {
            // Save Kirby state before unload (prefer new extension)
            var kirbyExt = GetKirbyPlayerExtension(level);
            if (kirbyExt != null)
            {
                kirbyExt.SaveToSession();
            }
            else
            {
                var kirby = GetKirbyExtension(level);
                kirby?.SaveToSession();
            }
        }

        #region Hooks

        private PlayerDeadBody Player_Die(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
        {
            if (!IsActive(self))
            {
                return orig(self, direction, evenIfInvincible, registerDeathInStats);
            }
            
            // Prefer new extension first
            var kirbyExt = GetKirbyPlayerExtension(self.Scene);
            if (kirbyExt != null && kirbyExt.CurrentHealth > 1)
            {
                kirbyExt.TakeDamage(1);
                self.Position -= direction * 4f;
                Audio.Play(SFX_HURT, self.Position);
                return null;
            }
            
            // Fallback to legacy KirbyMode
            var kirby = GetKirbyExtension(self.Scene);
            if (kirby != null && kirby.CurrentHealth > 1)
            {
                kirby.TakeDamage(1);
                self.Position -= direction * 4f;
                Audio.Play(SFX_HURT, self.Position);
                return null;
            }
            
            // No health left, actually die
            Audio.Play(SFX_DIE, self.Position);
            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        private void Player_OnCollideH(On.Celeste.Player.orig_OnCollideH orig, Player self, CollisionData data)
        {
            orig(self, data);
            
            if (!IsActive(self)) return;
            
            // Check for Kirby slide collision damage
            if (self.StateMachine.State == PlayerCharacterStates.StKirbySlide)
            {
                // Check for enemy collision (by type name since Celeste doesn't have a base Enemy class)
                if (data.Hit?.GetType().Name.Contains("Enemy") == true || 
                    data.Hit?.GetType().Name.Contains("Seeker") == true)
                {
                    // Deal damage to enemy on slide collision
                    // This would need to be integrated with your enemy system
                }
            }
        }

        private void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV orig, Player self, CollisionData data)
        {
            orig(self, data);
            
            if (!IsActive(self)) return;
            
            // Handle Kirby bounce on enemies
            // Note: CollisionData.Hit is a Platform, not an Actor
            // We need to check the scene for bounceable actors at the collision point
            if (self.Speed.Y > 0 && self.Scene != null)
            {
                var level = self.SceneAs<Level>();
                if (level != null)
                {
                    // Check for bounceable actors at player's feet
                    var bounceCheck = self.CollideFirst<Actor>(self.Position + Vector2.UnitY);
                    if (bounceCheck != null && bounceCheck.IsBounceable())
                    {
                        // Bounce off enemy
                        self.Speed.Y = -120f;
                        Audio.Play("event:/desolozantas/char/kirby/bounce", self.Position);
                    }
                }
            }
        }

        #endregion

        #region Helper Methods — New KirbyPlayerExtension

        private void SpawnKirbyPlayerExtension(Player player, Level level)
        {
            var existing = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (existing == null)
            {
                var ext = new KirbyPlayerExtension(player.Position);
                level.Add(ext);
                ext.EnablePlayerSync();
                IngesteLogger.Debug("KirbyCharacterModule: Spawned new KirbyPlayerExtension");
            }
            else
            {
                existing.Active = true;
                existing.Visible = true;
                existing.Position = player.Position;
                existing.EnablePlayerSync();
                IngesteLogger.Debug("KirbyCharacterModule: Reactivated existing KirbyPlayerExtension");
            }
        }

        private KirbyPlayerExtension GetKirbyPlayerExtension(Level level)
        {
            return level?.Tracker.GetEntity<KirbyPlayerExtension>();
        }

        private KirbyPlayerExtension GetKirbyPlayerExtension(Scene scene)
        {
            return (scene as Level)?.Tracker.GetEntity<KirbyPlayerExtension>();
        }

        private void DeactivateKirbyPlayerExtension(Level level)
        {
            var ext = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (ext != null)
            {
                ext.DisablePlayerSync();
                ext.SaveToSession();
                ext.Active = false;
                ext.Visible = false;
                IngesteLogger.Debug("KirbyCharacterModule: Deactivated KirbyPlayerExtension");
            }
        }

        #endregion

        #region Helper Methods — Legacy KirbyMode (backward compat)

        private void SpawnKirbyExtension(Player player, Level level)
        {
            var existingKirby = level.Tracker.GetEntity<KirbyMode>();
            if (existingKirby == null)
            {
                var kirby = new KirbyMode(player.Position);
                level.Add(kirby);
                kirby.EnablePlayerSync();
            }
            else
            {
                existingKirby.Active = true;
                existingKirby.Visible = true;
                existingKirby.Position = player.Position;
                existingKirby.EnablePlayerSync();
            }
        }

        private KirbyMode GetKirbyExtension(Level level)
        {
            return level?.Tracker.GetEntity<KirbyMode>();
        }

        private new KirbyMode GetKirbyExtension(Scene scene)
        {
            return (scene as Level)?.Tracker.GetEntity<KirbyMode>();
        }

        private void DeactivateKirbyExtension(Level level)
        {
            var kirby = level.Tracker.GetEntity<KirbyMode>();
            if (kirby != null)
            {
                kirby.DisablePlayerSync();
                kirby.SaveToSession();
                kirby.Active = false;
                kirby.Visible = false;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the current Kirby power state
        /// </summary>
        public KirbyMode.KirbyPowerState GetPowerState(Level level)
        {
            var ext = GetKirbyPlayerExtension(level);
            if (ext != null) return ext.CurrentPower;

            var kirby = level?.Tracker.GetEntity<KirbyMode>();
            return kirby?.CurrentPower ?? KirbyMode.KirbyPowerState.None;
        }

        /// <summary>
        /// Set the Kirby power state
        /// </summary>
        public void SetPowerState(Level level, KirbyMode.KirbyPowerState power)
        {
            var ext = GetKirbyPlayerExtension(level);
            ext?.SetPowerState(power);

            var kirby = level?.Tracker.GetEntity<KirbyMode>();
            kirby?.SetPowerState(power);
            
            var state = LevelStateManager.GetState();
            if (state != null)
            {
                state.KirbyPower = power;
            }
        }

        /// <summary>
        /// Get Kirby's current health
        /// </summary>
        public int GetHealth(Level level)
        {
            var ext = GetKirbyPlayerExtension(level);
            if (ext != null) return ext.CurrentHealth;

            var kirby = level?.Tracker.GetEntity<KirbyMode>();
            return kirby?.CurrentHealth ?? 0;
        }

        /// <summary>
        /// Heal Kirby
        /// </summary>
        public void Heal(Level level, int amount = 1)
        {
            var ext = GetKirbyPlayerExtension(level);
            if (ext != null) { ext.Heal(amount); return; }

            var kirby = level?.Tracker.GetEntity<KirbyMode>();
            kirby?.Heal(amount);
        }

        /// <summary>
        /// Damage Kirby
        /// </summary>
        public void Damage(Level level, int amount = 1)
        {
            var ext = GetKirbyPlayerExtension(level);
            if (ext != null) { ext.TakeDamage(amount); return; }

            var kirby = level?.Tracker.GetEntity<KirbyMode>();
            kirby?.TakeDamage(amount);
        }

        #endregion
    }
}
