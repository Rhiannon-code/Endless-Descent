using System;
using System.Collections.Generic;

namespace EndlessDescent.Core
{
    // xorshift128 rather than System.Random, dungeon seeds are save data, so the stream must stay
    // identical across engine and runtime upgrades
    [Serializable]
    public struct DeterministicRandom
    {
        uint x;
        uint y;
        uint z;
        uint w;

        public DeterministicRandom(int seed)
        {
            unchecked
            {
                x = (uint)seed == 0u ? 0x9E3779B9u : (uint)seed;
                y = x * 1812433253u + 1u;
                z = y * 1812433253u + 1u;
                w = z * 1812433253u + 1u;
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint t = x ^ (x << 11);
                x = y;
                y = z;
                z = w;
                w = w ^ (w >> 19) ^ t ^ (t >> 8);
                return w;
            }
        }

        public int NextInt(int maxExclusive)
        {
            return maxExclusive <= 0 ? 0 : (int)(NextUInt() % (uint)maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return maxExclusive <= minInclusive ? minInclusive : minInclusive + NextInt(maxExclusive - minInclusive);
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float minInclusive, float maxExclusive)
        {
            return minInclusive + NextFloat() * (maxExclusive - minInclusive);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public void Shuffle<T>(IList<T> list)
        {
            if (list == null)
                return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
