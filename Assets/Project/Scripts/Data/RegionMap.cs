using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [Serializable]
    public struct RegionLocation
    {
        public string DisplayName;
        public Vector2 Position;
        public DungeonTypeDefinition Dungeon;
        public DungeonScale Scale;
        public SettlementDefinition Settlement;

        // Who lives here. A generated quest is about one of these people and somebody they know
        // (ADR 0003), so a town without a cast has no work to post
        public NpcDefinition[] Residents;

        public bool IsSettlement => Settlement != null;
    }

    // The region as ground rather than as a menu: every place has a position, and how long you walk
    // between two of them falls out of how far apart they are
    [CreateAssetMenu(menuName = "Endless Descent/World/Region Map", fileName = "RegionMap")]
    public class RegionMap : ScriptableObject
    {
        [SerializeField] RegionLocation[] locations = new RegionLocation[0];
        [SerializeField, Min(1f)] float kilometresPerDay = 28f;

        public IReadOnlyList<RegionLocation> Locations => locations;
        public float KilometresPerDay => kilometresPerDay;

        public bool Has(int index) => locations != null && index >= 0 && index < locations.Length;

        public float Distance(int from, int to) =>
            Has(from) && Has(to) ? Vector2.Distance(locations[from].Position, locations[to].Position) : 0f;

        public int TravelDays(int from, int to)
        {
            if (from == to || !Has(from) || !Has(to))
                return 0;

            return Mathf.Max(1, Mathf.CeilToInt(Distance(from, to) / kilometresPerDay));
        }

        public Rect Extent()
        {
            if (locations == null || locations.Length == 0)
                return new Rect(-1f, -1f, 2f, 2f);

            Vector2 min = locations[0].Position;
            Vector2 max = min;

            foreach (RegionLocation location in locations)
            {
                min = Vector2.Min(min, location.Position);
                max = Vector2.Max(max, location.Position);
            }

            return new Rect(min, Vector2.Max(max - min, Vector2.one));
        }
    }
}
