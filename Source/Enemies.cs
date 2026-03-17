using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace MaggyHelper
{
    /// <summary>
    /// Base class for all enemy entities
    /// </summary>
    [Tracked]
    public abstract class Enemy : Actor
    {
        public int Health { get; protected set; }
        public int MaxHealth { get; protected set; }
        public bool IsAlive => Health > 0;
        public float InvincibilityTimer { get; protected set; }
        
        protected Sprite sprite;
        protected Vector2 startPosition;
        protected bool playerDead = false;
        // Cached player reference - updated in Update()
        protected Player cachedPlayer;

        public Enemy(Vector2 position, int maxHealth = 1)
            : base(position)
        {
            MaxHealth = maxHealth;
            Health = maxHealth;
            startPosition = position;
            Collidable = true;
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = -100;
        }

        public override void Update()
        {
            base.Update();
            
            if (InvincibilityTimer > 0)
            {
                InvincibilityTimer -= Engine.DeltaTime;
            }

            // Cache player reference once per frame for all derived classes
            cachedPlayer = Scene.Tracker.GetEntity<Player>();
            if (cachedPlayer != null && !playerDead && CollideCheck(cachedPlayer))
            {
                OnHitPlayer(cachedPlayer);
            }
        }

        public virtual void TakeDamage(int damage)
        {
            if (InvincibilityTimer > 0 || !IsAlive)
                return;

            Health -= damage;
            InvincibilityTimer = 0.5f;
            
            OnTakeDamage();
            
            if (Health <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            Health = Math.Min(Health + amount, MaxHealth);
        }

        protected virtual void OnTakeDamage()
        {
            // Flash effect, sound, etc.
        }

        protected virtual void OnHitPlayer(Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
            playerDead = true;
        }

        protected virtual void Die()
        {
            // Death animation, particles, etc.
            RemoveSelf();
        }

        public override void Render()
        {
            if (InvincibilityTimer > 0 && Scene.OnInterval(0.1f))
            {
                return; // Flashing effect
            }
            
            base.Render();
        }
    }

    /// <summary>
    /// Basic patrolling enemy
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/PatrolEnemy")]
    [Tracked]
    public class PatrolEnemy : Enemy
    {
        private Vector2 patrolStart;
        private Vector2 patrolEnd;
        private float speed;
        private bool movingToEnd = true;
        
        public PatrolEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            patrolStart = Position;
            patrolEnd = Position + new Vector2(data.Float("patrolDistance", 100f), 0f);
            speed = data.Float("speed", 30f);
            
            Collider = new Hitbox(12f, 12f, -6f, -12f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/enemies/patrol/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        public override void Update()
        {
            base.Update();
            
            Vector2 target = movingToEnd ? patrolEnd : patrolStart;
            Vector2 direction = (target - Position).SafeNormalize();
            
            MoveH(direction.X * speed * Engine.DeltaTime);
            
            if (Vector2.Distance(Position, target) < 4f)
            {
                movingToEnd = !movingToEnd;
            }
            
            sprite.Scale.X = Math.Sign(direction.X);
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }

    /// <summary>
    /// Flying enemy that swoops at the player
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/FlyingEnemy")]
    [Tracked]
    public class FlyingEnemy : Enemy
    {
        private Vector2 homePosition;
        private float flySpeed = 80f;
        private float detectionRange = 150f;
        private bool swooping = false;
        private Vector2 swoopDirection;
        
        public FlyingEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            homePosition = Position;
            detectionRange = data.Float("detectionRange", 150f);
            flySpeed = data.Float("speed", 80f);
            
            Collider = new Circle(8f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/enemies/flying/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("swoop", "swoop", 0.08f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        public override void Update()
        {
            base.Update();
            
            // Use cached player from base class
            if (!swooping && cachedPlayer != null)
            {
                float distance = Vector2.Distance(Position, cachedPlayer.Center);
                
                if (distance < detectionRange)
                {
                    swooping = true;
                    swoopDirection = (cachedPlayer.Center - Position).SafeNormalize();
                    sprite.Play("swoop");
                }
                else
                {
                    // Return to home position
                    Vector2 toHome = (homePosition - Position).SafeNormalize();
                    MoveH(toHome.X * flySpeed * 0.5f * Engine.DeltaTime);
                    MoveV(toHome.Y * flySpeed * 0.5f * Engine.DeltaTime);
                }
            }
            else if (swooping)
            {
                MoveH(swoopDirection.X * flySpeed * Engine.DeltaTime);
                MoveV(swoopDirection.Y * flySpeed * Engine.DeltaTime);
                
                // Stop swooping if far from home
                if (Vector2.Distance(Position, homePosition) > detectionRange * 2)
                {
                    swooping = false;
                    sprite.Play("idle");
                }
            }
            
            // Idle floating animation
            if (!swooping)
            {
                Position.Y = homePosition.Y + (float)Math.Sin(Scene.TimeActive * 2f) * 4f;
            }
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }

    /// <summary>
    /// Turret enemy that shoots projectiles
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/TurretEnemy")]
    [Tracked]
    public class TurretEnemy : Enemy
    {
        private float shootInterval;
        private float shootTimer;
        private float detectionRange;
        private Color projectileColor;
        
        public TurretEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 3))
        {
            shootInterval = data.Float("shootInterval", 2f);
            detectionRange = data.Float("detectionRange", 200f);
            shootTimer = shootInterval;
            
            string colorHex = data.Attr("projectileColor", "FF0000");
            projectileColor = Calc.HexToColor(colorHex);
            
            Collider = new Hitbox(16f, 16f, -8f, -8f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/enemies/turret/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("shoot", "shoot", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        public override void Update()
        {
            base.Update();
            
            // Use cached player from base class
            if (cachedPlayer != null)
            {
                float distance = Vector2.Distance(Position, cachedPlayer.Center);
                
                if (distance < detectionRange)
                {
                    shootTimer -= Engine.DeltaTime;
                    
                    if (shootTimer <= 0)
                    {
                        Shoot(cachedPlayer);
                        shootTimer = shootInterval;
                    }
                }
            }
        }

        private void Shoot(Player player)
        {
            sprite.Play("shoot");
            
            Vector2 direction = (player.Center - Position).SafeNormalize();
            EnergyBall projectile = new EnergyBall(Position, direction, 150f, projectileColor);
            Scene.Add(projectile);
            
            Audio.Play("event:/game/general/thing_booped");
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
            
            // Draw health bar
            if (Health < MaxHealth)
            {
                float healthPercent = (float)Health / MaxHealth;
                Vector2 barPos = Position + new Vector2(-8f, -20f);
                Draw.Rect(barPos, 16f, 2f, Color.Black);
                Draw.Rect(barPos, 16f * healthPercent, 2f, Color.Red);
            }
        }
    }

    /// <summary>
    /// Jumping enemy that leaps toward the player
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/JumpingEnemy")]
    [Tracked]
    public class JumpingEnemy : Enemy
    {
        private float jumpForce = 200f;
        private float jumpInterval = 1.5f;
        private float jumpTimer;
        private Vector2 velocity;
        private bool onGround = true;
        
        public JumpingEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            jumpInterval = data.Float("jumpInterval", 1.5f);
            jumpTimer = jumpInterval;
            
            Collider = new Hitbox(12f, 12f, -6f, -12f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/enemies/jumping/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("jump", "jump", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        public override void Update()
        {
            base.Update();
            
            // Gravity
            velocity.Y += 400f * Engine.DeltaTime;
            
            MoveV(velocity.Y * Engine.DeltaTime);
            MoveH(velocity.X * Engine.DeltaTime);
            
            // Check if on ground
            onGround = OnGround();
            
            if (onGround)
            {
                velocity.Y = 0;
                velocity.X *= 0.9f;
                sprite.Play("idle");
                
                jumpTimer -= Engine.DeltaTime;
                
                if (jumpTimer <= 0)
                {
                    // Use cached player from base class
                    if (cachedPlayer != null)
                    {
                        Jump(cachedPlayer);
                    }
                    jumpTimer = jumpInterval;
                }
            }
            else
            {
                sprite.Play("jump");
            }
        }

        private void Jump(Player player)
        {
            Vector2 direction = (player.Center - Position).SafeNormalize();
            velocity = new Vector2(direction.X * 80f, -jumpForce);
            Audio.Play("event:/game/general/thing_booped");
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }
}
