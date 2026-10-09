using System.Collections.Generic;
using System.Threading.Tasks;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Settlements;
using UnityEngine;

namespace EndlessDescent.World
{
    // Which places exist is decided by where the player is standing. Settlements are at least 30 km
    // apart, so one built town and one prepared dungeon are always enough
    [DisallowMultipleComponent]
    public class PlaceStreamer : MonoBehaviour
    {
        [SerializeField] PlaceLoader place;
        [SerializeField] Transform player;
        [SerializeField] Material silhouetteMaterial;
        [SerializeField, Min(0.5f)] float unloadKm = 3.5f;
        [SerializeField, Min(1f)] float silhouetteKm = 40f;
        [SerializeField, Min(0.05f)] float prepareDungeonKm = 0.25f;
        [SerializeField, Min(0.05f)] float releaseDungeonKm = 0.6f;
        [SerializeField, Min(0.05f)] float interval = 0.5f;

        readonly Dictionary<int, SettlementPlan> plans = new Dictionary<int, SettlementPlan>();
        readonly Dictionary<int, Task<SettlementPlan>> planning = new Dictionary<int, Task<SettlementPlan>>();
        readonly Dictionary<int, GameObject> silhouettes = new Dictionary<int, GameObject>();
        float next;

        void OnEnable() => CurrentSpace.Changed += OnSpaceChanged;
        void OnDisable() => CurrentSpace.Changed -= OnSpaceChanged;

        // Underground or indoors the town silhouettes are overhead and nothing of them can be seen
        void OnSpaceChanged(SpaceKind kind)
        {
            foreach (GameObject shape in silhouettes.Values)
                if (shape != null) shape.SetActive(kind == SpaceKind.Overworld);
        }

        void Update()
        {
            WorldTerrainStreamer world = place != null ? place.World : null;

            if (world == null || player == null || !world.Ready || Transition.Busy || Time.time < next)
                return;

            next = Time.time + interval;
            place.StandEntrances();

            if (place.Inside)
                return;

            Vector2 here = WorldOrigin.KmAt(player.position);
            RegionMap region = place.Region;

            int town = -1, dungeon = -1;
            float townKm = float.MaxValue, dungeonKm = float.MaxValue;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];
                float km = Vector2.Distance(here, location.Position);

                if (location.IsSettlement)
                {
                    Silhouette(i, location, km, world.Surface);

                    if (km < townKm) { townKm = km; town = i; }
                }
                else if (location.Dungeon != null && km < dungeonKm)
                {
                    dungeonKm = km;
                    dungeon = i;
                }
            }

            Settlement(town, townKm);
            Dungeon(dungeon, dungeonKm);

            place.SetNearest(townKm <= PlaceLoader.TownReachKm ? town : dungeonKm <= prepareDungeonKm ? dungeon : -1);
        }

        void Settlement(int town, float km)
        {
            if (place.BuiltSettlement >= 0 && (place.BuiltSettlement != town || km > unloadKm))
                place.ClearSettlement();

            if (town >= 0 && km <= PlaceLoader.TownReachKm && place.BuiltSettlement != town && plans.TryGetValue(town, out SettlementPlan plan))
                place.BuildSettlement(town, plan);
        }

        // Preparing is where the dungeon is built, and that is the one cost here that is not spread
        // over frames, it happens once per approach rather than on pressing the door
        void Dungeon(int dungeon, float km)
        {
            if (place.PreparedDungeon >= 0 && (place.PreparedDungeon != dungeon || km > releaseDungeonKm))
                place.ClearDungeon();

            if (dungeon >= 0 && km <= prepareDungeonKm && place.PreparedDungeon != dungeon)
                place.PrepareDungeon(dungeon);
        }

        void Silhouette(int index, RegionLocation location, float km, WorldSurface surface)
        {
            if (km > silhouetteKm)
            {
                if (km > silhouetteKm + 5f)
                    Forget(index);

                return;
            }

            if (!plans.TryGetValue(index, out SettlementPlan plan))
            {
                if (!planning.TryGetValue(index, out Task<SettlementPlan> task))
                {
                    SettlementGrowth growth = SettlementGrowth.From(location.Settlement);
                    int seed = place.SeedFor(index);
                    Vector2 at = location.Position;

                    planning[index] = Task.Run(() => SettlementGenerator.PlanOn(growth, seed, surface, at));
                    return;
                }

                if (!task.IsCompleted)
                    return;

                planning.Remove(index);

                // Kept as a null plan so a town that cannot be planned is reported once, not every tick
                if (task.IsFaulted)
                    Debug.LogException(task.Exception.InnerException, this);

                plans[index] = plan = task.IsFaulted ? null : task.Result;
            }

            if (plan == null)
                return;

            if (silhouettes.ContainsKey(index))
                return;

            GameObject shape = new GameObject($"Silhouette_{location.DisplayName}", typeof(MeshFilter), typeof(MeshRenderer));
            shape.transform.SetParent(transform, false);
            shape.transform.position = WorldOrigin.UnityAt(location.Position, surface.Height(location.Position));
            shape.SetActive(CurrentSpace.Outdoors);

            shape.GetComponent<MeshFilter>().sharedMesh = SettlementSilhouette.Build(plan, location.Settlement);
            shape.GetComponent<MeshRenderer>().sharedMaterial = silhouetteMaterial;
            silhouettes[index] = shape;
        }

        void Forget(int index)
        {
            plans.Remove(index);

            if (!silhouettes.TryGetValue(index, out GameObject shape))
                return;

            Destroy(shape.GetComponent<MeshFilter>().sharedMesh);
            Destroy(shape);
            silhouettes.Remove(index);
        }

        void OnDestroy()
        {
            foreach (GameObject shape in silhouettes.Values)
                if (shape != null) Destroy(shape.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
