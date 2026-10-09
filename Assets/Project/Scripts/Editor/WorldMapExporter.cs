using System.IO;
using System.Threading.Tasks;
using EndlessDescent.Data;
using EndlessDescent.World;
using UnityEditor;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    // A map of the world as it actually is, rather than as it was drawn, every pixel is a real
    // WorldSurface sample, so if the ground and the map ever disagree it is the map that is right
    // and the Survey that has drifted
    public static class WorldMapExporter
    {
        internal const string OutputPath = "Assets/Project/Content/Generated/WorldMap.png";
        const int PixelsPerKm = 4;
        const int Seed = 20260907;
        const float RiverKm = 0.7f;
        const float RoadKm = 0.35f;

        // Relief read over a quarter of a kilometre only ever picks up the detail noise, which is
        // why the land came out flat and mottled with every hill and scarp in it invisible
        const float ReliefStepKm = 1.2f;

        [MenuItem("Endless Descent/Export World Map")]
        public static void Export()
        {
            if (!EditorUtility.DisplayDialog("Export world map",
                    $"Samples the whole survey and overwrites:\n\n{OutputPath}\n\nNothing else is touched.",
                    "Export", "Cancel"))
                return;

            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            WorldGeography geography = WorldGeography.Ordovan();
            WorldSurface surface = new WorldSurface(geography, Seed);

            Rect area = WorldMapView.Bounds(geography);

            int width = Mathf.RoundToInt(area.width * PixelsPerKm);
            int height = Mathf.RoundToInt(area.height * PixelsPerKm);
            Color32[] pixels = new Color32[width * height];

            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

            // Sample is pure and thread safe, so the map costs wall clock only once per core
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 km = new Vector2(area.xMin + x / (float)PixelsPerKm, area.yMin + y / (float)PixelsPerKm);
                    pixels[y * width + x] = Shade(surface, km);
                }
            });

            clock.Stop();

            foreach (OrdovanPlaces.Place place in OrdovanPlaces.All)
                Mark(pixels, width, height, area.xMin, area.yMin, place.Km,
                    place.Kind != PlaceKind.Dungeon);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            // Imported as a Sprite, or the menu cannot show it, a 3D project imports a new texture as
            // a Texture by default and an Image component will not take one
            if (AssetImporter.GetAtPath(OutputPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }

            Debug.Log($"World map written to {OutputPath} - {width} x {height} px at {PixelsPerKm} px/km, " +
                      $"covering {area.width:0} x {area.height:0} km, sampled in {clock.Elapsed.TotalSeconds:0.0} s.");
        }

        static Color32 Shade(WorldSurface surface, Vector2 km)
        {
            SurfaceSample sample = surface.Sample(km);

            if (sample.Submerged)
            {
                float depth = Mathf.InverseLerp(0f, -40f, sample.HeightMetres);
                return Color.Lerp(new Color32(0xBC, 0xCD, 0xD1, 0xFF), new Color32(0x4C, 0x77, 0x89, 0xFF), depth);
            }

            Color colour = WorldBiomePalette.Of(sample.Biome);

            // Standing water sits above ground rather than below sea level, so testing Submerged
            // alone drew every lake in the world as dry land
            float lake = surface.WaterMetres(km);

            if (lake > sample.HeightMetres)
            {
                float depth = Mathf.InverseLerp(0f, 12f, lake - sample.HeightMetres);
                colour = Color.Lerp(new Color32(0x9D, 0xB6, 0xBE, 0xFF), new Color32(0x5E, 0x86, 0x97, 0xFF), depth);
            }
            else if (sample.RiverKm < RiverKm)
                colour = Color.Lerp(new Color(0.435f, 0.58f, 0.65f), colour, sample.RiverKm / RiverKm);

            if (sample.RoadKm < RoadKm)
                colour = Color.Lerp(new Color(0.42f, 0.33f, 0.24f), colour, sample.RoadKm / RoadKm);

            // Relief shading off the real gradient, lit from the north-west the way a survey sheet
            // is, and read over a kilometre so it shows landforms rather than noise
            float here = surface.Height(km);
            float east = surface.Height(km + new Vector2(ReliefStepKm, 0f)) - here;
            float north = surface.Height(km + new Vector2(0f, ReliefStepKm)) - here;

            float slope = (-east - north) / (ReliefStepKm * 1000f);
            float light = Mathf.Clamp(1f + slope * 2.4f, 0.55f, 1.45f);

            return new Color(colour.r * light, colour.g * light, colour.b * light, 1f);
        }

        // Settlements are circles and dungeons diamonds, the same convention the Survey sheet uses
        static void Mark(Color32[] pixels, int width, int height, float minX, float minY,
            Vector2 km, bool settlement)
        {
            int cx = Mathf.RoundToInt((km.x - minX) * PixelsPerKm);
            int cy = Mathf.RoundToInt((km.y - minY) * PixelsPerKm);
            const int radius = 5;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;

                float reach = settlement ? Mathf.Sqrt(dx * dx + dy * dy) : Mathf.Abs(dx) + Mathf.Abs(dy);

                if (reach > radius)
                    continue;

                pixels[y * width + x] = reach > radius - 2f
                    ? new Color32(0x16, 0x19, 0x1C, 0xFF)
                    : new Color32(0xF2, 0xF3, 0xEE, 0xFF);
            }
        }

    }
}
