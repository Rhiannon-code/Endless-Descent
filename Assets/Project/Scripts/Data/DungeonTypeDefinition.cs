using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Dungeons/Dungeon Type", fileName = "DungeonType")]
    public class DungeonTypeDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "Crypt";
        [SerializeField] ModuleSet moduleSet;

        [Header("Main path")]
        [SerializeField] Vector2Int mainPathRooms = new Vector2Int(100, 140);

        [Header("Locks")]
        [SerializeField] Vector2Int gateCount = new Vector2Int(1, 3);
        [SerializeField, Min(1)] int minRoomsBetweenGates = 2;
        [SerializeField] Vector2Int keyBranchLength = new Vector2Int(1, 3);

        [Header("Optional branches")]
        [SerializeField] Vector2Int extraBranchCount = new Vector2Int(60, 100);
        [SerializeField] Vector2Int extraBranchLength = new Vector2Int(1, 3);
        [SerializeField, Range(0f, 1f)] float secretChance = 0.25f;
        [SerializeField, Range(0f, 1f)] float setPieceChance = 0.3f;

        [Header("Corridors")]
        [SerializeField] RoomModuleDefinition corridorStraight;
        [SerializeField] RoomModuleDefinition corridorCorner;
        [SerializeField] RoomModuleDefinition stair;
        [SerializeField] GameObject doorPlug;
        [SerializeField] GameObject door;
        [SerializeField] GameObject secretDoor;

        [Header("Inhabitants")]
        [SerializeField] EnemyDefinition[] enemies = new EnemyDefinition[0];
        [SerializeField] EnemyDefinition boss;
        [SerializeField] LootTableDefinition loot;
        [SerializeField] LootTableDefinition bossLoot;

        [Header("Hazards")]
        [SerializeField] GameObject[] traps = new GameObject[0];
        [SerializeField, Range(0f, 1f)] float trapChance = 0.18f;

        // Campsites and repair benches, rare enough that finding one matters
        [SerializeField] GameObject[] furnishings = new GameObject[0];
        [SerializeField, Range(0f, 1f)] float furnishingChance = 0.05f;

        // Who lives in which kind of building. Settlements use this, dungeons leave it empty
        [SerializeField] RoomPurpose[] residentPurposes = new RoomPurpose[0];
        [SerializeField] GameObject[] residentPrefabs = new GameObject[0];
        [SerializeField, Min(0)] int maxCorridorSegments = 4;

        [Header("Shortcuts")]
        [SerializeField, Range(0f, 1f)] float shortcutChance = 0.4f;
        [SerializeField, Min(2)] int minShortcutSpan = 3;

        // Off while the dungeon is being walked for testing, every door opens. Flip it back on to
        // make gate edges want the key their layout already places
        [SerializeField] bool lockDoors;

        // A square room module has four faces, so a room can never host more than four connections
        [SerializeField, Min(1)] int maxRoomExits = 4;

        [Header("Levels")]
        [SerializeField] Vector2Int roomsPerLevel = new Vector2Int(5, 7);

        // Chance an optional branch drops a level instead of staying flat. This is what puts several
        // staircases on a floor, in different places, rather than one stairwell running top to bottom
        [SerializeField, Range(0f, 1f)] float branchDescendChance = 0.45f;

        // Guaranteed, not left to chance, every floor above the deepest gets at least this many ways
        // down, anchored on rooms spread along that floor's spine
        [SerializeField, Min(1)] int minStairsPerLevel = 3;

        [Header("Shape")]
        [SerializeField, Range(0f, 1f)] float bigLevelChance = 0.55f;
        [SerializeField] Vector2Int bigLevelRooms = new Vector2Int(15, 25);
        [SerializeField, Range(0f, 1f)] float loopChance = 0.16f;
        [SerializeField, Min(2)] int minLoopSpan = 4;
        [SerializeField, Range(0f, 1f)] float deadEndReconnectChance = 0.8f;

        public string DisplayName => displayName;
        public ModuleSet ModuleSet => moduleSet;
        public Vector2Int MainPathRooms => mainPathRooms;
        public Vector2Int GateCount => gateCount;
        public int MinRoomsBetweenGates => Mathf.Max(1, minRoomsBetweenGates);
        public Vector2Int KeyBranchLength => keyBranchLength;
        public Vector2Int ExtraBranchCount => extraBranchCount;
        public Vector2Int ExtraBranchLength => extraBranchLength;
        public float SecretChance => secretChance;
        public float SetPieceChance => setPieceChance;
        public float ShortcutChance => shortcutChance;
        public int MinShortcutSpan => Mathf.Max(2, minShortcutSpan);
        public RoomModuleDefinition CorridorStraight => corridorStraight;
        public RoomModuleDefinition CorridorCorner => corridorCorner;
        public int MaxCorridorSegments => Mathf.Max(0, maxCorridorSegments);
        public int MaxRoomExits => Mathf.Max(1, maxRoomExits);
        public RoomModuleDefinition Stair => stair;
        public GameObject DoorPlug => doorPlug;
        public GameObject Door => door;
        public GameObject SecretDoor => secretDoor;
        public EnemyDefinition[] Enemies => enemies;
        public EnemyDefinition Boss => boss;
        public LootTableDefinition Loot => loot;
        public LootTableDefinition BossLoot => bossLoot != null ? bossLoot : loot;
        public GameObject[] Traps => traps;
        public float TrapChance => trapChance;
        public GameObject[] Furnishings => furnishings;
        public float FurnishingChance => furnishingChance;

        public GameObject ResidentFor(RoomPurpose purpose)
        {
            for (int i = 0; i < residentPurposes.Length && i < residentPrefabs.Length; i++)
                if (residentPurposes[i] == purpose) return residentPrefabs[i];

            return null;
        }
        public bool LockDoors => lockDoors;
        public Vector2Int RoomsPerLevel => roomsPerLevel;
        public float BranchDescendChance => branchDescendChance;
        public int MinStairsPerLevel => Mathf.Max(1, minStairsPerLevel);
        public float BigLevelChance => bigLevelChance;
        public Vector2Int BigLevelRooms => bigLevelRooms;
        public float LoopChance => loopChance;
        public int MinLoopSpan => Mathf.Max(2, minLoopSpan);
        public float DeadEndReconnectChance => deadEndReconnectChance;
    }
}
