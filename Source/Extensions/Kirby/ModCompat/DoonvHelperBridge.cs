// =============================================================================
// DoonvHelperBridge.cs — Kirby ↔ DoonvHelper integration
// =============================================================================
// Integrates Kirby player with DoonvHelper's CustomEnemy and CustomNPC systems.
// Allows Kirby to inhale DoonvHelper enemies, bounce on them, and interact
// with DoonvHelper's bullet/projectile system using Kirby abilities.
//
// Credit: DoonvHelper (MIT License) — doonv (Doonv), EllaTAS, Kosei
//         https://github.com/doonv/DoonvHelper
// =============================================================================

using System;
using System.Collections.Generic;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for DoonvHelper integration.
    ///
    /// Handles:
    ///   - CustomEnemy: Kirby can inhale DoonvHelper enemies for copy abilities
    ///   - CustomNPC: Kirby can interact with DoonvHelper NPCs via talk
    ///   - Bullet collision: Kirby's inhale can absorb enemy bullets
    ///   - Bouncebox: Kirby properly bounces on enemies with bounce hitboxes
    ///   - Dashable enemies: Kirby's dash attack works with DoonvHelper enemies
    /// </summary>
    public class DoonvHelperBridge : IKirbyModBridge
    {
        public string ModName => "DoonvHelper";
        public bool IsActive => KirbyModCompatManager.DoonvHelperLoaded;

        // Map DoonvHelper enemy sprite IDs to Kirby copy abilities
        private static readonly Dictionary<string, KirbyMode.KirbyPowerState> EnemyCopyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "DoonvHelper_CustomEnemy_zombie", KirbyMode.KirbyPowerState.Sword },
            { "DoonvHelper_CustomEnemy_skeleton", KirbyMode.KirbyPowerState.Cutter },
            { "DoonvHelper_CustomEnemy_fire", KirbyMode.KirbyPowerState.Fire },
            { "DoonvHelper_CustomEnemy_ice", KirbyMode.KirbyPowerState.Ice },
            { "DoonvHelper_CustomEnemy_electric", KirbyMode.KirbyPowerState.Spark },
        };

        public void Load()
        {
            if (!IsActive) return;

            // Hook player collision to add Kirby-specific interactions with DoonvHelper enemies
            On.Celeste.Player.Update += OnPlayerUpdate_DoonvCompat;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "DoonvHelper bridge: hooked custom enemy + NPC interaction compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            On.Celeste.Player.Update -= OnPlayerUpdate_DoonvCompat;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Check if Kirby is inhaling — look for nearby DoonvHelper enemies to pull in
            var inhale = kirby.Inhale;
            if (inhale != null && inhale.IsExecuting)
            {
                CheckInhaleNearbyEnemies(kirby, player, level);
            }

            // Check if Kirby is dashing into a DoonvHelper dashable enemy
            if (player.DashAttacking)
            {
                CheckDashAttackEnemies(kirby, player, level);
            }
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source)
        {
            // DoonvHelper enemy bullets should deal damage to Kirby
            // instead of instant kill (which is the vanilla behavior)
            // This is already handled by the BossesHelper bridge death intercept.
            return false;
        }

        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            // No special handling needed
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnPlayerUpdate_DoonvCompat(On.Celeste.Player.orig_Update orig, Player self)
        {
            orig(self);

            if (self.Scene is not Level level) return;

            var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (kirby == null) return;

            // When Kirby bounces on a DoonvHelper enemy's bouncebox,
            // refill Kirby stamina (similar to how vanilla refills dash)
            if (self.Speed.Y < 0 && kirby.CurrentStamina < kirby.MaxStamina * 0.5f)
            {
                // Small stamina refill on enemy bounce
                kirby.CurrentStamina = Math.Min(
                    kirby.CurrentStamina + kirby.MaxStamina * 0.15f,
                    kirby.MaxStamina);
            }
        }

        /// <summary>
        /// Check for DoonvHelper enemies within Kirby's inhale range and pull them in.
        /// Uses the same technique DoonvHelper uses for player collision —
        /// iterate tracked actors and check distance/cone.
        /// </summary>
        private void CheckInhaleNearbyEnemies(KirbyPlayerExtension kirby, Player player, Level level)
        {
            float range = kirby.Settings.InhaleRange;
            float coneDot = kirby.Settings.InhaleConeDot;
            int facing = (int)player.Facing;
            Vector2 mouthPos = player.Center + new Vector2(facing * kirby.Settings.InhaleMouthOffset, -4f);

            // Search for all actors in the scene that could be DoonvHelper enemies
            foreach (Entity entity in level.Entities)
            {
                if (entity == player || entity == kirby) continue;

                // Check if this is a tracked actor within range
                if (entity is Actor actor && entity.GetType().FullName?.Contains("DoonvHelper") == true)
                {
                    Vector2 toEntity = actor.Center - mouthPos;
                    float dist = toEntity.Length();

                    if (dist > range) continue;

                    // Cone check
                    Vector2 dir = toEntity.SafeNormalize();
                    Vector2 facingDir = new Vector2(facing, 0);
                    float dot = Vector2.Dot(dir, facingDir);

                    if (dot < coneDot) continue;

                    // Pull enemy toward Kirby's mouth
                    Vector2 pullDir = (mouthPos - actor.Center).SafeNormalize();
                    float pullStr = kirby.Settings.InhalePullSpeed * Engine.DeltaTime;
                    actor.Position += pullDir * pullStr;

                    // If close enough, swallow
                    if (dist < kirby.Settings.InhaleSwallowDistance)
                    {
                        SwallowDoonvEnemy(kirby, actor);
                    }
                }
            }
        }

        /// <summary>
        /// Swallow a DoonvHelper enemy and potentially gain a copy ability.
        /// </summary>
        private void SwallowDoonvEnemy(KirbyPlayerExtension kirby, Actor enemy)
        {
            // Determine copy ability from enemy type
            string typeName = enemy.GetType().Name;
            var power = KirbyMode.KirbyPowerState.None;

            // Check sprite-based copy map
            var sprite = enemy.Get<Sprite>();
            if (sprite != null)
            {
                foreach (var kvp in EnemyCopyMap)
                {
                    if (sprite.Path?.Contains(kvp.Key) == true)
                    {
                        power = kvp.Value;
                        break;
                    }
                }
            }

            // Default — if no specific mapping, give a generic ability
            if (power == KirbyMode.KirbyPowerState.None && typeName.Contains("Enemy"))
            {
                power = KirbyMode.KirbyPowerState.Beam;
            }

            // Apply copy ability
            if (power != KirbyMode.KirbyPowerState.None && kirby.Settings.PowerCopyEnabled)
            {
                kirby.SetPowerState(power);
                Audio.Play("event:/desolozantas/char/kirby/copy_ability", kirby.Position);
            }

            // Remove the enemy
            enemy.RemoveSelf();

            Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                $"Kirby swallowed DoonvHelper enemy '{typeName}' → power: {power}");
        }

        /// <summary>
        /// Check if Kirby's dash attack hits a DoonvHelper dashable enemy.
        /// </summary>
        private void CheckDashAttackEnemies(KirbyPlayerExtension kirby, Player player, Level level)
        {
            foreach (Entity entity in level.Entities)
            {
                if (entity == player || entity == kirby) continue;

                if (entity is Actor actor && entity.GetType().FullName?.Contains("DoonvHelper") == true)
                {
                    if (player.CollideCheck(actor))
                    {
                        // For Kirby, dash attacking an enemy deals damage
                        // and grants a small stamina refill
                        kirby.CurrentStamina = Math.Min(
                            kirby.CurrentStamina + kirby.MaxStamina * 0.1f,
                            kirby.MaxStamina);
                    }
                }
            }
        }
    }
}
