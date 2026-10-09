namespace EndlessDescent.Data
{
    public static class Schools
    {
        public static string Describe(SkillId school)
        {
            switch (school)
            {
                case SkillId.Destruction:
                    return "Destruction: workings that spend themselves on something. Dorrek's, by the " +
                           "old reckoning, and the only school the empire will admit exists.";
                case SkillId.Restoration:
                    return "Restoration: workings that return a thing to how it was. Aissa's. The one " +
                           "school a village will hide a practitioner of.";
                case SkillId.Alteration:
                    return "Alteration: workings that hold a thing in a state it would not keep. Aissa's, " +
                           "and the most useful and least dramatic of the five.";
                case SkillId.Illusion:
                    return "Illusion: workings on what is perceived rather than what is. Aissa's, and " +
                           "the school imperial scholarship took least seriously before the burning.";
                case SkillId.Mysticism:
                    return "Mysticism: workings that reach past the immediate. Dorrek's. Everything to do " +
                           "with the veil is filed here, which is an accident of cataloguing and has " +
                           "misled four hundred years of scholars.";
                default:
                    return "Not a school of the imperial classification.";
            }
        }

        public static bool IsMagic(SkillId skill) =>
            skill >= SkillId.Destruction && skill <= SkillId.Mysticism;

        // Which twin the pre Ban academy assigned a school to. The assignment is real in the sense
        // that the cycle affects these workings, the reasoning behind it was entirely wrong
        public static string Twin(SkillId school)
        {
            switch (school)
            {
                case SkillId.Destruction:
                case SkillId.Mysticism:
                    return "Dorrek";
                case SkillId.Restoration:
                case SkillId.Alteration:
                case SkillId.Illusion:
                    return "Aissa";
                default:
                    return "neither";
            }
        }
    }
}
