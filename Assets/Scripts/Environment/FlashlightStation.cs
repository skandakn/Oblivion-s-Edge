using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Flashlight station in Sector 1 (Derelict Crew Quarters).
    /// Provides the player with emergency illumination to cut through the survival horror darkness.
    /// Implements IInteractable.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class FlashlightStation : MonoBehaviour, IInteractable
    {
        [Header("Flashlight Settings")]
        [SerializeField] private string _promptText = "Take Emergency Flashlight [E]";
        [SerializeField] private Light _spotlight;
        [SerializeField] private SpriteRenderer _propVisual;
        [SerializeField] private Color _beamColor = new Color(0.9f, 0.95f, 1f, 1f);

        [Header("State")]
        [SerializeField] private bool _isCollected;

        public string InteractionPromptText => _isCollected ? "Flashlight Equipped" : _promptText;
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;
        public bool IsCollected => _isCollected;

        public event Action<GameObject> OnInteracted;
        public event Action OnFlashlightEquipped;

        public bool CanInteract(GameObject interactor) => !_isCollected;

        public void Interact(GameObject interactor)
        {
            if (_isCollected) return;
            _isCollected = true;

            if (_propVisual != null)
            {
                _propVisual.enabled = false;
            }

            if (_spotlight != null)
            {
                _spotlight.color = _beamColor;
                _spotlight.enabled = true;
            }

            OnInteracted?.Invoke(interactor);
            OnFlashlightEquipped?.Invoke();
            Debug.Log("[FlashlightStation] Emergency flashlight equipped! Survival horror visibility restored.");
        }

        public void CancelInteraction(GameObject interactor) { }
    }
}
