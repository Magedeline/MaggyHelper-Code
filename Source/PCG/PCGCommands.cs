using System;
using System;
using System.Linq;
using Celeste;
using Monocle;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Debug console commands for testing the PCG system in-game.
    ///
    /// Usage from Celeste debug console (~):
    ///   pcg_generate [preset] [roomCount] [seed]
    ///   pcg_generate default 8 42
    ///   pcg_generate space 12
    ///   pcg_generate              (uses default preset, 8 rooms, random seed)
    /// </summary>
    public static class PCGCommands
    {
        [Command("pcg_generate", "Generate PCG rooms and teleport to them. Usage: pcg_generate [preset] [roomCount] [seed]")]
        public static void CmdGenerate(string arg0 = null, string arg1 = null, string arg2 = null)
        {
            if (Engine.Scene is not Level level)
            {
                Engine.Commands.Log("Must be in a level to generate PCG rooms.");
                return;
            }

            // Parse arguments manually to avoid Monocle reflection cast errors
            // when the user passes a non-numeric value for roomCount/seed.
            string preset = "default";
            int roomCount = 8;
            int seed = -1;

            // Collect non-null, non-empty args
            string[] rawArgs = new[] { arg0, arg1, arg2 }
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToArray();

            if (rawArgs.Length >= 1)
                preset = rawArgs[0];

            if (rawArgs.Length >= 2)
            {
                if (!int.TryParse(rawArgs[1], out roomCount))
                {
                    Engine.Commands.Log($"Invalid roomCount '{rawArgs[1]}'. Must be a number. Using default (8).");
                    roomCount = 8;
                }
            }

            if (rawArgs.Length >= 3)
            {
                if (!int.TryParse(rawArgs[2], out seed))
                {
                    Engine.Commands.Log($"Invalid seed '{rawArgs[2]}'. Must be a number. Using default (-1).");
                    seed = -1;
                }
            }

            // Validate preset name
            if (!PCGPresets.AllNames.Contains(preset, StringComparer.OrdinalIgnoreCase))
            {
                Engine.Commands.Log($"Unknown preset '{preset}'. Use pcg_presets to list available presets.");
                Engine.Commands.Log("Available: " + string.Join(", ", PCGPresets.AllNames));
                return;
            }

            try
            {
                var config = PCGPresets.GetByName(preset);
                Engine.Commands.Log($"Generating {roomCount} rooms with preset '{preset}'...");

                var (levels, skeleton) = PCGLevelBuilder.GenerateFullLevel(
                    config, roomCount, seed, level.Session.MapData);

                PCGLevelBuilder.InjectIntoSession(level.Session, levels);

                string targetRoom = $"pcg-{skeleton.StartRoomId}";
                Engine.Commands.Log($"Injected {levels.Count} rooms. Teleporting to {targetRoom}...");

                var player = level.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    level.OnEndOfFrame += () =>
                    {
                        level.TeleportTo(player, targetRoom, Player.IntroTypes.Transition);
                    };
                }
                else
                {
                    Engine.Commands.Log("No player found — rooms injected but cannot teleport.");
                }
            }
            catch (Exception ex)
            {
                Engine.Commands.Log($"PCG generation failed: {ex.Message}");
                Engine.Commands.Log($"  at: {ex.StackTrace?.Split('\n')[0]?.Trim()}");
                Logger.Log(LogLevel.Error, "MaggyHelper/PCG", $"pcg_generate failed: {ex}");
            }
        }

        [Command("pcg_presets", "List all available PCG presets.")]
        public static void CmdListPresets()
        {
            Engine.Commands.Log("Available PCG presets:");
            foreach (var name in PCGPresets.AllNames)
                Engine.Commands.Log($"  - {name}");
        }
    }
}
