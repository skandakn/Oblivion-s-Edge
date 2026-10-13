using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblivionsEdge.Inventory
{
    /// <summary>
    /// Small progression inventory for the story's three unique DeathStorm parts.
    /// Parts can only enter through TryCollectComponent, which enforces the story order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventorySystem : MonoBehaviour, IInventory
    {
        public const string EdgeItemId = "deathstorm_edge";
        public const string HandleItemId = "deathstorm_handle";
        public const string CoreItemId = "deathstorm_core";
        public const string DeathStormItemId = "deathstorm";

        [SerializeField, Min(1)] private int _slotCapacity = 12;
        [SerializeField, Min(0f)] private float _maxWeight = 30f;

        private readonly List<IItem> _items = new List<IItem>();
        private bool _hasEdge;
        private bool _hasHandle;
        private bool _hasCore;
        private bool _hasDeathStorm;
        private bool _noteDiscovered;

        public int SlotCapacity => _slotCapacity;
        public float CurrentWeight
        {
            get
            {
                float weight = 0f;
                foreach (IItem item in _items)
                    weight += item.Data.UnitWeight * item.Quantity;
                return weight;
            }
        }
        public float MaxWeight => _maxWeight;
        public bool IsFull => _items.Count >= _slotCapacity;
        public bool HasEdge => _hasEdge;
        public bool HasHandle => _hasHandle;
        public bool HasCore => _hasCore;
        public bool HasDeathStorm => _hasDeathStorm;
        public bool HasDiscoveredNote => _noteDiscovered;

        public event Action<int, IItem> OnSlotChanged;
        public event Action<IItem> OnItemAdded;
        public event Action<string, int> OnItemRemoved;
        public event Action<float, float> OnWeightChanged;
        public event Action<bool, bool, bool, bool> OnComponentsChanged;
        public event Action OnNoteDiscovered;
        public event Action OnDeathStormCrafted;

        /// <summary>Called only when the player interacts with the story note.</summary>
        internal bool DiscoverNote()
        {
            if (_noteDiscovered) return false;
            _noteDiscovered = true;
            OnNoteDiscovered?.Invoke();
            return true;
        }

        /// <summary>
        /// Collects a unique component. Edge/Handle require the note; Core requires both
        /// Edge and Handle. Duplicate collection attempts never add another item.
        /// </summary>
        internal bool TryCollectComponent(string itemId)
        {
            if (!_noteDiscovered) return false;
            if (itemId == EdgeItemId && _hasEdge || itemId == HandleItemId && _hasHandle || itemId == CoreItemId && _hasCore)
                return false;
            if (itemId == CoreItemId && !(_hasEdge && _hasHandle)) return false;
            if (itemId != EdgeItemId && itemId != HandleItemId && itemId != CoreItemId) return false;

            ItemData data = CreateComponentData(itemId);
            IItem item = new RuntimeItem(data, 1);
            if (!AddItemInternal(item)) return false;

            if (itemId == EdgeItemId) _hasEdge = true;
            else if (itemId == HandleItemId) _hasHandle = true;
            else _hasCore = true;
            OnComponentsChanged?.Invoke(_hasEdge, _hasHandle, _hasCore, _hasDeathStorm);
            return true;
        }

        /// <summary>
        /// Connect this to the crafting system's successful-forge event. It cannot
        /// succeed until all three unique components are in inventory.
        /// </summary>
        internal bool NotifyDeathStormCrafted()
        {
            if (_hasDeathStorm || !(_hasEdge && _hasHandle && _hasCore)) return false;
            _hasDeathStorm = true;
            OnComponentsChanged?.Invoke(_hasEdge, _hasHandle, _hasCore, _hasDeathStorm);
            OnDeathStormCrafted?.Invoke();
            return true;
        }

        /// <summary>Generic additions are supported for non-progression items.</summary>
        public bool AddItem(IItem item)
        {
            if (item == null || IsProgressionItem(item.Data.ItemId)) return false;
            return AddItemInternal(item);
        }

        public bool AddItemById(string itemId, int quantity)
        {
            // Story parts must go through TryCollectComponent; a generic ID grant would
            // otherwise bypass note discovery and the Edge/Handle gate for the Core.
            if (quantity <= 0 || string.IsNullOrEmpty(itemId) || IsProgressionItem(itemId)) return false;
            return AddItemInternal(new RuntimeItem(new ItemData(itemId, itemId, string.Empty, ItemCategory.CraftingComponent), quantity));
        }

        public bool RemoveItem(string itemId, int quantity)
        {
            if (quantity <= 0 || IsProgressionItem(itemId) || GetItemCount(itemId) < quantity)
                return false;

            int remaining = quantity;
            for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                IItem item = _items[i];
                if (item.Data.ItemId != itemId) continue;
                int removed = item.RemoveQuantity(remaining);
                remaining -= removed;
                if (item.Quantity == 0)
                {
                    _items.RemoveAt(i);
                    OnSlotChanged?.Invoke(i, null);
                }
            }
            OnItemRemoved?.Invoke(itemId, quantity);
            OnWeightChanged?.Invoke(CurrentWeight, MaxWeight);
            return true;
        }

        public bool HasItem(string itemId, int quantity = 1) => quantity > 0 && GetItemCount(itemId) >= quantity;
        public int GetItemCount(string itemId)
        {
            int count = 0;
            foreach (IItem item in _items)
                if (item.Data.ItemId == itemId) count += item.Quantity;
            return count;
        }
        public IItem GetItemAtSlot(int slotIndex) => slotIndex >= 0 && slotIndex < _items.Count ? _items[slotIndex] : null;

        private bool AddItemInternal(IItem item)
        {
            if (item == null || item.Quantity <= 0 || item.Data.MaxStackSize <= 0 ||
                CurrentWeight + item.Data.UnitWeight * item.Quantity > _maxWeight) return false;

            int slot = _items.FindIndex(existing => existing.CanStackWith(item) && existing.Quantity + item.Quantity <= existing.Data.MaxStackSize);
            if (slot < 0)
            {
                if (IsFull) return false;
                slot = _items.Count;
                _items.Add(item);
            }
            else
            {
                _items[slot].AddQuantity(item.Quantity);
            }
            OnSlotChanged?.Invoke(slot, _items[slot]);
            OnItemAdded?.Invoke(_items[slot]);
            OnWeightChanged?.Invoke(CurrentWeight, MaxWeight);
            return true;
        }

        private static bool IsProgressionItem(string itemId) => itemId == EdgeItemId || itemId == HandleItemId ||
            itemId == CoreItemId || itemId == DeathStormItemId;

        private static ItemData CreateComponentData(string itemId)
        {
            string label = itemId == EdgeItemId ? "DeathStorm Edge" : itemId == HandleItemId ? "DeathStorm Handle" : "DeathStorm Core";
            return new ItemData(itemId, label, "A unique component of the DeathStorm weapon.", ItemCategory.CraftingComponent);
        }

        private sealed class RuntimeItem : IItem
        {
            public ItemData Data { get; }
            public int Quantity { get; private set; }
            public RuntimeItem(ItemData data, int quantity) { Data = data; Quantity = quantity; }
            public bool CanStackWith(IItem other) => other != null && Data.ItemId == other.Data.ItemId && Data.MaxStackSize > 1;
            public int AddQuantity(int amount)
            {
                int added = Mathf.Clamp(amount, 0, Data.MaxStackSize - Quantity);
                Quantity += added;
                return added;
            }
            public int RemoveQuantity(int amount)
            {
                int removed = Mathf.Clamp(amount, 0, Quantity);
                Quantity -= removed;
                return removed;
            }
        }
    }
}
