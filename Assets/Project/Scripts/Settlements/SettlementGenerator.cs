using System.Collections;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace EndlessDescent.Settlements
{
    // Turns a plan into ground you can walk. Nothing here searches or decides, streets, plots and
    // storeys are all settled by the time this runs
    [DisallowMultipleComponent]
    public class SettlementGenerator : MonoBehaviour, IMapSource
    {
        [SerializeField] SettlementDefinition definition;
        [SerializeField] int seed = 12345;
        [SerializeField] bool generateOnStart;

        [Header("Greybox materials")]
        [SerializeField] Material groundMaterial;
        [SerializeField] Material streetMaterial;
        [SerializeField] Material squareMaterial;
        [SerializeField] Material wallMaterial;
        [SerializeField] Material roofMaterial;
        [SerializeField] Material waterMaterial;
        [SerializeField] Material doorMaterial;

        [Header("People")]
        [SerializeField] GameObject[] townsfolk = new GameObject[0];
        [SerializeField, Min(0)] int folkPerHundredBuildings = 22;
        [SerializeField, Min(0)] int folkCap = 70;

        [Header("Testing")]
        [SerializeField] bool colourByPurpose = true;

        const float DoorWidth = 2.2f;
        const float DoorHeight = 2.6f;
        const float WallThickness = 0.3f;
        const float TerrainCell = 6f;
        const float RampartHeight = 8f;
        const float InteriorSpacing = 60f;
        const float InteriorDepth = -600f;

        readonly Dictionary<(Vector2Int cell, Material material), BoxMesh> meshes =
            new Dictionary<(Vector2Int, Material), BoxMesh>();
        readonly List<Mesh> owned = new List<Mesh>();

        NpcDefinition[] cast;

        // Building index to the person who works there
        readonly Dictionary<int, NpcDefinition> workplaces = new Dictionary<int, NpcDefinition>();

        SettlementPlan plan;
        GameObject root;
        Transform interiors;
        int interiorSlots;
        bool constructing;

        public SettlementDefinition Definition => definition;
        // Null until the whole town stands, culling indexes renderers the moment a plan appears
        public SettlementPlan Plan => constructing ? null : plan;
        // The plan whether or not the town has finished going up, for whoever is building it
        public SettlementPlan Planned => plan;
        // In world space. The plan lays a settlement out around its own origin, and whoever placed
        // that origin is the only one who knows where it actually is
        public Vector3 Entrance => plan != null ? transform.TransformPoint(plan.Arrival) : transform.position;
        public Quaternion Facing => plan != null ? plan.ArrivalFacing : Quaternion.identity;

        public string PlaceName => definition != null ? definition.DisplayName : "Settlement";
        public int LevelCount => 1;

        void Start()
        {
            if (generateOnStart)
                Generate(seed);
        }

        public void SetDefinition(SettlementDefinition value) => definition = value;

        // Who lives in this particular town, each of them stood at the door of their own trade
        public void SetCast(IReadOnlyList<NpcDefinition> residents)
        {
            cast = residents != null && residents.Count > 0 ? new NpcDefinition[residents.Count] : null;

            if (cast == null)
                return;

            for (int i = 0; i < residents.Count; i++)
                cast[i] = residents[i];
        }

        WorldSurface world;
        Vector2 originKm;
        float originHeight;

        // Stand this settlement on the world at a map coordinate instead of on ground of its own.
        // The streamed terrain becomes its ground, so it stops generating any
        public void StandOn(WorldSurface surface, Vector2 placeKm)
        {
            world = surface;
            originKm = placeKm;
            originHeight = surface.Height(placeKm);
        }

        // Pure, so it can run off the main thread. The same ground relation the build uses, so a plan
        // made here is exactly the town that Generate would lay out
        public static SettlementPlan PlanOn(SettlementGrowth growth, int seed, WorldSurface world, Vector2 placeKm)
        {
            float origin = world.Height(placeKm);
            return SettlementLayout.Generate(growth, seed, (x, z) => Relative(world, placeKm, origin, x, z));
        }

        // Local metres out, heights relative to the place's own height. The origin is sampled once,
        // because laying out a town asks this tens of thousands of times
        static float Relative(WorldSurface world, Vector2 placeKm, float origin, float x, float z) =>
            world.Height(placeKm + new Vector2(x, z) / WorldSurface.MetresPerKm) - origin;

        float WorldGround(float x, float z) => Relative(world, originKm, originHeight, x, z);

        public bool Generate(int newSeed, SettlementPlan planned = null)
        {
            if (!Prepare(newSeed, planned))
                return false;

            IEnumerator steps = Construct();
            while (steps.MoveNext()) { }

            return true;
        }

        // The same build a step at a time, for a town going up while someone walks towards it or
        // while the screen is dark, the caller decides how many steps fit in a frame
        public IEnumerator GenerateInSteps(int newSeed, SettlementPlan planned)
        {
            if (!Prepare(newSeed, planned))
                yield break;

            IEnumerator steps = Construct();

            while (steps.MoveNext())
                yield return null;
        }

        bool Prepare(int newSeed, SettlementPlan planned)
        {
            Clear();
            seed = newSeed;

            if (definition == null)
            {
                Debug.LogError($"{nameof(SettlementGenerator)} has no settlement assigned.", this);
                return false;
            }

            if (world != null)
                transform.position = WorldOrigin.UnityAt(originKm, originHeight);

            plan = planned ?? SettlementLayout.Generate(SettlementGrowth.From(definition), seed,
                world != null ? WorldGround : (System.Func<float, float, float>)null);

            constructing = true;

            root = new GameObject($"Settlement_{definition.DisplayName}_{seed}");
            root.transform.SetParent(transform, false);

            interiors = new GameObject("Interiors").transform;
            interiors.SetParent(root.transform, false);
            interiors.localPosition = new Vector3(0f, InteriorDepth, 0f);
            interiorSlots = 0;
            return true;
        }

        IEnumerator Construct()
        {
            // On the world the terrain is already there, a second ground mesh would only fight it
            // The same is true of the water, WorldWater owns every surface out there, and this box
            // is an opaque slab at a fixed depth that knows nothing about the ground it lands on
            // It stays for the settlement playtest scene, which has no world
            if (world == null)
            {
                BuildTerrain();
                BuildWater();
            }

            foreach (object step in BuildStreets())
                yield return step;

            foreach (object step in BuildWall())
                yield return step;

            DeterministicRandom rng = new DeterministicRandom(seed);

            AssignWorkplaces();

            for (int i = 0; i < plan.Buildings.Count; i++)
            {
                BuildBuilding(plan.Buildings[i], workplaces.ContainsKey(i));
                yield return null;
            }

            foreach (object step in Flush())
                yield return step;

            StaffWorkplaces();
            PopulateStreets(ref rng);
            PlaceHiddenCircle(ref rng);
            constructing = false;
        }

        // On the back wall of an ordinary house, away from the civic quarter and off the main streets,
        // because a circle that can be found by walking down the high street has already been hanged
        void PlaceHiddenCircle(ref DeterministicRandom rng)
        {
            GameObject prefab = definition.HiddenCircle;
            if (prefab == null || plan.Buildings.Count == 0)
                return;

            Building host = plan.Buildings[plan.Buildings.Count - 1];
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Building candidate = plan.Buildings[rng.NextInt(plan.Buildings.Count)];
                if (candidate.Purpose != RoomPurpose.Townhouse)
                    continue;

                host = candidate;
                break;
            }

            Quaternion facing = Quaternion.Euler(0f, host.Yaw + 180f, 0f);
            Vector3 back = facing * new Vector3(0f, 0f, host.Size.y * 0.5f + 0.3f);
            Vector3 at = new Vector3(host.Centre.x, host.Ground, host.Centre.y) + back;

            Instantiate(prefab, root.transform).transform.SetLocalPositionAndRotation(at, facing);
        }

        // Each person gets one building of their trade, or a house when this town has none of it
        void AssignWorkplaces()
        {
            workplaces.Clear();

            if (cast == null)
                return;

            foreach (NpcDefinition person in cast)
            {
                if (person == null)
                    continue;

                int at = Unclaimed(person.Workplace);

                if (at < 0) at = Unclaimed(RoomPurpose.Townhouse);
                if (at < 0) at = Unclaimed(null);
                if (at >= 0) workplaces[at] = person;
            }
        }

        int Unclaimed(RoomPurpose? purpose)
        {
            for (int i = 0; i < plan.Buildings.Count; i++)
                if (!workplaces.ContainsKey(i) && (purpose == null || plan.Buildings[i].Purpose == purpose)) return i;

            return -1;
        }

        // At their own door and a step to the side of it, facing the street, where somebody looking
        // for a smith would look
        void StaffWorkplaces()
        {
            foreach (KeyValuePair<int, NpcDefinition> pair in workplaces)
            {
                Building building = plan.Buildings[pair.Key];
                GameObject prefab = definition.ResidentFor(building.Purpose);

                if (prefab == null && townsfolk != null && townsfolk.Length > 0)
                    prefab = townsfolk[0];

                if (prefab == null)
                    continue;

                Quaternion facing = Quaternion.Euler(0f, building.Yaw, 0f);
                Vector3 offset = facing * new Vector3(DoorWidth, 0f, building.Size.y * 0.5f + 1.2f);
                Vector2 flat = building.Centre + new Vector2(offset.x, offset.z);

                GameObject person = Instantiate(prefab, root.transform);
                person.transform.SetLocalPositionAndRotation(new Vector3(flat.x, plan.Height(flat), flat.y), facing);
                person.GetComponent<NpcActor>()?.Adopt(pair.Value);
            }
        }

        // Capped rather than proportional, a port city of 620 buildings does not want six hundred
        // people each running an Update, and the streets read as busy well before that
        void PopulateStreets(ref DeterministicRandom rng)
        {
            if (townsfolk == null || townsfolk.Length == 0 || plan.Streets.Count == 0)
                return;

            int wanted = Mathf.Min(folkCap, plan.Buildings.Count * folkPerHundredBuildings / 100);

            Transform crowd = new GameObject("Townsfolk").transform;
            crowd.SetParent(root.transform, false);

            for (int i = 0; i < wanted; i++)
            {
                GameObject prefab = townsfolk[rng.NextInt(townsfolk.Length)];
                if (prefab == null)
                    continue;

                Street street = plan.Streets[rng.NextInt(plan.Streets.Count)];
                Vector2 at = Vector2.Lerp(street.A, street.B, rng.NextFloat());

                GameObject person = Instantiate(prefab, crowd);
                person.transform.localPosition = new Vector3(at.x, plan.Height(at), at.y);

                // Every named resident used to be dealt round these, so each existed several times
                // over, walking anywhere. With a cast the crowd is background and the named are at
                // their doors; without one (the settlement playtest) the crowd is all there is
                if (cast != null)
                    person.GetComponent<NpcActor>()?.Unlist();
                person.AddComponent<StreetWanderer>().Bind(this);
            }
        }

        public void Clear()
        {
            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);

            foreach (Mesh mesh in owned)
                Discard(mesh);

            owned.Clear();
            meshes.Clear();

            root = null;
            plan = null;
            constructing = false;
        }

        float Ground(float x, float z) => plan.Height(x, z);

        // Everything a town is made of goes into one mesh per material per block
        void Add(Vector3 centre, Vector3 size, Quaternion rotation, Material material)
        {
            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt(centre.x / SettlementVisibility.CellMetres),
                Mathf.FloorToInt(centre.z / SettlementVisibility.CellMetres));

            if (!meshes.TryGetValue((cell, material), out BoxMesh batch))
                meshes[(cell, material)] = batch = new BoxMesh();

            batch.Add(centre - BlockCentre(cell), size, rotation);
        }

        static Vector3 BlockCentre(Vector2Int cell) => new Vector3(
            (cell.x + 0.5f) * SettlementVisibility.CellMetres, 0f,
            (cell.y + 0.5f) * SettlementVisibility.CellMetres);

        IEnumerable Flush()
        {
            foreach (KeyValuePair<(Vector2Int cell, Material material), BoxMesh> block in meshes)
            {
                GameObject drawn = new GameObject($"Block_{block.Key.cell.x}_{block.Key.cell.y}_{block.Key.material.name}",
                    typeof(MeshFilter), typeof(MeshRenderer));

                drawn.transform.SetParent(root.transform, false);
                drawn.transform.localPosition = BlockCentre(block.Key.cell);

                Mesh mesh = block.Value.ToMesh("Town");
                owned.Add(mesh);

                drawn.GetComponent<MeshFilter>().sharedMesh = mesh;
                drawn.GetComponent<MeshRenderer>().sharedMaterial = block.Key.material;

                yield return null;
            }

            meshes.Clear();
        }

        // Colliders are objects of their own, without renderers, shapes are merged for drawing and kept
        // apart for walking into
        BoxCollider Solid(string name, Vector3 centre, Quaternion rotation)
        {
            GameObject solid = new GameObject(name);
            solid.transform.SetParent(root.transform, false);
            solid.transform.SetLocalPositionAndRotation(centre, rotation);
            return solid.AddComponent<BoxCollider>();
        }

        static void Discard(Mesh mesh)
        {
            if (mesh == null)
                return;

            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        // One mesh rather than a Unity Terrain, the ground is generated per seed and thrown away on
        // travel, and a Terrain asset cannot be built and discarded that cheaply
        void BuildTerrain()
        {
            Rect bounds = plan.Bounds;
            float margin = 30f;

            int nx = Mathf.CeilToInt((bounds.width + margin * 2f) / TerrainCell);
            int nz = Mathf.CeilToInt((bounds.height + margin * 2f) / TerrainCell);

            Vector3[] vertices = new Vector3[(nx + 1) * (nz + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[nx * nz * 6];

            for (int z = 0; z <= nz; z++)
            {
                for (int x = 0; x <= nx; x++)
                {
                    float wx = bounds.xMin - margin + x * TerrainCell;
                    float wz = bounds.yMin - margin + z * TerrainCell;

                    int i = z * (nx + 1) + x;
                    vertices[i] = new Vector3(wx, Ground(wx, wz), wz);
                    uv[i] = new Vector2(wx * 0.1f, wz * 0.1f);
                }
            }

            int t = 0;
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    int i = z * (nx + 1) + x;

                    triangles[t++] = i;
                    triangles[t++] = i + nx + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + nx + 1;
                    triangles[t++] = i + nx + 2;
                }
            }

            Mesh mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.transform.SetParent(root.transform, false);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        void BuildWater()
        {
            if (definition.Shore != SettlementShore.Coastal || plan.Water.width <= 0f)
                return;

            GameObject water = Box("Water", new Vector3(plan.Water.center.x, -definition.Relief * 0.8f,
                plan.Water.center.y), new Vector3(plan.Water.width, 0.5f, plan.Water.height),
                waterMaterial, root.transform);

            water.GetComponent<Collider>().isTrigger = true;
        }

        // Streets are laid in short pieces so they ride the ground rather than floating over it
        IEnumerable BuildStreets()
        {
            foreach (Street street in plan.Streets)
            {
                float length = street.Length;
                if (length < 0.5f)
                    continue;

                int pieces = Mathf.Max(1, Mathf.CeilToInt(length / 12f));
                Material surface = street.Kind == StreetKind.Alley ? groundMaterial : streetMaterial;

                for (int i = 0; i < pieces; i++)
                {
                    Vector2 a = Vector2.Lerp(street.A, street.B, i / (float)pieces);
                    Vector2 b = Vector2.Lerp(street.A, street.B, (i + 1) / (float)pieces);
                    Vector2 mid = (a + b) * 0.5f;

                    float ya = Ground(a.x, a.y);
                    float yb = Ground(b.x, b.y);
                    float run = Vector2.Distance(a, b);

                    // A flat slab over rolling ground pokes through wherever the ground rises under
                    // it, so the slab is sunk by the worst deviation across its own footprint
                    float sink = Deviation(a, b, street.Width, (ya + yb) * 0.5f) + 0.25f;

                    // Pitched to the slope it crosses, so a road up a hill is a ramp not a stair.
                    // No collider, the ground underneath already carries one, five centimetres below
                    float pitch = Mathf.Atan2(yb - ya, run) * Mathf.Rad2Deg;

                    Add(new Vector3(mid.x, (ya + yb) * 0.5f + 0.05f - sink * 0.5f, mid.y),
                        new Vector3(run + 0.4f, sink, street.Width),
                        Quaternion.Euler(0f, -street.Yaw, 0f) * Quaternion.Euler(0f, 0f, pitch),
                        surface);
                }

                yield return null;
            }

            float r = plan.SquareRadius;
            float level = Ground(plan.Square.x, plan.Square.y);
            float depth = Deviation(plan.Square - Vector2.right * r, plan.Square + Vector2.right * r,
                r * 2f, level) + 0.4f;

            Add(new Vector3(plan.Square.x, level + 0.06f - depth * 0.5f, plan.Square.y),
                new Vector3(r * 2f, depth, r * 2f), Quaternion.identity, squareMaterial);

            Vector3 well = new Vector3(plan.Square.x, level + 0.6f, plan.Square.y);
            Vector3 wellSize = new Vector3(2.4f, 1.2f, 2.4f);

            Add(well, wellSize, Quaternion.identity, wallMaterial);
            Solid("Well", well, Quaternion.identity).size = wellSize;

            // Work is posted where people already gather.
            if (definition.QuestBoard != null)
            {
                Vector2 at = plan.Square + Vector2.right * (r * 0.6f);
                GameObject board = Instantiate(definition.QuestBoard, root.transform);
                board.transform.localPosition = new Vector3(at.x, Ground(at.x, at.y), at.y);
            }
        }

        // How far the ground strays from a level plane over a strip, sampled across it rather than
        // only at the ends: a road can be flat end to end and still cross a ridge sideways
        float Deviation(Vector2 a, Vector2 b, float width, float level)
        {
            Vector2 along = (b - a).normalized;
            Vector2 across = new Vector2(-along.y, along.x) * (width * 0.5f);
            float worst = 0f;

            for (int i = 0; i <= 4; i++)
            {
                Vector2 spine = Vector2.Lerp(a, b, i * 0.25f);

                for (int side = -1; side <= 1; side++)
                {
                    Vector2 at = spine + across * side;
                    worst = Mathf.Max(worst, Mathf.Abs(Ground(at.x, at.y) - level));
                }
            }

            return worst * 2f;
        }

        IEnumerable BuildWall()
        {
            foreach ((Vector3 centre, Vector3 size) in RampartPieces(plan, definition))
            {
                Add(centre, size, Quaternion.identity, wallMaterial);
                Solid("Rampart", centre, Quaternion.identity).size = size;
                yield return null;
            }
        }

        public static IEnumerable<(Vector3 centre, Vector3 size)> RampartPieces(SettlementPlan plan, SettlementDefinition definition)
        {
            if (!definition.Walled)
                yield break;

            Rect bounds = plan.Bounds;
            float gate = definition.MainStreetWidth + 6f;

            foreach (var piece in Side(plan, new Vector2(bounds.center.x, bounds.yMax), true, bounds.width, gate)) yield return piece;
            foreach (var piece in Side(plan, new Vector2(bounds.center.x, bounds.yMin), true, bounds.width, gate)) yield return piece;
            foreach (var piece in Side(plan, new Vector2(bounds.xMax, bounds.center.y), false, bounds.height, gate)) yield return piece;
            foreach (var piece in Side(plan, new Vector2(bounds.xMin, bounds.center.y), false, bounds.height, gate)) yield return piece;
        }

        // Each rampart is laid in pieces that follow the ground, with a gateway left in the middle
        static IEnumerable<(Vector3 centre, Vector3 size)> Side(SettlementPlan plan, Vector2 centre, bool alongX,
            float length, float gate)
        {
            float run = Mathf.Max(1f, (length - gate) * 0.5f);

            for (int half = 0; half < 2; half++)
            {
                float from = half == 0 ? -length * 0.5f : gate * 0.5f;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(run / 14f));

                for (int i = 0; i < pieces; i++)
                {
                    float a = from + run * (i / (float)pieces);
                    float b = from + run * ((i + 1) / (float)pieces);
                    float mid = (a + b) * 0.5f;

                    Vector2 at = alongX ? new Vector2(centre.x + mid, centre.y) : new Vector2(centre.x, centre.y + mid);
                    float size = b - a;

                    yield return (new Vector3(at.x, plan.Height(at.x, at.y) + RampartHeight * 0.5f, at.y),
                        alongX ? new Vector3(size, RampartHeight, WallThickness * 5f)
                               : new Vector3(WallThickness * 5f, RampartHeight, size));
                }
            }
        }

        void BuildBuilding(Building building, bool staffed)
        {
            Vector3 at = new Vector3(building.Centre.x, building.Ground, building.Centre.y);
            Quaternion yaw = Quaternion.Euler(0f, building.Yaw, 0f);

            Material walls = colourByPurpose && definition.TintFor(building.Purpose) != null
                ? definition.TintFor(building.Purpose)
                : wallMaterial;

            float storey = definition.StoreyHeight;
            Vector2 footprint = building.Size;

            // A building on a slope needs a plinth, or the uphill corner floats and the downhill one
            // is buried. Depth comes from the worst corner of its own footprint
            float drop = 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 local = new Vector3((corner < 2 ? -1f : 1f) * footprint.x * 0.5f, 0f,
                    (corner % 2 == 0 ? -1f : 1f) * footprint.y * 0.5f);

                Vector3 world = at + yaw * local;
                drop = Mathf.Max(drop, building.Ground - Ground(world.x, world.z));
            }

            drop += 0.5f;

            Piece(at, yaw, new Vector3(0f, -drop * 0.5f, 0f),
                new Vector3(footprint.x, drop, footprint.y), wallMaterial);

            for (int level = 0; level < building.Storeys; level++)
            {
                // Upper storeys overhang the street, the way a timber framed one does
                float grow = level * definition.Jetty;
                Vector2 size = footprint + Vector2.one * grow;
                float bottom = level * storey;

                Storey(at, yaw, size, bottom, storey, walls, level == 0);

                Piece(at, yaw, new Vector3(0f, bottom + storey, 0f),
                    new Vector3(size.x + 0.25f, 0.22f, size.y + 0.25f), roofMaterial);
            }

            float top = building.Storeys * storey;
            Vector2 roof = footprint + Vector2.one * ((building.Storeys - 1) * definition.Jetty);

            Piece(at, yaw, new Vector3(0f, top + 0.2f, 0f),
                new Vector3(roof.x + 0.4f, 0.4f, roof.y + 0.4f), roofMaterial);

            Doorway(Shell(at, yaw, building.Purpose, footprint, drop, top), at, yaw, building, footprint, staffed);
        }

        // Two colliders rather than one a wall, a building is never entered, its door leads to an
        // interior somewhere else entirely, so the shell only has to be solid
        Transform Shell(Vector3 at, Quaternion yaw, RoomPurpose purpose, Vector2 footprint, float drop, float top)
        {
            GameObject solid = new GameObject($"Building_{purpose}");
            solid.transform.SetParent(root.transform, false);
            solid.transform.SetLocalPositionAndRotation(at, yaw);

            BoxCollider plinth = solid.AddComponent<BoxCollider>();
            plinth.center = new Vector3(0f, -drop * 0.5f, 0f);
            plinth.size = new Vector3(footprint.x, drop, footprint.y);

            // Stops short of the door leaf, or looking at the door hits the wall in front of it first
            float front = (footprint.y - WallThickness) * 0.5f - 0.1f;
            float back = -footprint.y * 0.5f;

            BoxCollider shell = solid.AddComponent<BoxCollider>();
            shell.center = new Vector3(0f, top * 0.5f, (front + back) * 0.5f);
            shell.size = new Vector3(footprint.x, top, front - back);

            return solid.transform;
        }

        // Four walls, with a doorway cut into the front one at street level
        void Storey(Vector3 at, Quaternion yaw, Vector2 size, float bottom, float height, Material material, bool ground)
        {
            float mid = bottom + height * 0.5f;

            Piece(at, yaw, new Vector3(0f, mid, -(size.y - WallThickness) * 0.5f),
                new Vector3(size.x, height, WallThickness), material);
            Piece(at, yaw, new Vector3(-(size.x - WallThickness) * 0.5f, mid, 0f),
                new Vector3(WallThickness, height, size.y), material);
            Piece(at, yaw, new Vector3((size.x - WallThickness) * 0.5f, mid, 0f),
                new Vector3(WallThickness, height, size.y), material);

            float front = (size.y - WallThickness) * 0.5f;

            if (!ground)
            {
                Piece(at, yaw, new Vector3(0f, mid, front), new Vector3(size.x, height, WallThickness), material);
                return;
            }

            float side = Mathf.Max(0.2f, (size.x - DoorWidth) * 0.5f);
            float shift = (DoorWidth + side) * 0.5f;

            Piece(at, yaw, new Vector3(-shift, mid, front), new Vector3(side, height, WallThickness), material);
            Piece(at, yaw, new Vector3(shift, mid, front), new Vector3(side, height, WallThickness), material);

            float lintel = Mathf.Max(0.2f, height - DoorHeight);
            Piece(at, yaw, new Vector3(0f, bottom + DoorHeight + lintel * 0.5f, front),
                new Vector3(DoorWidth, lintel, WallThickness), material);
        }

        // A staffed building's keeper stands outside it, so the interior does not get a second one
        void Doorway(Transform shell, Vector3 at, Quaternion yaw, Building building, Vector2 footprint, bool staffed)
        {
            Vector3 local = new Vector3(0f, DoorHeight * 0.5f, (footprint.y - WallThickness) * 0.5f);

            GameObject door = new GameObject("Door");
            door.transform.SetParent(shell, false);
            door.transform.localPosition = local;
            door.AddComponent<BoxCollider>().size = new Vector3(DoorWidth, DoorHeight, 0.14f);

            Piece(at, yaw, local, new Vector3(DoorWidth, DoorHeight, 0.14f), doorMaterial);

            GameObject street = new GameObject("Outside");
            street.transform.SetParent(door.transform, false);
            street.transform.localPosition = new Vector3(0f, -DoorHeight * 0.5f + 1.2f, 2.2f);
            street.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Vector3 origin = new Vector3((interiorSlots % 16) * InteriorSpacing, 0f,
                (interiorSlots / 16) * InteriorSpacing);
            interiorSlots++;

            door.AddComponent<BuildingDoor>().Configure(
                Describe(building.Purpose), definition.InteriorFor(building.Purpose), definition.DoorPlug,
                staffed ? null : definition.ResidentFor(building.Purpose), definition.ServiceFor(building.Purpose),
                interiors, origin, street.transform);
        }

        void Piece(Vector3 at, Quaternion yaw, Vector3 local, Vector3 size, Material material) =>
            Add(at + yaw * local, size, yaw, material);

        public static string Describe(RoomPurpose purpose)
        {
            string name = purpose.ToString();
            System.Text.StringBuilder spaced = new System.Text.StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    spaced.Append(' ');

                spaced.Append(char.ToLowerInvariant(name[i]));
            }

            return spaced.ToString();
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;

            if (material != null)
                box.GetComponent<MeshRenderer>().sharedMaterial = material;

            return box;
        }

        public void CollectMarkers(List<MapMarker> into)
        {
            if (plan == null)
                return;

            foreach (Street street in plan.Streets)
            {
                into.Add(new MapMarker
                {
                    Centre = street.Centre,
                    Size = new Vector2(street.Length, street.Width),
                    Rotation = -street.Yaw,
                    Kind = MapMarkerKind.Street
                });
            }

            if (plan.Water.width > 0f)
                into.Add(new MapMarker { Centre = plan.Water.center, Size = plan.Water.size, Kind = MapMarkerKind.Water });

            into.Add(new MapMarker
            {
                Centre = plan.Square,
                Size = Vector2.one * (plan.SquareRadius * 2f),
                Label = "Square",
                Kind = MapMarkerKind.Square
            });

            foreach (Building building in plan.Buildings)
            {
                into.Add(new MapMarker
                {
                    Centre = building.Centre,
                    Size = building.Size,
                    Rotation = building.Yaw,
                    Label = Describe(building.Purpose),
                    Purpose = (int)building.Purpose,
                    Kind = MapMarkerKind.Building
                });
            }
        }
    }
}
