using Microsoft.Xna.Framework.Input;

namespace MaggyHelper.MaggyHelper
{
    /// <summary>
    /// Settings for the Hot Code Reloading system.
    /// These settings appear in Everest's mod options menu under MaggyHelper.
    /// </summary>
    public partial class MaggyHelperModuleSettings
    {
        #region Hot Reload Settings
        
        /// <summary>
        /// Header for hot reload section.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_HEADER")]
        [SettingSubHeader("MAGGYHELPER_HOTRELOAD_HEADER")]
        public string HotReloadHeader { get; set; } = "";
        
        /// <summary>
        /// Enable the hot reload system.
        /// When enabled, code changes can be applied without restarting.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_ENABLED")]
        [SettingSubText("MAGGYHELPER_HOTRELOAD_ENABLED_DESC")]
        public bool HotReloadEnabled { get; set; } = false;
        
        /// <summary>
        /// Automatically reload when files change.
        /// If disabled, use the hotkey to manually trigger reloads.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_AUTO")]
        [SettingSubText("MAGGYHELPER_HOTRELOAD_AUTO_DESC")]
        public bool HotReloadAutoReload { get; set; } = true;
        
        /// <summary>
        /// Show the hot reload status overlay in-game.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_SHOW_UI")]
        public bool HotReloadShowUI { get; set; } = true;
        
        /// <summary>
        /// Play a sound when hot reload succeeds.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_SOUND")]
        public bool HotReloadPlaySound { get; set; } = true;
        
        /// <summary>
        /// Allow all methods to be hot-reloaded, not just those marked with [HotReloadable].
        /// Warning: This may cause issues with some code.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_ALL_METHODS")]
        [SettingSubText("MAGGYHELPER_HOTRELOAD_ALL_METHODS_DESC")]
        public bool HotReloadAllMethods { get; set; } = false;
        
        /// <summary>
        /// Log verbose information about hot reload operations.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_VERBOSE")]
        public bool HotReloadVerbose { get; set; } = false;
        
        /// <summary>
        /// Custom source path override.
        /// Leave empty for auto-detection.
        /// </summary>
        [SettingName("MAGGYHELPER_HOTRELOAD_SOURCE_PATH")]
        [SettingMaxLength(256)]
        public string HotReloadSourcePath { get; set; } = "";
        
        #endregion
        
        #region Hot Reload Key Bindings
        
        /// <summary>
        /// Key binding to toggle hot reload on/off.
        /// </summary>
        [SettingName("MAGGYHELPER_BIND_HOTRELOAD_TOGGLE")]
        [DefaultButtonBinding(0, Keys.F5)]
        public ButtonBinding HotReloadToggleBind { get; set; }
        
        /// <summary>
        /// Key binding to manually trigger a reload.
        /// </summary>
        [SettingName("MAGGYHELPER_BIND_HOTRELOAD_RELOAD")]
        [DefaultButtonBinding(0, Keys.F6)]
        public ButtonBinding HotReloadReloadBind { get; set; }
        
        /// <summary>
        /// Key binding to reload all files.
        /// </summary>
        [SettingName("MAGGYHELPER_BIND_HOTRELOAD_RELOAD_ALL")]
        [DefaultButtonBinding(0, Keys.F7)]
        public ButtonBinding HotReloadReloadAllBind { get; set; }
        
        /// <summary>
        /// Key binding to show/hide the hot reload UI.
        /// </summary>
        [SettingName("MAGGYHELPER_BIND_HOTRELOAD_UI")]
        [DefaultButtonBinding(0, Keys.F8)]
        public ButtonBinding HotReloadUIBind { get; set; }
        
        #endregion
    }
}

namespace MaggyHelper.HotReload
{
    /// <summary>
    /// Extension of the main module to handle hot reload settings changes.
    /// </summary>
    public static class HotReloadSettingsHandler
    {
        private static bool _lastEnabledState = false;
        
        /// <summary>
        /// Initialize settings handler.
        /// </summary>
        public static void Initialize()
        {
            // Track initial state
            _lastEnabledState = MaggyHelperModule.Settings?.HotReloadEnabled ?? false;
            
            // Apply initial settings
            ApplySettings();
        }
        
        /// <summary>
        /// Apply current settings to the hot reload system.
        /// </summary>
        public static void ApplySettings()
        {
            var settings = MaggyHelperModule.Settings;
            if (settings == null)
                return;
            
            // Handle enable/disable state change
            if (settings.HotReloadEnabled != _lastEnabledState)
            {
                _lastEnabledState = settings.HotReloadEnabled;
                
                if (settings.HotReloadEnabled)
                {
                    // Initialize and enable
                    string sourcePath = string.IsNullOrEmpty(settings.HotReloadSourcePath) 
                        ? null 
                        : settings.HotReloadSourcePath;
                    
                    HotReloadManager.Initialize(sourcePath);
                    
                    if (settings.HotReloadAutoReload)
                        HotReloadManager.Enable();
                }
                else
                {
                    // Disable and shutdown
                    HotReloadManager.Shutdown();
                }
            }
            
            // Handle auto-reload setting
            if (settings.HotReloadEnabled)
            {
                if (settings.HotReloadAutoReload && !HotReloadManager.IsEnabled)
                    HotReloadManager.Enable();
                else if (!settings.HotReloadAutoReload && HotReloadManager.IsEnabled)
                    HotReloadManager.Disable();
            }
        }
        
        /// <summary>
        /// Handle input for hot reload keybindings.
        /// </summary>
        public static void HandleInput()
        {
            var settings = MaggyHelperModule.Settings;
            if (settings == null)
                return;
            
            // Toggle hot reload
            if (settings.HotReloadToggleBind?.Pressed == true)
            {
                settings.HotReloadEnabled = !settings.HotReloadEnabled;
                ApplySettings();
                Logger.Log(LogLevel.Info, "HotReload", $"Hot reload toggled: {settings.HotReloadEnabled}");
            }
            
            // Manual reload (F6)
            if (settings.HotReloadReloadBind?.Pressed == true && HotReloadManager.IsInitialized)
            {
                // For manual reload, we'd need to know the last changed file
                // For now, just do a full reload
                HotReloadManager.ReloadAll();
            }
            
            // Reload all (F7)
            if (settings.HotReloadReloadAllBind?.Pressed == true && HotReloadManager.IsInitialized)
            {
                HotReloadManager.ReloadAll();
            }
            
            // Toggle UI (F8)
            if (settings.HotReloadUIBind?.Pressed == true)
            {
                settings.HotReloadShowUI = !settings.HotReloadShowUI;
            }
        }
    }
}
