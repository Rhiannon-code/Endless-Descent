using System.IO;
using System.Threading.Tasks;
using EndlessDescent.Data;
using EndlessDescent.World;
using UnityEditor;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    // The world as scene objects rather than as a function. The streamer makes terrain at play time
    // and throws it away, this writes it down so it can be looked at, walked around in the scene
    // view, and edited by hand
    // It cannot write the whole world at playing detail. Meregard alone is 140 x 124 km, which is
    // 17,360 one kilometre tiles, the empire is about 179,000. So there are two bakes, a coarse one
    // covering everything, for orientation and broad edits, and a fine one covering a few kilometres
    // around a place, for the ground you actually work on
    public static class WorldBaker
    {
        const string TerrainRoot = "Assets/Project/Content/Generated/Terrain";
        const int Seed = 20260907;

        const float OverviewTileKm = 16f;
        const int OverviewResolution = 129;
        const int DetailResolution = 257;
        const float DetailRadiusKm = 6f;

        [MenuItem("Endless Descent/Bake World/Overview (whole empire, coarse)")]
        public static void BakeOverview()
        {
            WorldGeography geography = WorldGeography.Ordovan();
            Rect area = geography.Extent();

            int columns = Mathf.CeilToInt(area.width / OverviewTileKm);
            int rows = Mathf.CeilToInt(area.height / OverviewTileKm);

            if (!EditorUtility.DisplayDialog("Bake world overview",
                    $"Writes {columns * rows} coarse terrains ({OverviewTileKm:0} km each, " +
                    $"{OverviewTileKm * 1000f / (OverviewResolution - 1):0} m between samples) covering " +
                    $"{area.width:0} x {area.height:0} km.\n\n" +
                    $"Terrain assets go under {TerrainRoot}/Overview and any previous overview there is " +
                    "deleted. Nothing else in the project is touched.\n\n" +
                    "This is for looking at and for broad edits. It is far too coarse to walk on.",
                    "Bake", "Cancel"))
                return;

            EditorApplication.delayCall += () => BakeOverviewInto(geography, new WorldSurface(geography, Seed));
        }

        [MenuItem("Endless Descent/Bake World/Detail block around Ashmere")]
        public static void BakeDetail()
        {
            WorldGeography geography = WorldGeography.Ordovan();
            Vector2 centre = OrdovanPlaces.KmOf("Ashmere");

            int span = Mathf.CeilToInt(DetailRadiusKm * 2f / WorldTiles.TileKm);
            float originX = centre.x - DetailRadiusKm;
            float originY = centre.y - DetailRadiusKm;

            if (!EditorUtility.DisplayDialog("Bake detail block",
                    $"Writes {span * span} terrains at playing detail ({WorldTiles.TileKm:0} km each, " +
                    $"{WorldTiles.TileKm * 1000f / (DetailResolution - 1):0.0} m between samples), " +
                    $"covering {DetailRadiusKm * 2f:0} x {DetailRadiusKm * 2f:0} km around Ashmere.\n\n" +
                    $"Terrain assets go under {TerrainRoot}/Detail and any previous detail block there is " +
                    "deleted. Nothing else in the project is touched.",
                    "Bake", "Cancel"))
                return;

            EditorApplication.delayCall += () => Bake("Detail", originX, originY, span, span,
                WorldTiles.TileKm, DetailResolution, geography, new WorldSurface(geography, Seed));
        }

        [MenuItem("Endless Descent/Bake World/Place markers")]
        public static void BakeMarkers()
        {
            EditorApplication.delayCall += () =>
            {
                Markers(new WorldSurface(WorldGeography.Ordovan(), Seed));

                Debug.Log($"Placed {OrdovanPlaces.All.Length} world place markers under 'World Places'. " +
                          "Their heights come from the same surface the terrain does.");
            };
        }

        internal static GameObject Markers(WorldSurface surface)
        {
            GameObject old = GameObject.Find("World Places");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new GameObject("World Places");

            foreach (OrdovanPlaces.Place place in OrdovanPlaces.All)
            {
                GameObject marker = new GameObject($"{place.Province} - {place.Name} ({place.Kind})");
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = new Vector3(
                    place.Km.x * WorldSurface.MetresPerKm,
                    surface.Height(place.Km),
                    place.Km.y * WorldSurface.MetresPerKm);
            }

            return root;
        }

        internal static GameObject BakeOverviewInto(WorldGeography geography, WorldSurface surface)
        {
            Rect area = geography.Extent();

            GameObject root = Bake("Overview", area.xMin, area.yMin,
                Mathf.CeilToInt(area.width / OverviewTileKm),
                Mathf.CeilToInt(area.height / OverviewTileKm),
                OverviewTileKm, OverviewResolution, geography, surface);

            // On the bake itself, not added by whoever called it, a menu re-bake used to produce a
            // root without it, and that terrain stays live at play inside the streamed ground
            root.AddComponent<WorldOverviewPreview>();
            return root;
        }

        static GameObject Bake(string label, float originX, float originY, int columns, int rows,
            float tileKm, int resolution, WorldGeography geography, WorldSurface surface)
        {
            string folder = $"{TerrainRoot}/{label}";

            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            TerrainLayer[] layers = WorldBiomePalette.Layers();

            GameObject old = GameObject.Find($"World Bake - {label}");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new GameObject($"World Bake - {label}");
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            int made = 0;

            try
            {
                for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < columns; tx++)
                {
                    float x0 = originX + tx * tileKm;
                    float y0 = originY + ty * tileKm;

                    if (EditorUtility.DisplayCancelableProgressBar($"Baking {label}",
                            $"tile {made + 1} of {columns * rows}", made / (float)(columns * rows)))
                        break;

                    float[,] heights = new float[resolution, resolution];
                    float step = tileKm / (resolution - 1);

                    // Rows in parallel, Sample is pure and thread safe, and a coarse bake is
                    // thousands of tiles
                    Parallel.For(0, resolution, y =>
                    {
                        for (int x = 0; x < resolution; x++)
                            heights[y, x] = WorldTiles.Normalised(
                                surface.Height(new Vector2(x0 + x * step, y0 + y * step)));
                    });

                    TerrainData data = new TerrainData { heightmapResolution = resolution };
                    data.size = new Vector3(tileKm * WorldSurface.MetresPerKm,
                        WorldTiles.CeilingMetres - WorldTiles.FloorMetres,
                        tileKm * WorldSurface.MetresPerKm);
                    data.SetHeights(0, 0, heights);
                    data.terrainLayers = layers;

                    AssetDatabase.CreateAsset(data, $"{folder}/{label}_{tx}_{ty}.asset");

                    GameObject go = Terrain.CreateTerrainGameObject(data);
                    go.name = $"{label} {tx} {ty}";
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(x0 * WorldSurface.MetresPerKm,
                        WorldTiles.FloorMetres, y0 * WorldSurface.MetresPerKm);

                    Terrain terrain = go.GetComponent<Terrain>();
                    terrain.groupingID = label == "Detail" ? 4711 : 4712;
                    terrain.allowAutoConnect = true;

                    made++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Terrain.SetConnectivityDirty();

            clock.Stop();

            int written = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.asset").Length : 0;

            Debug.Log($"{label} bake: {made} tiles built, {written} terrain assets on disk under {folder}, " +
                      $"in {clock.Elapsed.TotalSeconds:0.0} s. Scene root is 'World Bake - {label}'.");

            return root;
        }
    }
}
