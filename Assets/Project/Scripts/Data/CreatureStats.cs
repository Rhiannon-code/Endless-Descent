using System;
using UnityEngine;

namespace EndlessDescent.Data
{
    // A creature's sheet. The same eight attributes the player has, and four skill bands spread across
    // the twenty skills, because authoring twenty numbers for a giant rat is how a bestiary stops
    // being worth keeping
    [Serializable]
    public class CreatureStats
    {
        [Min(1)] public int Level = 1;
        [Min(0)] public int HitPointsPerLevel = 10;

        [Header("Attributes")]
        [Range(1, 100)] public int Strength = 40;
        [Range(1, 100)] public int Intelligence = 20;
        [Range(1, 100)] public int Willpower = 30;
        [Range(1, 100)] public int Agility = 40;
        [Range(1, 100)] public int Speed = 40;
        [Range(1, 100)] public int Endurance = 45;
        [Range(1, 100)] public int Personality = 20;
        [Range(1, 100)] public int Luck = 40;

        [Header("Skill bands")]
        [Range(1, 100)] public int Weapon = 25;
        [Range(1, 100)] public int Defence = 20;
        [Range(1, 100)] public int Magic = 5;
        [Range(1, 100)] public int Stealth = 20;

        public int Attribute(CharacterAttribute attribute)
        {
            switch (attribute)
            {
                case CharacterAttribute.Strength: return Strength;
                case CharacterAttribute.Intelligence: return Intelligence;
                case CharacterAttribute.Willpower: return Willpower;
                case CharacterAttribute.Agility: return Agility;
                case CharacterAttribute.Speed: return Speed;
                case CharacterAttribute.Endurance: return Endurance;
                case CharacterAttribute.Personality: return Personality;
                default: return Luck;
            }
        }

        public int Skill(SkillId skill)
        {
            if (skill == SkillId.Block || skill == SkillId.Dodging)
                return Defence;

            if (skill <= SkillId.CriticalStrike)
                return Weapon;

            if (skill <= SkillId.Mysticism)
                return Magic;

            if (skill <= SkillId.Running)
                return Stealth;

            return Mathf.Max(5, Personality / 2);
        }

        // Nothing carries the player's flat starting health, a rat is a rat at level one
        public int MaxHealth => CharacterMaths.MaxHealth(Level, Endurance, HitPointsPerLevel, 0);
    }
}
