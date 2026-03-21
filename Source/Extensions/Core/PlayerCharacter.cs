namespace MaggyHelper.Extensions.Core;

public static class PlayerCharacterIds
{
    public const string Default = "default";
    public const string Madeline = "madeline";
    public const string Kirby = "kirby";
}

public readonly struct PlayerCharacter
{
    public string Id { get; }
    public bool IsKirby { get; }

    public static PlayerCharacter MadelineCharacter => new(PlayerCharacterIds.Madeline, false);
    public static PlayerCharacter KirbyCharacter => new(PlayerCharacterIds.Kirby, true);

    public PlayerCharacter(string id, bool isKirby)
    {
        Id = NormalizeId(id);
        IsKirby = isKirby;
    }

    public static PlayerCharacter FromId(string id)
    {
        string normalized = NormalizeId(id);
        return normalized == PlayerCharacterIds.Kirby
            ? KirbyCharacter
            : MadelineCharacter;
    }

    public static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return PlayerCharacterIds.Madeline;

        string normalized = id.Trim().ToLowerInvariant();
        return normalized switch
        {
            PlayerCharacterIds.Default => PlayerCharacterIds.Madeline,
            "maggy_player" => PlayerCharacterIds.Madeline,
            "maddy" => PlayerCharacterIds.Madeline,
            "kirby_player" => PlayerCharacterIds.Kirby,
            _ => normalized
        };
    }
}
