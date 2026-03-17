namespace MaggyHelper
{
    #region Boss Projectiles

    /// <summary>
    /// Generic directional projectile used by all DX bosses.
    /// Type determines visual appearance and behavior.
    /// </summary>
    [Tracked]
    public class DXBossProjectile : Entity
    {
        private Vector2 velocity;
        private string projectileType;
        private float lifetime;
        private float elapsed;
        private Color color;

        public DXBossProjectile(Vector2 position, Vector2 velocity, string type)
            : base(position)
        {
            this.velocity = velocity;
            this.projectileType = type;
            lifetime = 5.0f;
            elapsed = 0f;
            color = GetColorForType(type);

            Collider = new Circle(6f);
            Add(new PlayerCollider(OnPlayerHit));
            Depth = -9999;
        }

        private Color GetColorForType(string type)
        {
            return type switch
            {
                "vine" => Color.DarkGreen,
                "bone" => Color.Beige,
                "skull" => Color.LightGray,
                "flesh" => Color.DarkRed,
                "blade" => Color.Silver,
                "missile" => Color.OrangeRed,
                "revenant" => Color.MediumPurple,
                "void" => Color.DarkViolet,
                "omega" => Color.Magenta,
                "star" => Color.Gold,
                "meteor" => Color.OrangeRed,
                "infinity" => Color.Cyan,
                "cosmic" => Color.DeepSkyBlue,
                "cosmic_nova" => Color.LightCyan,
                "transcendence" => Color.Goldenrod,
                "shadow" => Color.DarkSlateGray,
                "vine_echo" => Color.LimeGreen,
                "star_echo" => Color.LightGoldenrodYellow,
                "bone_echo" => Color.AntiqueWhite,
                "parasite" => Color.DarkOliveGreen,
                "soul_corrupt" => Color.Indigo,
                "eternal" => Color.Black,
                _ => Color.White,
            };
        }

        private void OnPlayerHit(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();
            Position += velocity * Engine.DeltaTime;
            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            Draw.Circle(Position, 6f, color, 8);
            Draw.Circle(Position, 3f, Color.White, 6);
        }
    }

    /// <summary>
    /// Homing projectile that tracks the player's position.
    /// </summary>
    [Tracked]
    public class DXHomingProjectile : Entity
    {
        private Vector2 velocity;
        private global::Celeste.Player target;
        private string projectileType;
        private float lifetime;
        private float elapsed;
        private float homingStrength;
        private float speed;

        public DXHomingProjectile(Vector2 position, Vector2 initialVelocity,
            global::Celeste.Player target, string type, float lifetime)
            : base(position)
        {
            velocity = initialVelocity;
            this.target = target;
            projectileType = type;
            this.lifetime = lifetime;
            elapsed = 0f;
            speed = initialVelocity.Length();
            homingStrength = 3.0f;

            Collider = new Circle(8f);
            Add(new PlayerCollider(OnPlayerHit));
            Depth = -9999;
        }

        private void OnPlayerHit(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();

            if (target != null && target.Scene != null)
            {
                Vector2 toTarget = (target.Position - Position).SafeNormalize();
                velocity = Vector2.Lerp(velocity.SafeNormalize(), toTarget, homingStrength * Engine.DeltaTime) * speed;
            }

            Position += velocity * Engine.DeltaTime;
            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            Draw.Circle(Position, 8f, Color.OrangeRed, 8);
            Draw.Circle(Position, 4f, Color.Yellow, 6);
        }
    }

    #endregion

    #region Expanding Ring Hazard

    /// <summary>
    /// An expanding ring that damages on contact.
    /// </summary>
    [Tracked]
    public class DXExpandingRing : Entity
    {
        private float maxRadius;
        private float currentRadius;
        private float duration;
        private float elapsed;
        private Color color;
        private float ringThickness;

        public DXExpandingRing(Vector2 position, float maxRadius, float duration, Color color)
            : base(position)
        {
            this.maxRadius = maxRadius;
            this.duration = duration;
            this.color = color;
            currentRadius = 0f;
            elapsed = 0f;
            ringThickness = 8f;
            Depth = -9998;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;
            currentRadius = MathHelper.Lerp(0f, maxRadius, elapsed / duration);

            // Check for player collision with ring edge
            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                float dist = Vector2.Distance(player.Position, Position);
                if (Math.Abs(dist - currentRadius) < ringThickness)
                {
                    player.Die((player.Position - Position).SafeNormalize());
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            Draw.Circle(Position, currentRadius, color * 0.6f, 32);
            Draw.Circle(Position, currentRadius - 2f, Color.Transparent, 32);
        }
    }

    #endregion

    #region Ground Pillar

    /// <summary>
    /// A pillar that erupts from the ground.
    /// </summary>
    [Tracked]
    public class DXGroundPillar : Entity
    {
        private float targetHeight;
        private float currentHeight;
        private float growSpeed;
        private float lifetime;
        private float elapsed;
        private bool fullyGrown;

        public DXGroundPillar(Vector2 groundPosition, float height, float lifetime)
            : base(groundPosition)
        {
            targetHeight = height;
            this.lifetime = lifetime;
            currentHeight = 0f;
            growSpeed = 400f;
            elapsed = 0f;
            fullyGrown = false;

            Collider = new Hitbox(24, 0, -12, 0);
            Add(new PlayerCollider(OnPlayer));
            Depth = -9997;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die(Vector2.UnitY * -1f);
        }

        public override void Update()
        {
            base.Update();

            if (!fullyGrown)
            {
                currentHeight += growSpeed * Engine.DeltaTime;
                if (currentHeight >= targetHeight)
                {
                    currentHeight = targetHeight;
                    fullyGrown = true;
                }
                Collider = new Hitbox(24, currentHeight, -12, -currentHeight);
            }

            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            Draw.Rect(Position.X - 12, Position.Y - currentHeight, 24, currentHeight, Color.DarkSlateGray);
            Draw.HollowRect(Position.X - 12, Position.Y - currentHeight, 24, currentHeight, Color.Gray);
        }
    }

    #endregion

    #region Projectile Wall

    /// <summary>
    /// A moving wall of projectiles.
    /// </summary>
    [Tracked]
    public class DXProjectileWall : Entity
    {
        private Vector2 velocity;
        private float wallLength;
        private float lifetime;
        private float elapsed;

        public DXProjectileWall(Vector2 position, Vector2 velocity, float length)
            : base(position)
        {
            this.velocity = velocity;
            wallLength = length;
            lifetime = 5.0f;
            elapsed = 0f;

            bool horizontal = Math.Abs(velocity.Y) < Math.Abs(velocity.X);
            Collider = horizontal
                ? new Hitbox(16, wallLength, -8, -wallLength / 2)
                : new Hitbox(wallLength, 16, -wallLength / 2, -8);
            Add(new PlayerCollider(OnPlayer));
            Depth = -9997;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();
            Position += velocity * Engine.DeltaTime;
            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            bool horizontal = Math.Abs(velocity.Y) < Math.Abs(velocity.X);
            if (horizontal)
                Draw.Rect(Position.X - 8, Position.Y - wallLength / 2, 16, wallLength, Color.DarkMagenta * 0.7f);
            else
                Draw.Rect(Position.X - wallLength / 2, Position.Y - 8, wallLength, 16, Color.DarkMagenta * 0.7f);
        }
    }

    #endregion

    #region Lightning Bolt

    /// <summary>
    /// A vertical lightning bolt that strikes downward.
    /// </summary>
    [Tracked]
    public class DXLightningBolt : Entity
    {
        private float speed;
        private float lifetime;
        private float elapsed;
        private bool hasStruck;
        private float strikeWidth;

        public DXLightningBolt(Vector2 position, float speed, float lifetime)
            : base(position)
        {
            this.speed = speed;
            this.lifetime = lifetime;
            elapsed = 0f;
            hasStruck = false;
            strikeWidth = 16f;

            Collider = new Hitbox(strikeWidth, 600, -strikeWidth / 2, 0);
            Add(new PlayerCollider(OnPlayer));
            Depth = -10000;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die(Vector2.UnitY);
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = 1f - (elapsed / lifetime);
            Color boltColor = Color.Yellow * alpha;
            Color innerColor = Color.White * alpha;

            Draw.Rect(Position.X - strikeWidth / 2, Position.Y, strikeWidth, 600, boltColor * 0.3f);
            Draw.Rect(Position.X - 2, Position.Y, 4, 600, innerColor);
        }
    }

    #endregion

    #region Wave Attack

    /// <summary>
    /// A horizontal wave attack that travels forward.
    /// </summary>
    [Tracked]
    public class DXWaveAttack : Entity
    {
        private float totalWidth;
        private float waveHeight;
        private Color color;
        private float elapsed;
        private float speed;

        public DXWaveAttack(Vector2 position, float width, float height, Color color)
            : base(position)
        {
            totalWidth = width;
            waveHeight = height;
            this.color = color;
            elapsed = 0f;
            speed = 150f;

            Collider = new Hitbox(width, height, 0, -height);
            Add(new PlayerCollider(OnPlayer));
            Depth = -9998;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();
            Position += Vector2.UnitX * speed * Engine.DeltaTime;
            elapsed += Engine.DeltaTime;
            if (elapsed >= 4.0f) RemoveSelf();
        }

        public override void Render()
        {
            Draw.Rect(Position.X, Position.Y - waveHeight, totalWidth, waveHeight, color * 0.5f);
        }
    }

    #endregion

    #region Drain Beam

    /// <summary>
    /// A beam that connects boss to player, dealing damage over time.
    /// </summary>
    [Tracked]
    public class DXDrainBeam : Entity
    {
        private global::Celeste.Player target;
        private float duration;
        private float elapsed;
        private float damageInterval;
        private float lastDamageTime;

        public DXDrainBeam(Vector2 position, global::Celeste.Player target, float duration)
            : base(position)
        {
            this.target = target;
            this.duration = duration;
            elapsed = 0f;
            damageInterval = 0.5f;
            lastDamageTime = 0f;
            Depth = -10001;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            if (target != null && target.Scene != null && elapsed - lastDamageTime >= damageInterval)
            {
                float dist = Vector2.Distance(Position, target.Position);
                if (dist < 200f)
                {
                    target.Die((target.Position - Position).SafeNormalize());
                }
                lastDamageTime = elapsed;
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            if (target != null && target.Scene != null)
            {
                float alpha = 1f - (elapsed / duration);
                Draw.Line(Position, target.Position, Color.MediumPurple * alpha, 4f);
                Draw.Line(Position, target.Position, Color.White * alpha * 0.5f, 1f);
            }
        }
    }

    #endregion

    #region Corruption Beam

    /// <summary>
    /// A wide directional beam attack for Corruption Overload phase.
    /// </summary>
    [Tracked]
    public class DXCorruptionBeam : Entity
    {
        private Vector2 direction;
        private float beamLength;
        private float duration;
        private float elapsed;
        private float beamWidth;

        public DXCorruptionBeam(Vector2 position, Vector2 direction, float length, float duration)
            : base(position)
        {
            this.direction = direction.SafeNormalize();
            beamLength = length;
            this.duration = duration;
            elapsed = 0f;
            beamWidth = 24f;
            Depth = -10001;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            // Check player collision along beam length
            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                Vector2 toPlayer = player.Position - Position;
                float projLength = Vector2.Dot(toPlayer, direction);
                if (projLength > 0 && projLength < beamLength)
                {
                    Vector2 closestPoint = Position + direction * projLength;
                    if (Vector2.Distance(player.Position, closestPoint) < beamWidth)
                    {
                        player.Die(direction);
                    }
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = 1f - (elapsed / duration) * 0.5f;
            Vector2 end = Position + direction * beamLength;

            // Outer beam
            Draw.Line(Position, end, Color.Purple * alpha, beamWidth);
            // Inner beam
            Draw.Line(Position, end, Color.Magenta * alpha, beamWidth / 3f);
            // Core
            Draw.Line(Position, end, Color.White * alpha, 2f);
        }
    }

    #endregion

    #region Corruption Tendril

    /// <summary>
    /// An organic tendril that grows outward from the boss.
    /// </summary>
    [Tracked]
    public class DXCorruptionTendril : Entity
    {
        private Vector2 growDirection;
        private float growSpeed;
        private float maxLength;
        private float currentLength;
        private float lifetime;
        private float elapsed;
        private List<Vector2> segments;

        public DXCorruptionTendril(Vector2 position, Vector2 direction)
            : base(position)
        {
            growDirection = direction.SafeNormalize();
            growSpeed = 200f;
            maxLength = 250f;
            currentLength = 0f;
            lifetime = 6.0f;
            elapsed = 0f;
            segments = new List<Vector2> { position };

            Collider = new Hitbox(12, 12, -6, -6);
            Add(new PlayerCollider(OnPlayer));
            Depth = -9996;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();

            if (currentLength < maxLength)
            {
                currentLength += growSpeed * Engine.DeltaTime;
                // Add segment with slight wobble
                Vector2 wobble = new Vector2(
                    (float)Math.Sin(currentLength * 0.1f) * 10f,
                    (float)Math.Cos(currentLength * 0.1f) * 10f);
                Vector2 newPoint = segments[0] + growDirection * currentLength + wobble;
                segments.Add(newPoint);
            }

            elapsed += Engine.DeltaTime;
            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                float alpha = 1f - ((float)i / segments.Count) * 0.5f;
                Draw.Line(segments[i], segments[i + 1], Color.DarkMagenta * alpha, 6f);
            }
        }
    }

    #endregion

    #region Void Orb

    /// <summary>
    /// Orbiting void orb that fires projectiles during Corruption Overload.
    /// </summary>
    [Tracked]
    public class DXVoidOrb : Entity
    {
        private Entity owner;
        private Vector2 orbitCenter;
        private float orbitRadius;
        private float orbitSpeed;
        private float orbitAngle;
        private float fireInterval;
        private float fireTimer;

        public DXVoidOrb(Vector2 position, Entity owner)
            : base(position)
        {
            this.owner = owner;
            orbitCenter = position;
            orbitRadius = Vector2.Distance(position, owner.Position);
            orbitSpeed = 1.5f;
            orbitAngle = Calc.Angle(owner.Position, position);
            fireInterval = 2.0f;
            fireTimer = 0f;

            Collider = new Circle(12f);
            Add(new PlayerCollider(OnPlayer));
            Add(new BloomPoint(0.3f, 20f));
            Depth = -9999;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();

            if (owner == null || owner.Scene == null)
            {
                RemoveSelf();
                return;
            }

            orbitAngle += orbitSpeed * Engine.DeltaTime;
            Position = owner.Position + Calc.AngleToVector(orbitAngle, orbitRadius);

            // Fire projectile at player
            fireTimer += Engine.DeltaTime;
            if (fireTimer >= fireInterval)
            {
                fireTimer = 0f;
                var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null)
                {
                    Vector2 dir = (player.Position - Position).SafeNormalize();
                    Scene?.Add(new DXBossProjectile(Position, dir * 180f, "void"));
                }
            }
        }

        public override void Render()
        {
            Draw.Circle(Position, 12f, Color.DarkViolet, 12);
            Draw.Circle(Position, 6f, Color.MediumPurple, 8);
            Draw.Circle(Position, 3f, Color.White, 6);
        }
    }

    #endregion

    #region Reality Crack

    /// <summary>
    /// A crack in reality that spawns projectiles.
    /// </summary>
    [Tracked]
    public class DXRealityCrack : Entity
    {
        private float lifetime;
        private float elapsed;
        private float spawnTimer;
        private float spawnInterval;

        public DXRealityCrack(Vector2 position, float lifetime)
            : base(position)
        {
            this.lifetime = lifetime;
            elapsed = 0f;
            spawnTimer = 0f;
            spawnInterval = 0.5f;

            Collider = new Circle(20f);
            Add(new PlayerCollider(OnPlayer));
            Depth = -10000;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            player.Die((player.Position - Position).SafeNormalize());
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            spawnTimer += Engine.DeltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                float angle = Calc.Random.NextAngle();
                Vector2 dir = Calc.AngleToVector(angle, 150f);
                Scene?.Add(new DXBossProjectile(Position, dir, "void"));
            }

            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(elapsed * 8f) * 0.3f + 0.7f;
            Draw.Circle(Position, 20f, Color.Magenta * pulse, 16);
            Draw.Circle(Position, 12f, Color.White * (pulse * 0.5f), 12);
        }
    }

    #endregion

    #region Dimensional Rift

    /// <summary>
    /// A tear in dimensional space used by DX Asriel Transcendence.
    /// Spawns periodic projectiles and can teleport the boss.
    /// </summary>
    [Tracked]
    public class DXDimensionalRift : Entity
    {
        private float lifetime;
        private float elapsed;
        private float spawnInterval;
        private float spawnTimer;

        public DXDimensionalRift(Vector2 position, float lifetime)
            : base(position)
        {
            this.lifetime = lifetime;
            elapsed = 0f;
            spawnInterval = 1.5f;
            spawnTimer = 0f;

            Collider = new Circle(30f);
            Add(new BloomPoint(0.5f, 30f));
            Depth = -9995;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            spawnTimer += Engine.DeltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float angle = Calc.Random.NextAngle();
                    Scene?.Add(new DXBossProjectile(Position, Calc.AngleToVector(angle, 200f), "cosmic"));
                }
            }

            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(elapsed * 4f) * 0.3f + 0.7f;
            // Rift visual — spiraling colors
            Draw.Circle(Position, 30f, Color.Cyan * pulse, 20);
            Draw.Circle(Position, 20f, Color.DeepSkyBlue * pulse, 16);
            Draw.Circle(Position, 10f, Color.White * pulse * 0.8f, 10);
        }
    }

    #endregion

    #region Cosmic Sweep

    /// <summary>
    /// A wide sweeping cosmic attack beam.
    /// </summary>
    [Tracked]
    public class DXCosmicSweep : Entity
    {
        private Vector2 direction;
        private float sweepWidth;
        private float duration;
        private float elapsed;
        private float sweepAngle;
        private float sweepSpeed;

        public DXCosmicSweep(Vector2 position, Vector2 initialDir, float width, float duration)
            : base(position)
        {
            direction = initialDir.SafeNormalize();
            sweepWidth = width;
            this.duration = duration;
            elapsed = 0f;
            sweepAngle = Calc.Angle(Vector2.Zero, direction);
            sweepSpeed = MathHelper.Pi; // Sweep PI radians during duration
            Depth = -10001;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;
            sweepAngle += sweepSpeed * Engine.DeltaTime;
            direction = Calc.AngleToVector(sweepAngle, 1f);

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                Vector2 toPlayer = player.Position - Position;
                float projLen = Vector2.Dot(toPlayer, direction);
                if (projLen > 0 && projLen < sweepWidth)
                {
                    Vector2 closest = Position + direction * projLen;
                    if (Vector2.Distance(player.Position, closest) < 20f)
                    {
                        player.Die(direction);
                    }
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = 1f - elapsed / duration * 0.5f;
            Vector2 end = Position + direction * sweepWidth;
            Draw.Line(Position, end, Color.Cyan * alpha, 20f);
            Draw.Line(Position, end, Color.White * alpha, 4f);
        }
    }

    #endregion

    #region Gravity Well

    /// <summary>
    /// An area that pulls the player toward its center.
    /// </summary>
    [Tracked]
    public class DXGravityWell : Entity
    {
        private float radius;
        private float duration;
        private float elapsed;
        private float pullStrength;

        public DXGravityWell(Vector2 position, float radius, float duration)
            : base(position)
        {
            this.radius = radius;
            this.duration = duration;
            elapsed = 0f;
            pullStrength = 80f;
            Depth = -9994;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                float dist = Vector2.Distance(player.Position, Position);
                if (dist < radius && dist > 10f)
                {
                    Vector2 pull = (Position - player.Position).SafeNormalize();
                    float strength = (1f - dist / radius) * pullStrength;
                    player.Speed += pull * strength * Engine.DeltaTime;
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = 1f - elapsed / duration * 0.5f;
            for (float r = radius; r > 10f; r -= 30f)
            {
                float ringAlpha = (r / radius) * alpha;
                Draw.Circle(Position, r, Color.DarkViolet * ringAlpha * 0.3f, 24);
            }
        }
    }

    #endregion

    #region Astral Chain

    /// <summary>
    /// A chain that connects to the player and restricts movement.
    /// </summary>
    [Tracked]
    public class DXAstralChain : Entity
    {
        private global::Celeste.Player target;
        private float duration;
        private float elapsed;
        private float maxRange;

        public DXAstralChain(Vector2 position, global::Celeste.Player target, float duration)
            : base(position)
        {
            this.target = target;
            this.duration = duration;
            elapsed = 0f;
            maxRange = 180f;
            Depth = -10001;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            if (target != null && target.Scene != null)
            {
                float dist = Vector2.Distance(target.Position, Position);
                if (dist > maxRange)
                {
                    // Pull player back
                    Vector2 pull = (Position - target.Position).SafeNormalize();
                    target.Speed += pull * 200f * Engine.DeltaTime;
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            if (target != null && target.Scene != null)
            {
                float alpha = 1f - elapsed / duration * 0.3f;
                Draw.Line(Position, target.Position, Color.Gold * alpha, 3f);
            }
        }
    }

    #endregion

    #region Dimensional Slash

    /// <summary>
    /// A fast-moving dimensional slash that cuts through space.
    /// </summary>
    [Tracked]
    public class DXDimensionalSlash : Entity
    {
        private float angle;
        private float length;
        private float lifetime;
        private float elapsed;

        public DXDimensionalSlash(Vector2 position, float angle, float length)
            : base(position)
        {
            this.angle = angle;
            this.length = length;
            lifetime = 0.8f;
            elapsed = 0f;
            Depth = -10002;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null && elapsed < lifetime * 0.6f)
            {
                Vector2 dir = Calc.AngleToVector(angle, 1f);
                Vector2 toPlayer = player.Position - Position;
                float proj = Vector2.Dot(toPlayer, dir);
                if (proj > 0 && proj < length)
                {
                    Vector2 closest = Position + dir * proj;
                    if (Vector2.Distance(player.Position, closest) < 16f)
                    {
                        player.Die(dir);
                    }
                }
            }

            if (elapsed >= lifetime) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = 1f - elapsed / lifetime;
            Vector2 dir = Calc.AngleToVector(angle, 1f);
            Vector2 end = Position + dir * length;
            Draw.Line(Position, end, Color.Cyan * alpha, 12f);
            Draw.Line(Position, end, Color.White * alpha, 3f);
        }
    }

    #endregion

    #region Reality Mirror

    /// <summary>
    /// Creates a mirror at the player's position that spawns a shadow copy attack.
    /// </summary>
    [Tracked]
    public class DXRealityMirror : Entity
    {
        private float duration;
        private float elapsed;
        private float spawnTimer;

        public DXRealityMirror(Vector2 position, float duration)
            : base(position)
        {
            this.duration = duration;
            elapsed = 0f;
            spawnTimer = 0f;

            Collider = new Hitbox(40, 60, -20, -60);
            Depth = -9998;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            spawnTimer += Engine.DeltaTime;
            if (spawnTimer >= 0.8f)
            {
                spawnTimer = 0f;
                // Fire projectile in random direction
                float angle = Calc.Random.NextAngle();
                Scene?.Add(new DXBossProjectile(Position, Calc.AngleToVector(angle, 200f), "shadow"));
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(elapsed * 6f) * 0.2f + 0.8f;
            Draw.Rect(Position.X - 20, Position.Y - 60, 40, 60, Color.Silver * pulse * 0.4f);
            Draw.HollowRect(Position.X - 20, Position.Y - 60, 40, 60, Color.White * pulse);
        }
    }

    #endregion

    #region Void Gate

    /// <summary>
    /// A portal that spawns enemies and projectiles.
    /// </summary>
    [Tracked]
    public class DXVoidGate : Entity
    {
        private float duration;
        private float elapsed;
        private float spawnTimer;
        private Entity owner;

        public DXVoidGate(Vector2 position, float duration, Entity owner)
            : base(position)
        {
            this.duration = duration;
            this.owner = owner;
            elapsed = 0f;
            spawnTimer = 0f;

            Collider = new Circle(25f);
            Add(new BloomPoint(0.4f, 25f));
            Depth = -9996;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            spawnTimer += Engine.DeltaTime;
            if (spawnTimer >= 0.6f)
            {
                spawnTimer = 0f;
                var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null)
                {
                    Vector2 dir = (player.Position - Position).SafeNormalize();
                    Scene?.Add(new DXBossProjectile(Position, dir * 220f, "void"));
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(elapsed * 5f) * 0.3f + 0.7f;
            Draw.Circle(Position, 25f, Color.DarkViolet * pulse, 16);
            Draw.Circle(Position, 15f, Color.Black * 0.8f, 12);
            Draw.Circle(Position, 8f, Color.MediumPurple * pulse, 8);
        }
    }

    #endregion

    #region Time Distortion

    /// <summary>
    /// An area effect that slows time/player within its radius.
    /// </summary>
    [Tracked]
    public class DXTimeDistortion : Entity
    {
        private float radius;
        private float duration;
        private float elapsed;

        public DXTimeDistortion(Vector2 position, float radius, float duration)
            : base(position)
        {
            this.radius = radius;
            this.duration = duration;
            elapsed = 0f;
            Depth = -9993;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                float dist = Vector2.Distance(player.Position, Position);
                if (dist < radius)
                {
                    // Slow the player
                    player.Speed *= 0.95f;
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = (1f - elapsed / duration) * 0.3f;
            for (float r = 0; r < radius; r += 40f)
            {
                float ringAlpha = alpha * (1f - r / radius);
                Draw.Circle(Position, r, Color.LightBlue * ringAlpha, 20);
            }
        }
    }

    #endregion

    #region Corrupted Soul

    /// <summary>
    /// A corrupted soul that must be purified by dashing into it.
    /// Used in DX Asriel's Soul Convergence phase.
    /// </summary>
    [Tracked]
    public class DXCorruptedSoul : Entity
    {
        private string soulName;
        private DXAsrielTranscendenceBoss boss;
        private int hitsRequired;
        private int hitsTaken;
        private SineWave floatWave;
        private float baseY;
        private bool purified;

        public DXCorruptedSoul(Vector2 position, string name, DXAsrielTranscendenceBoss boss)
            : base(position)
        {
            soulName = name;
            this.boss = boss;
            hitsRequired = 3;
            hitsTaken = 0;
            purified = false;
            baseY = position.Y;

            Collider = new Circle(16f);
            Add(new PlayerCollider(OnPlayer));
            Add(floatWave = new SineWave(1.2f, 0f));
            Add(new BloomPoint(0.4f, 16f));
            Depth = -9998;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            if (player.DashAttacking && !purified)
            {
                hitsTaken++;
                Audio.Play("event:/game/general/thing_booped", Position);
                if (hitsTaken >= hitsRequired)
                {
                    purified = true;
                    boss?.OnSoulPurified(soulName);
                    Audio.Play("event:/game/general/collectible_keyget", Position);
                    RemoveSelf();
                }
            }
            else if (!player.DashAttacking)
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
        }

        public override void Update()
        {
            base.Update();
            Position = new Vector2(Position.X, baseY + floatWave.Value * 8f);
        }

        public override void Render()
        {
            Color c = purified ? Color.Gold : Color.Lerp(Color.Indigo, Color.White, (float)hitsTaken / hitsRequired);
            float pulse = (float)Math.Sin(Scene.TimeActive * 4f) * 0.2f + 0.8f;
            Draw.Circle(Position, 16f, c * pulse, 12);
            Draw.Circle(Position, 8f, Color.White * pulse * 0.5f, 8);
        }
    }

    #endregion

    #region Soul Tether

    /// <summary>
    /// A soul tether that restricts the player and deals periodic damage.
    /// </summary>
    [Tracked]
    public class DXSoulTether : Entity
    {
        private global::Celeste.Player target;
        private float duration;
        private float elapsed;
        private float maxRange;

        public DXSoulTether(Vector2 position, global::Celeste.Player target, float duration, float range)
            : base(position)
        {
            this.target = target;
            this.duration = duration;
            elapsed = 0f;
            maxRange = range;
            Depth = -10001;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            if (target != null && target.Scene != null)
            {
                float dist = Vector2.Distance(target.Position, Position);
                if (dist > maxRange)
                {
                    target.Die((target.Position - Position).SafeNormalize());
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            if (target != null && target.Scene != null)
            {
                float alpha = 1f - elapsed / duration * 0.3f;
                float dist = Vector2.Distance(target.Position, Position);
                Color c = dist > maxRange * 0.8f ? Color.Red : Color.MediumPurple;
                Draw.Line(Position, target.Position, c * alpha, 2f);
            }
        }
    }

    #endregion

    #region Boss Echo (Dark Construct)

    /// <summary>
    /// An echo/shadow version of a previously defeated boss.
    /// Used by DX Dark Matter's Dark Construct phase.
    /// Has simplified attack patterns from the original boss.
    /// </summary>
    [Tracked]
    public class DXBossEcho : Entity
    {
        private string echoType;
        private DXDarkMatterBoss owner;
        private int health;
        private int maxHealth;
        private float attackTimer;
        private float attackInterval;

        public DXBossEcho(Vector2 position, string type, DXDarkMatterBoss owner)
            : base(position)
        {
            echoType = type;
            this.owner = owner;

            switch (type)
            {
                case "flowey":
                    maxHealth = 200;
                    attackInterval = 1.5f;
                    break;
                case "asriel":
                    maxHealth = 300;
                    attackInterval = 2.0f;
                    break;
                case "dedede":
                    maxHealth = 250;
                    attackInterval = 1.8f;
                    break;
                default:
                    maxHealth = 150;
                    attackInterval = 2.0f;
                    break;
            }

            health = maxHealth;
            attackTimer = 0f;

            Collider = new Hitbox(32, 48, -16, -48);
            Add(new PlayerCollider(OnPlayer));
            Depth = -9998;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            if (player.DashAttacking)
            {
                health -= 50;
                Audio.Play("event:/game/general/thing_booped", Position);
                if (health <= 0)
                {
                    owner?.OnEchoDefeated();
                    RemoveSelf();
                }
            }
            else
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
        }

        public override void Update()
        {
            base.Update();
            attackTimer += Engine.DeltaTime;

            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                // Fire burst of type-appropriate projectiles
                int count = echoType == "asriel" ? 8 : 6;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathHelper.TwoPi / count * i;
                    string projType = echoType + "_echo";
                    Scene?.Add(new DXBossProjectile(Position, Calc.AngleToVector(angle, 180f), projType));
                }
            }
        }

        public override void Render()
        {
            Color echoColor = echoType switch
            {
                "flowey" => Color.DarkGreen,
                "asriel" => Color.Gold,
                "dedede" => Color.RoyalBlue,
                _ => Color.Gray,
            };

            float alpha = 0.6f + (float)Math.Sin(Scene.TimeActive * 3f) * 0.2f;
            Draw.Rect(Position.X - 16, Position.Y - 48, 32, 48, echoColor * alpha);

            // Health bar
            float hp = (float)health / maxHealth;
            Draw.Rect(Position.X - 16, Position.Y - 56, 32 * hp, 4, Color.Red);
            Draw.HollowRect(Position.X - 16, Position.Y - 56, 32, 4, Color.White);
        }
    }

    #endregion

    #region Dark Star

    /// <summary>
    /// A dark star that orbits and fires beams.
    /// </summary>
    [Tracked]
    public class DXDarkStar : Entity
    {
        private float orbitRadius;
        private float duration;
        private float elapsed;
        private float angle;
        private float fireTimer;
        private Vector2 center;

        public DXDarkStar(Vector2 center, float radius, float duration)
            : base(center + new Vector2(radius, 0))
        {
            this.center = center;
            orbitRadius = radius;
            this.duration = duration;
            elapsed = 0f;
            angle = 0f;
            fireTimer = 0f;

            Collider = new Circle(14f);
            Add(new PlayerCollider(p => p.Die((p.Position - Position).SafeNormalize())));
            Depth = -9999;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;
            angle += 2f * Engine.DeltaTime;
            Position = center + Calc.AngleToVector(angle, orbitRadius);

            fireTimer += Engine.DeltaTime;
            if (fireTimer >= 0.8f)
            {
                fireTimer = 0f;
                // Fire 4 beams in cardinal directions
                for (int i = 0; i < 4; i++)
                {
                    float beamAngle = MathHelper.PiOver2 * i + angle;
                    Scene?.Add(new DXBossProjectile(Position, Calc.AngleToVector(beamAngle, 250f), "shadow"));
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(elapsed * 6f) * 0.3f + 0.7f;
            Draw.Circle(Position, 14f, Color.DarkViolet * pulse, 12);
            Draw.Circle(Position, 8f, Color.Black, 8);
            Draw.Circle(Position, 4f, Color.MediumPurple * pulse, 6);
        }
    }

    #endregion

    #region Null Field

    /// <summary>
    /// Area that disables player dash when inside.
    /// </summary>
    [Tracked]
    public class DXNullField : Entity
    {
        private float radius;
        private float duration;
        private float elapsed;

        public DXNullField(Vector2 position, float radius, float duration)
            : base(position)
        {
            this.radius = radius;
            this.duration = duration;
            elapsed = 0f;
            Depth = -9992;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null)
            {
                float dist = Vector2.Distance(player.Position, Position);
                if (dist < radius)
                {
                    // Drain player speed to prevent dashing
                    if (player.Speed.Length() > 200f)
                    {
                        player.Speed *= 0.85f;
                    }
                }
            }

            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float alpha = (1f - elapsed / duration) * 0.4f;
            Draw.Circle(Position, radius, Color.DarkSlateGray * alpha, 20);
            Draw.Circle(Position, radius * 0.7f, Color.Black * alpha * 0.5f, 16);
        }
    }

    #endregion

    #region Warp Indicator

    /// <summary>
    /// Visual indicator showing where a space warp will occur.
    /// </summary>
    [Tracked]
    public class DXWarpIndicator : Entity
    {
        private float duration;
        private float elapsed;

        public DXWarpIndicator(Vector2 position, float duration)
            : base(position)
        {
            this.duration = duration;
            elapsed = 0f;
            Depth = -10002;
        }

        public override void Update()
        {
            base.Update();
            elapsed += Engine.DeltaTime;
            if (elapsed >= duration) RemoveSelf();
        }

        public override void Render()
        {
            float progress = elapsed / duration;
            float radius = 30f * progress;
            float alpha = 1f - progress;
            Draw.Circle(Position, radius, Color.Cyan * alpha, 16);
            Draw.Circle(Position, radius * 0.5f, Color.White * alpha, 10);
        }
    }

    #endregion
}
