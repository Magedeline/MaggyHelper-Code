using MaggyHelper.Entities;
using MaggyHelper.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace MaggyHelper
{
    /// <summary>
    /// Base class for projectile entities in the game.
    /// </summary>
    [Tracked]
    public abstract class Projectile : Actor
    {
        public Vector2 Velocity;
        public float Speed;
        public bool DestroyOnCollision = true;
        public bool DamagesPlayer = true;
        public int Damage = 1;
        
        protected Sprite sprite;
        protected Cooldown lifetimeCooldown;

        public Projectile(Vector2 position, Vector2 direction, float speed)
            : base(position)
        {
            Velocity = direction * speed;
            Speed = speed;
            lifetimeCooldown = new Cooldown(10f, startReady: false);
            Collider = new Hitbox(8f, 8f, -4f, -4f);
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = -1000;
        }

        public override void Update()
        {
            base.Update();
            
            if (lifetimeCooldown.Update(Engine.DeltaTime))
            {
                RemoveSelf();
                return;
            }

            MoveH(Velocity.X * Engine.DeltaTime);
            MoveV(Velocity.Y * Engine.DeltaTime);

            // Check collision with player
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && DamagesPlayer && CollideCheck(player))
            {
                OnHitPlayer(player);
                if (DestroyOnCollision)
                {
                    RemoveSelf();
                }
            }

            // Check collision with solid
            if (CollideCheck<Solid>())
            {
                OnHitWall();
                if (DestroyOnCollision)
                {
                    RemoveSelf();
                }
            }
        }

        protected virtual void OnHitPlayer(Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        protected virtual void OnHitWall()
        {
            // Override in derived classes
        }

        public override void Render()
        {
            base.Render();
            sprite?.Render();
        }
    }

    /// <summary>
    /// Simple energy ball projectile
    /// </summary>
    public class EnergyBall : Projectile
    {
        private Color color;
        private ParticleType particleType;

        public EnergyBall(Vector2 position, Vector2 direction, float speed, Color color)
            : base(position, direction, speed)
        {
            this.color = color;
            particleType = new ParticleType
            {
                Color = color,
                Size = 1f,
                SpeedMin = 10f,
                SpeedMax = 20f,
                LifeMin = 0.3f,
                LifeMax = 0.6f
            };
            
            Collider = new Circle(6f);
        }

        public override void Update()
        {
            base.Update();
            
            // Emit particles
            if (Scene.OnInterval(0.05f))
            {
                (Scene as Level)?.Particles.Emit(particleType, Position);
            }
        }

        public override void Render()
        {
            ShapeRenderer.DrawCircleOutline(Position, 6f, color, 1f, 8);
            ShapeRenderer.DrawCircleOutline(Position, 4f, Color.White * 0.8f, 1f, 6);
        }
    }

    /// <summary>
    /// Homing projectile that follows the player
    /// </summary>
    public class HomingProjectile : Projectile
    {
#pragma warning disable CS0414
        private float turnSpeed = 2f;
#pragma warning restore CS0414
        private float acceleration = 50f;

        public HomingProjectile(Vector2 position, Vector2 direction, float speed)
            : base(position, direction, speed)
        {
            Collider = new Circle(5f);
        }

        public override void Update()
        {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
            {
                Vector2 targetDirection = (player.Center - Position).SafeNormalize();
                Velocity = Calc.Approach(Velocity, targetDirection * Speed, acceleration * Engine.DeltaTime);
                
                if (Velocity != Vector2.Zero)
                {
                    Velocity = Velocity.SafeNormalize() * Speed;
                }
            }

            base.Update();
        }

        public override void Render()
        {
            ShapeRenderer.DrawCircleOutline(Position, 5f, Color.Purple, 1f, 8);
            ShapeRenderer.DrawCircleOutline(Position, 3f, Color.White * 0.6f, 1f, 6);

            // Draw trail using ShapeRenderer utility
            Vector2 direction = Velocity.SafeNormalize();
            ShapeRenderer.DrawTrail(Position, direction, Color.Purple,
                count: 3, spacing: 8f, startRadius: 4f, radiusShrink: 1f, alphaFade: 0.15f, segments: 6);
        }
    }

    /// <summary>
    /// Bouncing projectile that bounces off walls
    /// </summary>
    public class BouncingProjectile : Projectile
    {
        private int maxBounces = 3;
        private int bounceCount = 0;

        public BouncingProjectile(Vector2 position, Vector2 direction, float speed)
            : base(position, direction, speed)
        {
            DestroyOnCollision = false;
            Collider = new Hitbox(8f, 8f, -4f, -4f);
        }

        protected override void OnHitWall()
        {
            bounceCount++;
            
            if (bounceCount >= maxBounces)
            {
                RemoveSelf();
                return;
            }

            // Bounce logic
            if (CollideCheck<Solid>(Position + Vector2.UnitX))
            {
                Velocity.X = -Velocity.X;
            }
            if (CollideCheck<Solid>(Position + Vector2.UnitY))
            {
                Velocity.Y = -Velocity.Y;
            }
        }

        public override void Render()
        {
            ShapeRenderer.DrawCircleOutline(Position, 4f, Color.Orange, 1f, 6);
            ShapeRenderer.DrawCircleOutline(Position, 2f, Color.Yellow, 1f, 4);
        }
    }

    /// <summary>
    /// Laser beam projectile
    /// </summary>
    public class LaserBeam : Actor
    {
        private Vector2 startPos;
        private Vector2 direction;
        private float length;
        private float maxLength;
        private Cooldown chargeCooldown;
        private Cooldown fireCooldown;
        private float width = 4f;
        private Color laserColor;
        private bool fired = false;

        public LaserBeam(Vector2 position, Vector2 direction, float maxLength, float chargeTime, float fireTime, Color color)
            : base(position)
        {
            this.startPos = position;
            this.direction = direction.SafeNormalize();
            this.maxLength = maxLength;
            chargeCooldown = new Cooldown(chargeTime, startReady: false);
            fireCooldown = new Cooldown(fireTime, startReady: false);
            this.laserColor = color;
            
            Depth = -10000;
        }

        public override void Update()
        {
            base.Update();
            
            if (!fired)
            {
                if (chargeCooldown.Update(Engine.DeltaTime))
                {
                    fired = true;
                    FireLaser();
                }
            }
            else if (fireCooldown.Update(Engine.DeltaTime))
            {
                RemoveSelf();
            }
        }

        private void FireLaser()
        {
            // Raycast to find actual length
            length = maxLength;
            Vector2 endPos = startPos + direction * maxLength;
            
            // Check collision with solids
            for (float i = 0; i < maxLength; i += 4f)
            {
                Vector2 checkPos = startPos + direction * i;
                if (Scene.CollideCheck<Solid>(checkPos))
                {
                    length = i;
                    break;
                }
            }

            // Check collision with player
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
            {
                Vector2 closest = Calc.ClosestPointOnLine(startPos, startPos + direction * length, player.Center);
                if (Vector2.Distance(closest, player.Center) < width + 8f)
                {
                    player.Die((player.Center - closest).SafeNormalize());
                }
            }
        }

        public override void Render()
        {
            if (!fired)
            {
                // Charging animation — progress goes 0→1 as charge completes
                float alpha = chargeCooldown.Progress;
                Draw.Line(startPos, startPos + direction * 20f, laserColor * alpha, 2f);
            }
            else
            {
                // Draw laser
                Vector2 endPos = startPos + direction * length;
                Draw.Line(startPos, endPos, laserColor, width);
                Draw.Line(startPos, endPos, Color.White * 0.8f, width * 0.5f);
            }
        }
    }
}
