# Kirby + Aqua Tutorial Bird Examples

These examples use the new Loenn objects:
- Entity: `MaggyHelper/KirbyTutorialBird`
- Trigger: `MaggyHelper/KirbyTutorialBirdTrigger`

## Test Map Script

You can generate a compact two-room test map for this flow with:

`lua Loenn/scripts/generate_kirby_aqua_tutorial_test.lua`

Output:

- `Maps/Maggy/PCG/kirby_aqua_tutorial_test.bin`
- SID: `Maggy/PCG/kirby_aqua_tutorial_test`

## Quick Setup

1. Place `aqua_hook_intro` from the Kirby tutorial bird placements.
2. Place `show_when_aqua_hook_fixed` trigger where you want the second tutorial panel to appear.
3. Optionally place `close_when_aqua_attracted` to close the tutorial and fly the bird away.

## Condition Function Format

Use a static function with signature `bool(Level)` and reference it as:

- Dot syntax:
  - `mod:MaggyHelper.Extensions.Kirby.ModCompat.AquaTutorialCompat.IsAquaHookFixed`
- Slash syntax:
  - `mod:MaggyHelper/Extensions/Kirby/ModCompat/AquaTutorialCompat/IsAquaHookFixed`

Both formats are supported by `KirbyTutorialBirdTrigger`.

## Built-in Aqua Conditions

From `AquaTutorialCompat`:

- `IsAquaHookFixed`
- `IsAquaHookActive`
- `IsKirbyAquaSwinging`
- `IsKirbyAquaAttracted`

## Controls Token Notes

The bird control parser supports:

- Button prompts: `Dash`, `Jump`, `Grab`, `Talk`, plus aliases `Confirm`, `Climb`
- Directions: `Left`, `Right`, `Up`, `Down`, `UpLeft`, `UpRight`, `DownLeft`, `DownRight`
- Direction variants: `Up-Right`, `Up_Right`, `Up Right`
- Literal helpers: `PLUS`, `HOLD`, `PRESS`, `THEN`, `tinyarrow`
- Mod bindings: `mod:ModName/SettingProperty` (example: `mod:Aqua/ThrowHook`)

Example controls string:

`mod:Aqua/ThrowHook,PLUS,UpRight;HOLD,Grab,PLUS,tinyarrow,Jump`
