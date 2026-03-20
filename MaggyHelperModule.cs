using MaggyHelper.Effects.ShaderEffects;
using MaggyHelper.Extensions.Core;
using MaggyHelper.Extensions.Kirby;
using MaggyHelper.HotReload;
using MaggyHelper.Popstarberry;
using MonoMod.RuntimeDetour;

namespace MaggyHelper;

/// <summary>
/// Primary Everest module for MaggyHelper / Desolo Zantas.
/// This single module wires all chapter, progression, postcard, and extended area-mode systems.
/// </summary>
public class MaggyHelperModule : EverestModule
{
    public static MaggyHelperModule Instance { get; private set; }

    // These aliases are provided by Source/GlobalUsings.cs.
    public override Type SettingsType => typeof(global::MaggyHelper.MaggyHelper.MaggyHelperModuleSettings);
    public static global::MaggyHelper.MaggyHelper.MaggyHelperModuleSettings Settings
        => (global::MaggyHelper.MaggyHelper.MaggyHelperModuleSettings)Instance?._Settings;

    public override Type SessionType => typeof(MaggyHelperModuleSession);
    public static MaggyHelperModuleSession Session => (MaggyHelperModuleSession)Instance?._Session;

    public override Type SaveDataType => typeof(MaggyHelperModuleSaveData);
    public static MaggyHelperModuleSaveData SaveData => (MaggyHelperModuleSaveData)Instance?._SaveData;

    public static void ResetModSaveData() => SaveData?.ResetForNewGame();

    public static SpriteBank SpriteBank { get; private set; }
    public static ParticleType P_StarExplosion { get; private set; }

    public static bool LaunchPart1Credits { get; set; } = false;
    public static bool LaunchPart2Credits { get; set; } = false;

    private bool _hooksLoaded;
    private bool _initialized;
    private Hook _levelUpdateHook;

    public MaggyHelperModule()
    {
        Instance = this;
    }

    public override void Load()
    {
        Logger.Log(LogLevel.Info, "MaggyHelper", "Loading MaggyHelper...");
        try
        {
            var startupStatus = new List<string>();

            void LoadSubsystem(string name, Action action)
            {
                try
                {
                    action();
                    startupStatus.Add($"{name}=OK");
                }
                catch (Exception ex)
                {
                    startupStatus.Add($"{name}=FAIL({ex.GetType().Name})");
                    throw;
                }
            }

            LoadSubsystem("MaggyHelperHooks", () =>
            {
                MaggyHelperHooks.Load();
                _hooksLoaded = true;
            });

            LoadSubsystem("PlayerExtensionCore", () => PlayerExtensionCore.Instance.Hook());
            LoadSubsystem("KirbyPauseMenuCompat", () => KirbyPauseMenuCompat.Load());
            LoadSubsystem("PopstarberryIntegration", () => PopstarberryIntegration.Initialize());

            // Core systems requested for Everest integration.
            LoadSubsystem("AreaModeExtender", () => AreaModeExtender.Load());
            LoadSubsystem("HeartGemManager", () => HeartGemManager.Load());
            LoadSubsystem("MaggyProgressionManager", () => MaggyProgressionManager.Load());
            LoadSubsystem("IntroRemixHooks", () => IntroRemixHooks.Load());
            LoadSubsystem("OverworldMusicManager", () => OverworldMusicManager.Load());
            LoadSubsystem("MountainOverworldManager", () => MountainOverworldManager.Load());
            LoadSubsystem("ChapterProgressionManager", () => ChapterProgressionManager.Load());

            LoadSubsystem("GlobalModAudioLoader", () => GlobalModAudioLoader.Load());
            LoadSubsystem("HotReloadInit", () => InitializeHotReload());
            LoadSubsystem("HotReloadHook", () => InstallHotReloadHook());

            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Startup diagnostics: {string.Join(", ", startupStatus)}");

            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper loaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Load failed: {ex}");
            throw;
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        if (_initialized)
            return;

        Logger.Log(LogLevel.Info, "MaggyHelper", "Initializing MaggyHelper...");
        try
        {
            try
            {
                MetadataRegistries.Initialize();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"MetadataRegistries skipped: {ex.Message}");
            }

            GlobalModAudioLoader.Initialize();
            AreaMapData.Initialize();
            MaggySaveDataMigration.Run();
            EffectManager.Initialize();

            try
            {
                PopstarberryModule.Instance?.Initialize();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"PopstarberryModule skipped: {ex.Message}");
            }

            _initialized = true;
            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper initialized successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Initialize failed: {ex}");
            throw;
        }
    }

    public override void Unload()
    {
        Logger.Log(LogLevel.Info, "MaggyHelper", "Unloading MaggyHelper...");
        try
        {
            if (_hooksLoaded)
            {
                MaggyHelperHooks.Unload();
                _hooksLoaded = false;
            }

            PlayerExtensionCore.Instance.Unhook();
            KirbyPauseMenuCompat.Unload();
            RemoveHotReloadHook();
            ShutdownHotReload();
            PopstarberryIntegration.Unload();

            AreaModeExtender.Unload();
            HeartGemManager.Unload();
            MaggyProgressionManager.Unload();
            IntroRemixHooks.Unload();
            OverworldMusicManager.Unload();
            MountainOverworldManager.Unload();
            ChapterProgressionManager.Unload();

            EffectManager.UnloadAll();
            GlobalModAudioLoader.Unload();

            _initialized = false;
            Logger.Log(LogLevel.Info, "MaggyHelper", "MaggyHelper unloaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MaggyHelper", $"Unload error: {ex}");
        }
    }

    private void InitializeHotReload()
    {
        try
        {
            HotReloadSettingsHandler.Initialize();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload init skipped: {ex.Message}");
        }
    }

    private void ShutdownHotReload()
    {
        try
        {
            HotReloadManager.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload shutdown error: {ex.Message}");
        }
    }

    private void InstallHotReloadHook()
    {
        if (_levelUpdateHook is not null)
            return;

        try
        {
            MethodInfo target = typeof(Level).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo detour = GetType().GetMethod(
                nameof(Level_Update_HotReload_Detour),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (target != null && detour != null)
                _levelUpdateHook = new Hook(target, detour);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"HotReload hook install skipped: {ex.Message}");
        }
    }

    private void RemoveHotReloadHook()
    {
        _levelUpdateHook?.Dispose();
        _levelUpdateHook = null;
    }

    private static void Level_Update_HotReload_Detour(Action<Level> orig, Level self)
    {
        orig(self);
        try
        {
            HotReloadSettingsHandler.HandleInput();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Warn, "MaggyHelper", $"Hot reload input error: {ex.Message}");
        }
    }

    public static bool IsInMaggyHelperMap()
    {
        var level = Engine.Scene as Level;
        if (level?.Session == null)
            return false;

        string sid = level.Session.Area.GetSID();
        return sid != null && (
            sid.StartsWith("Maggy/", StringComparison.OrdinalIgnoreCase) ||
            sid.StartsWith("DesoloZantas/", StringComparison.OrdinalIgnoreCase)
        );
    }

    public static void LogDebug(string message)
    {
        if (Settings?.DebugMode == true)
            Logger.Log(LogLevel.Debug, "MaggyHelper", message);
    }
}
