using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Character Class", fileName = "Class")]
    public class ClassDefinition : ScriptableObject
    {
        [SerializeField] string id = "class.unnamed";
        [SerializeField] string displayName = "Adventurer";
        [SerializeField, TextArea] string description;

        [Header("Skills")]
        [SerializeField] SkillId[] primarySkills = new SkillId[3];
        [SerializeField] SkillId[] majorSkills = new SkillId[3];
        [SerializeField] SkillId[] minorSkills = new SkillId[6];

        [Header("Attributes")]
        [SerializeField] int[] attributeBonuses = new int[8];

        [Header("Progression")]
        [SerializeField, Min(1)] int hitPointsPerLevel = 10;
        [SerializeField, Range(0.5f, 3f)] float mageryMultiplier = 1f;

        [Header("Advantages and disadvantages")]
        [SerializeField] CharacterSpecial[] specials = new CharacterSpecial[0];

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillId[] PrimarySkills => primarySkills;
        public SkillId[] MajorSkills => majorSkills;
        public SkillId[] MinorSkills => minorSkills;
        public int HitPointsPerLevel => hitPointsPerLevel;
        public float MageryMultiplier => mageryMultiplier;
        public CharacterSpecial[] SpecialTraits => specials;

        public int AttributeBonus(CharacterAttribute attribute)
        {
            int index = (int)attribute;
            return attributeBonuses != null && index < attributeBonuses.Length ? attributeBonuses[index] : 0;
        }

        // Daggerfall charges you for a strong class by making its levels cost more. The exact table is
        // not reproduced, the shape is, net advantage points push the multiplier up, and a class
        // that takes on real drawbacks levels faster
        public float Difficulty
        {
            get
            {
                int net = 0;
                foreach (CharacterSpecial special in specials)
                    net += Specials.Cost(special);

                return Mathf.Clamp(1f + net * 0.03f, 0.6f, 2f);
            }
        }

        // Authored classes come from an asset; the character creator builds one of these at runtime
        public void Configure(string name, SkillId[] primary, SkillId[] major, SkillId[] minor,
            int[] attributeSpend, CharacterSpecial[] chosen)
        {
            id = "class.custom";
            displayName = name;
            primarySkills = primary ?? new SkillId[0];
            majorSkills = major ?? new SkillId[0];
            minorSkills = minor ?? new SkillId[0];
            attributeBonuses = attributeSpend != null ? (int[])attributeSpend.Clone() : new int[8];
            specials = chosen ?? new CharacterSpecial[0];

            // A class that leads with fighting skills is tougher per level, as Daggerfall's are
            int combat = 0;
            foreach (SkillId skill in primarySkills)
                if (Skills.Group(skill) == "Combat") combat++;

            hitPointsPerLevel = 6 + combat * 2;
            mageryMultiplier = Has(CharacterSpecial.IncreasedMagery) ? 2f : 1f;
        }

        public bool Has(CharacterSpecial special)
        {
            foreach (CharacterSpecial candidate in specials)
            {
                if (candidate == special)
                    return true;
            }

            return false;
        }
    }
}
