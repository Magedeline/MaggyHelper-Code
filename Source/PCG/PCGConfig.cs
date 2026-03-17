using System.Collections.Generic;

namespace MaggyHelper.PCG
{
    /// <summary>
    /// Configuration for a single PCG room generation pass.
    /// Mirrors the Lönn pcg.PRESETS table so both editor and runtime use the same parameters.
    /// </summary>
    public class PCGRoomConfig
    {
        /// <summary>9-char MdMC neighbourhood config string (e.g. "000011012").</summary>
        public string MarkovConfig { get; set; } = "000011012";

        /// <summary>Room width in tiles.</summary>
        public int RoomWidth { get; set; } = 40;

        /// <summary>Room height in tiles.</summary>
        public int RoomHeight { get; set; } = 23;

        /// <summary>Width/height of exit openings in tiles.</summary>
        public int ExitSize { get; set; } = 5;

        /// <summary>Tileset character for borders (e.g. '1' = dirt).</summary>
        public char BorderMaterial { get; set; } = '1';

        /// <summary>MdMC backtracking depth.</summary>
        public int BacktrackDepth { get; set; } = 3;

        /// <summary>Entity placement probability per eligible tile (0–1).</summary>
        public float EntityDensity { get; set; } = 0.15f;

        /// <summary>Number of post-generation tile-cleanup passes.</summary>
        public int CleanupPasses { get; set; } = 2;

        /// <summary>Probability of branching during skeleton generation (0–1).</summary>
        public float BranchProbability { get; set; } = 0.3f;

        /// <summary>Validate rooms with A* pathfinding before accepting them.</summary>
        public bool PlayabilityCheck { get; set; } = true;

        /// <summary>Maximum retries for generating a playable room.</summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>Visual style name used for tile/entity/styleground selection.</summary>
        public string RoomStyle { get; set; } = "normal";

        /// <summary>If true, skip placing solid border walls (used for space rooms).</summary>
        public bool NoBorders { get; set; } = false;

        /// <summary>Fraction of solid tiles to erode for space-style rooms (0–1).</summary>
        public float SpaceErodePercent { get; set; } = 0f;

        /// <summary>Use open-top "trimmed" borders instead of full dungeon enclosure.</summary>
        public bool TrimmedBorders { get; set; } = false;
    }

    /// <summary>
    /// Named presets matching the Lönn <c>pcg.PRESETS</c> table.
    /// </summary>
    public static class PCGPresets
    {
        public static PCGRoomConfig Default => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 23,
            ExitSize = 5, BorderMaterial = '1',
            BacktrackDepth = 3, EntityDensity = 0.15f,
            CleanupPasses = 2, BranchProbability = 0.3f,
            PlayabilityCheck = true, MaxRetries = 10,
            RoomStyle = "normal",
        };

        public static PCGRoomConfig Open => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 48, RoomHeight = 27,
            ExitSize = 6, BorderMaterial = '1',
            BacktrackDepth = 4, EntityDensity = 0.2f,
            CleanupPasses = 3, BranchProbability = 0.2f,
            PlayabilityCheck = true, MaxRetries = 15,
            RoomStyle = "normal",
        };

        public static PCGRoomConfig Tight => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 32, RoomHeight = 18,
            ExitSize = 4, BorderMaterial = '1',
            BacktrackDepth = 2, EntityDensity = 0.1f,
            CleanupPasses = 1, BranchProbability = 0.4f,
            PlayabilityCheck = true, MaxRetries = 8,
            RoomStyle = "normal",
        };

        public static PCGRoomConfig Space => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 48, RoomHeight = 27,
            ExitSize = 6, BorderMaterial = '1',
            BacktrackDepth = 4, EntityDensity = 0.25f,
            CleanupPasses = 4, BranchProbability = 0.2f,
            PlayabilityCheck = false, MaxRetries = 8,
            RoomStyle = "space", SpaceErodePercent = 0.55f,
            NoBorders = true,
        };

        public static PCGRoomConfig DeepSpace => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 56, RoomHeight = 32,
            ExitSize = 8, BorderMaterial = '1',
            BacktrackDepth = 4, EntityDensity = 0.3f,
            CleanupPasses = 5, BranchProbability = 0.15f,
            PlayabilityCheck = false, MaxRetries = 8,
            RoomStyle = "deepSpace", SpaceErodePercent = 0.70f,
            NoBorders = true,
        };

        public static PCGRoomConfig Resort => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 23,
            ExitSize = 5, BorderMaterial = '5',
            BacktrackDepth = 3, EntityDensity = 0.2f,
            CleanupPasses = 2, BranchProbability = 0.3f,
            PlayabilityCheck = true, MaxRetries = 10,
            RoomStyle = "resort",
        };

        public static PCGRoomConfig Temple => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 23,
            ExitSize = 5, BorderMaterial = 'd',
            BacktrackDepth = 3, EntityDensity = 0.18f,
            CleanupPasses = 2, BranchProbability = 0.35f,
            PlayabilityCheck = true, MaxRetries = 10,
            RoomStyle = "temple",
        };

        public static PCGRoomConfig Summit => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 27,
            ExitSize = 5, BorderMaterial = 'i',
            BacktrackDepth = 3, EntityDensity = 0.22f,
            CleanupPasses = 2, BranchProbability = 0.25f,
            PlayabilityCheck = true, MaxRetries = 12,
            RoomStyle = "summit",
        };

        public static PCGRoomConfig Core => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 23,
            ExitSize = 5, BorderMaterial = 'k',
            BacktrackDepth = 3, EntityDensity = 0.2f,
            CleanupPasses = 2, BranchProbability = 0.3f,
            PlayabilityCheck = true, MaxRetries = 10,
            RoomStyle = "core",
        };

        public static PCGRoomConfig Wind => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 40, RoomHeight = 30,
            ExitSize = 5, BorderMaterial = '3',
            BacktrackDepth = 3, EntityDensity = 0.2f,
            CleanupPasses = 2, BranchProbability = 0.2f,
            PlayabilityCheck = true, MaxRetries = 10,
            RoomStyle = "wind",
        };

        public static PCGRoomConfig Farewell => new()
        {
            MarkovConfig = "000011012",
            RoomWidth = 48, RoomHeight = 27,
            ExitSize = 6, BorderMaterial = 'n',
            BacktrackDepth = 3, EntityDensity = 0.22f,
            CleanupPasses = 3, BranchProbability = 0.25f,
            PlayabilityCheck = true, MaxRetries = 12,
            RoomStyle = "farewell",
        };

        /// <summary>Look up a preset by name (case-insensitive). Returns <see cref="Default"/> for unknown names.</summary>
        public static PCGRoomConfig GetByName(string name)
        {
            return (name ?? "default").ToLowerInvariant() switch
            {
                "default"   => Default,
                "open"      => Open,
                "tight"     => Tight,
                "space"     => Space,
                "deepspace" => DeepSpace,
                "resort"    => Resort,
                "temple"    => Temple,
                "summit"    => Summit,
                "core"      => Core,
                "wind"      => Wind,
                "farewell"  => Farewell,
                _           => Default,
            };
        }

        /// <summary>All available preset names.</summary>
        public static IReadOnlyList<string> AllNames { get; } = new[]
        {
            "default", "open", "tight", "space", "deepSpace",
            "resort", "temple", "summit", "core", "wind", "farewell",
        };
    }
}
