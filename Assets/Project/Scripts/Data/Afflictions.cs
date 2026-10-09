using EndlessDescent.Core;

namespace EndlessDescent.Data
{
    public static class Afflictions
    {
        public static string Describe(AfflictionId affliction)
        {
            switch (affliction)
            {
                case AfflictionId.Poison: return "Poisoned";
                case AfflictionId.SwampFever: return "Swamp Fever";
                case AfflictionId.Plague: return "Plague";
                case AfflictionId.Witheringrot: return "Witheringrot";
                case AfflictionId.Vampirism: return "Vampirism";
                default: return string.Empty;
            }
        }

        // Poison burns out on its own, diseases do not, which is what makes a cure worth paying for
        public static bool IsDisease(AfflictionId affliction) =>
            affliction == AfflictionId.SwampFever || affliction == AfflictionId.Plague ||
            affliction == AfflictionId.Witheringrot;

        public static float HoursToOnset(AfflictionId affliction) =>
            affliction == AfflictionId.Poison ? 0f : 6f;

        public static int DamagePerHour(AfflictionId affliction)
        {
            switch (affliction)
            {
                case AfflictionId.Poison: return 4;
                case AfflictionId.SwampFever: return 2;
                case AfflictionId.Plague: return 5;
                case AfflictionId.Witheringrot: return 3;
                default: return 0;
            }
        }

        // Which attribute the illness eats away at while it runs
        public static CharacterAttribute Drains(AfflictionId affliction)
        {
            switch (affliction)
            {
                case AfflictionId.SwampFever: return CharacterAttribute.Endurance;
                case AfflictionId.Plague: return CharacterAttribute.Strength;
                case AfflictionId.Witheringrot: return CharacterAttribute.Agility;
                default: return CharacterAttribute.Endurance;
            }
        }

        public static float HoursToBurnOut(AfflictionId affliction) =>
            affliction == AfflictionId.Poison ? 3f : 0f;
    }
}
