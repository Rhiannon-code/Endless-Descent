namespace EndlessDescent.Data
{
    // What combat needs to know about whoever is swinging, and whoever is being hit. The player's
    // sheet and a creature's sheet both answer it, so nothing in combat knows which it is holding
    public interface ICharacterStats
    {
        int Level { get; }
        int Attribute(CharacterAttribute attribute);
        int Skill(SkillId skill);
    }
}
