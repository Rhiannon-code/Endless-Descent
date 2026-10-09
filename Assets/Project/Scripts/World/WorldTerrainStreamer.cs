using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class WorldTerrainStreamer : MonoBehaviour
    {
        [SerializeField] Transform viewer;
        [SerializeField] int seed = 20260907;
        [SerializeField, Min(1)] int ringRadius = 3;
        [SerializeField] int heightmapResolution = 129;
        [SerializeField] int alphamapResolution = 64;
        [SerializeField] Material terrainMaterial;
        [SerializeField] WorldWater water;
        [SerializeField] GameObject[] plantPrefabs;
        [SerializeField] float treeDistance = 450f;
        [SerializeField] float treeBillboardDistance = 260f;
        [SerializeField, Min(1)] int maxConcurrentBuilds = 6;
        [SerializeField, Min(1f)] float applyBudgetMs = 4f;
        [SerializeField, Min(1f)] float darkApplyBudgetMs = 30f;
        [SerializeField] bool dropViewerOnFirstTile = true;

        [Header("Terrain rendering")]
        [SerializeField] float heightmapPixelError = 12f;
        [SerializeField] float basemapDistance = 220f;
        [SerializeField] bool drawInstanced = true;

        [Header("Distant ground")]
        [SerializeField] float farTileKm = 12f;
        [SerializeField] int farResolution = 65;
        [SerializeField, Min(0)] int farRingRadius = 3;
        [SerializeField, Min(1)] int farPerFrame = 2;
        [SerializeField, Min(1)] int maxFarBuilds = 2;
        [SerializeField, Min(2)] int farAlphaResolution = 32;
        [SerializeField, Min(0f)] float farDropMetres = 25f;

        const int GroupingId = 4711;
        const int FarGroupingId = 4713;
        const float PrepareTimeoutSeconds = 30f;

        readonly Dictionary<Vector2Int, Terrain> live = new Dictionary<Vector2Int, Terrain>();
        readonly HashSet<Vector2Int> pending = new HashSet<Vector2Int>();
        readonly ConcurrentQueue<(Vector2Int key, WorldTileData tile, WorldWaterData water)> finished =
            new ConcurrentQueue<(Vector2Int, WorldTileData, WorldWaterData)>();
        readonly List<Vector2Int> wanted = new List<Vector2Int>();
        readonly List<Vector2Int> stale = new List<Vector2Int>();
        readonly HashSet<Vector2Int> ahead = new HashSet<Vector2Int>();
        readonly Dictionary<Vector2Int, Terrain> far = new Dictionary<Vector2Int, Terrain>();
        readonly List<Vector2Int> farWanted = new List<Vector2Int>();
        readonly HashSet<Vector2Int> farPending = new HashSet<Vector2Int>();
        readonly ConcurrentQueue<(Vector2Int key, float[,] heights, float[,,] alphas)> farFinished =
            new ConcurrentQueue<(Vector2Int, float[,], float[,,])>();

        WorldSurface surface;
        TerrainLayer[] layers;
        TreePrototype[] prototypes;
        Vector2Int centre;
        Vector2Int farHome = new Vector2Int(int.MinValue, int.MinValue);
        Vector2Int leadTile;
        Vector3 anchor;
        Vector3? focus;
        Vector3? lead;
        bool dropped;
        bool connectivityDirty;
        bool reported;
        bool begun;

        // Anyone who needs the ground gets the ground, waiting for it if the build has not finished.
        // Handing out null instead was worse than the stall it avoided, a settlement whose StandOn
        // was skipped is built at height zero, and the player arrives underneath the terrain
        public WorldSurface Surface
        {
            get
            {
                if (surface == null && building != null)
                    surface = building.Result;

                return surface;
            }
        }
        public int Seed => seed;
        public int Resident => live.Count;
        public int FarResident => far.Count;
        public int Building => pending.Count;
        public int Backlog => wanted.Count;

        // Tiles handed to Unity in the last second. The only honest measure of whether the ground can
        // keep up with whoever is walking over it
        public float TilesPerSecond { get; private set; }

        readonly Queue<float> applied = new Queue<float>();

        Task<WorldSurface> building;

        void Awake()
        {
            // Building the world is a second and a quarter of noise, drainage and routing, and none
            // of it touches Unity, so it does not belong on the frame that starts the game. The
            // layers and prototypes below do need the main thread, and stay on it
            building = Task.Run(() => new WorldSurface(WorldGeography.Ordovan(), seed));

            layers = WorldBiomePalette.Layers();
            prototypes = Prototypes();

            if (viewer != null && GetComponent<FloatingOrigin>() == null)
                gameObject.AddComponent<FloatingOrigin>().Bind(viewer);
        }

        void OnEnable()
        {
            CurrentSpace.Changed += OnSpaceChanged;
            WorldOrigin.Shifted += OnShifted;
        }

        void OnDisable()
        {
            CurrentSpace.Changed -= OnSpaceChanged;
            WorldOrigin.Shifted -= OnShifted;
        }

        // FloatingOrigin moves this container with every other scene root, which leaves each tile's
        // local position holding its raw world coordinate, hundreds of kilometres, and undoes the
        // precision the shift was performed for. Putting the container back on the origin and paying
        // the move into the tiles keeps both small
        void OnShifted(Vector3 by)
        {
            Vector3 back = transform.position;

            if (back == Vector3.zero)
                return;

            transform.position = Vector3.zero;

            // The tiles this owns, not every child, water surfaces and anything else parented here
            // are not laid out in tile coordinates and must not be dragged along
            foreach (Terrain tile in live.Values)
                if (tile != null) tile.transform.localPosition += back;

            foreach (Terrain tile in far.Values)
                if (tile != null) tile.transform.localPosition += back;
        }

        // Whether the ground is there without waiting for it. Asking Surface always succeeds; this is
        // for callers that would rather do something else this frame than block
        public bool Ready => surface != null || (building != null && building.IsCompleted);

        // One prototype per plant kind, in WorldVegetation.Plant order, because a scattered instance
        // names its kind by index and nothing re-maps it afterwards
        TreePrototype[] Prototypes()
        {
            if (plantPrefabs == null || plantPrefabs.Length < WorldVegetation.PlantCount)
            {
                Debug.LogError($"{nameof(WorldTerrainStreamer)} has {plantPrefabs?.Length ?? 0} plant " +
                               $"prefabs and needs {WorldVegetation.PlantCount}. Nothing will grow. " +
                               "Rebuild the scene, or fill Plant Prefabs on this component.", this);
                return null;
            }

            TreePrototype[] built = new TreePrototype[WorldVegetation.PlantCount];

            for (int i = 0; i < built.Length; i++)
            {
                if (plantPrefabs[i] == null)
                {
                    Debug.LogError($"Plant prefab slot {i} ({(WorldVegetation.Plant)i}) is empty, so no " +
                                   "plant of any kind is placed. Rebuild the greybox content and the scene.", this);
                    return null;
                }

                if (plantPrefabs[i].GetComponentInChildren<MeshRenderer>() == null)
                    Debug.LogError($"Plant prefab {plantPrefabs[i].name} has no MeshRenderer anywhere, " +
                                   "so it cannot draw.", this);

                built[i] = new TreePrototype { prefab = plantPrefabs[i], bendFactor = 0.3f };
            }

            return built;
        }

        // Where the ring is built around, in world metres. Indoors it stays where the player left the
        // world, so the ground they walk back out onto is still there
        Vector3 Anchor()
        {
            if (focus.HasValue)
                return focus.Value;

            if (CurrentSpace.Outdoors && viewer != null)
                anchor = WorldOrigin.ToWorld(viewer.position);

            return anchor;
        }

        // The ground around somewhere the player is about to be put, built while a transition has the
        // screen dark. The ring stays on that spot until Release, which the loader calls once the
        // player is standing there
        public IEnumerator Prepare(Vector3 worldMetres)
        {
            focus = worldMetres;
            Vector2Int target = WorldTiles.TileAt(worldMetres);
            float giveUp = Time.unscaledTime + PrepareTimeoutSeconds;

            while (!Surrounded(target) && Time.unscaledTime < giveUp)
                yield return Steps.WaitFrame;

            if (!Surrounded(target))
                Debug.LogError($"The ground around tile {target} was not ready after {PrepareTimeoutSeconds} s; " +
                               "arriving anyway.", this);
        }

        public void Release() => focus = null;

        // While a journey is under way, where it will be in a few seconds
        public void LeadTowards(Vector3? worldMetres) => lead = worldMetres;

        bool Surrounded(Vector2Int tile)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (!live.ContainsKey(new Vector2Int(tile.x + dx, tile.y + dy))) return false;

            return true;
        }

        void Update()
        {
            if (viewer == null)
                return;

            if (!begun)
            {
                if (!Ready)
                    return;

                // Read on the main thread on purpose, a fault in the build rethrows here, where it
                // is a visible error, rather than being swallowed on a worker
                _ = Surface;
                begun = true;
                centre = WorldTiles.TileAt(Anchor());
                Refresh();
                FarRing();
            }

            Vector2Int at = WorldTiles.TileAt(Anchor());
            Vector2Int leading = lead.HasValue ? WorldTiles.TileAt(lead.Value) : at;

            if (at != centre || leading != leadTile)
            {
                centre = at;
                leadTile = leading;
                Refresh();
                FarRing();
            }

            // With the screen dark there is no frame rate to protect, only the wait
            float budget = Transition.Busy ? darkApplyBudgetMs : applyBudgetMs;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

            while (clock.Elapsed.TotalMilliseconds < budget
                   && finished.TryDequeue(out (Vector2Int key, WorldTileData tile, WorldWaterData water) done))
            {
                pending.Remove(done.key);

                if (done.tile != null && Wanted(done.key))
                {
                    Apply(done.tile, done.water);
                    applied.Enqueue(Time.unscaledTime);
                }
            }

            while (applied.Count > 0 && Time.unscaledTime - applied.Peek() > 1f)
                applied.Dequeue();

            TilesPerSecond = applied.Count;

            Schedule();
            FarWork();
            Drop();

            if (connectivityDirty)
            {
                Terrain.SetConnectivityDirty();
                connectivityDirty = false;
            }
        }

        // A TerrainData made with new is an asset of its own, not part of the GameObject, and outlives
        // it until assets are unloaded, which in one streamed scene is never. Heights, splat maps,
        // thousands of tree instances and their textures, one set for every tile ever passed
        static void Discard(Terrain terrain)
        {
            if (terrain == null)
                return;

            Destroy(terrain.terrainData);
            Destroy(terrain.gameObject);
        }

        // Coarse ground out to the horizon. Without it the three great ranges are 129 km from
        // Ashmere and the eight smaller ones 46 km, so however far the camera can see there is
        // nothing there to see. No trees, no colliders, and 125 m between samples
        void FarRing()
        {
            if (surface == null || farRingRadius <= 0)
                return;

            Vector3 world = Anchor();
            float span = farTileKm * WorldSurface.MetresPerKm;
            Vector2Int home = new Vector2Int(Mathf.FloorToInt(world.x / span), Mathf.FloorToInt(world.z / span));

            if (home == farHome)
                return;

            farHome = home;

            List<Vector2Int> gone = new List<Vector2Int>();

            foreach (KeyValuePair<Vector2Int, Terrain> pair in far)
                if (!FarWanted(pair.Key)) gone.Add(pair.Key);

            foreach (Vector2Int key in gone)
            {
                Discard(far[key]);
                far.Remove(key);
            }

            farWanted.Clear();

            for (int x = home.x - farRingRadius; x <= home.x + farRingRadius; x++)
            for (int y = home.y - farRingRadius; y <= home.y + farRingRadius; y++)
            {
                Vector2Int key = new Vector2Int(x, y);

                if (!far.ContainsKey(key) && !farPending.Contains(key))
                    farWanted.Add(key);
            }

            // Nearest first, the horizon can wait a frame, the middle distance cannot
            farWanted.Sort((a, b) =>
                (Mathf.Abs(a.x - home.x) + Mathf.Abs(a.y - home.y))
                .CompareTo(Mathf.Abs(b.x - home.x) + Mathf.Abs(b.y - home.y)));
        }

        bool FarWanted(Vector2Int key) =>
            Mathf.Abs(key.x - farHome.x) <= farRingRadius && Mathf.Abs(key.y - farHome.y) <= farRingRadius;

        // Sampled on workers and handed to Unity a couple a frame. Sampled on the main thread, two a
        // frame were most of a frame each time the ring moved on, which is the hitch auto run had
        void FarWork()
        {
            while (farPending.Count < maxFarBuilds && farWanted.Count > 0)
            {
                Vector2Int key = farWanted[0];
                farWanted.RemoveAt(0);

                if (far.ContainsKey(key) || farPending.Contains(key))
                    continue;

                farPending.Add(key);

                WorldSurface shared = surface;
                int resolution = farResolution;
                float tileKm = farTileKm;
                int alpha = farAlphaResolution;

                Task.Run(() =>
                {
                    try
                    {
                        farFinished.Enqueue((key, FarHeights(shared, key, resolution, tileKm),
                            FarAlphas(shared, key, alpha, tileKm)));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        farFinished.Enqueue((key, null, null));
                    }
                });
            }

            for (int i = 0; i < farPerFrame
                 && farFinished.TryDequeue(out (Vector2Int key, float[,] heights, float[,,] alphas) done); i++)
            {
                farPending.Remove(done.key);

                if (done.heights != null && FarWanted(done.key) && !far.ContainsKey(done.key))
                    far[done.key] = BuildFar(done.key, done.heights, done.alphas);
            }
        }

        static float[,] FarHeights(WorldSurface shared, Vector2Int key, int resolution, float tileKm)
        {
            float[,] heights = new float[resolution, resolution];

            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                heights[y, x] = WorldTiles.Normalised(
                    shared.Height(WorldTiles.SampleKm(key.x, key.y, x, y, resolution, tileKm)));

            return heights;
        }

        // Without this the far ring had no alphamap at all, and a TerrainData that is given layers
        // and never told their weights puts layer 0 everywhere. Layer 0 is BiomeId.Sea, so every far
        // terrain in the world was painted solid sea colour, which is the water that kept turning up
        // on dry land, forty kilometres from any
        static float[,,] FarAlphas(WorldSurface shared, Vector2Int key, int resolution, float tileKm)
        {
            float[,,] alphas = new float[resolution, resolution, WorldTileData.BiomeCount];
            float step = tileKm / resolution;

            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 km = new Vector2(key.x * tileKm + (x + 0.5f) * step,
                                         key.y * tileKm + (y + 0.5f) * step);
                alphas[y, x, (int)shared.Biome(km)] = 1f;
            }

            return alphas;
        }

        Terrain BuildFar(Vector2Int key, float[,] heights, float[,,] alphas)
        {
            TerrainData data = new TerrainData
            {
                heightmapResolution = farResolution,
                alphamapResolution = farAlphaResolution
            };
            data.size = new Vector3(farTileKm * WorldSurface.MetresPerKm,
                WorldTiles.CeilingMetres - WorldTiles.FloorMetres,
                farTileKm * WorldSurface.MetresPerKm);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = layers;

            if (alphas != null)
                data.SetAlphamaps(0, 0, alphas);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = $"Far {key.x} {key.y}";
            go.transform.SetParent(transform, false);
            go.transform.position = WorldOrigin.ToUnity(new Vector3(
                key.x * farTileKm * WorldSurface.MetresPerKm,
                WorldTiles.FloorMetres - farDropMetres,
                key.y * farTileKm * WorldSurface.MetresPerKm));

            // Scenery only. The near ring is what you stand on, and two colliders in the same place
            // fight each other
            TerrainCollider collider = go.GetComponent<TerrainCollider>();
            if (collider != null) collider.enabled = false;

            Terrain terrain = go.GetComponent<Terrain>();
            terrain.groupingID = FarGroupingId;
            terrain.allowAutoConnect = true;
            terrain.heightmapPixelError = 40f;
            terrain.basemapDistance = 0f;
            terrain.drawInstanced = drawInstanced;

            // Scenery eight to forty kilometres out. No shadow cascade reaches that far, so every
            // one of these in the shadow pass was work for a shadow nobody can be standing in
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Show(terrain, CurrentSpace.Outdoors, false);

            return terrain;
        }

        // Is the ground under this spot actually built? Travel outruns the streamer, and a journey
        // that keeps going regardless spends its time over tiles that have not arrived
        public bool HasGroundAt(Vector3 position) =>
            live.ContainsKey(WorldTiles.TileAt(WorldOrigin.ToWorld(position)));

        bool Wanted(Vector2Int tile) =>
            (Mathf.Abs(tile.x - centre.x) <= ringRadius && Mathf.Abs(tile.y - centre.y) <= ringRadius) ||
            ahead.Contains(tile);

        void Refresh()
        {
            ahead.Clear();

            // The tiles a journey is about to cross, and one either side, so auto run meets ground it
            // asked for seconds ago rather than ground only now being sampled
            if (lead.HasValue)
            {
                Vector2 from = centre, to = leadTile;
                int samples = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);

                for (int s = 0; s <= samples; s++)
                {
                    Vector2 p = Vector2.Lerp(from, to, samples == 0 ? 0f : s / (float)samples);
                    Vector2Int tile = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));

                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        ahead.Add(new Vector2Int(tile.x + dx, tile.y + dy));
                }
            }

            stale.Clear();

            foreach (KeyValuePair<Vector2Int, Terrain> pair in live)
                if (!Wanted(pair.Key)) stale.Add(pair.Key);

            foreach (Vector2Int tile in stale)
            {
                Discard(live[tile]);

                if (water != null)
                    water.Drop(tile);

                live.Remove(tile);
                connectivityDirty = true;
            }

            wanted.Clear();
            HashSet<Vector2Int> listed = new HashSet<Vector2Int>();

            void Want(Vector2Int tile)
            {
                if (!live.ContainsKey(tile) && !pending.Contains(tile) && listed.Add(tile))
                    wanted.Add(tile);
            }

            for (int x = centre.x - ringRadius; x <= centre.x + ringRadius; x++)
            for (int y = centre.y - ringRadius; y <= centre.y + ringRadius; y++)
                Want(new Vector2Int(x, y));

            foreach (Vector2Int tile in ahead)
                Want(tile);

            // Nearest first, and ahead before behind while travelling
            Vector2 heading = lead.HasValue && leadTile != centre ? ((Vector2)(leadTile - centre)).normalized : Vector2.zero;
            wanted.Sort((a, b) => Priority(a, heading).CompareTo(Priority(b, heading)));
        }

        float Priority(Vector2Int tile, Vector2 heading)
        {
            Vector2 offset = tile - centre;
            return offset.magnitude - 0.6f * Mathf.Max(0f, Vector2.Dot(offset, heading));
        }

        void Schedule()
        {
            while (pending.Count < maxConcurrentBuilds && wanted.Count > 0)
            {
                Vector2Int tile = wanted[0];
                wanted.RemoveAt(0);

                if (live.ContainsKey(tile) || pending.Contains(tile))
                    continue;

                pending.Add(tile);

                int resolution = heightmapResolution, alpha = alphamapResolution, scatter = seed;
                int wet = water != null ? water.Resolution : 0;
                WorldSurface shared = surface;

                Task.Run(() =>
                {
                    try
                    {
                        finished.Enqueue((tile,
                            WorldTileData.Build(shared, tile.x, tile.y, resolution, alpha, scatter),
                            wet > 0 ? WorldWaterData.Build(shared, tile.x, tile.y, wet) : null));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        finished.Enqueue((tile, null, null));
                    }
                });
            }
        }

        void Apply(WorldTileData tile, WorldWaterData wet)
        {
            Vector2Int key = new Vector2Int(tile.TileX, tile.TileY);

            if (live.ContainsKey(key))
                return;

            TerrainData data = new TerrainData
            {
                heightmapResolution = heightmapResolution,
                alphamapResolution = alphamapResolution
            };

            // Resolution first, then size. Unity's docs do not say whether changing the resolution
            // disturbs size, and this order is safe whichever way that goes
            data.size = new Vector3(WorldTiles.TileKm * WorldSurface.MetresPerKm,
                WorldTiles.CeilingMetres - WorldTiles.FloorMetres,
                WorldTiles.TileKm * WorldSurface.MetresPerKm);

            data.SetHeights(0, 0, tile.Heights);
            data.terrainLayers = layers;
            data.SetAlphamaps(0, 0, tile.Alphas);

            if (prototypes != null && tile.Trees.Length > 0)
            {
                data.treePrototypes = prototypes;
                data.SetTreeInstances(tile.Trees, true);

                // Said once. "No flora at all" and "flora that will not draw" look identical on
                // screen and have completely different causes
                if (!reported)
                {
                    reported = true;
                    Debug.Log($"First tile placed {tile.Trees.Length} plants from " +
                              $"{prototypes.Length} prototypes, drawn out to {treeDistance} m. " +
                              $"Terrain reports {data.treeInstanceCount} instances.");
                }
            }
            else if (!reported)
            {
                reported = true;
                Debug.LogWarning($"First tile placed no plants: prototypes " +
                                 $"{(prototypes == null ? "MISSING" : prototypes.Length.ToString())}, " +
                                 $"scattered {tile.Trees.Length}.");
            }

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = $"Tile {tile.TileX} {tile.TileY}";
            go.transform.SetParent(transform, false);
            go.transform.position = WorldOrigin.ToUnity(WorldTiles.OriginMetres(tile.TileX, tile.TileY)
                + Vector3.up * WorldTiles.FloorMetres);

            Terrain terrain = go.GetComponent<Terrain>();
            terrain.groupingID = GroupingId;
            terrain.allowAutoConnect = true;
            terrain.heightmapPixelError = heightmapPixelError;
            terrain.basemapDistance = basemapDistance;
            terrain.drawInstanced = drawInstanced;
            terrain.treeDistance = treeDistance;
            terrain.treeBillboardDistance = treeBillboardDistance;

            if (terrainMaterial != null)
                terrain.materialTemplate = terrainMaterial;

            if (water != null && wet != null)
                water.Build(wet, go.transform);

            Show(terrain, CurrentSpace.Outdoors, true);

            live[key] = terrain;
            connectivityDirty = true;
        }

        void OnSpaceChanged(SpaceKind kind)
        {
            bool shown = kind == SpaceKind.Overworld;

            foreach (Terrain terrain in live.Values) Show(terrain, shown, true);
            foreach (Terrain terrain in far.Values) Show(terrain, shown, false);
        }

        static void Show(Terrain terrain, bool shown, bool near)
        {
            if (terrain == null)
                return;

            terrain.drawHeightmap = shown;
            terrain.drawTreesAndFoliage = shown && near;
        }

        // For a scene with nobody to put the player anywhere, once the tile under them exists, stand
        // them on it
        void Drop()
        {
            if (!dropViewerOnFirstTile || dropped || viewer == null || !HasGroundAt(viewer.position))
                return;

            Vector3 at = viewer.position;
            float ground = surface.Height(WorldOrigin.KmAt(at));

            CharacterController controller = viewer.GetComponent<CharacterController>();

            if (controller != null)
                controller.enabled = false;

            viewer.position = new Vector3(at.x, ground + 2f, at.z);

            if (controller != null)
                controller.enabled = true;

            dropped = true;
        }
    }
}
