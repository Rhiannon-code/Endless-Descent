using UnityEngine;

namespace EndlessDescent.Core
{
    // Ground that is not flat. Value noise rather than Perlin so the same seed gives the same hills
    // on every machine and in every build, which the save system needs
    public readonly struct TerrainField
    {
        readonly int seed;
        readonly float relief;
        readonly float scale;

        public TerrainField(int seed, float relief, float scale)
        {
            this.seed = seed;
            this.relief = relief;
            this.scale = Mathf.Max(1f, scale);
        }

        public float Height(float x, float z)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f / scale;
            float norm = 0f;

            for (int octave = 0; octave < 3; octave++)
            {
                total += Noise(x * frequency, z * frequency, seed + octave * 7919) * amplitude;
                norm += amplitude;
                amplitude *= 0.45f;
                frequency *= 2.3f;
            }

            return (total / norm - 0.5f) * 2f * relief;
        }

        public float Height(Vector2 at) => Height(at.x, at.y);

        static float Noise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;

            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);

            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
