using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace MaggyHelper
{
    /// <summary>
    /// Manages item collection state with the player
    /// </summary>
    public class ItemCollectionState
    {
        private Player player;
        private bool isCollecting;

        public bool IsCollecting => isCollecting;

        public bool TryStartCollection(Player player)
        {
            if (player == null || player.StateMachine.State == Player.StDummy)
                return false;

            this.player = player;
            this.isCollecting = true;
            player.StateMachine.State = Player.StDummy;
            return true;
        }

        public void EndCollection()
        {
            if (player != null && isCollecting)
            {
                player.StateMachine.State = Player.StNormal;
                isCollecting = false;
            }
        }

        public IEnumerator RunCollectionSequence(IEnumerator sequence)
        {
            yield return sequence;
            EndCollection();
        }
    }

    /// <summary>
    /// Base class for collectible items
    /// </summary>
    [Tracked]
    public abstract class CollectibleItem : Entity
    {
        protected Sprite sprite;
        protected Wiggler wiggler;
        protected BloomPoint bloom;
        protected VertexLight light;
        protected SineWave sine;
        protected bool collected = false;
        protected string collectSound = "event:/game/general/seed_touch";
        protected ItemCollectionState collectionState;
        
        public CollectibleItem(Vector2 position)
            : base(position)
        {
            collectionState = new ItemCollectionState();
            Collider = new Hitbox(12f, 12f, -6f, -6f);
            
            Add(wiggler = Wiggler.Create(0.5f, 4f, f => sprite.Scale = Vector2.One * (1f + f * 0.25f)));
            Add(sine = new SineWave(0.5f, 0f));
            
            Add(bloom = new BloomPoint(0.5f, 12f));
            Add(light = new VertexLight(Color.White, 1f, 16, 32));
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = -1000000;
        }

        public override void Update()
        {
            base.Update();
            
            if (collected)
                return;

            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && CollideCheck(player))
            {
                OnCollect(player);
            }
            
            // Floating animation
            Position.Y += sine.Value * 0.2f;
        }

        protected virtual void OnCollect(Player player)
        {
            if (!collectionState.TryStartCollection(player))
                return;
                
            collected = true;
            Audio.Play(collectSound, Position);
            wiggler.Start();
            
            Add(new Coroutine(collectionState.RunCollectionSequence(CollectRoutine(player))));
        }

        protected virtual IEnumerator CollectRoutine(Player player)
        {
            yield return 0.1f;
            RemoveSelf();
        }

        public override void Render()
        {
            if (sprite != null)
            {
                sprite.DrawOutline(Color.Black);
            }
            base.Render();
        }
    }

    /// <summary>
    /// Health heart item that restores player health
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/HealthHeart")]
    [Tracked]
    public class HealthHeart : CollectibleItem
    {
        private int healAmount;
        private ParticleType particleType;
        
        public HealthHeart(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            healAmount = data.Int("healAmount", 1);
            
            Add(sprite = new Sprite(GFX.Game, "collectibles/items/"));
            sprite.AddLoop("idle", "heart", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            
            particleType = new ParticleType
            {
                Color = Color.Red,
                Size = 1f,
                SpeedMin = 20f,
                SpeedMax = 40f,
                LifeMin = 0.5f,
                LifeMax = 1f
            };
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            
            // Emit particles
            Level level = Scene as Level;
            if (level != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    level.Particles.Emit(particleType, Position, Calc.Random.NextFloat(MathF.PI * 2));
                }
            }
        }
    }

    /// <summary>
    /// Coin collectible
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/Coin")]
    [Tracked]
    public class Coin : CollectibleItem
    {
        private int value;
        
        public Coin(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            value = data.Int("value", 1);
            
            Add(sprite = new Sprite(GFX.Game, "collectibles/items/"));
            sprite.AddLoop("idle", "coin", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            
            collectSound = "event:/game/general/seed_touch";
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            
            // Add to player's coin count (would need to integrate with save system)
            Level level = Scene as Level;
            if (level != null)
            {
                level.Session.SetFlag($"coin_{GetHashCode()}_collected");
            }
        }
    }

    /// <summary>
    /// Power-up item that grants temporary abilities
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/PowerUp")]
    [Tracked]
    public class PowerUp : CollectibleItem
    {
        public enum PowerUpType
        {
            Speed,
            Jump,
            Dash,
            Shield,
            Flight
        }
        
        private PowerUpType powerUpType;
        private float duration;
        
        public PowerUp(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            string typeStr = data.Attr("powerUpType", "Speed");
            Enum.TryParse(typeStr, out powerUpType);
            duration = data.Float("duration", 10f);
            
            Add(sprite = new Sprite(GFX.Game, "collectibles/powerups/"));
            sprite.AddLoop("speed", "speed", 0.1f);
            sprite.AddLoop("jump", "jump", 0.1f);
            sprite.AddLoop("dash", "dash", 0.1f);
            sprite.AddLoop("shield", "shield", 0.1f);
            sprite.AddLoop("flight", "flight", 0.1f);
            sprite.Play(powerUpType.ToString().ToLower());
            sprite.CenterOrigin();
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            ApplyPowerUp(player);
        }

        private void ApplyPowerUp(Player player)
        {
            // Apply power-up effect based on type
            Level level = Scene as Level;
            if (level == null)
                return;

            switch (powerUpType)
            {
                case PowerUpType.Speed:
                    level.Session.SetFlag("powerup_speed_active");
                    break;
                case PowerUpType.Jump:
                    level.Session.SetFlag("powerup_jump_active");
                    break;
                case PowerUpType.Dash:
                    player.Dashes = Math.Max(player.Dashes, 2);
                    break;
                case PowerUpType.Shield:
                    level.Session.SetFlag("powerup_shield_active");
                    break;
                case PowerUpType.Flight:
                    level.Session.SetFlag("powerup_flight_active");
                    break;
            }
        }
    }

    /// <summary>
    /// Key item for unlocking doors or progressing story
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KeyItem")]
    [Tracked]
    public class KeyItem : CollectibleItem
    {
        private string keyId;
        private string keyName;
        private Color keyColor;
        
        public KeyItem(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            keyId = data.Attr("keyId", "key_1");
            keyName = data.Attr("keyName", "Key");
            
            string colorHex = data.Attr("keyColor", "FFD700");
            keyColor = Calc.HexToColor(colorHex);
            
            Add(sprite = new Sprite(GFX.Game, "collectibles/keys/"));
            sprite.AddLoop("idle", "key", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            sprite.Color = keyColor;
            
            collectSound = "event:/game/general/key_get";
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            
            Level level = Scene as Level;
            if (level != null)
            {
                level.Session.SetFlag($"key_{keyId}_collected");
            }
        }

        protected override IEnumerator CollectRoutine(Player player)
        {
            // Show key obtained message
            yield return 0.5f;
            
            // Could add text display here
            
            yield return 0.5f;
            
            RemoveSelf();
        }
    }

    /// <summary>
    /// Star collectible (like Super Mario)
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/StarItem")]
    [Tracked]
    public class StarItem : CollectibleItem
    {
        private float rotationSpeed = 2f;
        
        public StarItem(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            Add(sprite = new Sprite(GFX.Game, "collectibles/items/"));
            sprite.AddLoop("idle", "star", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            
            collectSound = "event:/game/general/seed_poof";
        }

        public override void Update()
        {
            base.Update();
            
            if (sprite != null)
            {
                sprite.Rotation += rotationSpeed * Engine.DeltaTime;
            }
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            
            Level level = Scene as Level;
            if (level != null)
            {
                // Grant invincibility or special power
                level.Session.SetFlag("star_power_active");
            }
        }
    }

    /// <summary>
    /// Crystal shard collectible
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/CrystalShard")]
    [Tracked]
    public class CrystalShard : CollectibleItem
    {
        private Color crystalColor;
        
        public CrystalShard(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            string colorHex = data.Attr("color", "00FFFF");
            crystalColor = Calc.HexToColor(colorHex);
            
            Add(sprite = new Sprite(GFX.Game, "collectibles/items/"));
            sprite.AddLoop("idle", "crystal", 0.15f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            sprite.Color = crystalColor;
        }

        public override void Update()
        {
            base.Update();
            
            if (light != null)
            {
                light.Color = crystalColor;
            }
        }

        protected override void OnCollect(Player player)
        {
            base.OnCollect(player);
            
            Level level = Scene as Level;
            if (level != null)
            {
                int shardCount = level.Session.GetCounter("crystal_shards");
                level.Session.SetCounter("crystal_shards", shardCount + 1);
            }
        }
    }
}
