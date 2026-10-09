using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum CraftingStation { None, Campfire, Forge, AlchemyBench, Workbench }

    [CreateAssetMenu(menuName = "Endless Descent/Crafting/Recipe", fileName = "Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        [Serializable]
        public class Ingredient
        {
            public ItemDefinition Item;
            [Min(1)] public int Count = 1;
        }

        [SerializeField] string id = "recipe.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField] Ingredient[] inputs;
        [SerializeField] ItemDefinition output;
        [SerializeField, Min(1)] int outputCount = 1;
        [SerializeField] CraftingStation station = CraftingStation.Workbench;
        [SerializeField, Min(0)] int craftingMinutes = 30;

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<Ingredient> Inputs => inputs;
        public ItemDefinition Output => output;
        public int OutputCount => outputCount;
        public CraftingStation Station => station;
        public int CraftingMinutes => craftingMinutes;
    }
}
