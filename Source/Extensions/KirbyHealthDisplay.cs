using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Reflection;
using MaggyHelper.Extensions.Kirby;

namespace MaggyHelper.Extensions
{
    /// <summary>
    /// Health display HUD for KirbyPlayerExtension / KirbyMode.
    /// Shows health hearts, stamina bar, and current power ability icon.
    /// Appears in the top-left corner of the screen.
    /// </summary>
    [Tracked]
    public class KirbyHealthDisplay : Entity
    {
        #region Constants

        private const float HEART_SIZE = 16f;
        private const float HEART_SPACING = 18f;
        private const float HUD_PADDING = 16f;
        private const float STAMINA_BAR_WIDTH = 100f;
        private const float STAMINA_BAR_HEIGHT = 8f;
        private const float POWER_ICON_SIZE = 24f;
        private const float FADE_IN_TIME = 0.3f;
        private const float FADE_OUT_TIME = 0.5f;
        private const float LOW_HEALTH_PULSE_SPEED = 4f;

        #endregion

        #region Fields

        // Support both old KirbyMode and new KirbyPlayerExtension
        private KirbyMode kirby;
        private KirbyPlayerExtension kirbyExt;
        private Level level;

        // Display state
        private float alpha = 0f;
        private bool visible = true;
        private float pulseTimer = 0f;

        // Heart sprites
        private MTexture heartFull;
        private MTexture heartHalf;
        private MTexture heartEmpty;

        // Power icons
        private MTexture[] powerIcons;

        // Colors
        private Color healthColor = Calc.HexToColor("FF69B4"); // Pink
        private Color staminaColor = Calc.HexToColor("87CEEB"); // Sky blue
        private Color lowHealthColor = Calc.HexToColor("FF0000"); // Red
        private Color bgColor = Color.Black * 0.6f;

        #endregion

        #region Constructor

        public KirbyHealthDisplay(KirbyMode kirby) : base(Vector2.Zero)
        {
            this.kirby = kirby;
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
            Depth = -10000;

            LoadTextures();
        }

        public KirbyHealthDisplay(KirbyPlayerExtension kirbyExt) : base(Vector2.Zero)
        {
            this.kirbyExt = kirbyExt;
            Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
            Depth = -10000;

            LoadTextures();
        }

        private void LoadTextures()
        {
            try
            {
                // Try to load custom heart sprites
                heartFull = GFX.Gui["kirby/heart_full"];
                heartHalf = GFX.Gui["kirby/heart_half"];
                heartEmpty = GFX.Gui["kirby/heart_empty"];
            }
            catch
            {
                // Fallback to basic shapes if textures don't exist
                heartFull = null;
                heartHalf = null;
                heartEmpty = null;
            }

            // Load power icons
            powerIcons = new MTexture[(int)KirbyMode.KirbyPowerState.Knight + 1];
            try
            {
                var powerStates = Enum.GetValues(typeof(KirbyMode.KirbyPowerState));
                foreach (KirbyMode.KirbyPowerState power in powerStates)
                {
                    try
                    {
                        powerIcons[(int)power] = GFX.Gui[$"kirby/powers/{power.ToString().ToLower()}"];
                    }
                    catch
                    {
                        powerIcons[(int)power] = null;
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Lifecycle

        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = scene as Level;
        }

        public override void Update()
        {
            base.Update();

            // Support both sources
            bool hasSource = (kirby != null && kirby.Scene == Scene) || (kirbyExt != null && kirbyExt.Scene == Scene);
            if (!hasSource)
            {
                RemoveSelf();
                return;
            }

            int currentHealth = kirbyExt?.CurrentHealth ?? kirby?.CurrentHealth ?? 0;
            bool isDead = kirbyExt?.IsDead ?? kirby?.IsDead ?? false;

            // Update alpha for fade in/out
            if (visible && alpha < 1f)
            {
                alpha = Calc.Approach(alpha, 1f, Engine.DeltaTime / FADE_IN_TIME);
            }
            else if (!visible && alpha > 0f)
            {
                alpha = Calc.Approach(alpha, 0f, Engine.DeltaTime / FADE_OUT_TIME);
            }

            // Update pulse effect for low health
            if (currentHealth > 0 && currentHealth <= 2)
            {
                pulseTimer += Engine.DeltaTime * LOW_HEALTH_PULSE_SPEED;
            }
            else
            {
                pulseTimer = 0f;
            }
        }

        // Unified accessors for either source
        private int GetCurrentHealth() => kirbyExt?.CurrentHealth ?? kirby?.CurrentHealth ?? 0;
        private int GetMaxHealth() => kirbyExt?.MaxHealth ?? kirby?.MaxHealth ?? 6;
        private float GetMaxStamina() => kirbyExt?.MaxStamina ?? kirby?.MaxStamina ?? 100f;
        private bool GetIsDead() => kirbyExt?.IsDead ?? kirby?.IsDead ?? false;
        private KirbyMode.KirbyPowerState GetCurrentPower() => kirbyExt?.CurrentPower ?? kirby?.CurrentPower ?? KirbyMode.KirbyPowerState.None;

        public override void Render()
        {
            if (alpha <= 0f || GetIsDead())
                return;

            base.Render();

            Vector2 position = new Vector2(HUD_PADDING, HUD_PADDING);

            // Draw background panel
            DrawBackground(position);

            // Draw health hearts
            DrawHealth(position + new Vector2(8f, 8f));

            // Draw stamina bar
            DrawStamina(position + new Vector2(8f, 32f));

            // Draw power icon
            if (GetCurrentPower() != KirbyMode.KirbyPowerState.None)
            {
                DrawPowerIcon(position + new Vector2(8f, 48f));
            }
        }

        #endregion

        #region Drawing

        private void DrawBackground(Vector2 position)
        {
            float width = Math.Max(STAMINA_BAR_WIDTH + 16f, (GetMaxHealth() / 2) * HEART_SPACING + 16f);
            float height = GetCurrentPower() != KirbyMode.KirbyPowerState.None ? 80f : 56f;

            Draw.Rect(position, width, height, bgColor * alpha);
            Draw.HollowRect(position, width, height, Color.White * 0.5f * alpha);
        }

        private void DrawHealth(Vector2 position)
        {
            int health = GetCurrentHealth();
            int fullHearts = health / 2;
            bool hasHalfHeart = health % 2 == 1;
            int maxHearts = GetMaxHealth() / 2;

            // Calculate pulse for low health
            float pulseFactor = 1f;
            if (health <= 2 && health > 0)
            {
                pulseFactor = 1f + (float)Math.Sin(pulseTimer) * 0.2f;
            }

            for (int i = 0; i < maxHearts; i++)
            {
                Vector2 heartPos = position + new Vector2(i * HEART_SPACING, 0f);
                Color heartColor = Color.White;

                if (health <= 2 && i < fullHearts + (hasHalfHeart ? 1 : 0))
                {
                    heartColor = Color.Lerp(Color.White, lowHealthColor, 0.5f + (float)Math.Sin(pulseTimer) * 0.5f);
                }

                if (i < fullHearts)
                {
                    DrawHeart(heartPos, HeartType.Full, heartColor * alpha, pulseFactor);
                }
                else if (i == fullHearts && hasHalfHeart)
                {
                    DrawHeart(heartPos, HeartType.Half, heartColor * alpha, pulseFactor);
                }
                else
                {
                    DrawHeart(heartPos, HeartType.Empty, Color.White * 0.3f * alpha, 1f);
                }
            }
        }

        private enum HeartType { Full, Half, Empty }

        private void DrawHeart(Vector2 position, HeartType type, Color color, float scale = 1f)
        {
            MTexture texture = type switch
            {
                HeartType.Full => heartFull,
                HeartType.Half => heartHalf,
                HeartType.Empty => heartEmpty,
                _ => null
            };

            if (texture != null)
            {
                texture.DrawCentered(position + Vector2.One * HEART_SIZE * 0.5f, color, scale);
            }
            else
            {
                // Fallback: Draw simple heart shape
                DrawSimpleHeart(position, type, color, scale);
            }
        }

        private void DrawSimpleHeart(Vector2 position, HeartType type, Color color, float scale)
        {
            Vector2 center = position + Vector2.One * HEART_SIZE * 0.5f;
            float size = HEART_SIZE * 0.4f * scale;

            if (type == HeartType.Empty)
            {
                // Draw outline only
                Draw.Circle(center + new Vector2(-size * 0.3f, -size * 0.2f), size * 0.5f, color, 2);
                Draw.Circle(center + new Vector2(size * 0.3f, -size * 0.2f), size * 0.5f, color, 2);
            }
            else
            {
                // Draw filled circles for heart top
                Draw.Circle(center + new Vector2(-size * 0.3f, -size * 0.2f), size * 0.5f, color, 32);
                Draw.Circle(center + new Vector2(size * 0.3f, -size * 0.2f), size * 0.5f, color, 32);
                
                // Draw triangle for heart bottom
                Vector2[] triangle = new Vector2[]
                {
                    center + new Vector2(-size * 0.7f, -size * 0.2f),
                    center + new Vector2(size * 0.7f, -size * 0.2f),
                    center + new Vector2(0f, size * 0.8f)
                };
                
                Draw.Rect(center.X - size * 0.7f, center.Y - size * 0.2f, size * 1.4f, size, color);

                // Half heart: cover right side
                if (type == HeartType.Half)
                {
                    Draw.Rect(center.X, center.Y - size, size, size * 2, Color.Black);
                }
            }
        }

        private void DrawStamina(Vector2 position)
        {
            float maxStam = GetMaxStamina();
            if (maxStam <= 0) return;

            float staminaForFloat = kirbyExt != null ? kirbyExt.CurrentStamina : GetStaminaForFloatSafe(kirby);
            float staminaPercent = Calc.Clamp(staminaForFloat / maxStam, 0f, 1f);

            // Background
            Draw.Rect(position, STAMINA_BAR_WIDTH, STAMINA_BAR_HEIGHT, Color.Black * 0.8f * alpha);

            // Stamina fill
            float fillWidth = STAMINA_BAR_WIDTH * staminaPercent;
            Color fillColor = Color.Lerp(Color.Red, staminaColor, staminaPercent);
            Draw.Rect(position, fillWidth, STAMINA_BAR_HEIGHT, fillColor * alpha);

            // Border
            Draw.HollowRect(position, STAMINA_BAR_WIDTH, STAMINA_BAR_HEIGHT, Color.White * 0.5f * alpha);

            // Label
            Vector2 textPos = position + new Vector2(STAMINA_BAR_WIDTH + 4f, -2f);
            ActiveFont.DrawOutline(
                "FLOAT",
                textPos,
                new Vector2(0f, 0f),
                Vector2.One * 0.5f,
                Color.White * alpha,
                2f,
                Color.Black * alpha
            );
        }

        private static float GetStaminaForFloatSafe(KirbyMode kirby)
        {
            if (kirby == null)
                return 0f;

            Type t = kirby.GetType();

            // Try common property names first (including the one that used to exist).
            string[] propertyNames = new[] { "StaminaForFloat", "FloatStamina", "Stamina" };
            foreach (string name in propertyNames)
            {
                PropertyInfo prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                {
                    object value = prop.GetValue(kirby);
                    if (value != null)
                        return Convert.ToSingle(value);
                }
            }

            // Try common field names as a fallback.
            string[] fieldNames = new[] { "StaminaForFloat", "FloatStamina", "Stamina", "staminaForFloat", "floatStamina", "stamina" };
            foreach (string name in fieldNames)
            {
                FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    object value = field.GetValue(kirby);
                    if (value != null)
                        return Convert.ToSingle(value);
                }
            }

            return 0f;
        }

        private void DrawPowerIcon(Vector2 position)
        {
            var curPower = GetCurrentPower();
            var powerIcon = powerIcons[(int)curPower];

            if (powerIcon != null)
            {
                powerIcon.DrawCentered(position + Vector2.One * POWER_ICON_SIZE * 0.5f, Color.White * alpha);
            }
            else
            {
                // Fallback: Draw colored box with power name
                DrawPowerFallback(position);
            }
        }

        private void DrawPowerFallback(Vector2 position)
        {
            var curPower = GetCurrentPower();
            Color powerColor = GetPowerColor(curPower);
            Draw.Rect(position, POWER_ICON_SIZE, POWER_ICON_SIZE, powerColor * 0.7f * alpha);
            Draw.HollowRect(position, POWER_ICON_SIZE, POWER_ICON_SIZE, Color.White * alpha);

            // Draw power name abbreviation
            string powerName = curPower.ToString();
            string abbreviation = powerName.Length > 3 ? powerName.Substring(0, 3) : powerName;

            Vector2 textPos = position + new Vector2(POWER_ICON_SIZE * 0.5f, POWER_ICON_SIZE * 0.5f);
            ActiveFont.DrawOutline(
                abbreviation.ToUpper(),
                textPos,
                new Vector2(0.5f, 0.5f),
                Vector2.One * 0.4f,
                Color.White * alpha,
                1f,
                Color.Black * alpha
            );
        }

        private Color GetPowerColor(KirbyMode.KirbyPowerState power)
        {
            return power switch
            {
                KirbyMode.KirbyPowerState.Fire => Color.Orange,
                KirbyMode.KirbyPowerState.Ice => Color.LightBlue,
                KirbyMode.KirbyPowerState.Spark => Color.Yellow,
                KirbyMode.KirbyPowerState.Stone => Color.Gray,
                KirbyMode.KirbyPowerState.Sword => Color.SteelBlue,
                KirbyMode.KirbyPowerState.Beam => Color.Cyan,
                KirbyMode.KirbyPowerState.Water => Color.Blue,
                KirbyMode.KirbyPowerState.Knight => Color.Gold,
                KirbyMode.KirbyPowerState.Bomb => Color.Red,
                KirbyMode.KirbyPowerState.Cutter => Color.Pink,
                _ => Color.Pink
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Show the health display
        /// </summary>
        public void Show()
        {
            visible = true;
        }

        /// <summary>
        /// Hide the health display
        /// </summary>
        public void Hide()
        {
            visible = false;
        }

        /// <summary>
        /// Toggle visibility
        /// </summary>
        public void Toggle()
        {
            visible = !visible;
        }

        #endregion
    }
}
