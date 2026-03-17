using System;
using System.Collections.Generic;
using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// In-game trigger that activates procedural content generation when the player enters it.
    /// Place in Lönn as <c>MaggyHelper/PCGTrigger</c>.
    ///
    /// When the player enters the trigger region, a new set of PCG rooms is generated
    /// and injected into the current session. The player is then teleported into the first
    /// generated room.
    ///
    /// Lönn attributes:
    ///   preset        (string)  – Preset name: "default", "open", "tight", "space", etc.
    ///   roomCount     (int)     – Number of rooms to generate (default 8).
    ///   seed          (int)     – Random seed (-1 = non-deterministic).
    ///   trainFromMap  (bool)    – If true, train Markov chains from the current map before generating.
    ///   targetRoom    (string)  – If non-empty, teleport to this specific generated room name.
    /// </summary>
    [CustomEntity("MaggyHelper/PCGTrigger")]
    public class PCGTrigger : Trigger
    {
        private readonly string _preset;
        private readonly int _roomCount;
        private readonly int _seed;
        private readonly bool _trainFromMap;
        private readonly string _targetRoom;

        private bool _triggered;

        public PCGTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            _preset = data.Attr("preset", "default");
            _roomCount = data.Int("roomCount", 8);
            _seed = data.Int("seed", -1);
            _trainFromMap = data.Bool("trainFromMap", true);
            _targetRoom = data.Attr("targetRoom", "");
        }

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);

            if (_triggered) return;
            _triggered = true;

            Logger.Log(LogLevel.Info, "MaggyHelper/PCG",
                $"PCGTrigger activated: preset={_preset}, rooms={_roomCount}, seed={_seed}");

            try
            {
                var level = SceneAs<Level>();
                if (level == null) return;

                var config = PCGPresets.GetByName(_preset);

                // Optionally train from the current map's rooms
                MapData trainingData = _trainFromMap ? level.Session.MapData : null;

                // Generate
                var (levels, skeleton) = PCGLevelBuilder.GenerateFullLevel(
                    config, _roomCount, _seed, trainingData);

                // Inject into session
                PCGLevelBuilder.InjectIntoSession(level.Session, levels);

                // Determine target room
                string targetRoom = !string.IsNullOrEmpty(_targetRoom)
                    ? _targetRoom
                    : $"pcg-{skeleton.StartRoomId}";

                // Teleport to the generated level
                level.OnEndOfFrame += () =>
                {
                    level.TeleportTo(player, targetRoom, Player.IntroTypes.Transition);
                };

                Logger.Log(LogLevel.Info, "MaggyHelper/PCG",
                    $"PCG generation complete: {levels.Count} rooms injected, teleporting to {targetRoom}");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/PCG",
                    $"PCGTrigger generation failed: {ex}");
            }
        }
    }
}
