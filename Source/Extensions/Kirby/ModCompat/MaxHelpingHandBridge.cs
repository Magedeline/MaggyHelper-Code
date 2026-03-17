// =============================================================================
// MaxHelpingHandBridge.cs — Kirby ↔ MaxHelpingHand (Maddie's Helping Hand)
// =============================================================================
// Integrates Kirby player with MaxHelpingHand's dash count modification,
// flag-based refill systems, and custom trigger behaviors.
//
// Credit: Maddie's Helping Hand (MIT License) — maddie480 & contributors
//         https://github.com/maddie480/MaddieHelpingHand
// =============================================================================

using System;
using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Extensions.Kirby.ModCompat
{
    /// <summary>
    /// Bridge for MaxHelpingHand integration.
    ///
    /// Handles:
    ///   - MaxDashes triggers: when dash count is modified, scale Kirby's
    ///     hover stamina and ability cooldowns proportionally
    ///   - Flag-based refills: sync Kirby ability state with flag refill triggers
    ///   - Custom dash refills: restore Kirby power along with dashes
    ///   - Inventory modification: Kirby abilities adapt to inventory state
    /// </summary>
    public class MaxHelpingHandBridge : IKirbyModBridge
    {
        public string ModName => "MaxHelpingHand";
        public bool IsActive => KirbyModCompatManager.MaxHelpingHandLoaded;

        private int _previousDashCount = -1;

        public void Load()
        {
            if (!IsActive) return;

            // Hook dash refill to also restore Kirby stamina
            On.Celeste.Player.RefillDash += OnRefillDash_MaxHH;
            On.Celeste.Player.UseRefill += OnUseRefill_MaxHH;

            Logger.Log(LogLevel.Info, "KirbyModCompat",
                "MaxHelpingHand bridge: hooked dash count + flag refill compat");
        }

        public void Unload()
        {
            if (!IsActive) return;

            On.Celeste.Player.RefillDash -= OnRefillDash_MaxHH;
            On.Celeste.Player.UseRefill -= OnUseRefill_MaxHH;
        }

        public void UpdateKirby(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (!IsActive || kirby == null || player == null) return;

            // Detect dash count changes (MaxHelpingHand can change this via triggers)
            if (player.Dashes != _previousDashCount)
            {
                int newCount = player.Dashes;
                if (_previousDashCount >= 0)
                {
                    OnDashCountActuallyChanged(kirby, player, _previousDashCount, newCount);
                }
                _previousDashCount = newCount;
            }

            // Check flag-based refills — MaxHH uses session flags
            // like "refill_dash", "refill_stamina" etc.
            CheckFlagRefills(kirby, player, level);
        }

        public bool OnDamage(KirbyPlayerExtension kirby, int amount, Vector2 source) => false;
        public bool OnDeath(KirbyPlayerExtension kirby, Player player) => false;

        public void OnDashCountChanged(KirbyPlayerExtension kirby, Player player, int newCount)
        {
            if (!IsActive || kirby == null) return;

            // Scale Kirby hover stamina proportionally to dash count
            // More dashes = more hover fuel
            float staminaBonus = (newCount - 1) * (kirby.MaxStamina * 0.15f);
            float targetStamina = Math.Min(kirby.MaxStamina + staminaBonus, kirby.MaxStamina * 2f);
            kirby.CurrentStamina = Math.Min(kirby.CurrentStamina + staminaBonus, targetStamina);
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void OnDashCountActuallyChanged(
            KirbyPlayerExtension kirby, Player player, int oldCount, int newCount)
        {
            if (newCount > oldCount)
            {
                // Gained dashes — give Kirby a stamina boost
                float bonus = (newCount - oldCount) * (kirby.MaxStamina * 0.2f);
                kirby.CurrentStamina = Math.Min(kirby.CurrentStamina + bonus, kirby.MaxStamina);

                Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                    $"MaxHH dash increase {oldCount}→{newCount}, Kirby stamina +{bonus:F0}");
            }
            else if (newCount < oldCount)
            {
                // Lost dashes — no stamina penalty, but log it
                Logger.Log(LogLevel.Verbose, "KirbyModCompat",
                    $"MaxHH dash decrease {oldCount}→{newCount}");
            }

            KirbyModCompatManager.OnDashCountChanged(kirby, player, newCount);
        }

        private bool OnRefillDash_MaxHH(On.Celeste.Player.orig_RefillDash orig, Player self)
        {
            bool result = orig(self);

            if (result && self.Scene is Level level)
            {
                var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
                if (kirby != null)
                {
                    // MaxHH refills often give more than vanilla — give Kirby
                    // proportional stamina recovery
                    float staminaRefill = kirby.MaxStamina * 0.3f;
                    kirby.CurrentStamina = Math.Min(
                        kirby.CurrentStamina + staminaRefill, kirby.MaxStamina);
                }
            }

            return result;
        }

        private bool OnUseRefill_MaxHH(On.Celeste.Player.orig_UseRefill orig, Player self, bool twoDashes)
        {
            bool result = orig(self, twoDashes);

            if (result && self.Scene is Level level)
            {
                var kirby = level.Tracker.GetEntity<KirbyPlayerExtension>();
                if (kirby != null)
                {
                    // Two-dash refills give more stamina than one-dash
                    float staminaRefill = twoDashes
                        ? kirby.MaxStamina * 0.5f
                        : kirby.MaxStamina * 0.25f;
                    kirby.CurrentStamina = Math.Min(
                        kirby.CurrentStamina + staminaRefill, kirby.MaxStamina);

                    // Also reduce any active ability cooldowns
                    foreach (var ability in kirby.AbilityManager.All)
                    {
                        ability.Cooldown = Math.Max(0f, ability.Cooldown - 0.5f);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Check if MaxHelpingHand flag-based refills have been triggered
        /// and sync Kirby state accordingly.
        /// </summary>
        private void CheckFlagRefills(KirbyPlayerExtension kirby, Player player, Level level)
        {
            if (level?.Session == null) return;

            // Check for custom flag-based refill patterns used by MaxHH
            if (level.Session.GetFlag("kirby_full_refill"))
            {
                kirby.CurrentStamina = kirby.MaxStamina;
                kirby.Heal(kirby.MaxHealth);
                foreach (var ability in kirby.AbilityManager.All)
                {
                    ability.Cooldown = 0f;
                }
                level.Session.SetFlag("kirby_full_refill", false);
            }

            if (level.Session.GetFlag("kirby_stamina_refill"))
            {
                kirby.CurrentStamina = kirby.MaxStamina;
                level.Session.SetFlag("kirby_stamina_refill", false);
            }

            if (level.Session.GetFlag("kirby_health_refill"))
            {
                kirby.Heal(kirby.MaxHealth);
                level.Session.SetFlag("kirby_health_refill", false);
            }
        }
    }
}
