using MaggyHelper.Entities.Kirby;

namespace MaggyHelper.Triggers.Kirby
{
    /// <summary>
    /// Trigger to enable/disable Knight mode or set chapter-specific flags.
    /// 
    /// This trigger is designed for use in:
    /// - Chapter 19: Mark the "final run" segment where Knight mode becomes available
    /// - Chapter 20: Mark the "last push" segment and auto-enable Knight mode
    /// 
    /// Placement guidelines:
    /// - Place at the start of boss rush/final sections
    /// - Use setFinalRun for Chapter 19's climax
    /// - Use setLastPush for Chapter 20's ultimate battle
    /// - Use autoTransform=true for dramatic story moments
    /// </summary>
    [CustomEntity(ids: "MaggyHelper/KirbyKnightTrigger")]
    public class KirbyKnightTrigger : Trigger
    {
        #region Enums

        public enum KnightTriggerMode
        {
            Enable,         // Enable knight mode
            Disable,        // Disable knight mode
            Toggle,         // Toggle knight mode
            SetFinalRun,    // Set Chapter 19 final run flag
            SetLastPush,    // Set Chapter 20 last push flag
            Unlock          // Just unlock knight (don't transform)
        }

        #endregion

        #region Fields

        private readonly KnightTriggerMode mode;
        private readonly bool autoTransform;
        private readonly bool playEffects;
        private readonly string requiredFlag;
        private readonly bool onlyOnce;
        private readonly float transformDelay;
        
        private bool triggered;
        private float delayTimer;

        #endregion

        #region Constructor

        public KirbyKnightTrigger(EntityData data, Vector2 offset) : base(data, offset)
        {
            mode = data.Enum("mode", KnightTriggerMode.Enable);
            autoTransform = data.Bool("autoTransform", false);
            playEffects = data.Bool("playEffects", true);
            requiredFlag = data.Attr("requiredFlag", "");
            onlyOnce = data.Bool("onlyOnce", true);
            transformDelay = data.Float("transformDelay", 0f);
        }

        #endregion

        #region Trigger Logic

        public override void OnEnter(Player player)
        {
            base.OnEnter(player);
            
            if (triggered && onlyOnce) return;
            
            // Check required flag
            if (!string.IsNullOrEmpty(requiredFlag))
            {
                var level = Scene as Level;
                if (level != null && !level.Session.GetFlag(requiredFlag))
                {
                    return;
                }
            }
            
            triggered = true;
            
            if (transformDelay > 0)
            {
                delayTimer = transformDelay;
            }
            else
            {
                ExecuteTrigger(player);
            }
        }

        public override void Update()
        {
            base.Update();
            
            if (delayTimer > 0)
            {
                delayTimer -= Engine.DeltaTime;
                if (delayTimer <= 0)
                {
                    var player = Scene.Tracker.GetEntity<Player>();
                    if (player != null)
                    {
                        ExecuteTrigger(player);
                    }
                }
            }
        }

        private void ExecuteTrigger(Player player)
        {
            var level = Scene as Level;
            if (level == null) return;
            
            switch (mode)
            {
                case KnightTriggerMode.Enable:
                    EnableKnightMode(level);
                    break;
                    
                case KnightTriggerMode.Disable:
                    DisableKnightMode(level);
                    break;
                    
                case KnightTriggerMode.Toggle:
                    ToggleKnightMode(level);
                    break;
                    
                case KnightTriggerMode.SetFinalRun:
                    SetFinalRunFlag(level);
                    break;
                    
                case KnightTriggerMode.SetLastPush:
                    SetLastPushFlag(level);
                    break;
                    
                case KnightTriggerMode.Unlock:
                    UnlockKnight(level);
                    break;
            }
        }

        #endregion

        #region Mode Implementations

        private void EnableKnightMode(Level level)
        {
            if (playEffects)
            {
                PlayTransformEffects(level);
            }
            
            // Ensure knight mode entity exists
            EnsureKnightModeEntity(level);
            
            // Activate
            KirbyKnightMode.TryActivateKnightMode(level, force: true);
            
            IngesteLogger.Info("KirbyKnightTrigger: Knight mode enabled");
        }

        private void DisableKnightMode(Level level)
        {
            if (playEffects)
            {
                PlayDetransformEffects(level);
            }
            
            KirbyKnightMode.DeactivateKnightMode(level);
            
            IngesteLogger.Info("KirbyKnightTrigger: Knight mode disabled");
        }

        private void ToggleKnightMode(Level level)
        {
            if (KirbyKnightMode.IsKnightModeActive(level))
            {
                DisableKnightMode(level);
            }
            else
            {
                EnableKnightMode(level);
            }
        }

        private void SetFinalRunFlag(Level level)
        {
            // Verify we're in chapter 19
            if (level.Session.Area.ID != 19)
            {
                IngesteLogger.Warn($"SetFinalRun triggered in chapter {level.Session.Area.ID} (expected 19)");
            }
            
            KirbyKnightMode.SetChapter19FinalRun(level, true);
            
            if (playEffects)
            {
                PlayUnlockEffects(level, "FINAL RUN");
            }
            
            // Optionally auto-transform
            if (autoTransform)
            {
                EnsureKnightModeEntity(level);
                KirbyKnightMode.TryActivateKnightMode(level);
            }
            
            IngesteLogger.Info("KirbyKnightTrigger: Chapter 19 Final Run activated");
        }

        private void SetLastPushFlag(Level level)
        {
            // Verify we're in chapter 20
            if (level.Session.Area.ID != 20)
            {
                IngesteLogger.Warn($"SetLastPush triggered in chapter {level.Session.Area.ID} (expected 20)");
            }
            
            KirbyKnightMode.SetChapter20LastPush(level, true);
            
            if (playEffects)
            {
                PlayUnlockEffects(level, "THE LAST PUSH");
            }
            
            // Auto-transform in chapter 20 (this is the final battle!)
            if (autoTransform)
            {
                EnsureKnightModeEntity(level);
                KirbyKnightMode.TryActivateKnightMode(level, force: true);
            }
            
            IngesteLogger.Info("KirbyKnightTrigger: Chapter 20 Last Push activated");
        }

        private void UnlockKnight(Level level)
        {
            EnsureKnightModeEntity(level);
            level.Session.SetFlag("kirby_knight_unlocked", true);
            
            if (playEffects)
            {
                PlayUnlockEffects(level, "KNIGHT UNLOCKED");
            }
            
            IngesteLogger.Info("KirbyKnightTrigger: Knight mode unlocked (not transformed)");
        }

        #endregion

        #region Effects

        private void PlayTransformEffects(Level level)
        {
            level.Flash(Color.Gold * 0.4f, true);
            level.Shake(0.3f);
            Audio.Play("event:/desolozantas/char/kirby/knight_transform", Position);
            
            // Golden particles burst
            if (level.ParticlesFG != null)
            {
                for (int i = 0; i < 30; i++)
                {
                    float angle = Calc.Random.NextFloat((float)Math.PI * 2f);
                    Vector2 dir = Calc.AngleToVector(angle, Calc.Random.Range(20f, 60f));
                    level.ParticlesFG.Emit(
                        ParticleTypes.Dust,
                        Position + dir * 0.3f,
                        Color.Gold,
                        angle
                    );
                }
            }
        }

        private void PlayDetransformEffects(Level level)
        {
            level.Flash(Color.White * 0.2f, true);
            Audio.Play("event:/desolozantas/char/kirby/knight_detransform", Position);
        }

        private void PlayUnlockEffects(Level level, string text)
        {
            level.Flash(Color.Gold * 0.3f, true);
            level.Shake(0.2f);
            Audio.Play("event:/ui/game/memorial_text_in", Position);
            
            // Display unlock text
            level.Add(new KnightUnlockText(Position, text));
        }

        private void EnsureKnightModeEntity(Level level)
        {
            var existing = level.Tracker.GetEntity<KirbyKnightMode>();
            if (existing == null)
            {
                level.Add(new KirbyKnightMode());
            }
        }

        #endregion
    }

    /// <summary>
    /// Floating text display for Knight unlock notifications
    /// </summary>
    public class KnightUnlockText : Entity
    {
        private string text;
        private float timer;
        private float alpha;
        private Vector2 startPos;

        public KnightUnlockText(Vector2 position, string text) : base(position)
        {
            this.text = text;
            this.startPos = position;
            Tag = Tags.HUD | Tags.FrozenUpdate;
            Depth = -10000;
        }

        public override void Update()
        {
            base.Update();
            
            timer += Engine.DeltaTime;
            
            // Fade in, hold, fade out
            if (timer < 0.5f)
            {
                alpha = Ease.SineOut(timer / 0.5f);
            }
            else if (timer < 2.5f)
            {
                alpha = 1f;
            }
            else if (timer < 3f)
            {
                alpha = 1f - Ease.SineIn((timer - 2.5f) / 0.5f);
            }
            else
            {
                RemoveSelf();
            }
            
            // Float upward
            Position = startPos + new Vector2(0, -timer * 20f);
        }

        public override void Render()
        {
            base.Render();
            
            if (Scene is not Level level) return;
            
            // Convert to screen position
            Vector2 screenPos = level.Camera.Position;
            Vector2 drawPos = new Vector2(960, 400); // Center of 1080p screen
            
            // Draw with glow effect
            Color glowColor = Color.Gold * alpha * 0.5f;
            Color textColor = Color.White * alpha;
            
            // Background glow
            ActiveFont.DrawOutline(
                text,
                drawPos,
                new Vector2(0.5f, 0.5f),
                Vector2.One * 1.5f,
                glowColor,
                2f,
                Color.Black * alpha * 0.3f
            );
            
            // Main text
            ActiveFont.DrawOutline(
                text,
                drawPos,
                new Vector2(0.5f, 0.5f),
                Vector2.One * 1.2f,
                textColor,
                2f,
                Color.Black * alpha
            );
        }
    }
}
