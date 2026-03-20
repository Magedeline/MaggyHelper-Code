using Microsoft.Xna.Framework;
using Monocle;
using MaggyHelper.Entities;
using System;

namespace MaggyHelper.Extensions.Kirby
{
    /// <summary>
    /// Kirby's spit ability — eject a swallowed entity/star projectile.
    /// 
    /// Behaviour:
    ///   • Available when Kirby has a mouthful (from inhale)
    ///   • Press Spit to fire a star projectile in faced direction
    ///   • Projectile damages enemies and bounces off walls
    ///   • Cooldown between spits
    ///   • If no mouthful, pressing spit with a power can drop it (optional)
    /// </summary>
    public class KirbySpitAbility : KirbyAbilityBase, IKirbyAnimationProvider
    {
        public override string AbilityId => "spit";
        public override string DisplayName => "Spit";
        public override float MaxCooldown => Settings?.SpitCooldown ?? 0.2f;

        private const string SFX_SPIT = "event:/desolozantas/char/kirby/spit";

        private float _animTimer;

        #region Lifecycle

        protected override void OnUpdate()
        {
            if (Player == null) return;

            // Tick anim timer
            if (_animTimer > 0f)
                _animTimer -= Engine.DeltaTime;
            else if (IsExecuting)
                FinishExecution();

            HandleInput();
        }

        #endregion

        #region Input

        private void HandleInput()
        {
            bool spitPressed = Settings.IsKeyPressed("Spit");

            if (spitPressed)
            {
                var inhale = Extension.Inhale;
                if (inhale != null && inhale.HasMouthful)
                {
                    TryActivate();
                }
                else if (Extension.CurrentPower != KirbyMode.KirbyPowerState.None && Settings.PowerDropEnabled)
                {
                    DropPower();
                }
            }

            // Dedicated drop-power button
            bool dropPressed = Settings.IsKeyPressed("DropPower");
            if (dropPressed && Extension.CurrentPower != KirbyMode.KirbyPowerState.None)
            {
                DropPower();
            }
        }

        #endregion

        #region Activation

        protected override bool CanActivate()
        {
            return Extension.Inhale?.HasMouthful == true;
        }

        protected override void OnActivate()
        {
            // Fire projectile
            Vector2 direction = FacingDirection;
            float speed = Settings.SpitSpeed;
            Vector2 spawnPos = Player.Position + direction * 8f;

            Level?.Add(new KirbySpitStarProjectile(spawnPos, direction, speed));
            PlaySfx(SFX_SPIT);

            // Clear mouthful
            Extension.Inhale?.ClearMouthful();

            // Short animation hold
            _animTimer = 0.15f;
        }

        protected override void OnFinish()
        {
            _animTimer = 0f;
        }

        #endregion

        #region Power Drop

        private void DropPower()
        {
            Extension.SetPowerState(KirbyMode.KirbyPowerState.None);
        }

        #endregion

        #region Animation

        public string GetAnimationId()
        {
            if (IsExecuting) return "spit";
            return null;
        }

        #endregion
    }

    /// <summary>
    /// Star projectile fired when Kirby spits.
    /// Damages enemies on contact and despawns on wall hit or timeout.
    /// </summary>
    public class KirbySpitStarProjectile : Actor
    {
        private Vector2 _velocity;
        private float _lifeTimer = 1.5f;
        private Sprite _sprite;

        public int Damage { get; set; } = 1;

        public KirbySpitStarProjectile(Vector2 position, Vector2 direction, float speed) : base(position)
        {
            _velocity = direction.SafeNormalize() * speed;
            Collider = new Hitbox(8f, 8f, -4f, -4f);
            Depth = Depths.Player - 5;
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);

            // Try kirby star sprite, fallback to simple
            try
            {
                _sprite = new Sprite(GFX.Game, "projectiles/kirby/star/");
                _sprite.AddLoop(KirbyAnimIds.Idle, "", 0.08f);
                _sprite.CenterOrigin();
                _sprite.Play(KirbyAnimIds.Idle);
            }
            catch
            {
                _sprite = new Sprite(GFX.Game, "objects/refill/");
                _sprite.AddLoop(KirbyAnimIds.Idle, KirbyAnimIds.Idle, 0.1f);
                _sprite.CenterOrigin();
                _sprite.Play(KirbyAnimIds.Idle);
                _sprite.Color = Color.Yellow;
            }
            Add(_sprite);

            // Player collision for enemies
            Add(new PlayerCollider(OnPlayerHit));
        }

        public override void Update()
        {
            base.Update();

            _lifeTimer -= Engine.DeltaTime;
            if (_lifeTimer <= 0f)
            {
                RemoveSelf();
                return;
            }

            MoveH(_velocity.X * Engine.DeltaTime, OnCollideWall);
            MoveV(_velocity.Y * Engine.DeltaTime, OnCollideWall);

            // Spin the star
            if (_sprite != null)
                _sprite.Rotation += 12f * Engine.DeltaTime;

            // Check for enemy hits
            CheckEnemyCollisions();
        }

        private void OnCollideWall(CollisionData data)
        {
            // Puff and despawn
            if (Scene is Level level)
            {
                level.ParticlesFG?.Emit(ParticleTypes.Dust, 3, Position, Vector2.One * 4f);
            }
            RemoveSelf();
        }

        private void OnPlayerHit(Player player)
        {
            // Spit stars don't hurt Kirby
        }

        private void CheckEnemyCollisions()
        {
            if (Scene == null) return;

            foreach (Entity candidate in Scene.Entities)
            {
                if (candidate is not Actor entity) continue;

                if (entity == this) continue;
                if (entity is Player) continue;
                if (entity is KirbyPlayerExtension) continue;
                if (entity is KirbyMode) continue;
                if (entity is KirbySpitStarProjectile) continue;

                if (!CollideCheck(entity)) continue;

                // Damage the entity
                if (entity is Entities.Kirby.KirbyActorBase kirbyActor)
                {
                    kirbyActor.OnHit(Damage, (entity.Position - Position).SafeNormalize() * 100f);
                }

                // Puff
                if (Scene is Level level)
                {
                    level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 5, Position, Vector2.One * 4f, Color.Yellow);
                }
                RemoveSelf();
                return;
            }
        }

        public override void Render()
        {
            base.Render();

            // Fallback: draw a simple yellow circle if no sprite
            if (_sprite == null)
            {
                Draw.Circle(Position, 3f, Color.Yellow, 8);
            }
        }
    }
}
