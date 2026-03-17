using MaggyHelper.Cutscenes;

namespace MaggyHelper.Entities
{
    [CustomEntity(ids: "MaggyHelper/BirdNPCMod")]
    [Tracked(true)]
    [HotReloadable]
    public class BirdNpcGoner : Actor
    {
        public static ParticleType PFeather;
        public static string FlownFlag = "bird_fly_away_";
        public Facings Facing = Facings.Left;
        public Sprite Sprite;
        public Vector2 StartPosition;
        public VertexLight Light;
        public bool AutoFly;
        public EntityID EntityId;
        public bool FlyAwayUp = true;
        public float WaitForLightningPostDelay;
        public bool DisableFlapSfx;
        public Coroutine TutorialRoutine;
        public Modes Mode;
        public BirdGonerTutorialGui Gui;
        public Level Level;
        public Vector2[] Nodes;
        public StaticMover StaticMover;
        public bool OnlyOnce;
        public bool OnlyIfPlayerLeft;
        public BirdTypes BirdType;
        private readonly TimeRateModifier timeRateModifier;
        /// <summary>Set to true by Bridge when the last tile falls, starting the freeze + land + dash tutorial sequence.</summary>
        public bool BridgeEndTriggered;

        static BirdNpcGoner()
        {
            // Initialize feather particle type
            PFeather = new ParticleType
            {
                Source = GFX.Game["particles/feather"],
                Color = Color.White,
                Color2 = Color.Gray,
                ColorMode = ParticleType.ColorModes.Choose,
                FadeMode = ParticleType.FadeModes.Late,
                LifeMin = 0.8f,
                LifeMax = 1.2f,
                Size = 1f,
                SizeRange = 0.5f,
                SpeedMin = 10f,
                SpeedMax = 20f,
                Direction = (float)Math.PI / 2f,
                DirectionRange = (float)Math.PI / 4f,
                Acceleration = new Vector2(0f, 10f),
                RotationMode = ParticleType.RotationModes.SameAsDirection
            };
        }

        public BirdNpcGoner(Vector2 position, Modes mode, BirdTypes birdType = BirdTypes.Default) : base(position)
        {
            BirdType = birdType;
            string spriteName = GetSpriteNameForBirdType(birdType);
            Add(Sprite = GFX.SpriteBank.Create(spriteName));
            Sprite.Scale.X = (float)Facing;
            Sprite.UseRawDeltaTime = true;
            Sprite.OnFrameChange = OnSpriteFrameChange;
            Add(Light = new VertexLight(new Vector2(0f, -8f), Color.White, 1f, 8, 32));
            Add(timeRateModifier = new TimeRateModifier(1f, false));
            StartPosition = Position;
            setMode(mode);
        }

        private static string GetSpriteNameForBirdType(BirdTypes birdType)
        {
            return birdType switch
            {
                BirdTypes.Clover => "birdgoner_clover",
                BirdTypes.Cody => "birdgoner_cody",
                BirdTypes.Emily => "birdgoner_emily",
                BirdTypes.Odin => "birdgoner_odin",
                BirdTypes.Robin => "birdgoner_robin",
                BirdTypes.Sabel => "birdgoner_sabel",
                _ => "bird"
            };
        }

        private void setMode(Modes mode)
        {
            Mode = mode;

            switch (mode)
            {
                case Modes.ClimbingTutorial:
                    Add(new Coroutine(ClimbingTutorial()));
                    break;

                case Modes.DashingTutorial:
                    Add(new Coroutine(DashingTutorial()));
                    break;

                case Modes.DreamJumpTutorial:
                    // Add logic for DreamJumpTutorial if required
                    break;

                case Modes.SuperWallJumpTutorial:
                    // Add logic for SuperWallJumpTutorial if required
                    break;

                case Modes.HyperJumpTutorial:
                    // Add logic for HyperJumpTutorial if required
                    break;

                case Modes.KirbyPlayerTutorial:
                    Add(new Coroutine(KirbyPlayerTutorial()));
                    break;

                case Modes.FlyAway:
                    if (AutoFly) Add(new Coroutine(startleAndFlyAway()));
                    break;

                case Modes.Sleeping:
                    this.Sprite.Play("sleep");
                    break;

                case Modes.MoveToNodes:
                    Add(new Coroutine(moveToNodesRoutine()));
                    break;

                case Modes.WaitForLightningOff:
                    Add(new Coroutine(waitForLightningOffRoutine()));
                    break;

                case Modes.BridgeEndDash:
                    Add(new Coroutine(BridgeEndDashRoutine()));
                    break;

                case Modes.None:
                default:
                    break;
            }
        }

        private IEnumerator waitForLightningOffRoutine()
        {
            while (Level.Session.BloomBaseAdd > 0.1f) yield return null;

            yield return WaitForLightningPostDelay;
        }

        private IEnumerator moveToNodesRoutine()
        {
            if (Nodes == null || Nodes.Length == 0) yield break;

            foreach (var target in Nodes)
            {
                while (Vector2.Distance(Position, target) > 1f)
                {
                    var direction = (target - Position).SafeNormalize();
                    Position += direction * 60f * Engine.DeltaTime;

                    Facing = direction.X > 0 ? Facings.Right : Facings.Left;

                    yield return null;
                }

                Position = target;
                yield return 0.2f;
            }
        }

        private IEnumerator startleAndFlyAway()
        {
            if (Level.Session.GetFlag(FlownFlag + Level.Session.Level)) yield break;

            Level.Session.SetFlag(FlownFlag + Level.Session.Level);
            this.Sprite.Play("fly");
            Light.Visible = false;

            if (!DisableFlapSfx) Audio.Play("event:/game/general/bird_startle", Position);

            var flyDirection = FlyAwayUp ? new Vector2(0f, -1f) : new Vector2((float)Facing, -0.5f);
            flyDirection.Normalize();

            for (var i = 0; i < 6; i++)
            {
                Level.Particles.Emit(PFeather, 1, Position + new Vector2(0f, -6f), Vector2.One * 4f);
                yield return 0.05f;
            }

            while (true)
            {
                Position += flyDirection * 60f * Engine.DeltaTime;
                if (Position.Y < Level.Camera.Top - 16f || Position.X < Level.Camera.Left - 16f ||
                    Position.X > Level.Camera.Right + 16f)
                    break;

                yield return null;
            }

            RemoveSelf();
        }

        public BirdNpcGoner(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Enum(nameof(Mode), Modes.None), data.Enum(nameof(BirdType), BirdTypes.Default))
        {
            EntityId = new EntityID(data.Level.Name, data.ID);
            Nodes = data.NodesOffset(offset);
            OnlyOnce = data.Bool(nameof(OnlyOnce));
            OnlyIfPlayerLeft = data.Bool(nameof(OnlyIfPlayerLeft));
            AutoFly = data.Bool(nameof(AutoFly), false);
            FlyAwayUp = data.Bool(nameof(FlyAwayUp), true);
            WaitForLightningPostDelay = data.Float(nameof(WaitForLightningPostDelay), 0f);
            DisableFlapSfx = data.Bool(nameof(DisableFlapSfx), false);
        }

        private void OnSpriteFrameChange(string spr)
        {
            if (Level != null && X > Level.Camera.Left + 64 && X < Level.Camera.Right - 64 &&
                (spr == "peck" || spr == "peckRare") && Sprite.CurrentAnimationFrame == 6)
            {
                Audio.Play("event:/game/general/bird_peck", Position);
            }
            if (Level != null && Level.Session.Area.ID == 10 && !DisableFlapSfx)
            {
                FlapSfxCheck(Sprite);
            }
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Level = scene as Level;
            if (Mode == Modes.ClimbingTutorial && Level.Session.GetLevelFlag("2"))
            {
                RemoveSelf();
            }
            else if (Mode == Modes.DashingTutorial && Level.Session.GetFlag("dash_tutorial_complete"))
            {
                RemoveSelf();
            }
            else if (Mode == Modes.KirbyPlayerTutorial && Level.Session.GetFlag("kirby_tutorial_complete"))
            {
                RemoveSelf();
            }
            else if (Mode == Modes.FlyAway && Level.Session.GetFlag(FlownFlag + Level.Session.Level))
            {
                RemoveSelf();
            }
            // BridgeEndDash bird should never be auto-removed; it waits for the bridge signal.
        }

        public override void Awake(Scene scene)
        {
            base.Awake(scene);
            if (Mode == Modes.SuperWallJumpTutorial)
            {
                var player = scene.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null && player.Y < Y + 32)
                {
                    RemoveSelf();
                }
            }
            if (OnlyIfPlayerLeft)
            {
                var player = Level.Tracker.GetEntity<global::Celeste.Player>();
                if (player != null && player.X > X)
                {
                    RemoveSelf();
                }
            }
        }

        public override bool IsRiding(Solid solid)
        {
            return Scene.CollideCheck(new Rectangle((int)X - 4, (int)Y, 8, 2), solid);
        }

        public override void Update()
        {
            Sprite.Scale.X = (float)Facing;
            base.Update();
        }

        public IEnumerator ClimbingTutorial()
        {
            var player = Scene.Tracker.GetEntity<global::Celeste.Player>();
            while (Math.Abs(player.X - X) > 120)
            {
                yield return null;
            }
            var tutorial1 = new BirdGonerTutorialGui(this, new Vector2(0f, -16f), Dialog.Clean("tutorial_climb"), new object[]
            {
                Dialog.Clean("tutorial_hold"),
                BirdGonerTutorialGui.ButtonPrompt.Grab
            });
            var tutorial2 = new BirdGonerTutorialGui(this, new Vector2(0f, -16f), Dialog.Clean("tutorial_climb"), new object[]
            {
                BirdGonerTutorialGui.ButtonPrompt.Grab,
                "+",
                new Vector2(0f, -1f)
            });
            bool first = true;
            bool willEnd;
            do
            {
                yield return showTutorial(tutorial1, first);
                first = false;
                while (player.StateMachine.State != 1 && player.Y > Y)
                {
                    yield return null;
                }
                if (player.Y > Y)
                {
                    Audio.Play("event:/ui/game/tutorial_note_flip_back");
                    yield return hideTutorial();
                }
                while (player.Scene != null && (!player.OnGround() || player.StateMachine.State == 1))
                {
                    yield return null;
                }
                willEnd = player.Y <= Y + 4;
                if (!willEnd)
                {
                    Audio.Play("event:/ui/game/tutorial_note_flip_front");
                }
                yield return hideTutorial();
            } while (!willEnd);
            yield return startleAndFlyAway();
        }

        public IEnumerator DashingTutorial()
        {
            var player = Scene.Tracker.GetEntity<global::Celeste.Player>();
            if (player == null) yield break;

            // Wait for player to get close
            while (Math.Abs(player.X - X) > 120)
            {
                yield return null;
            }

            // Create dash tutorial GUI
            var dashTutorial = new BirdGonerTutorialGui(this, new Vector2(0f, -16f), Dialog.Clean("tutorial_dash"), new object[]
            {
                BirdGonerTutorialGui.ButtonPrompt.Dash,
                "+",
                new Vector2(1f, 0f)
            });

            bool tutorialComplete = false;
            bool first = true;

            while (!tutorialComplete)
            {
                yield return showTutorial(dashTutorial, first);
                first = false;

                // Wait for player to dash (state 2 is dashing, state 5 is red dash)
                while (player.Scene != null && player.StateMachine.State != 2 && player.StateMachine.State != 5)
                {
                    yield return null;
                }

                if (player.Scene == null) yield break;

                // Player dashed! Mark tutorial as complete
                tutorialComplete = true;
                Audio.Play("event:/ui/game/tutorial_note_flip_back");
            }

            yield return hideTutorial();

            // Small delay before flying away
            yield return 0.5f;

            // Set flag before flying away (must be before RemoveSelf stops this coroutine)
            Level.Session.SetFlag("dash_tutorial_complete");
            Level.Session.SetFlag(FlownFlag + Level.Session.Level);
            var savedSession = Level.Session;

            // Inline fly-away so we can trigger the vignette AFTER the bird exits
            this.Sprite.Play("fly");
            Light.Visible = false;
            if (!DisableFlapSfx) Audio.Play("event:/game/general/bird_startle", Position);

            for (var fi = 0; fi < 6; fi++)
            {
                Level.Particles.Emit(PFeather, 1, Position + new Vector2(0f, -6f), Vector2.One * 4f);
                yield return 0.05f;
            }

            var flyDir = FlyAwayUp ? new Vector2(0f, -1f) : new Vector2((float)Facing, -0.5f);
            flyDir.Normalize();

            while (true)
            {
                Position += flyDir * 60f * Engine.DeltaTime;
                if (Position.Y < Level.Camera.Top - 16f || Position.X < Level.Camera.Left - 16f ||
                    Position.X > Level.Camera.Right + 16f)
                    break;
                yield return null;
            }

            // Transition to the Bridge Ending Vignette before removing self
            yield return 0.3f;
            Engine.Scene = new Cs00BridgeEndingVignette(savedSession);
            RemoveSelf();
        }

        /// <summary>
        /// Bridge-end sequence: freeze time, fly in from above, show dash tutorial,
        /// unfreeze on player dash, fly away, then start the ending vignette.
        /// Place this bird in the map at the desired landing position with Mode = BridgeEndDash.
        /// </summary>
        public IEnumerator BridgeEndDashRoutine()
        {
            var player = Scene.Tracker.GetEntity<global::Celeste.Player>();
            if (player == null) yield break;

            // Hide until the bridge signals us
            Sprite.Visible = false;
            while (!BridgeEndTriggered) yield return null;

            // --- Freeze time (near-zero so the screen looks paused but inputs still register) ---
            timeRateModifier.SetTimeRateMultiplier(0.001f);

            // Start the bird above the screen and fly down to the landing position
            var landingPos = Position;
            Position = new Vector2(landingPos.X, Level.Camera.Top - 48f);
            Sprite.Visible = true;
            Sprite.UseRawDeltaTime = true;
            Sprite.Play("flyup");

            // Fly down over ~1.5 raw seconds
            const float flyDuration = 1.5f;
            for (float t = 0f; t < flyDuration; t += Engine.RawDeltaTime)
            {
                Position = Vector2.Lerp(
                    new Vector2(landingPos.X, Level.Camera.Top - 48f),
                    landingPos,
                    Ease.SineInOut(t / flyDuration));
                yield return null;
            }
            Position = landingPos;

            // Land
            Sprite.Play("idle");
            // Small raw-time pause so landing settles
            for (float t = 0f; t < 0.4f; t += Engine.RawDeltaTime) yield return null;

            // --- Show dash tutorial prompt ---
            var dashTutorial = new BirdGonerTutorialGui(this, new Vector2(0f, -16f),
                Dialog.Clean("tutorial_dash"), new object[]
                {
                    BirdGonerTutorialGui.ButtonPrompt.Dash,
                    "+",
                    new Vector2(1f, 0f)
                });
            Gui = dashTutorial;
            Level.Add(Gui);
            Gui.Open = true;

            // Wait for the player to dash (state 2 = normal dash, state 5 = red/super dash)
            // Input.Dash.Pressed is checked too because DeltaTime=~0 can slow state transitions
            while (player.Scene != null
                   && player.StateMachine.State != 2
                   && player.StateMachine.State != 5
                   && !Input.Dash.Pressed)
            {
                yield return null;
            }

            // --- Unfreeze time ---
            timeRateModifier.ResetTimeRateMultiplier();

            // Hide tutorial
            if (Gui != null)
            {
                Gui.Open = false;
                for (float t = 0f; t < 0.15f; t += Engine.RawDeltaTime) yield return null;
                Gui.RemoveSelf();
                Gui = null;
            }

            // Brief pause before flying away
            yield return 0.4f;

            // Store session before RemoveSelf stops the coroutine
            Level.Session.SetFlag("dash_tutorial_complete");
            Level.Session.SetFlag(FlownFlag + Level.Session.Level);
            var savedSession = Level.Session;

            // Fly straight up and out
            Sprite.Play("fly");
            Light.Visible = false;
            if (!DisableFlapSfx)
                Audio.Play("event:/game/general/bird_startle", Position);

            for (var fi = 0; fi < 6; fi++)
            {
                Level.Particles.Emit(PFeather, 1, Position + new Vector2(0f, -6f), Vector2.One * 4f);
                yield return 0.05f;
            }

            while (true)
            {
                Position += new Vector2(0f, -1f) * 60f * Engine.DeltaTime;
                if (Position.Y < Level.Camera.Top - 16f) break;
                yield return null;
            }

            // Transition to the bridge ending vignette
            yield return 0.3f;
            Engine.Scene = new Cs00BridgeEndingVignette(savedSession);
            RemoveSelf();
        }

        private void add(BirdGonerTutorialGui components)
        {
            Gui = components;
            Level.Add(Gui);
        }

        private IEnumerator hideTutorial()
        {
            if (Gui != null)
            {
                Gui.Open = false;
                yield return 0.15f;
                Gui.RemoveSelf();
                Gui = null;
            }

            yield break;
        }

        private IEnumerator showTutorial(BirdGonerTutorialGui tutorial, bool first)
        {
            if (first)
            {
                Gui = tutorial;
                Level.Add(Gui);
                Gui.Open = true;
                yield return null;
            }
            else
            {
                if (Gui != null) Gui.Open = true;
                yield return null;
            }
        }

        // Methods used by cutscenes
        public IEnumerator Startle(string sfx = null, float duration = 0.5f, Vector2? targetOffset = null)
        {
            Sprite.Play("fly");

            if (!string.IsNullOrEmpty(sfx))
            {
                Audio.Play(sfx, Position);
            }
            else if (!DisableFlapSfx)
            {
                Audio.Play("event:/game/general/bird_startle", Position);
            }

            Vector2 startPos = Position;
            Vector2 offset = targetOffset ?? new Vector2(0f, -16f);

            for (float t = 0f; t < 1f; t += Engine.DeltaTime / duration)
            {
                Position = startPos + offset * Ease.CubeOut(t);
                yield return null;
            }

            Sprite.Play("idle");
        }

        public IEnumerator FlyAway(float delay = 0f)
        {
            if (delay > 0f)
            {
                yield return delay;
            }

            yield return startleAndFlyAway();
        }

        public IEnumerator FlyTo(Vector2 target, float duration = 1f, bool playSound = true)
        {
            Sprite.Play("fly");

            if (playSound && !DisableFlapSfx)
            {
                Audio.Play("event:/game/general/bird_startle", Position);
            }

            Vector2 startPos = Position;

            // Set facing direction
            if (target.X > Position.X)
            {
                Facing = Facings.Right;
            }
            else if (target.X < Position.X)
            {
                Facing = Facings.Left;
            }

            for (float t = 0f; t < 1f; t += Engine.DeltaTime / duration)
            {
                Position = Vector2.Lerp(startPos, target, Ease.SineInOut(t));

                // Add some bobbing motion
                float bobAmount = (float)Math.Sin(t * Math.PI * 4f) * 4f;
                Position += new Vector2(0f, bobAmount);

                yield return null;
            }

            Position = target;
            Sprite.Play("idle");
        }

        public IEnumerator Caw()
        {
            Audio.Play("event:/game/general/bird_caw", Position);
            Sprite.Play("peck");

            while (Sprite.Animating)
            {
                yield return null;
            }

            Sprite.Play("idle");
        }

        public IEnumerator KirbyPlayerTutorial()
        {
            var player = Scene.Tracker.GetEntity<global::Celeste.Player>();
            if (player == null) yield break;

            // Wait for player to get close
            while (Math.Abs(player.X - X) > 120)
            {
                yield return null;
            }

            // Tutorial step 1: Teach Kirby inhale (Grab key)
            var inhaleTutorial = new BirdGonerTutorialGui(this, new Vector2(0f, -16f), Dialog.Clean("tutorial_kirby_inhale"), new object[]
            {
                BirdGonerTutorialGui.ButtonPrompt.Grab
            });

            bool first = true;
            yield return showTutorial(inhaleTutorial, first);
            first = false;

            // Wait for player to press Grab to inhale
            while (player.Scene != null && !Input.Grab.Pressed)
            {
                yield return null;
            }

            if (player.Scene == null) yield break;

            Audio.Play("event:/ui/game/tutorial_note_flip_back");
            yield return hideTutorial();
            yield return 0.4f;

            // Tutorial step 2: Teach Kirby power use (Dash key)
            var powerTutorial = new BirdGonerTutorialGui(this, new Vector2(0f, -16f), Dialog.Clean("tutorial_kirby_power"), new object[]
            {
                BirdGonerTutorialGui.ButtonPrompt.Dash
            });

            yield return showTutorial(powerTutorial, true);

            // Wait for player to press Dash to use power
            while (player.Scene != null && !Input.Dash.Pressed)
            {
                yield return null;
            }

            if (player.Scene == null) yield break;

            Audio.Play("event:/ui/game/tutorial_note_flip_back");
            yield return hideTutorial();
            yield return 0.3f;

            // Mark tutorial complete so it doesn't show again
            Level.Session.SetFlag("kirby_tutorial_complete");

            yield return startleAndFlyAway();
        }

        public static void FlapSfxCheck(Sprite sprite)
        {
            if (sprite.Entity?.Scene is Level level)
            {
                var camera = level.Camera;
                var renderPosition = sprite.RenderPosition;
                if (renderPosition.X < camera.X - 32 || renderPosition.Y < camera.Y - 32 ||
                    renderPosition.X > camera.X + 320 + 32 || renderPosition.Y > camera.Y + 180 + 32)
                {
                    return;
                }
            }
            var currentAnimationId = sprite.CurrentAnimationID;
            var currentAnimationFrame = sprite.CurrentAnimationFrame;
            if ((currentAnimationId == "hover" && currentAnimationFrame == 0) ||
                (currentAnimationId == "hoverStressed" && currentAnimationFrame == 0) ||
                (currentAnimationId == "fly" && currentAnimationFrame == 0) ||
                (currentAnimationId == "flyupIdle" && currentAnimationFrame == 0))
            {
                Audio.Play("event:/new_content/game/10_farewell/bird_wingflap", sprite.RenderPosition);
            }
        }

        public enum Modes
        {
            ClimbingTutorial,
            DashingTutorial,
            DreamJumpTutorial,
            SuperWallJumpTutorial,
            HyperJumpTutorial,
            KirbyPlayerTutorial,
            FlyAway,
            None,
            Sleeping,
            MoveToNodes,
            WaitForLightningOff,
            /// <summary>
            /// Placed at the end of the bridge. Waits invisible until Bridge signals it,
            /// then freezes time, flies in, prompts a dash to unfreeze, and starts the ending vignette.
            /// </summary>
            BridgeEndDash
        }

        public enum BirdTypes
        {
            Default,
            Clover,
            Cody,
            Emily,
            Odin,
            Robin,
            Sabel
        }
    }
}




