using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;

namespace MaggyHelper.Extensions.Kirby.Core;

/// <summary>
/// Core Kirby player lifecycle logic.
///
/// Responsibility:
/// - Spawn/activate/deactivate Kirby runtime entities
/// - Persist runtime state across room transitions
/// - Provide a unified API for health/power operations
///
/// This intentionally mirrors the Aqua pattern where a small "core" object
/// owns behavior while the module class wires lifecycle events.
/// </summary>
public sealed class KirbyPlayerCore
{
    private const string SfxPath = "event:/desolozantas/char/kirby/";
    private const string SfxTransform = SfxPath + "transform_in";
    private const string SfxDetransform = SfxPath + "transform_out";

    public void Enable(Player player, Level level)
    {
        Audio.Play(SfxTransform, player.Position);
        level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 20, player.Position, Vector2.One * 16f);

        SpawnOrActivateExtension(player, level);
        SpawnOrActivateLegacy(player, level);

        var state = LevelStateManager.GetState();
        if (state != null)
        {
            state.KirbyModeEnabled = true;
        }
    }

    public void Disable(Player player, Level level)
    {
        Audio.Play(SfxDetransform, player.Position);
        level.ParticlesFG?.Emit(ParticleTypes.Dust, 10, player.Position, Vector2.One * 12f);

        DeactivateExtension(level);
        DeactivateLegacy(level);

        var state = LevelStateManager.GetState();
        if (state != null)
        {
            state.KirbyModeEnabled = false;
        }
    }

    public void RestoreOnLevelLoaded(Level level)
    {
        var state = LevelStateManager.GetState();
        if (state == null || !state.KirbyModeEnabled)
        {
            return;
        }

        var player = level.Tracker.GetEntity<Player>();
        if (player == null)
        {
            return;
        }

        SpawnOrActivateExtension(player, level);
        SpawnOrActivateLegacy(player, level);
    }

    public void SaveOnLevelUnloaded(Level level)
    {
        var ext = GetExtension(level);
        if (ext != null)
        {
            ext.SaveToSession();
            return;
        }

        var legacy = GetLegacy(level);
        legacy?.SaveToSession();
    }

    public KirbyPlayerExtension GetExtension(Level level)
    {
        return level?.Tracker.GetEntity<KirbyPlayerExtension>();
    }

    public KirbyPlayerExtension GetExtension(Scene scene)
    {
        return (scene as Level)?.Tracker.GetEntity<KirbyPlayerExtension>();
    }

    public KirbyMode GetLegacy(Level level)
    {
        return level?.Tracker.GetEntity<KirbyMode>();
    }

    public KirbyMode.KirbyPowerState GetPowerState(Level level)
    {
        var ext = GetExtension(level);
        if (ext != null)
        {
            return ext.CurrentPower;
        }

        var legacy = GetLegacy(level);
        return legacy?.CurrentPower ?? KirbyMode.KirbyPowerState.None;
    }

    public void SetPowerState(Level level, KirbyMode.KirbyPowerState power)
    {
        var ext = GetExtension(level);
        ext?.SetPowerState(power);

        var legacy = GetLegacy(level);
        legacy?.SetPowerState(power);

        var state = LevelStateManager.GetState();
        if (state != null)
        {
            state.KirbyPower = power;
        }
    }

    public int GetHealth(Level level)
    {
        var ext = GetExtension(level);
        if (ext != null)
        {
            return ext.CurrentHealth;
        }

        var legacy = GetLegacy(level);
        return legacy?.CurrentHealth ?? 0;
    }

    public void Heal(Level level, int amount = 1)
    {
        var ext = GetExtension(level);
        if (ext != null)
        {
            ext.Heal(amount);
            return;
        }

        var legacy = GetLegacy(level);
        legacy?.Heal(amount);
    }

    public void Damage(Level level, int amount = 1)
    {
        var ext = GetExtension(level);
        if (ext != null)
        {
            ext.TakeDamage(amount);
            return;
        }

        var legacy = GetLegacy(level);
        legacy?.TakeDamage(amount);
    }

    private void SpawnOrActivateExtension(Player player, Level level)
    {
        var existing = level.Tracker.GetEntity<KirbyPlayerExtension>();
        if (existing == null)
        {
            var ext = new KirbyPlayerExtension(player.Position);
            level.Add(ext);
            ext.EnablePlayerSync();
            IngesteLogger.Debug("KirbyPlayerCore: Spawned KirbyPlayerExtension");
            return;
        }

        existing.Active = true;
        existing.Visible = true;
        existing.Position = player.Position;
        existing.EnablePlayerSync();
        IngesteLogger.Debug("KirbyPlayerCore: Reactivated KirbyPlayerExtension");
    }

    private void DeactivateExtension(Level level)
    {
        var ext = level.Tracker.GetEntity<KirbyPlayerExtension>();
        if (ext == null)
        {
            return;
        }

        ext.DisablePlayerSync();
        ext.SaveToSession();
        ext.Active = false;
        ext.Visible = false;
        IngesteLogger.Debug("KirbyPlayerCore: Deactivated KirbyPlayerExtension");
    }

    private void SpawnOrActivateLegacy(Player player, Level level)
    {
        var existing = level.Tracker.GetEntity<KirbyMode>();
        if (existing == null)
        {
            var kirby = new KirbyMode(player.Position);
            level.Add(kirby);
            kirby.EnablePlayerSync();
            return;
        }

        existing.Active = true;
        existing.Visible = true;
        existing.Position = player.Position;
        existing.EnablePlayerSync();
    }

    private void DeactivateLegacy(Level level)
    {
        var kirby = level.Tracker.GetEntity<KirbyMode>();
        if (kirby == null)
        {
            return;
        }

        kirby.DisablePlayerSync();
        kirby.SaveToSession();
        kirby.Active = false;
        kirby.Visible = false;
    }
}
