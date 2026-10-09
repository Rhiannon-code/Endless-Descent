namespace EndlessDescent.Data
{
    // What a piece of gear can carry beyond its own numbers. One effect per item, bound either by
    // the enchanting table or by whatever left it in a dungeon
    public enum GearEffectId
    {
        None,
        Sharpness,
        Ember,
        Leeching,
        Warding,
        Vigour,
        Swiftness,
        Might,
        Fortitude,
        Insight,
        Fortune,
        Resilience
    }

    public static class GearEffects
    {
        public static string Describe(GearEffectId effect)
        {
            switch (effect)
            {
                case GearEffectId.Sharpness: return "Sharpness";
                case GearEffectId.Ember: return "Embers";
                case GearEffectId.Leeching: return "Leeching";
                case GearEffectId.Warding: return "Warding";
                case GearEffectId.Vigour: return "Vigour";
                case GearEffectId.Swiftness: return "Swiftness";
                case GearEffectId.Might: return "Might";
                case GearEffectId.Fortitude: return "Fortitude";
                case GearEffectId.Insight: return "Insight";
                case GearEffectId.Fortune: return "Fortune";
                case GearEffectId.Resilience: return "Resilience";
                default: return "Nothing";
            }
        }

        public static string Explain(GearEffectId effect, int power)
        {
            switch (effect)
            {
                case GearEffectId.Sharpness: return $"+{power} damage";
                case GearEffectId.Ember: return $"+{power} burning damage, which armour does not stop";
                case GearEffectId.Leeching: return $"returns {power}% of what it deals";
                case GearEffectId.Warding: return $"+{power} armour";
                case GearEffectId.Vigour: return $"+{power}% stamina recovery";
                case GearEffectId.Swiftness: return $"+{power}% movement";
                case GearEffectId.Might: return $"+{power} Strength";
                case GearEffectId.Fortitude: return $"+{power} Endurance";
                case GearEffectId.Insight: return $"+{power} Intelligence";
                case GearEffectId.Fortune: return $"+{power} Luck";
                case GearEffectId.Resilience: return $"takes {power}% less from every blow";
                default: return string.Empty;
            }
        }

        // Sharpness on a helmet does nothing, so nothing offers it for one
        public static bool Suits(GearEffectId effect, ItemCategory category)
        {
            switch (effect)
            {
                case GearEffectId.None:
                    return false;
                case GearEffectId.Sharpness:
                case GearEffectId.Ember:
                case GearEffectId.Leeching:
                    return category == ItemCategory.Weapon;
                case GearEffectId.Warding:
                case GearEffectId.Vigour:
                case GearEffectId.Resilience:
                    return category == ItemCategory.Armour;
                default:
                    return category == ItemCategory.Weapon || category == ItemCategory.Armour;
            }
        }

        // Which of the eight it raises, and Luck for none of them, so callers can ask once rather
        // than switching on the effect themselves
        public static bool RaisesAttribute(GearEffectId effect, out CharacterAttribute attribute)
        {
            switch (effect)
            {
                case GearEffectId.Might: attribute = CharacterAttribute.Strength; return true;
                case GearEffectId.Fortitude: attribute = CharacterAttribute.Endurance; return true;
                case GearEffectId.Insight: attribute = CharacterAttribute.Intelligence; return true;
                case GearEffectId.Fortune: attribute = CharacterAttribute.Luck; return true;
                default: attribute = CharacterAttribute.Strength; return false;
            }
        }
    }
}
