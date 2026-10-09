using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Items
{
    [DisallowMultipleComponent]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDefinition item;
        [SerializeField, Min(1)] int count = 1;

        public ItemDefinition Item => item;
        public int Count => count;

        [SerializeField] Transform shape;

        // What a drop actually is, wear and enchantment included. Handing over only the definition and
        // a count gave every find back pristine and plain
        ItemInstance carried;

        string Name => carried != null ? carried.DisplayName : item.DisplayName;

        public string Prompt => item == null ? "Pick up" : count > 1 ? $"Take {Name} x{count}" : $"Take {Name}";

        void Awake()
        {
            if (shape == null && transform.childCount > 0)
                shape = transform.GetChild(0);

            Dress();
        }

        public void Set(ItemInstance instance)
        {
            carried = instance;
            Set(instance.Item, instance.Count);
        }

        public void Set(ItemDefinition newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
            Dress();
        }

        // Everything on the floor used to be the same box, so the only way to find out what you had
        // walked past was to walk onto it. Shape says what kind of thing it is, colour says how good
        void Dress()
        {
            if (shape == null || item == null)
                return;

            shape.localScale = SizeOf(item.Category);

            // A property block, not renderer.material, which makes a copy of the material for every
            // pickup that nothing ever destroys
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColour, Tier(item.Category, item.BaseValue));

            foreach (Renderer renderer in shape.GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(block);
        }

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        static Vector3 SizeOf(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon: return new Vector3(0.10f, 0.10f, 0.85f);
                case ItemCategory.Armour: return new Vector3(0.45f, 0.40f, 0.22f);
                case ItemCategory.Consumable: return new Vector3(0.18f, 0.34f, 0.18f);
                case ItemCategory.Ingredient: return new Vector3(0.16f, 0.16f, 0.16f);
                case ItemCategory.Key: return new Vector3(0.09f, 0.26f, 0.09f);
                case ItemCategory.Valuable: return new Vector3(0.24f, 0.24f, 0.24f);
                default: return new Vector3(0.28f, 0.22f, 0.28f);
            }
        }

        static Color Tier(ItemCategory category, int value)
        {
            switch (category)
            {
                case ItemCategory.Consumable: return new Color(0.42f, 0.72f, 0.48f);
                case ItemCategory.Ingredient: return new Color(0.46f, 0.54f, 0.34f);
                case ItemCategory.Key: return new Color(0.88f, 0.76f, 0.32f);
                case ItemCategory.Valuable: return new Color(0.92f, 0.84f, 0.46f);
            }

            if (value >= 300) return new Color(0.72f, 0.62f, 0.86f);
            if (value >= 140) return new Color(0.78f, 0.80f, 0.84f);
            if (value >= 60) return new Color(0.58f, 0.60f, 0.62f);
            return new Color(0.52f, 0.42f, 0.30f);
        }

        public bool CanInteract(GameObject actor)
        {
            return item != null && actor != null && actor.GetComponentInChildren<Inventory>() != null;
        }

        public void Interact(GameObject actor)
        {
            Inventory inventory = actor.GetComponentInChildren<Inventory>();
            if (inventory == null)
                return;

            if (carried != null && carried.HasCondition)
            {
                if (inventory.AddInstance(carried))
                    Destroy(gameObject);

                return;
            }

            int leftover = inventory.Add(item, count);

            if (leftover <= 0)
                Destroy(gameObject);
            else
                count = leftover;
        }
    }
}
