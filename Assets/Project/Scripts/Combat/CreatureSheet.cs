using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Combat
{
    // The sheet a creature fights from, taken from its definition when it spawns. It answers the same
    // interface the player's sheet does, so one set of formulas serves both sides
    [DisallowMultipleComponent]
    public class CreatureSheet : MonoBehaviour, ICharacterStats
    {
        [SerializeField] CreatureStats stats = new CreatureStats();

        public void Adopt(CreatureStats from)
        {
            if (from != null)
                stats = from;
        }

        public int Level => stats.Level;
        public int Attribute(CharacterAttribute attribute) => stats.Attribute(attribute);
        public int Skill(SkillId skill) => stats.Skill(skill);
        public int MaxHealth => stats.MaxHealth;
    }
}
