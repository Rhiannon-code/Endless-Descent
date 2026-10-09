using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using UnityEngine;

namespace EndlessDescent.World
{
    // Binds an effect to the weapon in your hand. Only equipment carries an enchantment, which is
    // exactly why items became instances rather than stacks of definitions
    [DisallowMultipleComponent]
    public class EnchantingTable : MonoBehaviour, IInteractable
    {
        [SerializeField] GearEffectId[] offers =
        {
            GearEffectId.Sharpness, GearEffectId.Ember, GearEffectId.Leeching,
            GearEffectId.Warding, GearEffectId.Swiftness, GearEffectId.Might
        };
        [SerializeField, Min(1)] int goldPerPoint = 8;

        public string Prompt => "Enchant held weapon";

        public bool CanInteract(GameObject actor) => actor.GetComponentInParent<Equipment>() != null;

        public void Interact(GameObject actor) =>
            ServiceScreens.Current?.Open("Enchanting table", () => Options(actor));

        IReadOnlyList<ServiceOption> Options(GameObject actor)
        {
            Equipment equipment = actor.GetComponentInParent<Equipment>();
            CharacterSheet sheet = actor.GetComponentInParent<CharacterSheet>();
            Wallet wallet = actor.GetComponentInParent<Wallet>();

            ItemInstance held = equipment?.WornInstance(EquipSlot.MainHand);
            List<ServiceOption> options = new List<ServiceOption>();

            if (held == null)
            {
                options.Add(new ServiceOption { Text = "Nothing in your main hand to enchant", Available = false });
                return options;
            }

            int power = Power(sheet);
            int gold = power * goldPerPoint;

            // Only what the held thing can carry is offered at all
            foreach (GearEffectId effect in offers)
            {
                if (!GearEffects.Suits(effect, held.Item.Category))
                    continue;

                GearEffectId captured = effect;

                options.Add(new ServiceOption
                {
                    Text = $"{GearEffects.Describe(effect)} {power} on {held.DisplayName}   {gold} gold",
                    Available = wallet != null && wallet.CanAfford(gold),
                    Choose = () => Enchant(captured, equipment, sheet, wallet)
                });
            }

            return options;
        }

        int Power(CharacterSheet sheet) => 8 + (sheet != null ? sheet.Skill(SkillId.Mysticism) / 4 : 0);

        string Enchant(GearEffectId effect, Equipment equipment, CharacterSheet sheet, Wallet wallet)
        {
            ItemInstance held = equipment?.WornInstance(EquipSlot.MainHand);
            int power = Power(sheet);
            int gold = power * goldPerPoint;

            if (held == null)
                return "Nothing in your main hand to enchant.";

            if (wallet == null || !wallet.TrySpend(gold))
                return $"That costs {gold} gold.";

            held.Enchant(effect, power);
            sheet?.Use(SkillId.Mysticism, 5f);
            equipment.NotifyChanged();

            return $"Enchanted {held.DisplayName} for {gold} gold.";
        }
    }
}
