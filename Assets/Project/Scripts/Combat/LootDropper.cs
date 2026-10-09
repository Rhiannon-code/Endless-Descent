using EndlessDescent.Core;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class LootDropper : MonoBehaviour
    {
        [SerializeField] EnemyBrain brain;
        [SerializeField] Health health;
        [SerializeField] GameObject pickupPrefab;
        [SerializeField] float scatterRadius = 0.6f;

        GameObject lastAttacker;

        // Given by whoever spawned this, from the dungeon's own seed and how deep the room is. The seed
        // used to be hashed from the position, which overflows an int past about 30 m from the origin
        int seed;
        float depth = 1f;
        bool prepared;

        public void Prepare(int lootSeed, float lootDepth)
        {
            seed = lootSeed;
            depth = lootDepth;
            prepared = true;
        }

        void Awake()
        {
            if (health == null)
                health = GetComponent<Health>();

            health.Damaged += OnDamaged;
            health.Died += DropLoot;
        }

        void OnDestroy()
        {
            if (health == null)
                return;

            health.Damaged -= OnDamaged;
            health.Died -= DropLoot;
        }

        void OnDamaged(DamageInfo info)
        {
            if (info.Source != null)
                lastAttacker = info.Source;
        }

        void DropLoot()
        {
            if (brain == null || brain.Definition == null || brain.Definition.Loot == null)
                return;

            DeterministicRandom rng = new DeterministicRandom(prepared ? seed : Random.Range(int.MinValue, int.MaxValue));
            LootResult loot = LootRoller.Roll(brain.Definition.Loot, ref rng, depth);

            if (loot.Gold > 0 && lastAttacker != null)
                lastAttacker.GetComponentInChildren<Wallet>()?.Add(loot.Gold);

            if (pickupPrefab == null)
                return;

            foreach (ItemInstance stack in loot.Items)
            {
                Vector3 offset = new Vector3(rng.NextFloat() - 0.5f, 0f, rng.NextFloat() - 0.5f) * scatterRadius;
                GameObject spawned = Instantiate(pickupPrefab, transform.position + offset, Quaternion.identity);
                spawned.GetComponent<ItemPickup>()?.Set(stack);
            }
        }
    }
}
