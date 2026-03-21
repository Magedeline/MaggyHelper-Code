using System;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Broad classification for playable character handling.
    /// </summary>
    public enum PlayerCharacterKind
    {
        Madeline,
        Kirby,
        Other
    }

    /// <summary>
    /// Canonical string IDs used by runtime and save/session state.
    /// </summary>
    public static class PlayerCharacterIds
    {
        public const string Default = "default";
        public const string Madeline = "madeline";
        public const string Kirby = "kirby";
    }

    /// <summary>
    /// Typed wrapper around the active player character ID.
    /// </summary>
    public readonly struct PlayerCharacter : IEquatable<PlayerCharacter>
    {
        public static PlayerCharacter MadelineCharacter => new(PlayerCharacterIds.Madeline, PlayerCharacterKind.Madeline);
        public static PlayerCharacter KirbyCharacter => new(PlayerCharacterIds.Kirby, PlayerCharacterKind.Kirby);

        public string Id { get; }
        public PlayerCharacterKind Kind { get; }

        public bool IsMadeline => Kind == PlayerCharacterKind.Madeline;
        public bool IsKirby => Kind == PlayerCharacterKind.Kirby;

        public PlayerCharacter(string characterId)
        {
            string normalizedId = NormalizeId(characterId);
            Id = normalizedId;
            Kind = ClassifyKind(normalizedId);
        }

        private PlayerCharacter(string characterId, PlayerCharacterKind kind)
        {
            Id = characterId;
            Kind = kind;
        }

        public static PlayerCharacter FromId(string characterId)
        {
            return new PlayerCharacter(characterId);
        }

        public static string NormalizeId(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return PlayerCharacterIds.Madeline;
            }

            string trimmed = characterId.Trim();
            if (trimmed.Equals(PlayerCharacterIds.Default, StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(PlayerCharacterIds.Madeline, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerCharacterIds.Madeline;
            }

            if (trimmed.Equals(PlayerCharacterIds.Kirby, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerCharacterIds.Kirby;
            }

            return trimmed;
        }

        public bool Equals(PlayerCharacter other)
        {
            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerCharacter other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty);
        }

        public override string ToString()
        {
            return Id;
        }

        public static bool operator ==(PlayerCharacter left, PlayerCharacter right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerCharacter left, PlayerCharacter right)
        {
            return !left.Equals(right);
        }

        private static PlayerCharacterKind ClassifyKind(string characterId)
        {
            if (characterId.StartsWith(PlayerCharacterIds.Kirby, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerCharacterKind.Kirby;
            }

            if (characterId.Equals(PlayerCharacterIds.Madeline, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerCharacterKind.Madeline;
            }

            return PlayerCharacterKind.Other;
        }
    }
}