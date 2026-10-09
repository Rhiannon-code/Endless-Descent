using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // What grows where. Scattered from the surface rather than placed, and hashed from the tile
    // coordinate rather than from a running random, so a tile that streams out and back comes back
    // with the same trees standing in the same places
    public static class WorldVegetation
    {
        // Fourteen kinds, so a moor does not look like a marsh. The prefab for each is named
        // Plant_<name>, which is how the content builder and the scene builder find them, nothing
        // anywhere carries a hand written list in this order
        public enum Plant
        {
            Broadleaf, Pine, Birch, Willow, Dead,
            Bush, Bramble, Fern, Reeds, Sedge,
            Heather, Gorse, Thorn, Juniper
        }

        public static readonly int PlantCount = System.Enum.GetValues(typeof(Plant)).Length;

        // Plain data, not a TreeInstance, Game.Data has no business depending on the Terrain module,
        // and this way the scatter can be measured headless
        public readonly struct PlantInstance
        {
            public readonly float U;
            public readonly float V;
            public readonly int Prototype;
            public readonly float Width;
            public readonly float Height;
            public readonly float Rotation;

            public PlantInstance(float u, float v, int prototype, float width, float height, float rotation)
            {
                U = u;
                V = v;
                Prototype = prototype;
                Width = width;
                Height = height;
                Rotation = rotation;
            }
        }

        const float CellMetres = 9f;
        const float TreelineMetres = 900f;
        // Things grow on hillsides. At 0.55 the bolder terrain was rejecting more candidates than
        // the raised densities were adding, so the plain came out thinner after being thickened
        const float SteepestSlope = 0.78f;

        // Clear of running water and of anything anyone drives a cart along, with a verge either side
        const float ChannelKm = 0.045f;
        const float CarriagewayKm = 0.055f;

        public static PlantInstance[] Scatter(WorldSurface surface, int tileX, int tileY, int seed)
        {
            int cells = Mathf.Max(1, Mathf.RoundToInt(WorldTiles.TileKm * WorldSurface.MetresPerKm / CellMetres));
            List<PlantInstance> plants = new List<PlantInstance>();
            float cellKm = WorldTiles.TileKm / cells;

            for (int cy = 0; cy < cells; cy++)
            for (int cx = 0; cx < cells; cx++)
            {
                uint hash = Hash(tileX, tileY, cx, cy, seed);

                float jitterX = Unit(hash, 0);
                float jitterY = Unit(hash, 1);
                float u = (cx + jitterX) / cells;
                float v = (cy + jitterY) / cells;

                Vector2 km = WorldTiles.OriginKm(tileX, tileY)
                    + new Vector2(u * WorldTiles.TileKm, v * WorldTiles.TileKm);

                SurfaceSample sample = surface.Sample(km);

                if (sample.Biome == BiomeId.Sea || sample.Submerged)
                    continue;

                // Nothing grows in the channel or on the carriageway. Terrain scatters where it is
                // told and has no idea either exists, so it has to be told here
                if (sample.RiverKm < ChannelKm || sample.RoadKm < CarriagewayKm)
                    continue;

                if (Unit(hash, 2) > Density(sample.Biome))
                    continue;

                Plant plant = Pick(sample.Biome, Unit(hash, 3));

                if (IsTree(plant) && sample.HeightMetres > TreelineMetres)
                    continue;

                if (Slope(surface, km, cellKm, sample.HeightMetres) > SteepestSlope)
                    continue;

                float scale = 0.8f + Unit(hash, 4) * 0.5f;

                plants.Add(new PlantInstance(u, v, (int)plant, scale,
                    scale * (0.85f + Unit(hash, 5) * 0.4f), Unit(hash, 6) * Mathf.PI * 2f));
            }

            return plants.ToArray();
        }

        // The height here is already known by the time a candidate gets this far, and asking the
        // surface for it again was a third of the whole scatter
        static float Slope(WorldSurface surface, Vector2 km, float stepKm, float here)
        {
            float east = surface.Height(km + new Vector2(stepKm, 0f));
            float north = surface.Height(km + new Vector2(0f, stepKm));
            float run = stepKm * WorldSurface.MetresPerKm;

            return Mathf.Max(Mathf.Abs(east - here), Mathf.Abs(north - here)) / run;
        }

        public static float Cover(BiomeId biome) => Density(biome);

        static float Density(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Marsh: return 0.38f;
                case BiomeId.Fen: return 0.34f;
                case BiomeId.Plain: return 0.36f;
                case BiomeId.Moor: return 0.20f;
                case BiomeId.Dry: return 0.11f;
                case BiomeId.Desert: return 0.015f;
                case BiomeId.Mountain: return 0.10f;
                default: return 0.58f;
            }
        }

        public static bool IsTree(Plant plant) =>
            plant == Plant.Broadleaf || plant == Plant.Pine || plant == Plant.Birch
            || plant == Plant.Willow || plant == Plant.Dead;

        // What grows where. A reedbed, a heather moor and a thorn waste should not be the same
        // scatter in different colours
        static Plant Pick(BiomeId biome, float roll)
        {
            switch (biome)
            {
                case BiomeId.Marsh:
                    return Weighted(roll,
                        (Plant.Reeds, 0.42f), (Plant.Sedge, 0.20f), (Plant.Willow, 0.16f),
                        (Plant.Bush, 0.12f), (Plant.Dead, 0.06f), (Plant.Broadleaf, 0.04f));

                case BiomeId.Fen:
                    return Weighted(roll,
                        (Plant.Sedge, 0.34f), (Plant.Reeds, 0.26f), (Plant.Dead, 0.18f),
                        (Plant.Willow, 0.14f), (Plant.Bush, 0.08f));

                case BiomeId.Plain:
                    return Weighted(roll,
                        (Plant.Broadleaf, 0.38f), (Plant.Bramble, 0.24f), (Plant.Bush, 0.20f),
                        (Plant.Birch, 0.18f));

                case BiomeId.Moor:
                    return Weighted(roll,
                        (Plant.Heather, 0.48f), (Plant.Gorse, 0.30f), (Plant.Bush, 0.12f),
                        (Plant.Birch, 0.06f), (Plant.Dead, 0.04f));

                case BiomeId.Dry:
                    return Weighted(roll,
                        (Plant.Thorn, 0.38f), (Plant.Gorse, 0.28f), (Plant.Juniper, 0.22f),
                        (Plant.Dead, 0.12f));

                case BiomeId.Desert:
                    return Weighted(roll, (Plant.Thorn, 0.70f), (Plant.Juniper, 0.30f));

                case BiomeId.Mountain:
                    return Weighted(roll,
                        (Plant.Pine, 0.44f), (Plant.Juniper, 0.30f), (Plant.Heather, 0.16f),
                        (Plant.Bush, 0.10f));

                default:
                    return Weighted(roll,
                        (Plant.Broadleaf, 0.28f), (Plant.Birch, 0.20f), (Plant.Fern, 0.16f),
                        (Plant.Pine, 0.14f), (Plant.Bramble, 0.12f), (Plant.Bush, 0.10f));
            }
        }

        static Plant Weighted(float roll, params (Plant plant, float share)[] mix)
        {
            float running = 0f;

            foreach ((Plant plant, float share) in mix)
            {
                running += share;
                if (roll < running) return plant;
            }

            return mix[mix.Length - 1].plant;
        }

        static uint Hash(int tileX, int tileY, int cx, int cy, int seed)
        {
            unchecked
            {
                uint n = (uint)(tileX * 73856093 ^ tileY * 19349663 ^ cx * 83492791 ^ cy * 2971215073 ^ seed * 6700417);
                n ^= n >> 13;
                n *= 1274126177u;
                return n ^ (n >> 16);
            }
        }

        // One hash, several independent-enough rolls out of it
        static float Unit(uint hash, int index)
        {
            unchecked
            {
                uint n = hash * (uint)(index * 2 + 1) + (uint)(index * 374761393);
                n ^= n >> 15;
                n *= 2246822519u;
                n ^= n >> 13;
                return (n & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
