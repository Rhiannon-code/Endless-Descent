using UnityEngine;

namespace EndlessDescent.Data
{
    // Hybrid combat, a swing that connects always lands. Skill decides how hard it lands, whether it
    // staggers, whether it crits, and what it costs in stamina. An aimed swing that misses because of
    // a dice roll reads as broken input in a game where you aim it yourself
    public static class CharacterMaths
    {
        public const float CriticalDamage = 1.6f;
        public const float CriticalPoise = 2f;
        public const float MaxCriticalChance = 0.35f;

        public static int MaxHealth(int level, int endurance, int hitPointsPerLevel, int baseHealth) =>
            Mathf.Max(1, baseHealth + level * (hitPointsPerLevel + endurance / 10));

        public static int MaxMagicka(int intelligence, int willpower, float magery) =>
            Mathf.RoundToInt(intelligence * magery) + willpower / 2;

        public static float MaxStamina(int strength, int endurance) => strength + endurance;

        public static float CarryWeight(int strength) => strength * 1.5f;

        // A novice swings a weapon for about two thirds of what it is worth; a master for a third more
        // than it is worth. The governing attribute decides how much of that is reach and how much is arm
        public static float WeaponPower(ICharacterStats stats, SkillId skill)
        {
            if (stats == null)
                return 1f;

            float trained = 0.55f + stats.Skill(skill) / 160f;
            float able = 0.8f + stats.Attribute(Skills.Governing(skill)) / 250f;
            return trained * able;
        }

        public static int MeleeDamage(ICharacterStats stats, SkillId skill, int weaponDamage) =>
            Mathf.Max(1, Mathf.RoundToInt(weaponDamage * WeaponPower(stats, skill)));

        public static float CriticalChance(ICharacterStats stats, SkillId skill)
        {
            if (stats == null)
                return 0f;

            float raw = stats.Skill(skill) + stats.Skill(SkillId.CriticalStrike)
                        + stats.Attribute(CharacterAttribute.Luck) * 0.5f - 40f;

            return Mathf.Clamp(raw / 400f, 0f, MaxCriticalChance);
        }

        // Fighting with something you barely know how to hold is what empties a stamina bar
        public static float StaminaCost(ICharacterStats stats, SkillId skill, float baseCost)
        {
            if (stats == null)
                return baseCost;

            float ease = 1.35f - stats.Skill(skill) / 200f - stats.Attribute(CharacterAttribute.Endurance) / 500f;
            return baseCost * Mathf.Clamp(ease, 0.75f, 1.5f);
        }

        public static float BlockFraction(ICharacterStats stats, float baseFraction) =>
            stats == null ? baseFraction : Mathf.Clamp01(baseFraction + stats.Skill(SkillId.Block) / 300f);

        public static int Armour(ICharacterStats stats, int worn) =>
            worn + (stats == null ? 0 : stats.Attribute(CharacterAttribute.Endurance) / 20);

        public static float MoveSpeed(ICharacterStats stats, float baseSpeed) =>
            stats == null ? baseSpeed : baseSpeed * (0.88f + stats.Attribute(CharacterAttribute.Speed) / 420f);

        public static float SprintSpeed(ICharacterStats stats, float baseSpeed) =>
            stats == null ? baseSpeed : MoveSpeed(stats, baseSpeed) * (1f + stats.Skill(SkillId.Running) / 500f);

        public static int SpellCost(ICharacterStats stats, SkillId school, int baseCost) =>
            stats == null
                ? baseCost
                : Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Clamp(1.3f - stats.Skill(school) / 200f, 0.7f, 1.3f)));

        public static int SpellPower(ICharacterStats stats, SkillId school, int basePower) =>
            stats == null ? basePower : Mathf.Max(1, Mathf.RoundToInt(basePower * (0.7f + stats.Skill(school) / 150f)));

        // What the asking price does when the person in front of the merchant can argue
        public static float PriceScale(ICharacterStats stats, bool buying)
        {
            if (stats == null)
                return 1f;

            float haggle = stats.Skill(SkillId.Mercantile) / 400f
                           + stats.Attribute(CharacterAttribute.Personality) / 600f;

            return buying ? Mathf.Clamp(1f - haggle, 0.6f, 1f) : Mathf.Clamp(1f + haggle, 1f, 1.4f);
        }
    }
}
