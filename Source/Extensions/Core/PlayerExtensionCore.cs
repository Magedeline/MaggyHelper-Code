using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using MonoMod.RuntimeDetour;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Central hook management class for player extension system.
    /// Similar to Aqua's HookCenter, this manages initialization and cleanup
    /// of all extension hooks for custom character systems.
    /// </summary>
    public class PlayerExtensionCore
    {
        private static PlayerExtensionCore _instance;
        public static PlayerExtensionCore Instance => _instance ??= new PlayerExtensionCore();

        private bool _initialized = false;
        private List<ICharacterModule> _characterModules = new List<ICharacterModule>();

        /// <summary>
        /// Initialize all extension hooks
        /// </summary>
        public void Hook()
        {
            if (_initialized) return;

            IngesteLogger.Info("PlayerExtensionCore: Initializing extension hooks...");

            try
            {
                // Core extensions
                EntityExtensionsCore.Initialize();
                SolidExtensionsCore.Initialize();
                PlatformExtensionsCore.Initialize();
                SpringExtensionsCore.Initialize();
                ActorExtensionsCore.Initialize();
                JumpThruExtensionsCore.Initialize();
                
                // Player extensions
                PlayerCharacterStates.Initialize();
                PlayerSpriteExtensionsCore.Initialize();
                
                // Level management
                LevelStateManager.Initialize();
                
                // Register default character modules
                RegisterDefaultCharacterModules();
                
                // Initialize all registered character modules
                foreach (var module in _characterModules)
                {
                    try
                    {
                        module.Initialize();
                        IngesteLogger.Debug($"Initialized character module: {module.CharacterName}");
                    }
                    catch (Exception ex)
                    {
                        IngesteLogger.Error($"Failed to initialize character module {module.CharacterName}: {ex}");
                    }
                }

                _initialized = true;
                IngesteLogger.Info("PlayerExtensionCore: All hooks initialized successfully");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error($"PlayerExtensionCore: Failed to initialize hooks: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Cleanup all extension hooks
        /// </summary>
        public void Unhook()
        {
            if (!_initialized) return;

            IngesteLogger.Info("PlayerExtensionCore: Cleaning up extension hooks...");

            try
            {
                // Uninitialize character modules in reverse order
                for (int i = _characterModules.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        _characterModules[i].Uninitialize();
                    }
                    catch (Exception ex)
                    {
                        IngesteLogger.Error($"Failed to uninitialize character module: {ex}");
                    }
                }

                // Level management
                LevelStateManager.Uninitialize();
                
                // Player extensions
                PlayerSpriteExtensionsCore.Uninitialize();
                PlayerCharacterStates.Uninitialize();
                
                // Core extensions
                JumpThruExtensionsCore.Uninitialize();
                ActorExtensionsCore.Uninitialize();
                SpringExtensionsCore.Uninitialize();
                PlatformExtensionsCore.Uninitialize();
                SolidExtensionsCore.Uninitialize();
                EntityExtensionsCore.Uninitialize();

                _initialized = false;
                IngesteLogger.Info("PlayerExtensionCore: All hooks cleaned up");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error($"PlayerExtensionCore: Failed to cleanup hooks: {ex}");
            }
        }

        /// <summary>
        /// Register a character module
        /// </summary>
        public void RegisterCharacterModule(ICharacterModule module)
        {
            if (module == null) return;
            
            if (!_characterModules.Contains(module))
            {
                _characterModules.Add(module);
                IngesteLogger.Debug($"Registered character module: {module.CharacterName}");
                
                // If already initialized, initialize the new module immediately
                if (_initialized)
                {
                    try
                    {
                        module.Initialize();
                    }
                    catch (Exception ex)
                    {
                        IngesteLogger.Error($"Failed to initialize new character module {module.CharacterName}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Unregister a character module
        /// </summary>
        public void UnregisterCharacterModule(ICharacterModule module)
        {
            if (module == null) return;
            
            if (_characterModules.Remove(module))
            {
                if (_initialized)
                {
                    try
                    {
                        module.Uninitialize();
                    }
                    catch (Exception ex)
                    {
                        IngesteLogger.Error($"Failed to uninitialize character module: {ex}");
                    }
                }
                IngesteLogger.Debug($"Unregistered character module: {module.CharacterName}");
            }
        }

        /// <summary>
        /// Get a character module by type
        /// </summary>
        public T GetCharacterModule<T>() where T : class, ICharacterModule
        {
            foreach (var module in _characterModules)
            {
                if (module is T typed)
                    return typed;
            }
            return null;
        }

        /// <summary>
        /// Get all registered character modules
        /// </summary>
        public IReadOnlyList<ICharacterModule> GetCharacterModules() => _characterModules.AsReadOnly();

        /// <summary>
        /// Register default character modules
        /// </summary>
        private void RegisterDefaultCharacterModules()
        {
            // Register Kirby module
            RegisterCharacterModule(new KirbyCharacterModule());
            
            // Register other character modules as needed
            // RegisterCharacterModule(new RalseiCharacterModule());
            // RegisterCharacterModule(new CharaCharacterModule());
        }

        /// <summary>
        /// Check if the core is initialized
        /// </summary>
        public bool IsInitialized => _initialized;
    }
}
