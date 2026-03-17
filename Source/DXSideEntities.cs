namespace MaggyHelper
{
    #region DX Void Platform
    /// <summary>
    /// A platform that phases in and out of existence on a timer.
    /// When phased out, the player falls through it.
    /// DX-Side exclusive platforming element.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXVoidPlatform")]
    public class DXVoidPlatform : CelesteJumpThru
    {
        private float phaseInterval;
        private float phaseTimer;
        private bool isPhased;
        private float phaseDuration;
        private Color activeColor;
        private Color fadedColor;

        public DXVoidPlatform(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, false)
        {
            phaseInterval = data.Float("phaseInterval", 2.0f);
            phaseDuration = data.Float("phaseDuration", 1.0f);
            phaseTimer = data.Float("phaseOffset", 0f);
            isPhased = false;
            activeColor = Color.DarkViolet;
            fadedColor = Color.DarkViolet * 0.2f;
        }

        public override void Update()
        {
            base.Update();
            phaseTimer += Engine.DeltaTime;

            float cycle = phaseInterval + phaseDuration;
            float inCycle = phaseTimer % cycle;

            bool shouldPhase = inCycle > phaseInterval;

            if (shouldPhase != isPhased)
            {
                isPhased = shouldPhase;
                Collidable = !isPhased;
                Visible = !isPhased;
            }
        }

        public override void Render()
        {
            if (!isPhased)
            {
                Draw.Rect(Position.X, Position.Y, Width, 8, activeColor);
                Draw.HollowRect(Position.X, Position.Y, Width, 8, Color.MediumPurple);
            }
            else
            {
                Draw.Rect(Position.X, Position.Y, Width, 8, fadedColor);
            }
        }
    }
    #endregion

    #region DX Gravity Shifter
    /// <summary>
    /// A trigger zone that reverses or alters gravity for the player.
    /// DX-Side exclusive platforming challenge.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXGravityShifter")]
    public class DXGravityShifter : Entity
    {
        private float gravityMultiplier;
        private float width;
        private float height;
        private bool isActive;
        private Color zoneColor;

        public DXGravityShifter(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            width = data.Width;
            height = data.Height;
            gravityMultiplier = data.Float("gravityMultiplier", -1.0f);
            isActive = data.Bool("startActive", true);
            zoneColor = gravityMultiplier < 0 ? Color.Cyan * 0.3f : Color.Orange * 0.3f;

            Collider = new Hitbox(width, height);
            Depth = 100;
        }

        public override void Update()
        {
            base.Update();

            if (!isActive) return;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null && Collider.Collide(player))
            {
                // Apply gravity modification
                player.Speed = new Vector2(player.Speed.X,
                    player.Speed.Y + gravityMultiplier * 20f * Engine.DeltaTime);
            }
        }

        public override void Render()
        {
            if (isActive)
            {
                Draw.Rect(Position.X, Position.Y, width, height, zoneColor);
                Draw.HollowRect(Position.X, Position.Y, width, height,
                    gravityMultiplier < 0 ? Color.Cyan : Color.Orange);

                // Direction arrows
                float arrowY = gravityMultiplier < 0 ? Position.Y + height / 2 : Position.Y + height / 2;
                float arrowDir = gravityMultiplier < 0 ? -1f : 1f;
                for (float x = Position.X + 20; x < Position.X + width - 20; x += 40)
                {
                    Draw.Line(x, arrowY, x, arrowY + arrowDir * 20,
                        Color.White * 0.5f, 2f);
                }
            }
        }
    }
    #endregion

    #region DX Dimensional Warp Block
    /// <summary>
    /// A solid block that teleports the player to a linked position on dash.
    /// DX-Side exclusive movement mechanic.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXDimensionalWarpBlock")]
    public class DXDimensionalWarpBlock : Solid
    {
        private Vector2 warpTarget;
        private float cooldown;
        private float cooldownTimer;
        private bool hasNodes;
        private Color blockColor;

        public DXDimensionalWarpBlock(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, true)
        {
            cooldown = data.Float("cooldown", 1.0f);
            cooldownTimer = 0f;

            if (data.Nodes != null && data.Nodes.Length > 0)
            {
                warpTarget = data.Nodes[0] + offset;
                hasNodes = true;
            }
            else
            {
                warpTarget = data.Position + offset;
                hasNodes = false;
            }

            blockColor = Color.DeepSkyBlue;
            Add(new DashListener { OnDash = OnDash });
        }

        private void OnDash(Vector2 dir)
        {
            if (cooldownTimer > 0f || !hasNodes) return;

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null && CollideCheck(player))
            {
                // Teleport player to warp target
                Vector2 offset = player.Position - Position;
                player.Position = warpTarget + offset;
                cooldownTimer = cooldown;

                Audio.Play("event:/game/general/wall_break_stone", Position);
                SceneAs<global::Celeste.Level>()?.Shake(0.2f);
            }
        }

        public override void Update()
        {
            base.Update();
            if (cooldownTimer > 0f)
                cooldownTimer -= Engine.DeltaTime;
        }

        public override void Render()
        {
            bool onCooldown = cooldownTimer > 0f;
            Color c = onCooldown ? Color.DarkSlateGray : blockColor;
            Draw.Rect(Position.X, Position.Y, Width, Height, c * 0.6f);
            Draw.HollowRect(Position.X, Position.Y, Width, Height, c);

            // Warp indicator
            if (!onCooldown && hasNodes)
            {
                float pulse = (float)Math.Sin(Scene.TimeActive * 4f) * 0.2f + 0.8f;
                Vector2 center = Position + new Vector2(Width / 2, Height / 2);
                Draw.Circle(center, Math.Min(Width, Height) / 3f, Color.Cyan * pulse, 12);
            }
        }
    }
    #endregion

    #region DX Corruption Zone
    /// <summary>
    /// A hazardous area that deals damage to the player over time.
    /// Visual corruption effect on the screen while inside.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXCorruptionZone")]
    public class DXCorruptionZone : Entity
    {
        private float width;
        private float height;
        private float damageInterval;
        private float damageTimer;
        private float corruptionIntensity;

        public DXCorruptionZone(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            width = data.Width;
            height = data.Height;
            damageInterval = data.Float("damageInterval", 1.0f);
            corruptionIntensity = data.Float("intensity", 1.0f);
            damageTimer = 0f;

            Collider = new Hitbox(width, height);
            Depth = 50;
        }

        public override void Update()
        {
            base.Update();

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null && Collider.Collide(player))
            {
                damageTimer += Engine.DeltaTime;
                if (damageTimer >= damageInterval)
                {
                    damageTimer = 0f;
                    player.Die((player.Position - Center).SafeNormalize());
                }
            }
            else
            {
                damageTimer = 0f;
            }
        }

        public override void Render()
        {
            float pulse = (float)Math.Sin(Scene.TimeActive * 3f * corruptionIntensity) * 0.15f + 0.25f;
            Draw.Rect(Position.X, Position.Y, width, height, Color.DarkMagenta * pulse);

            // Particle-like effect
            for (int i = 0; i < 5; i++)
            {
                float seed = Scene.TimeActive + i * 7.37f;
                float px = Position.X + ((float)Math.Sin(seed * 1.3f) * 0.5f + 0.5f) * width;
                float py = Position.Y + ((float)Math.Cos(seed * 0.9f) * 0.5f + 0.5f) * height;
                Draw.Circle(new Vector2(px, py), 3f, Color.Magenta * pulse, 6);
            }
        }
    }
    #endregion

    #region DX Time Block
    /// <summary>
    /// A solid block that appears/disappears based on a time cycle.
    /// Synced across all DXTimeBlocks sharing the same group ID.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXTimeBlock")]
    public class DXTimeBlock : Solid
    {
        private int groupId;
        private float cycleTime;
        private float offset;
        private bool isVisible;
        private Color blockColor;

        public DXTimeBlock(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, true)
        {
            groupId = data.Int("groupId", 0);
            cycleTime = data.Float("cycleTime", 3.0f);
            this.offset = data.Float("phaseOffset", 0f);
            blockColor = data.HexColor("color", Color.MediumPurple);
            isVisible = true;
        }

        public override void Update()
        {
            base.Update();

            float t = (Scene.TimeActive + offset) % (cycleTime * 2f);
            bool shouldBeVisible = t < cycleTime;

            // Even groups are opposite of odd groups
            if (groupId % 2 == 1)
                shouldBeVisible = !shouldBeVisible;

            if (shouldBeVisible != isVisible)
            {
                isVisible = shouldBeVisible;
                Collidable = isVisible;
                Visible = isVisible;
            }
        }

        public override void Render()
        {
            if (isVisible)
            {
                Draw.Rect(Position.X, Position.Y, Width, Height, blockColor * 0.8f);
                Draw.HollowRect(Position.X, Position.Y, Width, Height, blockColor);
            }
        }
    }
    #endregion

    #region DX Moving Hazard Platform
    /// <summary>
    /// A platform that moves between nodes and has hazard edges.
    /// Player can stand on it, but touching the sides or bottom kills.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXMovingHazardPlatform")]
    public class DXMovingHazardPlatform : CelesteJumpThru
    {
        private Vector2[] nodes;
        private int currentNode;
        private float speed;
        private float pauseTime;
        private float pauseTimer;
        private bool isPaused;
        private bool hazardSides;

        public DXMovingHazardPlatform(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, false)
        {
            speed = data.Float("speed", 80f);
            pauseTime = data.Float("pauseTime", 0.5f);
            hazardSides = data.Bool("hazardSides", true);

            // Build node list
            var nodeList = new List<Vector2> { data.Position + offset };
            if (data.Nodes != null)
            {
                foreach (var node in data.Nodes)
                    nodeList.Add(node + offset);
            }
            nodes = nodeList.ToArray();
            currentNode = 0;
            isPaused = false;
            pauseTimer = 0f;
        }

        public override void Update()
        {
            base.Update();

            if (isPaused)
            {
                pauseTimer += Engine.DeltaTime;
                if (pauseTimer >= pauseTime)
                {
                    isPaused = false;
                    pauseTimer = 0f;
                }
                return;
            }

            if (nodes.Length <= 1) return;

            int nextNode = (currentNode + 1) % nodes.Length;
            Vector2 target = nodes[nextNode];
            Vector2 dir = (target - Position).SafeNormalize();
            float dist = Vector2.Distance(Position, target);

            if (dist < speed * Engine.DeltaTime)
            {
                MoveTo(target);
                currentNode = nextNode;
                isPaused = true;
            }
            else
            {
                MoveH(dir.X * speed * Engine.DeltaTime);
                MoveV(dir.Y * speed * Engine.DeltaTime);
            }

            // Check for side/bottom collision with player (hazard)
            if (hazardSides)
            {
                var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null)
                {
                    // Check bottom collision
                    if (player.Top < Bottom && player.Bottom > Bottom - 4 &&
                        player.Right > Left && player.Left < Right)
                    {
                        player.Die(Vector2.UnitY);
                    }
                    // Check side collision
                    if (player.Bottom > Top + 4 && player.Top < Bottom)
                    {
                        if ((player.Right > Left && player.Right < Left + 4) ||
                            (player.Left < Right && player.Left > Right - 4))
                        {
                            Vector2 killDir = player.Position.X < Center.X ? -Vector2.UnitX : Vector2.UnitX;
                            player.Die(killDir);
                        }
                    }
                }
            }
        }

        public override void Render()
        {
            // Safe top
            Draw.Rect(Position.X, Position.Y, Width, 4, Color.MediumPurple);
            // Hazard bottom and sides
            if (hazardSides)
            {
                Draw.Rect(Position.X, Position.Y + 4, Width, 4, Color.Red * 0.7f);
                Draw.Rect(Position.X, Position.Y, 2, 8, Color.Red * 0.7f);
                Draw.Rect(Position.X + Width - 2, Position.Y, 2, 8, Color.Red * 0.7f);
            }
        }
    }
    #endregion

    #region DX Void Dash Refill
    /// <summary>
    /// A special DX-Side refill that grants an extra dash with void properties.
    /// The void dash passes through solid objects for a brief moment.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXVoidDashRefill")]
    public class DXVoidDashRefill : Entity
    {
        private Sprite sprite;
        private SineWave floatWave;
        private bool collected;
        private float respawnTimer;
        private float respawnTime;
        private bool oneUse;

        public DXVoidDashRefill(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            respawnTime = data.Float("respawnTime", 2.5f);
            oneUse = data.Bool("oneUse", false);
            collected = false;
            respawnTimer = 0f;

            Collider = new Circle(10f);
            Add(new PlayerCollider(OnPlayer));
            Add(floatWave = new SineWave(0.8f, 0f));
            Add(new BloomPoint(0.5f, 16f));
            Depth = -100;
        }

        private void OnPlayer(global::Celeste.Player player)
        {
            if (collected) return;

            // Refill dashes
            player.RefillDash();
            Audio.Play("event:/game/general/diamond_touch", Position);

            collected = true;
            Visible = false;
            Collidable = false;

            if (!oneUse)
                respawnTimer = respawnTime;
        }

        public override void Update()
        {
            base.Update();

            if (collected && !oneUse)
            {
                respawnTimer -= Engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    collected = false;
                    Visible = true;
                    Collidable = true;
                }
            }
        }

        public override void Render()
        {
            if (!collected)
            {
                float pulse = (float)Math.Sin(Scene.TimeActive * 4f) * 0.3f + 0.7f;
                Draw.Circle(Position, 10f, Color.DarkViolet * pulse, 10);
                Draw.Circle(Position, 5f, Color.MediumPurple * pulse, 8);
                Draw.Circle(Position, 2f, Color.White * pulse, 6);
            }
        }
    }
    #endregion

    #region DX Spike Ball Chain
    /// <summary>
    /// A swinging spike ball on a chain — classic platforming hazard with DX flair.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXSpikeBallChain")]
    public class DXSpikeBallChain : Entity
    {
        private Vector2 anchor;
        private float chainLength;
        private float swingAngle;
        private float swingSpeed;
        private float currentAngle;
        private float ballRadius;

        public DXSpikeBallChain(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            anchor = data.Position + offset;
            chainLength = data.Float("chainLength", 80f);
            swingSpeed = data.Float("swingSpeed", 2.0f);
            ballRadius = data.Float("ballRadius", 12f);
            swingAngle = data.Float("swingAngle", MathHelper.PiOver2);
            currentAngle = 0f;

            Depth = -9000;
        }

        public override void Update()
        {
            base.Update();
            currentAngle = (float)Math.Sin(Scene.TimeActive * swingSpeed) * swingAngle;

            Vector2 ballPos = GetBallPosition();

            var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
            if (player != null && Vector2.Distance(player.Position, ballPos) < ballRadius + 4f)
            {
                player.Die((player.Position - ballPos).SafeNormalize());
            }
        }

        private Vector2 GetBallPosition()
        {
            return anchor + new Vector2(
                (float)Math.Sin(currentAngle) * chainLength,
                (float)Math.Cos(currentAngle) * chainLength);
        }

        public override void Render()
        {
            Vector2 ballPos = GetBallPosition();

            // Draw chain
            Draw.Line(anchor, ballPos, Color.Gray, 2f);

            // Draw ball
            Draw.Circle(ballPos, ballRadius, Color.DarkSlateGray, 12);
            Draw.Circle(ballPos, ballRadius - 2, Color.DarkMagenta, 10);
            Draw.Circle(ballPos, 3f, Color.Red, 6);

            // Draw anchor point
            Draw.Circle(anchor, 4f, Color.Gray, 8);
        }
    }
    #endregion

    #region DX Laser Gate
    /// <summary>
    /// A laser barrier between two anchor points that toggles on/off.
    /// </summary>
    [Tracked]
    [CustomEntity("MaggyHelper/DXLaserGate")]
    public class DXLaserGate : Entity
    {
        private Vector2 endPoint;
        private float onTime;
        private float offTime;
        private float timer;
        private bool isOn;
        private Color laserColor;

        public DXLaserGate(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            onTime = data.Float("onTime", 2.0f);
            offTime = data.Float("offTime", 1.0f);
            isOn = data.Bool("startOn", true);
            laserColor = data.HexColor("color", Color.Red);
            timer = 0f;

            if (data.Nodes != null && data.Nodes.Length > 0)
                endPoint = data.Nodes[0] + offset;
            else
                endPoint = Position + new Vector2(0, 100);

            Depth = -9500;
        }

        public override void Update()
        {
            base.Update();
            timer += Engine.DeltaTime;

            float cycleTime = isOn ? onTime : offTime;
            if (timer >= cycleTime)
            {
                timer = 0f;
                isOn = !isOn;
            }

            if (isOn)
            {
                var player = Scene?.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null)
                {
                    // Line-to-point distance check
                    Vector2 dir = (endPoint - Position).SafeNormalize();
                    float length = Vector2.Distance(Position, endPoint);
                    Vector2 toPlayer = player.Position - Position;
                    float projLen = Vector2.Dot(toPlayer, dir);

                    if (projLen >= 0 && projLen <= length)
                    {
                        Vector2 closest = Position + dir * projLen;
                        if (Vector2.Distance(player.Position, closest) < 6f)
                        {
                            player.Die(new Vector2(-dir.Y, dir.X));
                        }
                    }
                }
            }
        }

        public override void Render()
        {
            // Anchor points
            Draw.Circle(Position, 4f, Color.Gray, 8);
            Draw.Circle(endPoint, 4f, Color.Gray, 8);

            if (isOn)
            {
                float pulse = (float)Math.Sin(Scene.TimeActive * 8f) * 0.15f + 0.85f;
                Draw.Line(Position, endPoint, laserColor * pulse, 4f);
                Draw.Line(Position, endPoint, Color.White * pulse * 0.5f, 1f);
            }
            else
            {
                Draw.Line(Position, endPoint, laserColor * 0.15f, 1f);
            }
        }
    }
    #endregion
}
