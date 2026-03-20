using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;

namespace MaggyHelper.Extensions.Kirby.Core;

/// <summary>
/// Core hook layer for Kirby-specific player interactions.
///
/// Aqua-style approach:
/// - Explicit Hook/Unhook entry points
/// - Stateless hook methods delegating gameplay state to KirbyPlayerCore
/// - Character-module agnostic runtime checks
/// </summary>
public sealed class KirbyPlayerExtensionCore
{
    private const string SfxPath = "event:/desolozantas/char/kirby/";
    private const string SfxHurt = SfxPath + "predeath";
    private const string SfxDie = SfxPath + "predeath";
    private const string SfxBounce = SfxPath + "bounce";
    private static readonly string[] InstantKillHazardKeywords =
    {
        "Spinner",
        "Blade",
        "Crush",
        "CrushingBlock",
        "CrushBlock",
        "Spikes",
        "Spike",
        "Saw"
    };

    private readonly KirbyPlayerCore _playerCore;
    private bool _hooked;

    public KirbyPlayerExtensionCore(KirbyPlayerCore playerCore)
    {
        _playerCore = playerCore;
    }

    public void Hook()
    {
        if (_hooked)
        {
            return;
        }

        On.Celeste.Player.Die += PlayerOnDie;
        On.Celeste.Player.OnCollideH += PlayerOnCollideH;
        On.Celeste.Player.OnCollideV += PlayerOnCollideV;

        _hooked = true;
    }

    public void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        On.Celeste.Player.Die -= PlayerOnDie;
        On.Celeste.Player.OnCollideH -= PlayerOnCollideH;
        On.Celeste.Player.OnCollideV -= PlayerOnCollideV;

        _hooked = false;
    }

    public void OnLevelLoaded(Level level)
    {
        _playerCore.RestoreOnLevelLoaded(level);
    }

    public void OnLevelUnloaded(Level level)
    {
        _playerCore.SaveOnLevelUnloaded(level);
    }

    private PlayerDeadBody PlayerOnDie(
        On.Celeste.Player.orig_Die orig,
        Player self,
        Vector2 direction,
        bool evenIfInvincible,
        bool registerDeathInStats)
    {
        if (!IsKirbyActive(self))
        {
            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        // Spinner/blade/crush-style hazards should stay lethal in one hit,
        // otherwise Kirby health buffering can leave the player trapped in hazard geometry.
        if (IsInstantKillHazardContact(self, true))
        {
            Audio.Play(SfxDie, self.Position);
            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        var ext = _playerCore.GetExtension(self.Scene);
        if (ext != null && ext.CurrentHealth > 1)
        {
            ext.TakeDamage(1);
            self.Position -= direction * 4f;
            Audio.Play(SfxHurt, self.Position);
            return null;
        }

        var legacy = (self.Scene as Level)?.Tracker.GetEntity<KirbyMode>();
        if (legacy != null && legacy.CurrentHealth > 1)
        {
            legacy.TakeDamage(1);
            self.Position -= direction * 4f;
            Audio.Play(SfxHurt, self.Position);
            return null;
        }

        Audio.Play(SfxDie, self.Position);
        return orig(self, direction, evenIfInvincible, registerDeathInStats);
    }

    private static bool IsInstantKillHazardContact(Player player, bool includeVertical = false)
    {
        if (player?.Scene is not Level level)
        {
            return false;
        }

        foreach (Entity entity in level.Entities)
        {
            if (entity == null || entity == player || !entity.Collidable)
            {
                continue;
            }

            if (!LooksLikeInstantKillHazard(entity.GetType().Name))
            {
                continue;
            }

            if (player.CollideCheck(entity))
            {
                return true;
            }

            if (includeVertical && player.CollideCheck(entity, Vector2.UnitY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeInstantKillHazard(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        for (int i = 0; i < InstantKillHazardKeywords.Length; i++)
        {
            if (typeName.Contains(InstantKillHazardKeywords[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void PlayerOnCollideH(On.Celeste.Player.orig_OnCollideH orig, Player self, CollisionData data)
    {
        orig(self, data);

        if (!IsKirbyActive(self))
        {
            return;
        }

        if (self.StateMachine.State != PlayerCharacterStates.StKirbySlide)
        {
            return;
        }

        if (!IsPotentialEnemy(data.Hit))
        {
            return;
        }

        // Intentionally left as a bridge point for enemy damage systems.
    }

    private void PlayerOnCollideV(On.Celeste.Player.orig_OnCollideV orig, Player self, CollisionData data)
    {
        orig(self, data);

        if (!IsKirbyActive(self))
        {
            return;
        }

        if (self.Speed.Y <= 0f || self.Scene == null)
        {
            return;
        }

        var bounceTarget = self.CollideFirst<Actor>(self.Position + Vector2.UnitY);
        if (bounceTarget == null || !bounceTarget.IsBounceable())
        {
            return;
        }

        self.Speed.Y = -120f;
        Audio.Play(SfxBounce, self.Position);
    }

    private static bool IsPotentialEnemy(Entity hit)
    {
        if (hit == null)
        {
            return false;
        }

        string typeName = hit.GetType().Name;
        return typeName.Contains("Enemy") || typeName.Contains("Seeker");
    }

    private static bool IsKirbyActive(Player player)
    {
        if (player?.Scene is not Level level)
        {
            return false;
        }

        return level.Session?.GetFlag("kirby_mode") == true || LevelStateManager.IsKirbyModeEnabled();
    }
}
