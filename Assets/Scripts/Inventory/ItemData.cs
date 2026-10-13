namespace OblivionsEdge.Inventory
{
    /// <summary>
    /// Item categorization for sorting, filtering, and usage rules.
    /// </summary>
    public enum ItemCategory
    {
        Weapon,
        Ammunition,
        MedicalConsumable,
        OxygenSupply,
        CraftingComponent,
        KeyItem,
        AudioLogOrDocument
    }

    /// <summary>
    /// Immutable specification describing an item's archetype and base properties.
    /// </summary>
    [System.Serializable]
    public struct ItemData
    {
        public string ItemId;
        public string DisplayName;
        public string Description;
        public ItemCategory Category;
        public int MaxStackSize;
        public float UnitWeight;
        public bool IsConsumable;

        public ItemData(string itemId, string displayName, string description, ItemCategory category, int maxStackSize = 1, float unitWeight = 0.5f, bool isConsumable = false)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Description = description;
            Category = category;
            MaxStackSize = maxStackSize;
            UnitWeight = unitWeight;
            IsConsumable = isConsumable;
        }
    }
}
