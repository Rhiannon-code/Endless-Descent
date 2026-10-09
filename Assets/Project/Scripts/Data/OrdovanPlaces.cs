using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum Province { Meregard, Vareth, Dolmarch, Skarrvald, Thessanur, Holds }

    public enum PlaceKind { Settlement, Dungeon, Ruin }

    // In the content builder's own order, smallest first. The name is also the settlement asset's,
    // a Town resolves to Settlement_Town and nothing has to map between them
    public enum SettlementTier { None, Hamlet, Village, Harbour, Town, Port, City, PortCity }

    // Every place the Imperial Survey records. A settlement resolves to Settlement_<Tier> and a
    // dungeon to DungeonType_<Archetype>, so every one of them has content without any of them being
    // named individually in the builder
    public static class OrdovanPlaces
    {
        public readonly struct Place
        {
            public readonly string Name;
            public readonly Province Province;
            public readonly PlaceKind Kind;
            public readonly SettlementTier Tier;
            public readonly string Archetype;

            // Roads run between these and nowhere else, the cities, and the seat of each province
            // and neighbour, so the trunk network reaches every region without paving the villages
            public readonly bool Hub;
            public readonly Vector2 Km;

            // Dungeons only, how big this particular one is
            public readonly DungeonScale Scale;

            public bool IsSettlement => Kind == PlaceKind.Settlement;

            public string Asset => IsSettlement ? $"Settlement_{Tier}" : $"DungeonType_{Archetype}";

            public Place(string name, Province province, PlaceKind kind, SettlementTier tier,
                string archetype, bool hub, float east, float north, DungeonScale scale = DungeonScale.Wayside)
            {
                Scale = scale;
                Name = name;
                Province = province;
                Kind = kind;
                Tier = tier;
                Archetype = archetype;
                Hub = hub;
                Km = new Vector2(east, north);
            }
        }

        public static readonly Place[] All =
        {
            new Place("Saltmere", Province.Meregard, PlaceKind.Settlement, SettlementTier.Harbour, null, false, -42f, 62f),
            new Place("Kelder Wharf", Province.Meregard, PlaceKind.Settlement, SettlementTier.Port, null, true, 18f, 66f),
            new Place("Grand Anchor", Province.Meregard, PlaceKind.Settlement, SettlementTier.PortCity, null, true, 74f, 58f),
            new Place("Dunmoor", Province.Meregard, PlaceKind.Settlement, SettlementTier.Village, null, false, -30f, 18f),
            new Place("Ashmere", Province.Meregard, PlaceKind.Settlement, SettlementTier.Town, null, false, 6f, 8f),
            new Place("Highwall", Province.Meregard, PlaceKind.Settlement, SettlementTier.City, null, true, 52f, 20f),
            new Place("Wren's Rest", Province.Meregard, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -56f, -24f),
            new Place("Crypt", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Crypt", false, -12f, -14f),
            new Place("Barrow", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Barrow", false, -48f, -46f),
            new Place("Undercroft", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Undercroft", false, 22f, -10f),
            new Place("Monastery", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Monastery", false, 64f, -18f),
            new Place("Gaol", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Gaol", false, 40f, -40f),
            new Place("Deep Mine", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, -22f, -58f),
            new Place("Arcane Sanctum", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Sanctum", false, 84f, 6f),
            new Place("Keep", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Keep", false, 30f, 36f),
            new Place("Barracks", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Barracks", false, -4f, 40f),
            new Place("Waterworks", Province.Meregard, PlaceKind.Dungeon, SettlementTier.None, "Waterworks", false, 12f, -32f),
            new Place("Casrenne", Province.Vareth, PlaceKind.Settlement, SettlementTier.City, null, true, 40f, -150f),
            new Place("Tallow Cross", Province.Vareth, PlaceKind.Settlement, SettlementTier.Town, null, false, -4f, -104f),
            new Place("Verrin", Province.Vareth, PlaceKind.Settlement, SettlementTier.Town, null, false, 92f, -128f),
            new Place("Oldgate", Province.Vareth, PlaceKind.Settlement, SettlementTier.Town, null, false, 8f, -186f),
            new Place("Hollybeck", Province.Vareth, PlaceKind.Settlement, SettlementTier.Village, null, false, 66f, -96f),
            new Place("Sarn", Province.Vareth, PlaceKind.Settlement, SettlementTier.Village, null, false, -14f, -166f),
            new Place("Mill Wenden", Province.Vareth, PlaceKind.Settlement, SettlementTier.Village, null, false, 104f, -172f),
            new Place("Cadger's Rest", Province.Vareth, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, 74f, -196f),
            new Place("Pell", Province.Vareth, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -22f, -132f),
            new Place("The Sunken Senate", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Undercroft", false, 46f, -142f, DungeonScale.Great),
            new Place("Ossuary of Verrin", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Crypt", false, 100f, -140f, DungeonScale.Holding),
            new Place("Tallow Undercroft", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Undercroft", false, -10f, -96f),
            new Place("Greyhold", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Keep", false, 22f, -118f, DungeonScale.Holding),
            new Place("The Debtors' Gaol", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Gaol", false, 58f, -178f, DungeonScale.Holding),
            new Place("Saint Halvory's", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Monastery", false, 84f, -112f, DungeonScale.Holding),
            new Place("Long Barrow of Sarn", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Barrow", false, -30f, -178f),
            new Place("Cadger's Delve", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, 86f, -206f),
            new Place("The Chancel", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Monastery", false, 30f, -196f),
            new Place("Oldgate Barracks", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Barracks", false, -6f, -200f, DungeonScale.Holding),
            new Place("The Weeping Stair", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Waterworks", false, 112f, -152f),
            new Place("Hollybeck Warren", Province.Vareth, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, 60f, -80f),
            new Place("Marrowgate", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Town, null, true, -108f, -40f),
            new Place("Colder", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Town, null, false, -156f, -96f),
            new Place("Ninefords", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Village, null, false, -84f, -96f),
            new Place("Hask", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Village, null, false, -172f, -30f),
            new Place("Ettrin", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Village, null, false, -122f, 8f),
            new Place("Stonewick", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -196f, -68f),
            new Place("Barrow-on-Dol", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -90f, 14f),
            new Place("Quarry End", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -142f, -140f),
            new Place("Thistlemoor", Province.Dolmarch, PlaceKind.Settlement, SettlementTier.Hamlet, null, false, -186f, 10f),
            new Place("Vaskir", Province.Dolmarch, PlaceKind.Ruin, SettlementTier.None, "Keep", false, -130f, -74f),
            new Place("The Choir Vault", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Undercroft", false, -134f, -82f, DungeonScale.Great),
            new Place("Dolmarch Keep", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Keep", false, -114f, -54f, DungeonScale.Holding),
            new Place("The Hollow March", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Barrow", false, -168f, -122f),
            new Place("Ninefords Waterworks", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Waterworks", false, -78f, -110f),
            new Place("The Red Cells", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Gaol", false, -100f, -24f, DungeonScale.Holding),
            new Place("Hask Deep", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, -184f, -20f, DungeonScale.Great),
            new Place("The Silent Cloister", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Monastery", false, -150f, 20f, DungeonScale.Holding),
            new Place("Ettrin Crypt", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Crypt", false, -128f, 22f),
            new Place("Undercroft of Colder", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Undercroft", false, -162f, -88f),
            new Place("Marrowgate Barracks", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Barracks", false, -102f, -48f),
            new Place("The Tally House", Province.Dolmarch, PlaceKind.Dungeon, SettlementTier.None, "Barrow", false, -206f, -104f),
            new Place("Hvarn", Province.Skarrvald, PlaceKind.Settlement, SettlementTier.Port, null, true, 40f, 124f),
            new Place("the Gullmarket", Province.Skarrvald, PlaceKind.Settlement, SettlementTier.Town, null, false, 92f, 116f),
            new Place("Skarr Deep", Province.Skarrvald, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, 66f, 138f),
            new Place("the Cold Chapter", Province.Skarrvald, PlaceKind.Dungeon, SettlementTier.None, "Monastery", false, -148f, 134f),
            new Place("Skellvard", Province.Skarrvald, PlaceKind.Settlement, SettlementTier.Town, null, false, 14f, 148f),
            new Place("the Winter Hall", Province.Skarrvald, PlaceKind.Dungeon, SettlementTier.None, "Sanctum", false, 118f, 152f),
            new Place("Ilsemer", Province.Thessanur, PlaceKind.Settlement, SettlementTier.Town, null, true, 168f, -96f),
            new Place("the Warded Hall", Province.Thessanur, PlaceKind.Settlement, SettlementTier.Town, null, false, 186f, -138f),
            new Place("Thessan Barrow", Province.Thessanur, PlaceKind.Dungeon, SettlementTier.None, "Barrow", false, 158f, -46f),
            new Place("the Long Register", Province.Thessanur, PlaceKind.Dungeon, SettlementTier.None, "Sanctum", false, 200f, -104f),
            new Place("Hold Verrick", Province.Holds, PlaceKind.Settlement, SettlementTier.Town, null, true, 30f, -258f),
            new Place("the Ash Line", Province.Holds, PlaceKind.Settlement, SettlementTier.Village, null, false, -14f, -246f),
            new Place("Verrick Deep", Province.Holds, PlaceKind.Dungeon, SettlementTier.None, "Mine", false, 62f, -272f),
            new Place("the Burnt Hold", Province.Holds, PlaceKind.Dungeon, SettlementTier.None, "Keep", false, -34f, -276f)
        };

        public static readonly Place[] Hubs = Where(place => place.Hub);

        public static Vector2 KmOf(string name)
        {
            foreach (Place place in All)
                if (place.Name == name) return place.Km;

            return Vector2.zero;
        }

        static Place[] Where(System.Func<Place, bool> match)
        {
            List<Place> kept = new List<Place>();

            foreach (Place place in All)
                if (match(place)) kept.Add(place);

            return kept.ToArray();
        }

        public static IEnumerable<Place> In(Province province)
        {
            foreach (Place place in All)
                if (place.Province == province) yield return place;
        }
    }
}
