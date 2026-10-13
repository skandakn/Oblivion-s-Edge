using System;
using OblivionsEdge.Core;
using OblivionsEdge.Puzzles;
using UnityEngine;

namespace OblivionsEdge.Inventory
{
    /// <summary>World pickup for a unique DeathStorm component.</summary>
    [DisallowMultipleComponent]
    public sealed class ComponentPickup : MonoBehaviour, IInteractable, IInteractionPromptSource
    {
        [SerializeField] private string _itemId = InventorySystem.EdgeItemId;
        [SerializeField] private string _prompt = "Press E to collect";
        [SerializeField] private bool _requiresPuzzleCompletion;
        [SerializeField] private bool _puzzleCompleted;
        private bool _collected;

        public string InteractionPromptText => _collected ? string.Empty : _prompt;
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;
        public event Action<GameObject> OnInteracted;

        private bool RequiresPuzzleCompletion => _requiresPuzzleCompletion || _itemId == InventorySystem.CoreItemId;

        public bool CanInteract(GameObject interactor)
        {
            if (_collected || (RequiresPuzzleCompletion && !_puzzleCompleted) || interactor == null) return false;
            InventorySystem inventory = interactor.GetComponentInParent<InventorySystem>();
            return inventory != null && inventory.HasDiscoveredNote && !inventory.HasItem(_itemId) &&
                (_itemId != InventorySystem.CoreItemId || (inventory.HasEdge && inventory.HasHandle));
        }

        public string GetInteractionPrompt(GameObject interactor)
        {
            if (_collected) return string.Empty;
            if (interactor == null) return _prompt;
            InventorySystem inventory = interactor.GetComponentInParent<InventorySystem>();
            if (inventory == null) return "Requires an inventory";
            if (RequiresPuzzleCompletion && !_puzzleCompleted) return "Puzzle lock is still active";
            if (!inventory.HasDiscoveredNote) return "Read the teammate's note first";
            if (inventory.HasItem(_itemId)) return "Already collected";
            if (_itemId == InventorySystem.CoreItemId && !(inventory.HasEdge && inventory.HasHandle))
                return "Requires the Edge and Handle";
            return _prompt;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            InventorySystem inventory = interactor.GetComponentInParent<InventorySystem>();
            if (!inventory.TryCollectComponent(_itemId)) return;
            _collected = true;
            OnInteracted?.Invoke(interactor);
            gameObject.SetActive(false);
        }

        public void CancelInteraction(GameObject interactor) { }

        /// <summary>Called by a puzzle completion event bridge; unlocks, never grants, this pickup.</summary>
        public void NotifyPuzzleCompleted() => _puzzleCompleted = true;
    }

    /// <summary>Optional adapter that unlocks a pickup after an existing puzzle solves.</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleCompletionPickupGate : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _puzzleSource;
        [SerializeField] private ComponentPickup _pickup;
        [SerializeField] private ComponentLockedDoor _door;
        private IPuzzleController _puzzle;

        private void OnEnable()
        {
            _puzzle = _puzzleSource as IPuzzleController;
            if (_puzzle == null || (_pickup == null && _door == null)) return;
            _puzzle.OnPuzzleSolved += HandlePuzzleSolved;
            if (_puzzle.IsSolved) HandlePuzzleSolved();
        }

        private void OnDisable()
        {
            if (_puzzle != null) _puzzle.OnPuzzleSolved -= HandlePuzzleSolved;
        }

        private void HandlePuzzleSolved()
        {
            _pickup?.NotifyPuzzleCompleted();
            _door?.NotifyPuzzleCompleted();
        }
    }

    /// <summary>Inspectable story note that reveals the dead teammate's weapon message.</summary>
    [DisallowMultipleComponent]
    public sealed class StoryNote : MonoBehaviour, IInteractable
    {
        public const string DeadTeammateMessage =
            "If you found this, I didn't make it. The DeathStorm Edge is in the armory and the Handle is by the relay. Keep them together. The Core is sealed behind the containment puzzle. Don't try to forge it until you have all three.";

        [SerializeField] private string _prompt = "Press E to read the note";
        [SerializeField] private MonoBehaviour _messageSink;
        private bool _read;

        public string InteractionPromptText => _read ? "Note read" : _prompt;
        public InteractionType Type => InteractionType.Inspection;
        public float HoldDurationRequired => 0f;
        public event Action<GameObject> OnInteracted;

        public bool CanInteract(GameObject interactor) => !_read && interactor != null &&
            interactor.GetComponentInParent<InventorySystem>() != null;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            InventorySystem inventory = interactor.GetComponentInParent<InventorySystem>();
            if (!inventory.DiscoverNote()) return;
            _read = true;
            if (_messageSink is IStoryMessageSink sink) sink.ShowStoryMessage(DeadTeammateMessage);
            else Debug.Log(DeadTeammateMessage, this);
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor) { }
    }

    /// <summary>Optional interface for an existing UI notification or journal system.</summary>
    public interface IStoryMessageSink
    {
        void ShowStoryMessage(string message);
    }

    /// <summary>Optional per-interactor prompt for objects whose prompt depends on inventory.</summary>
    public interface IInteractionPromptSource
    {
        string GetInteractionPrompt(GameObject interactor);
    }

    /// <summary>One-shot in-range teammate transmission trigger.</summary>
    [DisallowMultipleComponent]
    public sealed class TeammateTransmissionTrigger : MonoBehaviour, IInteractable
    {
        [TextArea(3, 6)]
        [SerializeField] private string _transmission = "This is Kira. I found the armory, but the relay is failing. Get the Edge and Handle first. The Core is still behind the containment puzzle. I'll keep trying to reach you.";
        [SerializeField] private string _prompt = "Press E to listen to transmission";
        [SerializeField] private MonoBehaviour _messageSink;
        private bool _played;

        public string InteractionPromptText => _played ? string.Empty : _prompt;
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;
        public event Action<GameObject> OnInteracted;
        public bool CanInteract(GameObject interactor) => !_played && interactor != null;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            _played = true;
            if (_messageSink is IStoryMessageSink sink) sink.ShowStoryMessage(_transmission);
            else Debug.Log(_transmission, this);
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor) { }
    }

    /// <summary>Door/vault that stays locked until required component flags and puzzle are satisfied.</summary>
    [DisallowMultipleComponent]
    public sealed class ComponentLockedDoor : MonoBehaviour, IInteractable, IInteractionPromptSource
    {
        [SerializeField] private bool _requireEdge = true;
        [SerializeField] private bool _requireHandle;
        [SerializeField] private bool _requireCore;
        [SerializeField] private bool _requirePuzzleCompletion;
        [SerializeField] private bool _puzzleCompleted;
        [SerializeField] private string _lockedPrompt = "Vault locked: missing required components";
        [SerializeField] private string _openPrompt = "Press E to open vault";
        [SerializeField] private GameObject _doorVisual;
        [SerializeField] private Collider2D _doorCollider;
        private bool _opened;

        public string InteractionPromptText => _opened ? string.Empty : _lockedPrompt;
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;
        public event Action<GameObject> OnInteracted;

        public bool CanInteract(GameObject interactor)
        {
            if (_opened || interactor == null) return false;
            InventorySystem inventory = interactor.GetComponentInParent<InventorySystem>();
            return inventory != null && CanUnlock(inventory);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            _opened = true;
            if (_doorVisual != null) _doorVisual.SetActive(false);
            if (_doorCollider != null) _doorCollider.enabled = false;
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor) { }
        public void NotifyPuzzleCompleted() => _puzzleCompleted = true;

        public string GetInteractionPrompt(GameObject interactor)
        {
            if (_opened) return string.Empty;
            InventorySystem inventory = interactor != null ? interactor.GetComponentInParent<InventorySystem>() : null;
            return CanUnlock(inventory) ? _openPrompt : _lockedPrompt;
        }

        private bool CanUnlock(InventorySystem inventory)
        {
            if (_requirePuzzleCompletion && !_puzzleCompleted) return false;
            if (inventory == null) return !_requireEdge && !_requireHandle && !_requireCore;
            return (!_requireEdge || inventory.HasEdge) && (!_requireHandle || inventory.HasHandle) &&
                (!_requireCore || inventory.HasCore);
        }
    }
}
