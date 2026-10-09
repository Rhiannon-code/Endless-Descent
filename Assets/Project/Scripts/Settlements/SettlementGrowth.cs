using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Settlements
{
    // Plain settings rather than the ScriptableObject, so the layout stage carries no Unity asset
    // dependency and can be generated and checked outside the editor, the same reason
    // DungeonLayoutSettings exists. Taking the asset directly is what let a layout bug
    // reach a playtest unmeasured
    public struct SettlementGrowth
    {
        public Vector2 Extent;
        public bool Coastal;
        public float Relief;
        public float TerrainScale;

        public float SquareRadius;
        public int Arteries;
        public int ArteryLength;
        public int Rings;
        public int Lanes;
        public Vector2 StepRange;
        public float Wander;
        public float RingGap;

        public float MainStreetWidth;
        public float LaneWidth;
        public float AlleyWidth;

        public float CivicQuarter;
        public float CivicSpacing;
        public Vector2Int CivicStoreys;

        public Vector2 BuildingWidth;
        public Vector2 BuildingDepth;
        public Vector2 Gap;
        public float Setback;
        public float Retry;
        public float Skew;
        public Vector2Int CoreStoreys;
        public Vector2Int EdgeStoreys;

        public RoomPurpose[] Trades;
        public RoomPurpose[] Civic;

        public static SettlementGrowth From(SettlementDefinition definition)
        {
            return new SettlementGrowth
            {
                Extent = definition.Extent,
                Coastal = definition.Shore == SettlementShore.Coastal,
                Relief = definition.Relief,
                TerrainScale = definition.TerrainScale,
                SquareRadius = definition.SquareRadius,
                Arteries = definition.Arteries,
                ArteryLength = definition.ArteryLength,
                Rings = definition.Rings,
                Lanes = definition.Lanes,
                StepRange = definition.StepRange,
                Wander = definition.Wander,
                RingGap = definition.RingGap,
                MainStreetWidth = definition.MainStreetWidth,
                LaneWidth = definition.LaneWidth,
                AlleyWidth = definition.AlleyWidth,
                CivicQuarter = definition.CivicQuarter,
                CivicSpacing = definition.CivicSpacing,
                CivicStoreys = definition.CivicStoreys,
                BuildingWidth = definition.BuildingWidth,
                BuildingDepth = definition.BuildingDepth,
                Gap = definition.Gap,
                Setback = definition.Setback,
                Retry = definition.Retry,
                Skew = definition.Skew,
                CoreStoreys = definition.CoreStoreys,
                EdgeStoreys = definition.EdgeStoreys,
                Trades = definition.BuildingPurposes,
                Civic = definition.CivicPurposes
            };
        }
    }
}
