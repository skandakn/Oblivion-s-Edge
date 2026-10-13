using System;
using System.Collections.Generic;

namespace OblivionsEdge.Crafting
{
    /// <summary>
    /// Requirement for an ingredient needed to craft a recipe.
    /// </summary>
    [Serializable]
    public struct RecipeIngredient
    {
        public string ItemId;
        public int RequiredQuantity;

        public RecipeIngredient(string itemId, int requiredQuantity)
        {
            ItemId = itemId;
            RequiredQuantity = requiredQuantity;
        }
    }

    /// <summary>
    /// Specification describing a crafting recipe with input ingredients and outcome.
    /// </summary>
    [Serializable]
    public class CraftingRecipe
    {
        public string RecipeId;
        public string DisplayName;
        public string Description;
        public string ResultItemId;
        public int ResultQuantity;
        public float CraftDurationSeconds;
        public List<RecipeIngredient> Ingredients = new List<RecipeIngredient>();

        public CraftingRecipe() { }

        public CraftingRecipe(string recipeId, string displayName, string resultItemId, int resultQuantity, float craftDurationSeconds)
        {
            RecipeId = recipeId;
            DisplayName = displayName;
            ResultItemId = resultItemId;
            ResultQuantity = resultQuantity;
            CraftDurationSeconds = craftDurationSeconds;
        }
    }
}
