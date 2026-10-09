using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.World
{
    // Greybox ground colour, one flat layer per biome, taken from the Imperial Survey's own palette
    // so the world and the map of it agree about what colour a fen is
    public static class WorldBiomePalette
    {
        public static Color Of(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Sea: return new Color32(0x6F, 0x94, 0xA6, 0xFF);
                case BiomeId.Marsh: return new Color32(0xB7, 0xC3, 0xB3, 0xFF);
                case BiomeId.Fen: return new Color32(0x9F, 0xB0, 0xA4, 0xFF);
                case BiomeId.Plain: return new Color32(0xCF, 0xD3, 0xB0, 0xFF);
                case BiomeId.Moor: return new Color32(0xC3, 0xBC, 0xA6, 0xFF);
                case BiomeId.Dry: return new Color32(0xDC, 0xD3, 0xB2, 0xFF);
                case BiomeId.Desert: return new Color32(0xE4, 0xD6, 0xA9, 0xFF);
                case BiomeId.Mountain: return new Color32(0x8A, 0x88, 0x7F, 0xFF);

                // Upland is not a gap in the map, it is most of the empire, wooded rolling country
                // between the named regions. At near white it read as blank paper, which is why
                // three fifths of the world looked unfinished
                default: return new Color32(0xAB, 0xB6, 0x92, 0xFF);
            }
        }

        public static TerrainLayer[] Layers()
        {
            int count = WorldTileData.BiomeCount;
            TerrainLayer[] layers = new TerrainLayer[count];

            for (int i = 0; i < count; i++)
            {
                Texture2D texture = new Texture2D(1, 1) { name = $"Biome_{(BiomeId)i}" };
                texture.SetPixel(0, 0, Of((BiomeId)i));
                texture.Apply();

                layers[i] = new TerrainLayer
                {
                    name = ((BiomeId)i).ToString(),
                    diffuseTexture = texture,
                    tileSize = new Vector2(32f, 32f)
                };
            }

            return layers;
        }
    }
}
