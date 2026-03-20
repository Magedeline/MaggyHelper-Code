using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Entities;

/// <summary>
/// Floating companion heart that follows the player.
/// Offensive and defensive combat behavior is only enabled during the true final boss fight.
/// </summary>

[CustomEntity(ids: "MaggyHelper/HeartCompanion")]
[HotReloadable]
[Tracked]
public sealed class HeartCompanion : Entity
{
    public const int SquadSize = 7;

    private const float FollowSpeed = 260f;
    private const float OrbitSpeed = 2.2f;
    private const float BaseOrbitRadius = 16f;

    private Player player;
    private float orbit;
    private float defenseTimer;
    private float attackTimer;
    private float supportTimer;
    private MTexture heartTexture;
    private readonly int slotIndex;
    private readonly HeartRole role;
    private readonly string texturePath;

    public int SlotIndex => slotIndex;

    public HeartCompanion() : this(0)
    {
    }

    public HeartCompanion(int slotIndex) : base(Vector2.Zero)
    {
        this.slotIndex = Calc.Clamp(slotIndex, 0, SquadSize - 1);
        role = GetRole(this.slotIndex);
        texturePath = $"characters/soul/soul/vessel_soul{GetTextureSuffix(this.slotIndex)}";

        Tag = Tags.Persistent | Tags.TransitionUpdate;
        Depth = Depths.Player - 20 - this.slotIndex;
        Add(new VertexLight(Color.White, 0.65f, 20, 56));
        Add(new BloomPoint(0.7f, 8f));
    }

    public static void EnsureSquad(Level level)
    {
        if (level == null)
        {
            return;
        }

        HashSet<int> presentSlots = new HashSet<int>();
        foreach (HeartCompanion companion in level.Tracker.GetEntities<HeartCompanion>())
        {
            if (companion != null)
            {
                presentSlots.Add(companion.SlotIndex);
            }
        }

        for (int i = 0; i < SquadSize; i++)
        {
            if (!presentSlots.Contains(i))
            {
                level.Add(new HeartCompanion(i));
            }
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        player = scene.Tracker.GetEntity<Player>();

        if (GFX.Game.Has(texturePath))
        {
            heartTexture = GFX.Game[texturePath];
        }
        else if (GFX.Game.Has("characters/soul/soul/vessel_soulA"))
        {
            heartTexture = GFX.Game["characters/soul/soul/vessel_soulA"];
        }
    }

    public override void Update()
    {
        base.Update();

        if (Scene is not Level level)
        {
            return;
        }

        if (player == null || player.Scene != Scene || player.Dead)
        {
            player = level.Tracker.GetEntity<Player>();
        }

        if (player == null || player.Dead)
        {
            Visible = false;
            return;
        }

        Visible = true;

        orbit += Engine.DeltaTime * (OrbitSpeed + slotIndex * 0.12f);
        Vector2 anchor = player.Center + new Vector2(player.Facing == Facings.Left ? -16f : 16f, -14f);
        float angleOffset = slotIndex * ((float)Math.PI * 2f / SquadSize);
        float orbitRadius = BaseOrbitRadius + slotIndex * 3f;
        Vector2 target = anchor + Calc.AngleToVector(orbit + angleOffset, orbitRadius);
        Position = Calc.Approach(Position, target, FollowSpeed * Engine.DeltaTime);

        if (!IsTrueFinalBossContext(level))
        {
            return;
        }

        defenseTimer -= Engine.DeltaTime;
        attackTimer -= Engine.DeltaTime;
        supportTimer -= Engine.DeltaTime;

        if (RoleHasDefense(role) && defenseTimer <= 0f)
        {
            defenseTimer = GetDefenseInterval(role);
            RunDefensePulse(level, GetDefenseRadius(role));
        }

        if (RoleHasAttack(role) && attackTimer <= 0f)
        {
            BossActor targetBoss = FindTargetBoss(level);
            if (targetBoss != null)
            {
                attackTimer = GetAttackInterval(role);
                SpawnAttackBolts(level, targetBoss);
            }
        }

        if (RoleHasSupport(role) && supportTimer <= 0f)
        {
            supportTimer = 1.8f;
            RunSupportAbility(level);
        }
    }

    public override void Render()
    {
        if (!Visible)
        {
            return;
        }

        if (heartTexture != null)
        {
            heartTexture.DrawCentered(Position, Color.White, 0.55f);
            return;
        }

        Draw.Circle(Position, 5f, Color.White, 14);
        Draw.Circle(Position + new Vector2(0f, 2f), 3f, Color.White, 10);
    }

    private void RunDefensePulse(Level level, float radius)
    {
        foreach (BossProjectile projectile in level.Tracker.GetEntities<BossProjectile>())
        {
            if (projectile != null && projectile.Scene == level && Vector2.DistanceSquared(projectile.Center, player.Center) <= radius * radius)
            {
                projectile.RemoveSelf();
            }
        }

        level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 2, Position, Vector2.One * 2f, Color.White);
    }

    private void SpawnAttackBolts(Level level, BossActor targetBoss)
    {
        int boltCount = role == HeartRole.Surge ? 2 : 1;
        for (int i = 0; i < boltCount; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector2 spawnPos = Position + new Vector2(i == 0 && boltCount == 1 ? 0f : side * 5f, 0f);
            level.Add(new HeartCompanionBolt(
                spawnPos,
                targetBoss,
                Color.White,
                GetBoltDamage(role),
                GetBoltSpeed(role)));
        }
    }

    private void RunSupportAbility(Level level)
    {
        if (role == HeartRole.Medic)
        {
            var kirbyExt = level.Tracker.GetEntity<KirbyPlayerExtension>();
            if (kirbyExt != null && !kirbyExt.IsDead && kirbyExt.CurrentHealth < kirbyExt.MaxHealth)
            {
                kirbyExt.Heal(1);
            }
            else
            {
                var kirbyLegacy = level.Tracker.GetEntity<KirbyMode>();
                if (kirbyLegacy != null && !kirbyLegacy.IsDead && kirbyLegacy.CurrentHealth < kirbyLegacy.MaxHealth)
                {
                    kirbyLegacy.Heal(1);
                }
            }

            RunDefensePulse(level, 42f);
        }
        else if (role == HeartRole.Purifier)
        {
            BossActor targetBoss = FindTargetBoss(level);
            if (targetBoss != null && Vector2.DistanceSquared(targetBoss.Center, player.Center) <= 128f * 128f)
            {
                targetBoss.TakeDamage(1);
            }
        }
    }

    private static HeartRole GetRole(int slot)
    {
        return slot switch
        {
            0 => HeartRole.Leader,
            1 => HeartRole.Guardian,
            2 => HeartRole.Striker,
            3 => HeartRole.Sniper,
            4 => HeartRole.Medic,
            5 => HeartRole.Surge,
            _ => HeartRole.Purifier
        };
    }

    private static char GetTextureSuffix(int slot)
    {
        return (char)('A' + Calc.Clamp(slot, 0, SquadSize - 1));
    }

    private static bool RoleHasAttack(HeartRole role)
    {
        return role != HeartRole.Guardian;
    }

    private static bool RoleHasDefense(HeartRole role)
    {
        return role != HeartRole.Striker && role != HeartRole.Sniper;
    }

    private static bool RoleHasSupport(HeartRole role)
    {
        return role == HeartRole.Medic || role == HeartRole.Purifier;
    }

    private static float GetAttackInterval(HeartRole role)
    {
        return role switch
        {
            HeartRole.Striker => 0.35f,
            HeartRole.Surge => 0.75f,
            HeartRole.Sniper => 0.95f,
            _ => 0.60f
        };
    }

    private static int GetBoltDamage(HeartRole role)
    {
        return role switch
        {
            HeartRole.Sniper => 2,
            _ => 1
        };
    }

    private static float GetBoltSpeed(HeartRole role)
    {
        return role switch
        {
            HeartRole.Sniper => 360f,
            HeartRole.Striker => 300f,
            _ => 260f
        };
    }

    private static float GetDefenseRadius(HeartRole role)
    {
        return role switch
        {
            HeartRole.Guardian => 72f,
            HeartRole.Leader => 52f,
            HeartRole.Purifier => 58f,
            _ => 44f
        };
    }

    private static float GetDefenseInterval(HeartRole role)
    {
        return role switch
        {
            HeartRole.Guardian => 0.18f,
            HeartRole.Leader => 0.25f,
            _ => 0.35f
        };
    }

    private static BossActor FindTargetBoss(Level level)
    {
        BossActor best = null;
        float bestDistSq = float.MaxValue;

        foreach (BossActor boss in level.Tracker.GetEntities<BossActor>())
        {
            if (boss == null || boss.Scene != level || !boss.Collidable || boss.IsDefeated)
            {
                continue;
            }

            float distSq = Vector2.DistanceSquared(level.Camera.Position + new Vector2(160f, 90f), boss.Center);
            if (distSq < bestDistSq)
            {
                best = boss;
                bestDistSq = distSq;
            }
        }

        return best;
    }

    private static bool IsTrueFinalBossContext(Level level)
    {
        if (level?.Tracker.GetEntity<ElsTrueFinalBoss>() != null)
        {
            return true;
        }

        MaggyHelperModuleSession session = MaggyHelperModule.Session;
        if (session?.BossFightActive != true)
        {
            return false;
        }

        string bossName = session.CurrentBossName ?? string.Empty;
        if (bossName.Length == 0)
        {
            return false;
        }

        string lowered = bossName.ToLowerInvariant();
        return lowered.Contains("els") && (lowered.Contains("true") || lowered.Contains("final") || lowered.Contains("siamo"));
    }

    private sealed class HeartCompanionBolt : Entity
    {
        private const float TurnRate = 9f;

        private BossActor target;
        private Vector2 velocity;
        private readonly Color boltColor;
        private readonly int damage;
        private readonly float speed;

        public HeartCompanionBolt(Vector2 position, BossActor target, Color color, int damage, float speed) : base(position)
        {
            this.target = target;
            boltColor = color;
            this.damage = Math.Max(1, damage);
            this.speed = Math.Max(120f, speed);
            Depth = Depths.Player - 40;
            Collider = new Circle(3f);
            velocity = Vector2.UnitX * this.speed;
        }

        public override void Update()
        {
            base.Update();

            if (Scene is not Level level)
            {
                RemoveSelf();
                return;
            }

            if (target == null || target.Scene != level || target.IsDefeated)
            {
                RemoveSelf();
                return;
            }

            Vector2 toTarget = (target.Center - Position).SafeNormalize();
            Vector2 wanted = toTarget * speed;
            velocity = Calc.Approach(velocity, wanted, speed * TurnRate * Engine.DeltaTime);
            Position += velocity * Engine.DeltaTime;

            if (Vector2.DistanceSquared(Position, target.Center) <= 12f * 12f)
            {
                target.TakeDamage(damage);
                level.ParticlesFG?.Emit(ParticleTypes.SparkyDust, 4, Position, Vector2.One * 3f, boltColor);
                RemoveSelf();
            }
        }

        public override void Render()
        {
            Draw.Circle(Position, 2.5f, boltColor, 10);
        }
    }

    private enum HeartRole
    {
        Leader,
        Guardian,
        Striker,
        Sniper,
        Medic,
        Surge,
        Purifier
    }
}