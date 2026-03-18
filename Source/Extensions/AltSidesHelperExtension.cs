#pragma warning disable CS0436 // Local patch save types intentionally shadow imported Celeste runtime types.

using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace MaggyHelper.Extensions
{
    /// <summary>
    /// AltSidesHelperExtension - Provides compatibility fixes and null safety hooks
    /// for AltSidesHelper mod integration. Prevents NullReferenceException crashes
    /// that occur when chapter panel data is missing or misconfigured.
    /// 
    /// The crash typically occurs in AltSidesHelperModule.AddExtraModes when
    /// accessing OuiChapterPanel properties that haven't been initialized.
    /// </summary>
    public static class AltSidesHelperExtension
    {
        private static bool _hooksRegistered = false;
        private static ILHook _addExtraModesHook;
        private static ILHook _customiseChapterPanelHook;
        private static Hook _resetHook;
        
        // Reflection cache for AltSidesHelper types
        private static Type _altSidesHelperModuleType;
        private static Assembly _altSidesHelperAssembly;
        
        /// <summary>
        /// Initialize the AltSidesHelper extension. Should be called during mod Load.
        /// </summary>
        public static void Initialize()
        {
            if (_hooksRegistered)
            {
                IngesteLogger.Warn("AltSidesHelperExtension: Hooks already registered");
                return;
            }

            try
            {
                // Try to find AltSidesHelper assembly
                _altSidesHelperAssembly = FindAltSidesHelperAssembly();
                
                if (_altSidesHelperAssembly == null)
                {
                    IngesteLogger.Info("AltSidesHelperExtension: AltSidesHelper not found, skipping hooks");
                    return;
                }

                _altSidesHelperModuleType = _altSidesHelperAssembly.GetType("AltSidesHelper.AltSidesHelperModule");
                
                if (_altSidesHelperModuleType == null)
                {
                    IngesteLogger.Warn("AltSidesHelperExtension: Could not find AltSidesHelperModule type");
                    return;
                }

                // Register safety hooks
                RegisterSafetyHooks();
                
                _hooksRegistered = true;
                IngesteLogger.Info("AltSidesHelperExtension: Safety hooks registered successfully");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error(ex, "AltSidesHelperExtension: Failed to initialize");
            }
        }

        /// <summary>
        /// Unload the AltSidesHelper extension. Should be called during mod Unload.
        /// </summary>
        public static void Unload()
        {
            if (!_hooksRegistered)
                return;

            try
            {
                UnregisterSafetyHooks();
                _hooksRegistered = false;
                IngesteLogger.Info("AltSidesHelperExtension: Hooks unregistered");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error(ex, "AltSidesHelperExtension: Failed to unload");
            }
        }

        /// <summary>
        /// Find the AltSidesHelper assembly from loaded assemblies
        /// </summary>
        private static Assembly FindAltSidesHelperAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetName().Name == "AltSidesHelper")
                    {
                        return assembly;
                    }
                }
                catch
                {
                    // Ignore assembly access errors
                }
            }
            return null;
        }

        /// <summary>
        /// Register all safety hooks for AltSidesHelper
        /// </summary>
        private static void RegisterSafetyHooks()
        {
            // Hook OuiChapterPanel.Reset to add safety checks before AltSidesHelper processes it
            On.Celeste.OuiChapterPanel.Reset += OnOuiChapterPanelReset;
            
            // Hook into the Level.Begin to ensure chapter data is valid
            On.Celeste.Level.Begin += OnLevelBegin;
            
            // Hook OuiChapterPanel constructor-related initialization
            On.Celeste.OuiChapterPanel.Enter += OnOuiChapterPanelEnter;
            
            IngesteLogger.Debug("AltSidesHelperExtension: Safety hooks for OuiChapterPanel registered");
        }

        /// <summary>
        /// Unregister all safety hooks
        /// </summary>
        private static void UnregisterSafetyHooks()
        {
            On.Celeste.OuiChapterPanel.Reset -= OnOuiChapterPanelReset;
            On.Celeste.Level.Begin -= OnLevelBegin;
            On.Celeste.OuiChapterPanel.Enter -= OnOuiChapterPanelEnter;
            
            // Dispose IL hooks if any
            _addExtraModesHook?.Dispose();
            _customiseChapterPanelHook?.Dispose();
            _resetHook?.Dispose();
            
            _addExtraModesHook = null;
            _customiseChapterPanelHook = null;
            _resetHook = null;
        }

        /// <summary>
        /// Hook for OuiChapterPanel.Reset - ensures panel data is properly initialized
        /// before AltSidesHelper tries to process it
        /// </summary>
        private static void OnOuiChapterPanelReset(On.Celeste.OuiChapterPanel.orig_Reset orig, OuiChapterPanel self)
        {
            try
            {
                // Ensure the panel has valid data before processing
                if (self == null)
                {
                    IngesteLogger.Warn("AltSidesHelperExtension: OuiChapterPanel is null in Reset");
                    return;
                }

                // Check for valid area data - this is what AltSidesHelper accesses
                var data = GetChapterPanelData(self);
                if (data == null)
                {
                    IngesteLogger.Debug("AltSidesHelperExtension: Skipping Reset - no valid chapter data");
                    // Still call orig but AltSidesHelper should handle null gracefully now
                }
                
                // Validate RealStats before calling orig - this is often the null source
                EnsureStatsInitialized(self);
            }
            catch (Exception ex)
            {
                IngesteLogger.Error(ex, "AltSidesHelperExtension: Error in pre-Reset validation");
            }
            
            // Always call original - let AltSidesHelper's hooks run
            try
            {
                orig(self);
            }
            catch (NullReferenceException nre)
            {
                // Catch and log AltSidesHelper NullReferenceException to prevent crash
                IngesteLogger.Error(nre, "AltSidesHelperExtension: Caught NullReferenceException in OuiChapterPanel.Reset (AltSidesHelper)");
                HandleAltSidesHelperCrash(self);
            }
        }

        /// <summary>
        /// Hook for OuiChapterPanel.Enter - initialization safety
        /// </summary>
        private static IEnumerator OnOuiChapterPanelEnter(On.Celeste.OuiChapterPanel.orig_Enter orig, OuiChapterPanel self, Oui from)
        {
            // Ensure required data is present before entering
            try
            {
                EnsureChapterPanelInitialized(self);
            }
            catch (Exception ex)
            {
                IngesteLogger.Error(ex, "AltSidesHelperExtension: Error ensuring panel initialization");
            }
            
            // Wrap the enumerator to catch exceptions during iteration
            IEnumerator innerEnumerator = null;
            try
            {
                innerEnumerator = orig(self, from);
            }
            catch (NullReferenceException nre)
            {
                IngesteLogger.Error(nre, "AltSidesHelperExtension: Caught NullReferenceException in OuiChapterPanel.Enter");
                HandleAltSidesHelperCrash(self);
                yield break;
            }
            
            if (innerEnumerator == null)
                yield break;
                
            while (true)
            {
                bool moveNext;
                try
                {
                    moveNext = innerEnumerator.MoveNext();
                }
                catch (NullReferenceException nre)
                {
                    IngesteLogger.Error(nre, "AltSidesHelperExtension: Caught NullReferenceException during OuiChapterPanel.Enter iteration");
                    HandleAltSidesHelperCrash(self);
                    yield break;
                }
                
                if (!moveNext)
                    break;
                    
                yield return innerEnumerator.Current;
            }
        }

        /// <summary>
        /// Hook for Level.Begin - validate level session data
        /// </summary>
        private static void OnLevelBegin(On.Celeste.Level.orig_Begin orig, Level self)
        {
            // Validate session before beginning
            if (self?.Session != null)
            {
                ValidateSessionAreaData(self.Session);
            }
            
            orig(self);
        }

        /// <summary>
        /// Get the chapter panel's current data safely using reflection
        /// </summary>
        private static AreaData GetChapterPanelData(OuiChapterPanel panel)
        {
            if (panel == null) return null;
            
            try
            {
                // Access the Data property using DynamicData for safety
                var dynData = new DynamicData(panel);
                var data = dynData.Get<AreaData>("Data");
                return data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ensure stats are initialized on the chapter panel
        /// </summary>
        private static void EnsureStatsInitialized(OuiChapterPanel panel)
        {
            if (panel == null) return;
            
            try
            {
                var dynData = new DynamicData(panel);
                
                // Check if RealStats exists - this is commonly null
                var realStats = dynData.Get<AreaModeStats>("RealStats");
                if (realStats == null)
                {
                    IngesteLogger.Debug("AltSidesHelperExtension: RealStats is null, initializing default");
                    // Create a default AreaModeStats if missing
                    dynData.Set("RealStats", new AreaModeStats());
                }
                
                // Check DisplayedStats
                var displayedStats = dynData.Get<AreaModeStats>("DisplayedStats");
                if (displayedStats == null)
                {
                    IngesteLogger.Debug("AltSidesHelperExtension: DisplayedStats is null, initializing default");
                    dynData.Set("DisplayedStats", new AreaModeStats());
                }
            }
            catch (Exception ex)
            {
                IngesteLogger.Debug($"AltSidesHelperExtension: Could not ensure stats - {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure the chapter panel is fully initialized
        /// </summary>
        private static void EnsureChapterPanelInitialized(OuiChapterPanel panel)
        {
            if (panel == null) return;
            
            try
            {
                var dynData = new DynamicData(panel);
                
                // Ensure modes list exists - use object type since Option is private
                var modes = dynData.Get<object>("modes");
                if (modes == null)
                {
                    IngesteLogger.Debug("AltSidesHelperExtension: modes list is null, will be initialized by Reset");
                    // Don't try to create the list - let the original Reset handle it
                    // The Option type is private so we can't create instances directly
                }
            }
            catch (Exception ex)
            {
                IngesteLogger.Debug($"AltSidesHelperExtension: Could not ensure initialization - {ex.Message}");
            }
        }

        /// <summary>
        /// Validate session area data to prevent null access
        /// </summary>
        private static void ValidateSessionAreaData(Session session)
        {
            if (session == null) return;
            
            try
            {
                // Ensure Area has valid key
                var area = session.Area;
                if (area.ID < 0)
                {
                    IngesteLogger.Warn($"AltSidesHelperExtension: Invalid area ID {area.ID}");
                }
                
                // Check if area data exists in the game
                if (area.ID >= AreaData.Areas.Count)
                {
                    IngesteLogger.Warn($"AltSidesHelperExtension: Area ID {area.ID} exceeds available areas ({AreaData.Areas.Count})");
                }
            }
            catch (Exception ex)
            {
                IngesteLogger.Debug($"AltSidesHelperExtension: Session validation error - {ex.Message}");
            }
        }

        /// <summary>
        /// Handle AltSidesHelper crash gracefully - try to recover the panel
        /// </summary>
        private static void HandleAltSidesHelperCrash(OuiChapterPanel panel)
        {
            IngesteLogger.Warn("AltSidesHelperExtension: Attempting to recover from AltSidesHelper crash");
            
            try
            {
                if (panel == null) return;
                
                // Try to set panel to a safe state using reflection
                var dynData = new DynamicData(panel);
                
                // Check if modes list exists - we can't directly create Option instances
                // since Option is a private nested type, but we can check if the list is null
                var modes = dynData.Get<object>("modes");
                if (modes == null)
                {
                    // Try to call the panel's original setup method to initialize properly
                    // Using reflection to find the Option type and create an empty list
                    try
                    {
                        var panelType = typeof(OuiChapterPanel);
                        var optionType = panelType.GetNestedType("Option", BindingFlags.NonPublic | BindingFlags.Public);
                        
                        if (optionType != null)
                        {
                            var listType = typeof(List<>).MakeGenericType(optionType);
                            var newList = Activator.CreateInstance(listType);
                            dynData.Set("modes", newList);
                            IngesteLogger.Info("AltSidesHelperExtension: Initialized empty modes list via reflection");
                        }
                    }
                    catch (Exception reflectionEx)
                    {
                        IngesteLogger.Debug($"AltSidesHelperExtension: Could not create modes list via reflection - {reflectionEx.Message}");
                    }
                }
                
                // Log recovery attempt
                IngesteLogger.Info("AltSidesHelperExtension: Crash recovery attempted - panel may need to be re-entered");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error(ex, "AltSidesHelperExtension: Failed to recover from crash");
            }
        }

        /// <summary>
        /// Check if the current map is from MaggyHelper
        /// </summary>
        public static bool IsMaggyHelperMap(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return false;
            
            return sid.StartsWith("DesoloZantas/", StringComparison.OrdinalIgnoreCase) ||
                   sid.StartsWith("Maggy/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if the current map is from MaggyHelper
        /// </summary>
        public static bool IsMaggyHelperMap(AreaKey area)
        {
            try
            {
                var areaData = AreaData.Get(area);
                return areaData != null && IsMaggyHelperMap(areaData.SID);
            }
            catch
            {
                return false;
            }
        }
    }
}
