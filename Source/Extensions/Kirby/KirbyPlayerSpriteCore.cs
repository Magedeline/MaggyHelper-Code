using System;
using System.Collections.Generic;
using Monocle;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Core sprite helpers for Kirby player banks.
    /// Keeps runtime animation requests synchronized with Graphics/customplayersprites.xml.
    /// </summary>
    public static class KirbyPlayerSpriteCore
    {
        private const string KirbyPlayerId = "kirby_player";
        private const string KirbyPlayerExtId = "kirby_player_ext";
        private const string KirbyPrefix = "kirby_";

        private static readonly Dictionary<string, string[]> LogicalAnimCandidates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = new[] { KirbyAnimIds.Idle, KirbyAnimIds.IdleA },
            ["walk"] = new[] { KirbyAnimIds.Walk, KirbyAnimIds.RunSlow },
            ["run"] = new[] { KirbyAnimIds.RunFast, KirbyAnimIds.RunSlow, KirbyAnimIds.Walk },
            ["jump"] = new[] { KirbyAnimIds.JumpFast, KirbyAnimIds.JumpSlow },
            ["fall"] = new[] { KirbyAnimIds.FallFast, KirbyAnimIds.FallSlow, KirbyAnimIds.Fall },
            ["dash"] = new[] { KirbyAnimIds.Dash, KirbyAnimIds.RunFast },
            ["inhale"] = new[] { KirbyAnimIds.Inhale, KirbyAnimIds.InhaleBegin, KirbyAnimIds.InhaleLoop },
            ["hover"] = new[] { KirbyAnimIds.Hover, KirbyAnimIds.Float, KirbyAnimIds.Idle },
            ["slide"] = new[] { KirbyAnimIds.Slide, KirbyAnimIds.Duck },
            ["death"] = new[] { KirbyAnimIds.DeadSide, KirbyAnimIds.DeadUp, KirbyAnimIds.DeadDown, KirbyAnimIds.Faint },
            ["damage"] = new[] { KirbyAnimIds.Hurt, KirbyAnimIds.Faint },
            ["spit"] = new[] { KirbyAnimIds.Spit, KirbyAnimIds.Exhale },
            ["melee"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.SwordAttack },
            ["melee_up"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.SwordAttack1 },
            ["melee_down"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.SwordAttack2 },
            ["range"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.BeamAttack, KirbyAnimIds.ArcherAttack },
            ["swallow"] = new[] { KirbyAnimIds.InhaleEnd, KirbyAnimIds.Exhale },
            ["copy"] = new[] { KirbyAnimIds.TransformIn, KirbyAnimIds.StarMorph },
            ["mouthful"] = new[] { KirbyAnimIds.IdleMouthful, KirbyAnimIds.WalkMouthful, KirbyAnimIds.Idle },

            // Movement/state-specific aliases
            ["climb"] = new[] { KirbyAnimIds.ClimbUp, KirbyAnimIds.WallSlide, KirbyAnimIds.ClimbPull, KirbyAnimIds.ClimbPush },
            ["swim"] = new[] { KirbyAnimIds.SwimIdle, KirbyAnimIds.SwimUp, KirbyAnimIds.SwimDown },
            ["sleep"] = new[] { KirbyAnimIds.Sleep, KirbyAnimIds.Asleep },
            ["wake"] = new[] { KirbyAnimIds.WakeUp, KirbyAnimIds.HalfWakeUp },
            ["starfly"] = new[] { KirbyAnimIds.StartStarFly, KirbyAnimIds.StarFly },

            // Common power action aliases
            ["fire"] = new[] { KirbyAnimIds.FireAttack, KirbyAnimIds.FireBurst, KirbyAnimIds.FireIdle },
            ["ice"] = new[] { KirbyAnimIds.IceAttack, KirbyAnimIds.IceFreeze, KirbyAnimIds.IceIdle },
            ["spark"] = new[] { KirbyAnimIds.SparkAttack, KirbyAnimIds.SparkChain, KirbyAnimIds.SparkIdle },
            ["stone"] = new[] { KirbyAnimIds.StoneAttack, KirbyAnimIds.StoneTransform, KirbyAnimIds.StoneIdle },
            ["sword"] = new[] { KirbyAnimIds.SwordAttack, KirbyAnimIds.SwordAttack1, KirbyAnimIds.SwordAttack2, KirbyAnimIds.SwordIdle },
            ["beam"] = new[] { KirbyAnimIds.BeamAttack, KirbyAnimIds.BeamWhip, KirbyAnimIds.BeamIdle },
            ["cutter"] = new[] { KirbyAnimIds.CutterAttack, KirbyAnimIds.CutterThrow, KirbyAnimIds.CutterIdle },
            ["hammer"] = new[] { KirbyAnimIds.HammerAttack, KirbyAnimIds.HammerSlam, KirbyAnimIds.HammerIdle },
            ["wing"] = new[] { KirbyAnimIds.WingAttack, KirbyAnimIds.WingDive, KirbyAnimIds.WingIdle },
            ["archer"] = new[] { KirbyAnimIds.ArcherAttack, KirbyAnimIds.ArcherShoot, KirbyAnimIds.ArcherIdle },
            ["leaf"] = new[] { KirbyAnimIds.LeafAttack, KirbyAnimIds.LeafBurst, KirbyAnimIds.LeafIdle },
            ["water"] = new[] { KirbyAnimIds.WaterAttack, KirbyAnimIds.WaterWave, KirbyAnimIds.WaterIdle },
            ["mirror"] = new[] { KirbyAnimIds.MirrorAttack, KirbyAnimIds.MirrorReflect, KirbyAnimIds.MirrorIdle },
            ["esp"] = new[] { KirbyAnimIds.EspAttack, KirbyAnimIds.EspIdle }
        };

        // Legacy runtime ids (kirby_*) mapped to full customplayersprites.xml ids.
        private static readonly Dictionary<string, string[]> LegacyKirbyAnimCandidates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["idle"] = new[] { KirbyAnimIds.Idle },
            ["walk"] = new[] { KirbyAnimIds.Walk },
            ["run"] = new[] { KirbyAnimIds.RunFast, KirbyAnimIds.RunSlow },
            ["jump"] = new[] { KirbyAnimIds.JumpFast, KirbyAnimIds.JumpSlow },
            ["fall"] = new[] { KirbyAnimIds.Fall, KirbyAnimIds.FallFast, KirbyAnimIds.FallSlow },
            ["dash"] = new[] { KirbyAnimIds.Dash, KirbyAnimIds.RunFast },
            ["inhale"] = new[] { KirbyAnimIds.Inhale, KirbyAnimIds.InhaleBegin, KirbyAnimIds.InhaleLoop },
            ["hover"] = new[] { KirbyAnimIds.Hover, KirbyAnimIds.Float },
            ["slide"] = new[] { KirbyAnimIds.Slide },
            ["death"] = new[] { KirbyAnimIds.DeadSide, KirbyAnimIds.DeadDown, KirbyAnimIds.DeadUp, KirbyAnimIds.Faint },
            ["damage"] = new[] { KirbyAnimIds.Hurt, KirbyAnimIds.Faint },
            ["spit"] = new[] { KirbyAnimIds.Spit, KirbyAnimIds.Exhale },
            ["melee"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.CombatPunchA },
            ["melee_up"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.CombatPunchB },
            ["melee_down"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.CombatGroundPound },
            ["range"] = new[] { KirbyAnimIds.Attack, KirbyAnimIds.BeamAttack, KirbyAnimIds.ArcherAttack },
            ["swallow"] = new[] { KirbyAnimIds.InhaleEnd, KirbyAnimIds.Exhale },
            ["copy"] = new[] { KirbyAnimIds.TransformIn, KirbyAnimIds.StarMorph },
            ["mouthful"] = new[] { KirbyAnimIds.IdleMouthful, KirbyAnimIds.WalkMouthful },

            // Power aliases that existed on old kirby_player_ext banks.
            ["fire_idle"] = new[] { KirbyAnimIds.FireIdle },
            ["fire_walk"] = new[] { KirbyAnimIds.FireWalk },
            ["fire_attack"] = new[] { KirbyAnimIds.FireAttack },
            ["ice_idle"] = new[] { KirbyAnimIds.IceIdle },
            ["ice_walk"] = new[] { KirbyAnimIds.IceWalk },
            ["ice_attack"] = new[] { KirbyAnimIds.IceAttack },
            ["spark_idle"] = new[] { KirbyAnimIds.SparkIdle },
            ["spark_walk"] = new[] { KirbyAnimIds.SparkWalk },
            ["spark_attack"] = new[] { KirbyAnimIds.SparkAttack },
            ["stone_idle"] = new[] { KirbyAnimIds.StoneIdle },
            ["stone_walk"] = new[] { KirbyAnimIds.StoneWalk },
            ["stone_attack"] = new[] { KirbyAnimIds.StoneAttack, KirbyAnimIds.StoneCrush },
            ["sword_idle"] = new[] { KirbyAnimIds.SwordIdle },
            ["sword_walk"] = new[] { KirbyAnimIds.SwordWalk },
            ["sword_attack"] = new[] { KirbyAnimIds.SwordAttack, KirbyAnimIds.SwordAttack1 },
            ["beam_idle"] = new[] { KirbyAnimIds.BeamIdle },
            ["beam_walk"] = new[] { KirbyAnimIds.BeamWalk },
            ["beam_attack"] = new[] { KirbyAnimIds.BeamAttack },
            ["cutter_idle"] = new[] { KirbyAnimIds.CutterIdle },
            ["cutter_walk"] = new[] { KirbyAnimIds.CutterWalk },
            ["cutter_attack"] = new[] { KirbyAnimIds.CutterAttack, KirbyAnimIds.CutterThrow },
            ["hammer_idle"] = new[] { KirbyAnimIds.HammerIdle },
            ["hammer_walk"] = new[] { KirbyAnimIds.HammerWalk },
            ["hammer_attack"] = new[] { KirbyAnimIds.HammerAttack },
            ["wing_idle"] = new[] { KirbyAnimIds.WingIdle },
            ["wing_attack"] = new[] { KirbyAnimIds.WingAttack },
            ["archer_idle"] = new[] { KirbyAnimIds.ArcherIdle },
            ["archer_attack"] = new[] { KirbyAnimIds.ArcherAttack, KirbyAnimIds.ArcherShoot },
            ["leaf_idle"] = new[] { KirbyAnimIds.LeafIdle },
            ["leaf_attack"] = new[] { KirbyAnimIds.LeafAttack, KirbyAnimIds.LeafBurst },
            ["water_idle"] = new[] { KirbyAnimIds.WaterIdle },
            ["water_walk"] = new[] { KirbyAnimIds.WaterWalk },
            ["water_attack"] = new[] { KirbyAnimIds.WaterAttack, KirbyAnimIds.WaterWave },
            ["mirror_idle"] = new[] { KirbyAnimIds.MirrorIdle },
            ["mirror_attack"] = new[] { KirbyAnimIds.MirrorAttack, KirbyAnimIds.MirrorReflect },
            ["esp_idle"] = new[] { KirbyAnimIds.EspIdle },
            ["esp_walk"] = new[] { KirbyAnimIds.EspWalk },
            ["esp_attack"] = new[] { KirbyAnimIds.EspAttack },

            // Legacy combat/death naming.
            ["backflip"] = new[] { KirbyAnimIds.CombatBackflip },
            ["puncha"] = new[] { KirbyAnimIds.CombatPunchA },
            ["punchb"] = new[] { KirbyAnimIds.CombatPunchB },
            ["groundpound"] = new[] { KirbyAnimIds.CombatGroundPound },
            ["grab_enemy"] = new[] { KirbyAnimIds.CombatGrabEnemy },
            ["pre_death"] = new[] { KirbyAnimIds.CombatPreDeath },
            ["mid_death"] = new[] { KirbyAnimIds.CombatMidDeath },
            ["post_death"] = new[] { KirbyAnimIds.CombatPostDeath }
        };

        private static readonly Dictionary<string, string> CustomPlayerSpriteMapping = new(StringComparer.Ordinal)
        {
            { KirbyPlayerExtId, KirbyPlayerId }
        };

        public static void Initialize()
        {
            On.Celeste.PlayerSprite.CreateFramesMetadata += PlayerSprite_CreateFramesMetadata;
        }

        public static void Uninitialize()
        {
            On.Celeste.PlayerSprite.CreateFramesMetadata -= PlayerSprite_CreateFramesMetadata;
        }

        /// <summary>
        /// Resolves a logical animation id to a concrete animation that exists on the sprite.
        /// </summary>
        public static string ResolveAnimId(Sprite sprite, string logicalAnimId)
        {
            if (sprite == null || string.IsNullOrEmpty(logicalAnimId))
                return KirbyAnimIds.Idle;

            logicalAnimId = logicalAnimId.Trim();

            if (sprite.Has(logicalAnimId))
                return logicalAnimId;

            if (TryResolveLegacyKirbyAnimId(sprite, logicalAnimId, out string legacyResolved))
                return legacyResolved;

            if (LogicalAnimCandidates.TryGetValue(logicalAnimId, out string[] candidates))
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = candidates[i];
                    if (sprite.Has(candidate))
                        return candidate;
                }
            }

            // Generic fallback: kirby_xxx -> xxx when both banks share data through copy.
            if (logicalAnimId.StartsWith(KirbyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string withoutPrefix = logicalAnimId.Substring(KirbyPrefix.Length);
                if (sprite.Has(withoutPrefix))
                    return withoutPrefix;
            }

            return sprite.Has(KirbyAnimIds.Idle) ? KirbyAnimIds.Idle : logicalAnimId;
        }

        private static bool TryResolveLegacyKirbyAnimId(Sprite sprite, string logicalAnimId, out string resolved)
        {
            resolved = null;

            string legacyKey = logicalAnimId;
            if (legacyKey.StartsWith(KirbyPrefix, StringComparison.OrdinalIgnoreCase))
                legacyKey = legacyKey.Substring(KirbyPrefix.Length);

            if (!LegacyKirbyAnimCandidates.TryGetValue(legacyKey, out string[] candidates))
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (sprite.Has(candidates[i]))
                {
                    resolved = candidates[i];
                    return true;
                }
            }

            return false;
        }

        private static void PlayerSprite_CreateFramesMetadata(On.Celeste.PlayerSprite.orig_CreateFramesMetadata orig, string sprite)
        {
            orig(sprite);

            if (!CustomPlayerSpriteMapping.TryGetValue(sprite, out string customSprite))
                return;

            try
            {
                if (!GFX.SpriteBank.SpriteData.ContainsKey(sprite) || !GFX.SpriteBank.SpriteData.ContainsKey(customSprite))
                    return;

                Celeste.PlayerSprite.CreateFramesMetadata(customSprite);

                var existingAnims = GFX.SpriteBank.SpriteData[sprite].Sprite.Animations;
                var customAnims = GFX.SpriteBank.SpriteData[customSprite].Sprite.Animations;
                foreach (var pair in customAnims)
                {
                    if (!existingAnims.ContainsKey(pair.Key))
                        existingAnims.Add(pair.Key, pair.Value);
                }
            }
            catch (Exception ex)
            {
                IngesteLogger.Warn($"KirbyPlayerSpriteCore metadata merge failed for '{sprite}': {ex.Message}");
            }
        }
    }
}
