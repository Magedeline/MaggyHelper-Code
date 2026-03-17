using MaggyHelper.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace MaggyHelper
{
    /// <summary>
    /// Manages NPC interaction state with the player
    /// </summary>
    public class NPCInteractionState
    {
        private Player player;
        private bool isActive;

        public bool IsActive => isActive;

        public bool TryStartInteraction(Player player)
        {
            if (player == null || player.StateMachine.State == Player.StDummy)
                return false;

            this.player = player;
            this.isActive = true;
            player.StateMachine.State = Player.StDummy;
            return true;
        }

        public void EndInteraction()
        {
            if (player != null && isActive)
            {
                player.StateMachine.State = Player.StNormal;
                isActive = false;
            }
        }

        public IEnumerator RunInteractionSequence(IEnumerator sequence)
        {
            yield return sequence;
            EndInteraction();
        }
    }

    /// <summary>
    /// Kirby NPC that can appear in levels and interact with the player.
    /// Sprite path: characters/kirby/npc
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KirbyNPC")]
    [Tracked]
    public class KirbyNPC : Actor
    {
        private Sprite sprite;
        private string dialogId;
        private bool talked;
        private TalkComponent talkComponent;
        private Vector2 origin;
        private float idleTimer;
        private NPCInteractionState interactionState;
        
        public enum KirbyState
        {
            Idle,
            Wave,
            Happy,
            Sleep,
            Eat
        }

        private KirbyState currentState = KirbyState.Idle;

        public KirbyNPC(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            dialogId = data.Attr("dialogId", "");
            origin = Position;
            interactionState = new NPCInteractionState();
            
            Collider = new Hitbox(16f, 16f, -8f, -16f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/kirby/npc/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("wave", "wave", 0.1f);
            sprite.AddLoop("happy", "happy", 0.1f);
            sprite.AddLoop("sleep", "sleep", 0.15f);
            sprite.AddLoop("eat", "eat", 0.08f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            
            Add(talkComponent = new TalkComponent(
                new Rectangle(-16, -24, 32, 24),
                new Vector2(0f, -24f),
                OnTalk
            ));
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = 100;
        }

        public override void Update()
        {
            base.Update();
            
            idleTimer -= Engine.DeltaTime;
            
            if (idleTimer <= 0 && currentState == KirbyState.Idle)
            {
                // Random idle behaviors
                float rand = Calc.Random.NextFloat();
                if (rand < 0.3f)
                {
                    SetState(KirbyState.Wave);
                }
                else if (rand < 0.5f)
                {
                    SetState(KirbyState.Happy);
                }
                else if (rand < 0.6f)
                {
                    SetState(KirbyState.Sleep);
                }
                
                idleTimer = Calc.Random.Range(3f, 8f);
            }

            // Small bobbing animation
            Position.Y = origin.Y + (float)Math.Sin(Scene.TimeActive * 2f) * 2f;
        }

        private void SetState(KirbyState state)
        {
            currentState = state;
            
            switch (state)
            {
                case KirbyState.Idle:
                    sprite.Play("idle");
                    break;
                case KirbyState.Wave:
                    sprite.Play("wave");
                    break;
                case KirbyState.Happy:
                    sprite.Play("happy");
                    break;
                case KirbyState.Sleep:
                    sprite.Play("sleep");
                    break;
                case KirbyState.Eat:
                    sprite.Play("eat");
                    break;
            }
        }

        private void OnTalk(Player player)
        {
            if (talked || string.IsNullOrEmpty(dialogId))
                return;

            if (!interactionState.TryStartInteraction(player))
                return;

            talked = true;
            Add(new Coroutine(interactionState.RunInteractionSequence(TalkSequence(player))));
        }

        private IEnumerator TalkSequence(Player player)
        {
            SetState(KirbyState.Happy);
            yield return 0.5f;

            // Show dialog
            if (!string.IsNullOrEmpty(dialogId))
            {
                yield return Textbox.Say(dialogId);
            }

            SetState(KirbyState.Wave);
            yield return 0.5f;
            
            SetState(KirbyState.Idle);
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }

    /// <summary>
    /// Kirby follower that follows the player around
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KirbyFollowerNPC")]
    [Tracked]
    public class KirbyFollowerNPC : Actor
    {
        private Sprite sprite;
        private float followSpeed = 60f;
        private float followDistance = 40f;
        private NPCInteractionState interactionState;
        
        public KirbyFollowerNPC(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            interactionState = new NPCInteractionState();
            Collider = new Hitbox(12f, 12f, -6f, -12f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/kirby/npc/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("run", "wave", 0.08f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = 100;
        }

        public override void Update()
        {
            base.Update();
            
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null)
            {
                float distance = Vector2.Distance(Position, player.Position);
                
                if (distance > followDistance)
                {
                    Vector2 direction = (player.Position - Position).SafeNormalize();
                    Vector2 move = direction * followSpeed * Engine.DeltaTime;
                    
                    MoveH(move.X);
                    MoveV(move.Y);
                    
                    sprite.Play("run");
                    sprite.Scale.X = Math.Sign(direction.X);
                }
                else
                {
                    sprite.Play("idle");
                }
            }
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }

    /// <summary>
    /// Kirby shopkeeper NPC
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KirbyShopkeeper")]
    [Tracked]
    public class KirbyShopkeeper : Actor
    {
        private Sprite sprite;
        private string shopDialogId;
        private TalkComponent talkComponent;
        private NPCInteractionState interactionState;
        
        public KirbyShopkeeper(EntityData data, Vector2 offset)
            : base(data.Position + offset)
        {
            interactionState = new NPCInteractionState();
            shopDialogId = data.Attr("shopDialogId", "kirby_shop");
            
            Collider = new Hitbox(16f, 16f, -8f, -16f);
            
            Add(sprite = new Sprite(GFX.Game, "characters/kirby/npc/"));
            sprite.AddLoop("idle", "idle", 0.1f);
            sprite.AddLoop("happy", "happy", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            
            Add(talkComponent = new TalkComponent(
                new Rectangle(-20, -24, 40, 24),
                new Vector2(0f, -24f),
                OnShopTalk
            ));
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Depth = 100;
        }

        private void OnShopTalk(Player player)
        {
            if (!interactionState.TryStartInteraction(player))
                return;
                
            Add(new Coroutine(interactionState.RunInteractionSequence(ShopSequence(player))));
        }

        private IEnumerator ShopSequence(Player player)
        {
            sprite.Play("happy");
            yield return 0.3f;

            if (!string.IsNullOrEmpty(shopDialogId))
            {
                yield return Textbox.Say(shopDialogId);
            }

            sprite.Play("idle");
        }

        public override void Render()
        {
            sprite.DrawOutline(Color.Black);
            base.Render();
        }
    }
}
