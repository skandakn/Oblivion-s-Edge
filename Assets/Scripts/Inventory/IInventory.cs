using UnityEngine;

namespace OblivionsEdge.Inventory
{
    /// <summary>
    /// Contract for an individual runtime instance of an item in inventory.
    /// </summary>
    public interface IItem
    {
        ItemData Data { get; }
        int Quantity { get; }

        bool CanStackWith(IItem other);
        int AddQuantity(int amount);
        int RemoveQuantity(int amount);
    }

    /// <summary>
    /// Contract defining inventory storage, weight limits, slots, and lookup operations.
    /// To be implemented by Member 5 (Inventory & Crafting).
    /// </summary>
    public interface IInventory
    {
        int SlotCapacity { get; }
        float CurrentWeight { get; }
        float MaxWeight { get; }
        bool IsFull { get; }

        bool AddItem(IItem item);
        bool AddItemById(string itemId, int quantity);
        bool RemoveItem(string itemId, int quantity);
        bool HasItem(string itemId, int quantity = 1);
        int GetItemCount(string itemId);
        IItem GetItemAtSlot(int slotIndex);

        event System.Action<int, IItem> OnSlotChanged; // (slotIndex, itemInstance)
        event System.Action<IItem> OnItemAdded;
        event System.Action<string, int> OnItemRemoved; // (itemId, quantityRemoved)
        event System.Action<float, float> OnWeightChanged; // (currentWeight, maxWeight)
    }
}
