using EndlessDescent.Data;

namespace EndlessDescent.Dungeons
{
    // Plain settings rather than the ScriptableObject, so the layout stage carries no Unity asset
    // dependency and can be generated and verified outside the editor
    public struct DungeonLayoutSettings
    {
        public int MinMainRooms;
        public int MaxMainRooms;
        public int MinGates;
        public int MaxGates;
        public int MinRoomsBetweenGates;
        public int MinKeyBranch;
        public int MaxKeyBranch;
        public int MinExtraBranches;
        public int MaxExtraBranches;
        public int MinExtraBranchLength;
        public int MaxExtraBranchLength;
        public float SecretChance;
        public float SetPieceChance;
        public float ShortcutChance;
        public int MinShortcutSpan;
        public int MaxRoomExits;
        public int MinRoomsPerLevel;
        public int MaxRoomsPerLevel;
        public float BranchDescendChance;
        public int MinStairsPerLevel;

        // Levels come in two sizes on purpose, somewhere to get lost, and a landing between them
        public float BigLevelChance;
        public int MinBigLevelRooms;
        public int MaxBigLevelRooms;

        // Loops are what make a level wind. Without them the graph is a tree, every branch tip is a
        // dead end by construction, and the only way back is the way you came
        public float LoopChance;
        public int MinLoopSpan;
        public float DeadEndReconnectChance;

        public static DungeonLayoutSettings From(DungeonTypeDefinition type, DungeonScale scale) =>
            Scaled(From(type), scale);

        // One table, so the game and the harnesses cannot disagree about what "wayside" means. Sizes
        // are absolute rather than a fraction of the archetype, a wayside crypt and a wayside mine
        // should both be a ten-minute errand, and differ in shape rather than in how long they take
        // Only Great keeps the archetype's own sprawl
        public static DungeonLayoutSettings Scaled(DungeonLayoutSettings settings, DungeonScale scale)
        {
            if (scale == DungeonScale.Great)
                return settings;

            bool wayside = scale == DungeonScale.Wayside;

            settings.MinMainRooms = wayside ? 12 : 55;
            settings.MaxMainRooms = wayside ? 18 : 72;
            settings.MinExtraBranches = wayside ? 4 : 22;
            settings.MaxExtraBranches = wayside ? 8 : 30;
            settings.MinBigLevelRooms = wayside ? 6 : 10;
            settings.MaxBigLevelRooms = wayside ? 9 : 15;

            return settings;
        }

        public static DungeonLayoutSettings From(DungeonTypeDefinition type)
        {
            return new DungeonLayoutSettings
            {
                MinMainRooms = type.MainPathRooms.x,
                MaxMainRooms = type.MainPathRooms.y,
                MinGates = type.GateCount.x,
                MaxGates = type.GateCount.y,
                MinRoomsBetweenGates = type.MinRoomsBetweenGates,
                MinKeyBranch = type.KeyBranchLength.x,
                MaxKeyBranch = type.KeyBranchLength.y,
                MinExtraBranches = type.ExtraBranchCount.x,
                MaxExtraBranches = type.ExtraBranchCount.y,
                MinExtraBranchLength = type.ExtraBranchLength.x,
                MaxExtraBranchLength = type.ExtraBranchLength.y,
                SecretChance = type.SecretChance,
                SetPieceChance = type.SetPieceChance,
                ShortcutChance = type.ShortcutChance,
                MinShortcutSpan = type.MinShortcutSpan,
                MaxRoomExits = type.MaxRoomExits,
                MinRoomsPerLevel = type.RoomsPerLevel.x,
                MaxRoomsPerLevel = type.RoomsPerLevel.y,
                BranchDescendChance = type.BranchDescendChance,
                MinStairsPerLevel = type.MinStairsPerLevel,
                BigLevelChance = type.BigLevelChance,
                MinBigLevelRooms = type.BigLevelRooms.x,
                MaxBigLevelRooms = type.BigLevelRooms.y,
                LoopChance = type.LoopChance,
                MinLoopSpan = type.MinLoopSpan,
                DeadEndReconnectChance = type.DeadEndReconnectChance
            };
        }

        public static readonly (string Set, int SpineLow, int SpineHigh,
            int BranchLow, int BranchHigh, int BigLow, int BigHigh)[] Scales =
        {
            ("Barrow",    165, 200,  62,  80, 16, 24),
            ("Crypt",     195, 232,  74,  92, 17, 25),
            ("Barracks",  210, 248,  80, 100, 17, 26),
            ("Gaol",      224, 262,  86, 107, 18, 27),
            ("Monastery", 252, 296,  99, 123, 19, 28),
            ("Keep",      280, 328, 112, 140, 20, 30),
            ("Sanctum",   308, 360, 122, 152, 21, 31),
            ("Undercroft",336, 394, 135, 168, 22, 32),
            ("Waterworks",392, 458, 156, 194, 23, 34),
            ("Mine",      448, 524, 179, 222, 24, 36)
        };

        public static DungeonLayoutSettings Preset(string set)
        {
            foreach ((string name, int spineLow, int spineHigh,
                      int branchLow, int branchHigh, int bigLow, int bigHigh) in Scales)
            {
                if (name != set)
                    continue;

                return new DungeonLayoutSettings
                {
                    MinMainRooms = spineLow,
                    MaxMainRooms = spineHigh,
                    MinGates = 1,
                    MaxGates = 3,
                    MinRoomsBetweenGates = 2,
                    MinKeyBranch = 1,
                    MaxKeyBranch = 3,
                    MinExtraBranches = branchLow,
                    MaxExtraBranches = branchHigh,
                    MinExtraBranchLength = 2,
                    MaxExtraBranchLength = 5,
                    SecretChance = 0.25f,
                    SetPieceChance = 0.3f,
                    ShortcutChance = 0.4f,
                    MinShortcutSpan = 3,
                    MaxRoomExits = 4,
                    MinRoomsPerLevel = 4,
                    MaxRoomsPerLevel = 7,
                    BranchDescendChance = 0.45f,
                    MinStairsPerLevel = 3,
                    BigLevelChance = 0.55f,
                    MinBigLevelRooms = bigLow,
                    MaxBigLevelRooms = bigHigh,
                    LoopChance = 0.16f,
                    MinLoopSpan = 4,
                    DeadEndReconnectChance = 0.8f
                };
            }

            return Preset("Crypt");
        }

        public static DungeonLayoutSettings Crypt() => Preset("Crypt");
    }
}
