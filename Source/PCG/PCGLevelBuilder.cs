using System;
using System.Collections.Generic;
using System.Text;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Converts PCG-generated room data into real Celeste <see cref="LevelData"/> objects
    /// that can be injected into a live <see cref="Session"/> / <see cref="MapData"/>.
    ///
    /// Full pipeline:
    ///   1. <see cref="PCGSkeletonGenerator.Generate"/> → room graph
    ///   2. <see cref="PCGRoomGenerator.GenerateRoom"/> per skeleton room → tile + entity data
    ///   3. <see cref="PCGLevelBuilder.Build"/> → list of <see cref="LevelData"/>
    ///   4. Inject via <see cref="InjectIntoSession"/> to make them playable
    /// </summary>
    public class PCGLevelBuilder
    {
        /// <summary>
        /// Build a complete set of <see cref="LevelData"/> from a skeleton and per-room results.
        /// </summary>
        /// <param name="skeleton">The level skeleton (room graph).</param>
        /// <param name="roomResults">Generated room data, keyed by skeleton room id.</param>
        /// <param name="roomNamePrefix">Prefix for generated room names (e.g. "pcg-").</param>
        /// <returns>List of <see cref="LevelData"/> ready for injection.</returns>
        public static List<LevelData> Build(PCGSkeleton skeleton,
                                             Dictionary<int, PCGRoomResult> roomResults,
                                             string roomNamePrefix = "pcg-")
        {
            var levels = new List<LevelData>();
            int globalEntityId = 1;

            foreach (var skelRoom in skeleton.Rooms)
            {
                if (!roomResults.TryGetValue(skelRoom.Id, out var result))
                    continue;

                string roomName = $"{roomNamePrefix}{skelRoom.Id}";

                // Build the LevelData via BinaryPacker.Element (the standard Celeste approach)
                var level = CreateLevelData(skelRoom, result, roomName, ref globalEntityId);
                levels.Add(level);
            }

            return levels;
        }

        /// <summary>
        /// Inject generated rooms into a running session's MapData.
        /// After calling this, the player can transition into the PCG rooms via normal room transitions.
        /// </summary>
        /// <param name="session">The current game session.</param>
        /// <param name="levels">Generated <see cref="LevelData"/> from <see cref="Build"/>.</param>
        /// <param name="replaceExisting">If true, remove any existing rooms with matching names first.</param>
        public static void InjectIntoSession(Session session, List<LevelData> levels, bool replaceExisting = true)
        {
            var mapData = session.MapData;
            if (mapData == null)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/PCG", "Cannot inject: session has no MapData.");
                return;
            }

            if (replaceExisting)
            {
                foreach (var newLevel in levels)
                {
                    mapData.Levels.RemoveAll(l => l.Name == newLevel.Name);
                }
            }

            foreach (var level in levels)
            {
                mapData.Levels.Add(level);
            }

            Logger.Log(LogLevel.Info, "MaggyHelper/PCG",
                $"Injected {levels.Count} PCG rooms into session map '{mapData.Filename}'.");
        }

        /// <summary>
        /// Run the full PCG pipeline: skeleton → rooms → LevelData.
        /// Convenience method combining all steps.
        /// </summary>
        /// <param name="config">Room generation config / preset.</param>
        /// <param name="numRooms">Number of rooms to generate.</param>
        /// <param name="seed">Random seed (-1 for non-deterministic).</param>
        /// <param name="trainingMapData">Optional existing map to train Markov chains from.</param>
        /// <returns>The generated levels and skeleton.</returns>
        public static (List<LevelData> Levels, PCGSkeleton Skeleton) GenerateFullLevel(
            PCGRoomConfig config, int numRooms = 8, int seed = -1, MapData trainingMapData = null)
        {
            var rng = seed >= 0 ? new Random(seed) : new Random();

            // 1. Skeleton
            var skeleton = PCGSkeletonGenerator.Generate(
                numRooms, config.RoomWidth, config.RoomHeight, config.BranchProbability, seed);

            // 2. Room generator + optional training
            var roomGen = new PCGRoomGenerator(rng);
            if (trainingMapData != null)
                roomGen.TrainFromMapData(trainingMapData, config.MarkovConfig);

            // 3. Generate each room
            var roomResults = new Dictionary<int, PCGRoomResult>();
            foreach (var skelRoom in skeleton.Rooms)
            {
                var result = roomGen.GenerateRoom(config, skelRoom);
                roomResults[skelRoom.Id] = result;
            }

            // 4. Build LevelData
            var levels = Build(skeleton, roomResults);

            return (levels, skeleton);
        }

        // ------------------------------------------------------------------ //
        //  LevelData construction
        // ------------------------------------------------------------------ //

        private static LevelData CreateLevelData(PCGSkeletonRoom skelRoom, PCGRoomResult result,
                                                  string name, ref int globalEntityId)
        {
            int pixelX = skelRoom.WorldX;
            int pixelY = skelRoom.WorldY;
            int pixelW = result.Width * 8;
            int pixelH = result.Height * 8;

            // Tile strings
            string fgStr = PCGMarkovChain.ToTileString(result.FGTiles, result.Width, result.Height);
            string bgStr = PCGMarkovChain.ToTileString(result.BGTiles, result.Width, result.Height);
            string objStr = BuildEmptyObjTiles(result.Width, result.Height);

            // Build entity data
            var entityDataList = new List<EntityData>();
            foreach (var eInfo in result.Entities)
            {
                var ed = new EntityData
                {
                    Name = eInfo.Name,
                    ID = globalEntityId++,
                    Position = new Vector2(eInfo.X - pixelX, eInfo.Y - pixelY),
                    Width = eInfo.Width,
                    Height = eInfo.Height,
                    Values = new Dictionary<string, object>(eInfo.Values),
                };
                entityDataList.Add(ed);
            }

            var triggerDataList = new List<EntityData>();
            foreach (var tInfo in result.Triggers)
            {
                var td = new EntityData
                {
                    Name = tInfo.Name,
                    ID = globalEntityId++,
                    Position = new Vector2(tInfo.X - pixelX, tInfo.Y - pixelY),
                    Width = tInfo.Width,
                    Height = tInfo.Height,
                    Values = new Dictionary<string, object>(tInfo.Values),
                };
                triggerDataList.Add(td);
            }

            // Construct LevelData
            // NOTE: Everest patches LevelData to read windPattern via Attr() (string),
            // not AttrInt(). All string-read attributes must be strings in the dictionary
            // to avoid InvalidCastException.
            var level = new LevelData(new BinaryPacker.Element
            {
                Name = "level",
                Attributes = new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["x"] = pixelX,
                    ["y"] = pixelY,
                    ["width"] = pixelW,
                    ["height"] = pixelH,
                    ["c"] = 0,
                    ["dark"] = false,
                    ["space"] = false,
                    ["underwater"] = false,
                    ["music"] = "",
                    ["alt_music"] = "",
                    ["musicProgress"] = "",
                    ["ambienceProgress"] = "",
                    ["ambience"] = "",
                    ["windPattern"] = "None",
                    ["solids"] = fgStr,
                    ["bg"] = bgStr,
                    ["objTiles"] = objStr,
                },
                Children = new List<BinaryPacker.Element>
                {
                    new() { Name = "entities", Children = new List<BinaryPacker.Element>() },
                    new() { Name = "triggers", Children = new List<BinaryPacker.Element>() },
                    new() { Name = "fgdecals", Children = new List<BinaryPacker.Element>() },
                    new() { Name = "bgdecals", Children = new List<BinaryPacker.Element>() },
                },
            });

            // Manually set entity/trigger data since LevelData constructor
            // populates from BinaryPacker elements but we have EntityData directly
            level.Entities.Clear();
            level.Entities.AddRange(entityDataList);
            level.Triggers.Clear();
            level.Triggers.AddRange(triggerDataList);

            return level;
        }

        private static string BuildEmptyObjTiles(int w, int h)
        {
            var sb = new StringBuilder(w * h + h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    sb.Append('-');
                if (y < h - 1)
                    sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
