using MaggyHelper.Entities;
using MaggyHelper.Extensions;

namespace MaggyHelper.Entities.Kirby
{
    /// <summary>
    /// Kirby Knight Mode - A special transformation available in Chapter 19's final run
    /// and Chapter 20 as the "last push" against Asriel.
    /// 
    /// Unlocked conditions:
    /// - Chapter 19: During the final run segment (flag: ch19_final_run)
    /// - Chapter 20: Throughout the chapter as the ultimate form
    /// - Emergency: When health drops critically low (configurable)
    /// </summary>
    [Tracked]
    public class KirbyKnightMode : Entity
    {
        #region Constants

        // Chapter IDs
        private const int CHAPTER_19 = 19;
        private const int CHAPTER_20 = 20;

        // Session flags
        private const string FLAG_KNIGHT_UNLOCKED = "kirby_knight_unlocked";
        private const string FLAG_KNIGHT_ACTIVE = "kirby_knight_active";
        private const string FLAG_CH19_FINAL_RUN = "ch19_final_run";
        private const string FLAG_CH20_LAST_PUSH = "ch20_last_push";
        private const string FLAG_KNIGHT_EMERGENCY = "kirby_knight_emergency";

        // SFX paths
        private const string SFX_TRANSFORM = "event:/desolozantas/char/kirby/knight_transform";
        private const string SFX_DETRANSFORM = "event:/desolozantas/char/kirby/knight_detransform";
        private const string SFX_ATTACK = "event:/desolozantas/char/kirby/knight_attack";
        private const string SFX_SPECIAL = "event:/desolozantas/char/kirby/knight_special";
        private const string SFX_CHARGE = "event:/desolozantas/char/kirby/knight_charge";
        private const string SFX_FINISHER = "event:/desolozantas/char/kirby/knight_finisher";
        private const string SFX_BLOCK = "event:/desolozantas/char/kirby/knight_block";

        // Combat values
        private const float BASE_DAMAGE_MULTIPLIER = 2.0f;
        private const float CHARGE_TIME_MAX = 2.0f;
        private const float FINISHER_CHARGE_TIME = 3.0f;
        private const float BLOCK_DAMAGE_REDUCTION = 0.5f;
        private const float ATTACK_COOLDOWN = 0.2f;
        private const float SPECIAL_COOLDOWN = 1.5f;
        private const float FINISHER_COOLDOWN = 10.0f;

        #endregion

        #region Enums

        public enum KnightState
        {
            Inactive,
            Transforming,
            Active,
            Attacking,
            Charging,
            SpecialAttack,
            Finisher,
            Blocking,
            Detransforming
        }

        public enum KnightAttackType
        {
            Slash,          // Basic sword slash
            Thrust,         // Forward thrust
            UpperSlash,     // Upward attack
            SpinSlash,      // 360 degree attack
            ChargeSlash,    // Charged heavy attack
            SwordBeam,      // Ranged projectile
            Finisher        // Ultimate attack
        }

        #endregion

        #region Properties

        public KnightState CurrentState { get; private set; } = KnightState.Inactive;
        public float ChargeLevel { get; private set; }
        public float DamageMultiplier => GetDamageMultiplier();
        public bool IsBlocking => CurrentState == KnightState.Blocking;
        public bool CanAttack => attackCooldown <= 0 && CurrentState == KnightState.Active;
        public bool CanSpecial => specialCooldown <= 0 && CurrentState == KnightState.Active;
        public bool CanFinisher => finisherCooldown <= 0 && CurrentState == KnightState.Active;

        #endregion

        #region Fields

        private Level level;
        private KirbyPlayer kirbyExtension;
        private global::Celeste.Player player;
        
        // Timers
        private float attackCooldown;
        private float specialCooldown;
        private float finisherCooldown;
        private float transformTimer;
        private float chargeTimer;
        private float stateTimer;
        
        // Visual components
        private SoundSource chargeSound;
        private BloomPoint bloom;
        private VertexLight light;
        
        // Particle types
        private static ParticleType P_KnightSlash;
        private static ParticleType P_KnightCharge;
        private static ParticleType P_KnightGlow;
        private static ParticleType P_KnightFinisher;

        // State tracking
        private int comboCount;
        private float comboTimer;
        private Vector2 lastAttackDirection;
        private bool isChapterEligible;
        private readonly TimeRateModifier timeRateModifier;

        #endregion

        #region Constructor

        public KirbyKnightMode() : base(Vector2.Zero)
        {
            Tag = Tags.Persistent | Tags.TransitionUpdate;
            Depth = -100;
            Add(timeRateModifier = new TimeRateModifier(1f, false));
            
            InitializeParticles();
        }

        #endregion

        #region Lifecycle

        public override void Added(Scene scene)
        {
            base.Added(scene);
            
            level = scene as Level;
            if (level == null) return;

            // Check chapter eligibility
            CheckChapterEligibility();
            
            // Find player/kirby
            kirbyExtension = level.Tracker.GetEntity<KirbyPlayer>();
            player = level.Tracker.GetEntity<global::Celeste.Player>();
            
            // Add visual components
            Add(bloom = new BloomPoint(0f, 16f));
            Add(light = new VertexLight(Color.Gold, 0f, 32, 64));
            Add(chargeSound = new SoundSource());
            
            // Check if knight mode should be auto-enabled
            if (level.Session.GetFlag(FLAG_KNIGHT_ACTIVE))
            {
                CurrentState = KnightState.Active;
                bloom.Alpha = 0.5f;
                light.Alpha = 0.8f;
            }
            
            IngesteLogger.Info($"KirbyKnightMode added - Chapter eligible: {isChapterEligible}");
        }

        public override void Update()
        {
            base.Update();
            
            if (level == null) return;
            
            // Update timers
            if (attackCooldown > 0) attackCooldown -= Engine.DeltaTime;
            if (specialCooldown > 0) specialCooldown -= Engine.DeltaTime;
            if (finisherCooldown > 0) finisherCooldown -= Engine.DeltaTime;
            if (comboTimer > 0) comboTimer -= Engine.DeltaTime;
            else comboCount = 0;
            
            // Update position to follow player/kirby
            UpdatePosition();
            
            // State machine
            switch (CurrentState)
            {
                case KnightState.Inactive:
                    UpdateInactive();
                    break;
                case KnightState.Transforming:
                    UpdateTransforming();
                    break;
                case KnightState.Active:
                    UpdateActive();
                    break;
                case KnightState.Attacking:
                    UpdateAttacking();
                    break;
                case KnightState.Charging:
                    UpdateCharging();
                    break;
                case KnightState.SpecialAttack:
                    UpdateSpecialAttack();
                    break;
                case KnightState.Finisher:
                    UpdateFinisher();
                    break;
                case KnightState.Blocking:
                    UpdateBlocking();
                    break;
                case KnightState.Detransforming:
                    UpdateDetransforming();
                    break;
            }
        }

        private void UpdatePosition()
        {
            if (kirbyExtension != null)
            {
                Position = kirbyExtension.Position;
            }
            else if (player != null)
            {
                Position = player.Position;
            }
        }

        #endregion

        #region State Updates

        private void UpdateInactive()
        {
            // Check for transformation conditions
            if (!isChapterEligible) return;
            
            var settings = IngesteModule.Settings;
            if (settings == null || !settings.KnightModeEnabled) return;
            
            // Check for manual activation
            if (settings.KirbyKnightBind.Pressed && CanTransform())
            {
                BeginTransformation();
                return;
            }
            
            // Check for emergency transformation (low health)
            if (settings.KnightLowHealthTransform && ShouldEmergencyTransform())
            {
                BeginTransformation(isEmergency: true);
            }
        }

        private void UpdateTransforming()
        {
            transformTimer += Engine.DeltaTime;
            
            // Visual effects during transformation
            bloom.Alpha = Ease.SineInOut(transformTimer / 1.5f) * 0.8f;
            light.Alpha = Ease.SineInOut(transformTimer / 1.5f);
            
            if (transformTimer >= 1.5f)
            {
                CompleteTransformation();
            }
        }

        private void UpdateActive()
        {
            var settings = IngesteModule.Settings;
            if (settings == null) return;
            
            // Check for detransformation
            if (settings.KirbyKnightBind.Pressed && !IsInCombat())
            {
                BeginDetransformation();
                return;
            }
            
            // Basic attack
            if (settings.KirbyAttackBind.Pressed && CanAttack)
            {
                PerformAttack(KnightAttackType.Slash);
                return;
            }
            
            // Charge attack (hold attack)
            if (settings.KirbyAttackBind.Check && CanAttack)
            {
                CurrentState = KnightState.Charging;
                chargeTimer = 0f;
                chargeSound?.Play(SFX_CHARGE);
                return;
            }
            
            // Special attack
            if (settings.KirbyInhaleBind.Pressed && CanSpecial)
            {
                PerformSpecialAttack();
                return;
            }
            
            // Finisher (charged special)
            if (settings.KirbyInhaleBind.Check && settings.KirbyAttackBind.Check && CanFinisher)
            {
                BeginFinisher();
                return;
            }
            
            // Block
            if (settings.KirbySlideBind.Check)
            {
                CurrentState = KnightState.Blocking;
            }
        }

        private void UpdateAttacking()
        {
            stateTimer += Engine.DeltaTime;
            
            if (stateTimer >= 0.3f)
            {
                CurrentState = KnightState.Active;
                stateTimer = 0f;
            }
        }

        private void UpdateCharging()
        {
            chargeTimer += Engine.DeltaTime;
            ChargeLevel = Math.Min(chargeTimer / CHARGE_TIME_MAX, 1f);
            
            // Visual feedback
            bloom.Alpha = 0.5f + ChargeLevel * 0.5f;
            
            // Emit charging particles
            if (chargeTimer % 0.1f < Engine.DeltaTime)
            {
                level.ParticlesFG?.Emit(P_KnightCharge, 1, Position, Vector2.One * 8f);
            }
            
            var settings = IngesteModule.Settings;
            if (settings != null && !settings.KirbyAttackBind.Check)
            {
                // Release charged attack
                if (ChargeLevel >= 0.5f)
                {
                    PerformAttack(KnightAttackType.ChargeSlash);
                }
                else
                {
                    PerformAttack(KnightAttackType.Slash);
                }
                chargeTimer = 0f;
                ChargeLevel = 0f;
            }
        }

        private void UpdateSpecialAttack()
        {
            stateTimer += Engine.DeltaTime;
            
            if (stateTimer >= 0.5f)
            {
                CurrentState = KnightState.Active;
                stateTimer = 0f;
            }
        }

        private void UpdateFinisher()
        {
            stateTimer += Engine.DeltaTime;
            
            // Screen effects during finisher
            if (stateTimer < 1.5f)
            {
                level.Shake(0.2f);
            }
            
            if (stateTimer >= 2.0f)
            {
                CompleteFinisher();
                CurrentState = KnightState.Active;
                stateTimer = 0f;
            }
        }

        private void UpdateBlocking()
        {
            var settings = IngesteModule.Settings;
            if (settings == null || !settings.KirbySlideBind.Check)
            {
                CurrentState = KnightState.Active;
            }
        }

        private void UpdateDetransforming()
        {
            transformTimer -= Engine.DeltaTime;
            
            bloom.Alpha = Ease.SineInOut(transformTimer / 1.5f) * 0.8f;
            light.Alpha = Ease.SineInOut(transformTimer / 1.5f);
            
            if (transformTimer <= 0f)
            {
                CompleteDetransformation();
            }
        }

        #endregion

        #region Transformation

        public bool CanTransform()
        {
            if (!isChapterEligible) return false;
            
            var settings = IngesteModule.Settings;
            if (settings == null || !settings.KnightModeEnabled) return false;
            
            // Check if in correct chapter segment
            if (level.Session.Area.ID == CHAPTER_19)
            {
                return level.Session.GetFlag(FLAG_CH19_FINAL_RUN);
            }
            else if (level.Session.Area.ID == CHAPTER_20)
            {
                return true; // Always available in Chapter 20
            }
            
            return false;
        }

        private bool ShouldEmergencyTransform()
        {
            if (kirbyExtension != null)
            {
                return kirbyExtension.CurrentHealth <= 1;
            }
            return false;
        }

        public void BeginTransformation(bool isEmergency = false)
        {
            if (CurrentState != KnightState.Inactive) return;
            
            CurrentState = KnightState.Transforming;
            transformTimer = 0f;
            
            // Play transformation effects
            Audio.Play(SFX_TRANSFORM, Position);
            level.Flash(Color.Gold * 0.4f, true);
            level.Shake(0.3f);
            
            // Emit transformation particles
            level.ParticlesFG?.Emit(P_KnightGlow, 30, Position, Vector2.One * 32f);
            
            // Set session flag
            level.Session.SetFlag(FLAG_KNIGHT_ACTIVE, true);
            if (isEmergency)
            {
                level.Session.SetFlag(FLAG_KNIGHT_EMERGENCY, true);
            }
            
            // Update helper state
            KirbyKnightHelper.TryTransformToKnight(level, true);
            
            IngesteLogger.Info($"Knight transformation started{(isEmergency ? " (EMERGENCY)" : "")}");
        }

        private void CompleteTransformation()
        {
            CurrentState = KnightState.Active;
            transformTimer = 0f;
            
            bloom.Alpha = 0.5f;
            light.Alpha = 0.8f;
            
            // Set Kirby power state
            kirbyExtension?.SetPowerState(KirbyMode.KirbyPowerState.Knight);
            
            IngesteLogger.Info("Knight transformation complete");
        }

        public void BeginDetransformation()
        {
            if (CurrentState != KnightState.Active) return;
            
            CurrentState = KnightState.Detransforming;
            transformTimer = 1.5f;
            
            Audio.Play(SFX_DETRANSFORM, Position);
        }

        private void CompleteDetransformation()
        {
            CurrentState = KnightState.Inactive;
            
            bloom.Alpha = 0f;
            light.Alpha = 0f;
            
            level.Session.SetFlag(FLAG_KNIGHT_ACTIVE, false);
            level.Session.SetFlag(FLAG_KNIGHT_EMERGENCY, false);
            
            KirbyKnightHelper.ResetKnight();
            kirbyExtension?.SetPowerState(KirbyMode.KirbyPowerState.None);
            
            IngesteLogger.Info("Knight detransformation complete");
        }

        #endregion

        #region Combat

        public void PerformAttack(KnightAttackType attackType)
        {
            CurrentState = KnightState.Attacking;
            stateTimer = 0f;
            attackCooldown = ATTACK_COOLDOWN;
            
            // Update combo
            comboCount++;
            comboTimer = 1.5f;
            
            // Get attack direction
            Vector2 direction = GetAttackDirection();
            lastAttackDirection = direction;
            
            // Calculate damage
            float damage = CalculateAttackDamage(attackType);
            
            // Play sound
            Audio.Play(SFX_ATTACK, Position);
            
            // Visual effects
            EmitAttackParticles(attackType, direction);
            
            // Create hitbox and deal damage
            CreateAttackHitbox(attackType, direction, damage);
            
            IngesteLogger.Debug($"Knight attack: {attackType}, combo: {comboCount}, damage: {damage}");
        }

        public void PerformSpecialAttack()
        {
            CurrentState = KnightState.SpecialAttack;
            stateTimer = 0f;
            specialCooldown = SPECIAL_COOLDOWN;
            
            Vector2 direction = GetAttackDirection();
            
            Audio.Play(SFX_SPECIAL, Position);
            
            // Sword beam projectile
            if (level != null)
            {
                var swordBeam = new KnightSwordBeam(Position, direction, DamageMultiplier);
                level.Add(swordBeam);
            }
            
            level.ParticlesFG?.Emit(P_KnightSlash, 20, Position + direction * 16f, Vector2.One * 16f);
            level.Shake(0.1f);
            
            IngesteLogger.Debug("Knight special attack: Sword Beam");
        }

        private void BeginFinisher()
        {
            CurrentState = KnightState.Finisher;
            stateTimer = 0f;
            finisherCooldown = FINISHER_COOLDOWN;
            
            Audio.Play(SFX_FINISHER, Position);
            level.Flash(Color.Gold * 0.6f, true);
            
            // Slow down time effect
            timeRateModifier.SetTimeRateMultiplier(0.5f);
        }

        private void CompleteFinisher()
        {
            timeRateModifier.ResetTimeRateMultiplier();
            
            // Massive damage in area
            float damage = DamageMultiplier * 5f;
            
            // Create large hitbox
            foreach (var entity in level.Tracker.GetEntities<KirbySmallEnemy>())
            {
                if (entity is KirbySmallEnemy enemy)
                {
                    float dist = Vector2.Distance(Position, enemy.Position);
                    if (dist < 128f)
                    {
                        enemy.OnHit((int)damage, (enemy.Position - Position).SafeNormalize() * 200f);
                    }
                }
            }
            
            level.ParticlesFG?.Emit(P_KnightFinisher, 50, Position, Vector2.One * 64f);
            level.Shake(0.5f);
            
            IngesteLogger.Debug($"Knight FINISHER complete! Damage: {damage}");
        }

        public float TakeBlockedDamage(float incomingDamage)
        {
            if (!IsBlocking) return incomingDamage;
            
            Audio.Play(SFX_BLOCK, Position);
            level.ParticlesFG?.Emit(P_KnightSlash, 5, Position, Vector2.One * 8f);
            
            return incomingDamage * BLOCK_DAMAGE_REDUCTION;
        }

        #endregion

        #region Helpers

        private void CheckChapterEligibility()
        {
            if (level?.Session == null)
            {
                isChapterEligible = false;
                return;
            }
            
            int chapterId = level.Session.Area.ID;
            isChapterEligible = chapterId == CHAPTER_19 || chapterId == CHAPTER_20;
            
            // Set unlock flag if in eligible chapter
            if (isChapterEligible)
            {
                level.Session.SetFlag(FLAG_KNIGHT_UNLOCKED, true);
            }
        }

        private bool IsInCombat()
        {
            // Check if there are enemies nearby
            foreach (var entity in level.Tracker.GetEntities<KirbySmallEnemy>())
            {
                if (Vector2.Distance(Position, entity.Position) < 200f)
                {
                    return true;
                }
            }
            return comboTimer > 0;
        }

        private Vector2 GetAttackDirection()
        {
            Vector2 dir = new Vector2(Input.MoveX.Value, Input.MoveY.Value);
            if (dir.LengthSquared() > 0)
            {
                return dir.SafeNormalize();
            }
            
            // Default to facing direction
            if (kirbyExtension != null)
            {
                return kirbyExtension.Facing == Facings.Right ? Vector2.UnitX : -Vector2.UnitX;
            }
            if (player != null)
            {
                return (int)player.Facing == 1 ? Vector2.UnitX : -Vector2.UnitX;
            }
            
            return Vector2.UnitX;
        }

        private float GetDamageMultiplier()
        {
            var settings = IngesteModule.Settings;
            float baseMult = settings?.KnightDamageMultiplier ?? BASE_DAMAGE_MULTIPLIER;
            
            // Combo bonus
            float comboMult = 1f + (comboCount * 0.1f);
            
            return baseMult * comboMult;
        }

        private float CalculateAttackDamage(KnightAttackType attackType)
        {
            float baseDamage = attackType switch
            {
                KnightAttackType.Slash => 10f,
                KnightAttackType.Thrust => 15f,
                KnightAttackType.UpperSlash => 12f,
                KnightAttackType.SpinSlash => 8f,
                KnightAttackType.ChargeSlash => 25f + (ChargeLevel * 25f),
                KnightAttackType.SwordBeam => 20f,
                KnightAttackType.Finisher => 100f,
                _ => 10f
            };
            
            return baseDamage * DamageMultiplier;
        }

        private void EmitAttackParticles(KnightAttackType attackType, Vector2 direction)
        {
            int count = attackType switch
            {
                KnightAttackType.ChargeSlash => 20,
                KnightAttackType.SpinSlash => 30,
                KnightAttackType.Finisher => 50,
                _ => 10
            };
            
            Vector2 offset = direction * 24f;
            level.ParticlesFG?.Emit(P_KnightSlash, count, Position + offset, Vector2.One * 16f);
        }

        private void CreateAttackHitbox(KnightAttackType attackType, Vector2 direction, float damage)
        {
            // Attack range varies by type
            float range = attackType switch
            {
                KnightAttackType.Thrust => 48f,
                KnightAttackType.SpinSlash => 32f,
                KnightAttackType.ChargeSlash => 40f,
                _ => 32f
            };
            
            // Check for enemies in range
            foreach (var entity in level.Tracker.GetEntities<KirbySmallEnemy>())
            {
                if (entity is KirbySmallEnemy enemy)
                {
                    Vector2 toEnemy = enemy.Position - Position;
                    float dist = toEnemy.Length();
                    
                    if (dist < range)
                    {
                        // For directional attacks, check angle
                        if (attackType != KnightAttackType.SpinSlash)
                        {
                            float dot = Vector2.Dot(toEnemy.SafeNormalize(), direction);
                            if (dot < 0.5f) continue;
                        }
                        
                        enemy.OnHit((int)damage, (enemy.Position - Position).SafeNormalize() * 100f);
                    }
                }
            }
        }

        private static void InitializeParticles()
        {
            if (P_KnightSlash != null) return;

            P_KnightSlash = new ParticleType
            {
                Size = 2f,
                Color = Color.Gold,
                Color2 = Color.White,
                ColorMode = ParticleType.ColorModes.Blink,
                FadeMode = ParticleType.FadeModes.Late,
                LifeMin = 0.2f,
                LifeMax = 0.4f,
                SpeedMin = 60f,
                SpeedMax = 120f,
                DirectionRange = (float)Math.PI * 0.5f
            };

            P_KnightCharge = new ParticleType
            {
                Size = 1.5f,
                Color = Color.Yellow,
                Color2 = Color.Gold,
                ColorMode = ParticleType.ColorModes.Fade,
                FadeMode = ParticleType.FadeModes.Linear,
                LifeMin = 0.3f,
                LifeMax = 0.6f,
                SpeedMin = 20f,
                SpeedMax = 40f,
                DirectionRange = (float)Math.PI * 2f,
                Acceleration = new Vector2(0f, -50f)
            };

            P_KnightGlow = new ParticleType
            {
                Size = 3f,
                Color = Color.Gold * 0.8f,
                Color2 = Color.White * 0.5f,
                ColorMode = ParticleType.ColorModes.Static,
                FadeMode = ParticleType.FadeModes.Late,
                LifeMin = 0.8f,
                LifeMax = 1.5f,
                SpeedMin = 10f,
                SpeedMax = 30f,
                DirectionRange = (float)Math.PI * 2f
            };

            P_KnightFinisher = new ParticleType
            {
                Size = 4f,
                Color = Color.Gold,
                Color2 = Color.Yellow,
                ColorMode = ParticleType.ColorModes.Blink,
                FadeMode = ParticleType.FadeModes.Late,
                LifeMin = 0.5f,
                LifeMax = 1.0f,
                SpeedMin = 80f,
                SpeedMax = 160f,
                DirectionRange = (float)Math.PI * 2f
            };
        }

        #endregion

        #region Public API

        /// <summary>
        /// Check if Knight mode is currently active
        /// </summary>
        public static bool IsKnightModeActive(Level level)
        {
            var knight = level?.Tracker.GetEntity<KirbyKnightMode>();
            return knight?.CurrentState != KnightState.Inactive;
        }

        /// <summary>
        /// Try to activate Knight mode from external code
        /// </summary>
        public static bool TryActivateKnightMode(Level level, bool force = false)
        {
            var knight = level?.Tracker.GetEntity<KirbyKnightMode>();
            if (knight == null)
            {
                // Create and add knight mode entity
                knight = new KirbyKnightMode();
                level?.Add(knight);
            }
            
            if (force || knight.CanTransform())
            {
                knight.BeginTransformation(isEmergency: force);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Deactivate Knight mode
        /// </summary>
        public static void DeactivateKnightMode(Level level)
        {
            var knight = level?.Tracker.GetEntity<KirbyKnightMode>();
            knight?.BeginDetransformation();
        }

        /// <summary>
        /// Set the chapter 19 final run flag
        /// </summary>
        public static void SetChapter19FinalRun(Level level, bool enabled)
        {
            level?.Session.SetFlag(FLAG_CH19_FINAL_RUN, enabled);
            IngesteLogger.Info($"Chapter 19 Final Run: {enabled}");
        }

        /// <summary>
        /// Set the chapter 20 last push flag
        /// </summary>
        public static void SetChapter20LastPush(Level level, bool enabled)
        {
            level?.Session.SetFlag(FLAG_CH20_LAST_PUSH, enabled);
            IngesteLogger.Info($"Chapter 20 Last Push: {enabled}");
        }

        #endregion
    }

    /// <summary>
    /// Sword beam projectile created by Knight special attack
    /// </summary>
    public class KnightSwordBeam : Entity
    {
        private Vector2 speed;
        private float damage;
        private float lifetime = 2f;
        private Level level;

        private static ParticleType P_Trail;

        public KnightSwordBeam(Vector2 position, Vector2 direction, float damageMultiplier) : base(position)
        {
            speed = direction.SafeNormalize() * 300f;
            damage = 20f * damageMultiplier;
            
            Collider = new Hitbox(16f, 8f, -8f, -4f);
            
            // Rotate collider based on direction
            if (Math.Abs(direction.Y) > Math.Abs(direction.X))
            {
                Collider = new Hitbox(8f, 16f, -4f, -8f);
            }
            
            InitializeParticle();
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = scene as Level;
            
            Audio.Play("event:/desolozantas/char/kirby/sword_beam", Position);
        }

        public override void Update()
        {
            base.Update();
            
            Position += speed * Engine.DeltaTime;
            lifetime -= Engine.DeltaTime;
            
            // Trail particles
            level?.ParticlesFG?.Emit(P_Trail, 1, Position, Vector2.One * 4f);
            
            // Check for enemy collision
            foreach (var entity in Scene.Tracker.GetEntities<KirbySmallEnemy>())
            {
                if (entity is KirbySmallEnemy enemy && CollideCheck(enemy))
                {
                    enemy.OnHit((int)damage, speed.SafeNormalize() * 100f);
                    RemoveSelf();
                    return;
                }
            }
            
            // Check for solid collision
            if (CollideCheck<Solid>())
            {
                // Create impact effect
                level?.ParticlesFG?.Emit(P_Trail, 10, Position, Vector2.One * 8f);
                RemoveSelf();
                return;
            }
            
            // Lifetime check
            if (lifetime <= 0)
            {
                RemoveSelf();
            }
        }

        public override void Render()
        {
            base.Render();
            
            // Simple sword beam visual
            float angle = speed.Angle();
            Draw.Line(Position - speed.SafeNormalize() * 12f, Position + speed.SafeNormalize() * 12f, Color.Gold, 3f);
            Draw.Line(Position - speed.SafeNormalize() * 8f, Position + speed.SafeNormalize() * 8f, Color.White, 1f);
        }

        private static void InitializeParticle()
        {
            if (P_Trail != null) return;
            
            P_Trail = new ParticleType
            {
                Size = 1.5f,
                Color = Color.Gold,
                Color2 = Color.Yellow,
                ColorMode = ParticleType.ColorModes.Fade,
                FadeMode = ParticleType.FadeModes.Linear,
                LifeMin = 0.1f,
                LifeMax = 0.3f,
                SpeedMin = 10f,
                SpeedMax = 20f
            };
        }
    }
}
