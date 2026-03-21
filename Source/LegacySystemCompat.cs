namespace MaggyHelper;

using global::MaggyHelper.Extensions.Core;

public sealed class PlayerExtensionCore
{
    public static PlayerExtensionCore Instance { get; } = new();

    public void Hook()
    {
    }

    public void Unhook()
    {
    }
}

public static class MadelineCombatSystem
{
    public static void Load()
    {
    }

    public static void Unload()
    {
    }
}

public static class KirbyPauseMenuCompat
{
    public static void Load()
    {
    }

    public static void Unload()
    {
    }
}

public static class LevelStateManager
{
    private static PlayerCharacter _activeCharacter = PlayerCharacter.MadelineCharacter;

    public static void SetActiveCharacter(PlayerCharacter character, Level level)
    {
        _activeCharacter = character;
    }

    public static PlayerCharacter GetActivePlayerCharacter()
    {
        return _activeCharacter;
    }
}
