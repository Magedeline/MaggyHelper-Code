using MaggyHelper.Extensions.Kirby;
using MaggyHelper.Extensions.Kirby.Core;
using MaggyHelper.Extensions.Kirby.ModCompat;

namespace MaggyHelper.Extensions.Core
{
    /// <summary>
    /// Kirby-specific character module implementing the full Kirby gameplay system.
    /// Integrates with the player extension core to provide Kirby mode.
    /// Manages the KirbyPlayerExtension entity which provides modular abilities:
    ///   Inhale, Hover, Spit, Melee, Range, and Copy Ability.
    /// </summary>
    public class KirbyCharacterModule : CharacterModuleBase
    {
        public override string CharacterId => PlayerCharacterIds.Kirby;
        public override string CharacterName => "Kirby";

        private readonly KirbyPlayerCore _playerCore;
        private readonly KirbyPlayerExtensionCore _extensionCore;

        public KirbyCharacterModule()
        {
            _playerCore = new KirbyPlayerCore();
            _extensionCore = new KirbyPlayerExtensionCore(_playerCore);
        }

        protected override void OnInitialize()
        {
            _extensionCore.Hook();
            KirbyModCompatManager.Initialize();
        }

        protected override void OnUninitialize()
        {
            KirbyModCompatManager.Uninitialize();
            _extensionCore.Unhook();
        }

        protected override void OnEnable(Player player, Level level)
        {
            _playerCore.Enable(player, level);
        }

        protected override void OnDisable(Player player, Level level)
        {
            _playerCore.Disable(player, level);
        }

        protected override void OnLevelLoaded(Level level)
        {
            _extensionCore.OnLevelLoaded(level);
        }

        protected override void OnLevelUnloaded(Level level)
        {
            _extensionCore.OnLevelUnloaded(level);
        }

        #region Public API

        /// <summary>
        /// Get the current Kirby power state
        /// </summary>
        public KirbyMode.KirbyPowerState GetPowerState(Level level)
        {
            return _playerCore.GetPowerState(level);
        }

        /// <summary>
        /// Set the Kirby power state
        /// </summary>
        public void SetPowerState(Level level, KirbyMode.KirbyPowerState power)
        {
            _playerCore.SetPowerState(level, power);
        }

        /// <summary>
        /// Get Kirby's current health
        /// </summary>
        public int GetHealth(Level level)
        {
            return _playerCore.GetHealth(level);
        }

        /// <summary>
        /// Heal Kirby
        /// </summary>
        public void Heal(Level level, int amount = 1)
        {
            _playerCore.Heal(level, amount);
        }

        /// <summary>
        /// Damage Kirby
        /// </summary>
        public void Damage(Level level, int amount = 1)
        {
            _playerCore.Damage(level, amount);
        }

        #endregion
    }
}
