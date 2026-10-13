using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// In-world artifact item representing the Edge component relic guarded within the vault.
    /// Exposes interaction and claim callbacks designed for Member 9's inventory pipeline
    /// without concrete coupling to unfinished inventory storage classes.
    /// Implements <see cref="IInteractable"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Edge Component Reward")]
    public class EdgeComponentReward : MonoBehaviour, IInteractable
    {
        #region Inspector Configuration

        [Header("Artifact Metadata")]
        [Tooltip("Authoritative item ID consumed by Member 9's inventory system.")]
        [SerializeField] private string _itemId = "deathstorm_edge";

        [Tooltip("Display name presented on screen.")]
        [SerializeField] private string _displayName = "Edge Component Matrix";

        [Tooltip("Prompt displayed to the player upon interaction focus.")]
        [SerializeField] private string _promptText = "Extract Edge Component";

        [Header("Visual & Energy Telemetry")]
        [Tooltip("Primary renderer representing the floating/pedestal relic.")]
        [SerializeField] private SpriteRenderer _relicRenderer;

        [Tooltip("Glow aura object enabled once the vault unlocks.")]
        [SerializeField] private GameObject _unlockedGlowObject;

        [Tooltip("Particle system active while component is ready for extraction.")]
        [SerializeField] private ParticleSystem _ambientParticles;

        [Tooltip("Pickup burst particle system played on collection.")]
        [SerializeField] private ParticleSystem _pickupBurstParticles;

        [Header("Audio Feedback")]
        [Tooltip("Audio source for artifact sounds.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Audio clip played when component unlocks.")]
        [SerializeField] private AudioClip _unlockedSfx;

        [Tooltip("Audio clip played when player collects the component.")]
        [SerializeField] private AudioClip _pickupSfx;

        #endregion

        #region Private State

        private bool _isUnlocked = false;
        private bool _isClaimed = false;
        private Collider2D _collider;

        #endregion

        #region Events

        public event Action<GameObject> OnInteracted;
        /// <summary>
        /// Fired when this component is successfully claimed by player.
        /// Member 9 can subscribe to this event to execute inventory insertion.
        /// </summary>
        public event Action<string> OnComponentClaimed; // (itemId)

        #endregion

        #region IInteractable Properties

        public string InteractionPromptText => _promptText;
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;

        #endregion

        #region Public Properties

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public bool IsUnlocked => _isUnlocked;
        public bool IsClaimed => _isClaimed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            UpdateVisuals();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Unlocks the relic for extraction following successful puzzle resolution.
        /// </summary>
        public void Unlock()
        {
            if (_isUnlocked || _isClaimed) return;

            _isUnlocked = true;
            if (_collider != null) _collider.enabled = true;

            UpdateVisuals();

            if (_audioSource != null && _unlockedSfx != null)
            {
                _audioSource.PlayOneShot(_unlockedSfx);
            }
        }

        /// <summary>
        /// Resets the relic to locked standby state.
        /// </summary>
        public void ResetState()
        {
            _isUnlocked = false;
            _isClaimed = false;

            if (_collider != null) _collider.enabled = false;
            if (_relicRenderer != null) _relicRenderer.enabled = true;

            UpdateVisuals();
        }

        #endregion

        #region IInteractable Implementation

        public bool CanInteract(GameObject interactor)
        {
            return _isUnlocked && !_isClaimed;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            Claim(interactor);
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor)
        {
            // Instantaneous interaction
        }

        private void Claim(GameObject interactor)
        {
            _isClaimed = true;

            if (_collider != null) _collider.enabled = false;
            if (_relicRenderer != null) _relicRenderer.enabled = false;
            if (_unlockedGlowObject != null) _unlockedGlowObject.SetActive(false);
            if (_ambientParticles != null) _ambientParticles.Stop();

            if (_pickupBurstParticles != null)
            {
                _pickupBurstParticles.Play();
            }

            if (_audioSource != null && _pickupSfx != null)
            {
                _audioSource.PlayOneShot(_pickupSfx);
            }

            // Fire event for Member 9 inventory integration
            OnComponentClaimed?.Invoke(_itemId);
        }

        private void UpdateVisuals()
        {
            if (_unlockedGlowObject != null)
            {
                _unlockedGlowObject.SetActive(_isUnlocked && !_isClaimed);
            }

            if (_ambientParticles != null)
            {
                if (_isUnlocked && !_isClaimed) _ambientParticles.Play();
                else _ambientParticles.Stop();
            }

            if (_collider != null)
            {
                _collider.enabled = _isUnlocked && !_isClaimed;
            }
        }

        #endregion
    }
}
