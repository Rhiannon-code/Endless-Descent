using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [DisallowMultipleComponent]
    public class CraftingSystem : MonoBehaviour
    {
        public bool CanCraft(RecipeDefinition recipe, Inventory inventory, CraftingStation available, out string reason)
        {
            if (recipe == null || inventory == null)
            {
                reason = "nothing to craft";
                return false;
            }

            if (recipe.Station != CraftingStation.None && recipe.Station != available)
            {
                reason = $"needs a {recipe.Station}";
                return false;
            }

            if (recipe.Inputs != null)
            {
                foreach (RecipeDefinition.Ingredient input in recipe.Inputs)
                {
                    if (input?.Item == null)
                        continue;

                    if (!inventory.Has(input.Item, input.Count))
                    {
                        reason = $"needs {input.Count}x {input.Item.DisplayName}";
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }

        public bool Craft(RecipeDefinition recipe, Inventory inventory, CraftingStation available, out string reason)
        {
            if (!CanCraft(recipe, inventory, available, out reason))
                return false;

            if (recipe.Inputs != null)
            {
                foreach (RecipeDefinition.Ingredient input in recipe.Inputs)
                {
                    if (input?.Item != null)
                        inventory.Remove(input.Item, input.Count);
                }
            }

            // Crafting costs time on the one clock, so it competes with shop hours and needs
            // rather than being free
            WorldClock.Instance?.Skip(recipe.CraftingMinutes);

            int leftover = inventory.Add(recipe.Output, recipe.OutputCount);
            if (leftover > 0)
                reason = "some output did not fit and was lost";

            return true;
        }
    }
}
