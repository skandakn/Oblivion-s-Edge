using UnityEngine;

namespace OblivionsEdge.Core
{
    /// <summary>
    /// Type of interaction required to trigger an interactable element.
    /// </summary>
    public enum InteractionType
    {
        DirectPrompt,
        HoldPrompt,
        Inspection,
        PuzzleTerminal,
        ContainerLoot,
        CraftingWorkbench
    }

    /// <summary>
    /// Contract for all in-world objects the player or AI can interact with (doors, terminals, loot boxes, NPCs).
    /// </summary>
    public interface IInteractable
    {
        string InteractionPromptText { get; }
        InteractionType Type { get; }
        float HoldDurationRequired { get; }

        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
        void CancelInteraction(GameObject interactor);

        event System.Action<GameObject> OnInteracted;
    }
}
