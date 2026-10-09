using System;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Player
{
    [Serializable]
    public struct CharacterSheetState
    {
        public int[] Attributes;
        public int[] Skills;
        public int Level;
        public string Name;
        public string Blueprint;
        public float[] Accumulated;
        public int SkillsAtLastLevel;
    }

    [DisallowMultipleComponent]
    public class CharacterSheet : MonoBehaviour, ISaveable, ICharacterStats
    {
        [SerializeField] ClassDefinition characterClass;
        [SerializeField] GameDatabase database;
        [SerializeField] Equipment equipment;
        [SerializeField] string characterName = "Wanderer";
        [SerializeField] string saveKey = "sheet.player";
        [SerializeField, Min(1)] int baseAttribute = 50;
        [SerializeField, Min(1)] int startingSkill = 15;
        [SerializeField, Min(1)] int levelSkillCost = 15;

        readonly int[] attributes = new int[8];
        readonly int[] skills = new int[Skills.Count];
        readonly float[] accumulated = new float[Skills.Count];
        BirthsignId birthsign = BirthsignId.None;
        CharacterBlueprint blueprint;

        int level = 1;
        int skillsAtLastLevel;

        public event Action Changed;
        public event Action<SkillId, int> SkillRaised;
        public event Action<int> LevelledUp;

        public string SaveKey => saveKey;
        public ClassDefinition Class => characterClass;
        public BirthsignId Birthsign => birthsign;
        public string CharacterName => characterName;
        public CharacterBlueprint Blueprint => blueprint;
        public int Level => level;

        // Daggerfall derives everything from the eight, nothing here is a free standing number
        // The formulas live in CharacterMaths because creatures derive theirs the same way 
        public const int StartingHealth = 25;

        // Derived from Attribute rather than Get, so a ring of Might is worth carry weight and a
        // helm of Fortitude is worth health, the same as the points would have been
        public int MaxHealth => CharacterMaths.MaxHealth(level, Attribute(CharacterAttribute.Endurance), HitPointsPerLevel, StartingHealth);
        public int MaxMagicka => CharacterMaths.MaxMagicka(Attribute(CharacterAttribute.Intelligence), Attribute(CharacterAttribute.Willpower), Magery);
        public float MaxStamina => CharacterMaths.MaxStamina(Attribute(CharacterAttribute.Strength), Attribute(CharacterAttribute.Endurance));
        public float CarryWeight => CharacterMaths.CarryWeight(Attribute(CharacterAttribute.Strength));

        float Magery => (characterClass != null ? characterClass.MageryMultiplier : 1f)
                        * Birthsigns.MageryMultiplier(birthsign);
        int HitPointsPerLevel => characterClass != null ? characterClass.HitPointsPerLevel : 8;

        void Awake()
        {
            if (attributes[0] == 0)
                Reset(characterClass);
        }

        // Made from the creator, or made again from a save. Either way the class is rebuilt rather
        // than referenced, because a custom one is not an asset and never was
        public void Apply(CharacterBlueprint fromCreator)
        {
            blueprint = fromCreator;
            characterName = string.IsNullOrWhiteSpace(fromCreator.Name) ? characterName : fromCreator.Name;

            Reset(fromCreator.ResolveClass(database), fromCreator.Birthsign);
        }

        public void Reset(ClassDefinition definition) => Reset(definition, BirthsignId.None);

        public void Reset(ClassDefinition definition, BirthsignId sign)
        {
            characterClass = definition;
            birthsign = sign;

            for (int i = 0; i < attributes.Length; i++)
            {
                attributes[i] = baseAttribute
                    + (definition != null ? definition.AttributeBonus((CharacterAttribute)i) : 0)
                    + Birthsigns.AttributeBonus(sign, (CharacterAttribute)i);
            }

            for (int i = 0; i < skills.Length; i++)
                skills[i] = startingSkill;

            if (definition != null)
            {
                Seed(definition.PrimarySkills, 30);
                Seed(definition.MajorSkills, 22);
                Seed(definition.MinorSkills, 18);
            }

            level = 1;
            skillsAtLastLevel = WeightedSkillTotal();
            Changed?.Invoke();
        }

        void Seed(SkillId[] set, int value)
        {
            if (set == null)
                return;

            foreach (SkillId skill in set)
                skills[(int)skill] = Mathf.Max(skills[(int)skill], value);
        }

        // Get is what the character is, Attribute is what they are while wearing what they are
        // wearing. Creation and the sheet page show the first, combat reads the second
        public int Get(CharacterAttribute attribute) => attributes[(int)attribute];

        public int Attribute(CharacterAttribute attribute) =>
            Get(attribute) + (equipment != null ? equipment.Bonuses.Attribute(attribute) : 0);

        public void Set(CharacterAttribute attribute, int value)
        {
            attributes[(int)attribute] = Mathf.Clamp(value, 1, 100);
            Changed?.Invoke();
        }

        public int Skill(SkillId skill) => skills[(int)skill];

        // Paid instruction skips the use accumulator entirely: you buy the point outright
        public void Train(SkillId skill, int points = 1)
        {
            int index = (int)skill;
            skills[index] = Mathf.Min(100, skills[index] + points);
            SkillRaised?.Invoke(skill, skills[index]);
            CheckLevel();
            Changed?.Invoke();
        }

        // Skills advance by being used, as Daggerfall does. Governing attribute biases how fast
        public void Use(SkillId skill, float amount = 1f)
        {
            int index = (int)skill;
            if (skills[index] >= 100)
                return;

            float governed = Get(Skills.Governing(skill)) / 50f;
            accumulated[index] += amount * Mathf.Clamp(governed, 0.5f, 2f);

            if (accumulated[index] < AdvanceCost(skills[index]))
                return;

            accumulated[index] = 0f;
            skills[index]++;
            SkillRaised?.Invoke(skill, skills[index]);
            CheckLevel();
            Changed?.Invoke();
        }

        static float AdvanceCost(int current) => 4f + current * 0.35f;

        int WeightedSkillTotal()
        {
            if (characterClass == null)
                return 0;

            int total = 0;
            foreach (SkillId skill in characterClass.PrimarySkills) total += skills[(int)skill];
            foreach (SkillId skill in characterClass.MajorSkills) total += skills[(int)skill];
            foreach (SkillId skill in characterClass.MinorSkills) total += skills[(int)skill] / 4;
            return total;
        }

        void CheckLevel()
        {
            float cost = levelSkillCost * (characterClass != null ? characterClass.Difficulty : 1f);

            while (WeightedSkillTotal() - skillsAtLastLevel >= cost)
            {
                skillsAtLastLevel += Mathf.RoundToInt(cost);
                level++;
                LevelledUp?.Invoke(level);
            }
        }

        public bool Has(CharacterSpecial special) =>
            (characterClass != null && characterClass.Has(special)) || Birthsigns.Grants(birthsign, special);

        public string CaptureJson() => JsonUtility.ToJson(new CharacterSheetState
        {
            Attributes = (int[])attributes.Clone(),
            Skills = (int[])skills.Clone(),
            Level = level,
            Name = characterName,
            Blueprint = blueprint.ToJson(),
            Accumulated = (float[])accumulated.Clone(),
            SkillsAtLastLevel = skillsAtLastLevel
        });

        public void RestoreJson(string json)
        {
            CharacterSheetState state = JsonUtility.FromJson<CharacterSheetState>(json);

            // The class and birthsign come back first, they decide hit points per level and magery,
            // which the restored numbers below are only meaningful against
            if (!string.IsNullOrEmpty(state.Blueprint))
            {
                blueprint = CharacterBlueprint.FromJson(state.Blueprint);
                characterClass = blueprint.ResolveClass(database);
                birthsign = blueprint.Birthsign;
            }

            if (!string.IsNullOrEmpty(state.Name))
                characterName = state.Name;

            if (state.Attributes != null)
                Array.Copy(state.Attributes, attributes, Mathf.Min(state.Attributes.Length, attributes.Length));

            if (state.Skills != null)
                Array.Copy(state.Skills, skills, Mathf.Min(state.Skills.Length, skills.Length));

            if (state.Accumulated != null)
                Array.Copy(state.Accumulated, accumulated, Mathf.Min(state.Accumulated.Length, accumulated.Length));

            // Progress towards the next level is the gap between this and the live total, recomputing it
            // here wiped that progress on every load
            level = Mathf.Max(1, state.Level);
            skillsAtLastLevel = state.SkillsAtLastLevel > 0 ? state.SkillsAtLastLevel : WeightedSkillTotal();
            Changed?.Invoke();
        }
    }
}
