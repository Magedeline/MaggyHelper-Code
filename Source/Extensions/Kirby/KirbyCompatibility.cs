using MonoMod.Utils;

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
        private const string KirbyEnabledKey = "MaggyHelper.KirbyEnabled";
        private const string KirbyPowerKey = "MaggyHelper.KirbyPower";

        public static bool IsKirbyMode(this global::Celeste.Player player)
        {
            if (player == null)
                return false;

            var level = player.Scene as Level;
            if (level?.Session?.GetFlag("kirby_mode") == true)
                return true;

            return new DynData<global::Celeste.Player>(player).Get<bool>(KirbyEnabledKey);
        }

        public static void EnableKirbyMode(this global::Celeste.Player player)
        {
            if (player == null)
                return;

            var dyn = new DynData<global::Celeste.Player>(player);
            dyn.Set(KirbyEnabledKey, true);

            if (player.Scene is Level level)
                level.Session?.SetFlag("kirby_mode", true);
        }

        public static void DisableKirbyMode(this global::Celeste.Player player)
        {
            if (player == null)
                return;

            var dyn = new DynData<global::Celeste.Player>(player);
            dyn.Set(KirbyEnabledKey, false);
            dyn.Set(KirbyPowerKey, KirbyMode.KirbyPowerState.None);

            if (player.Scene is Level level)
                level.Session?.SetFlag("kirby_mode", false);
        }

        public static void SetKirbyPowerState(this global::Celeste.Player player, KirbyMode.KirbyPowerState state)
        {
            if (player == null)
                return;

            var dyn = new DynData<global::Celeste.Player>(player);
            dyn.Set(KirbyPowerKey, state);
        }

        public static KirbyMode.KirbyPowerState GetKirbyPowerState(this global::Celeste.Player player)
        {
            if (player == null)
                return KirbyMode.KirbyPowerState.None;

            return new DynData<global::Celeste.Player>(player).Get<KirbyMode.KirbyPowerState>(KirbyPowerKey);
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
            if (player == null || amount <= 0)
                return false;

            Level level = player.Scene as Level;
            var manager = global::MaggyHelper.Entities.PlayerHealthManager.Instance
                ?? level?.Tracker?.GetEntity<global::MaggyHelper.Entities.PlayerHealthManager>();

            if (manager != null)
                return manager.Damage(amount);

            player.Die((player.Position - source).SafeNormalize());
            return true;
        }

        public static bool TryHealKirby(this global::Celeste.Player player, int amount)
        {
            if (player == null || amount <= 0)
                return false;

            Level level = player.Scene as Level;
            var manager = global::MaggyHelper.Entities.PlayerHealthManager.Instance
                ?? level?.Tracker?.GetEntity<global::MaggyHelper.Entities.PlayerHealthManager>();

            if (manager == null)
                return false;

            manager.Heal(amount);
            return true;
        }
    }
}

namespace MaggyHelper.Extensions.Kirby
{
    public static class KirbyNamespaceMarker
    {
    }
}
