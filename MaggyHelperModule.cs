using System;
using System.Collections.Generic;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.DesoloZantas.Core.Core;
using Celeste.Mod.DesoloZantas.Core.Utils;
using DesoloZantas.Core.Core.Extensions.Kirby.ModCompat;
using DesoloZantas.Core.Core.UI;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper
{
    public class MaggyHelperModule : EverestModule
    {
        public static MaggyHelperModule Instance { get; private set; }

        public override Type SettingsType => typeof(MaggyHelperSettings);
        public static MaggyHelperSettings Settings => (MaggyHelperSettings)Instance._Settings;

        public override Type SessionType => typeof(MaggyHelperSession);
        public static MaggyHelperSession Session => (MaggyHelperSession)Instance._Session;

        public override Type SaveDataType => typeof(MaggyHelperSaveData);
        public static MaggyHelperSaveData SaveData => (MaggyHelperSaveData)Instance._SaveData;

        /// <summary>
        /// Resets all mod save-data for a clean new-game start.
        /// Exposed as a static wrapper so Source/ project code can call this
        /// without needing to cast the Everest-generated save-data type.
        /// </summary>
        public static void ResetModSaveData() => SaveData?.ResetForNewGame();

        // Track whether we've already checked for the mod intro this launch
        private bool hasCheckedModIntro = false;

        public MaggyHelperModule()
        {
            Instance = this;
        }

        public override void Load()
        {
            Logger.Log(LogLevel.Info, "MaggyHelper", "Loading MaggyHelper mod...");
            
            try
            {
                // Validate critical dependencies
                if (!ValidateDependencies())
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", "Some dependencies are missing. Mod will run with limited functionality.");
                }
                
                // Hook into SaveData initialization
                Everest.Events.MainMenu.OnCreateButtons += OnMainMenuCreate;
                
                // Hook into player spawn to allow Kirby player swap
                On.Celeste.Player.ctor += OnPlayerConstruct;
                On.Celeste.Player.Update += OnPlayerUpdate;
                
                // Register custom entities
                Everest.Events.Level.OnLoadEntity += OnLoadEntity;

                // Hook into the Overworld to show mod selection screen for new players
                On.Celeste.Overworld.Begin += OnOverworldBegin;

                // Hook the main menu Credits button to show our custom mod credits
                On.Celeste.OuiMainMenu.OnCredits += OnMainMenuCredits;

                // Hook save data loading to initialize and migrate data
                On.Celeste.UserIO.Load += OnSaveDataLoad;

                // Initialize Kirby mod compatibility bridges
                // (CommunalHelper, VivHelper, BossesHelper, MaxHelpingHand,
                //  DJMapHelper, HonlyHelper, MoreDasheline, DoonvHelper)
                try
                {
                    KirbyModCompatManager.Initialize();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to initialize Kirby mod compatibility: {ex.Message}");
                }

                // Hook into level load complete to validate assets
                Everest.Events.Level.OnLoadLevel += OnLevelLoadComplete;

                // Register advanced MonoMod hooks (IL hooks + manual Hook class)
                MonoModHooks.Load();

                Logger.Log(LogLevel.Info, "MaggyHelper", "All hooks registered successfully");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Critical error during mod load: {ex}");
                throw; // Re-throw to prevent broken state
            }
        }

        /// <summary>
        /// Called after a level finishes loading - validate critical assets
        /// </summary>
        private void OnLevelLoadComplete(Level level, Player.IntroTypes playerIntro, bool isFromLoader)
        {
            try
            {
                // Only validate once per session
                if (Session != null && !Session.HasValidatedAssets)
                {
                    ValidateCriticalAssets();
                    Session.HasValidatedAssets = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"Error validating assets: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that critical mod assets are available
        /// </summary>
        private void ValidateCriticalAssets()
        {
            Logger.Log(LogLevel.Info, "MaggyHelper", "Validating critical assets...");

            int missingCount = 0;

            // Validate critical sprites by checking the sprite bank
            string[] criticalSprites = new[]
            {
                "maggy_player",
                "kirby_player",
                "maggy_badeline",
                "kirby",
            };

            foreach (var spriteId in criticalSprites)
            {
                if (!GFX.SpriteBank.SpriteData.ContainsKey(spriteId))
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", $"Sprite bank entry missing: {spriteId}");
                    missingCount++;
                }
            }

            // Validate critical audio events
            string[] criticalAudio = new[]
            {
                "event:/desolozantas/music/CSide/",
                "event:/desolozantas/music/DXSide/",
            };

            foreach (var audioPath in criticalAudio)
            {
                if (!AssetValidator.ValidateAudioEvent(audioPath))
                {
                    missingCount++;
                }
            }

            if (missingCount > 0)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", 
                    $"Asset validation found {missingCount} missing critical assets. Some features may not work.");
                AssetValidator.LogMissingSummary();
            }
            else
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "All critical assets validated successfully");
            }
        }

        /// <summary>
        /// Validates that critical dependencies are loaded
        /// </summary>
        private bool ValidateDependencies()
        {
            bool allCritical = true;
            
            // Check for Everest (always required)
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata { Name = "Everest", Version = new Version(1, 5848, 0) }))
            {
                Logger.Log(LogLevel.Info, "MaggyHelper", "✓ Everest dependency validated");
            }
            else
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", "✗ Everest version too old!");
                allCritical = false;
            }
            
            // Check for LuaCutscenes (critical for cutscene functionality)
            if (!Everest.Loader.DependencyLoaded(new EverestModuleMetadata { Name = "LuaCutscenes", Version = new Version(0, 2, 13) }))
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", "✗ LuaCutscenes not found - custom cutscenes will be disabled");
            }
            
            return allCritical;
        }

        public override void Unload()
        {
            Logger.Log(LogLevel.Info, "MaggyHelper", "Unloading MaggyHelper mod...");
            
            Everest.Events.MainMenu.OnCreateButtons -= OnMainMenuCreate;
            On.Celeste.Player.ctor -= OnPlayerConstruct;
            On.Celeste.Player.Update -= OnPlayerUpdate;
            Everest.Events.Level.OnLoadEntity -= OnLoadEntity;
            On.Celeste.Overworld.Begin -= OnOverworldBegin;
            On.Celeste.OuiMainMenu.OnCredits -= OnMainMenuCredits;
            On.Celeste.UserIO.Load -= OnSaveDataLoad;

            // Cleanup Kirby mod compatibility bridges
            KirbyModCompatManager.Uninitialize();

            // Unload advanced MonoMod hooks
            MonoModHooks.Unload();

            Logger.Log(LogLevel.Info, "MaggyHelper", "All hooks unregistered successfully");
        }

        /// <summary>
        /// Hook for when save data is loaded - initializes and migrates data
        /// </summary>
        private static void OnSaveDataLoad(On.Celeste.UserIO.orig_Load orig)
        {
            try
            {
                orig();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error loading save data: {ex.Message}");
                Logger.Log(LogLevel.Warn, "MaggyHelper", "Attempting to recover with fresh save data...");
                
                // Try to recover by creating fresh save data
                try
                {
                    Celeste.SaveData.InitializeDebugMode(false);
                }
                catch
                {
                    Logger.Log(LogLevel.Error, "MaggyHelper", "Failed to recover save data. Player may need to delete save file.");
                }
            }

            try
            {
                // Initialize SaveData if needed
                if (SaveData != null)
                {
                    SaveData.Initialize();
                    
                    // Run migrations if needed
                    if (SaveData.SaveDataVersion < 1)
                    {
                        Logger.Log(LogLevel.Info, "MaggyHelper", "Running save data migration...");
                        try
                        {
                            MaggySaveDataMigration.Run();
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error, "MaggyHelper", $"Save data migration failed: {ex.Message}");
                            // Continue anyway - better to have incomplete data than crash
                        }
                    }
                }
                else
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", "SaveData is null after load!");
                }

                Logger.Log(LogLevel.Info, "MaggyHelper", "SaveData loaded and initialized");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error initializing MaggyHelper save data: {ex}");
                // Don't crash - just log the error
            }
        }

        /// <summary>
        /// Hook for when the main menu is created - ensures SaveData is ready
        /// </summary>
        private void OnMainMenuCreate(OuiMainMenu menu, List<MenuButton> buttons)
        {
            try
            {
                // Ensure SaveData is initialized when main menu is created
                if (SaveData != null && !SaveData.UnlockedColors.Contains(KirbyColorOption.Pink))
                {
                    SaveData.Initialize();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnMainMenuCreate: {ex.Message}");
            }
        }

        /// <summary>
        /// Intercepts the Overworld (main menu) once per engine launch.
        ///
        /// NOTE: <see cref="MaggyHelperSaveData"/> (<c>SaveData</c>) is always
        /// <b>null</b> here. Everest per-slot save data is only populated <em>after</em>
        /// the player picks a file on the file-select screen, which appears inside the
        /// Overworld — not before it.  Do not read or write SaveData from this hook.
        ///
        /// The actual mod-intro interception (VesselCreationVignette) happens in
        /// <see cref="MaggyHelperHooks.LevelEnterGo"/>, which fires after a save slot
        /// is selected and SaveData is fully available.
        /// </summary>
        private void OnOverworldBegin(On.Celeste.Overworld.orig_Begin orig, Overworld self)
        {
            try
            {
                hasCheckedModIntro = true; // record that the overworld has been visited
                orig(self);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnOverworldBegin: {ex}");
                try { orig(self); } catch { }
            }
        }

        private void OnPlayerConstruct(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode)
        {
            try
            {
                orig(self, position, spriteMode);
                
                if (Settings?.EnableKirbyPlayer == true)
                {
                    try
                    {
                        // Apply Kirby skin modifications
                        ApplyKirbySkin(self);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warn, "MaggyHelper", $"Failed to apply Kirby skin: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnPlayerConstruct: {ex}");
                // Re-throw if original constructor failed
                throw;
            }
        }

        private void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self)
        {
            try
            {
                orig(self);
                
                if (Settings?.EnableKirbyPlayer == true && Session != null)
                {
                    try
                    {
                        // Kirby-specific abilities
                        HandleKirbyAbilities(self);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warn, "MaggyHelper", $"Error handling Kirby abilities: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Error in OnPlayerUpdate: {ex}");
                // Don't re-throw - just skip this frame's update
            }
        }

        private void ApplyKirbySkin(Player player)
        {
            // Override player sprite with Kirby sprites
            // This will be configured via Graphics/Sprites.xml
            Logger.Log(LogLevel.Debug, "MaggyHelper", "Applying Kirby skin to player");
        }

        private void HandleKirbyAbilities(Player player)
        {
            try
            {
                // Kirby float ability (hold jump to float)
                if (Settings?.EnableKirbyFloat == true && Session?.KirbyFloatEnabled == true)
                {
                    // Float logic handled in Kirby player component
                }
                
                // Kirby inhale ability
                if (Settings?.EnableKirbyInhale == true && Session?.KirbyInhaleEnabled == true)
                {
                    // Inhale logic handled in Kirby player component
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Debug, "MaggyHelper", $"Error in HandleKirbyAbilities: {ex.Message}");
            }
        }

        private bool OnLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        {
            try
            {
                if (level == null || entityData == null)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper", "OnLoadEntity called with null parameters");
                    return false;
                }

                switch (entityData.Name)
                {
                case "MaggyHelper/PinkPlatinumStrawberry":
                    level.Add(new DesoloZantas.Core.Core.Entities.PinkPlatinumBerry(entityData, offset, new EntityID(level.Session.Level, entityData.ID)));
                    return true;
                    
                case "MaggyHelper/KirbyBoss":
                    level.Add(new Entities.Bosses.KirbyBoss(entityData, offset));
                    return true;
                    
                case "MaggyHelper/DededeBoss":
                    level.Add(new Entities.Bosses.DededeBoss(entityData, offset));
                    return true;
                    
                case "MaggyHelper/MetaKnightBoss":
                    level.Add(new Entities.Bosses.MetaKnightBoss(entityData, offset));
                    return true;
                    
                case "MaggyHelper/WaddleDee":
                    level.Add(new Entities.Enemies.WaddleDee(entityData, offset));
                    return true;
                    
                case "MaggyHelper/WaddleDoo":
                    level.Add(new Entities.Enemies.WaddleDoo(entityData, offset));
                    return true;
                    
                case "MaggyHelper/Gordo":
                    level.Add(new Entities.Enemies.Gordo(entityData, offset));
                    return true;
                    
                case "MaggyHelper/ScarfyEnemy":
                    level.Add(new Entities.Enemies.ScarfyEnemy(entityData, offset));
                    return true;
                    
                case "MaggyHelper/KirbySpawnPoint":
                    level.Add(new Entities.KirbySpawnPoint(entityData, offset));
                    return true;
                    
                case "MaggyHelper/AbilityStar":
                    level.Add(new Entities.AbilityStar(entityData, offset));
                    return true;
                    
                case "MaggyHelper/Gondola":
                    level.Add(new DesoloZantas.Core.Core.GondolaMod(entityData, offset));
                    return true;
                    
                case "MaggyHelper/MaggyMemorial":
                    level.Add(new DesoloZantas.Core.Core.Entities.MaggyMemorial(entityData, offset));
                    return true;
                    
                // DX-Side Boss Entities
                case "MaggyHelper/DXFloweyOmegaBoss":
                    level.Add(new DesoloZantas.Core.Core.DXFloweyOmegaBoss(entityData, offset));
                    return true;
                    
                case "MaggyHelper/DXAsrielTranscendenceBoss":
                    level.Add(new DesoloZantas.Core.Core.DXAsrielTranscendenceBoss(entityData, offset));
                    return true;
                    
                case "MaggyHelper/DXDarkMatterBoss":
                    level.Add(new DesoloZantas.Core.Core.DXDarkMatterBoss(entityData, offset));
                    return true;
                    
                default:
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", 
                    $"Failed to load entity '{entityData?.Name ?? "<null>"}': {ex.Message}\n{ex.StackTrace}");
                
                // Return false so the game knows we didn't handle it
                // This allows the level to continue loading without this entity
                return false;
            }
        }

        /// <summary>
        /// Replaces the main-menu "Credits" action with our custom mod credits scene.
        /// When the player clicks "Credits" on the title screen, they see the
        /// Desolo Zantas credits instead of the vanilla Celeste credits.
        /// </summary>
        private void OnMainMenuCredits(On.Celeste.OuiMainMenu.orig_OnCredits orig, OuiMainMenu self)
        {
            try
            {
                Logger.Log(LogLevel.Info, "MaggyHelper",
                    "[MainMenuCredit] Launching Desolo Zantas credits from main menu");

                // Transition to our custom credits scene (bypasses the vanilla credits entirely)
                Audio.Play("event:/ui/main/button_select");
                Engine.Scene = new MainMenuCredit();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper", $"Failed to load custom credits: {ex.Message}");
                // Fall back to vanilla credits
                try { orig(self); } catch { }
            }
        }
    }
}
