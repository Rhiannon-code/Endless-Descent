using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // Where the character was before the game starts. It is the one part of creation that answers
    // "why do you own this" rather than "what are you good at", so it decides the kit, the purse
    // and who already knows your name
    [CreateAssetMenu(menuName = "Endless Descent/Character Background", fileName = "Background")]
    public class BackgroundDefinition : ScriptableObject
    {
        [SerializeField] string id = "background.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField, TextArea] string description;

        [Header("What you arrive with")]
        [SerializeField] ItemDefinition[] worn;
        [SerializeField] ItemDefinition[] carried;
        [SerializeField, Min(0)] int startingGold = 50;

        [Header("What it left you knowing")]
        [SerializeField] SkillId[] trainedSkills;
        [SerializeField, Min(0)] int trainedBonus = 8;

        [Header("Who already knows you")]
        [SerializeField] FactionDefinition faction;
        [SerializeField] int standing;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public IReadOnlyList<ItemDefinition> Worn => worn;
        public IReadOnlyList<ItemDefinition> Carried => carried;
        public int StartingGold => startingGold;
        public IReadOnlyList<SkillId> TrainedSkills => trainedSkills;
        public int TrainedBonus => trainedBonus;
        public FactionDefinition Faction => faction;
        public int Standing => standing;

        public void Configure(string newId, string name, string text, ItemDefinition[] newWorn,
            ItemDefinition[] newCarried, int gold, SkillId[] skills, int bonus,
            FactionDefinition newFaction, int newStanding)
        {
            id = newId;
            displayName = name;
            description = text;
            worn = newWorn;
            carried = newCarried;
            startingGold = gold;
            trainedSkills = skills;
            trainedBonus = bonus;
            faction = newFaction;
            standing = newStanding;
        }
    }
}
