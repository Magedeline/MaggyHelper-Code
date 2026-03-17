using System;

namespace MaggyHelper.Utils
{
    /// <summary>
    /// Small deterministic PCG32 generator with a 64-bit internal state.
    /// Keep seeds and streams within 32 bits when matching Loenn's Lua port.
    /// </summary>
    public sealed class Pcg32Random : Random
    {
        private const ulong Multiplier = 6364136223846793005ul;
        private const double UInt32Scale = 1.0 / 4294967296.0;

        private ulong state;
        private readonly ulong increment;

        public Pcg32Random(uint seed, uint sequence = 54u)
            : this((ulong)seed, (ulong)sequence)
        {
        }

        public Pcg32Random(ulong seed, ulong sequence = 54ul)
        {
            increment = (sequence << 1) | 1ul;
            state = 0ul;
            NextUInt32();
            state = unchecked(state + seed);
            NextUInt32();
        }

        protected override double Sample()
        {
            return NextUInt32() * UInt32Scale;
        }

        public override int Next()
        {
            return Next(int.MaxValue);
        }

        public uint NextUInt32()
        {
            ulong oldState = state;
            state = unchecked(oldState * Multiplier + increment);

            uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);

            return RotateRight(xorshifted, rotation);
        }

        public override int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");

            uint bound = (uint)maxExclusive;
            uint threshold = (uint)(0u - bound) % bound;

            while (true)
            {
                uint roll = NextUInt32();
                if (roll >= threshold)
                    return (int)(roll % bound);
            }
        }

        public override int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            return minInclusive + Next(maxExclusive - minInclusive);
        }

        public float NextFloat()
        {
            return (float)(NextUInt32() * UInt32Scale);
        }

        public override double NextDouble()
        {
            return NextUInt32() * UInt32Scale;
        }

        public override void NextBytes(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            int index = 0;
            while (index < buffer.Length)
            {
                uint value = NextUInt32();
                for (int offset = 0; offset < 4 && index < buffer.Length; offset++)
                {
                    buffer[index++] = (byte)(value >> (offset * 8));
                }
            }
        }

        public bool NextBool()
        {
            return (NextUInt32() & 1u) != 0;
        }

        private static uint RotateRight(uint value, int rotation)
        {
            rotation &= 31;
            if (rotation == 0)
                return value;

            return (value >> rotation) | (value << ((-rotation) & 31));
        }
    }
}