using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Settlements
{
    // A city is ~650 buildings of a dozen boxes each. Outdoors there is nothing to occlude them, so
    // culling is by distance rather than by the graph trick dungeons use
    // Whole grid cells are switched at once: a hundred cells is a cheap loop, six thousand renderers
    // is not
    [DisallowMultipleComponent]
    public class SettlementVisibility : MonoBehaviour
    {
        [SerializeField] SettlementGenerator generator;
        [SerializeField] Transform viewer;
        [SerializeField, Min(20f)] float range = 700f;
        [SerializeField, Min(20f)] float cell = CellMetres;

        // The generator merges a town's geometry into one mesh per material per cell of this size, so
        // both sides have to agree on it
        public const float CellMetres = 48f;
        [SerializeField, Min(0.05f)] float interval = 0.25f;

        sealed class Cell
        {
            public Vector2 Centre;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public bool Drawn = true;
        }

        readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();

        SettlementPlan indexed;
        float next;

        public int VisibleCells { get; private set; }

        void OnEnable()
        {
            WorldOrigin.Shifted += OnShifted;
            CurrentSpace.Changed += OnSpaceChanged;
        }

        void OnDisable()
        {
            WorldOrigin.Shifted -= OnShifted;
            CurrentSpace.Changed -= OnSpaceChanged;
        }

        // The cells are indexed where the town stood at the time, and the whole scene has just moved
        void OnShifted(Vector3 by)
        {
            Vector2 flat = new Vector2(by.x, by.z);

            foreach (Cell bucket in cells.Values)
                bucket.Centre -= flat;
        }

        void OnSpaceChanged(SpaceKind kind) => next = 0f;

        void LateUpdate()
        {
            if (generator == null || viewer == null)
                return;

            if (!ReferenceEquals(generator.Plan, indexed))
                Index();

            if (cells.Count == 0 || Time.time < next)
                return;

            next = Time.time + interval;
            Apply();
        }

        void Index()
        {
            cells.Clear();
            indexed = generator.Plan;

            if (indexed == null)
                return;

            // The ground is one mesh and the sky needs it under your feet wherever you stand, so it
            // is never a candidate for culling
            foreach (Transform child in generator.transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.parent == null || child.name == "Ground" || child.name == "Water")
                    continue;

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                Vector3 at = child.position;
                Vector2Int key = new Vector2Int(Mathf.FloorToInt(at.x / cell), Mathf.FloorToInt(at.z / cell));

                if (!cells.TryGetValue(key, out Cell bucket))
                {
                    cells[key] = bucket = new Cell
                    {
                        Centre = new Vector2((key.x + 0.5f) * cell, (key.y + 0.5f) * cell)
                    };
                }

                bucket.Renderers.Add(renderer);
            }
        }

        void Apply()
        {
            // Inside a building or a dungeon the town is overhead and nothing of it can be seen
            bool outdoors = CurrentSpace.Outdoors;

            Vector2 here = new Vector2(viewer.position.x, viewer.position.z);
            float limit = range + cell;
            float squared = limit * limit;

            VisibleCells = 0;

            foreach (Cell bucket in cells.Values)
            {
                bool draw = outdoors && (bucket.Centre - here).sqrMagnitude <= squared;

                if (draw)
                    VisibleCells++;

                if (bucket.Drawn == draw)
                    continue;

                foreach (Renderer renderer in bucket.Renderers)
                    if (renderer != null) renderer.enabled = draw;

                bucket.Drawn = draw;
            }
        }
    }
}
