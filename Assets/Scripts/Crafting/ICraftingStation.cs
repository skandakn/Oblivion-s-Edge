using System.Collections.Generic;
using OblivionsEdge.Core;
using OblivionsEdge.Inventory;

namespace OblivionsEdge.Crafting
{
    /// <summary>
    /// Contract defining an in-world crafting workbench/station.
    /// To be implemented by Member 5 (Crafting & Upgrades).
    /// </summary>
    public interface ICraftingStation : IInteractable
    {
        string StationId { get; }
        string StationName { get; }
        IReadOnlyList<CraftingRecipe> AvailableRecipes { get; }

        bool CanCraft(CraftingRecipe recipe, IInventory sourceInventory);
        bool TryCraft(CraftingRecipe recipe, IInventory sourceInventory);

        event System.Action<CraftingRecipe> OnCraftSuccess;
        event System.Action<CraftingRecipe, string> OnCraftFailed; // (recipe, reason)
    }
}
