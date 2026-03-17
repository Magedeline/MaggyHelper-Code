using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MaggyHelper
{
    // =============================================
    // ShieldEnemy - Has a front-facing shield
    // =============================================
    [CustomEntity("MaggyHelper/ShieldEnemy")]
    [Tracked]
    public class ShieldEnemy : Enemy
    {
        private float speed;
        private int facing = 1;
        private bool shieldBroken = false;
        private int shieldHealth;

        public ShieldEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            speed = data.Float("speed", 30f);
            shieldHealth = data.Int("shieldHealth", 3);
            Collider = new Hitbox(14f, 16f, -7f, -16f);
        }

        public override void Update()
        {
            base.Update();
            if (cachedPlayer != null)
            {
                facing = cachedPlayer.X > X ? 1 : -1;
            }
        }

        public override void TakeDamage(int damage)
        {
            if (!shieldBroken)
            {
                // Check if hit from behind - use cachedPlayer from base
                if (cachedPlayer != null)
                {
                    bool hitFromBehind = (facing == 1 && cachedPlayer.X < X) || (facing == -1 && cachedPlayer.X > X);
                    bool hitFromAbove = cachedPlayer.Y < Y - 12f;
                    if (hitFromBehind || hitFromAbove)
                    {
                        base.TakeDamage(damage);
                    }
                    else
                    {
                        shieldHealth -= damage;
                        if (shieldHealth <= 0)
                        {
                            shieldBroken = true;
                            Audio.Play("event:/game/general/wall_break_ice", Position);
                        }
                    }
                }
            }
            else
            {
                base.TakeDamage(damage);
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(X - 7f, Y - 16f, 14f, 16f, Color.DarkRed * 0.6f);
            if (!shieldBroken)
            {
                float sx = facing == 1 ? X + 5f : X - 9f;
                Draw.Rect(sx, Y - 16f, 4f, 16f, Color.Gold * 0.8f);
            }
        }
    }

    // =============================================
    // MirrorEnemy - Reflects projectiles
    // =============================================
    [CustomEntity("MaggyHelper/MirrorEnemy")]
    [Tracked]
    public class MirrorEnemy : Enemy
    {
        private float reflectRadius;

        public MirrorEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            reflectRadius = data.Float("reflectRadius", 40f);
            Collider = new Hitbox(12f, 12f, -6f, -12f);
        }

        public override void Update()
        {
            base.Update();
            // Reflect nearby projectiles
            foreach (Projectile proj in Scene.Tracker.GetEntities<Projectile>())
            {
                if (proj.DamagesPlayer && Vector2.Distance(Position, proj.Position) < reflectRadius)
                {
                    proj.Velocity = -proj.Velocity;
                    proj.DamagesPlayer = false;
                    Audio.Play("event:/game/general/assist_screenbottom", Position);
                }
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Circle(Position + new Vector2(0, -6f), 8f, Color.Silver * 0.7f, 12);
            Draw.Circle(Position + new Vector2(0, -6f), reflectRadius, Color.Silver * 0.15f, 24);
        }
    }

    // =============================================
    // SplittingEnemy - Splits into smaller ones
    // =============================================
    [CustomEntity("MaggyHelper/SplittingEnemy")]
    [Tracked]
    public class SplittingEnemy : Enemy
    {
        private int splitCount;
        private bool isSmall;
        private float speed;

        public SplittingEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            splitCount = data.Int("splitCount", 2);
            isSmall = data.Bool("isSmall", false);
            speed = data.Float("speed", 30f);
            float size = isSmall ? 6f : 12f;
            Collider = new Hitbox(size * 2, size * 2, -size, -size * 2);
        }

        protected override void Die()
        {
            if (!isSmall && splitCount > 0)
            {
                for (int i = 0; i < splitCount; i++)
                {
                    float angle = (float)i / splitCount * MathHelper.TwoPi;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f;
                    EntityData smallData = new EntityData();
                    SplittingEnemy small = new SplittingEnemy(Position + offset, 1, true, speed * 1.5f);
                    Scene.Add(small);
                }
            }
            base.Die();
        }

        private SplittingEnemy(Vector2 position, int health, bool isSmall, float speed)
            : base(position, health)
        {
            this.isSmall = isSmall;
            this.speed = speed;
            splitCount = 0;
            float size = isSmall ? 6f : 12f;
            Collider = new Hitbox(size * 2, size * 2, -size, -size * 2);
        }

        public override void Render()
        {
            base.Render();
            float size = isSmall ? 5f : 10f;
            Draw.Rect(X - size, Y - size * 2, size * 2, size * 2, Color.Purple * 0.7f);
        }
    }

    // =============================================
    // BurrowingEnemy - Pops out of ground
    // =============================================
    [CustomEntity("MaggyHelper/BurrowingEnemy")]
    [Tracked]
    public class BurrowingEnemy : Enemy
    {
        private float detectionRange;
        private float surfaceTime;
        private float burrowTime;
        private float timer = 0f;
        private bool surfaced = false;
        private Vector2 surfacePos;
        private Vector2 burrowPos;

        public BurrowingEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            detectionRange = data.Float("detectionRange", 80f);
            surfaceTime = data.Float("surfaceTime", 2f);
            burrowTime = data.Float("burrowTime", 3f);
            timer = burrowTime;
            surfacePos = Position;
            burrowPos = Position + new Vector2(0, 16f);
            Position = burrowPos;
            Collider = new Hitbox(12f, 12f, -6f, -12f);
            Collidable = false;
        }

        public override void Update()
        {
            base.Update();
            timer -= Engine.DeltaTime;

            if (!surfaced)
            {
                Collidable = false;
                Position = burrowPos;
                if (cachedPlayer != null && Vector2.Distance(cachedPlayer.Position, surfacePos) < detectionRange && timer <= 0)
                {
                    surfaced = true;
                    timer = surfaceTime;
                    Collidable = true;
                    Audio.Play("event:/game/general/fallblock_shake", Position);
                    (Scene as Level)?.Shake(0.1f);
                }
            }
            else
            {
                Position = Vector2.Lerp(burrowPos, surfacePos, Math.Min(1f, (surfaceTime - timer) * 4f));
                if (timer <= 0)
                {
                    surfaced = false;
                    timer = burrowTime;
                }
            }
        }

        public override void Render()
        {
            if (surfaced)
            {
                base.Render();
                Draw.Rect(X - 6f, Y - 12f, 12f, 12f, Color.SandyBrown * 0.7f);
            }
        }
    }

    // =============================================
    // GhostEnemy - Visible only when player moves away
    // =============================================
    [CustomEntity("MaggyHelper/GhostEnemy")]
    [Tracked]
    public class GhostEnemy : Enemy
    {
        private float chaseSpeed;
        private bool playerLooking = false;

        public GhostEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 3))
        {
            chaseSpeed = data.Float("chaseSpeed", 50f);
            Collider = new Hitbox(12f, 16f, -6f, -16f);
        }

        public override void Update()
        {
            base.Update();
            if (cachedPlayer == null) return;

            // Check if player is looking at ghost (moving toward it)
            Vector2 toGhost = (Position - cachedPlayer.Position);
            playerLooking = Vector2.Dot(toGhost, cachedPlayer.Speed) > 0 || cachedPlayer.Speed.Length() < 10f;

            if (!playerLooking)
            {
                // Chase player
                Vector2 dir = (cachedPlayer.Position - Position).SafeNormalize();
                Position += dir * chaseSpeed * Engine.DeltaTime;
            }

            Collidable = !playerLooking;
        }

        public override void Render()
        {
            float alpha = playerLooking ? 0.15f : 0.7f;
            Draw.Rect(X - 6f, Y - 16f, 12f, 16f, Color.White * alpha);
            Draw.Rect(X - 4f, Y - 14f, 3f, 3f, Color.Black * alpha);
            Draw.Rect(X + 1f, Y - 14f, 3f, 3f, Color.Black * alpha);
        }
    }

    // =============================================
    // CloneEnemy - Mimics player from 2s ago
    // =============================================
    [CustomEntity("MaggyHelper/CloneEnemy")]
    [Tracked]
    public class CloneEnemy : Enemy
    {
        private Queue<Vector2> positionHistory = new Queue<Vector2>();
        private float recordInterval = 0.05f;
        private float recordTimer = 0f;
        private int delay; // frames of delay
        private Color tint;

        public CloneEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            delay = (int)(data.Float("delaySeconds", 2f) / recordInterval);
            tint = Calc.HexToColor(data.Attr("color", "8800ff"));
            Collider = new Hitbox(8f, 12f, -4f, -12f);
        }

        public override void Update()
        {
            base.Update();
            if (cachedPlayer == null) return;

            recordTimer -= Engine.DeltaTime;
            if (recordTimer <= 0)
            {
                recordTimer = recordInterval;
                positionHistory.Enqueue(cachedPlayer.Position);

                if (positionHistory.Count > delay)
                {
                    Position = positionHistory.Dequeue();
                }
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(X - 4f, Y - 12f, 8f, 12f, tint * 0.6f);
        }
    }

    // =============================================
    // SwarmEnemy - Flock of small enemies
    // =============================================
    [CustomEntity("MaggyHelper/SwarmEnemy")]
    [Tracked]
    public class SwarmEnemy : Enemy
    {
        private struct SwarmMember
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public bool Alive;
        }

        private SwarmMember[] members;
        private float chaseRange;

        public SwarmEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("count", 8))
        {
            int count = data.Int("count", 8);
            chaseRange = data.Float("chaseRange", 100f);
            members = new SwarmMember[count];
            for (int i = 0; i < count; i++)
            {
                members[i] = new SwarmMember
                {
                    Position = Position + new Vector2(Calc.Random.Range(-20f, 20f), Calc.Random.Range(-20f, 20f)),
                    Velocity = Vector2.Zero,
                    Alive = true
                };
            }
            Collider = new Hitbox(40f, 40f, -20f, -20f);
        }

        public override void Update()
        {
            base.Update();
            Vector2 center = Vector2.Zero;
            int alive = 0;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Alive) { center += members[i].Position; alive++; }
            }
            if (alive == 0) { Die(); return; }
            center /= alive;
            Position = center;

            for (int i = 0; i < members.Length; i++)
            {
                if (!members[i].Alive) continue;
                // Cohesion + chase
                Vector2 toCenter = (center - members[i].Position) * 0.02f;
                Vector2 chase = Vector2.Zero;
                if (cachedPlayer != null && Vector2.Distance(center, cachedPlayer.Position) < chaseRange)
                    chase = (cachedPlayer.Position - members[i].Position).SafeNormalize() * 40f;

                members[i].Velocity = (members[i].Velocity + toCenter + chase) * 0.98f;
                members[i].Position += members[i].Velocity * Engine.DeltaTime;
            }
        }

        public override void TakeDamage(int damage)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Alive) { members[i].Alive = false; Health--; break; }
            }
            if (Health <= 0) Die();
        }

        public override void Render()
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Alive)
                    Draw.Rect(members[i].Position - Vector2.One * 2f, 4f, 4f, Color.DarkGreen * 0.8f);
            }
        }
    }

    // =============================================
    // ElectricEnemy - Releases shockwave rings
    // =============================================
    [CustomEntity("MaggyHelper/ElectricEnemy")]
    [Tracked]
    public class ElectricEnemy : Enemy
    {
        private float chargeTime;
        private float chargeTimer;
        private float shockRadius = 0f;
        private float maxShockRadius;
        private bool shocking = false;
        private float shockSpeed;

        public ElectricEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            chargeTime = data.Float("chargeTime", 3f);
            maxShockRadius = data.Float("shockRadius", 60f);
            shockSpeed = data.Float("shockSpeed", 100f);
            chargeTimer = chargeTime;
            Collider = new Hitbox(14f, 14f, -7f, -14f);
        }

        public override void Update()
        {
            base.Update();
            if (shocking)
            {
                shockRadius += shockSpeed * Engine.DeltaTime;
                if (cachedPlayer != null)
                {
                    float dist = Vector2.Distance(Position, cachedPlayer.Position);
                    if (Math.Abs(dist - shockRadius) < 6f)
                    {
                        OnHitPlayer(cachedPlayer);
                    }
                }
                if (shockRadius >= maxShockRadius)
                {
                    shocking = false;
                    shockRadius = 0f;
                    chargeTimer = chargeTime;
                }
            }
            else
            {
                chargeTimer -= Engine.DeltaTime;
                if (chargeTimer <= 0)
                {
                    shocking = true;
                    Audio.Play("event:/game/general/assist_screenbottom", Position);
                }
            }
        }

        public override void Render()
        {
            base.Render();
            Color bodyColor = shocking ? Color.Yellow : Color.Lerp(Color.DarkBlue, Color.Yellow, 1f - chargeTimer / chargeTime);
            Draw.Rect(X - 7f, Y - 14f, 14f, 14f, bodyColor * 0.7f);
            if (shocking)
            {
                Draw.Circle(Position + new Vector2(0, -7f), shockRadius, Color.Yellow * (1f - shockRadius / maxShockRadius), 24);
            }
        }
    }

    // =============================================
    // MagnetEnemy - Pulls player toward it
    // =============================================
    [CustomEntity("MaggyHelper/MagnetEnemy")]
    [Tracked]
    public class MagnetEnemy : Enemy
    {
        private float pullStrength;
        private float pullRange;

        public MagnetEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            pullStrength = data.Float("pullStrength", 80f);
            pullRange = data.Float("pullRange", 120f);
            Collider = new Hitbox(16f, 16f, -8f, -16f);
        }

        public override void Update()
        {
            base.Update();
            if (cachedPlayer == null) return;

            float dist = Vector2.Distance(Position, cachedPlayer.Position);
            if (dist < pullRange && dist > 5f)
            {
                Vector2 pull = (Position - cachedPlayer.Position).SafeNormalize();
                float falloff = 1f - (dist / pullRange);
                cachedPlayer.Speed += pull * pullStrength * falloff * Engine.DeltaTime;
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(X - 8f, Y - 16f, 16f, 16f, Color.DarkRed * 0.6f);
            // Draw pull field
            for (float r = pullRange; r > 10; r -= 20f)
            {
                Draw.Circle(Position + new Vector2(0, -8f), r, Color.Red * 0.08f, 16);
            }
        }
    }

    // =============================================
    // HealerEnemy - Heals nearby enemies
    // =============================================
    [CustomEntity("MaggyHelper/HealerEnemy")]
    [Tracked]
    public class HealerEnemy : Enemy
    {
        private float healRange;
        private float healRate;
        private float healTimer = 0f;

        public HealerEnemy(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            healRange = data.Float("healRange", 80f);
            healRate = data.Float("healRate", 1f);
            Collider = new Hitbox(10f, 10f, -5f, -10f);
        }

        public override void Update()
        {
            base.Update();
            healTimer -= Engine.DeltaTime;
            if (healTimer <= 0)
            {
                healTimer = healRate;
                foreach (Enemy enemy in Scene.Tracker.GetEntities<Enemy>())
                {
                    if (enemy != this && enemy.IsAlive && Vector2.Distance(Position, enemy.Position) < healRange)
                    {
                        if (enemy.Health < enemy.MaxHealth)
                        {
                            enemy.Heal(1);
                        }
                    }
                }
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(X - 5f, Y - 10f, 10f, 10f, Color.LimeGreen * 0.7f);
            // Heal aura
            float pulse = 1f + (float)Math.Sin(Scene.TimeActive * 2f) * 0.1f;
            Draw.Circle(Position + new Vector2(0, -5f), healRange * pulse, Color.LimeGreen * 0.1f, 24);
        }
    }

    // =============================================
    // PhantomKnight - Teleporting sword enemy
    // =============================================
    [CustomEntity("MaggyHelper/PhantomKnight")]
    [Tracked]
    public class PhantomKnight : Enemy
    {
        private enum PhantomState { Hidden, Appearing, Attacking, Disappearing }
        private PhantomState state = PhantomState.Hidden;
        private float stateTimer;
        private float hiddenTime;
        private float attackTime;
        private Vector2 attackTarget;
        private float alpha = 0f;
        private float slashRange;

        public PhantomKnight(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 3))
        {
            hiddenTime = data.Float("hiddenTime", 2f);
            attackTime = data.Float("attackTime", 0.5f);
            slashRange = data.Float("slashRange", 30f);
            stateTimer = hiddenTime;
            Collider = new Hitbox(12f, 20f, -6f, -20f);
            Collidable = false;
        }

        public override void Update()
        {
            base.Update();
            stateTimer -= Engine.DeltaTime;

            switch (state)
            {
                case PhantomState.Hidden:
                    alpha = Calc.Approach(alpha, 0f, Engine.DeltaTime * 3f);
                    Collidable = false;
                    if (stateTimer <= 0 && cachedPlayer != null)
                    {
                        // Teleport near player
                        float side = Calc.Random.Choose(-1f, 1f);
                        Position = cachedPlayer.Position + new Vector2(side * 40f, 0f);
                        attackTarget = cachedPlayer.Position;
                        state = PhantomState.Appearing;
                        stateTimer = 0.5f;
                    }
                    break;

                case PhantomState.Appearing:
                    alpha = Calc.Approach(alpha, 1f, Engine.DeltaTime * 4f);
                    Collidable = true;
                    if (stateTimer <= 0)
                    {
                        state = PhantomState.Attacking;
                        stateTimer = attackTime;
                        Audio.Play("event:/game/general/fallblock_impact", Position);
                    }
                    break;

                case PhantomState.Attacking:
                    if (cachedPlayer != null)
                    {
                        float dist = Vector2.Distance(Position, cachedPlayer.Position);
                        if (dist < slashRange)
                        {
                            OnHitPlayer(cachedPlayer);
                        }
                    }
                    if (stateTimer <= 0)
                    {
                        state = PhantomState.Disappearing;
                        stateTimer = 0.3f;
                    }
                    break;

                case PhantomState.Disappearing:
                    alpha = Calc.Approach(alpha, 0f, Engine.DeltaTime * 5f);
                    Collidable = false;
                    if (stateTimer <= 0)
                    {
                        state = PhantomState.Hidden;
                        stateTimer = hiddenTime;
                    }
                    break;
            }
        }

        public override void Render()
        {
            if (alpha <= 0.01f) return;
            Draw.Rect(X - 6f, Y - 20f, 12f, 20f, Color.DarkSlateBlue * alpha);
            // Sword
            if (state == PhantomState.Attacking)
            {
                int dir = attackTarget.X > X ? 1 : -1;
                Draw.Line(Position + new Vector2(dir * 6f, -12f),
                          Position + new Vector2(dir * 30f, -12f), Color.Silver * alpha, 2f);
            }
        }
    }

    // =============================================
    // BombWaddleDee - Throws bombs
    // =============================================
    [CustomEntity("MaggyHelper/BombWaddleDee")]
    [Tracked]
    public class BombWaddleDee : Enemy
    {
        private float throwInterval;
        private float throwTimer;
        private float throwRange;
        private float bombSpeed;

        public BombWaddleDee(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 1))
        {
            throwInterval = data.Float("throwInterval", 2f);
            throwRange = data.Float("throwRange", 120f);
            bombSpeed = data.Float("bombSpeed", 150f);
            throwTimer = throwInterval;
            Collider = new Hitbox(12f, 12f, -6f, -12f);
        }

        public override void Update()
        {
            base.Update();
            throwTimer -= Engine.DeltaTime;

            if (cachedPlayer != null && throwTimer <= 0 && Vector2.Distance(Position, cachedPlayer.Position) < throwRange)
            {
                ThrowBomb(cachedPlayer.Position);
                throwTimer = throwInterval;
            }
        }

        private void ThrowBomb(Vector2 target)
        {
            Vector2 dir = (target - Position).SafeNormalize();
            Scene.Add(new ThrownBomb(Position + new Vector2(0, -8f), dir, bombSpeed));
            Audio.Play("event:/game/general/spring", Position);
        }

        public override void Render()
        {
            base.Render();
            Draw.Rect(X - 6f, Y - 12f, 12f, 12f, Color.Orange * 0.7f);
        }
    }

    /// <summary>
    /// Bomb projectile thrown by BombWaddleDee
    /// </summary>
    public class ThrownBomb : Projectile
    {
        private float fuseTime = 2f;
        private float explosionRadius;
        private float gravity = 200f;

        public ThrownBomb(Vector2 position, Vector2 direction, float speed, float explosionRadius = 40f)
            : base(position, direction, speed)
        {
            this.explosionRadius = explosionRadius;
            Velocity.Y -= 100f; // Arc upward
        }

        public override void Update()
        {
            Velocity.Y += gravity * Engine.DeltaTime;
            base.Update();
            fuseTime -= Engine.DeltaTime;
            if (fuseTime <= 0) Explode();
        }

        protected override void OnHitWall()
        {
            Explode();
        }

        private void Explode()
        {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && Vector2.Distance(Position, player.Position) < explosionRadius)
            {
                player.Die((player.Position - Position).SafeNormalize());
            }
            Audio.Play("event:/game/general/fallblock_impact", Position);
            (Scene as Level)?.Shake(0.2f);
            RemoveSelf();
        }

        public override void Render()
        {
            Draw.Circle(Position, 5f, Color.Black, 8);
            Draw.Circle(Position, 4f, Color.DarkGray, 8);
            // Fuse spark
            float sparkY = Position.Y - 5f - (float)Math.Sin(Scene.TimeActive * 20f) * 2f;
            Draw.Point(new Vector2(Position.X, sparkY), Color.Yellow);
        }
    }

    // =============================================
    // DarkMatterMinion - Shoots beams in cardinal directions
    // =============================================
    [CustomEntity("MaggyHelper/DarkMatterMinion")]
    [Tracked]
    public class DarkMatterMinion : Enemy
    {
        private float fireInterval;
        private float fireTimer;
        private float beamLength;
        private bool firing = false;
        private float fireShowTime = 0.3f;
        private float fireShowTimer = 0f;

        public DarkMatterMinion(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Int("health", 2))
        {
            fireInterval = data.Float("fireInterval", 2.5f);
            beamLength = data.Float("beamLength", 80f);
            fireTimer = fireInterval;
            Collider = new Hitbox(14f, 14f, -7f, -7f);
        }

        public override void Update()
        {
            base.Update();
            if (firing)
            {
                fireShowTimer -= Engine.DeltaTime;
                if (cachedPlayer != null)
                {
                    Vector2[] dirs = { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };
                    foreach (var dir in dirs)
                    {
                        // Check if player is in beam
                        Vector2 beamEnd = Position + dir * beamLength;
                        float cross = Math.Abs((cachedPlayer.Position - Position).X * dir.Y - (cachedPlayer.Position - Position).Y * dir.X);
                        float dot = Vector2.Dot(cachedPlayer.Position - Position, dir);
                        if (cross < 6f && dot > 0 && dot < beamLength)
                        {
                            OnHitPlayer(cachedPlayer);
                        }
                    }
                }
                if (fireShowTimer <= 0) firing = false;
            }
            else
            {
                fireTimer -= Engine.DeltaTime;
                if (fireTimer <= 0)
                {
                    firing = true;
                    fireShowTimer = fireShowTime;
                    fireTimer = fireInterval;
                    Audio.Play("event:/game/general/assist_screenbottom", Position);
                }
            }
        }

        public override void Render()
        {
            base.Render();
            Draw.Circle(Position, 8f, Color.DarkMagenta * 0.8f, 12);
            // Eye
            Draw.Circle(Position, 4f, Color.Red * 0.9f, 8);

            if (firing)
            {
                Vector2[] dirs = { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };
                foreach (var dir in dirs)
                {
                    Draw.Line(Position, Position + dir * beamLength, Color.DarkMagenta * 0.7f, 3f);
                }
            }
        }
    }
}
