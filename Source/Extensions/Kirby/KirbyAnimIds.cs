namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Canonical Kirby animation ids from Graphics/customplayersprites.xml.
    /// Keep this file synchronized with the sprite bank definitions.
    /// </summary>
    public static class KirbyAnimIds
    {
        /// <summary>
        /// Logical animation keys used by gameplay code before being resolved by KirbyPlayerSpriteCore.
        /// </summary>
        public static class Logical
        {
            public const string Idle = "idle";
            public const string Walk = "walk";
            public const string Run = "run";
            public const string Jump = "jump";
            public const string Fall = "fall";
            public const string Dash = "dash";
            public const string Hover = "hover";
            public const string Inhale = "inhale";
            public const string Spit = "spit";
            public const string Damage = "damage";
            public const string Death = "death";
            public const string Melee = "melee";
            public const string MeleeUp = "melee_up";
            public const string MeleeDown = "melee_down";
            public const string Range = "range";
            public const string Swallow = "swallow";
            public const string Copy = "copy";
            public const string Mouthful = "mouthful";
        }

        public const string Idle = "idle";
        public const string IdleA = "idleA";
        public const string Walk = "walk";
        public const string RunSlow = "runSlow";
        public const string RunFast = "runFast";
        public const string JumpSlow = "jumpSlow";
        public const string JumpFast = "jumpFast";
        public const string Fall = "fall";
        public const string FallSlow = "fallSlow";
        public const string FallFast = "fallFast";
        public const string Dash = "dash";
        public const string Hover = "hover";
        public const string Float = "float";
        public const string Slide = "slide";
        public const string Duck = "duck";

        public const string Inhale = "inhale";
        public const string InhaleBegin = "inhalebegin";
        public const string InhaleLoop = "inhaleloop";
        public const string InhaleEnd = "inhaleend";
        public const string Exhale = "exhale";
        public const string Spit = "spit";

        public const string Hurt = "hurt";
        public const string Faint = "faint";
        public const string DeadSide = "deadside";
        public const string DeadUp = "deadup";
        public const string DeadDown = "deaddown";

        public const string Attack = "attack";
        public const string SwordAttack = "sword_attack";
        public const string SwordAttack1 = "sword_attack1";
        public const string SwordAttack2 = "sword_attack2";
        public const string BeamAttack = "beam_attack";
        public const string ArcherAttack = "archer_attack";

        public const string TransformIn = "transform_in";
        public const string StarMorph = "starMorph";
        public const string IdleMouthful = "idlemouthful";
        public const string WalkMouthful = "walkmouthful";

        public const string ClimbUp = "climbup";
        public const string WallSlide = "wallslide";
        public const string ClimbPull = "climbPull";
        public const string ClimbPush = "climbPush";

        public const string SwimIdle = "swimIdle";
        public const string SwimUp = "swimUp";
        public const string SwimDown = "swimDown";
        public const string Sleep = "sleep";
        public const string Asleep = "asleep";
        public const string WakeUp = "wakeUp";
        public const string HalfWakeUp = "halfWakeUp";
        public const string StartStarFly = "startStarFly";
        public const string StarFly = "starFly";

        public const string FireIdle = "fire_idle";
        public const string FireWalk = "fire_walk";
        public const string FireAttack = "fire_attack";
        public const string FireBurst = "fire_burst";

        public const string IceIdle = "ice_idle";
        public const string IceWalk = "ice_walk";
        public const string IceAttack = "ice_attack";
        public const string IceFreeze = "ice_freeze";

        public const string SparkIdle = "spark_idle";
        public const string SparkWalk = "spark_walk";
        public const string SparkAttack = "spark_attack";
        public const string SparkChain = "spark_chain";

        public const string StoneIdle = "stone_idle";
        public const string StoneWalk = "stone_walk";
        public const string StoneAttack = "stone_attack";
        public const string StoneCrush = "stone_crush";
        public const string StoneTransform = "stone_transform";

        public const string SwordIdle = "sword_idle";
        public const string SwordWalk = "sword_walk";

        public const string BeamIdle = "beam_idle";
        public const string BeamWalk = "beam_walk";
        public const string BeamWhip = "beam_whip";

        public const string CutterIdle = "cutter_idle";
        public const string CutterWalk = "cutter_walk";
        public const string CutterAttack = "cutter_attack";
        public const string CutterThrow = "cutter_throw";

        public const string HammerIdle = "hammer_idle";
        public const string HammerWalk = "hammer_walk";
        public const string HammerAttack = "hammer_attack";
        public const string HammerSlam = "hammer_slam";

        public const string WingIdle = "wing_idle";
        public const string WingAttack = "wing_attack";
        public const string WingDive = "wing_dive";

        public const string ArcherIdle = "archer_idle";
        public const string ArcherShoot = "archer_shoot";

        public const string LeafIdle = "leaf_idle";
        public const string LeafAttack = "leaf_attack";
        public const string LeafBurst = "leaf_burst";

        public const string WaterIdle = "water_idle";
        public const string WaterWalk = "water_walk";
        public const string WaterAttack = "water_attack";
        public const string WaterWave = "water_wave";

        public const string MirrorIdle = "mirror_idle";
        public const string MirrorAttack = "mirror_attack";
        public const string MirrorReflect = "mirror_reflect";

        public const string EspIdle = "esp_idle";
        public const string EspWalk = "esp_walk";
        public const string EspAttack = "esp_attack";

        public const string CombatBackflip = "combat_backflip";
        public const string CombatPunchA = "combat_punchA";
        public const string CombatPunchB = "combat_punchB";
        public const string CombatGroundPound = "combat_groundpound";
        public const string CombatGrabEnemy = "combat_grab_enemy";
        public const string CombatPreDeath = "combat_pre_death";
        public const string CombatMidDeath = "combat_mid_death";
        public const string CombatPostDeath = "combat_post_death";
    }
}
