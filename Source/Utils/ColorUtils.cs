using System;
using Microsoft.Xna.Framework;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Color utility methods inspired by MonoGame.Extended's <c>ColorHelper</c> and <c>HslColor</c>.
    /// Provides HSV/HSL conversion, spectrum generation, and interpolation helpers
    /// that work natively with FNA's <see cref="Color"/> type.
    /// </summary>
    public static class ColorUtils
    {
        /// <summary>
        /// Convert HSV (Hue 0–1, Saturation 0–1, Value 0–1) to an XNA <see cref="Color"/>.
        /// Drop-in replacement for hand-rolled HSV converters throughout the codebase.
        /// </summary>
        public static Color FromHsv(float h, float s, float v)
        {
            h = ((h % 1f) + 1f) % 1f; // wrap to [0,1)
            int i = (int)Math.Floor(h * 6f);
            float f = h * 6f - i;
            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);

            return (i % 6) switch
            {
                0 => new Color(v, t, p),
                1 => new Color(q, v, p),
                2 => new Color(p, v, t),
                3 => new Color(p, q, v),
                4 => new Color(t, p, v),
                5 => new Color(v, p, q),
                _ => Color.White,
            };
        }

        /// <summary>
        /// Convert HSL (Hue 0–1, Saturation 0–1, Lightness 0–1) to an XNA <see cref="Color"/>.
        /// Matches MonoGame.Extended's <c>HslColor.ToRgb()</c>.
        /// </summary>
        public static Color FromHsl(float h, float s, float l)
        {
            h = ((h % 1f) + 1f) % 1f;

            if (s < 0.001f)
                return new Color(l, l, l);

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            float r = HueToRgb(p, q, h + 1f / 3f);
            float g = HueToRgb(p, q, h);
            float b = HueToRgb(p, q, h - 1f / 3f);

            return new Color(r, g, b);
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        /// <summary>
        /// Generate an evenly-spaced rainbow palette with <paramref name="count"/> colours.
        /// Replaces manual for-loops that compute <c>HSV(i/n, 1, 1)</c>.
        /// </summary>
        public static Color[] GenerateRainbowPalette(int count, float saturation = 1f, float value = 1f)
        {
            var palette = new Color[count];
            for (int i = 0; i < count; i++)
                palette[i] = FromHsv((float)i / count, saturation, value);
            return palette;
        }

        /// <summary>
        /// Sample a smoothly-cycling rainbow colour at the given <paramref name="phase"/> (radians).
        /// Commonly used for time-based rainbow effects:
        /// <c>ColorUtils.RainbowFromPhase(time * speed + offset)</c>.
        /// </summary>
        public static Color RainbowFromPhase(float phase, float saturation = 1f, float value = 1f)
        {
            float hue = (phase / (MathF.PI * 2f)) % 1f;
            if (hue < 0f) hue += 1f;
            return FromHsv(hue, saturation, value);
        }

        /// <summary>
        /// Linearly interpolate between two colours with an optional alpha override.
        /// Wraps <see cref="Color.Lerp"/> but accepts an extra alpha multiplier used
        /// in fade-in / fade-out transitions.
        /// </summary>
        public static Color LerpWithAlpha(Color a, Color b, float t, float alpha)
        {
            var result = Color.Lerp(a, b, MathHelper.Clamp(t, 0f, 1f));
            result.A = (byte)(result.A * MathHelper.Clamp(alpha, 0f, 1f));
            return result;
        }
    }
}
