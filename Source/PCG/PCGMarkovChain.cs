using System;
using System.Collections.Generic;
using System.Text;
using MaggyHelper.Utils;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Multi-dimensional Markov Chain (MdMC) tile generator for Celeste rooms.
    /// C# port of the Lönn <c>libraries/pcg/markov.lua</c>.
    ///
    /// Based on "Towards a Celeste AI Framework" (Robinet, Gómez-Maureira, Preuss 2025).
    /// A 3×3 configuration matrix encodes which neighbours influence the current tile.
    /// </summary>
    public class PCGMarkovChain
    {
        // Valid foreground tile IDs used in vanilla Celeste
        private static readonly char[] ValidFgTiles =
        {
            '1', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h',
            'i', 'j', 'k', 'l', 'm', 'n',
        };

        private readonly Random _rng;

        /// <summary>
        /// Neighbourhood offsets derived from the config string.
        /// Each entry is (dx, dy) relative to the target tile.
        /// </summary>
        public List<(int dx, int dy)> Offsets { get; private set; }

        /// <summary>
        /// The trained Dictionary of Probability Transitions.
        /// Key = n-gram string of neighbour tile values; Value = distribution over tiles.
        /// </summary>
        public Dictionary<string, Dictionary<char, int>> DPT { get; } = new();

        /// <summary>Default tile emitted when no training data matches the context.</summary>
        public char DefaultTile { get; set; } = '0';

        public PCGMarkovChain(Random rng = null)
        {
            _rng = rng ?? new Random();
            Offsets = new List<(int dx, int dy)>();
        }

        // ------------------------------------------------------------------ //
        //  Configuration parsing
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Parse a 9-character config string (e.g. "000011012") into neighbourhood offsets.
        /// '2' marks the target tile; '1' marks context neighbours.
        /// </summary>
        public void ParseConfig(string configStr)
        {
            if (configStr == null || configStr.Length != 9)
                throw new ArgumentException("Config string must be exactly 9 characters.");

            int targetRow = -1, targetCol = -1;
            for (int i = 0; i < 9; i++)
            {
                if (configStr[i] == '2')
                {
                    targetRow = i / 3;
                    targetCol = i % 3;
                    break;
                }
            }
            if (targetRow < 0) throw new ArgumentException("Config must contain exactly one '2' (target tile).");

            Offsets.Clear();
            for (int i = 0; i < 9; i++)
            {
                if (configStr[i] == '1')
                {
                    int row = i / 3;
                    int col = i % 3;
                    Offsets.Add((col - targetCol, row - targetRow));
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  N-gram extraction
        // ------------------------------------------------------------------ //

        private string GetNgram(char[,] matrix, int x, int y, int w, int h)
        {
            var sb = new StringBuilder(Offsets.Count * 2);
            for (int i = 0; i < Offsets.Count; i++)
            {
                if (i > 0) sb.Append(',');
                int nx = x + Offsets[i].dx;
                int ny = y + Offsets[i].dy;
                char tile = (nx >= 0 && nx < w && ny >= 0 && ny < h) ? matrix[nx, ny] : '0';
                sb.Append(tile);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ //
        //  Training
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Train the Markov chain from a single room tile matrix.
        /// Accumulates into the existing DPT (call multiple times for multi-room training).
        /// </summary>
        /// <param name="tiles">Tile matrix [x, y], 0-indexed.</param>
        /// <param name="w">Width in tiles.</param>
        /// <param name="h">Height in tiles.</param>
        public void TrainFromMatrix(char[,] tiles, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    string ngram = GetNgram(tiles, x, y, w, h);
                    char tile = tiles[x, y];

                    if (!DPT.TryGetValue(ngram, out var dist))
                    {
                        dist = new Dictionary<char, int>();
                        DPT[ngram] = dist;
                    }
                    dist[tile] = dist.GetValueOrDefault(tile) + 1;
                }
            }
        }

        /// <summary>
        /// Train from a Celeste tile string (newline-delimited rows).
        /// </summary>
        public void TrainFromTileString(string tileStr, int w, int h)
        {
            var matrix = ParseTileString(tileStr, w, h);
            TrainFromMatrix(matrix, w, h);
        }

        // ------------------------------------------------------------------ //
        //  Generation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Generate a room tile matrix using the trained MdMC model.
        /// Tiles are generated left-to-right, top-to-bottom.
        /// </summary>
        /// <param name="w">Width in tiles.</param>
        /// <param name="h">Height in tiles.</param>
        /// <param name="backtrackDepth">Max tiles to re-generate on dead-end (0 = none).</param>
        /// <returns>Tile matrix [x, y].</returns>
        public char[,] Generate(int w, int h, int backtrackDepth = 3)
        {
            var matrix = new char[w, h];
            // Fill with air
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    matrix[x, y] = '0';

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    string ngram = GetNgram(matrix, x, y, w, h);

                    if (DPT.TryGetValue(ngram, out var dist))
                    {
                        matrix[x, y] = SampleDistribution(dist);
                    }
                    else
                    {
                        // Backtrack: try regenerating previous tiles
                        bool resolved = false;
                        if (backtrackDepth > 0)
                        {
                            resolved = TryBacktrack(matrix, x, y, w, h, backtrackDepth);
                        }
                        if (!resolved)
                        {
                            matrix[x, y] = DefaultTile;
                        }
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Generate a room without training data by using a simple noise-based approach.
        /// Produces a basic platformer layout with ground, platforms, and air.
        /// </summary>
        public char[,] GenerateFallback(int w, int h, char material = '1', float solidRatio = 0.35f)
        {
            var matrix = new char[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    matrix[x, y] = '0';

            // Ground floor: bottom 3 rows solid
            for (int y = h - 3; y < h; y++)
                for (int x = 0; x < w; x++)
                    matrix[x, y] = material;

            // Floating platforms at semi-random intervals
            int platformCount = Math.Max(2, (int)(w * h * solidRatio * 0.02f));
            for (int p = 0; p < platformCount; p++)
            {
                int px = _rng.Next(3, w - 6);
                int py = _rng.Next(4, h - 5);
                int pLen = _rng.Next(3, 8);
                for (int dx = 0; dx < pLen && px + dx < w - 1; dx++)
                {
                    matrix[px + dx, py] = material;
                }
            }

            return matrix;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private char SampleDistribution(Dictionary<char, int> dist)
        {
            return _rng.WeightedChoice(dist, DefaultTile);
        }

        private bool TryBacktrack(char[,] matrix, int startX, int startY, int w, int h, int depth)
        {
            // Simple backtrack: re-roll the previous <depth> tiles and try again
            int linearPos = startY * w + startX;
            int backStart = Math.Max(0, linearPos - depth);

            for (int attempt = 0; attempt < depth * 2; attempt++)
            {
                // Re-generate tiles from backStart to current position
                for (int pos = backStart; pos <= linearPos; pos++)
                {
                    int bx = pos % w;
                    int by = pos / w;
                    string ngram = GetNgram(matrix, bx, by, w, h);

                    if (DPT.TryGetValue(ngram, out var dist))
                    {
                        matrix[bx, by] = SampleDistribution(dist);
                    }
                    else
                    {
                        matrix[bx, by] = DefaultTile;
                    }
                }

                // Check if the current position now has a valid ngram
                string currentNgram = GetNgram(matrix, startX, startY, w, h);
                if (DPT.ContainsKey(currentNgram))
                {
                    matrix[startX, startY] = SampleDistribution(DPT[currentNgram]);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parse a Celeste tile string into a char[x,y] matrix.</summary>
        public static char[,] ParseTileString(string tileStr, int w, int h)
        {
            var matrix = new char[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    matrix[x, y] = '0';

            if (string.IsNullOrEmpty(tileStr)) return matrix;

            string[] rows = tileStr.Split('\n');
            for (int y = 0; y < rows.Length && y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length && x < w; x++)
                {
                    matrix[x, y] = row[x];
                }
            }
            return matrix;
        }

        /// <summary>Convert a tile matrix back to a Celeste tile string.</summary>
        public static string ToTileString(char[,] matrix, int w, int h)
        {
            var sb = new StringBuilder(w * h + h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    sb.Append(matrix[x, y]);
                if (y < h - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
