using UnityEngine;

namespace EndlessDescent.Data
{
    // The pieces a custom spell is built from. Daggerfall prices a spell by its effects and their
    // magnitude and duration, this keeps that shape with a much shorter list
    public enum SpellEffectId
    {
        Damage,
        Heal,
        Shield,
        Speed,
        Light,
        CurePoison
    }

    public static class SpellEffects
    {
        public static string Describe(SpellEffectId effect)
        {
            switch (effect)
            {
                case SpellEffectId.Damage: return "Damage";
                case SpellEffectId.Heal: return "Restore health";
                case SpellEffectId.Shield: return "Shield";
                case SpellEffectId.Speed: return "Haste";
                case SpellEffectId.Light: return "Light";
                default: return "Cure poison";
            }
        }

        public static SkillId School(SpellEffectId effect)
        {
            switch (effect)
            {
                case SpellEffectId.Damage: return SkillId.Destruction;
                case SpellEffectId.Heal:
                case SpellEffectId.CurePoison: return SkillId.Restoration;
                case SpellEffectId.Shield:
                case SpellEffectId.Speed: return SkillId.Alteration;
                default: return SkillId.Illusion;
            }
        }

        // Cost rises faster than power so a huge spell is a real investment, as Daggerfall's does
        public static int Cost(SpellEffectId effect, int magnitude, float duration)
        {
            float baseCost = effect == SpellEffectId.Damage || effect == SpellEffectId.Heal ? 1.1f : 0.8f;
            return Mathf.Max(1, Mathf.RoundToInt(baseCost * magnitude * (1f + duration * 0.15f)));
        }

        public static string CustomId(SpellEffectId effect) => $"spell.custom.{effect}".ToLowerInvariant();

        public static bool IsCustom(string id, out SpellEffectId effect)
        {
            foreach (SpellEffectId candidate in System.Enum.GetValues(typeof(SpellEffectId)))
            {
                if (CustomId(candidate) != id)
                    continue;

                effect = candidate;
                return true;
            }

            effect = default;
            return false;
        }

        // A made spell is effect and magnitude and nothing else, which is also all a save has to keep
        // to make it again
        public static SpellDefinition Custom(SpellEffectId effect, int magnitude)
        {
            SpellDefinition spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.name = $"Custom_{effect}";

            SpellKind kind = effect == SpellEffectId.Damage ? SpellKind.Projectile
                : effect == SpellEffectId.Heal ? SpellKind.Heal
                : SpellKind.Ward;

            spell.Configure(CustomId(effect), $"{Describe(effect)} {magnitude}", kind, School(effect),
                Cost(effect, magnitude, 0f), 1.2f, magnitude, 10f, Tint(effect));

            return spell;
        }

        static Color Tint(SpellEffectId effect)
        {
            switch (effect)
            {
                case SpellEffectId.Damage: return new Color(1f, 0.45f, 0.2f);
                case SpellEffectId.Heal: return new Color(0.4f, 1f, 0.5f);
                default: return new Color(0.5f, 0.65f, 1f);
            }
        }
    }
}
