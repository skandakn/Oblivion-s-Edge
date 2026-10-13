using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Core extraction station situated inside the alien nest.
    /// Extracting the Core triggers the 60-second shared crafting countdown
    /// and transitions the environment into an emergency horror state.
    /// Implements IInteractable.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class CoreExtractionPoint : MonoBehaviour, IInteractable
    {
        [Header("Extraction Settings")]
        [SerializeField] private string _coreItemId = "deathstorm_core";
        [SerializeField] private float _extractionHoldTime = 1.0f;

        [Header("Visual & Audio Cues")]
        [SerializeField] private SpriteRenderer _coreOrbVisual;
        [SerializeField] private AtmosphericLighting _emergencyLighting;
        [SerializeField] private AudioAmbienceController _audioController;

        [Header("State")]
        [SerializeField] private bool _isCoreExtracted;

        public string InteractionPromptText => _isCoreExtracted ? "Core Extracted" : "Extract Bio-Core [Hold E]";
        public InteractionType Type => InteractionType.HoldPrompt;
        public float HoldDurationRequired => _extractionHoldTime;

        public string CoreItemId => _coreItemId;
        public bool IsCoreExtracted => _isCoreExtracted;

        public event Action<GameObject> OnInteracted;
        public event Action OnCoreExtracted;

        private void Start()
        {
            UpdateVisuals();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_isCoreExtracted;
        }

        public void Interact(GameObject interactor)
        {
            if (_isCoreExtracted) return;

            _isCoreExtracted = true;
            UpdateVisuals();

            // Trigger environmental alarm cues
            if (_emergencyLighting != null)
            {
                _emergencyLighting.ActiveMode = LightingMode.RedEmergencyStrobe;
            }
            if (_audioController != null)
            {
                _audioController.TriggerAttackWarning(true);
            }

            OnInteracted?.Invoke(interactor);
            OnCoreExtracted?.Invoke();

            Debug.Log("[CoreExtractionPoint] Void Core extracted! Emergency alarm engaged!");
        }

        public void CancelInteraction(GameObject interactor)
        {
            // Hold interaction canceled
        }

        private void UpdateVisuals()
        {
            if (_coreOrbVisual != null)
            {
                _coreOrbVisual.enabled = !_isCoreExtracted;
            }
        }
    }
}
