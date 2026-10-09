namespace EndlessDescent.Data
{
    public enum WeaponClass { Blade, Thrust, Axe, Blunt, Bow, Unarmed }

    // What separates an axe from a mace once both have a damage number. Derived from the weapon's
    // own skill rather than stored beside it, two fields holding the same fact drift apart, and
    // every weapon already carries its skill
    public static class WeaponClasses
    {
        public static WeaponClass Of(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.ShortBlade: return WeaponClass.Thrust;
                case SkillId.Axe: return WeaponClass.Axe;
                case SkillId.BluntWeapon: return WeaponClass.Blunt;
                case SkillId.Archery: return WeaponClass.Bow;
                case SkillId.HandToHand: return WeaponClass.Unarmed;
                default: return WeaponClass.Blade;
            }
        }

        public static WeaponClass Of(WeaponDefinition weapon) =>
            weapon == null ? WeaponClass.Unarmed : Of(weapon.Skill);

        // The share of the target's armour this class goes straight through. An axe splits mail, a
        // sword slides off it
        public static float Penetration(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust: return 0.25f;
                case WeaponClass.Axe: return 0.45f;
                case WeaponClass.Blunt: return 0.30f;
                case WeaponClass.Bow: return 0.15f;
                case WeaponClass.Unarmed: return 0f;
                default: return 0.10f;
            }
        }

        // What it does to footing. A hammer does not have to cut to put someone on the floor
        public static float PoiseScale(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust: return 0.7f;
                case WeaponClass.Axe: return 1.15f;
                case WeaponClass.Blunt: return 1.6f;
                case WeaponClass.Bow: return 0.6f;
                case WeaponClass.Unarmed: return 0.8f;
                default: return 1f;
            }
        }

        // How wide the swing is, a dagger reaches one throat, a greataxe catches what stands beside it
        public static float ArcScale(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust: return 0.6f;
                case WeaponClass.Axe: return 1.25f;
                case WeaponClass.Blunt: return 1.1f;
                case WeaponClass.Bow: return 0.5f;
                case WeaponClass.Unarmed: return 0.8f;
                default: return 1f;
            }
        }

        // What a strike on something that has not noticed you is worth in this class's hands
        public static float SneakScale(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust: return 1.6f;
                case WeaponClass.Axe: return 0.9f;
                case WeaponClass.Blunt: return 0.8f;
                case WeaponClass.Bow: return 1.4f;
                case WeaponClass.Unarmed: return 1.2f;
                default: return 1f;
            }
        }

        public static int ArmourAfter(WeaponClass weapon, int armour) =>
            UnityEngine.Mathf.RoundToInt(armour * (1f - Penetration(weapon)));

        public static string Describe(WeaponClass weapon)
        {
            switch (weapon)
            {
                case WeaponClass.Thrust: return "Fast, narrow, and worth most from behind";
                case WeaponClass.Axe: return "Splits armour, wide swing";
                case WeaponClass.Blunt: return "Takes their footing away";
                case WeaponClass.Bow: return "Reaches what you have not closed with";
                case WeaponClass.Unarmed: return "Nothing but you";
                default: return "Even in every direction";
            }
        }
    }
}
