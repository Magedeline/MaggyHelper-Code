using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using MaggyHelper.Entities;
using MaggyHelper.Entities.Kirby;

namespace MaggyHelper.Extensions
{
    /// <summary>
    /// Kirby mode extension for the player.
    /// Handles Kirby-specific visuals, inhale/spit, hover stamina, power copy, and health.
    /// Syncs to the main Celeste Player to preserve standard mechanics.
    /// </summary>
    [Tracked]
    public class KirbyMode : Actor
    {
        public enum KirbyPowerState
        {
            None,
            Fire,
            Ice,
            Spark,
            Stone,
            Sword,
            Archer,
            Leaf,
            Water,
            Esp,
            Hammer,
            Ranger,
            Mike,
            Crash,
            Bomb,
            Cutter,
            Painter,
            Cook,
            Bell,
            Light,
            Drill,
            Beam,
            Wheel,
            Phase,
            TripleSwap,
            TimeCrash,
            Umbrella,
            Mirror,
            Recycler,
            Mini,
            InfernoLight,
            GrandHammer,
            MechanizeRanger,
            FrostMind,
            UltraSword,
            Knight
        }

        private const string SFX_INHALE = "event:/desolozantas/char/kirby/inhale_start";
        private const string SFX_INHALE_END = "event:/desolozantas/char/kirby/spit";
        private const string SFX_SPIT = "event:/desolozantas/char/kirby/spit";
        private const string SFX_HURT = "event:/desolozantas/char/kirby/predeath";
        private const string SFX_HEAL = "event:/desolozantas/char/kirby/revive";
        private const string SFX_DEATH = "event:/desolozantas/char/kirby/predeath";

        private readonly KirbySettings settings = new KirbySettings();
        private global::Celeste.Player player;
        private Sprite sprite;
        private KirbyHealthDisplay hud;

        private bool syncToPlayer;
        private bool useKirbyExtSprite;

        private bool isInhaling;
        private bool hasMouthful;
        private float inhaleTimer;
        private float mouthOpenTimer;
        private float spitCooldown;
        private float invulnTimer;
        private float hoverStamina;
        private int floatJumpsUsed;

        private bool wasOnGround;
        private bool wasDead;

        private const int StateIntroRespawn = 14;

        public List<KirbyActorBase> InhaledEntities { get; } = new();
        public bool IsInhaling => this.isInhaling;
        public Facings Facing => this.player?.Facing ?? Facings.Right;
        public bool IsDashing => this.player?.DashAttacking ?? false;

        private Vector2 cachedSpeed;
        public Vector2 Speed
        {
            get => this.player?.Speed ?? this.cachedSpeed;
            set
            {
                if (this.player != null)
                    this.player.Speed = value;
                else
                    this.cachedSpeed = value;
            }
        }

        private KirbyPowerState pendingCopyPower = KirbyPowerState.None;

        public int MaxHealth => this.settings.MaxHealth;
        public float MaxStamina => this.settings.MaxStamina;
        public int CurrentHealth { get; private set; }
        public float CurrentStamina { get; private set; }
        public bool IsDead { get; private set; }
        public KirbyPowerState CurrentPower { get; private set; } = KirbyPowerState.None;

        public KirbyMode(Vector2 position) : base(position)
        {
            this.Tag = Tags.Persistent | Tags.TransitionUpdate;
            this.Depth = Depths.Player;
            this.Collider = new Hitbox(16f, 20f, -8f, -20f);
            this.hoverStamina = this.settings.MaxStamina;
            this.CurrentStamina = this.hoverStamina;
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            this.player = scene.Tracker.GetEntity<global::Celeste.Player>();
            this.LoadFromSession();
            this.CreateSprite();

            if (this.settings.ShowPowerHud)
            {
                this.hud = new KirbyHealthDisplay(this);
                scene.Add(this.hud);
            }
        }

        public override void Update()
        {
            base.Update();

            if (this.Scene is not Level)
                return;

            if (this.player == null || this.player.Scene != this.Scene)
                this.player = this.Scene.Tracker.GetEntity<global::Celeste.Player>();

            if (this.player == null)
                return;

            if (this.syncToPlayer)
            {
                this.Position = this.player.Position;
                this.Depth = this.player.Depth - 1;
            }

            this.UpdateTimers();
            this.UpdateInhale();
            this.UpdateHover();
            this.UpdateCopyPowerTimer();
            this.UpdateAnimation();
            this.UpdateRespawnState();

            this.wasOnGround = this.player.OnGround();
        }

        public override void Render()
        {
            if (!this.Visible || this.sprite == null)
                return;

            this.sprite.Render();
        }

        #region Public API

        public void EnablePlayerSync()
        {
            this.syncToPlayer = true;
            if (this.player != null)
            {
                this.player.Visible = false;
                if (this.player.Sprite != null)
                    this.player.Sprite.Visible = false;
                if (this.player.Hair != null)
                    this.player.Hair.Visible = false;
            }
        }

        public void DisablePlayerSync()
        {
            this.syncToPlayer = false;
            if (this.player != null)
            {
                this.player.Visible = true;
                if (this.player.Sprite != null)
                    this.player.Sprite.Visible = true;
                if (this.player.Hair != null)
                    this.player.Hair.Visible = true;
            }
        }

        public void SaveToSession()
        {
            var session = MaggyHelperModule.Session;
            if (session == null)
                return;

            session.KirbyHealth = this.CurrentHealth;
            session.KirbyStamina = this.CurrentStamina;
            session.CurrentKirbyPower = this.CurrentPower.ToString();
            session.IsKirbyModeActive = true;
        }

        public void SetPowerState(KirbyPowerState power)
        {
            this.CurrentPower = power;
            var session = MaggyHelperModule.Session;
            if (session != null)
            {
                session.CurrentKirbyPower = power.ToString();
                if (power != KirbyPowerState.None)
                    session.PowersCopied += 1;
            }
        }

        public void Heal(int amount = 1)
        {
            if (amount <= 0 || this.IsDead)
                return;

            int previous = this.CurrentHealth;
            this.CurrentHealth = Math.Min(this.CurrentHealth + amount, this.MaxHealth);
            if (this.CurrentHealth > previous)
                Audio.Play(SFX_HEAL, this.Position);
        }

        public void TakeDamage(int amount = 1, Vector2? source = null)
        {
            if (amount <= 0 || this.IsDead || (double)this.invulnTimer > 0.0)
                return;

            this.CurrentHealth -= amount;
            this.invulnTimer = this.settings.DamageInvulnTime;
            Audio.Play(SFX_HURT, this.Position);

            if (this.CurrentHealth <= 0)
            {
                this.CurrentHealth = 0;
                this.TriggerDeath(source ?? Vector2.Zero);
            }
        }

        #endregion

        #region Initialization

        private void CreateSprite()
        {
            try
            {
                this.sprite = GFX.SpriteBank.Create("kirby_player_ext");
                this.useKirbyExtSprite = true;
            }
            catch
            {
                this.sprite = GFX.SpriteBank.Create("kirby_player");
                this.useKirbyExtSprite = false;
            }

            this.Add(this.sprite);
            this.sprite.Position = Vector2.Zero;
            this.sprite.Scale = Vector2.One;
            this.sprite.Play(this.ResolveAnim("idle"));
        }

        private void LoadFromSession()
        {
            var session = MaggyHelperModule.Session;
            if (session == null)
            {
                this.CurrentHealth = this.MaxHealth;
                this.CurrentStamina = this.MaxStamina;
                return;
            }

            this.CurrentHealth = Math.Min(session.KirbyHealth, this.MaxHealth);
            this.CurrentStamina = Math.Min(session.KirbyStamina, this.MaxStamina);
            this.hoverStamina = this.CurrentStamina;

            if (Enum.TryParse(session.CurrentKirbyPower, out KirbyPowerState power))
                this.CurrentPower = power;
            if (this.CurrentHealth <= 0)
                this.CurrentHealth = this.MaxHealth;
        }

        #endregion

        #region Update Logic

        private void UpdateTimers()
        {
            if ((double)this.inhaleTimer > 0.0)
                this.inhaleTimer -= Engine.DeltaTime;
            if ((double)this.mouthOpenTimer > 0.0)
                this.mouthOpenTimer -= Engine.DeltaTime;
            if ((double)this.spitCooldown > 0.0)
                this.spitCooldown -= Engine.DeltaTime;
            if ((double)this.invulnTimer > 0.0)
                this.invulnTimer -= Engine.DeltaTime;
        }

        private void UpdateInhale()
        {
            if (!this.settings.KirbyPlayerEnabled || this.player == null)
                return;

            bool inhaleHeld = this.settings.IsKeyCheck("Inhale");
            bool inhalePressed = this.settings.IsKeyPressed("Inhale");
            bool inhaleActive = this.settings.InhaleHoldMode ? inhaleHeld : inhalePressed;

            if (inhaleActive && !this.isInhaling && (double)this.mouthOpenTimer <= 0.0)
                this.StartInhale();

            if (!inhaleHeld && this.isInhaling && this.settings.InhaleHoldMode)
                this.StopInhale();

            if (this.isInhaling)
            {
                if (this.settings.InhaleHoldMode)
                {
                    this.inhaleTimer = this.settings.InhaleDuration;
                }
                else
                {
                    this.inhaleTimer -= Engine.DeltaTime;
                    if ((double)this.inhaleTimer <= 0.0)
                        this.StopInhale();
                }
                this.EmitInhaleParticles();
                this.PullInhaleTargets();
            }
            else if ((double)this.inhaleTimer > 0.0)
            {
                this.inhaleTimer -= Engine.DeltaTime;
                if ((double)this.inhaleTimer <= 0.0)
                    this.StopInhale();
            }

            bool spitPressed = this.settings.IsKeyPressed("Spit");
            if (spitPressed)
            {
                if (this.hasMouthful)
                    this.SpitMouthful();
                else if (this.CurrentPower != KirbyPowerState.None && this.settings.PowerDropEnabled)
                    this.DropPower();
            }

            bool dropPressed = this.settings.IsKeyPressed("DropPower");
            if (dropPressed && this.CurrentPower != KirbyPowerState.None)
                this.DropPower();
        }

        private void UpdateHover()
        {
            if (this.player == null || this.IsDead)
                return;

            bool hoverHeld = this.settings.IsKeyCheck("Hover");
            bool hoverPressed = this.settings.IsKeyPressed("Hover");

            if (this.player.OnGround())
            {
                this.floatJumpsUsed = 0;
                this.hoverStamina = this.MaxStamina;
            }

            if (hoverHeld && (double)this.hoverStamina > 0.0 && !this.player.OnGround())
            {
                this.hoverStamina = Math.Max(0.0f, this.hoverStamina - this.settings.HoverStaminaDrain * Engine.DeltaTime);
                this.CurrentStamina = this.hoverStamina;

                if (hoverPressed && this.floatJumpsUsed < this.settings.MaxFloatJumps)
                {
                    this.player.Speed = new Vector2(this.player.Speed.X, this.settings.HoverFlapSpeed);
                    ++this.floatJumpsUsed;
                }

                if ((double)this.player.Speed.Y > (double)this.settings.HoverFallSpeed)
                    this.player.Speed = new Vector2(this.player.Speed.X, Calc.Approach(this.player.Speed.Y, this.settings.HoverFallSpeed, this.settings.HoverGravity * Engine.DeltaTime));
            }
            else
            {
                this.CurrentStamina = this.hoverStamina;
            }
        }

        private void UpdateCopyPowerTimer()
        {
            if (this.CurrentPower == KirbyPowerState.None)
                return;
            if (this.settings.PowerDurationSeconds <= 0)
                return;

            var session = MaggyHelperModule.Session;
            if (session == null)
                return;

            if ((double)session.PowerTimeRemaining <= 0.0)
                session.PowerTimeRemaining = this.settings.PowerDurationSeconds;

            session.PowerTimeRemaining -= Engine.DeltaTime;
            if ((double)session.PowerTimeRemaining <= 0.0)
            {
                this.SetPowerState(KirbyPowerState.None);
                session.PowerTimeRemaining = 0.0f;
            }
        }

        private void UpdateRespawnState()
        {
            if (this.player == null)
                return;

            bool isDeadState = this.player.StateMachine.State == StateIntroRespawn;
            if (isDeadState && !this.wasDead)
            {
                this.CurrentHealth = this.MaxHealth;
                this.CurrentStamina = this.MaxStamina;
                this.IsDead = false;
            }
            this.wasDead = isDeadState;
        }

        #endregion

        #region Inhale/Spit

        private void StartInhale()
        {
            this.isInhaling = true;
            this.inhaleTimer = this.settings.InhaleDuration;
            Audio.Play(SFX_INHALE, this.Position);
        }

        private void StopInhale()
        {
            if (!this.isInhaling)
                return;
            this.isInhaling = false;
            this.mouthOpenTimer = this.settings.MouthOpenTime;
            this.inhaleTimer = 0.0f;
            Audio.Play(SFX_INHALE_END, this.Position);
        }

        private void PullInhaleTargets()
        {
            if (this.Scene is not Level level)
                return;

            Vector2 facingDir = this.player.Facing == Facings.Left ? -Vector2.UnitX : Vector2.UnitX;
            Vector2 mouthPos = this.Position + new Vector2(facingDir.X * this.settings.InhaleMouthOffset, -6f);
            float range = this.settings.InhaleRange;
            float rangeSq = range * range;

            foreach (var entity in level.Tracker.GetEntities<Actor>())
            {
                if (entity == this || entity == this.player)
                    continue;
                if (!this.IsInhalable(entity))
                    continue;

                Vector2 toEntity = entity.Position - mouthPos;
                if ((double)toEntity.LengthSquared() > (double)rangeSq)
                    continue;

                float dirDot = Vector2.Dot(toEntity.SafeNormalize(), facingDir);
                if ((double)dirDot < (double)this.settings.InhaleConeDot)
                    continue;

                Vector2 pull = facingDir * this.settings.InhalePullSpeed * Engine.DeltaTime;
                entity.Position = Vector2.Lerp(entity.Position, mouthPos, this.settings.InhalePullLerp * Engine.DeltaTime);
                entity.Position -= pull;

                if ((double)Vector2.Distance(entity.Position, mouthPos) <= (double)this.settings.InhaleSwallowDistance)
                {
                    this.SwallowEntity(entity);
                    break;
                }
            }
        }

        private bool IsInhalable(Entity entity)
        {
            if (entity == null || !entity.Active)
                return false;
            if (entity is KirbyMode)
                return false;

            if (entity.Get<Holdable>() != null)
                return true;

            string typeName = entity.GetType().Name;
            if (typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase))
                return true;
            if (typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase))
                return this.settings.AllowInhaleBosses;
            if (typeName.Contains("Seeker", StringComparison.OrdinalIgnoreCase))
                return true;
            if (typeName.Contains("Oshiro", StringComparison.OrdinalIgnoreCase))
                return true;
            if (typeName.Contains("Theo", StringComparison.OrdinalIgnoreCase))
                return this.settings.AllowInhaleCarryables;
            if (typeName.Contains("Crystal", StringComparison.OrdinalIgnoreCase))
                return this.settings.AllowInhaleCarryables;

            return entity is KirbyFood;
        }

        private void SwallowEntity(Entity entity)
        {
            if (entity == null)
                return;

            if (entity is KirbyActorBase actorBase && !this.InhaledEntities.Contains(actorBase))
                this.InhaledEntities.Add(actorBase);

            if (entity is KirbyFood)
            {
                this.Heal(1);
            }
            else
            {
                this.pendingCopyPower = KirbyPowerState.None;
                this.pendingCopyPower = this.TryCopyPower(entity);
            }

            if (this.settings.PowerCopyEnabled && this.pendingCopyPower != KirbyPowerState.None)
                this.SetPowerState(this.pendingCopyPower);
            else
                this.hasMouthful = true;

            entity.RemoveSelf();
        }

        private void SpitMouthful()
        {
            if ((double)this.spitCooldown > 0.0 || this.player == null)
                return;

            this.spitCooldown = this.settings.SpitCooldown;
            this.hasMouthful = false;
            Audio.Play(SFX_SPIT, this.Position);

            Vector2 direction = this.player.Facing == Facings.Left ? -Vector2.UnitX : Vector2.UnitX;
            if (this.Scene is Level level)
                level.Add(new KirbySpitProjectile(this.Position + direction * 8f, direction, this.settings.SpitSpeed));
        }

        private void DropPower() => this.SetPowerState(KirbyPowerState.None);

        private KirbyPowerState TryCopyPower(Entity entity)
        {
            string name = entity.GetType().Name;
            if (name.Contains("Fire", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Fire;
            if (name.Contains("Ice", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Ice;
            if (name.Contains("Spark", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Spark;
            if (name.Contains("Stone", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Stone;
            if (name.Contains("Sword", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Sword;
            if (name.Contains("Beam", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Beam;
            if (name.Contains("Water", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Water;
            if (name.Contains("Hammer", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Hammer;
            if (name.Contains("Bomb", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Bomb;
            if (name.Contains("Cutter", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Cutter;
            if (name.Contains("Knight", StringComparison.OrdinalIgnoreCase))
                return KirbyPowerState.Knight;
            return KirbyPowerState.None;
        }

        private void EmitInhaleParticles()
        {
            if (this.Scene is not Level level)
                return;

            Vector2 facingDir = this.player.Facing == Facings.Left ? -Vector2.UnitX : Vector2.UnitX;
            Vector2 mouthPos = this.Position + new Vector2(facingDir.X * this.settings.InhaleMouthOffset, -6f);
            level.ParticlesFG?.Emit(ParticleTypes.Dust, 1, mouthPos, Vector2.One * 4f);
        }

        #endregion

        #region Animation

        private void UpdateAnimation()
        {
            if (this.sprite == null || this.player == null)
                return;

            this.sprite.FlipX = this.player.Facing == Facings.Left;
            this.sprite.FlipY = false;

            if (this.IsDead)
            {
                this.sprite.Play(this.ResolveAnim("death"));
                return;
            }

            if (this.isInhaling)
            {
                this.sprite.Play(this.ResolveAnim("inhale"));
                return;
            }

            if (this.player.DashAttacking || this.player.StateMachine.State == 2)
            {
                this.sprite.Play(this.ResolveAnim("dash"));
                return;
            }

            if (!this.player.OnGround())
            {
                if (this.settings.IsKeyCheck("Hover") && (double)this.hoverStamina > 0.0)
                    this.sprite.Play(this.ResolveAnim("hover"));
                else if ((double)this.player.Speed.Y > 0.0)
                    this.sprite.Play(this.ResolveAnim("fall"));
                else
                    this.sprite.Play(this.ResolveAnim("jump"));
                return;
            }

            float speedX = Math.Abs(this.player.Speed.X);
            if ((double)speedX <= 1.0)
                this.sprite.Play(this.ResolveAnim("idle"));
            else if ((double)speedX < 90.0)
                this.sprite.Play(this.ResolveAnim("walk"));
            else
                this.sprite.Play(this.ResolveAnim("run"));
        }

        private string ResolveAnim(string baseId)
        {
            if (!this.useKirbyExtSprite)
            {
                switch (baseId)
                {
                    case "run":
                        return "runFast";
                    default:
                        return baseId;
                }
            }

            switch (baseId)
            {
                case "idle":
                    return "kirby_idle";
                case "walk":
                    return "kirby_walk";
                case "run":
                    return "kirby_run";
                case "jump":
                    return "kirby_jump";
                case "fall":
                    return "kirby_fall";
                case "dash":
                    return "kirby_run";
                case "inhale":
                    return "kirby_inhale";
                case "hover":
                    return "kirby_hover";
                case "slide":
                    return "kirby_slide";
                case "death":
                    return "kirby_death";
                default:
                    return "kirby_idle";
            }
        }

        #endregion

        #region Damage/Death

        private void TriggerDeath(Vector2 source)
        {
            if (this.IsDead)
                return;

            this.IsDead = true;
            Audio.Play(SFX_DEATH, this.Position);

            if (this.Scene is Level level)
            {
                level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 20, this.Position, Vector2.One * 16f, Color.Pink);
                level.Displacement.AddBurst(this.Position, 0.4f, 16f, 48f, 0.4f);
            }

            if (this.player != null)
                this.player.Die((this.player.Position - source).SafeNormalize());
        }

        #endregion
    }

    /// <summary>
    /// Basic spit projectile for Kirby.
    /// </summary>
    public class KirbySpitProjectile : Actor
    {
        private Vector2 velocity;
        private float lifeTimer = 1.2f;

        public KirbySpitProjectile(Vector2 position, Vector2 direction, float speed) : base(position)
        {
            this.velocity = direction.SafeNormalize() * speed;
            this.Collider = new Hitbox(6f, 6f, -3f, -3f);
            this.Depth = Depths.Player - 5;
        }

        public override void Update()
        {
            base.Update();

            this.lifeTimer -= Engine.DeltaTime;
            if ((double)this.lifeTimer <= 0.0)
            {
                this.RemoveSelf();
                return;
            }

            this.MoveH(this.velocity.X * Engine.DeltaTime, this.OnCollide);
            this.MoveV(this.velocity.Y * Engine.DeltaTime, this.OnCollide);
        }

        private void OnCollide(CollisionData data) => this.RemoveSelf();
    }
}


