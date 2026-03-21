using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.Utils;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Manages level-specific state for character extension systems.
    /// Similar to Aqua's LevelStates, tracks character settings per level.
    /// </summary>
    public static class LevelStateManager
    {
        /// <summary>
        /// Per-level state for character systems
        /// </summary>
        [Serializable]
        public class CharacterLevelState
        {
            // Kirby settings
            public bool KirbyModeEnabled { get; set; } = false;
            public KirbyMode.KirbyPowerState KirbyPower { get; set; } = KirbyMode.KirbyPowerState.None;
            public int KirbyHealth { get; set; } = 6;
            public int KirbyMaxHealth { get; set; } = 6;
            public float KirbyStamina { get; set; } = 100f;
            
            // Chara settings
            public bool CharaModeEnabled { get; set; } = false;
            public int CharaLV { get; set; } = 1;
            
            // Ralsei settings
            public bool RalseiModeEnabled { get; set; } = false;
            public float RalseiMagic { get; set; } = 100f;
            
            // Generic character settings
            public string ActiveCharacterId { get; set; } = PlayerCharacterIds.Madeline;
            public bool CustomPhysicsEnabled { get; set; } = true;
            public bool InfiniteDash { get; set; } = false;
            public bool InfiniteStamina { get; set; } = false;
            
            // Reset on transition settings
            public bool ResetHealthOnTransition { get; set; } = false;
            public bool ResetPowerOnTransition { get; set; } = false;
            
            public CharacterLevelState()
            {
            }
            
            public CharacterLevelState(AreaData areaData)
            {
                // Initialize from area data if needed
            }
            
            public void Reset()
            {
                KirbyModeEnabled = false;
                KirbyPower = KirbyMode.KirbyPowerState.None;
                KirbyHealth = KirbyMaxHealth;
                KirbyStamina = 100f;
                CharaModeEnabled = false;
                RalseiModeEnabled = false;
                ActiveCharacterId = PlayerCharacterIds.Madeline;
            }

            public PlayerCharacter GetActivePlayerCharacter()
            {
                return PlayerCharacter.FromId(ActiveCharacterId);
            }

            public void SetActivePlayerCharacter(PlayerCharacter character)
            {
                ActiveCharacterId = character.Id;
                KirbyModeEnabled = character.IsKirby;
                CharaModeEnabled = character.Id == "chara";
                RalseiModeEnabled = character.Id == "ralsei";
            }
        }

        private static CharacterLevelState _currentState;

        public static void Initialize()
        {
            IngesteLogger.Debug("LevelStateManager: Initializing...");
            
            On.Celeste.Level.LoadLevel += Level_LoadLevel;
            On.Celeste.Level.UnloadLevel += Level_UnloadLevel;
            On.Celeste.Level.TransitionRoutine += Level_TransitionRoutine;
            
            IngesteLogger.Debug("LevelStateManager: Initialized");
        }

        public static void Uninitialize()
        {
            IngesteLogger.Debug("LevelStateManager: Uninitializing...");
            
            On.Celeste.Level.LoadLevel -= Level_LoadLevel;
            On.Celeste.Level.UnloadLevel -= Level_UnloadLevel;
            On.Celeste.Level.TransitionRoutine -= Level_TransitionRoutine;
            
            _currentState = null;
            
            IngesteLogger.Debug("LevelStateManager: Uninitialized");
        }

        private static void Level_LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
        {
            orig(self, playerIntro, isFromLoader);
            
            // Get or create state
            if (_currentState == null)
            {
                AreaData areaData = AreaData.Get(self.Session.Area);
                _currentState = new CharacterLevelState(areaData);
            }
            
            // Restore from session if available
            RestoreFromSession(self);
            
            // Initialize any active character modules for this level
            foreach (var module in PlayerExtensionCore.Instance.GetCharacterModules())
            {
                try
                {
                    module.OnLevelLoad(self);
                }
                catch (Exception ex)
                {
                    IngesteLogger.Error($"Error loading character module: {ex}");
                }
            }

            EnsureHeartCompanion(self);
            
            IngesteLogger.Debug($"LevelStateManager: Level loaded - {self.Session.Level}");
        }

        private static void Level_UnloadLevel(On.Celeste.Level.orig_UnloadLevel orig, Level self)
        {
            // Save state to session before unload
            SaveToSession(self);
            
            // Notify character modules
            foreach (var module in PlayerExtensionCore.Instance.GetCharacterModules())
            {
                try
                {
                    module.OnLevelUnload(self);
                }
                catch (Exception ex)
                {
                    IngesteLogger.Error($"Error unloading character module: {ex}");
                }
            }
            
            orig(self);
        }

        private static System.Collections.IEnumerator Level_TransitionRoutine(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Vector2 direction)
        {
            // Handle state on transition
            if (_currentState != null)
            {
                if (_currentState.ResetHealthOnTransition)
                {
                    _currentState.KirbyHealth = _currentState.KirbyMaxHealth;
                }
                
                if (_currentState.ResetPowerOnTransition)
                {
                    _currentState.KirbyPower = KirbyMode.KirbyPowerState.None;
                }
            }
            
            return orig(self, next, direction);
        }

        private static void SaveToSession(Level level)
        {
            if (_currentState == null || level?.Session == null) return;
            
            var session = level.Session;
            
            // Save Kirby state
            session.SetFlag("kirby_mode", _currentState.KirbyModeEnabled);
            // Store power as counter (can't store enum directly in session)
            // Would need custom session data for full persistence
        }

        private static void RestoreFromSession(Level level)
        {
            if (_currentState == null || level?.Session == null) return;
            
            var session = level.Session;
            
            // Restore Kirby state
            _currentState.KirbyModeEnabled = session.GetFlag("kirby_mode");
            _currentState.CharaModeEnabled = session.GetFlag("chara_mode");
            _currentState.RalseiModeEnabled = session.GetFlag("ralsei_mode");
        }

        private static void EnsureHeartCompanion(Level level)
        {
            if (level == null)
            {
                return;
            }

            if (!MaggyHelperModule.IsInMaggyHelperMap())
            {
                return;
            }

            HeartCompanion.EnsureSquad(level);
        }

        #region Public API

        /// <summary>
        /// Get the current level state
        /// </summary>
        public static CharacterLevelState GetState()
        {
            return _currentState;
        }

        /// <summary>
        /// Get the state for a specific level
        /// </summary>
        public static CharacterLevelState GetState(this Level level)
        {
            return _currentState;
        }

        /// <summary>
        /// Check if Kirby mode is enabled
        /// </summary>
        public static bool IsKirbyModeEnabled()
        {
            return _currentState?.KirbyModeEnabled ?? false;
        }

        /// <summary>
        /// Enable Kirby mode
        /// </summary>
        public static void EnableKirbyMode(Level level = null)
        {
            if (_currentState != null)
            {
                _currentState.KirbyModeEnabled = true;
                _currentState.ActiveCharacterId = PlayerCharacterIds.Kirby;
            }
            
            if (level?.Session != null)
            {
                level.Session.SetFlag("kirby_mode", true);
            }
        }

        /// <summary>
        /// Disable Kirby mode
        /// </summary>
        public static void DisableKirbyMode(Level level = null)
        {
            if (_currentState != null)
            {
                _currentState.KirbyModeEnabled = false;
                if (PlayerCharacter.FromId(_currentState.ActiveCharacterId).IsKirby)
                {
                    _currentState.ActiveCharacterId = PlayerCharacterIds.Madeline;
                }
            }
            
            if (level?.Session != null)
            {
                level.Session.SetFlag("kirby_mode", false);
            }
        }

        /// <summary>
        /// Set the active character
        /// </summary>
        public static void SetActiveCharacter(string characterId, Level level = null)
        {
            SetActiveCharacter(PlayerCharacter.FromId(characterId), level);
        }

        /// <summary>
        /// Set the active character
        /// </summary>
        public static void SetActiveCharacter(PlayerCharacter character, Level level = null)
        {
            if (_currentState != null)
            {
                _currentState.SetActivePlayerCharacter(character);
            }
            
            if (level?.Session != null)
            {
                level.Session.SetFlag("kirby_mode", character.IsKirby);
                level.Session.SetFlag("chara_mode", character.Id == "chara");
                level.Session.SetFlag("ralsei_mode", character.Id == "ralsei");
            }
        }

        /// <summary>
        /// Get the active character ID
        /// </summary>
        public static string GetActiveCharacter()
        {
            return GetActivePlayerCharacter().Id;
        }

        /// <summary>
        /// Get the active character as a typed value.
        /// </summary>
        public static PlayerCharacter GetActivePlayerCharacter()
        {
            return _currentState?.GetActivePlayerCharacter() ?? PlayerCharacter.MadelineCharacter;
        }

        #endregion
    }
}
