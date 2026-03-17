using Celeste.Mod;

namespace MaggyHelper
{
    public class MaggyHelperSettings : EverestModuleSettings
    {
        // ===== Player Settings =====
        
        [SettingName("Enable Kirby Player")]
        [SettingSubText("Replace Madeline with Kirby as the playable character")]
        public bool EnableKirbyPlayer { get; set; } = false;

        [SettingName("Kirby Float Ability")]
        [SettingSubText("Allow Kirby to float by holding jump")]
        public bool EnableKirbyFloat { get; set; } = true;

        [SettingName("Kirby Inhale Ability")]
        [SettingSubText("Allow Kirby to inhale enemies and projectiles")]
        public bool EnableKirbyInhale { get; set; } = true;

        [SettingName("Kirby Copy Ability")]
        [SettingSubText("Allow Kirby to copy abilities from inhaled enemies")]
        public bool EnableKirbyCopyAbility { get; set; } = true;

        [SettingName("Float Duration")]
        [SettingSubText("How long Kirby can float (in seconds)")]
        [SettingRange(1, 10)]
        public int FloatDuration { get; set; } = 5;

        [SettingName("Float Puff Count")]
        [SettingSubText("Number of puffs Kirby can do while floating")]
        [SettingRange(1, 8)]
        public int FloatPuffCount { get; set; } = 5;

        // ===== Boss Settings =====
        
        [SettingName("Boss Difficulty Multiplier")]
        [SettingSubText("Adjusts boss health and damage")]
        [SettingRange(1, 5)]
        public int BossDifficultyMultiplier { get; set; } = 1;

        [SettingName("Enable Boss Music")]
        [SettingSubText("Play custom boss music during boss fights")]
        public bool EnableBossMusic { get; set; } = true;

        // ===== Enemy Settings =====
        
        [SettingName("Enemy Spawn Rate")]
        [SettingSubText("Multiplier for enemy spawn frequency")]
        [SettingRange(1, 3)]
        public int EnemySpawnRate { get; set; } = 1;

        [SettingName("Enemies Drop Stars")]
        [SettingSubText("Enemies drop ability stars when defeated")]
        public bool EnemiesDropStars { get; set; } = true;

        // ===== Visual Settings =====
        
        [SettingName("Kirby Color")]
        [SettingSubText("Choose Kirby's color palette")]
        public KirbyColorOption KirbyColor { get; set; } = KirbyColorOption.Pink;

        [SettingName("Show Ability Icon")]
        [SettingSubText("Display current copy ability icon on screen")]
        public bool ShowAbilityIcon { get; set; } = true;

        // ===== Mod Intro Settings =====

        [SettingName("Skip Mod Intro")]
        [SettingSubText("Skip the Desolo Zantas intro and go straight to Celeste")]
        public bool SkipModIntro { get; set; } = false;

        // ===== Debug Settings =====
        
        [SettingName("Debug Mode")]
        [SettingSubText("Enable debug features and logging")]
        public bool DebugMode { get; set; } = false;
    }

    public enum KirbyColorOption
    {
        Pink,
        Yellow,
        Blue,
        Red,
        Green,
        White,
        Orange,
        Purple
    }
}
