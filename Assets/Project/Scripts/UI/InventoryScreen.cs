using System;
using System.Collections.Generic;
using EndlessDescent.Combat;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using EndlessDescent.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Laid out the way Daggerfall's is, what you are wearing on the left, what you are carrying on
    // the right under category tabs, with gold and encumbrance along the bottom. Rows are generated
    // rather than authored, so the scene builder only has to place one panel
    [DisallowMultipleComponent]
    public class InventoryScreen : MonoBehaviour, IMenuPage
    {
        [SerializeField] Inventory inventory;
        [SerializeField] Equipment equipment;
        [SerializeField] Wallet wallet;
        [SerializeField] CharacterSheet sheet;
        [SerializeField] SurvivalNeeds needs;
        [SerializeField] Health health;
        [SerializeField] RectTransform root;

        static readonly EquipSlot[] Slots =
        {
            EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head,
            EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Hands, EquipSlot.Feet
        };

        static readonly (string label, ItemCategory[] categories)[] Tabs =
        {
            ("Weapons & Armour", new[] { ItemCategory.Weapon, ItemCategory.Armour }),
            ("Consumables", new[] { ItemCategory.Consumable, ItemCategory.Ingredient }),
            ("Misc", new[] { ItemCategory.Misc, ItemCategory.Key, ItemCategory.Valuable })
        };

        readonly List<GameObject> rows = new List<GameObject>();
        readonly Dictionary<EquipSlot, Text> slotLabels = new Dictionary<EquipSlot, Text>();

        RectTransform carriedColumn;
        Text footer;
        Text header;
        int tab;

        void Awake() => Build();

        void OnEnable()
        {
            if (inventory != null) inventory.Changed += Rebuild;
            if (equipment != null) equipment.Changed += Rebuild;
            if (wallet != null) wallet.Changed += OnGold;
            Rebuild();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Rebuild;
            if (equipment != null) equipment.Changed -= Rebuild;
            if (wallet != null) wallet.Changed -= OnGold;
        }

        void OnGold(int amount) => Rebuild();

        public string Title => "Inventory";
        public RectTransform Root => root;

        public void Refresh() => Rebuild();

        void Build()
        {
            header = MenuWidgets.Label(root, new Vector2(20f, -14f), 700f, string.Empty);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            MenuWidgets.Label(root, new Vector2(20f, -44f), 300f, "WORN").color = new Color(0.8f, 0.8f, 0.9f);

            float y = -68f;
            foreach (EquipSlot slot in Slots)
            {
                EquipSlot captured = slot;
                Text label = MenuWidgets.Button(root, new Vector2(20f, y), new Vector2(300f, 22f), () => Unequip(captured));
                slotLabels[slot] = label;
                y -= 26f;
            }

            for (int i = 0; i < Tabs.Length; i++)
            {
                int captured = i;
                Text label = MenuWidgets.Button(root, new Vector2(340f + i * 150f, -44f), new Vector2(146f, 22f),
                    () => { tab = captured; Rebuild(); });
                label.text = Tabs[i].label;
            }

            carriedColumn = MenuWidgets.ScrollPanel(root, "Carried", new Vector2(340f, -72f), new Vector2(450f, 380f));

            footer = MenuWidgets.Label(root, new Vector2(20f, -470f), 760f, string.Empty);
        }

        void Rebuild()
        {
            if (carriedColumn == null)
                return;

            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();

            foreach (EquipSlot slot in Slots)
            {
                ItemInstance worn = equipment != null ? equipment.WornInstance(slot) : null;
                string condition = worn != null && worn.HasCondition ? $"  [{worn.ConditionLabel}]" : string.Empty;

                slotLabels[slot].text = $"{slot,-10} {(worn?.Item != null ? worn.DisplayName : "-")}{condition}";
                slotLabels[slot].color = worn?.Item == null ? new Color(0.55f, 0.55f, 0.55f)
                    : worn.IsBroken ? new Color(1f, 0.5f, 0.45f) : Color.white;
            }

            float y = 0f;

            if (inventory != null)
            {
                foreach (ItemInstance stack in inventory.Stacks)
                {
                    if (stack.Item == null || !InTab(stack.Item.Category))
                        continue;

                    ItemInstance captured = stack;
                    Text label = MenuWidgets.Button(carriedColumn, new Vector2(0f, y), new Vector2(440f, 22f), () => Use(captured));

                    string count = stack.Count > 1 ? $" x{stack.Count}" : string.Empty;
                    string condition = stack.HasCondition ? $"  [{stack.ConditionLabel}]" : string.Empty;

                    label.text = $"{captured.DisplayName}{count}{condition}".PadRight(34) +
                                 $"{captured.Weight:F1} kg   {stack.Value} g";

                    if (stack.IsBroken)
                        label.color = new Color(1f, 0.5f, 0.45f);

                    rows.Add(label.transform.parent.gameObject);
                    y -= 24f;
                }
            }

            MenuWidgets.Fit(carriedColumn, -y);

            string className = sheet?.Class != null ? sheet.Class.DisplayName : "Unclassed";
            header.text = sheet != null ? $"{className}   Level {sheet.Level}" : "Inventory";

            float carried = inventory != null ? inventory.CarriedWeight : 0f;
            float limit = inventory != null ? inventory.CarryLimit : 0f;
            string over = inventory != null && inventory.IsOverloaded ? "   OVERLOADED" : string.Empty;

            footer.text = $"Gold {(wallet != null ? wallet.Gold : 0)}      " +
                          $"Encumbrance {carried:F1} / {limit:F0} kg{over}";
        }

        bool InTab(ItemCategory category)
        {
            foreach (ItemCategory allowed in Tabs[tab].categories)
                if (allowed == category) return true;

            return false;
        }

        void Unequip(EquipSlot slot)
        {
            equipment?.Unequip(slot);
            Rebuild();
        }

        void Use(ItemInstance instance)
        {
            ItemDefinition item = instance?.Item;
            if (item == null)
                return;

            if (item is ConsumableDefinition consumable)
            {
                if (!inventory.Remove(consumable, 1))
                    return;

                needs?.Consume(consumable);
                health?.Heal(consumable.RestoreHealth);
                Rebuild();
                return;
            }

            if (item.Slot != EquipSlot.None)
                equipment?.Equip(instance);

            Rebuild();
        }
    }
}
