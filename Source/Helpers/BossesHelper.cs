namespace MaggyHelper.Helpers;

/// <summary>
/// Base class for boss entities in the BossesHelper system.
/// This is a stub implementation for when BossesHelper mod is not available.
/// When BossesHelper is installed, this will be superseded by the mod's implementation.
/// </summary>
public abstract class BossActor : Entity
{
    /// <summary>
    /// The current health of the boss
    /// </summary>
    public int Health { get; protected set; } = 100;
    
    /// <summary>
    /// Maximum health of the boss
    /// </summary>
    public int MaxHealth { get; protected set; } = 100;
    
    /// <summary>
    /// Whether the boss is currently active
    /// </summary>
    public bool IsActive { get; protected set; } = true;
    
    /// <summary>
    /// Whether the boss has been defeated
    /// </summary>
    public bool IsDefeated { get; protected set; } = false;
    
    /// <summary>
    /// The sprite for the boss
    /// </summary>
    protected Sprite Sprite { get; set; }
    
    /// <summary>
    /// Reference to the player
    /// </summary>
    protected global::Celeste.Player Player { get; set; }
    
    /// <summary>
    /// Current velocity of the boss
    /// </summary>
    protected Vector2 Speed { get; set; } = Vector2.Zero;
    
    /// <summary>
    /// Whether the boss is currently on the ground
    /// </summary>
    protected bool Grounded { get; set; } = false;

    /// <summary>
    /// Reference to the level
    /// </summary>
    protected Level Level => Scene as Level;
    
    public BossActor(Vector2 position) : base(position)
    {
        Depth = -10;
    }
    
    public BossActor(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Depth = -10;
    }

    /// <summary>
    /// Constructor with full parameters for boss configuration
    /// </summary>
    public BossActor(Vector2 position, string spriteName, Vector2 spriteScale, float maxFall, bool collidable, bool solidCollidable, float gravityMult, Collider collider) 
        : base(position)
    {
        Depth = -10;
        Collidable = collidable;
        Collider = collider;
        // Store additional parameters as needed
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Player = (scene as Level)?.Tracker.GetEntity<global::Celeste.Player>();
    }

    public override void Update()
    {
        base.Update();
        
        if (Player == null || Player.Dead)
        {
            Player = Level?.Tracker.GetEntity<global::Celeste.Player>();
        }
    }

    /// <summary>
    /// Deals damage to the boss
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        if (IsDefeated) return;
        
        Health -= amount;
        if (Health <= 0)
        {
            Health = 0;
            OnDefeated();
        }
    }
    
    /// <summary>
    /// Called when the boss is defeated
    /// </summary>
    protected virtual void OnDefeated()
    {
        IsDefeated = true;
        IsActive = false;
    }
    
    /// <summary>
    /// Heals the boss
    /// </summary>
    public virtual void Heal(int amount)
    {
        Health = Math.Min(Health + amount, MaxHealth);
    }
    
    /// <summary>
    /// Resets the boss to full health
    /// </summary>
    public virtual void Reset()
    {
        Health = MaxHealth;
        IsDefeated = false;
        IsActive = true;
    }
    
    /// <summary>
    /// Starts a boss attack pattern
    /// </summary>
    protected virtual IEnumerator AttackRoutine()
    {
        yield break;
    }
    
    /// <summary>
    /// Starts the boss fight
    /// </summary>
    public virtual void StartBossFight()
    {
        IsActive = true;
    }
    
    /// <summary>
    /// Ends the boss fight
    /// </summary>
    public virtual void EndBossFight()
    {
        IsActive = false;
    }
}

/// <summary>
/// Interface for boss projectiles
/// </summary>
public interface IBossProjectile
{
    Vector2 Velocity { get; set; }
    int Damage { get; set; }
    void Launch(Vector2 direction, float speed);
}

/// <summary>
/// Base class for boss projectiles
/// </summary>
public class BossProjectile : Entity, IBossProjectile
{
    public Vector2 Velocity { get; set; }
    public int Damage { get; set; } = 1;
    
    protected Sprite Sprite { get; set; }
    
    public BossProjectile(Vector2 position) : base(position)
    {
        Depth = -5;
    }
    
    public virtual void Launch(Vector2 direction, float speed)
    {
        Velocity = direction * speed;
    }
    
    public override void Update()
    {
        base.Update();
        Position += Velocity * Engine.DeltaTime;
        
        // Remove if off-screen
        if (Scene is Level level)
        {
            if (!level.IsInBounds(Position))
            {
                RemoveSelf();
            }
        }
    }
}

/// <summary>
/// Helper methods for boss entities
/// </summary>
public static class BossHelperExtensions
{
    /// <summary>
    /// Gets the direction to the player
    /// </summary>
    public static Vector2 DirectionToPlayer(this Entity entity, global::Celeste.Player player)
    {
        if (player == null) return Vector2.Zero;
        return (player.Position - entity.Position).SafeNormalize();
    }
    
    /// <summary>
    /// Gets the distance to the player
    /// </summary>
    public static float DistanceToPlayer(this Entity entity, global::Celeste.Player player)
    {
        if (player == null) return float.MaxValue;
        return Vector2.Distance(entity.Position, player.Position);
    }
    
    /// <summary>
    /// Checks if the player is within a certain range
    /// </summary>
    public static bool IsPlayerInRange(this Entity entity, global::Celeste.Player player, float range)
    {
        return entity.DistanceToPlayer(player) <= range;
    }
}
