using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class WorldWater : MonoBehaviour
    {
        [SerializeField] Material riverMaterial;
        [SerializeField, Min(8)] int resolution = 64;
        [SerializeField] bool drawSea = true;

        [Header("Swell")]
        [SerializeField, Min(25f)] float swellPatchMetres = 150f;
        [SerializeField, Range(0f, 30f)] float swellWindMetresPerSecond = 4f;
        [SerializeField, Range(0f, 1f)] float swellChaos = 0.6f;
        [SerializeField, Range(0f, 30f)] float rippleWindMetresPerSecond = 3f;

        readonly Dictionary<Vector2Int, List<GameObject>> built = new Dictionary<Vector2Int, List<GameObject>>();
        readonly Dictionary<int, WaterSurface> lakes = new Dictionary<int, WaterSurface>();
        readonly List<int> empty = new List<int>();

        WaterSurface sea;

        public int Resolution => resolution;
        public int Lakes => lakes.Count;
        public int Tiles => built.Count;

        // The invariant that matters, a lake with no meshes is a lake HDRP draws as a plane. This
        // should always read zero, and if it ever does not, the world is about to flood
        public int Starved
        {
            get
            {
                int count = 0;

                foreach (WaterSurface lake in lakes.Values)
                    if (lake == null || lake.meshRenderers.Count == 0) count++;

                return count;
            }
        }

        void OnEnable() => CurrentSpace.Changed += OnSpaceChanged;
        void OnDisable() => CurrentSpace.Changed -= OnSpaceChanged;

        // Underground and indoors there is no sea to see, and an infinite surface is not something to
        // leave running behind a dungeon wall
        void OnSpaceChanged(SpaceKind kind)
        {
            bool shown = kind == SpaceKind.Overworld;

            if (sea != null) sea.enabled = shown;

            foreach (WaterSurface lake in lakes.Values)
                if (lake != null) lake.enabled = shown && lake.meshRenderers.Count > 0;

            foreach (List<GameObject> pieces in built.Values)
            foreach (GameObject piece in pieces)
                if (piece != null) piece.SetActive(shown);
        }

        void Sea()
        {
            if (sea != null || !drawSea)
                return;

            GameObject go = new GameObject("Sea");
            go.transform.SetParent(transform, false);

            // Sea level is zero in world metres and the floating origin never moves anything
            // vertically, so this is the one water height that never needs shifting
            go.transform.position = Vector3.zero;

            sea = go.AddComponent<WaterSurface>();
            sea.surfaceType = WaterSurfaceType.OceanSeaLake;
            sea.geometryType = WaterGeometryType.Infinite;
            Calm(sea);
            sea.enabled = CurrentSpace.Outdoors;
        }

        WaterSurface Lake(int level)
        {
            if (lakes.TryGetValue(level, out WaterSurface found) && found != null)
                return found;

            GameObject go = new GameObject($"Lake {level}");
            go.transform.SetParent(transform, false);

            WaterSurface surface = go.AddComponent<WaterSurface>();
            surface.surfaceType = WaterSurfaceType.OceanSeaLake;
            surface.geometryType = WaterGeometryType.Custom;
            Calm(surface);
            surface.enabled = CurrentSpace.Outdoors;

            lakes[level] = surface;
            return surface;
        }

        // A lake standing 15 m up a valley has the same problem the sea has: its waves are measured
        // against its own level, and anything they reach is flooded. Both get the same calm water
        void Calm(WaterSurface surface)
        {
            surface.repetitionSize = swellPatchMetres;
            surface.largeWindSpeed = swellWindMetresPerSecond;
            surface.largeChaos = swellChaos;
            surface.ripplesWindSpeed = rippleWindMetresPerSecond;
        }

        // Called with the tile's ground, on the frame that ground is handed to Unity
        public void Build(WorldWaterData water, Transform tile)
        {
            Sea();

            Vector2Int key = new Vector2Int(water.TileX, water.TileY);

            if (built.ContainsKey(key) || !water.Any)
                return;

            List<GameObject> pieces = new List<GameObject>();

            foreach ((float Level, Vector3[] Vertices, int[] Triangles) still in water.Still)
            {
                WaterSurface surface = Lake(Mathf.RoundToInt(still.Level / 0.25f));

                // The surface carries the height; the mesh only says where the water reaches. HDRP
                // levels custom geometry to the surface it belongs to, which is the whole reason a
                // lake can be a mesh at all
                surface.transform.position = new Vector3(0f, still.Level, 0f);

                GameObject piece = Piece($"Lake {water.TileX} {water.TileY}", tile,
                    still.Vertices, still.Triangles, null);

                // HDRP draws this mesh itself, with the water material, straight from the MeshFilter
                // Left enabled it would also draw normally and put a second surface over the first
                piece.GetComponent<MeshRenderer>().enabled = false;
                surface.meshRenderers.Add(piece.GetComponent<MeshRenderer>());
                pieces.Add(piece);
            }

            if (water.Flowing.Length > 0)
                pieces.Add(Piece($"River {water.TileX} {water.TileY}", tile,
                    water.Flowing, water.FlowingTriangles, riverMaterial));

            built[key] = pieces;
        }

        public void Drop(Vector2Int key)
        {
            if (!built.TryGetValue(key, out List<GameObject> pieces))
                return;

            foreach (GameObject piece in pieces)
            {
                if (piece == null)
                    continue;

                MeshRenderer renderer = piece.GetComponent<MeshRenderer>();

                // Left in the list it is a null entry HDRP walks over every frame, for ever
                foreach (WaterSurface lake in lakes.Values)
                    if (lake != null) lake.meshRenderers.Remove(renderer);

                Destroy(piece.GetComponent<MeshFilter>().sharedMesh);
                Destroy(piece);
            }

            built.Remove(key);
            Prune();
        }

        // Every lake that has just lost its last tile. Destroyed rather than disabled, each surface
        // carries a simulation, and travelling across the region would otherwise leave a trail of
        // them behind
        void Prune()
        {
            empty.Clear();

            foreach (KeyValuePair<int, WaterSurface> lake in lakes)
            {
                if (lake.Value == null)
                {
                    empty.Add(lake.Key);
                    continue;
                }

                // A tile destroyed by any route other than Drop leaves its renderer here as a null,
                // which still counts towards the list being non-empty and would keep a lake alive
                // with nothing to draw
                lake.Value.meshRenderers.RemoveAll(renderer => renderer == null);

                if (lake.Value.meshRenderers.Count == 0)
                    empty.Add(lake.Key);
            }

            foreach (int level in empty)
            {
                if (lakes[level] != null)
                    Destroy(lakes[level].gameObject);

                lakes.Remove(level);
            }
        }

        GameObject Piece(string name, Transform parent, Vector3[] vertices, int[] triangles, Material material)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            Mesh mesh = new Mesh { name = name };

            // A fully wet tile at 64 square is over sixteen thousand vertices, which is fine, but the
            // default index format stops being fine not far above it
            mesh.indexFormat = vertices.Length > 60000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            go.SetActive(CurrentSpace.Outdoors);
            return go;
        }

        void OnDestroy()
        {
            foreach (List<GameObject> pieces in built.Values)
            foreach (GameObject piece in pieces)
                if (piece != null) Destroy(piece.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
