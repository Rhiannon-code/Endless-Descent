using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.World
{
    // One tile's worth of ground, computed and nothing more. No Unity object is touched here, which
    // is what lets Build run on a worker thread, at 257 square a tile costs about 200 ms, and that
    // is far too long to spend inside a frame
    public sealed class WorldTileData
    {
        public readonly int TileX;
        public readonly int TileY;
        public readonly float[,] Heights;
        public readonly float[,,] Alphas;
        public readonly TreeInstance[] Trees;

        WorldTileData(int tileX, int tileY, float[,] heights, float[,,] alphas, TreeInstance[] trees)
        {
            TileX = tileX;
            TileY = tileY;
            Heights = heights;
            Alphas = alphas;
            Trees = trees;
        }

        public static WorldTileData Build(WorldSurface surface, int tileX, int tileY,
            int resolution, int alphaResolution, int seed)
        {
            // Unity indexes both of these [y, x], with y running north
            float[,] heights = new float[resolution, resolution];
            float[,,] alphas = new float[alphaResolution, alphaResolution, BiomeCount];

            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 km = WorldTiles.SampleKm(tileX, tileY, x, y, resolution);
                heights[y, x] = WorldTiles.Normalised(surface.Height(km));
            }

            for (int y = 0; y < alphaResolution; y++)
            for (int x = 0; x < alphaResolution; x++)
            {
                Vector2 km = WorldTiles.AlphaKm(tileX, tileY, x, y, alphaResolution);
                alphas[y, x, (int)surface.Biome(km)] = 1f;
            }

            WorldVegetation.PlantInstance[] scattered =
                WorldVegetation.Scatter(surface, tileX, tileY, seed);

            TreeInstance[] trees = new TreeInstance[scattered.Length];

            for (int i = 0; i < scattered.Length; i++)
                trees[i] = new TreeInstance
                {
                    position = new Vector3(scattered[i].U, 0f, scattered[i].V),
                    prototypeIndex = scattered[i].Prototype,
                    widthScale = scattered[i].Width,
                    heightScale = scattered[i].Height,
                    rotation = scattered[i].Rotation,
                    color = Color.white,
                    lightmapColor = Color.white
                };

            return new WorldTileData(tileX, tileY, heights, alphas, trees);
        }

        public static int BiomeCount => System.Enum.GetValues(typeof(BiomeId)).Length;
    }
}
