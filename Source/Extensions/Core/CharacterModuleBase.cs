using System;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Base class for character-specific modules.
    /// Provides common functionality that all character modules share.
    /// </summary>
    public abstract class CharacterModuleBase : ICharacterModule
    {
        public abstract string CharacterId { get; }
        public abstract string CharacterName { get; }
        
        protected bool _initialized = false;

        public virtual void Initialize()
        {
            if (_initialized) return;
            
            IngesteLogger.Debug($"CharacterModule [{CharacterName}]: Initializing...");
            
            OnInitialize();
            
            _initialized = true;
            IngesteLogger.Debug($"CharacterModule [{CharacterName}]: Initialized");
        }

        public virtual void Uninitialize()
        {
            if (!_initialized) return;
            
            IngesteLogger.Debug($"CharacterModule [{CharacterName}]: Uninitializing...");
            
            OnUninitialize();
            
            _initialized = false;
            IngesteLogger.Debug($"CharacterModule [{CharacterName}]: Uninitialized");
        }

        public virtual bool IsActive(Player player)
        {
            if (player?.Scene is Level level)
            {
                return level.Session.GetFlag($"{CharacterId}_mode");
            }
            return false;
        }

        public virtual void Enable(Player player)
        {
            if (player?.Scene is Level level)
            {
                level.Session.SetFlag($"{CharacterId}_mode", true);
                LevelStateManager.SetActiveCharacter(CharacterId, level);
                OnEnable(player, level);
                
                IngesteLogger.Info($"CharacterModule [{CharacterName}]: Enabled for player");
            }
        }

        public virtual void Disable(Player player)
        {
            if (player?.Scene is Level level)
            {
                level.Session.SetFlag($"{CharacterId}_mode", false);
                if (LevelStateManager.GetActiveCharacter() == CharacterId)
                {
                    LevelStateManager.SetActiveCharacter("", level);
                }
                OnDisable(player, level);
                
                IngesteLogger.Info($"CharacterModule [{CharacterName}]: Disabled for player");
            }
        }

        public virtual void OnLevelLoad(Level level)
        {
            OnLevelLoaded(level);
        }

        public virtual void OnLevelUnload(Level level)
        {
            OnLevelUnloaded(level);
        }

        #region Abstract/Virtual Methods for Override

        /// <summary>
        /// Called during initialization - override to add hooks
        /// </summary>
        protected abstract void OnInitialize();

        /// <summary>
        /// Called during uninitialization - override to remove hooks
        /// </summary>
        protected abstract void OnUninitialize();

        /// <summary>
        /// Called when this character mode is enabled for a player
        /// </summary>
        protected virtual void OnEnable(Player player, Level level)
        {
        }

        /// <summary>
        /// Called when this character mode is disabled for a player
        /// </summary>
        protected virtual void OnDisable(Player player, Level level)
        {
        }

        /// <summary>
        /// Called when a level is loaded
        /// </summary>
        protected virtual void OnLevelLoaded(Level level)
        {
        }

        /// <summary>
        /// Called when a level is unloaded
        /// </summary>
        protected virtual void OnLevelUnloaded(Level level)
        {
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get the player entity in the scene
        /// </summary>
        protected Player GetPlayer(Scene scene)
        {
            return scene?.Tracker.GetEntity<Player>();
        }

        /// <summary>
        /// Play a sound effect
        /// </summary>
        protected void PlaySound(string eventPath, Vector2 position)
        {
            Audio.Play(eventPath, position);
        }

        /// <summary>
        /// Spawn particles at position
        /// </summary>
        protected void SpawnParticles(Level level, ParticleType type, Vector2 position, int count = 10, Vector2? range = null)
        {
            Vector2 r = range ?? Vector2.One * 8f;
            level?.ParticlesFG?.Emit(type, count, position, r);
        }

        #endregion
    }
}
