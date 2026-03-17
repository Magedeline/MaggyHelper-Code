using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MaggyHelper
{
    // =============================================
    // LavaGeyser - Periodic lava eruption
    // =============================================
    [CustomEntity("MaggyHelper/LavaGeyser")]
    [Tracked]
    public class LavaGeyser : Entity
    {
        private float interval;
        private float duration;
        private float geyserHeight;
        private float timer;
        private bool erupting = false;
        private float eruptTimer = 0f;
        private float geyserWidth;
        private ParticleType lavaParticle;

        public LavaGeyser(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            interval = data.Float("interval", 3f);
            duration = data.Float("duration", 1.5f);
            geyserHeight = data.Float("height", 80f);
            geyserWidth = data.Float("width", 16f);
            timer = data.Float("startDelay", 0f);
            Depth = -200;

            lavaParticle = new ParticleType
            {
                Color = Color.OrangeRed,
                Color2 = Color.Yellow,
                Size = 2f,
                SpeedMin = 20f,
                SpeedMax = 50f,
                LifeMin = 0.3f,
                LifeMax = 0.6f,
                DirectionRange = 0.5f
            };
        }

        public override void Update()
        {
            base.Update();
            timer -= Engine.DeltaTime;

            if (erupting)
            {
                eruptTimer -= Engine.DeltaTime;
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    if (player.X > X - geyserWidth / 2 && player.X < X + geyserWidth / 2 &&
                        player.Y < Y && player.Y > Y - geyserHeight)
                    {
                        player.Die(Vector2.UnitY);
                    }
                }

                if (Scene.OnInterval(0.05f))
                {
                    SceneAs<Level>().Particles.Emit(lavaParticle,
                        Position + new Vector2(Calc.Random.Range(-geyserWidth / 2, geyserWidth / 2), -geyserHeight),
                        -Vector2.UnitY.Angle());
                }

                if (eruptTimer <= 0)
                {
                    erupting = false;
                    timer = interval;
                }
            }
            else if (timer <= 0)
            {
                erupting = true;
                eruptTimer = duration;
                Audio.Play("event:/game/general/fallblock_shake", Position);
                (Scene as Level)?.Shake(0.15f);
            }
        }

        public override void Render()
        {
            // Base vent
            Draw.Rect(X - geyserWidth / 2, Y - 4f, geyserWidth, 4f, Color.DarkRed * 0.8f);

            if (erupting)
            {
                float progress = eruptTimer / duration;
                float currentHeight = geyserHeight * Math.Min(1f, (1f - Math.Abs(progress - 0.5f) * 2f) * 2f);
                for (float y = 0; y < currentHeight; y += 4f)
                {
                    float wobble = (float)Math.Sin(y * 0.1f + Scene.TimeActive * 10f) * 3f;
                    float alpha = 1f - (y / geyserHeight) * 0.5f;
                    Draw.Rect(X - geyserWidth / 2 + wobble, Y - y - 4f, geyserWidth, 4f, Color.OrangeRed * alpha * 0.7f);
                }
            }
            else if (timer < 1f)
            {
                // Warning shake
                float warn = (float)Math.Sin(timer * 20f) * 2f;
                Draw.Rect(X - geyserWidth / 2 + warn, Y - 4f, geyserWidth, 4f, Color.Red * 0.8f);
            }
        }
    }

    // =============================================
    // CrumblingCeiling - Falls when player passes under
    // =============================================
    [CustomEntity("MaggyHelper/CrumblingCeiling")]
    [Tracked]
    public class CrumblingCeiling : Solid
    {
        private bool triggered = false;
        private bool falling = false;
        private float fallDelay;
        private float fallSpeed = 0f;
        private bool respawns;
        private float respawnTime;
        private Vector2 startPos;

        public CrumblingCeiling(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, safe: false)
        {
            fallDelay = data.Float("fallDelay", 0.5f);
            respawns = data.Bool("respawns", true);
            respawnTime = data.Float("respawnTime", 3f);
            startPos = Position;
            Depth = 0;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();

            if (!triggered && !falling && player != null)
            {
                // Check if player is below this block
                if (player.X > Left && player.X < Right && player.Y > Bottom && player.Y < Bottom + 32f)
                {
                    triggered = true;
                    Add(new Coroutine(FallRoutine()));
                }
            }

            if (falling)
            {
                fallSpeed += 300f * Engine.DeltaTime;
                MoveV(fallSpeed * Engine.DeltaTime);

                if (Top > SceneAs<Level>().Bounds.Bottom + 32f)
                {
                    if (respawns)
                    {
                        Add(new Coroutine(RespawnRoutine()));
                    }
                    else
                    {
                        RemoveSelf();
                    }
                }
            }
        }

        private IEnumerator FallRoutine()
        {
            // Shake warning
            float shake = 0f;
            for (float t = 0; t < fallDelay; t += Engine.DeltaTime)
            {
                shake = Calc.Random.Range(-1f, 1f);
                Position = startPos + new Vector2(shake, 0);
                yield return null;
            }

            Audio.Play("event:/game/general/fallblock_shake", Position);
            falling = true;
            fallSpeed = 10f;
        }

        private IEnumerator RespawnRoutine()
        {
            falling = false;
            triggered = false;
            Visible = false;
            Collidable = false;
            MoveTo(startPos);
            fallSpeed = 0f;

            yield return respawnTime;

            Visible = true;
            Collidable = true;
        }

        public override void Render()
        {
            Draw.Rect(Collider, Color.DimGray * 0.8f);
            if (triggered && !falling)
            {
                Draw.HollowRect(Collider, Color.Red * 0.5f);
            }
        }
    }

    // =============================================
    // ToxicFog - Slowly damages the player
    // =============================================
    [CustomEntity("MaggyHelper/ToxicFog")]
    [Tracked]
    public class ToxicFog : Entity
    {
        private float damageInterval;
        private float timer;
        private int damage;
        private Color fogColor;

        public ToxicFog(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            damageInterval = data.Float("damageInterval", 1f);
            damage = data.Int("damage", 1);
            fogColor = Calc.HexToColor(data.Attr("color", "44ff44"));
            Collider = new Hitbox(data.Width, data.Height);
            Depth = -500;
            timer = damageInterval;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && CollideCheck(player))
            {
                timer -= Engine.DeltaTime;
                if (timer <= 0)
                {
                    timer = damageInterval;
                    // Use session flag or direct damage
                    SceneAs<Level>().Session.SetFlag("toxic_damage", true);
                    Add(new Coroutine(ClearFlag()));
                }
            }
        }

        private IEnumerator ClearFlag()
        {
            yield return 0.1f;
            SceneAs<Level>().Session.SetFlag("toxic_damage", false);
        }

        public override void Render()
        {
            float alpha = 0.15f + (float)Math.Sin(Scene.TimeActive * 0.5f) * 0.05f;
            Draw.Rect(Collider, fogColor * alpha);
        }
    }

    // =============================================
    // LaserGrid - Cycling laser grid
    // =============================================
    [CustomEntity("MaggyHelper/LaserGrid")]
    [Tracked]
    public class LaserGrid : Entity
    {
        private float onTime;
        private float offTime;
        private float timer;
        private bool active = true;
        private bool horizontal;
        private float gridSpacing;
        private Color laserColor;

        public LaserGrid(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            onTime = data.Float("onTime", 2f);
            offTime = data.Float("offTime", 1.5f);
            horizontal = data.Bool("horizontal", false);
            gridSpacing = data.Float("spacing", 16f);
            laserColor = Calc.HexToColor(data.Attr("color", "ff0000"));
            timer = onTime;

            Collider = new Hitbox(data.Width, data.Height);
            Depth = -300;
        }

        public override void Update()
        {
            base.Update();
            timer -= Engine.DeltaTime;
            if (timer <= 0)
            {
                active = !active;
                timer = active ? onTime : offTime;
                if (active) Audio.Play("event:/game/general/assist_screenbottom", Position);
            }

            if (active)
            {
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null && CollideCheck(player))
                {
                    player.Die((player.Position - Center).SafeNormalize());
                }
            }
        }

        public override void Render()
        {
            if (!active)
            {
                Draw.Rect(Collider, laserColor * 0.05f);
                return;
            }

            if (horizontal)
            {
                for (float y = 0; y < Collider.Height; y += gridSpacing)
                {
                    Draw.Line(Position + new Vector2(0, y), Position + new Vector2(Collider.Width, y), laserColor * 0.8f, 1f);
                }
            }
            else
            {
                for (float x = 0; x < Collider.Width; x += gridSpacing)
                {
                    Draw.Line(Position + new Vector2(x, 0), Position + new Vector2(x, Collider.Height), laserColor * 0.8f, 1f);
                }
            }
        }
    }

    // =============================================
    // ShatterIce - Breakable ice covering
    // =============================================
    [CustomEntity("MaggyHelper/ShatterIce")]
    [Tracked]
    public class ShatterIce : Solid
    {
        private bool shattered = false;

        public ShatterIce(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, safe: false)
        {
            Depth = -5;
        }

        public override void Update()
        {
            base.Update();
            if (shattered) return;

            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
            {
                // Break on dash
                if (player.StateMachine.State == Player.StDash && CollideCheck(player))
                {
                    Shatter();
                }
            }

            // Break with fire flag
            if (SceneAs<Level>().Session.GetFlag("fire_pillar_active"))
            {
                Shatter();
            }
        }

        private void Shatter()
        {
            shattered = true;
            Audio.Play("event:/game/general/wall_break_ice", Position);
            Collidable = false;
            Visible = false;
            (Scene as Level)?.Shake(0.1f);
        }

        public override void Render()
        {
            Draw.Rect(Collider, Color.LightCyan * 0.6f);
            Draw.HollowRect(Collider, Color.White * 0.4f);
        }
    }

    // =============================================
    // SandCurrent - Quicksand that pulls player down
    // =============================================
    [CustomEntity("MaggyHelper/SandCurrent")]
    [Tracked]
    public class SandCurrent : Entity
    {
        private float sinkSpeed;
        private float sinkStrength;

        public SandCurrent(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            sinkSpeed = data.Float("sinkSpeed", 30f);
            sinkStrength = data.Float("sinkStrength", 0.5f);
            Collider = new Hitbox(data.Width, data.Height);
            Depth = 100;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && CollideCheck(player))
            {
                player.Speed.Y += sinkSpeed * Engine.DeltaTime;
                player.Speed.X *= (1f - sinkStrength * Engine.DeltaTime);

                // Kill if fully submerged
                if (player.Y > Bottom - 4f)
                {
                    player.Die(Vector2.UnitY);
                }
            }
        }

        public override void Render()
        {
            float wobble = (float)Math.Sin(Scene.TimeActive * 0.5f) * 2f;
            Draw.Rect(Position, Collider.Width, Collider.Height, Color.SandyBrown * 0.5f);
            Draw.Rect(Position.X + wobble, Position.Y, Collider.Width, 4f, Color.Tan * 0.3f);
        }
    }

    // =============================================
    // StormCloud - Follows player, strikes lightning
    // =============================================
    [CustomEntity("MaggyHelper/StormCloud")]
    [Tracked]
    public class StormCloud : Entity
    {
        private float followSpeed;
        private float strikeInterval;
        private float timer;
        private float strikeWarning = 0f;
        private Vector2 strikeTarget;
        private bool striking = false;

        public StormCloud(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            followSpeed = data.Float("followSpeed", 40f);
            strikeInterval = data.Float("strikeInterval", 3f);
            timer = strikeInterval;
            Depth = -2000;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player == null) return;

            // Follow player horizontally above them
            float targetX = player.X;
            float targetY = player.Y - 60f;
            Position.X = Calc.Approach(Position.X, targetX, followSpeed * Engine.DeltaTime);
            Position.Y = Calc.Approach(Position.Y, targetY, followSpeed * 0.5f * Engine.DeltaTime);

            timer -= Engine.DeltaTime;
            if (timer <= 0 && !striking)
            {
                striking = true;
                strikeWarning = 0.5f;
                strikeTarget = new Vector2(Position.X, player.Y);
            }

            if (striking)
            {
                strikeWarning -= Engine.DeltaTime;
                if (strikeWarning <= 0)
                {
                    // Strike!
                    if (Math.Abs(player.X - Position.X) < 12f)
                    {
                        player.Die(-Vector2.UnitY);
                    }
                    Audio.Play("event:/game/general/fallblock_impact", Position);
                    (Scene as Level)?.Flash(Color.White * 0.5f);
                    (Scene as Level)?.Shake(0.2f);
                    striking = false;
                    timer = strikeInterval;
                }
            }
        }

        public override void Render()
        {
            // Cloud
            Draw.Rect(X - 20f, Y - 8f, 40f, 12f, Color.DarkGray * 0.8f);
            Draw.Rect(X - 14f, Y - 14f, 28f, 8f, Color.Gray * 0.7f);

            if (striking)
            {
                float alpha = (0.5f - strikeWarning) / 0.5f;
                // Warning line
                Draw.Line(new Vector2(Position.X, Position.Y + 4f),
                          new Vector2(Position.X, strikeTarget.Y), Color.Yellow * alpha * 0.3f, 2f);

                if (strikeWarning <= 0.1f)
                {
                    // Lightning bolt
                    Vector2 from = Position + new Vector2(0, 4f);
                    Vector2 to = strikeTarget;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 mid = Vector2.Lerp(from, to, (i + 1f) / 6f) + new Vector2(Calc.Random.Range(-6f, 6f), 0);
                        Draw.Line(from, mid, Color.Yellow, 3f);
                        from = mid;
                    }
                    Draw.Line(from, to, Color.Yellow, 3f);
                }
            }
        }
    }

    // =============================================
    // VolcanicRock - Falling rocks from above
    // =============================================
    [CustomEntity("MaggyHelper/VolcanicRock")]
    [Tracked]
    public class VolcanicRock : Entity
    {
        private float spawnRate;
        private float timer;
        private float zoneWidth;

        public VolcanicRock(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            spawnRate = data.Float("spawnRate", 1f);
            zoneWidth = data.Float("zoneWidth", 200f);
            timer = spawnRate;
            Depth = -1000;
        }

        public override void Update()
        {
            base.Update();
            timer -= Engine.DeltaTime;
            if (timer <= 0)
            {
                timer = spawnRate + Calc.Random.Range(-0.3f, 0.3f);
                float x = Position.X + Calc.Random.Range(-zoneWidth / 2, zoneWidth / 2);
                Scene.Add(new FallingRock(new Vector2(x, Position.Y)));
            }
        }
    }

    public class FallingRock : Actor
    {
        private float speed = 0f;
        private float size;

        public FallingRock(Vector2 position)
            : base(position)
        {
            size = Calc.Random.Range(4f, 10f);
            Collider = new Hitbox(size, size, -size / 2, -size / 2);
            Depth = -500;
        }

        public override void Update()
        {
            base.Update();
            speed += 300f * Engine.DeltaTime;
            MoveV(speed * Engine.DeltaTime);

            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && CollideCheck(player))
            {
                player.Die((player.Position - Position).SafeNormalize());
            }

            if (CollideCheck<Solid>() || Y > SceneAs<Level>().Bounds.Bottom + 32f)
            {
                Audio.Play("event:/game/general/fallblock_impact", Position);
                RemoveSelf();
            }
        }

        public override void Render()
        {
            Draw.Rect(Position - Vector2.One * size / 2, size, size, Color.DarkGray * 0.8f);
        }
    }

    // =============================================
    // AcidPool - Rising/falling damaging liquid
    // =============================================
    [CustomEntity("MaggyHelper/AcidPool")]
    [Tracked]
    public class AcidPool : Entity
    {
        private float minHeight;
        private float maxHeight;
        private float cycleSpeed;
        private float currentHeight;
        private Color acidColor;

        public AcidPool(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            minHeight = data.Float("minHeight", 8f);
            maxHeight = data.Float("maxHeight", 48f);
            cycleSpeed = data.Float("cycleSpeed", 0.5f);
            acidColor = Calc.HexToColor(data.Attr("color", "88ff00"));
            Collider = new Hitbox(data.Width, maxHeight);
            Depth = -100;
        }

        public override void Update()
        {
            base.Update();
            float t = (float)(Math.Sin(Scene.TimeActive * cycleSpeed * Math.PI * 2) * 0.5 + 0.5);
            currentHeight = MathHelper.Lerp(minHeight, maxHeight, t);

            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
            {
                float acidTop = Position.Y + Collider.Height - currentHeight;
                if (player.X > Left && player.X < Right && player.Bottom > acidTop)
                {
                    player.Die(Vector2.UnitY);
                }
            }
        }

        public override void Render()
        {
            float acidTop = Position.Y + Collider.Height - currentHeight;
            float wobble = (float)Math.Sin(Scene.TimeActive * 3f) * 2f;
            Draw.Rect(Position.X, acidTop + wobble, Collider.Width, currentHeight, acidColor * 0.5f);
            Draw.Rect(Position.X, acidTop + wobble, Collider.Width, 3f, acidColor * 0.8f);
        }
    }

    // =============================================
    // VineTrap - Grabs and holds player
    // =============================================
    [CustomEntity("MaggyHelper/VineTrap")]
    [Tracked]
    public class VineTrap : Entity
    {
        private float holdTime;
        private float timer = 0f;
        private bool holding = false;

        public VineTrap(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            holdTime = data.Float("holdTime", 1f);
            Collider = new Hitbox(data.Width, data.Height);
            Depth = 100;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();

            if (holding)
            {
                timer -= Engine.DeltaTime;
                if (player != null)
                {
                    player.Speed = Vector2.Zero;

                    // Mash to escape faster
                    if (Input.Jump.Pressed || Input.Dash.Pressed)
                    {
                        timer -= 0.3f;
                    }
                }

                if (timer <= 0)
                {
                    holding = false;
                    if (player != null)
                        player.StateMachine.State = Player.StNormal;
                }
            }
            else if (player != null && CollideCheck(player))
            {
                holding = true;
                timer = holdTime;
                player.StateMachine.State = Player.StDummy;
                player.Speed = Vector2.Zero;
                Audio.Play("event:/game/general/assist_screenbottom", Position);
            }
        }

        public override void Render()
        {
            Color c = holding ? Color.DarkGreen : Color.ForestGreen;
            Draw.Rect(Collider, c * 0.4f);
            // Draw vine tendrils
            for (float x = 0; x < Collider.Width; x += 8f)
            {
                float wave = (float)Math.Sin((x + Scene.TimeActive * 2f) * 0.5f) * 4f;
                Draw.Line(Position + new Vector2(x, 0), Position + new Vector2(x + wave, Collider.Height), c * 0.6f, 1f);
            }
        }
    }

    // =============================================
    // MagneticField - Disables dash/float
    // =============================================
    [CustomEntity("MaggyHelper/MagneticField")]
    [Tracked]
    public class MagneticField : Entity
    {
        private bool disableDash;
        private bool disableFloat;
        private bool disableClimb;

        public MagneticField(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            disableDash = data.Bool("disableDash", true);
            disableFloat = data.Bool("disableFloat", true);
            disableClimb = data.Bool("disableClimb", false);
            Collider = new Hitbox(data.Width, data.Height);
            Depth = 500;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player == null) return;

            if (CollideCheck(player))
            {
                if (disableDash)
                    SceneAs<Level>().Session.SetFlag("disable_dash", true);
                if (disableFloat)
                    SceneAs<Level>().Session.SetFlag("disable_float", true);
                if (disableClimb)
                    SceneAs<Level>().Session.SetFlag("disable_climb", true);
            }
            else
            {
                SceneAs<Level>().Session.SetFlag("disable_dash", false);
                SceneAs<Level>().Session.SetFlag("disable_float", false);
                SceneAs<Level>().Session.SetFlag("disable_climb", false);
            }
        }

        public override void Render()
        {
            Draw.Rect(Collider, Color.MediumPurple * 0.1f);
            // Magnetic field lines
            for (float x = 0; x < Collider.Width; x += 16f)
            {
                for (float y = 0; y < Collider.Height; y += 16f)
                {
                    float wave = (float)Math.Sin((x + y + Scene.TimeActive * 2f) * 0.2f) * 3f;
                    Draw.Line(Position + new Vector2(x, y), Position + new Vector2(x + 8f, y + wave), Color.MediumPurple * 0.15f);
                }
            }
        }
    }

    // =============================================
    // DarkZone - Complete darkness around player
    // =============================================
    [CustomEntity("MaggyHelper/DarkZone")]
    [Tracked]
    public class DarkZone : Entity
    {
        private float playerLightRadius;

        public DarkZone(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            playerLightRadius = data.Float("playerLightRadius", 40f);
            Collider = new Hitbox(data.Width, data.Height);
            Depth = -100000;
        }

        public override void Update()
        {
            base.Update();
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && CollideCheck(player))
            {
                SceneAs<Level>().Session.SetFlag("in_dark_zone", true);

                // Check for lantern
                float radius = playerLightRadius;
                if (SceneAs<Level>().Session.GetFlag("lantern_carried"))
                {
                    radius *= 3f;
                }
                SceneAs<Level>().Lighting.Alpha = Calc.Approach(SceneAs<Level>().Lighting.Alpha, 0.95f, Engine.DeltaTime);
            }
            else
            {
                if (SceneAs<Level>().Session.GetFlag("in_dark_zone"))
                {
                    SceneAs<Level>().Session.SetFlag("in_dark_zone", false);
                    SceneAs<Level>().Lighting.Alpha = Calc.Approach(SceneAs<Level>().Lighting.Alpha, 0f, Engine.DeltaTime * 2f);
                }
            }
        }
    }
}
