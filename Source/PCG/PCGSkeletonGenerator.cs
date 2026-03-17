using System;
using System.Collections.Generic;
using MaggyHelper.Utils;
using Microsoft.Xna.Framework;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Describes a single room inside a generated level skeleton.
    /// </summary>
    public class PCGSkeletonRoom
    {
        public int Id { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int TilesW { get; set; }
        public int TilesH { get; set; }
        public bool IsStart { get; set; }
        public bool IsEnd { get; set; }

        /// <summary>Which sides of the room have exit openings.</summary>
        public bool ExitLeft { get; set; }
        public bool ExitRight { get; set; }
        public bool ExitTop { get; set; }
        public bool ExitBottom { get; set; }

        /// <summary>IDs of rooms this room connects to.</summary>
        public List<int> Connections { get; } = new();

        /// <summary>Position of this room in world pixels (computed from grid position).</summary>
        public int WorldX => GridX * TilesW * 8;
        public int WorldY => GridY * TilesH * 8;
        public Rectangle WorldBounds => new(WorldX, WorldY, TilesW * 8, TilesH * 8);
    }

    /// <summary>
    /// The result of skeleton generation — a graph of connected rooms.
    /// </summary>
    public class PCGSkeleton
    {
        public List<PCGSkeletonRoom> Rooms { get; } = new();
        public int StartRoomId { get; set; }
        public int EndRoomId { get; set; }
    }

    /// <summary>
    /// Generates the high-level room layout for a PCG level.
    /// Uses a random-walk with branching algorithm inspired by Spelunky and
    /// the Celeskeleton concept from the Celeste AI Framework paper.
    /// C# port of <c>libraries/pcg/skeleton.lua</c>.
    /// </summary>
    public static class PCGSkeletonGenerator
    {
        private struct Direction
        {
            public string Name;
            public int Dx, Dy;
            public string Opposite;
        }

        private static readonly Direction[] Directions =
        {
            new() { Name = "right",  Dx =  1, Dy =  0, Opposite = "left"   },
            new() { Name = "left",   Dx = -1, Dy =  0, Opposite = "right"  },
            new() { Name = "down",   Dx =  0, Dy =  1, Opposite = "top"    },
            new() { Name = "up",     Dx =  0, Dy = -1, Opposite = "bottom" },
        };

        /// <summary>
        /// Generate a level skeleton with <paramref name="numRooms"/> connected rooms.
        /// </summary>
        /// <param name="numRooms">Number of rooms to generate.</param>
        /// <param name="roomW">Room width in tiles.</param>
        /// <param name="roomH">Room height in tiles.</param>
        /// <param name="branchProb">Probability of creating a branch (0–1).</param>
        /// <param name="seed">Optional random seed. -1 for non-deterministic.</param>
        public static PCGSkeleton Generate(int numRooms = 10, int roomW = 40, int roomH = 23,
                                           float branchProb = 0.3f, int seed = -1)
        {
            var rng = seed >= 0 ? new Pcg32Random((uint)seed) : new Random();

            // Grid bookkeeping
            var occupied = new Dictionary<(int, int), int>(); // gridPos → roomId
            var rooms = new Dictionary<int, PCGSkeletonRoom>();
            int nextId = 1;

            PCGSkeletonRoom AddRoom(int gx, int gy)
            {
                var room = new PCGSkeletonRoom
                {
                    Id = nextId, GridX = gx, GridY = gy,
                    TilesW = roomW, TilesH = roomH,
                };
                occupied[(gx, gy)] = nextId;
                rooms[nextId] = room;
                nextId++;
                return room;
            }

            void Connect(PCGSkeletonRoom a, PCGSkeletonRoom b, Direction dir)
            {
                a.Connections.Add(b.Id);
                b.Connections.Add(a.Id);
                SetExit(a, dir.Name, true);
                SetExit(b, dir.Opposite, true);
            }

            // --- main path via random walk ---
            var start = AddRoom(0, 0);
            start.IsStart = true;

            var mainPath = new List<PCGSkeletonRoom> { start };
            var current = start;

            int maxAttempts = numRooms * 20;
            int attempts = 0;

            while (mainPath.Count < numRooms && attempts < maxAttempts)
            {
                attempts++;
                var dirs = ShuffledDirections(rng);
                bool moved = false;

                foreach (var dir in dirs)
                {
                    int nx = current.GridX + dir.Dx;
                    int ny = current.GridY + dir.Dy;
                    if (!occupied.ContainsKey((nx, ny)))
                    {
                        var next = AddRoom(nx, ny);
                        Connect(current, next, dir);
                        mainPath.Add(next);
                        current = next;
                        moved = true;
                        break;
                    }
                }

                if (!moved)
                {
                    // Backtrack to a random room on the path that still has open neighbours
                    current = FindOpenRoom(mainPath, occupied, rng);
                    if (current == null) break;
                }
            }

            // Mark end room
            mainPath[^1].IsEnd = true;

            // --- branching ---
            var branchCandidates = new List<PCGSkeletonRoom>(mainPath);
            int branchRooms = (int)(numRooms * branchProb);
            for (int b = 0; b < branchRooms && nextId <= numRooms * 2; b++)
            {
                if (branchCandidates.Count == 0) break;
                var parent = branchCandidates[rng.Next(branchCandidates.Count)];
                var dirs = ShuffledDirections(rng);

                foreach (var dir in dirs)
                {
                    int nx = parent.GridX + dir.Dx;
                    int ny = parent.GridY + dir.Dy;
                    if (!occupied.ContainsKey((nx, ny)))
                    {
                        var branch = AddRoom(nx, ny);
                        Connect(parent, branch, dir);
                        branchCandidates.Add(branch);
                        break;
                    }
                }
            }

            // Build result
            var skeleton = new PCGSkeleton
            {
                StartRoomId = mainPath[0].Id,
                EndRoomId = mainPath[^1].Id,
            };
            foreach (var room in rooms.Values)
                skeleton.Rooms.Add(room);

            return skeleton;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static void SetExit(PCGSkeletonRoom room, string side, bool value)
        {
            switch (side)
            {
                case "left":   room.ExitLeft   = value; break;
                case "right":  room.ExitRight  = value; break;
                case "top":    room.ExitTop    = value; break;
                case "bottom": room.ExitBottom = value; break;
            }
        }

        private static Direction[] ShuffledDirections(Random rng)
        {
            var arr = (Direction[])Directions.Clone();
            rng.Shuffle(arr);
            return arr;
        }

        private static PCGSkeletonRoom FindOpenRoom(List<PCGSkeletonRoom> path,
                                                     Dictionary<(int, int), int> occupied, Random rng)
        {
            var candidates = new List<PCGSkeletonRoom>();
            foreach (var room in path)
            {
                foreach (var dir in Directions)
                {
                    if (!occupied.ContainsKey((room.GridX + dir.Dx, room.GridY + dir.Dy)))
                    {
                        candidates.Add(room);
                        break;
                    }
                }
            }
            return candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : null;
        }
    }
}
