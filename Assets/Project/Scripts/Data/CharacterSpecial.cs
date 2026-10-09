namespace EndlessDescent.Data
{
    // Daggerfall's advantages and disadvantages. Each carries a point cost, a class must balance its
    // advantages against its disadvantages, and the total shifts how much experience a level costs
    public enum CharacterSpecial
    {
        None,

        // Advantages (positive cost)
        BonusToHitUndead,
        BonusToHitAnimals,
        ExpertiseLongsword,
        ExpertiseShortBlade,
        ImmunityToParalysis,
        ImmunityToPoison,
        ResistanceToFire,
        ResistanceToFrost,
        IncreasedMagery,
        RapidHealing,
        RegenerateHealth,
        SpellAbsorption,
        AcuteHearing,
        AthleticismBonus,

        // Disadvantages (negative cost)
        ForbiddenArmourPlate,
        ForbiddenShield,
        ForbiddenWeaponryMissile,
        DamageFromSunlight,
        DamageFromHolyPlaces,
        CriticalWeaknessToFire,
        CriticalWeaknessToFrost,
        LowToleranceToPoison,
        InabilityToRegenerate,
        PhobiaOfUndead
    }

    public static class Specials
    {
        public static bool IsAdvantage(CharacterSpecial special) => Cost(special) > 0;

        public static int Cost(CharacterSpecial special)
        {
            switch (special)
            {
                case CharacterSpecial.IncreasedMagery: return 10;
                case CharacterSpecial.SpellAbsorption: return 10;
                case CharacterSpecial.ImmunityToParalysis: return 8;
                case CharacterSpecial.RegenerateHealth: return 8;
                case CharacterSpecial.ImmunityToPoison: return 6;
                case CharacterSpecial.ResistanceToFire: return 5;
                case CharacterSpecial.ResistanceToFrost: return 5;
                case CharacterSpecial.RapidHealing: return 5;
                case CharacterSpecial.ExpertiseLongsword: return 4;
                case CharacterSpecial.ExpertiseShortBlade: return 4;
                case CharacterSpecial.BonusToHitUndead: return 4;
                case CharacterSpecial.BonusToHitAnimals: return 3;
                case CharacterSpecial.AthleticismBonus: return 3;
                case CharacterSpecial.AcuteHearing: return 2;

                case CharacterSpecial.ForbiddenArmourPlate: return -6;
                case CharacterSpecial.ForbiddenWeaponryMissile: return -5;
                case CharacterSpecial.CriticalWeaknessToFire: return -6;
                case CharacterSpecial.CriticalWeaknessToFrost: return -6;
                case CharacterSpecial.InabilityToRegenerate: return -6;
                case CharacterSpecial.DamageFromSunlight: return -5;
                case CharacterSpecial.DamageFromHolyPlaces: return -4;
                case CharacterSpecial.ForbiddenShield: return -4;
                case CharacterSpecial.LowToleranceToPoison: return -3;
                case CharacterSpecial.PhobiaOfUndead: return -3;

                default: return 0;
            }
        }

        public static string Describe(CharacterSpecial special)
        {
            switch (special)
            {
                case CharacterSpecial.BonusToHitUndead: return "Bonus to hit: Undead";
                case CharacterSpecial.BonusToHitAnimals: return "Bonus to hit: Animals";
                case CharacterSpecial.ExpertiseLongsword: return "Expertise: Longsword";
                case CharacterSpecial.ExpertiseShortBlade: return "Expertise: Short Blade";
                case CharacterSpecial.ImmunityToParalysis: return "Immunity: Paralysis";
                case CharacterSpecial.ImmunityToPoison: return "Immunity: Poison";
                case CharacterSpecial.ResistanceToFire: return "Resistance: Fire";
                case CharacterSpecial.ResistanceToFrost: return "Resistance: Frost";
                case CharacterSpecial.IncreasedMagery: return "Increased magery";
                case CharacterSpecial.RapidHealing: return "Rapid healing";
                case CharacterSpecial.RegenerateHealth: return "Regenerate health";
                case CharacterSpecial.SpellAbsorption: return "Spell absorption";
                case CharacterSpecial.AcuteHearing: return "Acute hearing";
                case CharacterSpecial.AthleticismBonus: return "Athleticism";
                case CharacterSpecial.ForbiddenArmourPlate: return "Forbidden armour: Plate";
                case CharacterSpecial.ForbiddenShield: return "Forbidden: Shield";
                case CharacterSpecial.ForbiddenWeaponryMissile: return "Forbidden weaponry: Missile";
                case CharacterSpecial.DamageFromSunlight: return "Damage: Sunlight";
                case CharacterSpecial.DamageFromHolyPlaces: return "Damage: Holy places";
                case CharacterSpecial.CriticalWeaknessToFire: return "Critical weakness: Fire";
                case CharacterSpecial.CriticalWeaknessToFrost: return "Critical weakness: Frost";
                case CharacterSpecial.LowToleranceToPoison: return "Low tolerance: Poison";
                case CharacterSpecial.InabilityToRegenerate: return "Inability to regenerate";
                case CharacterSpecial.PhobiaOfUndead: return "Phobia: Undead";
                default: return special.ToString();
            }
        }
    }
}
