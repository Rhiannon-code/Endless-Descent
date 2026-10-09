using System;
using UnityEngine;

namespace EndlessDescent.Data
{
    [Serializable]
    public struct CharacterBlueprint
    {
        public string Name;
        public string ClassId;
        public string ClassName;
        public string BackgroundId;
        public int[] AttributeSpend;
        public SkillId[] Primary;
        public SkillId[] Major;
        public SkillId[] Minor;
        public CharacterSpecial[] Specials;
        public BirthsignId Birthsign;

        public bool IsCustomClass => string.IsNullOrEmpty(ClassId);

        public string ToJson() => JsonUtility.ToJson(this);

        public static CharacterBlueprint FromJson(string json) =>
            string.IsNullOrEmpty(json) ? default : JsonUtility.FromJson<CharacterBlueprint>(json);

        // Preset classes are assets, custom ones are built here from what was spent. Either way the
        // sheet is handed a ClassDefinition and never learns which it was
        public ClassDefinition ResolveClass(GameDatabase database)
        {
            if (!IsCustomClass)
            {
                ClassDefinition preset = database != null ? database.Class(ClassId) : null;
                if (preset != null)
                    return preset;
            }

            ClassDefinition custom = ScriptableObject.CreateInstance<ClassDefinition>();
            custom.name = string.IsNullOrEmpty(ClassName) ? "Custom" : ClassName;
            custom.Configure(custom.name, Primary, Major, Minor, AttributeSpend, Specials);
            return custom;
        }
    }
}
