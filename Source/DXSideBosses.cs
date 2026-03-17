using MaggyHelper.Helpers;

namespace MaggyHelper
{
    #region DX Flowey Omega Boss
    /// <summary>
    /// DX Flowey Omega - Enhanced version of Els Flowey for the DX-Side.
    /// All phases are remixed with faster attacks, new hybrid patterns,
    /// and a unique "Corruption Overload" finale phase.
    /// Inspired by Desolo Zantas content.
    /// </summary>
    [Tracked]
    [HotReloadable]
    [CustomEntity("MaggyHelper/DXFloweyOmegaBoss")]
    public class DXFloweyOmegaBoss : BossActor
    {
        #region Phases & Attacks
        public enum BossPhase
        {
            Intro,
            CorruptedGarden,        // Remixed OrganGarden - vines + bone hybrid
            AbyssalCathedral,       // Remixed BoneCathedral - gravity-shifting bones
            NightmareNexus,         // Remixed FleshLabyrinth - teleporting flesh walls
            ArsenalOverdrive,       // Remixed WeaponArsenal - dual-wielding weapons
            SoulHarvest,            // Remixed SoulCollection - souls fight back
            CorruptionOverload,     // NEW DX-exclusive finale phase
            Defeated,
        }

        public enum AttackType
        {
            // Corrupted Garden attacks
            ToxicVineBarrage,
            CorruptedBlossom,
            RootCageSlam,
            // Abyssal Cathedral attacks
            GravityBoneStorm,
            SkullCrusher,
            BoneLabyrinthWall,
            // Nightmare Nexus attacks
            FleshTeleport,
            NerveNetShock,
            BloodTidalWave,
            // Arsenal Overdrive attacks
            DualBladeStorm,
            ChainLightning,
            MissileBarrage,
            // Soul Harvest attacks
            SoulDrain,
            RevenantSwarm,
            DespairPulse,
            // Corruption Overload attacks (DX exclusive)
            CorruptionBeam,
            RealityShatter,
            VoidMaelstrom,
            OmegaFlare,
        }
        #endregion

        #region Properties
        public BossPhase CurrentPhase { get; private set; }
        public bool IsVulnerable { get; private set; }
        public int SoulsHarvested { get; private set; }
        public float CorruptionMeter { get; private set; }

        private global::Celeste.Player player;
        private global::Celeste.Level level;
        private Camera camera;

        private Vector2 basePosition;
#pragma warning disable CS0414
        private string currentAnimation;
#pragma warning restore CS0414

        // Attack state
        private List<AttackType> currentAttackPattern;
        private int attackIndex;
        private float attackCooldown;
        private float phaseTimer;

        // DX-exclusive: Corruption Overload mechanics
        private float corruptionLevel;
        private float maxCorruption;
        private bool corruptionOverloading;
        private List<DXCorruptionTendril> activeTendrils;
        private List<DXVoidOrb> activeVoidOrbs;

        // Sub-entities
        private List<Entity> activeProjectiles;

        // Audio
        private const string MUSIC_DX_FLOWEY_PHASE1 = "event:/desolozantas/dx_content/music/dx_flowey_phase1";
        private const string MUSIC_DX_FLOWEY_PHASE2 = "event:/desolozantas/dx_content/music/dx_flowey_overdrive";
        private const string MUSIC_DX_FLOWEY_FINALE = "event:/desolozantas/dx_content/music/dx_flowey_corruption_overload";

        #endregion

        #region Constructors
        public DXFloweyOmegaBoss(EntityData data, Vector2 offset)
            : base(data.Position + offset,
                   spriteName: "characters/dx_flowey_omega/flowey",
                   spriteScale: Vector2.One,
                   maxFall: 160f,
                   collidable: true,
                   solidCollidable: true,
                   gravityMult: 1.0f,
                   collider: new Hitbox(48, 64, -24, -64))
        {
            MaxHealth = data.Int("maxHealth", 1500);
            Health = MaxHealth;
            corruptionLevel = data.Float("corruptionLevel", 0f);
            maxCorruption = data.Float("maxCorruption", 100f);
            Initialize();
        }

        public DXFloweyOmegaBoss(Vector2 position)
            : base(position,
                   spriteName: "characters/dx_flowey_omega/flowey",
                   spriteScale: Vector2.One,
                   maxFall: 160f,
                   collidable: true,
                   solidCollidable: true,
                   gravityMult: 1.0f,
                   collider: new Hitbox(48, 64, -24, -64))
        {
            MaxHealth = 1500;
            Health = MaxHealth;
            Initialize();
        }

        private void Initialize()
        {
            CurrentPhase = BossPhase.Intro;
            IsVulnerable = false;
            SoulsHarvested = 0;
            CorruptionMeter = 0f;
            corruptionOverloading = false;
            maxCorruption = 100f;
            phaseTimer = 0f;
            attackIndex = 0;
            attackCooldown = 0f;

            basePosition = Position;

            activeTendrils = new List<DXCorruptionTendril>();
            activeVoidOrbs = new List<DXVoidOrb>();
            activeProjectiles = new List<Entity>();

            currentAttackPattern = new List<AttackType>();

            SetupSprite();
            Add(new PlayerCollider(OnPlayerCollision));
            Add(new Coroutine(BossRoutine()));
        }
        #endregion

        #region Sprite Setup
        private void SetupSprite()
        {
            if (Sprite != null)
            {
                if (!Sprite.Has("idle")) Sprite.Add("idle", "idle", 0.1f);
                if (!Sprite.Has("attacking")) Sprite.Add("attacking", "attack", 0.06f);
                if (!Sprite.Has("corrupting")) Sprite.Add("corrupting", "corrupt", 0.08f);
                if (!Sprite.Has("overload")) Sprite.Add("overload", "overload", 0.05f);
                if (!Sprite.Has("vulnerable")) Sprite.Add("vulnerable", "vulnerable", 0.15f);
                if (!Sprite.Has("dying")) Sprite.Add("dying", "dying", 0.2f);

                Sprite.Play("idle");
                Sprite.CenterOrigin();
            }
            currentAnimation = "idle";
        }
        #endregion

        #region Main Boss Routine
        private IEnumerator BossRoutine()
        {
            // Wait until player is nearby
            while (player == null)
            {
                player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                yield return null;
            }
            level = SceneAs<global::Celeste.Level>();
            camera = level?.Camera;

            while (CurrentPhase != BossPhase.Defeated)
            {
                switch (CurrentPhase)
                {
                    case BossPhase.Intro:
                        yield return IntroPhase();
                        break;
                    case BossPhase.CorruptedGarden:
                        yield return CorruptedGardenPhase();
                        break;
                    case BossPhase.AbyssalCathedral:
                        yield return AbyssalCathedralPhase();
                        break;
                    case BossPhase.NightmareNexus:
                        yield return NightmareNexusPhase();
                        break;
                    case BossPhase.ArsenalOverdrive:
                        yield return ArsenalOverdrivePhase();
                        break;
                    case BossPhase.SoulHarvest:
                        yield return SoulHarvestPhase();
                        break;
                    case BossPhase.CorruptionOverload:
                        yield return CorruptionOverloadPhase();
                        break;
                }
                yield return null;
            }

            yield return DefeatSequence();
        }
        #endregion

        #region Phase Implementations
        private IEnumerator IntroPhase()
        {
            // Dramatic entrance — screen shake + music start
            if (level != null)
            {
                Audio.SetMusic(MUSIC_DX_FLOWEY_PHASE1);
                level.Shake(0.5f);
            }
            yield return 2.0f;
            CurrentPhase = BossPhase.CorruptedGarden;
        }

        private IEnumerator CorruptedGardenPhase()
        {
            IsVulnerable = true;
            currentAttackPattern = new List<AttackType>
            {
                AttackType.ToxicVineBarrage,
                AttackType.CorruptedBlossom,
                AttackType.RootCageSlam,
            };

            while (Health > MaxHealth * 0.80f && CurrentPhase == BossPhase.CorruptedGarden)
            {
                yield return ExecuteAttackPattern();
                IncrementCorruption(5f);
            }

            CurrentPhase = BossPhase.AbyssalCathedral;
        }

        private IEnumerator AbyssalCathedralPhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.GravityBoneStorm,
                AttackType.SkullCrusher,
                AttackType.BoneLabyrinthWall,
            };

            if (level != null)
            {
                level.Shake(0.3f);
                Audio.SetMusic(MUSIC_DX_FLOWEY_PHASE2);
            }

            while (Health > MaxHealth * 0.60f && CurrentPhase == BossPhase.AbyssalCathedral)
            {
                yield return ExecuteAttackPattern();
                IncrementCorruption(8f);
            }

            CurrentPhase = BossPhase.NightmareNexus;
        }

        private IEnumerator NightmareNexusPhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.FleshTeleport,
                AttackType.NerveNetShock,
                AttackType.BloodTidalWave,
            };

            while (Health > MaxHealth * 0.40f && CurrentPhase == BossPhase.NightmareNexus)
            {
                yield return ExecuteAttackPattern();
                // Teleport to random position
                if (Calc.Random.Chance(0.3f))
                {
                    Vector2 teleportTarget = basePosition + new Vector2(
                        Calc.Random.Range(-120f, 120f),
                        Calc.Random.Range(-80f, 80f));
                    Position = teleportTarget;
                }
                IncrementCorruption(10f);
            }

            CurrentPhase = BossPhase.ArsenalOverdrive;
        }

        private IEnumerator ArsenalOverdrivePhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.DualBladeStorm,
                AttackType.ChainLightning,
                AttackType.MissileBarrage,
            };

            while (Health > MaxHealth * 0.25f && CurrentPhase == BossPhase.ArsenalOverdrive)
            {
                yield return ExecuteAttackPattern();
                IncrementCorruption(12f);
            }

            CurrentPhase = BossPhase.SoulHarvest;
        }

        private IEnumerator SoulHarvestPhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.SoulDrain,
                AttackType.RevenantSwarm,
                AttackType.DespairPulse,
            };

            while (Health > MaxHealth * 0.10f && CurrentPhase == BossPhase.SoulHarvest)
            {
                yield return ExecuteAttackPattern();
                IncrementCorruption(15f);
            }

            CurrentPhase = BossPhase.CorruptionOverload;
        }

        /// <summary>
        /// DX-Exclusive finale — Corruption Overload.
        /// The boss goes berserk as corruption reaches maximum.
        /// Screen distortion, rapid multi-pattern attacks, void orbs.
        /// </summary>
        private IEnumerator CorruptionOverloadPhase()
        {
            corruptionOverloading = true;
            CorruptionMeter = maxCorruption;

            if (level != null)
            {
                Audio.SetMusic(MUSIC_DX_FLOWEY_FINALE);
                level.Shake(1.0f);
            }

            currentAttackPattern = new List<AttackType>
            {
                AttackType.CorruptionBeam,
                AttackType.RealityShatter,
                AttackType.VoidMaelstrom,
                AttackType.OmegaFlare,
            };

            // Spawn void orbs around the arena
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi / 6 * i;
                Vector2 orbPos = basePosition + Calc.AngleToVector(angle, 180f);
                var orb = new DXVoidOrb(orbPos, this);
                activeVoidOrbs.Add(orb);
                Scene?.Add(orb);
            }

            // Spawn corruption tendrils
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi / 4 * i;
                Vector2 tendrilPos = Position + Calc.AngleToVector(angle, 40f);
                var tendril = new DXCorruptionTendril(tendrilPos, Calc.AngleToVector(angle, 1f));
                activeTendrils.Add(tendril);
                Scene?.Add(tendril);
            }

            IsVulnerable = true;

            while (Health > 0 && CurrentPhase == BossPhase.CorruptionOverload)
            {
                yield return ExecuteAttackPattern();

                // Rapidly cycle through all attack types
                if (Calc.Random.Chance(0.4f))
                {
                    yield return ExecuteRandomAttack();
                }
            }

            CurrentPhase = BossPhase.Defeated;
        }

        private IEnumerator DefeatSequence()
        {
            IsVulnerable = false;
            corruptionOverloading = false;

            // Clean up sub-entities
            foreach (var tendril in activeTendrils) tendril.RemoveSelf();
            foreach (var orb in activeVoidOrbs) orb.RemoveSelf();
            foreach (var proj in activeProjectiles) proj.RemoveSelf();
            activeTendrils.Clear();
            activeVoidOrbs.Clear();
            activeProjectiles.Clear();

            if (Sprite != null && Sprite.Has("dying"))
                Sprite.Play("dying");

            if (level != null)
            {
                level.Shake(1.5f);
                Audio.SetMusic(null);
            }

            yield return 3.0f;

            // Set session flags for boss defeat tracking
            var lvl = SceneAs<Level>();
            lvl?.Session.SetFlag("DXFloweyOmega_Defeated", true);
            lvl?.Session.SetFlag("DXBossDefeated", true);

            RemoveSelf();
        }
        #endregion

        #region Attack Execution
        private IEnumerator ExecuteAttackPattern()
        {
            if (currentAttackPattern.Count == 0) yield break;

            AttackType attack = currentAttackPattern[attackIndex % currentAttackPattern.Count];
            attackIndex++;

            yield return ExecuteAttack(attack);
            yield return attackCooldown > 0 ? attackCooldown : 0.8f;
        }

        private IEnumerator ExecuteRandomAttack()
        {
            var allAttacks = Enum.GetValues(typeof(AttackType)) as AttackType[];
            if (allAttacks == null || allAttacks.Length == 0) yield break;
            AttackType randomAttack = allAttacks[Calc.Random.Next(allAttacks.Length)];
            yield return ExecuteAttack(randomAttack);
        }

        private IEnumerator ExecuteAttack(AttackType attack)
        {
            if (Sprite != null && Sprite.Has("attacking"))
                Sprite.Play("attacking");

            switch (attack)
            {
                case AttackType.ToxicVineBarrage:
                    yield return SpawnProjectileBurst("vine", 8, 200f, 0.1f);
                    break;
                case AttackType.CorruptedBlossom:
                    yield return SpawnExpandingRing(Color.Purple, 300f, 2.0f);
                    break;
                case AttackType.RootCageSlam:
                    yield return SpawnGroundSlam(4, 100f);
                    break;
                case AttackType.GravityBoneStorm:
                    yield return SpawnProjectileBurst("bone", 12, 250f, 0.08f);
                    break;
                case AttackType.SkullCrusher:
                    yield return SpawnHomingProjectile("skull", 180f, 3.0f);
                    break;
                case AttackType.BoneLabyrinthWall:
                    yield return SpawnWallPattern(6, 80f);
                    break;
                case AttackType.FleshTeleport:
                    yield return TeleportAttack(3);
                    break;
                case AttackType.NerveNetShock:
                    yield return SpawnExpandingRing(Color.Yellow, 250f, 1.5f);
                    break;
                case AttackType.BloodTidalWave:
                    yield return SpawnWaveAttack(Color.DarkRed, 400f);
                    break;
                case AttackType.DualBladeStorm:
                    yield return SpawnProjectileBurst("blade", 16, 300f, 0.05f);
                    break;
                case AttackType.ChainLightning:
                    yield return SpawnLightningChain(5, 150f);
                    break;
                case AttackType.MissileBarrage:
                    yield return SpawnHomingProjectile("missile", 220f, 2.5f);
                    yield return 0.3f;
                    yield return SpawnHomingProjectile("missile", 220f, 2.5f);
                    yield return 0.3f;
                    yield return SpawnHomingProjectile("missile", 220f, 2.5f);
                    break;
                case AttackType.SoulDrain:
                    yield return SpawnDrainBeam(2.0f);
                    break;
                case AttackType.RevenantSwarm:
                    yield return SpawnProjectileBurst("revenant", 10, 180f, 0.12f);
                    break;
                case AttackType.DespairPulse:
                    yield return SpawnExpandingRing(Color.DarkMagenta, 350f, 2.5f);
                    yield return 0.5f;
                    yield return SpawnExpandingRing(Color.DarkMagenta, 350f, 2.5f);
                    break;
                case AttackType.CorruptionBeam:
                    yield return SpawnCorruptionBeam();
                    break;
                case AttackType.RealityShatter:
                    yield return SpawnRealityShatter();
                    break;
                case AttackType.VoidMaelstrom:
                    yield return SpawnVoidMaelstrom();
                    break;
                case AttackType.OmegaFlare:
                    yield return SpawnOmegaFlare();
                    break;
            }

            if (Sprite != null && Sprite.Has("idle"))
                Sprite.Play("idle");
        }
        #endregion

        #region Attack Helpers
        private IEnumerator SpawnProjectileBurst(string type, int count, float speed, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi / count * i;
                Vector2 dir = Calc.AngleToVector(angle, 1f);
                var proj = new DXBossProjectile(Position, dir * speed, type);
                activeProjectiles.Add(proj);
                Scene?.Add(proj);
                yield return delay;
            }
        }

        private IEnumerator SpawnExpandingRing(Color color, float maxRadius, float duration)
        {
            var ring = new DXExpandingRing(Position, maxRadius, duration, color);
            activeProjectiles.Add(ring);
            Scene?.Add(ring);
            yield return duration;
        }

        private IEnumerator SpawnGroundSlam(int pillars, float spacing)
        {
            if (level == null) yield break;
            for (int i = 0; i < pillars; i++)
            {
                float xOff = (i - pillars / 2f) * spacing;
                Vector2 pillarPos = new Vector2(Position.X + xOff, level.Bounds.Bottom - 8);
                var pillar = new DXGroundPillar(pillarPos, 200f, 1.5f);
                activeProjectiles.Add(pillar);
                Scene?.Add(pillar);
                yield return 0.15f;
            }
            level.Shake(0.3f);
        }

        private IEnumerator SpawnHomingProjectile(string type, float speed, float lifetime)
        {
            if (player == null) yield break;
            Vector2 dir = (player.Position - Position).SafeNormalize();
            var proj = new DXHomingProjectile(Position, dir * speed, player, type, lifetime);
            activeProjectiles.Add(proj);
            Scene?.Add(proj);
            yield return null;
        }

        private IEnumerator SpawnWallPattern(int walls, float spacing)
        {
            for (int i = 0; i < walls; i++)
            {
                bool fromLeft = i % 2 == 0;
                float yOffset = i * spacing;
                var wall = new DXProjectileWall(
                    new Vector2(fromLeft ? Position.X - 200 : Position.X + 200, Position.Y + yOffset),
                    fromLeft ? Vector2.UnitX * 150f : -Vector2.UnitX * 150f,
                    200f);
                activeProjectiles.Add(wall);
                Scene?.Add(wall);
                yield return 0.3f;
            }
        }

        private IEnumerator TeleportAttack(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Vanish
                Visible = false;
                Collidable = false;
                yield return 0.3f;

                // Reappear near player
                if (player != null)
                {
                    Position = player.Position + new Vector2(
                        Calc.Random.Range(-80f, 80f),
                        Calc.Random.Range(-60f, 20f));
                }
                Visible = true;
                Collidable = true;

                // Quick attack burst
                yield return SpawnProjectileBurst("flesh", 6, 250f, 0.05f);
                yield return 0.5f;
            }
        }

        private IEnumerator SpawnLightningChain(int bolts, float range)
        {
            if (player == null) yield break;
            Vector2 target = player.Position;
            for (int i = 0; i < bolts; i++)
            {
                Vector2 boltPos = target + new Vector2(Calc.Random.Range(-range, range), -200);
                var bolt = new DXLightningBolt(boltPos, 400f, 0.8f);
                activeProjectiles.Add(bolt);
                Scene?.Add(bolt);
                yield return 0.2f;
            }
        }

        private IEnumerator SpawnWaveAttack(Color color, float width)
        {
            if (level == null) yield break;
            var wave = new DXWaveAttack(
                new Vector2(Position.X - width / 2, Position.Y),
                width, 120f, color);
            activeProjectiles.Add(wave);
            Scene?.Add(wave);
            yield return 2.0f;
        }

        private IEnumerator SpawnDrainBeam(float duration)
        {
            if (player == null) yield break;
            var beam = new DXDrainBeam(Position, player, duration);
            activeProjectiles.Add(beam);
            Scene?.Add(beam);
            yield return duration;
        }

        // DX-Exclusive Corruption Overload attacks
        private IEnumerator SpawnCorruptionBeam()
        {
            if (player == null) yield break;
            Vector2 dir = (player.Position - Position).SafeNormalize();
            var beam = new DXCorruptionBeam(Position, dir, 500f, 1.5f);
            activeProjectiles.Add(beam);
            Scene?.Add(beam);
            level?.Shake(0.5f);
            yield return 1.5f;
        }

        private IEnumerator SpawnRealityShatter()
        {
            // Create cracks in multiple positions
            for (int i = 0; i < 5; i++)
            {
                Vector2 crackPos = basePosition + new Vector2(
                    Calc.Random.Range(-200f, 200f),
                    Calc.Random.Range(-150f, 150f));
                var crack = new DXRealityCrack(crackPos, 2.0f);
                activeProjectiles.Add(crack);
                Scene?.Add(crack);
                yield return 0.2f;
            }
            level?.Shake(0.8f);
            yield return 1.5f;
        }

        private IEnumerator SpawnVoidMaelstrom()
        {
            // Spinning vortex of projectiles
            float duration = 3.0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float angle = elapsed * MathHelper.TwoPi * 2f;
                Vector2 dir = Calc.AngleToVector(angle, 1f);
                var proj = new DXBossProjectile(Position, dir * 200f, "void");
                activeProjectiles.Add(proj);
                Scene?.Add(proj);
                elapsed += Engine.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator SpawnOmegaFlare()
        {
            // Massive screen-filling attack
            IsVulnerable = false;
            if (Sprite != null && Sprite.Has("overload"))
                Sprite.Play("overload");

            yield return 1.0f; // Charge time

            // Radial burst from all directions
            for (int wave = 0; wave < 3; wave++)
            {
                yield return SpawnProjectileBurst("omega", 24, 350f, 0.02f);
                yield return 0.5f;
            }
            level?.Shake(1.0f);
            IsVulnerable = true;
            yield return 1.0f;
        }
        #endregion

        #region Corruption System
        private void IncrementCorruption(float amount)
        {
            CorruptionMeter = Math.Min(CorruptionMeter + amount, maxCorruption);
        }
        #endregion

        #region Collision & Update
        private void OnPlayerCollision(global::Celeste.Player player)
        {
            if (player.DashAttacking && IsVulnerable)
            {
                Health -= 25;
                level?.Shake(0.2f);
                Audio.Play("event:/game/general/thing_booped", Position);
            }
            else
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
        }

        public override void Update()
        {
            base.Update();
            player = Scene?.Tracker.GetEntity<global::Celeste.Player>();

            // Clean up dead projectiles
            activeProjectiles.RemoveAll(p => p.Scene == null);
            activeTendrils.RemoveAll(t => t.Scene == null);
            activeVoidOrbs.RemoveAll(o => o.Scene == null);
        }

        public override void Render()
        {
            base.Render();

            // Draw health bar
            if (CurrentPhase != BossPhase.Defeated && CurrentPhase != BossPhase.Intro)
            {
                float healthPercent = (float)Health / MaxHealth;
                Vector2 barPos = Position + new Vector2(-40, -80);
                Draw.Rect(barPos, 80, 6, Color.DarkGray);
                Draw.Rect(barPos, 80 * healthPercent, 6,
                    corruptionOverloading ? Color.Magenta : Color.Red);
                Draw.HollowRect(barPos, 80, 6, Color.White);

                // Corruption meter
                if (CorruptionMeter > 0)
                {
                    float corruptPercent = CorruptionMeter / maxCorruption;
                    Vector2 corruptBarPos = Position + new Vector2(-40, -72);
                    Draw.Rect(corruptBarPos, 80 * corruptPercent, 4, Color.Purple);
                    Draw.HollowRect(corruptBarPos, 80, 4, Color.DarkMagenta);
                }
            }
        }
        #endregion
    }
    #endregion

    #region DX Asriel Transcendence Boss
    /// <summary>
    /// DX Asriel Transcendence - Enhanced ultimate version of Asriel for the DX-Side.
    /// Features new Transcendence phase beyond the original fight,
    /// with cosmic attacks, reality manipulation, and a redemption-through-combat mechanic.
    /// </summary>
    [Tracked]
    [HotReloadable]
    [CustomEntity("MaggyHelper/DXAsrielTranscendenceBoss")]
    public class DXAsrielTranscendenceBoss : BossActor
    {
        #region Phases & Attacks
        public enum BossPhase
        {
            Dormant,
            CosmicAwakening,       // DX intro — Asriel emerges from a dimensional rift
            DivineFury,            // Remixed Phase1 — faster, more complex patterns
            AstralStorm,           // DX-exclusive storm phase with gravity manipulation
            TranscendenceRift,     // DX-exclusive — Asriel tears reality, multi-dimensional attacks
            SoulConvergence,       // Remixed LostSouls — souls are corrupted, must purify
            CosmicJudgment,        // DX-exclusive finale — ultimate cosmic attacks
            Redemption,
            Defeated,
        }

        public enum AttackType
        {
            // Cosmic Awakening
            StarBurst,
            NebulaSweep,
            GravityWell,
            // Divine Fury
            UltimaBarrage,
            CrossShocker,
            DivineLightning,
            CosmicSweepDX,
            // Astral Storm
            MeteorShower,
            GravityReverse,
            AstralChainBind,
            // Transcendence Rift
            DimensionalSlash,
            RealityMirror,
            VoidGate,
            TimeDistortion,
            // Soul Convergence
            CorruptedSoulBeam,
            PurificationBlast,
            SoulTether,
            // Cosmic Judgment (DX exclusive)
            HyperGigaNova,
            InfinityStrike,
            CosmicApocalypse,
            TranscendenceFlare,
        }
        #endregion

        #region Properties
        public BossPhase CurrentPhase { get; private set; }
        public bool IsVulnerable { get; private set; }
        public int SoulsPurified { get; private set; }
        public float TranscendenceLevel { get; private set; }

        private const int TOTAL_CORRUPTED_SOULS = 8;

        private global::Celeste.Player player;
        private global::Celeste.Level level;
        private Camera camera;

        private Vector2 basePosition;
        private string currentAnimation;

        // Multi-part sprites
        private Sprite cosmicWingSprite;
        private Sprite haloSprite;

        // Attack state
        private List<AttackType> currentAttackPattern;
        private int attackIndex;
        private float attackCooldown;

        // DX mechanics
        private float transcendenceEnergy;
        private float maxTranscendenceEnergy;
        private bool isTranscendent;
        private List<DXDimensionalRift> activeRifts;
        private List<Entity> activeProjectiles;

        // Soul tracking
        private Dictionary<string, bool> soulsPurifiedMap;

        // Audio
        private const string MUSIC_DX_ASRIEL_AWAKENING = "event:/desolozantas/dx_content/music/dx_asriel_awakening";
        private const string MUSIC_DX_ASRIEL_FURY = "event:/desolozantas/dx_content/music/dx_asriel_divine_fury";
        private const string MUSIC_DX_ASRIEL_TRANSCENDENCE = "event:/desolozantas/dx_content/music/dx_asriel_transcendence";
        private const string MUSIC_DX_ASRIEL_JUDGMENT = "event:/desolozantas/dx_content/music/dx_asriel_cosmic_judgment";

        #endregion

        #region Constructors
        public DXAsrielTranscendenceBoss(EntityData data, Vector2 offset)
            : base(data.Position + offset,
                   spriteName: "asriel_dx_transcendence",
                   spriteScale: Vector2.One,
                   maxFall: 0f,
                   collidable: true,
                   solidCollidable: false,
                   gravityMult: 0f,
                   collider: new Hitbox(160, 224, -80, -224))
        {
            MaxHealth = data.Int("maxHealth", 3500);
            Health = MaxHealth;
            maxTranscendenceEnergy = data.Float("maxTranscendence", 100f);
            Initialize();
        }

        public DXAsrielTranscendenceBoss(Vector2 position)
            : base(position,
                   spriteName: "asriel_dx_transcendence",
                   spriteScale: Vector2.One,
                   maxFall: 0f,
                   collidable: true,
                   solidCollidable: false,
                   gravityMult: 0f,
                   collider: new Hitbox(160, 224, -80, -224))
        {
            MaxHealth = 3500;
            Health = MaxHealth;
            maxTranscendenceEnergy = 100f;
            Initialize();
        }

        private void Initialize()
        {
            CurrentPhase = BossPhase.Dormant;
            IsVulnerable = false;
            SoulsPurified = 0;
            TranscendenceLevel = 0f;
            transcendenceEnergy = 0f;
            isTranscendent = false;

            basePosition = Position;
            attackIndex = 0;

            activeRifts = new List<DXDimensionalRift>();
            activeProjectiles = new List<Entity>();
            currentAttackPattern = new List<AttackType>();

            InitializeSoulMap();
            SetupSpriteComponents();

            Add(new PlayerCollider(OnPlayerCollision));
            Add(new Coroutine(BossRoutine()));
        }
        #endregion

        #region Setup
        private void SetupSpriteComponents()
        {
            if (Sprite != null)
            {
                if (!Sprite.Has("idle")) Sprite.Add("idle", "idle", 0.1f);
                if (!Sprite.Has("awakening")) Sprite.Add("awakening", "awaken", 0.08f);
                if (!Sprite.Has("attacking")) Sprite.Add("attacking", "attack", 0.06f);
                if (!Sprite.Has("transcendent")) Sprite.Add("transcendent", "transcend", 0.05f);
                if (!Sprite.Has("judgment")) Sprite.Add("judgment", "judgment", 0.04f);
                if (!Sprite.Has("redemption")) Sprite.Add("redemption", "redeem", 0.12f);

                Sprite.Play("idle");
                Sprite.CenterOrigin();
            }

            // Create additional cosmic effect layers
            try
            {
                cosmicWingSprite = new Sprite(GFX.Game, "characters/dx_asriel_transcendence/wings/");
                cosmicWingSprite.AddLoop("idle", "", 0.1f);
                cosmicWingSprite.CenterOrigin();
                Add(cosmicWingSprite);
            }
            catch { cosmicWingSprite = null; }

            try
            {
                haloSprite = new Sprite(GFX.Game, "characters/dx_asriel_transcendence/halo/");
                haloSprite.AddLoop("idle", "", 0.08f);
                haloSprite.CenterOrigin();
                Add(haloSprite);
            }
            catch { haloSprite = null; }

            currentAnimation = "idle";
        }

        private void InitializeSoulMap()
        {
            soulsPurifiedMap = new Dictionary<string, bool>
            {
                { "magolor", false },
                { "chara", false },
                { "theo", false },
                { "toriel", false },
                { "papyrus", false },
                { "undyne", false },
                { "ralsei", false },
                { "starsei", false },
            };
        }
        #endregion

        #region Main Boss Routine
        private IEnumerator BossRoutine()
        {
            while (player == null)
            {
                player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                yield return null;
            }
            level = SceneAs<global::Celeste.Level>();
            camera = level?.Camera;

            while (CurrentPhase != BossPhase.Defeated)
            {
                switch (CurrentPhase)
                {
                    case BossPhase.Dormant:
                        yield return DormantPhase();
                        break;
                    case BossPhase.CosmicAwakening:
                        yield return CosmicAwakeningPhase();
                        break;
                    case BossPhase.DivineFury:
                        yield return DivineFuryPhase();
                        break;
                    case BossPhase.AstralStorm:
                        yield return AstralStormPhase();
                        break;
                    case BossPhase.TranscendenceRift:
                        yield return TranscendenceRiftPhase();
                        break;
                    case BossPhase.SoulConvergence:
                        yield return SoulConvergencePhase();
                        break;
                    case BossPhase.CosmicJudgment:
                        yield return CosmicJudgmentPhase();
                        break;
                    case BossPhase.Redemption:
                        yield return RedemptionPhase();
                        break;
                }
                yield return null;
            }
        }
        #endregion

        #region Phase Implementations
        private IEnumerator DormantPhase()
        {
            // Wait for player to approach
            while (player != null && Vector2.Distance(player.Position, Position) > 200f)
                yield return null;

            CurrentPhase = BossPhase.CosmicAwakening;
        }

        private IEnumerator CosmicAwakeningPhase()
        {
            Audio.SetMusic(MUSIC_DX_ASRIEL_AWAKENING);
            if (Sprite != null && Sprite.Has("awakening"))
                Sprite.Play("awakening");

            level?.Shake(0.8f);
            yield return 3.0f;

            CurrentPhase = BossPhase.DivineFury;
        }

        private IEnumerator DivineFuryPhase()
        {
            Audio.SetMusic(MUSIC_DX_ASRIEL_FURY);
            IsVulnerable = true;

            currentAttackPattern = new List<AttackType>
            {
                AttackType.UltimaBarrage,
                AttackType.CrossShocker,
                AttackType.DivineLightning,
                AttackType.CosmicSweepDX,
            };

            while (Health > MaxHealth * 0.70f && CurrentPhase == BossPhase.DivineFury)
            {
                yield return ExecuteAttackPattern();
                transcendenceEnergy += 5f;
            }

            CurrentPhase = BossPhase.AstralStorm;
        }

        private IEnumerator AstralStormPhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.MeteorShower,
                AttackType.GravityReverse,
                AttackType.AstralChainBind,
            };

            while (Health > MaxHealth * 0.50f && CurrentPhase == BossPhase.AstralStorm)
            {
                yield return ExecuteAttackPattern();

                // Gravity fluctuations
                if (Calc.Random.Chance(0.2f) && level != null)
                {
                    level.Shake(0.4f);
                }
                transcendenceEnergy += 8f;
            }

            CurrentPhase = BossPhase.TranscendenceRift;
        }

        private IEnumerator TranscendenceRiftPhase()
        {
            Audio.SetMusic(MUSIC_DX_ASRIEL_TRANSCENDENCE);
            isTranscendent = true;

            if (Sprite != null && Sprite.Has("transcendent"))
                Sprite.Play("transcendent");

            // Open dimensional rifts
            for (int i = 0; i < 3; i++)
            {
                Vector2 riftPos = basePosition + new Vector2(
                    Calc.Random.Range(-200f, 200f),
                    Calc.Random.Range(-150f, 150f));
                var rift = new DXDimensionalRift(riftPos, 3.0f + i);
                activeRifts.Add(rift);
                Scene?.Add(rift);
            }

            currentAttackPattern = new List<AttackType>
            {
                AttackType.DimensionalSlash,
                AttackType.RealityMirror,
                AttackType.VoidGate,
                AttackType.TimeDistortion,
            };

            while (Health > MaxHealth * 0.30f && CurrentPhase == BossPhase.TranscendenceRift)
            {
                yield return ExecuteAttackPattern();
                transcendenceEnergy += 10f;
            }

            // Close rifts
            foreach (var rift in activeRifts) rift.RemoveSelf();
            activeRifts.Clear();

            CurrentPhase = BossPhase.SoulConvergence;
        }

        private IEnumerator SoulConvergencePhase()
        {
            currentAttackPattern = new List<AttackType>
            {
                AttackType.CorruptedSoulBeam,
                AttackType.PurificationBlast,
                AttackType.SoulTether,
            };

            // Spawn corrupted souls for the player to purify
            int soulIndex = 0;
            foreach (var kvp in soulsPurifiedMap)
            {
                float angle = MathHelper.TwoPi / TOTAL_CORRUPTED_SOULS * soulIndex;
                Vector2 soulPos = basePosition + Calc.AngleToVector(angle, 200f);
                var soul = new DXCorruptedSoul(soulPos, kvp.Key, this);
                Scene?.Add(soul);
                soulIndex++;
            }

            while (Health > MaxHealth * 0.15f && CurrentPhase == BossPhase.SoulConvergence)
            {
                yield return ExecuteAttackPattern();
            }

            CurrentPhase = BossPhase.CosmicJudgment;
        }

        /// <summary>
        /// DX-Exclusive Finale — Cosmic Judgment.
        /// Asriel channels all transcendence energy into devastating cosmic attacks.
        /// </summary>
        private IEnumerator CosmicJudgmentPhase()
        {
            Audio.SetMusic(MUSIC_DX_ASRIEL_JUDGMENT);
            transcendenceEnergy = maxTranscendenceEnergy;

            if (Sprite != null && Sprite.Has("judgment"))
                Sprite.Play("judgment");

            level?.Shake(1.2f);

            currentAttackPattern = new List<AttackType>
            {
                AttackType.HyperGigaNova,
                AttackType.InfinityStrike,
                AttackType.CosmicApocalypse,
                AttackType.TranscendenceFlare,
            };

            while (Health > 0 && CurrentPhase == BossPhase.CosmicJudgment)
            {
                yield return ExecuteAttackPattern();

                // Random transcendence bursts
                if (Calc.Random.Chance(0.3f))
                {
                    yield return SpawnProjectileBurst("cosmic", 20, 300f, 0.03f);
                    level?.Shake(0.6f);
                }
            }

            CurrentPhase = BossPhase.Redemption;
        }

        private IEnumerator RedemptionPhase()
        {
            IsVulnerable = false;
            isTranscendent = false;

            if (Sprite != null && Sprite.Has("redemption"))
                Sprite.Play("redemption");

            Audio.SetMusic(null);
            yield return 4.0f;

            // Set session flags for boss defeat tracking
            {
                var lvl = SceneAs<Level>();
                lvl?.Session.SetFlag("DXAsrielTranscendence_Defeated", true);
                lvl?.Session.SetFlag("DXBossDefeated", true);
            }

            CurrentPhase = BossPhase.Defeated;

            // Clean up
            foreach (var rift in activeRifts) rift.RemoveSelf();
            foreach (var proj in activeProjectiles) proj.RemoveSelf();
            activeRifts.Clear();
            activeProjectiles.Clear();

            RemoveSelf();
        }
        #endregion

        #region Attack Execution
        private IEnumerator ExecuteAttackPattern()
        {
            if (currentAttackPattern.Count == 0) yield break;
            AttackType attack = currentAttackPattern[attackIndex % currentAttackPattern.Count];
            attackIndex++;
            yield return ExecuteAttack(attack);
            yield return 0.6f;
        }

        private IEnumerator ExecuteAttack(AttackType attack)
        {
            if (Sprite != null && Sprite.Has("attacking"))
                Sprite.Play("attacking");

            switch (attack)
            {
                case AttackType.StarBurst:
                case AttackType.UltimaBarrage:
                    yield return SpawnProjectileBurst("star", 12, 250f, 0.06f);
                    break;
                case AttackType.NebulaSweep:
                case AttackType.CosmicSweepDX:
                    yield return SpawnSweepAttack(400f, 2.0f);
                    break;
                case AttackType.GravityWell:
                case AttackType.GravityReverse:
                    yield return SpawnGravityWell(2.5f);
                    break;
                case AttackType.CrossShocker:
                    yield return SpawnCrossPattern(4, 300f);
                    break;
                case AttackType.DivineLightning:
                    yield return SpawnLightningStorm(8, 2.0f);
                    break;
                case AttackType.MeteorShower:
                    yield return SpawnMeteorShower(10, 3.0f);
                    break;
                case AttackType.AstralChainBind:
                    yield return SpawnChainBind(2.0f);
                    break;
                case AttackType.DimensionalSlash:
                    yield return SpawnDimensionalSlash();
                    break;
                case AttackType.RealityMirror:
                    yield return SpawnRealityMirror();
                    break;
                case AttackType.VoidGate:
                    yield return SpawnVoidGate();
                    break;
                case AttackType.TimeDistortion:
                    yield return SpawnTimeDistortion(2.0f);
                    break;
                case AttackType.CorruptedSoulBeam:
                    yield return SpawnProjectileBurst("soul_corrupt", 8, 200f, 0.1f);
                    break;
                case AttackType.PurificationBlast:
                    yield return SpawnExpandingRing(Color.Gold, 300f, 2.0f);
                    break;
                case AttackType.SoulTether:
                    yield return SpawnSoulTether(3.0f);
                    break;
                case AttackType.HyperGigaNova:
                    yield return SpawnHyperGigaNova();
                    break;
                case AttackType.InfinityStrike:
                    yield return SpawnInfinityStrike();
                    break;
                case AttackType.CosmicApocalypse:
                    yield return SpawnCosmicApocalypse();
                    break;
                case AttackType.TranscendenceFlare:
                    yield return SpawnTranscendenceFlare();
                    break;
            }
        }
        #endregion

        #region Attack Helpers
        private IEnumerator SpawnProjectileBurst(string type, int count, float speed, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi / count * i;
                var proj = new DXBossProjectile(Position, Calc.AngleToVector(angle, speed), type);
                activeProjectiles.Add(proj);
                Scene?.Add(proj);
                yield return delay;
            }
        }

        private IEnumerator SpawnExpandingRing(Color color, float maxRadius, float duration)
        {
            var ring = new DXExpandingRing(Position, maxRadius, duration, color);
            activeProjectiles.Add(ring);
            Scene?.Add(ring);
            yield return duration;
        }

        private IEnumerator SpawnSweepAttack(float width, float duration)
        {
            if (player == null) yield break;
            Vector2 dir = (player.Position - Position).SafeNormalize();
            var sweep = new DXCosmicSweep(Position, dir, width, duration);
            activeProjectiles.Add(sweep);
            Scene?.Add(sweep);
            yield return duration;
        }

        private IEnumerator SpawnGravityWell(float duration)
        {
            if (player == null) yield break;
            var well = new DXGravityWell(player.Position, 150f, duration);
            activeProjectiles.Add(well);
            Scene?.Add(well);
            yield return duration;
        }

        private IEnumerator SpawnCrossPattern(int arms, float length)
        {
            for (int i = 0; i < arms; i++)
            {
                float angle = MathHelper.TwoPi / arms * i;
                var beam = new DXCorruptionBeam(Position, Calc.AngleToVector(angle, 1f), length, 1.5f);
                activeProjectiles.Add(beam);
                Scene?.Add(beam);
            }
            yield return 1.5f;
        }

        private IEnumerator SpawnLightningStorm(int bolts, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Vector2 boltPos = basePosition + new Vector2(
                    Calc.Random.Range(-250f, 250f), -300f);
                var bolt = new DXLightningBolt(boltPos, 600f, 0.6f);
                activeProjectiles.Add(bolt);
                Scene?.Add(bolt);
                elapsed += duration / bolts;
                yield return duration / bolts;
            }
        }

        private IEnumerator SpawnMeteorShower(int count, float duration)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPos = new Vector2(
                    basePosition.X + Calc.Random.Range(-300f, 300f),
                    basePosition.Y - 400f);
                Vector2 vel = new Vector2(Calc.Random.Range(-50f, 50f), 300f);
                var meteor = new DXBossProjectile(spawnPos, vel, "meteor");
                activeProjectiles.Add(meteor);
                Scene?.Add(meteor);
                yield return duration / count;
            }
        }

        private IEnumerator SpawnChainBind(float duration)
        {
            if (player == null) yield break;
            var chain = new DXAstralChain(Position, player, duration);
            activeProjectiles.Add(chain);
            Scene?.Add(chain);
            yield return duration;
        }

        private IEnumerator SpawnDimensionalSlash()
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = Calc.Random.NextAngle();
                var slash = new DXDimensionalSlash(Position, angle, 400f);
                activeProjectiles.Add(slash);
                Scene?.Add(slash);
                yield return 0.3f;
            }
        }

        private IEnumerator SpawnRealityMirror()
        {
            if (player == null) yield break;
            var mirror = new DXRealityMirror(player.Position, 2.5f);
            activeProjectiles.Add(mirror);
            Scene?.Add(mirror);
            yield return 2.5f;
        }

        private IEnumerator SpawnVoidGate()
        {
            Vector2 gatePos = basePosition + new Vector2(
                Calc.Random.Range(-150f, 150f),
                Calc.Random.Range(-100f, 100f));
            var gate = new DXVoidGate(gatePos, 3.0f, this);
            activeProjectiles.Add(gate);
            Scene?.Add(gate);
            yield return 3.0f;
        }

        private IEnumerator SpawnTimeDistortion(float duration)
        {
            var distortion = new DXTimeDistortion(basePosition, 200f, duration);
            activeProjectiles.Add(distortion);
            Scene?.Add(distortion);
            yield return duration;
        }

        private IEnumerator SpawnSoulTether(float duration)
        {
            if (player == null) yield break;
            var tether = new DXSoulTether(Position, player, duration, 200f);
            activeProjectiles.Add(tether);
            Scene?.Add(tether);
            yield return duration;
        }

        // DX-Exclusive Cosmic Judgment Attacks
        private IEnumerator SpawnHyperGigaNova()
        {
            IsVulnerable = false;
            yield return 1.5f; // Charge

            // Massive expanding ring system
            for (int i = 0; i < 4; i++)
            {
                yield return SpawnExpandingRing(
                    i % 2 == 0 ? Color.Gold : Color.Cyan,
                    400f + i * 50f, 2.0f);
                yield return 0.3f;
            }
            level?.Shake(1.0f);
            IsVulnerable = true;
            yield return 1.0f;
        }

        private IEnumerator SpawnInfinityStrike()
        {
            // Figure-eight pattern of projectiles
            float duration = 3.0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration * MathHelper.TwoPi * 2f;
                float x = (float)Math.Sin(t) * 200f;
                float y = (float)Math.Sin(t * 2f) * 100f;
                Vector2 projectilePos = basePosition + new Vector2(x, y);
                var proj = new DXBossProjectile(projectilePos, Vector2.Zero, "infinity");
                activeProjectiles.Add(proj);
                Scene?.Add(proj);
                elapsed += Engine.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator SpawnCosmicApocalypse()
        {
            // Combination of meteors, lightning, and beams
            yield return SpawnMeteorShower(6, 1.5f);
            yield return SpawnLightningStorm(4, 1.0f);
            yield return SpawnProjectileBurst("cosmic_nova", 16, 350f, 0.04f);
            level?.Shake(1.5f);
        }

        private IEnumerator SpawnTranscendenceFlare()
        {
            if (Sprite != null && Sprite.Has("judgment"))
                Sprite.Play("judgment");

            IsVulnerable = false;
            yield return 2.0f; // Long charge

            // Full-screen radial blast
            for (int wave = 0; wave < 5; wave++)
            {
                int count = 32;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathHelper.TwoPi / count * i + wave * 0.1f;
                    var proj = new DXBossProjectile(Position, Calc.AngleToVector(angle, 400f), "transcendence");
                    activeProjectiles.Add(proj);
                    Scene?.Add(proj);
                }
                yield return 0.4f;
            }
            level?.Shake(2.0f);
            IsVulnerable = true;
        }
        #endregion

        #region Soul Purification
        public void OnSoulPurified(string soulName)
        {
            if (soulsPurifiedMap.ContainsKey(soulName))
            {
                soulsPurifiedMap[soulName] = true;
                SoulsPurified++;
                Health -= MaxHealth / (TOTAL_CORRUPTED_SOULS * 2);
            }
        }
        #endregion

        #region Collision & Update
        private void OnPlayerCollision(global::Celeste.Player player)
        {
            if (player.DashAttacking && IsVulnerable)
            {
                Health -= 30;
                level?.Shake(0.3f);
                Audio.Play("event:/game/general/thing_booped", Position);
            }
            else
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
        }

        public override void Update()
        {
            base.Update();
            player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            activeProjectiles.RemoveAll(p => p.Scene == null);
            activeRifts.RemoveAll(r => r.Scene == null);

            TranscendenceLevel = transcendenceEnergy / maxTranscendenceEnergy;
        }

        public override void Render()
        {
            base.Render();

            if (CurrentPhase != BossPhase.Defeated && CurrentPhase != BossPhase.Dormant)
            {
                float healthPercent = (float)Health / MaxHealth;
                Vector2 barPos = Position + new Vector2(-60, -240);
                Draw.Rect(barPos, 120, 8, Color.DarkGray);
                Draw.Rect(barPos, 120 * healthPercent, 8,
                    isTranscendent ? Color.Gold : Color.Cyan);
                Draw.HollowRect(barPos, 120, 8, Color.White);

                // Transcendence meter
                if (transcendenceEnergy > 0)
                {
                    float tPercent = transcendenceEnergy / maxTranscendenceEnergy;
                    Vector2 tBarPos = Position + new Vector2(-60, -228);
                    Draw.Rect(tBarPos, 120 * tPercent, 4, Color.Gold);
                    Draw.HollowRect(tBarPos, 120, 4, Color.Goldenrod);
                }
            }
        }
        #endregion
    }
    #endregion

    #region DX Dark Matter Boss
    /// <summary>
    /// DX Dark Matter — A new DX-exclusive boss inspired by Desolo Zantas lore.
    /// A primordial void entity that combines elements from all previous bosses.
    /// Features a unique "Dark Construct" system where defeated boss echoes fight alongside it.
    /// </summary>
    [Tracked]
    [HotReloadable]
    [CustomEntity("MaggyHelper/DXDarkMatterBoss")]
    public class DXDarkMatterBoss : BossActor
    {
        #region Phases & Attacks
        public enum BossPhase
        {
            Emergence,             // Rising from the void
            DarkConstruct,         // Summons echoes of defeated bosses
            VoidHeart,             // Core exposed — rapid complex attacks
            EventHorizon,          // DX signature — gravity/space warping
            Singularity,           // Final phase — everything collapses inward
            Obliterated,
        }

        public enum AttackType
        {
            // Emergence
            VoidTentacle,
            DarkPulse,
            ShadowProjectile,
            // Dark Construct
            EchoFloweyVines,
            EchoAsrielStar,
            EchoBoneStorm,
            ConstructShield,
            // Void Heart
            HeartBeat,
            DarkMatterBeam,
            VoidBurst,
            ParasiteSwarm,
            // Event Horizon
            GravityCollapse,
            SpaceWarp,
            TimeFracture,
            DarkStar,
            // Singularity
            AbsoluteVoid,
            NullField,
            FinalCollapse,
            EternalDarkness,
        }
        #endregion

        #region Properties
        public BossPhase CurrentPhase { get; private set; }
        public bool IsVulnerable { get; private set; }
        public int ConstructsDestroyed { get; private set; }

        private global::Celeste.Player player;
        private global::Celeste.Level level;
        private Camera camera;

        private Vector2 basePosition;
        private Vector2 corePosition;
        private string currentAnimation;

        // Attack state
        private List<AttackType> currentAttackPattern;
        private int attackIndex;

        // Dark Construct system
        private List<DXBossEcho> activeEchoes;
        private bool coreExposed;

        // Sub entities 
        private List<Entity> activeProjectiles;

        // Event Horizon mechanics
        private float voidPullStrength;
        private bool eventHorizonActive;

        // Audio
        private const string MUSIC_DX_DARKMATTER_EMERGE = "event:/desolozantas/dx_content/music/dx_darkmatter_emergence";
        private const string MUSIC_DX_DARKMATTER_CONSTRUCT = "event:/desolozantas/dx_content/music/dx_darkmatter_construct";
        private const string MUSIC_DX_DARKMATTER_VOID = "event:/desolozantas/dx_content/music/dx_darkmatter_voidheart";
        private const string MUSIC_DX_DARKMATTER_SINGULARITY = "event:/desolozantas/dx_content/music/dx_darkmatter_singularity";

        #endregion

        #region Constructors
        public DXDarkMatterBoss(EntityData data, Vector2 offset)
            : base(data.Position + offset,
                   spriteName: "characters/dx_darkmatter/darkmatter",
                   spriteScale: Vector2.One,
                   maxFall: 0f,
                   collidable: true,
                   solidCollidable: false,
                   gravityMult: 0f,
                   collider: new Hitbox(96, 96, -48, -48))
        {
            MaxHealth = data.Int("maxHealth", 2000);
            Health = MaxHealth;
            Initialize();
        }

        public DXDarkMatterBoss(Vector2 position)
            : base(position,
                   spriteName: "characters/dx_darkmatter/darkmatter",
                   spriteScale: Vector2.One,
                   maxFall: 0f,
                   collidable: true,
                   solidCollidable: false,
                   gravityMult: 0f,
                   collider: new Hitbox(96, 96, -48, -48))
        {
            MaxHealth = 2000;
            Health = MaxHealth;
            Initialize();
        }

        private void Initialize()
        {
            CurrentPhase = BossPhase.Emergence;
            IsVulnerable = false;
            ConstructsDestroyed = 0;
            coreExposed = false;
            eventHorizonActive = false;
            voidPullStrength = 0f;

            basePosition = Position;
            corePosition = Position;
            attackIndex = 0;

            activeEchoes = new List<DXBossEcho>();
            activeProjectiles = new List<Entity>();
            currentAttackPattern = new List<AttackType>();

            SetupSprite();
            Add(new PlayerCollider(OnPlayerCollision));
            Add(new Coroutine(BossRoutine()));
        }
        #endregion

        #region Sprite Setup
        private void SetupSprite()
        {
            if (Sprite != null)
            {
                if (!Sprite.Has("dormant")) Sprite.Add("dormant", "dormant", 0.15f);
                if (!Sprite.Has("emerge")) Sprite.Add("emerge", "emerge", 0.08f);
                if (!Sprite.Has("core")) Sprite.Add("core", "core", 0.06f);
                if (!Sprite.Has("horizon")) Sprite.Add("horizon", "horizon", 0.05f);
                if (!Sprite.Has("singularity")) Sprite.Add("singularity", "singularity", 0.04f);
                if (!Sprite.Has("collapse")) Sprite.Add("collapse", "collapse", 0.03f);

                Sprite.Play("dormant");
                Sprite.CenterOrigin();
            }
            currentAnimation = "dormant";
        }
        #endregion

        #region Main Boss Routine
        private IEnumerator BossRoutine()
        {
            while (player == null)
            {
                player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                yield return null;
            }
            level = SceneAs<global::Celeste.Level>();
            camera = level?.Camera;

            while (CurrentPhase != BossPhase.Obliterated)
            {
                switch (CurrentPhase)
                {
                    case BossPhase.Emergence:
                        yield return EmergencePhase();
                        break;
                    case BossPhase.DarkConstruct:
                        yield return DarkConstructPhase();
                        break;
                    case BossPhase.VoidHeart:
                        yield return VoidHeartPhase();
                        break;
                    case BossPhase.EventHorizon:
                        yield return EventHorizonPhase();
                        break;
                    case BossPhase.Singularity:
                        yield return SingularityPhase();
                        break;
                }
                yield return null;
            }

            yield return ObliterationSequence();
        }
        #endregion

        #region Phase Implementations
        private IEnumerator EmergencePhase()
        {
            Audio.SetMusic(MUSIC_DX_DARKMATTER_EMERGE);
            if (Sprite != null && Sprite.Has("emerge"))
                Sprite.Play("emerge");

            level?.Shake(1.0f);
            yield return 3.0f;

            currentAttackPattern = new List<AttackType>
            {
                AttackType.VoidTentacle,
                AttackType.DarkPulse,
                AttackType.ShadowProjectile,
            };
            IsVulnerable = true;

            while (Health > MaxHealth * 0.80f && CurrentPhase == BossPhase.Emergence)
            {
                yield return ExecuteAttackPattern();
            }

            CurrentPhase = BossPhase.DarkConstruct;
        }

        /// <summary>
        /// Dark Construct Phase — Summons echo versions of previous bosses.
        /// Player must defeat the echoes to expose the Dark Matter core.
        /// </summary>
        private IEnumerator DarkConstructPhase()
        {
            Audio.SetMusic(MUSIC_DX_DARKMATTER_CONSTRUCT);
            IsVulnerable = false; // Shielded while constructs are active

            // Spawn boss echoes
            string[] echoTypes = { "flowey", "asriel", "dedede" };
            for (int i = 0; i < echoTypes.Length; i++)
            {
                float angle = MathHelper.TwoPi / echoTypes.Length * i;
                Vector2 echoPos = basePosition + Calc.AngleToVector(angle, 150f);
                var echo = new DXBossEcho(echoPos, echoTypes[i], this);
                activeEchoes.Add(echo);
                Scene?.Add(echo);
                yield return 0.5f;
            }

            currentAttackPattern = new List<AttackType>
            {
                AttackType.EchoFloweyVines,
                AttackType.EchoAsrielStar,
                AttackType.EchoBoneStorm,
                AttackType.ConstructShield,
            };

            // Wait until all echoes are defeated
            while (activeEchoes.Any(e => e.Scene != null))
            {
                yield return ExecuteAttackPattern();
                activeEchoes.RemoveAll(e => e.Scene == null);
            }

            coreExposed = true;
            IsVulnerable = true;
            CurrentPhase = BossPhase.VoidHeart;
        }

        private IEnumerator VoidHeartPhase()
        {
            Audio.SetMusic(MUSIC_DX_DARKMATTER_VOID);
            if (Sprite != null && Sprite.Has("core"))
                Sprite.Play("core");

            currentAttackPattern = new List<AttackType>
            {
                AttackType.HeartBeat,
                AttackType.DarkMatterBeam,
                AttackType.VoidBurst,
                AttackType.ParasiteSwarm,
            };

            while (Health > MaxHealth * 0.40f && CurrentPhase == BossPhase.VoidHeart)
            {
                yield return ExecuteAttackPattern();
            }

            CurrentPhase = BossPhase.EventHorizon;
        }

        private IEnumerator EventHorizonPhase()
        {
            eventHorizonActive = true;
            if (Sprite != null && Sprite.Has("horizon"))
                Sprite.Play("horizon");

            currentAttackPattern = new List<AttackType>
            {
                AttackType.GravityCollapse,
                AttackType.SpaceWarp,
                AttackType.TimeFracture,
                AttackType.DarkStar,
            };

            while (Health > MaxHealth * 0.15f && CurrentPhase == BossPhase.EventHorizon)
            {
                yield return ExecuteAttackPattern();

                // Pull player toward center
                if (player != null)
                {
                    Vector2 pullDir = (Position - player.Position).SafeNormalize();
                    voidPullStrength = Math.Min(voidPullStrength + Engine.DeltaTime * 10f, 60f);
                }
            }

            eventHorizonActive = false;
            CurrentPhase = BossPhase.Singularity;
        }

        /// <summary>
        /// Singularity — The final DX-exclusive phase.
        /// Everything collapses. Extremely fast attacks from all directions.
        /// </summary>
        private IEnumerator SingularityPhase()
        {
            Audio.SetMusic(MUSIC_DX_DARKMATTER_SINGULARITY);
            if (Sprite != null && Sprite.Has("singularity"))
                Sprite.Play("singularity");

            level?.Shake(1.5f);

            currentAttackPattern = new List<AttackType>
            {
                AttackType.AbsoluteVoid,
                AttackType.NullField,
                AttackType.FinalCollapse,
                AttackType.EternalDarkness,
            };

            while (Health > 0 && CurrentPhase == BossPhase.Singularity)
            {
                yield return ExecuteAttackPattern();

                // Random rapid-fire attacks
                if (Calc.Random.Chance(0.5f))
                {
                    yield return SpawnProjectileBurst("void", 24, 350f, 0.02f);
                }
            }

            CurrentPhase = BossPhase.Obliterated;
        }

        private IEnumerator ObliterationSequence()
        {
            IsVulnerable = false;

            if (Sprite != null && Sprite.Has("collapse"))
                Sprite.Play("collapse");

            Audio.SetMusic(null);
            level?.Shake(2.0f);

            // Clean up
            foreach (var echo in activeEchoes) echo?.RemoveSelf();
            foreach (var proj in activeProjectiles) proj?.RemoveSelf();
            activeEchoes.Clear();
            activeProjectiles.Clear();

            yield return 4.0f;

            // Set session flags for boss defeat tracking
            {
                var lvl = SceneAs<Level>();
                lvl?.Session.SetFlag("DXDarkMatter_Defeated", true);
                lvl?.Session.SetFlag("DXBossDefeated", true);
            }

            RemoveSelf();
        }
        #endregion

        #region Attack Execution
        private IEnumerator ExecuteAttackPattern()
        {
            if (currentAttackPattern.Count == 0) yield break;
            AttackType attack = currentAttackPattern[attackIndex % currentAttackPattern.Count];
            attackIndex++;
            yield return ExecuteAttack(attack);
            yield return 0.5f;
        }

        private IEnumerator ExecuteAttack(AttackType attack)
        {
            switch (attack)
            {
                case AttackType.VoidTentacle:
                    yield return SpawnTentacleAttack(4);
                    break;
                case AttackType.DarkPulse:
                    yield return SpawnExpandingRing(Color.DarkViolet, 250f, 1.5f);
                    break;
                case AttackType.ShadowProjectile:
                    yield return SpawnProjectileBurst("shadow", 8, 200f, 0.1f);
                    break;
                case AttackType.EchoFloweyVines:
                    yield return SpawnProjectileBurst("vine_echo", 6, 180f, 0.12f);
                    break;
                case AttackType.EchoAsrielStar:
                    yield return SpawnProjectileBurst("star_echo", 10, 220f, 0.08f);
                    break;
                case AttackType.EchoBoneStorm:
                    yield return SpawnProjectileBurst("bone_echo", 12, 250f, 0.06f);
                    break;
                case AttackType.ConstructShield:
                    yield return SpawnExpandingRing(Color.Purple, 150f, 1.0f);
                    break;
                case AttackType.HeartBeat:
                    yield return SpawnHeartbeatPulse();
                    break;
                case AttackType.DarkMatterBeam:
                    yield return SpawnDarkMatterBeam();
                    break;
                case AttackType.VoidBurst:
                    yield return SpawnProjectileBurst("void", 16, 300f, 0.04f);
                    break;
                case AttackType.ParasiteSwarm:
                    yield return SpawnProjectileBurst("parasite", 20, 150f, 0.05f);
                    break;
                case AttackType.GravityCollapse:
                    yield return SpawnGravityCollapse();
                    break;
                case AttackType.SpaceWarp:
                    yield return SpawnSpaceWarp();
                    break;
                case AttackType.TimeFracture:
                    yield return SpawnTimeFracture();
                    break;
                case AttackType.DarkStar:
                    yield return SpawnDarkStar();
                    break;
                case AttackType.AbsoluteVoid:
                    yield return SpawnAbsoluteVoid();
                    break;
                case AttackType.NullField:
                    yield return SpawnNullField();
                    break;
                case AttackType.FinalCollapse:
                    yield return SpawnFinalCollapse();
                    break;
                case AttackType.EternalDarkness:
                    yield return SpawnEternalDarkness();
                    break;
            }
        }
        #endregion

        #region Attack Helpers
        private IEnumerator SpawnProjectileBurst(string type, int count, float speed, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi / count * i;
                var proj = new DXBossProjectile(Position, Calc.AngleToVector(angle, speed), type);
                activeProjectiles.Add(proj);
                Scene?.Add(proj);
                yield return delay;
            }
        }

        private IEnumerator SpawnExpandingRing(Color color, float maxRadius, float duration)
        {
            var ring = new DXExpandingRing(Position, maxRadius, duration, color);
            activeProjectiles.Add(ring);
            Scene?.Add(ring);
            yield return duration;
        }

        private IEnumerator SpawnTentacleAttack(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi / count * i + Calc.Random.NextFloat() * 0.5f;
                Vector2 dir = Calc.AngleToVector(angle, 1f);
                var tendril = new DXCorruptionTendril(Position, dir);
                activeProjectiles.Add(tendril);
                Scene?.Add(tendril);
                yield return 0.2f;
            }
        }

        private IEnumerator SpawnHeartbeatPulse()
        {
            // Double pulse like a heartbeat
            yield return SpawnExpandingRing(Color.DarkRed, 200f, 0.5f);
            yield return 0.3f;
            yield return SpawnExpandingRing(Color.Red, 250f, 0.5f);
        }

        private IEnumerator SpawnDarkMatterBeam()
        {
            if (player == null) yield break;
            Vector2 dir = (player.Position - Position).SafeNormalize();
            var beam = new DXCorruptionBeam(Position, dir, 500f, 2.0f);
            activeProjectiles.Add(beam);
            Scene?.Add(beam);
            level?.Shake(0.4f);
            yield return 2.0f;
        }

        private IEnumerator SpawnGravityCollapse()
        {
            var well = new DXGravityWell(Position, 300f, 3.0f);
            activeProjectiles.Add(well);
            Scene?.Add(well);
            yield return 3.0f;
        }

        private IEnumerator SpawnSpaceWarp()
        {
            // Teleport player to random position in arena
            if (player != null)
            {
                Vector2 warpPos = basePosition + new Vector2(
                    Calc.Random.Range(-200f, 200f),
                    Calc.Random.Range(-100f, 100f));
                // Visual indicator before warp
                var indicator = new DXWarpIndicator(warpPos, 1.0f);
                activeProjectiles.Add(indicator);
                Scene?.Add(indicator);
            }
            yield return 1.5f;
        }

        private IEnumerator SpawnTimeFracture()
        {
            var distortion = new DXTimeDistortion(Position, 250f, 2.5f);
            activeProjectiles.Add(distortion);
            Scene?.Add(distortion);
            yield return 2.5f;
        }

        private IEnumerator SpawnDarkStar()
        {
            var star = new DXDarkStar(basePosition, 150f, 3.0f);
            activeProjectiles.Add(star);
            Scene?.Add(star);
            yield return 3.0f;
        }

        private IEnumerator SpawnAbsoluteVoid()
        {
            // Expanding void that leaves only safe spots
            IsVulnerable = false;
            yield return SpawnExpandingRing(Color.Black, 500f, 3.0f);
            level?.Shake(1.0f);
            IsVulnerable = true;
        }

        private IEnumerator SpawnNullField()
        {
            // Area that disables player dash
            var field = new DXNullField(player?.Position ?? Position, 100f, 2.0f);
            activeProjectiles.Add(field);
            Scene?.Add(field);
            yield return 2.0f;
        }

        private IEnumerator SpawnFinalCollapse()
        {
            // Arena shrinks temporarily
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi / 8 * i;
                Vector2 wallPos = basePosition + Calc.AngleToVector(angle, 300f);
                var wall = new DXProjectileWall(wallPos,
                    (basePosition - wallPos).SafeNormalize() * 100f, 150f);
                activeProjectiles.Add(wall);
                Scene?.Add(wall);
            }
            level?.Shake(0.8f);
            yield return 2.0f;
        }

        private IEnumerator SpawnEternalDarkness()
        {
            // Screen goes dark, only safe zones visible
            yield return 0.5f;
            yield return SpawnProjectileBurst("eternal", 32, 200f, 0.02f);
            level?.Shake(1.5f);
            yield return 1.0f;
        }
        #endregion

        #region Echo Management
        public void OnEchoDefeated()
        {
            ConstructsDestroyed++;
        }
        #endregion

        #region Collision & Update
        private void OnPlayerCollision(global::Celeste.Player player)
        {
            if (player.DashAttacking && IsVulnerable)
            {
                Health -= 20;
                level?.Shake(0.2f);
                Audio.Play("event:/game/general/thing_booped", Position);
            }
            else if (!player.DashAttacking)
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
        }

        public override void Update()
        {
            base.Update();
            player = Scene?.Tracker.GetEntity<global::Celeste.Player>();

            // Void pull effect during Event Horizon
            if (eventHorizonActive && player != null && voidPullStrength > 0f)
            {
                Vector2 pullDir = (Position - player.Position).SafeNormalize();
                player.Speed += pullDir * voidPullStrength * Engine.DeltaTime;
            }

            activeProjectiles.RemoveAll(p => p.Scene == null);
            activeEchoes.RemoveAll(e => e.Scene == null);
        }

        public override void Render()
        {
            base.Render();

            if (CurrentPhase != BossPhase.Obliterated && CurrentPhase != BossPhase.Emergence)
            {
                float healthPercent = (float)Health / MaxHealth;
                Vector2 barPos = Position + new Vector2(-50, -60);
                Draw.Rect(barPos, 100, 8, Color.DarkGray);
                Draw.Rect(barPos, 100 * healthPercent, 8,
                    eventHorizonActive ? Color.DarkViolet : Color.DarkMagenta);
                Draw.HollowRect(barPos, 100, 8, Color.White);
            }
        }
        #endregion
    }
    #endregion
}
