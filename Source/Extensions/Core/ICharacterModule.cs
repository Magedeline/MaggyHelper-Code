namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Interface for character-specific modules that extend the base player system.
    /// Each playable character (Kirby, Ralsei, etc.) should implement this interface.
    /// </summary>
    public interface ICharacterModule
    {
        /// <summary>
        /// Unique identifier for this character module
        /// </summary>
        string CharacterId { get; }
        
        /// <summary>
        /// Display name for this character
        /// </summary>
        string CharacterName { get; }
        
        /// <summary>
        /// Initialize this character module's hooks and extensions
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Uninitialize this character module's hooks and extensions
        /// </summary>
        void Uninitialize();
        
        /// <summary>
        /// Check if this character module is currently active for a player
        /// </summary>
        bool IsActive(Celeste.Player player);
        
        /// <summary>
        /// Enable this character module for a player
        /// </summary>
        void Enable(Celeste.Player player);
        
        /// <summary>
        /// Disable this character module for a player
        /// </summary>
        void Disable(Celeste.Player player);
        
        /// <summary>
        /// Called when the level loads
        /// </summary>
        void OnLevelLoad(Celeste.Level level);
        
        /// <summary>
        /// Called when the level unloads
        /// </summary>
        void OnLevelUnload(Celeste.Level level);
    }
}
