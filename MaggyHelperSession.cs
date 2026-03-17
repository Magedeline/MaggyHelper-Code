using Celeste.Mod;

namespace MaggyHelper
{
    public class MaggyHelperSession : EverestModuleSession
    {
        // ===== Kirby State =====
        
        /// <summary>
        /// Whether Kirby's float ability is currently available
        /// </summary>
        public bool KirbyFloatEnabled { get; set; } = true;

        /// <summary>
        /// Whether Kirby's inhale ability is currently available
        /// </summary>
        public bool KirbyInhaleEnabled { get; set; } = true;

        /// <summary>
        /// Current copy ability Kirby has (null if none)
        /// </summary>
        public CopyAbilityType? CurrentCopyAbility { get; set; } = null;

        /// <summary>
        /// Remaining float puffs before needing to land
        /// </summary>
        public int RemainingFloatPuffs { get; set; } = 5;

        /// <summary>
        /// Whether Kirby is currently floating
        /// </summary>
        public bool IsFloating { get; set; } = false;

        /// <summary>
        /// Whether Kirby is currently inhaling
        /// </summary>
        public bool IsInhaling { get; set; } = false;

        /// <summary>
        /// Whether Kirby has something inhaled
        /// </summary>
        public bool HasInhaledObject { get; set; } = false;

        // ===== Boss State =====
        
        /// <summary>
        /// Whether a boss fight is currently active
        /// </summary>
        public bool BossFightActive { get; set; } = false;

        /// <summary>
        /// Current boss being fought (if any)
        /// </summary>
        public string CurrentBossName { get; set; } = null;

        /// <summary>
        /// Number of bosses defeated in this session
        /// </summary>
        public int BossesDefeated { get; set; } = 0;

        // ===== Level State =====
        
        /// <summary>
        /// Number of enemies defeated in current room
        /// </summary>
        public int EnemiesDefeatedInRoom { get; set; } = 0;

        /// <summary>
        /// Total enemies defeated this session
        /// </summary>
        public int TotalEnemiesDefeated { get; set; } = 0;

        /// <summary>
        /// Ability stars collected this session
        /// </summary>
        public int AbilityStarsCollected { get; set; } = 0;

        // ===== System State =====
        
        /// <summary>
        /// Whether assets have been validated this session
        /// </summary>
        public bool HasValidatedAssets { get; set; } = false;
    }

    public enum CopyAbilityType
    {
        None,
        Fire,
        Ice,
        Spark,
        Sword,
        Cutter,
        Beam,
        Stone,
        Needle,
        Parasol,
        Wheel,
        Bomb,
        Fighter,
        Suplex,
        Ninja,
        Mirror,
        Hammer,
        Wing,
        UFO,
        Sleep
    }
}
