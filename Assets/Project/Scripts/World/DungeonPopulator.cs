using System.Collections;
using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Dungeons;
using EndlessDescent.Items;
using EndlessDescent.Quests;
using UnityEngine;

namespace EndlessDescent.World
{
    [DisallowMultipleComponent]
    public class DungeonPopulator : MonoBehaviour
    {
        [SerializeField] DungeonGenerator generator;
        [SerializeField] EnemyDefinition[] enemies;
        [SerializeField] EnemyDefinition bossEnemy;
        [SerializeField] LootTableDefinition treasureLoot;
        [SerializeField] GameObject pickupPrefab;
        [SerializeField] GameObject keyPickupPrefab;
        [SerializeField] Vector2Int enemiesPerRoom = new Vector2Int(0, 2);
        [SerializeField] Vector2Int enemiesPerSetPiece = new Vector2Int(3, 6);
        [SerializeField] Vector2Int enemiesPerBossRoom = new Vector2Int(2, 4);
        [SerializeField] float spawnRadius = 2.5f;

        DungeonTypeDefinition type;

        readonly HashSet<int> filled = new HashSet<int>();

        public void Populate()
        {
            for (int level = 0; level < (generator?.Current != null ? generator.Current.LevelCount : 1); level++)
                foreach (object _ in PopulatingLevel(level)) { }
        }

        // Cleared dungeons are rebuilt from scratch, so what was filled before means nothing
        public void Forget() => filled.Clear();

        // The entrance floor, which is all that is needed before the player is let in
        public IEnumerable Populating() => PopulatingLevel(0);

        // A room a step, one level at a time, a great dungeon is several hundred enemies, and filling
        // the lot before anybody has walked down a corridor is the cost nobody had measured
        public IEnumerable PopulatingLevel(int level)
        {
            if (generator?.Current == null || generator.Current.Layout == null || !filled.Add(level))
                yield break;

            type = generator.DungeonType;

            DungeonGraph graph = generator.Graph;
            // Its own stream per floor. Every floor used to start from the same seed, so each one repeated
            // the enemy, trap and treasure pattern of the one above
            DeterministicRandom rng = new DeterministicRandom(graph.Seed ^ 0x5f3759df ^ (level + 1) * 7919);

            foreach (KeyValuePair<int, GameObject> pair in generator.Current.Rooms)
            {
                if (generator.Current.Layout.Level[pair.Key] != level)
                    continue;

                DungeonNode node = graph.Node(pair.Key);
                Transform room = pair.Value.transform;

                switch (node.Role)
                {
                    case RoomRole.Entrance:
                        break;
                    case RoomRole.Boss:
                        WatchForClear(SpawnEnemy(Boss ?? PickEnemy(ref rng), room, Depth(pair.Key), ref rng));
                        SpawnGroup(room, enemiesPerBossRoom, Depth(pair.Key), ref rng);
                        SpawnTreasure(room, Depth(pair.Key), true, ref rng);
                        break;
                    case RoomRole.SetPiece:
                        SpawnTreasure(room, Depth(pair.Key), false, ref rng);
                        SpawnGroup(room, enemiesPerSetPiece, Depth(pair.Key), ref rng);
                        break;
                    case RoomRole.Treasure:
                        SpawnTreasure(room, Depth(pair.Key), false, ref rng);
                        SpawnGroup(room, enemiesPerRoom, Depth(pair.Key), ref rng);
                        break;
                    default:
                        SpawnGroup(room, enemiesPerRoom, Depth(pair.Key), ref rng);
                        break;
                }

                if (node.ContainsKeyId >= 0)
                    SpawnKey(room, node.ContainsKeyId);

                if (node.Role != RoomRole.Entrance)
                {
                    TrySpawnTrap(room, room, false, ref rng);
                    TrySpawnFurnishing(room, ref rng);
                }

                SpawnResident(room, ref rng);
                yield return null;
            }

            // A corridor is stretched to its run, so anything parented to one is stretched
            // with it. Traps hang off the room the corridor belongs to instead
            foreach (GameObject corridor in generator.Current.Corridors)
            {
                Transform owner = corridor.transform.parent != null ? corridor.transform.parent : transform;

                if (owner == transform || Mathf.RoundToInt(
                        -generator.Current.Root.transform.InverseTransformPoint(owner.position).y
                        / EndlessDescent.Dungeons.DungeonGridLayout.Storey) != level)
                    continue;

                TrySpawnTrap(corridor.transform, owner, true, ref rng);
            }

            FindFirstObjectByType<DungeonVisibility>()?.Restate();
        }

        EnemyDefinition Boss => type != null && type.Boss != null ? type.Boss : bossEnemy;

        LootTableDefinition Treasure => type != null && type.Loot != null ? type.Loot : treasureLoot;

        void SpawnGroup(Transform room, Vector2Int range, float depth, ref DeterministicRandom rng)
        {
            int count = rng.NextInt(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);

            for (int i = 0; i < count; i++)
                SpawnEnemy(PickEnemy(ref rng), room, depth, ref rng);
        }

        // A shop gets a shopkeeper, an inn an innkeeper, a house its occupants. Driven off the room's
        // own purpose, so adding a building type is enough to give it people
        void SpawnResident(Transform room, ref DeterministicRandom rng)
        {
            if (type == null)
                return;

            RoomModule module = room.GetComponent<RoomModule>();
            if (module == null)
                return;

            GameObject prefab = type.ResidentFor(module.Purpose);
            if (prefab == null)
                return;

            Instantiate(prefab, room.position + Scatter(ref rng), Quaternion.identity, room);
        }

        void TrySpawnFurnishing(Transform room, ref DeterministicRandom rng)
        {
            if (type == null || type.Furnishings == null || type.Furnishings.Length == 0)
                return;

            if (!rng.Chance(type.FurnishingChance))
                return;

            GameObject prefab = type.Furnishings[rng.NextInt(0, type.Furnishings.Length)];
            if (prefab == null)
                return;

            Instantiate(prefab, room.position, room.rotation, room);
        }

        // A corridor is 4 m wide. Down the middle a trap left a body's width either side; to one side of
        // it there is a clear lane past
        const float CorridorTrapOffset = 0.9f;

        void TrySpawnTrap(Transform at, Transform owner, bool inCorridor, ref DeterministicRandom rng)
        {
            if (type == null || type.Traps == null || type.Traps.Length == 0)
                return;

            if (!rng.Chance(type.TrapChance))
                return;

            GameObject prefab = type.Traps[rng.NextInt(0, type.Traps.Length)];
            if (prefab == null)
                return;

            Vector3 position = at.position;

            if (inCorridor)
                position += at.right * (rng.Chance(0.5f) ? CorridorTrapOffset : -CorridorTrapOffset);

            Instantiate(prefab, position, at.rotation, owner);
        }

        GameObject SpawnEnemy(EnemyDefinition definition, Transform room, float depth, ref DeterministicRandom rng)
        {
            if (definition?.Prefab == null)
                return null;

            GameObject enemy = Instantiate(definition.Prefab, room.position + Scatter(ref rng), Quaternion.identity, room);
            enemy.GetComponent<LootDropper>()?.Prepare((int)rng.NextUInt(), depth);
            return enemy;
        }

        // A thousand room dungeon is never cleared room by room, so the boss dying is what "cleared"
        // means. It is also the only thing a quest could reasonably ask for
        void WatchForClear(GameObject boss)
        {
            EndlessDescent.Combat.Health health = boss != null ? boss.GetComponent<EndlessDescent.Combat.Health>() : null;

            if (health == null)
                return;

            QuestJournal journal = FindFirstObjectByType<QuestJournal>();

            if (journal == null)
                return;

            string place = PlaceName();
            health.Died += () => journal.ReportDungeonCleared(place);
        }

        static string PlaceName()
        {
            PlaceLoader place = FindFirstObjectByType<PlaceLoader>();

            if (place == null || place.Region == null)
                return null;

            int index = place.PreparedDungeon >= 0 ? place.PreparedDungeon : place.Current;
            return place.Region.Has(index) ? place.Region.Locations[index].DisplayName : null;
        }

        // How far down a room is, as a fraction of the dungeon, what the loot tables read
        float Depth(int roomId)
        {
            DungeonGridLayout layout = generator.Current?.Layout;

            if (layout == null || layout.LevelCount <= 1 || roomId < 0 || roomId >= layout.Level.Length)
                return 1f;

            return layout.Level[roomId] / (float)(layout.LevelCount - 1);
        }

        void SpawnTreasure(Transform room, float depth, bool boss, ref DeterministicRandom rng)
        {
            LootTableDefinition table = boss && type != null ? type.BossLoot : Treasure;

            if (table == null || pickupPrefab == null)
                return;

            LootResult loot = LootRoller.Roll(table, ref rng, depth);

            foreach (ItemInstance stack in loot.Items)
            {
                GameObject spawned = Instantiate(pickupPrefab, room.position + Scatter(ref rng), Quaternion.identity, room);
                spawned.GetComponent<ItemPickup>()?.Set(stack);
            }
        }

        void SpawnKey(Transform room, int keyId)
        {
            if (keyPickupPrefab == null)
                return;

            GameObject spawned = Instantiate(keyPickupPrefab, room.position + Vector3.up * 0.5f, Quaternion.identity, room);
            spawned.GetComponent<DungeonKeyPickup>()?.Bind(generator, keyId);
        }

        EnemyDefinition PickEnemy(ref DeterministicRandom rng)
        {
            EnemyDefinition[] roster = type != null && type.Enemies != null && type.Enemies.Length > 0
                ? type.Enemies
                : enemies;

            return roster == null || roster.Length == 0 ? null : roster[rng.NextInt(roster.Length)];
        }

        Vector3 Scatter(ref DeterministicRandom rng)
        {
            return new Vector3(rng.NextFloat() - 0.5f, 0f, rng.NextFloat() - 0.5f) * spawnRadius;
        }
    }
}
