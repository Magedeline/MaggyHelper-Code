using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Entity descriptor produced by the room generator before being converted
    /// into actual Celeste <see cref="EntityData"/> by <see cref="PCGLevelBuilder"/>.
    /// </summary>
    public class PCGEntityInfo
    {
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<string, object> Values { get; } = new();
    }

    /// <summary>
    /// Full output of generating one room: tiles + entities.
    /// </summary>
    public class PCGRoomResult
    {
        public char[,] FGTiles { get; set; }
        public char[,] BGTiles { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PCGEntityInfo> Entities { get; } = new();
        public List<PCGEntityInfo> Triggers { get; } = new();
    }

    /// <summary>
    /// Generates the tile content and entity placement for a single PCG room.
    /// C# port of the Lönn <c>libraries/pcg/generator.lua</c>.
    /// </summary>
    public class PCGRoomGenerator
    {
        /// <summary>
        /// Tileset character per room-style, matching <c>generator.STYLE_TILES</c> in Lua.
        /// </summary>
        private static readonly Dictionary<string, char> StyleTiles = new()
        {
            ["normal"]     = '1', ["resort"]    = '5', ["temple"]     = 'd',
            ["reflection"] = 'g', ["summit"]    = 'i', ["core"]       = 'k',
            ["wind"]       = '3', ["ice"]       = '3', ["cave"]       = '8',
            ["ruins"]      = 'A', ["castle"]    = 'B', ["darkStars"]  = 'O',
            ["void"]       = 'X', ["nightmare"] = 'p', ["farewell"]   = 'n',
            ["dream"]      = 'N', ["space"]     = '1', ["deepSpace"]  = '1',
        };

        private readonly Random _rng;
        private readonly PCGMarkovChain _markov;

        public PCGRoomGenerator(Random rng = null)
        {
            _rng = rng ?? new Random();
            _markov = new PCGMarkovChain(_rng);
        }

        /// <summary>The underlying Markov chain. Expose so callers can train it.</summary>
        public PCGMarkovChain Markov => _markov;

        // ------------------------------------------------------------------ //
        //  Training
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Train the Markov chain from existing Celeste level data.
        /// Call for each room whose style you want the generator to learn.
        /// </summary>
        public void TrainFromRoom(string fgTileStr, int w, int h, string configStr = "000011012")
        {
            _markov.ParseConfig(configStr);
            _markov.TrainFromTileString(fgTileStr, w, h);
        }

        /// <summary>
        /// Train from all rooms in a <see cref="MapData"/>.
        /// </summary>
        public void TrainFromMapData(MapData mapData, string configStr = "000011012")
        {
            _markov.ParseConfig(configStr);
            foreach (var level in mapData.Levels)
            {
                int w = level.Bounds.Width / 8;
                int h = level.Bounds.Height / 8;
                if (!string.IsNullOrEmpty(level.Solids))
                    _markov.TrainFromTileString(level.Solids, w, h);
            }
        }

        // ------------------------------------------------------------------ //
        //  Room generation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Generate a single room using the given configuration and skeleton room descriptor.
        /// </summary>
        public PCGRoomResult GenerateRoom(PCGRoomConfig config, PCGSkeletonRoom skelRoom = null)
        {
            int w = config.RoomWidth;
            int h = config.RoomHeight;
            char material = GetStyleMaterial(config.RoomStyle, config.BorderMaterial);

            bool hasLeft   = skelRoom?.ExitLeft   ?? false;
            bool hasRight  = skelRoom?.ExitRight  ?? true;
            bool hasTop    = skelRoom?.ExitTop    ?? false;
            bool hasBottom = skelRoom?.ExitBottom ?? false;

            // --- Generate FG tiles ---
            char[,] fgTiles;
            if (_markov.DPT.Count > 0)
            {
                _markov.ParseConfig(config.MarkovConfig);
                fgTiles = _markov.Generate(w, h, config.BacktrackDepth);
            }
            else
            {
                fgTiles = _markov.GenerateFallback(w, h, material);
            }

            // --- Apply borders ---
            if (!config.NoBorders)
            {
                if (config.TrimmedBorders)
                    PlaceTrimmedBorders(fgTiles, w, h, material, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize);
                else
                    PlaceBorders(fgTiles, w, h, material, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize);
            }

            // --- Place exits ---
            PlaceExits(fgTiles, w, h, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize);

            // --- Space erosion ---
            if (config.SpaceErodePercent > 0f)
                ErodeForSpace(fgTiles, w, h, config.SpaceErodePercent);

            // --- Cleanup ---
            for (int pass = 0; pass < config.CleanupPasses; pass++)
                CleanupTiles(fgTiles, w, h);

            // --- BG tiles: simple fill behind solids ---
            var bgTiles = GenerateBackground(fgTiles, w, h, material);

            // --- Entities ---
            int roomX = skelRoom?.WorldX ?? 0;
            int roomY = skelRoom?.WorldY ?? 0;
            var entities = GenerateEntities(fgTiles, w, h, roomX, roomY, config.EntityDensity,
                                             hasLeft, hasRight, hasTop, hasBottom, config.RoomStyle);

            // --- Playability check via simple flood-fill ---
            if (config.PlayabilityCheck)
            {
                for (int retry = 0; retry < config.MaxRetries; retry++)
                {
                    if (IsPlayable(fgTiles, w, h, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize))
                        break;
                    // Re-generate tiles
                    if (_markov.DPT.Count > 0)
                        fgTiles = _markov.Generate(w, h, config.BacktrackDepth);
                    else
                        fgTiles = _markov.GenerateFallback(w, h, material);
                    if (!config.NoBorders)
                        PlaceBorders(fgTiles, w, h, material, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize);
                    PlaceExits(fgTiles, w, h, hasLeft, hasRight, hasTop, hasBottom, config.ExitSize);
                    for (int pass = 0; pass < config.CleanupPasses; pass++)
                        CleanupTiles(fgTiles, w, h);
                }
            }

            return new PCGRoomResult
            {
                FGTiles = fgTiles,
                BGTiles = bgTiles,
                Width = w,
                Height = h,
                Entities = { },  // populated below
            }.Also(r =>
            {
                foreach (var e in entities) r.Entities.Add(e);
            });
        }

        // ------------------------------------------------------------------ //
        //  Borders
        // ------------------------------------------------------------------ //

        private static void PlaceBorders(char[,] m, int w, int h, char mat,
            bool exitL, bool exitR, bool exitT, bool exitB, int exitSize)
        {
            int ExitStart(int span) => span / 2 - exitSize / 2;
            int ExitEnd(int span) => ExitStart(span) + exitSize - 1;

            // Top & bottom
            for (int x = 0; x < w; x++)
            {
                if (!(exitT && x >= ExitStart(w) && x <= ExitEnd(w)))
                    m[x, 0] = mat;
                if (!(exitB && x >= ExitStart(w) && x <= ExitEnd(w)))
                    m[x, h - 1] = mat;
            }
            // Left & right
            for (int y = 0; y < h; y++)
            {
                if (!(exitL && y >= ExitStart(h) && y <= ExitEnd(h)))
                    m[0, y] = mat;
                if (!(exitR && y >= ExitStart(h) && y <= ExitEnd(h)))
                    m[w - 1, y] = mat;
            }
        }

        private static void PlaceTrimmedBorders(char[,] m, int w, int h, char mat,
            bool exitL, bool exitR, bool exitT, bool exitB, int exitSize)
        {
            int ExitStart(int span) => span / 2 - exitSize / 2;
            int ExitEnd(int span) => ExitStart(span) + exitSize - 1;

            // Bottom: always solid floor
            for (int x = 0; x < w; x++)
            {
                if (!(exitB && x >= ExitStart(w) && x <= ExitEnd(w)))
                    m[x, h - 1] = mat;
            }

            // Top: always air (open sky)
            for (int x = 0; x < w; x++)
                m[x, 0] = '0';

            // Side walls: only bottom quarter
            int stubHeight = Math.Max(2, h / 4);
            for (int y = h - stubHeight; y < h; y++)
            {
                if (!(exitL && y >= ExitStart(h) && y <= ExitEnd(h)))
                    m[0, y] = mat;
                if (!(exitR && y >= ExitStart(h) && y <= ExitEnd(h)))
                    m[w - 1, y] = mat;
            }
        }

        // ------------------------------------------------------------------ //
        //  Exits
        // ------------------------------------------------------------------ //

        private static void PlaceExits(char[,] m, int w, int h,
            bool exitL, bool exitR, bool exitT, bool exitB, int exitSize)
        {
            void ClearExit(int startX, int startY, int dx, int dy, int length, int depth)
            {
                for (int d = 0; d < depth; d++)
                    for (int i = 0; i < length; i++)
                    {
                        int x = startX + dx * i + (dx == 0 ? 0 : 0) + (dy != 0 ? 0 : d * (startX == 0 ? 1 : -1));
                        int y = startY + dy * i + (dx != 0 ? 0 : d * (startY == 0 ? 1 : -1));
                        // Simplified: just clear the border region
                    }
            }

            if (exitL)
            {
                int startY = h / 2 - exitSize / 2;
                for (int dy = 0; dy < exitSize; dy++)
                {
                    int y = startY + dy;
                    if (y >= 0 && y < h) { m[0, y] = '0'; if (w > 1) m[1, y] = '0'; }
                }
            }
            if (exitR)
            {
                int startY = h / 2 - exitSize / 2;
                for (int dy = 0; dy < exitSize; dy++)
                {
                    int y = startY + dy;
                    if (y >= 0 && y < h) { m[w - 1, y] = '0'; if (w > 1) m[w - 2, y] = '0'; }
                }
            }
            if (exitT)
            {
                int startX = w / 2 - exitSize / 2;
                for (int dx = 0; dx < exitSize; dx++)
                {
                    int x = startX + dx;
                    if (x >= 0 && x < w) { m[x, 0] = '0'; if (h > 1) m[x, 1] = '0'; }
                }
            }
            if (exitB)
            {
                int startX = w / 2 - exitSize / 2;
                for (int dx = 0; dx < exitSize; dx++)
                {
                    int x = startX + dx;
                    if (x >= 0 && x < w) { m[x, h - 1] = '0'; if (h > 1) m[x, h - 2] = '0'; }
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Post-processing
        // ------------------------------------------------------------------ //

        private static void CleanupTiles(char[,] m, int w, int h)
        {
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (m[x, y] != '0')
                    {
                        // Remove isolated single tiles
                        int neighbors = 0;
                        if (m[x - 1, y] != '0') neighbors++;
                        if (m[x + 1, y] != '0') neighbors++;
                        if (m[x, y - 1] != '0') neighbors++;
                        if (m[x, y + 1] != '0') neighbors++;
                        if (neighbors == 0) m[x, y] = '0';
                    }
                    else
                    {
                        // Fill 1-tile air gaps surrounded by 3+ solids
                        int solidCount = 0;
                        char best = '1'; int bestN = 0;
                        void Check(int cx, int cy)
                        {
                            char t = m[cx, cy];
                            if (t != '0')
                            {
                                solidCount++;
                                // Track most common material
                                if (t == best) bestN++;
                                else if (bestN == 0) { best = t; bestN = 1; }
                            }
                        }
                        Check(x - 1, y); Check(x + 1, y);
                        Check(x, y - 1); Check(x, y + 1);
                        if (solidCount >= 3) m[x, y] = best;
                    }
                }
            }
        }

        private static void ErodeForSpace(char[,] m, int w, int h, float erodePercent)
        {
            var rng = new Random();
            var solidTiles = new List<(int x, int y, float score)>();

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    if (m[x, y] != '0')
                    {
                        int neighbors = 0;
                        if (m[x - 1, y] != '0') neighbors++;
                        if (m[x + 1, y] != '0') neighbors++;
                        if (m[x, y - 1] != '0') neighbors++;
                        if (m[x, y + 1] != '0') neighbors++;
                        float edgeScore = 4 - neighbors + (float)rng.NextDouble() * 0.5f;
                        solidTiles.Add((x, y, edgeScore));
                    }
                }
            }

            // Sort by edge score descending (most exposed tiles removed first)
            solidTiles.Sort((a, b) => b.score.CompareTo(a.score));

            int toRemove = (int)(solidTiles.Count * erodePercent);
            for (int i = 0; i < toRemove && i < solidTiles.Count; i++)
            {
                var t = solidTiles[i];
                m[t.x, t.y] = '0';
            }
        }

        // ------------------------------------------------------------------ //
        //  Background generation
        // ------------------------------------------------------------------ //

        private static char[,] GenerateBackground(char[,] fg, int w, int h, char mat)
        {
            var bg = new char[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    bg[x, y] = '0';

            // Extend solids 1 tile outward for background fill
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (fg[x, y] != '0')
                    {
                        bg[x, y] = mat;
                        // Extend to adjacent air tiles
                        if (x > 0 && fg[x - 1, y] == '0') bg[x - 1, y] = mat;
                        if (x < w - 1 && fg[x + 1, y] == '0') bg[x + 1, y] = mat;
                        if (y > 0 && fg[x, y - 1] == '0') bg[x, y - 1] = mat;
                        if (y < h - 1 && fg[x, y + 1] == '0') bg[x, y + 1] = mat;
                    }
                }
            }
            return bg;
        }

        // ------------------------------------------------------------------ //
        //  Entity generation
        // ------------------------------------------------------------------ //

        private List<PCGEntityInfo> GenerateEntities(char[,] tiles, int w, int h,
            int roomX, int roomY, float density,
            bool exitL, bool exitR, bool exitT, bool exitB, string style)
        {
            var entities = new List<PCGEntityInfo>();
            int nextId = 1;

            PCGEntityInfo Add(string name, int x, int y)
            {
                var e = new PCGEntityInfo { Name = name, X = x, Y = y };
                e.Values["id"] = nextId++;
                entities.Add(e);
                return e;
            }

            // --- Player spawn ---
            int spawnX, spawnY;
            if (exitL)
            {
                spawnX = 24;
                spawnY = (h / 2) * 8;
            }
            else if (exitT)
            {
                spawnX = (w / 2) * 8;
                spawnY = 24;
            }
            else
            {
                spawnX = 24;
                spawnY = (h / 2) * 8;
            }

            // Find ground below spawn
            int spawnTX = spawnX / 8;
            int spawnTY = spawnY / 8;
            for (int sy = spawnTY; sy < h; sy++)
            {
                if (spawnTX >= 0 && spawnTX < w && tiles[spawnTX, sy] != '0')
                {
                    spawnY = (sy - 1) * 8;
                    break;
                }
            }
            Add("player", roomX + spawnX, roomY + spawnY);

            // --- Scan tiles for entity placement ---
            bool isSpace = style == "space" || style == "deepSpace";
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    char tile = tiles[x, y];
                    char above = tiles[x, y - 1];
                    char below = (y + 1 < h) ? tiles[x, y + 1] : '0';

                    // Platform top: solid with air above
                    if (tile != '0' && above == '0')
                    {
                        float roll = (float)_rng.NextDouble();

                        if (roll < density * 0.05f)
                        {
                            var e = Add("refill", roomX + (x) * 8, roomY + (y - 1) * 8);
                            e.Values["oneUse"] = false;
                            e.Values["twoDash"] = isSpace && _rng.NextDouble() > 0.5;
                        }
                        else if (roll < density * 0.10f)
                        {
                            var e = Add("spring", roomX + (x) * 8, roomY + (y) * 8);
                            e.Values["orientation"] = 0;
                        }
                    }

                    // Ceiling bottom: solid with air below → spikes
                    if (tile != '0' && below == '0')
                    {
                        float roll = (float)_rng.NextDouble();
                        if (roll < density * 0.08f)
                        {
                            var e = Add("spikesDown", roomX + (x) * 8, roomY + (y + 1) * 8);
                            e.Width = 8;
                            e.Values["type"] = "default";
                        }
                    }

                    // Large air gap → boosters
                    if (tile == '0' && above == '0' && below == '0')
                    {
                        float roll = (float)_rng.NextDouble();
                        if (roll < density * 0.01f)
                        {
                            var e = Add("booster", roomX + (x) * 8, roomY + (y) * 8);
                            e.Values["red"] = _rng.NextDouble() > 0.5;
                        }
                    }

                    // Space-specific: dream blocks, feathers
                    if (isSpace && tile == '0')
                    {
                        float roll = (float)_rng.NextDouble();
                        if (roll < density * 0.005f)
                        {
                            var e = Add("dreamBlock", roomX + (x) * 8, roomY + (y) * 8);
                            e.Width = _rng.Next(2, 5) * 8;
                            e.Height = _rng.Next(2, 4) * 8;
                        }
                        else if (roll < density * 0.008f)
                        {
                            Add("infiniteStar", roomX + (x) * 8, roomY + (y) * 8);
                        }
                    }
                }
            }

            return entities;
        }

        // ------------------------------------------------------------------ //
        //  Playability check (simplified flood-fill)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Check if the room is traversable from entrance to exit using a BFS flood-fill.
        /// Simulates basic platformer movement (walk left/right, jump up ≤3 tiles).
        /// </summary>
        private static bool IsPlayable(char[,] m, int w, int h,
            bool exitL, bool exitR, bool exitT, bool exitB, int exitSize)
        {
            // Find entrance position
            int startX, startY;
            if (exitL)
            {
                startX = 2;
                startY = h / 2;
            }
            else if (exitT)
            {
                startX = w / 2;
                startY = 2;
            }
            else
            {
                startX = 2;
                startY = h / 2;
            }

            // Find a target exit position
            int endX, endY;
            if (exitR)
            {
                endX = w - 3;
                endY = h / 2;
            }
            else if (exitB)
            {
                endX = w / 2;
                endY = h - 3;
            }
            else
            {
                endX = w - 3;
                endY = h / 2;
            }

            // BFS with platformer physics approximation
            var visited = new bool[w, h];
            var queue = new Queue<(int x, int y)>();

            // Drop start to nearest ground
            for (int y = startY; y < h; y++)
            {
                if (m[startX, y] != '0')
                {
                    startY = y - 1;
                    break;
                }
            }

            queue.Enqueue((startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();

                // Check if we reached the target region
                if (Math.Abs(cx - endX) <= 2 && Math.Abs(cy - endY) <= 3)
                    return true;

                // Try movements: walk left/right, fall down, jump up (≤4 tiles)
                (int dx, int dy)[] moves =
                {
                    (-1, 0), (1, 0),   // walk
                    (0, 1),             // fall
                    (0, -1), (0, -2), (0, -3), (0, -4), // jump
                    (-1, -1), (1, -1), (-1, -2), (1, -2), // diagonal jumps
                };

                foreach (var (dx, dy) in moves)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny] && m[nx, ny] == '0')
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ //
        //  Style helpers
        // ------------------------------------------------------------------ //

        private static char GetStyleMaterial(string style, char fallback)
        {
            return StyleTiles.GetValueOrDefault(style ?? "normal", fallback);
        }
    }

    /// <summary>Extension to allow inline mutation of return values.</summary>
    internal static class PCGRoomResultExtensions
    {
        public static T Also<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }
    }
}
