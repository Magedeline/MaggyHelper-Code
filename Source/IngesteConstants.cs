namespace MaggyHelper
{
    /// <summary>
    /// Constants used throughout the IngesteHelper/MaggyHelper mod.
    /// </summary>
    public static class IngesteConstants
    {
        /// <summary>
        /// Entity names for custom entities registered with Everest.
        /// Format: "ModName/EntityName"
        /// </summary>
        public static class EntityNames
        {
            public const string ANCIENT_SWITCH = "MaggyHelper/AncientSwitch";
            public const string DELTA_BERRY = "MaggyHelper/DeltaBerry";
            public const string SAMPLE_TRIGGER = "MaggyHelper/SampleTrigger";
        }
    }
    
    /// <summary>
    /// Configuration constants for gameplay
    /// </summary>
    public static class IngesteConfig
    {
        /// <summary>
        /// Default float speed for entities
        /// </summary>
        public const float FLOAT_SPEED = 80f;
        
        /// <summary>
        /// Default walk speed for NPCs
        /// </summary>
        public const float WALK_SPEED = 60f;
        
        /// <summary>
        /// Default run speed for NPCs
        /// </summary>
        public const float RUN_SPEED = 120f;
    }
}
