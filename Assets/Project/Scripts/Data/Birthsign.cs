namespace EndlessDescent.Data
{
    // The eight phases of the moon cycle. A sign is not where you were born, it is the sky on the
    // day you were NAMED, because that is the date the register carries and the one anybody can
    // check
    public enum BirthsignId
    {
        None,
        Opening,
        Bright,
        Ebb,
        ColdTurn,
        Onset,
        Weight,
        Lifting,
        WarmTurn
    }

    // Pure data with no references, as before, a missing asset here would silently cost the player
    // their sign, so there are no assets
    public static class Birthsigns
    {
        public static string Describe(BirthsignId sign)
        {
            switch (sign)
            {
                case BirthsignId.Opening: return "the Opening:  Aissa coming on.  +15 Speed, +10 Agility";
                case BirthsignId.Bright: return "the Bright:  Aissa near.  magicka x1.5, weak to fire";
                case BirthsignId.Ebb: return "the Ebb:  Aissa withdrawing.  +15 Willpower, heals quickly";
                case BirthsignId.ColdTurn: return "the Cold Turn:  neither near.  +15 Luck, absorbs spells";
                case BirthsignId.Onset: return "the Onset:  Dorrek coming on.  +15 Endurance, +10 Strength";
                case BirthsignId.Weight: return "the Weight:  Dorrek near.  +20 Strength, immune to poison";
                case BirthsignId.Lifting: return "the Lifting:  Dorrek withdrawing.  +15 Intelligence, +10 Personality";
                case BirthsignId.WarmTurn: return "the Warm Turn:  neither near.  +10 Luck, +10 Personality, strikes the undead";
                default: return "No sign recorded";
            }
        }

        // Which twin was the far one, and therefore whose temple was open to do the naming. A child
        // named under the Bright was named in Dorrek's house, and everyone reads that off the sign
        // without being told
        public static string NamedIn(BirthsignId sign)
        {
            switch (sign)
            {
                case BirthsignId.Opening:
                case BirthsignId.Bright:
                case BirthsignId.Ebb:
                    return "Dorrek";
                case BirthsignId.Onset:
                case BirthsignId.Weight:
                case BirthsignId.Lifting:
                    return "Aissa";
                case BirthsignId.ColdTurn:
                case BirthsignId.WarmTurn:
                    return "both, and neither";
                default:
                    return "nowhere";
            }
        }

        public static int AttributeBonus(BirthsignId sign, CharacterAttribute attribute)
        {
            switch (sign)
            {
                case BirthsignId.Opening:
                    return attribute == CharacterAttribute.Speed ? 15
                        : attribute == CharacterAttribute.Agility ? 10 : 0;
                case BirthsignId.Ebb:
                    return attribute == CharacterAttribute.Willpower ? 15 : 0;
                case BirthsignId.ColdTurn:
                    return attribute == CharacterAttribute.Luck ? 15 : 0;
                case BirthsignId.Onset:
                    return attribute == CharacterAttribute.Endurance ? 15
                        : attribute == CharacterAttribute.Strength ? 10 : 0;
                case BirthsignId.Weight:
                    return attribute == CharacterAttribute.Strength ? 20 : 0;
                case BirthsignId.Lifting:
                    return attribute == CharacterAttribute.Intelligence ? 15
                        : attribute == CharacterAttribute.Personality ? 10 : 0;
                case BirthsignId.WarmTurn:
                    return attribute == CharacterAttribute.Luck || attribute == CharacterAttribute.Personality ? 10 : 0;
                default:
                    return 0;
            }
        }

        // Only the Bright. Being born while the kindly twin is close is the one thing in imperial
        // superstition everybody agrees is a sign, and it is the only sign that costs you something
        public static float MageryMultiplier(BirthsignId sign) =>
            sign == BirthsignId.Bright ? 1.5f : 1f;

        public static bool Grants(BirthsignId sign, CharacterSpecial special)
        {
            switch (sign)
            {
                case BirthsignId.Bright: return special == CharacterSpecial.CriticalWeaknessToFire;
                case BirthsignId.Ebb: return special == CharacterSpecial.RapidHealing;
                case BirthsignId.ColdTurn: return special == CharacterSpecial.SpellAbsorption;
                case BirthsignId.Weight: return special == CharacterSpecial.ImmunityToPoison;
                case BirthsignId.WarmTurn: return special == CharacterSpecial.BonusToHitUndead;
                default: return false;
            }
        }
    }
}
