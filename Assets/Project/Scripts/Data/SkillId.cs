namespace EndlessDescent.Data
{
    public enum SkillId
    {
        Longsword,
        ShortBlade,
        Axe,
        BluntWeapon,
        HandToHand,
        Archery,
        Block,
        Dodging,
        CriticalStrike,
        Destruction,
        Restoration,
        Alteration,
        Illusion,
        Mysticism,
        Stealth,
        Lockpicking,
        Climbing,
        Running,
        Mercantile,
        Etiquette
    }

    public static class Skills
    {
        public const int Count = 20;

        // Daggerfall governs every skill with an attribute, advancement and effectiveness both lean
        // on it, which is what stops attributes being a separate, inert screen
        public static CharacterAttribute Governing(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Longsword:
                case SkillId.Axe:
                case SkillId.BluntWeapon:
                case SkillId.HandToHand:
                    return CharacterAttribute.Strength;

                case SkillId.ShortBlade:
                case SkillId.Archery:
                case SkillId.CriticalStrike:
                case SkillId.Lockpicking:
                    return CharacterAttribute.Agility;

                case SkillId.Block:
                case SkillId.Climbing:
                    return CharacterAttribute.Endurance;

                case SkillId.Dodging:
                case SkillId.Running:
                case SkillId.Stealth:
                    return CharacterAttribute.Speed;

                case SkillId.Destruction:
                case SkillId.Mysticism:
                case SkillId.Illusion:
                    return CharacterAttribute.Intelligence;

                case SkillId.Restoration:
                case SkillId.Alteration:
                    return CharacterAttribute.Willpower;

                default:
                    return CharacterAttribute.Personality;
            }
        }

        public static string Group(SkillId skill)
        {
            if (skill <= SkillId.CriticalStrike) return "Combat";
            if (skill <= SkillId.Mysticism) return "Magic";
            if (skill <= SkillId.Running) return "Stealth";
            return "Social";
        }
    }
}
