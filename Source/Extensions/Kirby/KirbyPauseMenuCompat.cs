using MaggyHelper.MaggyHelper;
using MaggyHelper.Extensions;
using System;
using System.Reflection;
using Monocle;

namespace MaggyHelper.Extensions.Kirby;

/// <summary>
/// Adds Kirby-specific quick settings to the pause menu after vanilla entries.
/// </summary>
public static class KirbyPauseMenuCompat
{
    private const string KirbyModeLabel = "Kirby Mode";
    private const string KirbyPowerHudLabel = "Kirby Power HUD";
    private const string KirbyPowerCopyLabel = "Kirby Power Copy";
    private const string KirbyPowerDropLabel = "Kirby Power Drop";
    private const string KirbyInhaleHoldLabel = "Kirby Inhale Hold";
    private const string KirbyHoverHoldLabel = "Kirby Hover Hold";
    private const string KirbyKnightModeLabel = "Kirby Knight Mode";
    private const string KirbyPowerPreviousLabel = "Previous Kirby Power";
    private const string KirbyPowerNextLabel = "Next Kirby Power";

    private static readonly KirbyMode.KirbyPowerState[] QuickCyclePowers =
    {
        KirbyMode.KirbyPowerState.None,
        KirbyMode.KirbyPowerState.Sword,
        KirbyMode.KirbyPowerState.Fire,
        KirbyMode.KirbyPowerState.Ice,
        KirbyMode.KirbyPowerState.Beam,
        KirbyMode.KirbyPowerState.Hammer,
        KirbyMode.KirbyPowerState.Water,
        KirbyMode.KirbyPowerState.Mirror,
        KirbyMode.KirbyPowerState.Knight
    };

    public static void Load()
    {
        Everest.Events.Level.OnCreatePauseMenuButtons += OnCreatePauseMenuButtons;
    }

    public static void Unload()
    {
        Everest.Events.Level.OnCreatePauseMenuButtons -= OnCreatePauseMenuButtons;
    }

    private static void OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal)
    {
        if (minimal || level?.Tracker == null)
        {
            return;
        }

        var player = level.Tracker.GetEntity<Player>();
        if (player == null)
        {
            return;
        }

        menu.Add(new TextMenu.Header("Kirby Settings"));

        var modeToggle = new TextMenu.OnOff(KirbyModeLabel, player.IsKirbyMode());
        modeToggle.Change(value =>
        {
            if (value)
            {
                player.EnableKirbyMode();
            }
            else
            {
                player.DisableKirbyMode();
            }
        });
        menu.Add(modeToggle);

        var showPowerHud = MaggyHelperModule.Settings?.ShowPowerHud ?? true;
        var hudToggle = new TextMenu.OnOff(KirbyPowerHudLabel, showPowerHud);
        hudToggle.Change(value =>
        {
            var settings = MaggyHelperModule.Settings;
            if (settings != null)
            {
                settings.ShowPowerHud = value;
            }
        });
        menu.Add(hudToggle);

        var settings = MaggyHelperModule.Settings;
        if (settings == null)
        {
            return;
        }

        var powerCopyToggle = new TextMenu.OnOff(KirbyPowerCopyLabel, settings.PowerCopyEnabled);
        powerCopyToggle.Change(value => settings.PowerCopyEnabled = value);
        menu.Add(powerCopyToggle);

        var powerDropToggle = new TextMenu.OnOff(KirbyPowerDropLabel, settings.PowerDropEnabled);
        powerDropToggle.Change(value => settings.PowerDropEnabled = value);
        menu.Add(powerDropToggle);

        var inhaleHoldToggle = new TextMenu.OnOff(KirbyInhaleHoldLabel, settings.InhaleHoldMode);
        inhaleHoldToggle.Change(value => settings.InhaleHoldMode = value);
        menu.Add(inhaleHoldToggle);

        var hoverHoldToggle = new TextMenu.OnOff(KirbyHoverHoldLabel, settings.HoverHoldMode);
        hoverHoldToggle.Change(value => settings.HoverHoldMode = value);
        menu.Add(hoverHoldToggle);

        var knightModeToggle = new TextMenu.OnOff(KirbyKnightModeLabel, settings.KnightModeEnabled);
        knightModeToggle.Change(value => settings.KnightModeEnabled = value);
        menu.Add(knightModeToggle);

        var currentPowerButton = new TextMenu.Button(string.Empty);
        SetCurrentPowerLabel(currentPowerButton, player);
        menu.Add(currentPowerButton);

        menu.Add(new TextMenu.Button(KirbyPowerPreviousLabel).Pressed(() =>
        {
            CycleKirbyPower(player, -1);
            SetCurrentPowerLabel(currentPowerButton, player);
        }));

        menu.Add(new TextMenu.Button(KirbyPowerNextLabel).Pressed(() =>
        {
            CycleKirbyPower(player, 1);
            SetCurrentPowerLabel(currentPowerButton, player);
        }));
    }

    private static void CycleKirbyPower(Player player, int direction)
    {
        if (!player.IsKirbyMode())
        {
            player.EnableKirbyMode();
        }

        KirbyMode.KirbyPowerState current = player.GetKirbyPowerState();
        int currentIndex = Array.IndexOf(QuickCyclePowers, current);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int normalizedDirection = direction < 0 ? -1 : 1;
        int nextIndex = (currentIndex + normalizedDirection + QuickCyclePowers.Length) % QuickCyclePowers.Length;
        KirbyMode.KirbyPowerState next = QuickCyclePowers[nextIndex];

        player.SetKirbyPowerState(next);
        Audio.Play("event:/game/general/diamond_touch", player.Position);
    }

    private static void SetCurrentPowerLabel(TextMenu.Button button, Player player)
    {
        string label = $"Current Power: {player.GetKirbyPowerState()}";
        if (!TrySetButtonLabel(button, label))
        {
            // If label mutation is unavailable in this Everest/Celeste runtime,
            // the button still exists and remains harmless.
        }
    }

    private static bool TrySetButtonLabel(TextMenu.Button button, string label)
    {
        Type type = button.GetType();

        PropertyInfo property = type.GetProperty("Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.CanWrite == true && property.PropertyType == typeof(string))
        {
            property.SetValue(button, label);
            return true;
        }

        FieldInfo field = type.GetField("Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(string))
        {
            field.SetValue(button, label);
            return true;
        }

        return false;
    }
}
