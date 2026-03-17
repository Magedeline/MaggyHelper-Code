using Microsoft.Xna.Framework;
using Monocle;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Primitive shape drawing helpers inspired by MonoGame.Extended's <c>ShapeExtensions</c>.
    /// Built on Monocle's <see cref="Draw"/> API so they work with Celeste's existing
    /// rendering pipeline — no SpriteBatch changes required.
    /// </summary>
    public static class ShapeRenderer
    {
        /// <summary>
        /// Draw a circle outline with configurable thickness.
        /// Replaces the many <c>Draw.Circle</c> calls that pass a fixed resolution
        /// and always use thickness 1–2.
        /// </summary>
        public static void DrawCircleOutline(Vector2 center, float radius, Color color,
            float thickness = 1f, int segments = 32)
        {
            float step = MathHelper.TwoPi / segments;
            Vector2 prev = center + new Vector2(radius, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector2 next = center + new Vector2(
                    (float)System.Math.Cos(angle) * radius,
                    (float)System.Math.Sin(angle) * radius);
                Draw.Line(prev, next, color, thickness);
                prev = next;
            }
        }

        /// <summary>
        /// Draw a filled circle by rendering concentric lines from centre outward.
        /// Useful for energy ball / homing projectile rendering.
        /// </summary>
        public static void DrawCircleFilled(Vector2 center, float radius, Color color, int segments = 32)
        {
            float step = MathHelper.TwoPi / segments;
            for (int i = 0; i < segments; i++)
            {
                float a1 = step * i;
                float a2 = step * (i + 1);
                Vector2 p1 = center + new Vector2(
                    (float)System.Math.Cos(a1) * radius,
                    (float)System.Math.Sin(a1) * radius);
                Vector2 p2 = center + new Vector2(
                    (float)System.Math.Cos(a2) * radius,
                    (float)System.Math.Sin(a2) * radius);
                // Triangle fan: center → p1 → p2 (approximated with line from center)
                Draw.Line(center, p1, color);
                Draw.Line(p1, p2, color);
            }
        }

        /// <summary>
        /// Draw a horizontal colour gradient filling the given rectangle.
        /// Replaces the per-pixel loop in <c>IngesteUtils.DrawGradient</c>.
        /// Uses wider strips for better performance.
        /// </summary>
        public static void DrawGradientH(float x, float y, float width, float height,
            Color left, Color right, int strips = 0)
        {
            if (strips <= 0)
                strips = System.Math.Max(1, (int)(width / 2f));

            float stripW = width / strips;
            for (int i = 0; i < strips; i++)
            {
                float t = (float)i / (strips - 1);
                Color c = Color.Lerp(left, right, t);
                Draw.Rect(x + i * stripW, y, stripW + 0.5f, height, c);
            }
        }

        /// <summary>
        /// Draw a vertical colour gradient filling the given rectangle.
        /// </summary>
        public static void DrawGradientV(float x, float y, float width, float height,
            Color top, Color bottom, int strips = 0)
        {
            if (strips <= 0)
                strips = System.Math.Max(1, (int)(height / 2f));

            float stripH = height / strips;
            for (int i = 0; i < strips; i++)
            {
                float t = (float)i / (strips - 1);
                Color c = Color.Lerp(top, bottom, t);
                Draw.Rect(x, y + i * stripH, width, stripH + 0.5f, c);
            }
        }

        /// <summary>
        /// Draw a trail of circles behind a moving object.
        /// Common pattern in projectile rendering (homing, bouncing, energy balls).
        /// </summary>
        /// <param name="head">Current position of the moving object.</param>
        /// <param name="direction">Normalised movement direction.</param>
        /// <param name="color">Base trail colour.</param>
        /// <param name="count">Number of trail segments.</param>
        /// <param name="spacing">Pixel spacing between segments.</param>
        /// <param name="startRadius">Radius of the first (nearest) trail segment.</param>
        /// <param name="radiusShrink">Radius reduction per segment.</param>
        /// <param name="alphaFade">Alpha reduction per segment.</param>
        /// <param name="segments">Circle rendering resolution.</param>
        public static void DrawTrail(Vector2 head, Vector2 direction, Color color,
            int count = 3, float spacing = 8f, float startRadius = 4f,
            float radiusShrink = 1f, float alphaFade = 0.15f, int segments = 6)
        {
            for (int i = 1; i <= count; i++)
            {
                Vector2 pos = head - direction * (i * spacing);
                float r = System.Math.Max(1f, startRadius - i * radiusShrink);
                float a = System.Math.Max(0f, 1f - i * alphaFade);
                DrawCircleOutline(pos, r, color * a, 1f, segments);
            }
        }
    }
}
