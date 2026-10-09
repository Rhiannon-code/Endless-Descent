using UnityEngine;

namespace EndlessDescent.Data
{
    public enum SettlementShore { Inland, Coastal }

    // A settlement is grown, not laid out, streets wander out from the market and buildings crowd
    // whatever frontage they make. The one regular part is the civic quarter
    [CreateAssetMenu(menuName = "Endless Descent/World/Settlement", fileName = "Settlement")]
    public class SettlementDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "Settlement";

        [Header("Ground")]
        [SerializeField] Vector2 extent = new Vector2(280f, 260f);
        [SerializeField] SettlementShore shore = SettlementShore.Inland;
        [SerializeField] bool walled = true;
        [SerializeField, Min(0f)] float relief = 6f;
        [SerializeField, Min(10f)] float terrainScale = 140f;

        [Header("Street growth")]
        [SerializeField, Min(0f)] float squareRadius = 17f;
        [SerializeField, Min(1)] int arteries = 4;
        [SerializeField, Min(1)] int arteryLength = 5;
        [SerializeField, Min(0)] int rings = 2;
        [SerializeField, Min(0)] int lanes = 22;
        [SerializeField] Vector2 stepRange = new Vector2(24f, 36f);
        [SerializeField, Min(0f)] float wander = 0.4f;
        [SerializeField, Min(0f)] float ringGap = 30f;

        [Header("Street widths")]
        [SerializeField, Min(3f)] float mainStreetWidth = 10f;
        [SerializeField, Min(2f)] float laneWidth = 6.5f;
        [SerializeField, Min(1.5f)] float alleyWidth = 3.6f;

        [Header("Civic quarter")]
        [SerializeField, Min(0f)] float civicQuarter = 0f;
        [SerializeField, Min(10f)] float civicSpacing = 34f;
        [SerializeField] Vector2Int civicStoreys = new Vector2Int(3, 5);

        [Header("Buildings")]
        [SerializeField] Vector2 buildingWidth = new Vector2(6.5f, 12f);
        [SerializeField] Vector2 buildingDepth = new Vector2(7f, 11f);
        [SerializeField] Vector2 gap = new Vector2(0.2f, 1.8f);
        [SerializeField, Min(0f)] float setback = 0.5f;
        [SerializeField, Min(0.5f)] float retry = 3f;
        [SerializeField, Min(0f)] float skew = 0.2f;
        [SerializeField] Vector2Int coreStoreys = new Vector2Int(2, 4);
        [SerializeField] Vector2Int edgeStoreys = new Vector2Int(1, 3);
        [SerializeField, Min(2f)] float storeyHeight = 3.1f;
        [SerializeField, Min(0f)] float jetty = 0.35f;

        [Header("Contents")]
        [SerializeField] RoomPurpose[] buildingPurposes = new RoomPurpose[0];
        [SerializeField] RoomPurpose[] civicPurposes = new RoomPurpose[0];
        [SerializeField] RoomModuleDefinition[] interiors = new RoomModuleDefinition[0];
        [SerializeField] GameObject doorPlug;

        // One of the nine circles keeps an unmarked door here, on the back of an ordinary building.
        // Most settlements have none, and none of them advertise it
        [SerializeField] GameObject hiddenCircle;
        [SerializeField] GameObject questBoard;
        [SerializeField] RoomPurpose[] servicePurposes = new RoomPurpose[0];
        [SerializeField] GameObject[] servicePrefabs = new GameObject[0];
        [SerializeField] RoomPurpose[] residentPurposes = new RoomPurpose[0];
        [SerializeField] GameObject[] residentPrefabs = new GameObject[0];

        [Header("Testing")]
        [SerializeField] RoomPurpose[] tintPurposes = new RoomPurpose[0];
        [SerializeField] Material[] tintMaterials = new Material[0];

        public string DisplayName => displayName;
        public Vector2 Extent => extent;
        public SettlementShore Shore => shore;
        public bool Walled => walled;
        public float Relief => relief;
        public float TerrainScale => terrainScale;

        public float SquareRadius => squareRadius;
        public int Arteries => Mathf.Max(1, arteries);
        public int ArteryLength => Mathf.Max(1, arteryLength);
        public int Rings => Mathf.Max(0, rings);
        public int Lanes => Mathf.Max(0, lanes);
        public Vector2 StepRange => stepRange;
        public float Wander => wander;
        public float RingGap => Mathf.Max(8f, ringGap);

        public float MainStreetWidth => mainStreetWidth;
        public float LaneWidth => laneWidth;
        public float AlleyWidth => alleyWidth;

        public float CivicQuarter => civicQuarter;
        public float CivicSpacing => Mathf.Max(10f, civicSpacing);
        public Vector2Int CivicStoreys => civicStoreys;

        public Vector2 BuildingWidth => buildingWidth;
        public Vector2 BuildingDepth => buildingDepth;
        public Vector2 Gap => gap;
        public float Setback => setback;
        public float Retry => Mathf.Max(0.5f, retry);
        public float Skew => skew;
        public Vector2Int CoreStoreys => coreStoreys;
        public Vector2Int EdgeStoreys => edgeStoreys;
        public float StoreyHeight => storeyHeight;
        public float Jetty => jetty;

        public RoomPurpose[] BuildingPurposes => buildingPurposes;
        public RoomPurpose[] CivicPurposes => civicPurposes;
        public GameObject DoorPlug => doorPlug;
        public GameObject HiddenCircle => hiddenCircle;

        public GameObject QuestBoard => questBoard;

        public RoomModuleDefinition InteriorFor(RoomPurpose purpose)
        {
            foreach (RoomModuleDefinition module in interiors)
                if (module != null && module.Purpose == purpose) return module;

            return null;
        }

        // What is inside a building of this trade, a bench in a smithy, a trainer in an armourer's,
        // a bed in an inn. Trainers and benches used to exist only as dungeon furnishings, which left
        // training and repair unreachable in a town
        public GameObject ServiceFor(RoomPurpose purpose)
        {
            for (int i = 0; i < servicePurposes.Length && i < servicePrefabs.Length; i++)
                if (servicePurposes[i] == purpose) return servicePrefabs[i];

            return null;
        }

        public GameObject ResidentFor(RoomPurpose purpose)
        {
            for (int i = 0; i < residentPurposes.Length && i < residentPrefabs.Length; i++)
                if (residentPurposes[i] == purpose) return residentPrefabs[i];

            return null;
        }

        // Colour by trade, so a guildhall can be picked out across a city while the greybox has no
        // signage, no windows and nothing else to tell one box from another
        public Material TintFor(RoomPurpose purpose)
        {
            for (int i = 0; i < tintPurposes.Length && i < tintMaterials.Length; i++)
                if (tintPurposes[i] == purpose) return tintMaterials[i];

            return null;
        }
    }
}
