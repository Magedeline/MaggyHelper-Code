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
    private const string KirbyBindingsHeaderLabel = "Key Bindings";
    private const string KeyboardBindingsLabel = "options_keyconfig";
    private const string ControllerBindingsLabel = "options_btnconfig";
    private const string KirbyCurrentPowerLabel = "Current Power";
    private const string UnboundLabel = "Unbound";
    private const string KirbyInhaleBindingLabel = "Inhale";
    private const string KirbyAttackBindingLabel = "Attack";
    private const string KirbyHoverBindingLabel = "Hover";
    private const string KirbySpitBindingLabel = "Spit";
    private const string KirbyCycleBindingLabel = "Cycle Power";
    private const string KirbyDropBindingLabel = "Drop Power";
    private const string KirbySlideBindingLabel = "Slide";
    private const string KirbyKnightBindingLabel = "Knight Mode";
    private const string KirbyPowerPreviousLabel = "Previous Kirby Power";
    private const string KirbyPowerNextLabel = "Next Kirby Power";
    private const int KnightModeChapter19 = 19;
    private const int KnightModeChapter20 = 20;
    private const string KnightUnlockedFlag = "kirby_knight_unlocked";
    private const string KnightActiveFlag = "kirby_knight_active";
    private const string Chapter19FinalRunFlag = "ch19_final_run";

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

        bool assistModeEnabled = SaveData.Instance?.AssistMode == true;
        bool showCurrentPower = assistModeEnabled || player.IsKirbyMode();
        bool showKnightModeOptions = ShouldShowKnightModeOptions(level, player);
        var settings = MaggyHelperModule.Settings;

        if (assistModeEnabled)
        {
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
        }

        var showPowerHud = settings?.ShowPowerHud ?? true;
        var hudToggle = new TextMenu.OnOff(KirbyPowerHudLabel, showPowerHud);
        hudToggle.Change(value =>
        {
            if (settings != null)
            {
                settings.ShowPowerHud = value;
            }
        });
        menu.Add(hudToggle);

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

        if (showKnightModeOptions)
        {
            var knightModeToggle = new TextMenu.OnOff(KirbyKnightModeLabel, settings.KnightModeEnabled);
            knightModeToggle.Change(value => settings.KnightModeEnabled = value);
            menu.Add(knightModeToggle);
        }

        TextMenu.Button currentPowerButton = null;
        if (showCurrentPower)
        {
            currentPowerButton = new TextMenu.Button(string.Empty);
            SetCurrentPowerLabel(currentPowerButton, player);
            menu.Add(currentPowerButton);
        }

        if (assistModeEnabled)
        {
            menu.Add(new TextMenu.Button(KirbyPowerPreviousLabel).Pressed(() =>
            {
                CycleKirbyPower(player, -1);
                if (currentPowerButton != null)
                {
                    SetCurrentPowerLabel(currentPowerButton, player);
                }
            }));

            menu.Add(new TextMenu.Button(KirbyPowerNextLabel).Pressed(() =>
            {
                CycleKirbyPower(player, 1);
                if (currentPowerButton != null)
                {
                    SetCurrentPowerLabel(currentPowerButton, player);
                }
            }));
        }

        AddBindingConfigEntry(menu, KirbyBindingsHeaderLabel, KeyboardBindingsLabel, openControllerConfig: false);
        AddBindingConfigEntry(menu, null, ControllerBindingsLabel, openControllerConfig: true);
        AddBindingSummary(menu, null, KirbyInhaleBindingLabel, settings.KirbyInhaleBind);
        AddBindingSummary(menu, null, KirbyAttackBindingLabel, settings.KirbyAttackBind);
        AddBindingSummary(menu, null, KirbyHoverBindingLabel, settings.KirbyHoverBind);
        AddBindingSummary(menu, null, KirbySpitBindingLabel, settings.KirbySpitBind);
        AddBindingSummary(menu, null, KirbyCycleBindingLabel, settings.KirbyCyclePowerBind);
        AddBindingSummary(menu, null, KirbyDropBindingLabel, settings.KirbyDropPowerBind);
        AddBindingSummary(menu, null, KirbySlideBindingLabel, settings.KirbySlideBind);
        if (showKnightModeOptions)
        {
            AddBindingSummary(menu, null, KirbyKnightBindingLabel, settings.KirbyKnightBind);
        }
    }

    private static bool ShouldShowKnightModeOptions(Level level, Player player)
    {
        if (level?.Session == null)
        {
            return player?.GetKirbyPowerState() == KirbyMode.KirbyPowerState.Knight;
        }

        if (player?.GetKirbyPowerState() == KirbyMode.KirbyPowerState.Knight)
        {
            return true;
        }

        if (MaggyHelperModule.Session?.IsKnightModeActive == true || MaggyHelperModule.SaveData?.KnightModeUnlocked == true)
        {
            return true;
        }

        if (level.Session.GetFlag(KnightActiveFlag) || level.Session.GetFlag(KnightUnlockedFlag))
        {
            return true;
        }

        int chapterId = level.Session.Area.ID;
        if (chapterId == KnightModeChapter20)
        {
            return true;
        }

        return chapterId == KnightModeChapter19 && level.Session.GetFlag(Chapter19FinalRunFlag);
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
        string label = $"{KirbyCurrentPowerLabel}: {player.GetKirbyPowerState()}";
        if (!TrySetButtonLabel(button, label))
        {
            // If label mutation is unavailable in this Everest/Celeste runtime,
            // the button still exists and remains harmless.
        }
    }

    private static void AddBindingSummary(TextMenu menu, string headerLabel, string actionLabel, ButtonBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(headerLabel))
        {
            menu.Add(new TextMenu.Header(headerLabel));
        }

        TextMenu.Button summaryButton = new($"{actionLabel}: {FormatBinding(binding)}")
        {
            Disabled = true
        };
        menu.Add(summaryButton);
    }

    private static void AddBindingConfigEntry(TextMenu menu, string headerLabel, string label, bool openControllerConfig)
    {
        if (!string.IsNullOrWhiteSpace(headerLabel))
        {
            menu.Add(new TextMenu.Header(headerLabel));
        }

        menu.Add(new TextMenu.Button(Dialog.Clean(label)).Pressed(() =>
        {
            OpenBindingConfig(menu, openControllerConfig);
        }));
    }

    private static void OpenBindingConfig(TextMenu menu, bool openControllerConfig)
    {
        EverestModule module = MaggyHelperModule.Instance;
        if (module == null || Engine.Scene == null)
        {
            return;
        }

        string methodName = openControllerConfig ? "CreateButtonConfigUI" : "CreateKeyboardConfigUI";
        MethodInfo createConfigMethod = typeof(EverestModule).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (createConfigMethod?.Invoke(module, new object[] { menu }) is not Entity configUi)
        {
            return;
        }

        menu.Focused = false;
        Engine.Scene.Add(configUi);
        Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
    }

    private static string FormatBinding(ButtonBinding binding)
    {
        if (binding == null)
        {
            return UnboundLabel;
        }

        List<string> groups = new();

        if (binding.Buttons is { Count: > 0 })
        {
            groups.Add(string.Join(", ", binding.Buttons.Select(FormatButton)));
        }

        if (binding.Keys is { Count: > 0 })
        {
            groups.Add(string.Join(", ", binding.Keys.Select(FormatKey)));
        }

        if (binding.MouseButtons is { Count: > 0 })
        {
            groups.Add(string.Join(", ", binding.MouseButtons.Select(button => FormatEnumName(button.ToString()))));
        }

        return groups.Count > 0 ? string.Join(" / ", groups) : UnboundLabel;
    }

    private static string FormatButton(Microsoft.Xna.Framework.Input.Buttons button)
    {
        return button switch
        {
            Microsoft.Xna.Framework.Input.Buttons.LeftShoulder => "LB",
            Microsoft.Xna.Framework.Input.Buttons.RightShoulder => "RB",
            Microsoft.Xna.Framework.Input.Buttons.LeftTrigger => "LT",
            Microsoft.Xna.Framework.Input.Buttons.RightTrigger => "RT",
            Microsoft.Xna.Framework.Input.Buttons.LeftStick => "L3",
            Microsoft.Xna.Framework.Input.Buttons.RightStick => "R3",
            Microsoft.Xna.Framework.Input.Buttons.Start => "Start",
            Microsoft.Xna.Framework.Input.Buttons.Back => "Back",
            _ => FormatEnumName(button.ToString())
        };
    }

    private static string FormatKey(Microsoft.Xna.Framework.Input.Keys key)
    {
        return key switch
        {
            Microsoft.Xna.Framework.Input.Keys.LeftShift => "Left Shift",
            Microsoft.Xna.Framework.Input.Keys.RightShift => "Right Shift",
            Microsoft.Xna.Framework.Input.Keys.LeftControl => "Left Ctrl",
            Microsoft.Xna.Framework.Input.Keys.RightControl => "Right Ctrl",
            Microsoft.Xna.Framework.Input.Keys.LeftAlt => "Left Alt",
            Microsoft.Xna.Framework.Input.Keys.RightAlt => "Right Alt",
            Microsoft.Xna.Framework.Input.Keys.OemQuestion => "?",
            Microsoft.Xna.Framework.Input.Keys.OemComma => ",",
            Microsoft.Xna.Framework.Input.Keys.OemPeriod => ".",
            Microsoft.Xna.Framework.Input.Keys.OemPlus => "+",
            Microsoft.Xna.Framework.Input.Keys.OemMinus => "-",
            _ => FormatEnumName(key.ToString())
        };
    }

    private static string FormatEnumName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var characters = new System.Text.StringBuilder(value.Length + 8);

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];

            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
            {
                characters.Append(' ');
            }

            characters.Append(current);
        }

        return characters.ToString();
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
