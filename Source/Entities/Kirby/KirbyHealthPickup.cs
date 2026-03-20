using MaggyHelper.Entities;
using MaggyHelper.Extensions;
using MaggyHelper.Extensions.Kirby;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace MaggyHelper.Entities.Kirby
{
    /// <summary>
    /// Health pickup for Kirby. Heals Kirby when collected.
    /// Appears as a tomato or other healing item.
    /// Can be placed in maps or dropped by enemies.
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KirbyHealthPickup")]
    [Tracked]
    public class KirbyHealthPickup : Actor
    {
        #region Constants

        private const float FLOAT_AMPLITUDE = 4f;
        private const float FLOAT_SPEED = 2f;
        private const float COLLECT_RADIUS = 16f;
        private const float BOUNCE_HEIGHT = -80f;
        private const float GRAVITY = 300f;
        private const string SFX_COLLECT = "event:/game/general/diamond_touch";

        #endregion

        #region Fields

        private Sprite sprite;
        private VertexLight light;
        private BloomPoint bloom;
        private Wiggler collectWiggle;
        
        private int healAmount;
        private Level level;
        private Vector2 startPosition;
        private float floatTimer;
        private float lifetime;
        private bool isCollected;
        private Vector2 velocity;
        private bool bouncing;

        #endregion

        #region Properties

        public int HealAmount => healAmount;

        #endregion

        #region Constructor

        public KirbyHealthPickup(Vector2 position, int healAmount = 2) : base(position)
        {
            this.healAmount = healAmount;
            this.startPosition = position;
            
            Collider = new Hitbox(12f, 12f, -6f, -6f);
            
            Add(collectWiggle = Wiggler.Create(0.5f, 4f));
            Add(light = new VertexLight(Color.LimeGreen, 0.8f, 16, 32));
            Add(bloom = new BloomPoint(0.5f, 12f));
            
            Depth = Depths.Pickups;
        }

        public KirbyHealthPickup(EntityData data, Vector2 offset) : this(data.Position + offset)
        {
            healAmount = data.Int("healAmount", 2);
            bouncing = data.Bool("bouncing", false);
            lifetime = data.Float("lifetime", 0f);
            
            if (bouncing)
            {
                velocity.Y = BOUNCE_HEIGHT;
            }
        }

        #endregion

        #region Lifecycle

        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = scene as Level;
            
            SetupSprite();
        }

        private void SetupSprite()
        {
            try
            {
                // Try to load custom sprite
                sprite = GFX.SpriteBank.Create("kirby_health_pickup");
                sprite.Play("idle");
            }
            catch
            {
                // Fallback sprite
                sprite = new Sprite(GFX.Game, "collectables/heartGem/0/");
                sprite.AddLoop("idle", "spin", 0.1f);
                sprite.Play("idle");
                sprite.Color = Color.LimeGreen;
            }
            
            Add(sprite);
        }

        public override void Update()
        {
            base.Update();

            if (isCollected)
                return;

            // Update lifetime
            if (lifetime > 0f)
            {
                lifetime -= Engine.DeltaTime;
                if (lifetime <= 0f)
                {
                    RemoveSelf();
                    return;
                }
                
                // Flash when about to expire
                if (lifetime < 2f)
                {
                    Visible = (int)(lifetime * 10) % 2 == 0;
                }
            }

            // Floating animation
            if (!bouncing)
            {
                floatTimer += Engine.DeltaTime * FLOAT_SPEED;
                Position.Y = startPosition.Y + (float)Math.Sin(floatTimer) * FLOAT_AMPLITUDE;
            }
            else
            {
                // Bouncing physics
                velocity.Y += GRAVITY * Engine.DeltaTime;
                MoveV(velocity.Y * Engine.DeltaTime, OnCollideV);
                
                if (OnGround())
                {
                    velocity.Y = BOUNCE_HEIGHT * 0.6f;
                    velocity.X *= 0.8f;
                    
                    if (Math.Abs(velocity.Y) < 20f)
                    {
                        bouncing = false;
                        startPosition = Position;
                    }
                }
            }

            // Check for Kirby collection
            CheckCollection();
        }

        private void CheckCollection()
        {
            var player = Scene.Tracker.GetEntity<global::Celeste.Player>();
            if (player == null || !player.IsKirbyMode())
            {
                return;
            }

            float distance = Vector2.Distance(Position, player.Position);
            if (distance < COLLECT_RADIUS)
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (isCollected)
                return;

            isCollected = true;

            // Heal active Kirby runtime target.
            var kirbyExt = Scene.Tracker.GetEntity<KirbyPlayerExtension>();
            var kirbyLegacy = Scene.Tracker.GetEntity<KirbyMode>();
            var kirbyShim = Scene.Tracker.GetEntity<KirbyPlayer>();
            if (kirbyExt != null && !kirbyExt.IsDead)
            {
                kirbyExt.Heal(healAmount);
            }
            else if (kirbyLegacy != null && !kirbyLegacy.IsDead)
            {
                kirbyLegacy.Heal(healAmount);
            }
            else if (kirbyShim != null && !kirbyShim.IsDead)
            {
                kirbyShim.Heal(healAmount);
            }
            
            // Play collection effects
            Audio.Play(SFX_COLLECT, Position);
            collectWiggle.Start();
            
            // Create collection particles
            if (level != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = Calc.Random.NextFloat() * MathHelper.TwoPi;
                    Vector2 direction = Calc.AngleToVector(angle, 1f);
                    level.Particles.Emit(ParticleTypes.Dust, Position + direction * 4f, Color.LimeGreen);
                }
            }
            
            // Remove self with a small delay for visual effect
            Alarm.Set(this, 0.1f, () => RemoveSelf());
        }

        private void OnCollideV(CollisionData data)
        {
            if (velocity.Y > 0f)
            {
                velocity.Y = -velocity.Y * 0.5f;
            }
        }

        public override void Render()
        {
            if (!isCollected)
            {
                // Pulsing scale effect
                float scale = 1f + collectWiggle.Value * 0.3f;
                sprite.Scale = Vector2.One * scale;
            }
            
            base.Render();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Make the pickup bounce
        /// </summary>
        public void StartBouncing(Vector2 initialVelocity)
        {
            bouncing = true;
            velocity = initialVelocity;
        }

        /// <summary>
        /// Set a lifetime for the pickup (auto-despawn after time)
        /// </summary>
        public void SetLifetime(float seconds)
        {
            lifetime = seconds;
        }

        #endregion
    }
}
