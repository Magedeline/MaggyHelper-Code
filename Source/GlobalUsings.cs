// Global using directives

// Global using directives for Ingeste mod
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using Celeste;
// REMOVED: global using FMOD.Studio; - Use Celeste's Audio system instead
global using Celeste.Mod;
global using Celeste.Mod.Backdrops; // For CustomBackdrop attribute
global using Celeste.Mod.Entities; // For CustomEntity attribute
global using MaggyHelper; // MaggyHelper root namespace (IngesteConstants, AreaModeExtender, etc.)
global using MaggyHelper.Helpers; // Helper classes (AudioHelper, TextureUtil, BossActor, etc.)
global using MaggyHelper.HotReload; // Hot reload attributes
global using global::MaggyHelper.Extensions; // KirbyMode, KirbyHealthDisplay, etc.
global using global::MaggyHelper.Extensions.Core; // Core extension helpers
// Explicit type aliases to resolve ambiguity between MaggyHelper custom types and Celeste vanilla types
global using KirbyModeExt = global::MaggyHelper.Extensions.KirbyMode;
global using MaggyHelperModule = MaggyHelper.MaggyHelperModule;
global using IngesteModule = MaggyHelper.MaggyHelper.IngesteModule;
global using IngesteLogger = MaggyHelper.MaggyHelper.IngesteLogger;
global using MaggyHelperModuleSettings = MaggyHelper.MaggyHelper.MaggyHelperModuleSettings;
global using MaggyHelperModuleSaveData = MaggyHelper.MaggyHelper.MaggyHelperModuleSaveData;
global using MaggyHelperModuleSession = MaggyHelper.MaggyHelper.MaggyHelperModuleSession;
global using MaggySaveDataMigration = MaggyHelper.MaggyHelper.MaggySaveDataMigration;
global using IngesteModuleSettings = MaggyHelper.MaggyHelper.IngesteModuleSettings;
global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;
global using Monocle;
global using On; // For hooks like On.Celeste.Level
// Import PlayerSpriteMode extensions to make extended character modes available
global using static MaggyHelper.PlayerSpriteModeExtensions;
global using CelesteGame = Celeste.Celeste; // Alias for Celeste game class (avoids confusion with Celeste namespace)
global using CelesteBridge = Celeste.Bridge;
global using CelesteDashBlock = Celeste.DashBlock; // Alias for DashBlock
global using CelesteJumpThru = Celeste.JumpThru;
global using CelesteNPC = Celeste.NPC; // Alias for Celeste.NPC to avoid ambiguity with custom NPC classes
// Type aliases to avoid conflicts with custom versions in DesoloZantas.Core.Entities
global using CelestePlayer = Celeste.Player;
global using CelestePlayerSprite = Celeste.PlayerSprite;
global using CelesteStarJumpBlock = Celeste.StarJumpBlock;
global using CelesteStrawberry = Celeste.Strawberry;


