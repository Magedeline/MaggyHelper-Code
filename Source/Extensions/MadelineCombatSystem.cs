using System.Collections.Generic;
using System.Reflection;
using Monocle;

namespace MaggyHelper.Extensions;

/// <summary>
/// Additive Madeline combat/runtime layer.
///
/// This keeps vanilla Player.cs as the source of truth and injects extra behavior
/// through hooks only:
/// - HUD health/stamina bars
/// - Melee strike input
/// - Side charge attack (ground lunge)
/// - Health buffering before death
/// </summary>
public static class MadelineCombatSystem
{
    private enum MeleePhase
    {
        Idle,
        Startup,
        Active,
        Recovery
    }

    private enum AttackDirection
    {
        Forward,
        Up,
        Down
    }

    private enum WeaponType
    {
        Katana,
        BattleAxe,
        ArcherBow
    }

    private sealed class CombatState
    {
        public bool Initialized;
        public int MaxHealth;
        public int Health;
        public float InvulnTimer;
        public MeleePhase Phase;
        public float PhaseTimer;
        public int ComboStep;
        public float ComboTimer;
        public AttackDirection CurrentAttackDirection;
        public readonly HashSet<Entity> HitThisSwing = new();
        public float ChargeHeld;
        public bool ChargeHeldLastFrame;
        public float LastHitFxTimer;
        public float LastSlashFxTimer;
        public Vector2 LastSlashFxPosition;
        public WeaponType CurrentWeapon;
        public float WeaponSwapCooldown;
    }

    private sealed class MadelineArrowProjectile : Entity
    {
        private Vector2 velocity;
        private readonly int damage;
        private readonly Player owner;
        private float lifeTimer;

        public MadelineArrowProjectile(Vector2 position, Vector2 velocity, int damage, Player owner)
            : base(position)
        {
            this.velocity = velocity;
            this.damage = damage;
            this.owner = owner;
            lifeTimer = 1.8f;
            Collider = new Hitbox(10f, 4f, -5f, -2f);
            Depth = Depths.Player - 2;
        }

        public override void Update()
        {
            base.Update();

            if (Scene is not Level level)
            {
                RemoveSelf();
                return;
            }

            lifeTimer -= Engine.DeltaTime;
            if (lifeTimer <= 0f)
            {
                RemoveSelf();
                return;
            }

            Position += velocity * Engine.DeltaTime;

            if (CollideCheck<Solid>() || CollideCheck<JumpThru>())
            {
                RemoveSelf();
                return;
            }

            foreach (Entity entity in level.Entities)
            {
                if (entity == null || entity == owner || !entity.Collidable)
                {
                    continue;
                }

                string typeName = entity.GetType().Name;
                if (!typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains("Seeker", StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!entity.CollideCheck(this))
                {
                    continue;
                }

                if (TryApplyDamage(entity, owner, velocity.SafeNormalize(), damage))
                {
                    level.ParticlesFG.Emit(ParticleTypes.SparkyDust, 6, Position, Vector2.One * 3f);
                    RemoveSelf();
                    return;
                }
            }
        }

        public override void Render()
        {
            Vector2 normal = velocity.SafeNormalize();
            Vector2 tail = Position - normal * 9f;
            Draw.Line(tail, Position, Calc.HexToColor("f7f4d2"), 2f);
            Draw.Line(tail - normal.Perpendicular() * 2f, Position, Calc.HexToColor("7f563b"), 1f);
        }
    }

    private static readonly Dictionary<Player, CombatState> States = new();
    private const string SfxPath = "event:/desolozantas/char/kirby/";
    private const string SfxHurt = SfxPath + "predeath";
    private const string SfxCharge = SfxPath + "bounce";
    private static readonly float[] ComboStartup = { 0.06f, 0.05f, 0.04f };
    private static readonly float[] ComboActive = { 0.05f, 0.06f, 0.07f };
    private static readonly float[] ComboRecovery = { 0.14f, 0.13f, 0.16f };
    private const float ComboWindow = 0.26f;
    private const float WeaponSwapCooldownTime = 0.15f;
    private const string HollowKnightFxBasePath = "characters/MaggyHelper/hollow_knight/";

    private static bool _fxFramesLoaded;
    private static List<MTexture> _fxShadeCloakFrames = new();
    private static List<MTexture> _fxDreamNailFrames = new();
    private static List<MTexture> _fxDreamNailReflectFrames = new();
    private static List<MTexture> _fxVoidHeartFrames = new();
    private static List<MTexture> _fxChallengeFrames = new();

    public static void Load()
    {
        On.Celeste.Player.Update += OnPlayerUpdate;
        On.Celeste.Player.Die += OnPlayerDie;
        On.Celeste.Level.Render += OnLevelRender;
        On.Celeste.Level.UnloadLevel += OnLevelUnload;
    }

    public static void Unload()
    {
        On.Celeste.Player.Update -= OnPlayerUpdate;
        On.Celeste.Player.Die -= OnPlayerDie;
        On.Celeste.Level.Render -= OnLevelRender;
        On.Celeste.Level.UnloadLevel -= OnLevelUnload;
        States.Clear();
        _fxFramesLoaded = false;
        _fxShadeCloakFrames.Clear();
        _fxDreamNailFrames.Clear();
        _fxDreamNailReflectFrames.Clear();
        _fxVoidHeartFrames.Clear();
        _fxChallengeFrames.Clear();
    }

    private static void OnLevelUnload(On.Celeste.Level.orig_UnloadLevel orig, Level self)
    {
        orig(self);
        States.Clear();
    }

    private static void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self)
    {
        orig(self);

        if (!IsFeatureEnabled() || self?.Scene is not Level level || self.Dead)
        {
            return;
        }

        CombatState state = GetState(self);
        TickTimers(state);

        if (IsWeaponCyclePressed() && state.WeaponSwapCooldown <= 0f)
        {
            state.CurrentWeapon = GetNextWeapon(state.CurrentWeapon);
            state.WeaponSwapCooldown = WeaponSwapCooldownTime;
            state.Phase = MeleePhase.Idle;
            state.ComboTimer = 0f;
        }

        if (state.CurrentWeapon == WeaponType.ArcherBow)
        {
            if (IsMeleePressed())
            {
                FireArrow(level, self, ResolveAttackDirection(self), 280f, 1);
                state.LastSlashFxTimer = 0.12f;
                state.LastSlashFxPosition = self.Center + new Vector2((int) self.Facing * 18f, 0f);
            }
        }
        else if (IsMeleePressed())
        {
            if (state.Phase == MeleePhase.Idle)
            {
                int comboStep = state.CurrentWeapon == WeaponType.Katana
                    ? (state.ComboTimer > 0f ? Math.Min(state.ComboStep + 1, 2) : 0)
                    : 0;
                BeginComboStep(self, state, comboStep);
            }
            else if (state.CurrentWeapon == WeaponType.Katana && state.Phase == MeleePhase.Recovery && state.ComboTimer > 0f)
            {
                BeginComboStep(self, state, Math.Min(state.ComboStep + 1, 2));
            }
        }

        UpdateMeleePhase(self, level, state);

        // Side charge attack inspired by high-mobility action games.
        bool chargeHeld = IsChargeHeld();
        if (chargeHeld && (state.CurrentWeapon == WeaponType.ArcherBow || self.OnGround()) && Math.Abs(self.Speed.X) < 40f)
        {
            state.ChargeHeld = Math.Min(1.2f, state.ChargeHeld + Engine.DeltaTime);
        }

        bool chargeReleased = state.ChargeHeldLastFrame && !chargeHeld;
        if (chargeReleased && state.ChargeHeld >= 0.18f)
        {
            float t = Calc.ClampedMap(state.ChargeHeld, 0.18f, 1.2f, 0f, 1f);
            if (state.CurrentWeapon == WeaponType.ArcherBow)
            {
                float shotSpeed = MathHelper.Lerp(300f, 520f, t);
                int damage = t >= 0.7f ? 3 : 2;
                FireArrow(level, self, ResolveAttackDirection(self), shotSpeed, damage);
                level.ParticlesFG.Emit(ParticleTypes.SparkyDust, 12, self.Center, Vector2.One * 5f);
            }
            else
            {
                float speedX = state.CurrentWeapon == WeaponType.BattleAxe
                    ? MathHelper.Lerp(160f, 300f, t) * (int) self.Facing
                    : MathHelper.Lerp(200f, 360f, t) * (int) self.Facing;
                float speedY = state.CurrentWeapon == WeaponType.BattleAxe
                    ? MathHelper.Lerp(-10f, -60f, t)
                    : (self.OnGround()
                        ? MathHelper.Lerp(-20f, -95f, t)
                        : MathHelper.Lerp(-10f, -70f, t));

                self.Speed = new Vector2(speedX, speedY);
                level.DirectionalShake(new Vector2(Math.Sign(speedX), 0f), 0.12f);
                level.ParticlesFG.Emit(ParticleTypes.SparkyDust, 10, self.Center, Vector2.One * 6f);
                Audio.Play(SfxCharge, self.Position);
            }
        }

        if (!chargeHeld)
        {
            state.ChargeHeld = 0f;
        }

        state.ChargeHeldLastFrame = chargeHeld;
    }

    private static PlayerDeadBody OnPlayerDie(
        On.Celeste.Player.orig_Die orig,
        Player self,
        Vector2 direction,
        bool evenIfInvincible,
        bool registerDeathInStats)
    {
        if (!IsFeatureEnabled() || self?.Scene is not Level)
        {
            return orig(self, direction, evenIfInvincible, registerDeathInStats);
        }

        CombatState state = GetState(self);
        if (state.InvulnTimer > 0f)
        {
            return null;
        }

        if (state.Health > 1)
        {
            state.Health--;
            state.InvulnTimer = 1f;
            self.Speed = (-direction.SafeNormalize()) * 140f + (Vector2.UnitY * -80f);
            Audio.Play(SfxHurt, self.Position);
            return null;
        }

        return orig(self, direction, evenIfInvincible, registerDeathInStats);
    }

    private static void OnLevelRender(On.Celeste.Level.orig_Render orig, Level self)
    {
        orig(self);

        if (!IsFeatureEnabled() || !ShowHud())
        {
            return;
        }

        Player player = self.Tracker.GetEntity<Player>();
        if (player == null)
        {
            return;
        }

        CombatState state = GetState(player);

        float staminaMax = Math.Max(1f, Player.ClimbMaxStamina);
        float staminaNorm = Calc.Clamp(player.Stamina / staminaMax, 0f, 1f);
        float healthNorm = Calc.Clamp(state.Health / (float) state.MaxHealth, 0f, 1f);

        Vector2 origin = self.Camera.Position + new Vector2(24f, 24f);
        DrawBar(origin, 170f, 10f, healthNorm, Calc.HexToColor("1f1f1f"), Calc.HexToColor("f05454"));
        DrawBar(origin + new Vector2(0f, 16f), 170f, 8f, staminaNorm, Calc.HexToColor("1f1f1f"), Calc.HexToColor("4ecdc4"));
        ActiveFont.DrawOutline(GetWeaponLabel(state.CurrentWeapon), origin + new Vector2(0f, 28f), Vector2.Zero, Vector2.One * 0.5f, Color.White, 2f, Color.Black);

        RenderHollowKnightEffects(self, player, state);
    }

    private static void DrawBar(Vector2 pos, float width, float height, float value, Color bg, Color fg)
    {
        Draw.Rect(pos.X - 1f, pos.Y - 1f, width + 2f, height + 2f, Color.Black * 0.6f);
        Draw.Rect(pos.X, pos.Y, width, height, bg);
        Draw.Rect(pos.X, pos.Y, width * value, height, fg);
    }

    private static void BeginComboStep(Player player, CombatState state, int comboStep)
    {
        state.ComboStep = comboStep;
        state.ComboTimer = ComboWindow;
        state.CurrentAttackDirection = ResolveAttackDirection(player);
        state.Phase = MeleePhase.Startup;
        state.PhaseTimer = GetStartupTime(state.CurrentWeapon, comboStep);
        state.HitThisSwing.Clear();
    }

    private static AttackDirection ResolveAttackDirection(Player player)
    {
        if (player == null)
        {
            return AttackDirection.Forward;
        }

        int moveY = Input.MoveY.Value;
        if (!player.OnGround() && moveY > 0)
        {
            return AttackDirection.Down;
        }

        if (moveY < 0)
        {
            return AttackDirection.Up;
        }

        return AttackDirection.Forward;
    }

    private static void UpdateMeleePhase(Player player, Level level, CombatState state)
    {
        if (state.Phase == MeleePhase.Idle)
        {
            return;
        }

        state.PhaseTimer -= Engine.DeltaTime;

        if (state.Phase == MeleePhase.Active)
        {
            ExecuteMeleeStrike(player, level, state);
        }

        if (state.PhaseTimer > 0f)
        {
            return;
        }

        switch (state.Phase)
        {
            case MeleePhase.Startup:
                state.Phase = MeleePhase.Active;
                state.PhaseTimer = GetActiveTime(state.CurrentWeapon, state.ComboStep);
                break;
            case MeleePhase.Active:
                state.Phase = MeleePhase.Recovery;
                state.PhaseTimer = GetRecoveryTime(state.CurrentWeapon, state.ComboStep);
                break;
            default:
                state.Phase = MeleePhase.Idle;
                break;
        }
    }

    private static void ExecuteMeleeStrike(Player player, Level level, CombatState state)
    {
        if (state.CurrentWeapon == WeaponType.ArcherBow)
        {
            return;
        }

        float range = state.ComboStep switch
        {
            0 => 16f,
            1 => 18f,
            _ => 22f
        };

        if (state.CurrentWeapon == WeaponType.BattleAxe)
        {
            range = 24f;
        }

        Vector2 attackDir;
        Rectangle hitRect;

        switch (state.CurrentAttackDirection)
        {
            case AttackDirection.Up:
                attackDir = -Vector2.UnitY;
                hitRect = new Rectangle(
                    (int) (player.Center.X - (state.CurrentWeapon == WeaponType.BattleAxe ? 15f : 11f)),
                    (int) (player.Center.Y - (state.CurrentWeapon == WeaponType.BattleAxe ? 38f : 34f)),
                    state.CurrentWeapon == WeaponType.BattleAxe ? 30 : 22,
                    state.CurrentWeapon == WeaponType.BattleAxe ? 30 : 24);
                break;
            case AttackDirection.Down:
                attackDir = Vector2.UnitY;
                hitRect = new Rectangle(
                    (int) (player.Center.X - (state.CurrentWeapon == WeaponType.BattleAxe ? 15f : 11f)),
                    (int) (player.Center.Y + 4f),
                    state.CurrentWeapon == WeaponType.BattleAxe ? 30 : 22,
                    state.CurrentWeapon == WeaponType.BattleAxe ? 30 : 24);
                break;
            default:
                attackDir = new Vector2((int) player.Facing, 0f);
                Vector2 offset = new Vector2((int) player.Facing * range, 0f);
                hitRect = new Rectangle(
                    (int) (player.Center.X + offset.X - (state.CurrentWeapon == WeaponType.BattleAxe ? 18f : 13f)),
                    (int) (player.Center.Y - (state.CurrentWeapon == WeaponType.BattleAxe ? 16f : 12f)),
                    state.CurrentWeapon == WeaponType.BattleAxe ? 36 : 26,
                    state.CurrentWeapon == WeaponType.BattleAxe ? 32 : 24);
                break;
        }

        level.ParticlesFG.Emit(ParticleTypes.SparkyDust, 6, hitRect.Center.ToVector2(), Vector2.One * 4f);

        foreach (Entity entity in level.Entities)
        {
            if (entity == null || entity == player || !entity.Collidable)
            {
                continue;
            }

            string typeName = entity.GetType().Name;
            if (!typeName.Contains("Enemy", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Seeker", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Boss", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entity.CollideRect(hitRect))
            {
                continue;
            }

            if (state.HitThisSwing.Contains(entity))
            {
                continue;
            }

            int damage = state.CurrentWeapon == WeaponType.BattleAxe ? 3 : state.ComboStep + 1;
            if (TryApplyDamage(entity, player, attackDir, damage))
            {
                state.HitThisSwing.Add(entity);
                level.DirectionalShake(attackDir, 0.05f);
                global::Celeste.Celeste.Freeze(0.02f);
                state.LastHitFxTimer = 0.2f;
                state.LastSlashFxTimer = 0.16f;
                state.LastSlashFxPosition = hitRect.Center.ToVector2();

                if (state.CurrentAttackDirection == AttackDirection.Down && !player.OnGround())
                {
                    // Downward strike bounce (pogo-like) to chain aerial pressure.
                    player.Speed = new Vector2(player.Speed.X * 0.35f, state.CurrentWeapon == WeaponType.BattleAxe ? -170f : -220f);
                }

                if (state.CurrentAttackDirection == AttackDirection.Up)
                {
                    player.Speed = new Vector2(player.Speed.X, Math.Min(player.Speed.Y, -120f));
                }
            }
        }
    }

    private static bool TryApplyDamage(Entity target, Player attacker, Vector2 direction, int amount)
    {
        Type type = target.GetType();

        // Prefer explicit health/damage interfaces if the entity exposes one.
        if (TryInvokeMethod(type, target, "TakeDamage", new object[] { amount }))
        {
            return true;
        }

        if (TryInvokeMethod(type, target, "Damage", new object[] { amount }))
        {
            return true;
        }

        if (TryInvokeMethod(type, target, "Hurt", new object[] { direction }))
        {
            return true;
        }

        if (TryInvokeMethod(type, target, "Hit", new object[] { direction }))
        {
            return true;
        }

        // Fallback: ask the target to resolve as a dash-style hit if it supports it.
        if (TryInvokeMethod(type, target, "OnDashCollide", new object[] { attacker, direction }))
        {
            return true;
        }

        return false;
    }

    private static void FireArrow(Level level, Player player, AttackDirection attackDirection, float speed, int damage)
    {
        Vector2 direction = attackDirection switch
        {
            AttackDirection.Up => -Vector2.UnitY,
            AttackDirection.Down => player.OnGround() ? new Vector2((int) player.Facing, 0f) : Vector2.UnitY,
            _ => new Vector2((int) player.Facing, 0f)
        };

        Vector2 spawn = player.Center + direction * 10f + new Vector2((int) player.Facing * 8f, 0f);
        level.Add(new MadelineArrowProjectile(spawn, direction.SafeNormalize() * speed, damage, player));
    }

    private static bool TryInvokeMethod(Type type, object instance, string methodName, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            ParameterInfo[] pars = method.GetParameters();
            if (pars.Length != args.Length)
            {
                continue;
            }

            bool compatible = true;
            for (int p = 0; p < pars.Length; p++)
            {
                object arg = args[p];
                if (arg == null)
                {
                    continue;
                }

                if (!pars[p].ParameterType.IsAssignableFrom(arg.GetType()))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible)
            {
                continue;
            }

            try
            {
                method.Invoke(instance, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static void TickTimers(CombatState state)
    {
        if (state.InvulnTimer > 0f)
        {
            state.InvulnTimer = Math.Max(0f, state.InvulnTimer - Engine.DeltaTime);
        }

        if (state.ComboTimer > 0f)
        {
            state.ComboTimer = Math.Max(0f, state.ComboTimer - Engine.DeltaTime);
        }

        if (state.LastHitFxTimer > 0f)
        {
            state.LastHitFxTimer = Math.Max(0f, state.LastHitFxTimer - Engine.DeltaTime);
        }

        if (state.LastSlashFxTimer > 0f)
        {
            state.LastSlashFxTimer = Math.Max(0f, state.LastSlashFxTimer - Engine.DeltaTime);
        }

        if (state.WeaponSwapCooldown > 0f)
        {
            state.WeaponSwapCooldown = Math.Max(0f, state.WeaponSwapCooldown - Engine.DeltaTime);
        }
    }

    private static void RenderHollowKnightEffects(Level level, Player player, CombatState state)
    {
        EnsureFxFramesLoaded();

        if (state.ChargeHeld > 0f)
        {
            DrawFxSequence(
                _fxShadeCloakFrames,
                player.Center + new Vector2((int) player.Facing * 8f, 0f),
                30f,
                Color.White * Calc.Clamp(0.35f + state.ChargeHeld * 0.45f, 0.35f, 0.9f),
                1.05f);
        }

        if (state.ChargeHeld > 0.55f)
        {
            DrawFxSequence(
                _fxVoidHeartFrames,
                player.Center + new Vector2(0f, -26f),
                24f,
                Color.White * 0.8f,
                0.9f);
        }

        if (state.Phase == MeleePhase.Active)
        {
            Vector2 slashPos = state.CurrentAttackDirection switch
            {
                AttackDirection.Up => player.Center + new Vector2(0f, -22f),
                AttackDirection.Down => player.Center + new Vector2(0f, 18f),
                _ => player.Center + new Vector2((int) player.Facing * 18f, 0f)
            };

            if (state.CurrentWeapon != WeaponType.ArcherBow)
            {
                DrawFxSequence(_fxDreamNailFrames, slashPos, 36f, Color.White * 0.72f, 1f);
            }
        }

        if (state.LastHitFxTimer > 0f)
        {
            DrawFxSequence(_fxDreamNailReflectFrames, state.LastSlashFxPosition, 45f, Color.White * 0.9f, 1f);
        }

        if (state.CurrentWeapon == WeaponType.Katana && state.ComboStep >= 2 && state.Phase != MeleePhase.Idle)
        {
            DrawFxSequence(_fxChallengeFrames, player.Center + new Vector2(0f, -10f), 28f, Color.White * 0.75f, 1f);
        }
    }

    private static void DrawFxSequence(List<MTexture> frames, Vector2 position, float fps, Color color, float scale)
    {
        if (frames == null || frames.Count == 0)
        {
            return;
        }

        int index = (int) (Engine.Scene.TimeActive * fps) % frames.Count;
        frames[index].DrawCentered(position, color, scale);
    }

    private static void EnsureFxFramesLoaded()
    {
        if (_fxFramesLoaded)
        {
            return;
        }

        _fxFramesLoaded = true;

        _fxShadeCloakFrames = LoadFrames("shadeCloak/shadeCloak");
        _fxDreamNailFrames = LoadFrames("dreamNail/dreamNail");
        _fxDreamNailReflectFrames = LoadFrames("dreamNailReflect/dreamNailReflect");
        _fxVoidHeartFrames = LoadFrames("voidHeart/voidHeart");
        _fxChallengeFrames = LoadFrames("challenge/challenge");
    }

    private static List<MTexture> LoadFrames(string suffix)
    {
        try
        {
            return GFX.Game.GetAtlasSubtextures(HollowKnightFxBasePath + suffix);
        }
        catch
        {
            return new List<MTexture>();
        }
    }

    private static CombatState GetState(Player player)
    {
        if (!States.TryGetValue(player, out CombatState state))
        {
            state = new CombatState();
            States[player] = state;
        }

        if (!state.Initialized)
        {
            state.Initialized = true;
            state.MaxHealth = Math.Max(1, GetMaxHealth());
            state.Health = state.MaxHealth;
            state.CurrentWeapon = WeaponType.Katana;
        }

        int max = Math.Max(1, GetMaxHealth());
        if (max != state.MaxHealth)
        {
            state.MaxHealth = max;
            state.Health = Math.Min(state.Health, state.MaxHealth);
        }

        return state;
    }

    private static bool IsFeatureEnabled()
    {
        return MaggyHelperModule.Settings?.MadelineCombatEnabled ?? false;
    }

    private static bool ShowHud()
    {
        return MaggyHelperModule.Settings?.MadelineCombatShowHud ?? true;
    }

    private static int GetMaxHealth()
    {
        return MaggyHelperModule.Settings?.MadelineCombatMaxHealth ?? 5;
    }

    private static bool IsMeleePressed()
    {
        ButtonBinding bind = MaggyHelperModule.Settings?.MadelineMeleeBind;
        if (bind == null)
        {
            return false;
        }

        return bind.Pressed;
    }

    private static bool IsChargeHeld()
    {
        ButtonBinding bind = MaggyHelperModule.Settings?.MadelineChargeBind;
        if (bind == null)
        {
            return false;
        }

        return bind.Check;
    }

    private static bool IsWeaponCyclePressed()
    {
        ButtonBinding bind = MaggyHelperModule.Settings?.MadelineWeaponCycleBind;
        if (bind == null)
        {
            return false;
        }

        return bind.Pressed;
    }

    private static float GetStartupTime(WeaponType weapon, int comboStep)
    {
        return weapon switch
        {
            WeaponType.BattleAxe => 0.12f,
            WeaponType.ArcherBow => 0f,
            _ => ComboStartup[comboStep]
        };
    }

    private static float GetActiveTime(WeaponType weapon, int comboStep)
    {
        return weapon switch
        {
            WeaponType.BattleAxe => 0.08f,
            WeaponType.ArcherBow => 0f,
            _ => ComboActive[comboStep]
        };
    }

    private static float GetRecoveryTime(WeaponType weapon, int comboStep)
    {
        return weapon switch
        {
            WeaponType.BattleAxe => 0.24f,
            WeaponType.ArcherBow => 0f,
            _ => ComboRecovery[comboStep]
        };
    }

    private static WeaponType GetNextWeapon(WeaponType current)
    {
        return current switch
        {
            WeaponType.Katana => WeaponType.BattleAxe,
            WeaponType.BattleAxe => WeaponType.ArcherBow,
            _ => WeaponType.Katana
        };
    }

    private static string GetWeaponLabel(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.BattleAxe => "Weapon: Battle Axe",
            WeaponType.ArcherBow => "Weapon: Archer Bow",
            _ => "Weapon: Small Katana"
        };
    }
}