using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Player;
using UnityEngine;

namespace EndlessDescent.World
{
    // Builds a spell from one effect at the magnitude your skill in its school allows, and teaches it
    [DisallowMultipleComponent]
    public class Spellmaker : MonoBehaviour, IInteractable
    {
        [SerializeField] SpellEffectId[] offers =
        {
            SpellEffectId.Damage, SpellEffectId.Heal, SpellEffectId.Shield
        };

        [SerializeField, Min(1)] int magnitudeStep = 15;
        [SerializeField, Min(1)] int goldPerPoint = 3;

        public string Prompt => "Make a spell";

        public bool CanInteract(GameObject actor) => actor.GetComponentInParent<Spellcasting>() != null;

        public void Interact(GameObject actor) =>
            ServiceScreens.Current?.Open("Spellmaker", () => Options(actor));

        IReadOnlyList<ServiceOption> Options(GameObject actor)
        {
            Spellcasting casting = actor.GetComponentInParent<Spellcasting>();
            CharacterSheet sheet = actor.GetComponentInParent<CharacterSheet>();
            Wallet wallet = actor.GetComponentInParent<Wallet>();

            List<ServiceOption> options = new List<ServiceOption>();

            foreach (SpellEffectId effect in offers)
            {
                SpellEffectId captured = effect;
                int magnitude = Magnitude(effect, sheet);
                int gold = Price(effect, magnitude);
                bool known = Knows(casting, effect, magnitude);

                options.Add(new ServiceOption
                {
                    Text = $"{SpellEffects.Describe(effect)} {magnitude}   {gold} gold" + (known ? "   (known)" : string.Empty),
                    Available = !known && wallet != null && wallet.CanAfford(gold),
                    Choose = () => Make(captured, casting, sheet, wallet)
                });
            }

            return options;
        }

        int Magnitude(SpellEffectId effect, CharacterSheet sheet) =>
            magnitudeStep + (sheet != null ? sheet.Skill(SpellEffects.School(effect)) / 2 : 0);

        int Price(SpellEffectId effect, int magnitude) => SpellEffects.Cost(effect, magnitude, 0f) * goldPerPoint;

        // Holding this working at this strength already, making it again would buy nothing
        static bool Knows(Spellcasting casting, SpellEffectId effect, int magnitude)
        {
            if (casting == null)
                return false;

            foreach (SpellDefinition spell in casting.Known)
                if (spell.Id == SpellEffects.CustomId(effect) && spell.Power >= magnitude) return true;

            return false;
        }

        string Make(SpellEffectId effect, Spellcasting casting, CharacterSheet sheet, Wallet wallet)
        {
            int magnitude = Magnitude(effect, sheet);
            int gold = Price(effect, magnitude);

            if (casting == null || wallet == null || !wallet.TrySpend(gold))
                return $"That costs {gold} gold.";

            SpellDefinition spell = SpellEffects.Custom(effect, magnitude);

            if (!casting.Learn(spell))
            {
                string refused = $"{spell.DisplayName} is not yours to learn. Your gold is returned.";
                wallet.Add(gold);
                Destroy(spell);
                return refused;
            }

            sheet?.Use(SpellEffects.School(effect), 4f);
            return $"Made and learned {spell.DisplayName} for {gold} gold.";
        }
    }
}
