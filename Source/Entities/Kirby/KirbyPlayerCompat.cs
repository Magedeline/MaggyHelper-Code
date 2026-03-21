namespace MaggyHelper.Entities;

[Tracked]
public class KirbyPlayer : Entity
{
    public bool IsInhaling { get; set; }
    public int MaxHealth { get; set; } = 6;
    public int CurrentHealth { get; set; } = 6;
    public bool IsDead => CurrentHealth <= 0;
    public float MaxStamina { get; set; } = 100f;
    public float CurrentStamina { get; set; } = 100f;
    public Facings Facing { get; set; } = Facings.Right;
    public List<Entity> InhaledEntities { get; } = new();

    public KirbyPlayer() : base(Vector2.Zero)
    {
    }

    public void Heal(int amount)
    {
        CurrentHealth = Calc.Clamp(CurrentHealth + Math.Max(0, amount), 0, MaxHealth);
    }
}

[Tracked]
public class KirbyPlayerExtension : Entity
{
    public bool IsEnabled { get; set; }

    public bool IsInhaling
    {
        get => Inhale.IsInhaling;
        set => Inhale.IsInhaling = value;
    }

    public int MaxHealth { get; set; } = 6;
    public int CurrentHealth { get; set; } = 6;
    public bool IsDead => CurrentHealth <= 0;
    public float MaxStamina { get; set; } = 100f;
    public float CurrentStamina { get; set; } = 100f;
    public Facings Facing { get; set; } = Facings.Right;

    public KirbyPlayerExtension() : base(Vector2.Zero)
    {
    }

    public void Heal(int amount)
    {
        CurrentHealth = Calc.Clamp(CurrentHealth + Math.Max(0, amount), 0, MaxHealth);
    }

    public void SetPowerState(global::MaggyHelper.Extensions.KirbyMode.KirbyPowerState state)
    {
        _powerState = state;
    }

    public global::MaggyHelper.Extensions.KirbyMode.KirbyPowerState GetPowerState()
    {
        return _powerState;
    }

    public KirbyInhaleState Inhale { get; } = new();
    private global::MaggyHelper.Extensions.KirbyMode.KirbyPowerState _powerState;

    public class KirbyInhaleState
    {
        public bool IsInhaling { get; set; }
        public List<Entity> InhaledEntities { get; } = new();
    }
}
