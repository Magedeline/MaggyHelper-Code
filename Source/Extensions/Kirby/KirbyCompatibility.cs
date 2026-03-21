namespace MaggyHelper.Extensions
{
    [Tracked]
    public class KirbyMode : Entity
    {
        private KirbyPowerState _powerState = KirbyPowerState.None;

        public enum KirbyPowerState
        {
            None,
            Fire,
            Ice,
            Spark,
            Stone,
            Sword,
            Beam,
            Cutter,
            Bomb,
            Wheel,
            Needle,
            Mirror,
            Knight
        }

        public List<Entity> InhaledEntities { get; } = new();
        public bool IsInhaling { get; set; }
        public int MaxHealth { get; set; } = 6;
        public int CurrentHealth { get; set; } = 6;
        public bool IsDead => CurrentHealth <= 0;
        public float MaxStamina { get; set; } = 100f;
        public float CurrentStamina { get; set; } = 100f;
        public Facings Facing { get; set; } = Facings.Right;

        public KirbyMode() : base(Vector2.Zero)
        {
        }

        public KirbyPowerState GetPowerState() => _powerState;

        public void SetPowerState(KirbyPowerState state)
        {
            _powerState = state;
        }

        public void Heal(int amount)
        {
            CurrentHealth = Calc.Clamp(CurrentHealth + Math.Max(0, amount), 0, MaxHealth);
        }
    }

    public static class PlayerKirbyExtensions
    {
        private static Entities.KirbyPlayerExtension GetKirbyExtension(global::Celeste.Player player, bool createIfMissing)
        {
            if (player?.Scene is not Level level)
                return null;

            var extension = level.Tracker.GetEntity<Entities.KirbyPlayerExtension>();
            if (extension == null && createIfMissing)
            {
                extension = new Entities.KirbyPlayerExtension
                {
                    IsEnabled = level.Session?.GetFlag("kirby_mode") == true
                };
                level.Add(extension);
            }

            return extension;
        }

        public static bool IsKirbyMode(this global::Celeste.Player player)
        {
            return false;
        }

        public static void EnableKirbyMode(this global::Celeste.Player player)
        {
            if (player == null)
                return;

            if (player.Scene is Level level)
                level.Session?.SetFlag("kirby_mode", false);

            var extension = GetKirbyExtension(player, createIfMissing: false);
            if (extension != null)
            {
                extension.IsEnabled = false;
                extension.SetPowerState(KirbyMode.KirbyPowerState.None);
            }

            var legacy = (player.Scene as Level)?.Tracker.GetEntity<KirbyMode>();
            legacy?.SetPowerState(KirbyMode.KirbyPowerState.None);
        }

        public static void DisableKirbyMode(this global::Celeste.Player player)
        {
            if (player == null)
                return;

            if (player.Scene is Level level)
                level.Session?.SetFlag("kirby_mode", false);

            var extension = GetKirbyExtension(player, createIfMissing: false);
            if (extension != null)
            {
                extension.IsEnabled = false;
                extension.SetPowerState(KirbyMode.KirbyPowerState.None);
            }

            var legacy = (player.Scene as Level)?.Tracker.GetEntity<KirbyMode>();
            legacy?.SetPowerState(KirbyMode.KirbyPowerState.None);
        }

        public static void SetKirbyPowerState(this global::Celeste.Player player, KirbyMode.KirbyPowerState state)
        {
            if (player == null)
                return;

            var extension = GetKirbyExtension(player, createIfMissing: false);
            extension?.SetPowerState(KirbyMode.KirbyPowerState.None);

            var legacy = (player.Scene as Level)?.Tracker.GetEntity<KirbyMode>();
            legacy?.SetPowerState(KirbyMode.KirbyPowerState.None);
        }

        public static KirbyMode.KirbyPowerState GetKirbyPowerState(this global::Celeste.Player player)
        {
            return KirbyMode.KirbyPowerState.None;
        }

        public static bool IsKirbyPlayerMode(this global::Celeste.Player player)
        {
            return player.IsKirbyMode();
        }

        public static void EnableKirbyPlayerMode(this global::Celeste.Player player)
        {
            player.EnableKirbyMode();
        }

        public static void DisableKirbyPlayerMode(this global::Celeste.Player player)
        {
            player.DisableKirbyMode();
        }

        public static bool TryDamageKirby(this global::Celeste.Player player, int amount, Vector2 source)
        {
            return false;
        }

        public static bool TryHealKirby(this global::Celeste.Player player, int amount)
        {
            return false;
        }
    }
}

namespace MaggyHelper.Extensions.Kirby
{
    public static class KirbyNamespaceMarker
    {
    }
}
