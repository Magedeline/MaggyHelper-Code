using System;
using System.Collections.Generic;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Extension methods for <see cref="Random"/> inspired by MonoGame.Extended's
    /// <c>RandomExtensions</c>.  Provides shuffling, weighted sampling,
    /// and range helpers used across the PCG pipeline and entity generators.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// Fisher-Yates in-place shuffle.
        /// Replaces the hand-rolled shuffle in <c>PCGSkeletonGenerator</c> and similar code.
        /// </summary>
        public static void Shuffle<T>(this Random rng, T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>
        /// Fisher-Yates in-place shuffle for lists.
        /// </summary>
        public static void Shuffle<T>(this Random rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Return a random element from the array.
        /// </summary>
        public static T Choose<T>(this Random rng, T[] array)
        {
            return array[rng.Next(array.Length)];
        }

        /// <summary>
        /// Return a random element from the list.
        /// </summary>
        public static T Choose<T>(this Random rng, IList<T> list)
        {
            return list[rng.Next(list.Count)];
        }

        /// <summary>
        /// Return a float in [<paramref name="min"/>, <paramref name="max"/>).
        /// Mirrors MonoGame.Extended's <c>NextSingle(min, max)</c>.
        /// </summary>
        public static float NextFloat(this Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>
        /// Sample an index from a weighted distribution.
        /// <paramref name="weights"/> must have at least one positive entry.
        /// Returns the 0-based index of the chosen weight.
        /// </summary>
        public static int WeightedChoice(this Random rng, ReadOnlySpan<int> weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0) return 0;

            int roll = rng.Next(total);
            int acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll < acc) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>
        /// Sample a key from a <c>Dictionary&lt;TKey, int&gt;</c> treated as a weighted distribution.
        /// Used by the Markov chain to sample tile characters from frequency counts.
        /// </summary>
        public static TKey WeightedChoice<TKey>(this Random rng, Dictionary<TKey, int> distribution, TKey fallback)
        {
            int total = 0;
            foreach (var kv in distribution) total += kv.Value;
            if (total <= 0) return fallback;

            int roll = rng.Next(total);
            int acc = 0;
            foreach (var kv in distribution)
            {
                acc += kv.Value;
                if (roll < acc) return kv.Key;
            }
            return fallback;
        }
    }
}
