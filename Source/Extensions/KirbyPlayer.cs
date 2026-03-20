using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using MaggyHelper.Entities;
using MaggyHelper.Entities.Kirby;
using MaggyHelper.Extensions.Kirby;
using MaggyHelper.Extensions.Kirby.Core;

namespace MaggyHelper.Extensions;

/// <summary>
/// Legacy compatibility shim for the old monolithic KirbyPlayer actor.
///
/// The original implementation duplicated a large portion of Celeste Player core.
/// This shim now delegates to the new Kirby runtime stack:
/// - KirbyPlayerExtension (primary)
/// - KirbyMode (legacy fallback)
/// via KirbyPlayerCore.
/// </summary>
[Tracked(false)]
public class KirbyPlayer : Actor
{
    private readonly KirbyPlayerCore _core = new();
    private static readonly List<KirbyActorBase> EmptyInhaledEntities = new();
    private Vector2 _cachedSpeed;

    public KirbyPlayer(Vector2 position, PlayerSpriteMode spriteMode = PlayerSpriteMode.Madeline)
        : base(position)
    {
        Visible = false;
        Tag = Tags.Persistent | Tags.TransitionUpdate;
    }

    public int CurrentHealth
    {
        get
        {
            Level level = Scene as Level;
            return level == null ? 0 : _core.GetHealth(level);
        }
    }

    public KirbyMode.KirbyPowerState CurrentPower
    {
        get
        {
            Level level = Scene as Level;
            return level == null ? KirbyMode.KirbyPowerState.None : _core.GetPowerState(level);
        }
    }

    public int MaxHealth
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return 0;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext != null)
            {
                return ext.MaxHealth;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.MaxHealth ?? 0;
        }
    }

    public bool IsDead
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return false;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext != null)
            {
                return ext.IsDead;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.IsDead == true;
        }
    }

    public bool IsInhaling
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return false;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext?.Inhale != null)
            {
                return ext.Inhale.IsInhaling;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.IsInhaling == true;
        }
    }

    public bool IsDashing
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return false;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext != null)
            {
                return ext.IsDashing;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.IsDashing == true;
        }
    }

    public Facings Facing
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return Facings.Right;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext != null)
            {
                return ext.Facing;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.Facing ?? Facings.Right;
        }
    }

    public List<KirbyActorBase> InhaledEntities
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return EmptyInhaledEntities;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext?.Inhale != null)
            {
                return ext.Inhale.InhaledEntities;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            return legacy?.InhaledEntities ?? EmptyInhaledEntities;
        }
    }

    public Vector2 Speed
    {
        get
        {
            Level level = Scene as Level;
            if (level == null)
            {
                return _cachedSpeed;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext?.Player != null)
            {
                return ext.Player.Speed;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            if (legacy != null)
            {
                return legacy.Speed;
            }

            return _cachedSpeed;
        }
        set
        {
            _cachedSpeed = value;

            Level level = Scene as Level;
            if (level == null)
            {
                return;
            }

            KirbyPlayerExtension ext = _core.GetExtension(level);
            if (ext?.Player != null)
            {
                ext.Player.Speed = value;
            }

            KirbyMode legacy = _core.GetLegacy(level);
            if (legacy != null)
            {
                legacy.Speed = value;
            }
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        // If any old map/entity still spawns this class directly, route it to the
        // modern Kirby system and keep this actor inert as a compatibility shell.
        if (scene is Level level)
        {
            Player player = level.Tracker.GetEntity<Player>();
            if (player != null && !player.IsKirbyMode())
            {
                player.EnableKirbyMode();
            }
        }
    }

    public void EnablePlayerSync()
    {
        Level level = Scene as Level;
        if (level == null)
        {
            return;
        }

        KirbyPlayerExtension ext = _core.GetExtension(level);
        if (ext != null)
        {
            ext.EnablePlayerSync();
            return;
        }

        KirbyMode legacy = _core.GetLegacy(level);
        legacy?.EnablePlayerSync();
    }

    public void DisablePlayerSync()
    {
        Level level = Scene as Level;
        if (level == null)
        {
            return;
        }

        KirbyPlayerExtension ext = _core.GetExtension(level);
        if (ext != null)
        {
            ext.DisablePlayerSync();
            return;
        }

        KirbyMode legacy = _core.GetLegacy(level);
        legacy?.DisablePlayerSync();
    }

    public bool TakeDamage(int amount = 1)
    {
        Level level = Scene as Level;
        if (level == null)
        {
            return false;
        }

        KirbyPlayerExtension ext = _core.GetExtension(level);
        if (ext != null)
        {
            return ext.TakeDamage(amount);
        }

        KirbyMode legacy = _core.GetLegacy(level);
        if (legacy == null)
        {
            return false;
        }

        legacy.TakeDamage(amount);
        return true;
    }

    public void Heal(int amount = 1)
    {
        Level level = Scene as Level;
        if (level != null)
        {
            _core.Heal(level, amount);
        }
    }

    public void SetPowerState(KirbyMode.KirbyPowerState power)
    {
        Level level = Scene as Level;
        if (level != null)
        {
            _core.SetPowerState(level, power);
        }
    }

    public void SaveToSession()
    {
        Level level = Scene as Level;
        if (level != null)
        {
            _core.SaveOnLevelUnloaded(level);
        }
    }
}
