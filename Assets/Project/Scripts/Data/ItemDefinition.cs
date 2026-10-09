using UnityEngine;

namespace EndlessDescent.Data
{
    public enum ItemCategory { Misc, Weapon, Armour, Consumable, Ingredient, Key, Valuable }

    public enum EquipSlot { None, MainHand, OffHand, Head, Chest, Legs, Hands, Feet }

    [CreateAssetMenu(menuName = "Endless Descent/Items/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] string id = "item.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField, TextArea] string description;
        [SerializeField] ItemCategory category = ItemCategory.Misc;
        [SerializeField, Min(0f)] float weight = 1f;
        [SerializeField, Min(0)] int baseValue = 1;
        [SerializeField, Min(1)] int maxStack = 1;
        [SerializeField] Sprite icon;
        [SerializeField] GameObject worldPrefab;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public float Weight => weight;
        public int BaseValue => baseValue;
        public int MaxStack => Mathf.Max(1, maxStack);
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;

        public virtual EquipSlot Slot => EquipSlot.None;
    }
}
