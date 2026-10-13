using System;
using System.Collections;
using UnityEngine;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Reinforced pneumatic vault barrier guarding the Edge component in OBLIVION'S EDGE.
    /// Responds to puzzle completion by retracting blast doors, updating access lighting,
    /// and disabling passage-blocking colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Vault Door")]
    public class VaultDoor : MonoBehaviour
    {
        #region Inspector Configuration

        [Header("Door Movement Mechanics")]
        [Tooltip("Transform representing the physical sliding blast door slab.")]
        [SerializeField] private Transform _doorSlab;

        [Tooltip("Local offset applied to door slab when fully retracted.")]
        [SerializeField] private Vector3 _openedOffset = new Vector3(0f, 2.5f, 0f);

        [Tooltip("Seconds required to transition from closed to open.")]
        [SerializeField] [Range(0.2f, 5f)] private float _openDuration = 1.2f;

        [Tooltip("Collider blocking player passage when closed. Disabled upon opening.")]
        [SerializeField] private Collider2D _barrierCollider;

        [Header("Access Indicators & Visuals")]
        [Tooltip("Indicator light / sprite display.")]
        [SerializeField] private SpriteRenderer _statusLight;

        [Tooltip("Locked indicator color (Red).")]
        [SerializeField] private Color _colorLocked = new Color(0.9f, 0.1f, 0.1f, 1f);

        [Tooltip("Unlocked indicator color (Green).")]
        [SerializeField] private Color _colorUnlocked = new Color(0.1f, 0.95f, 0.3f, 1f);

        [Tooltip("Particle system triggered upon door unsealing / pneumatic vent.")]
        [SerializeField] private ParticleSystem _ventSteamParticles;

        [Header("Audio Feedback")]
        [Tooltip("Audio source for mechanical playback.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Audio clip played when vault unseals and opens.")]
        [SerializeField] private AudioClip _openSfx;

        [Tooltip("Audio clip played if vault closes / locks.")]
        [SerializeField] private AudioClip _closeSfx;

        #endregion

        #region Private State

        private Vector3 _initialLocalPos;
        private Vector3 _targetLocalPos;
        private Coroutine _movementRoutine;
        private bool _isOpen = false;

        #endregion

        #region Events

        public event Action OnVaultOpened;
        public event Action OnVaultClosed;

        #endregion

        #region Public Properties

        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_doorSlab != null)
            {
                _initialLocalPos = _doorSlab.localPosition;
            }
            else
            {
                _initialLocalPos = Vector3.zero;
            }

            _targetLocalPos = _initialLocalPos + _openedOffset;
            SetVisualState(false);
        }

        #endregion

        #region Public Control API

        /// <summary>
        /// Initiates vault unsealing sequence.
        /// </summary>
        public void OpenVault()
        {
            if (_isOpen) return;
            _isOpen = true;

            if (_barrierCollider != null)
            {
                _barrierCollider.enabled = false;
            }

            SetVisualState(true);

            if (_ventSteamParticles != null)
            {
                _ventSteamParticles.Play();
            }

            if (_audioSource != null && _openSfx != null)
            {
                _audioSource.PlayOneShot(_openSfx);
            }

            if (_movementRoutine != null) StopCoroutine(_movementRoutine);
            _movementRoutine = StartCoroutine(AnimateDoor(_doorSlab != null ? _doorSlab.localPosition : _initialLocalPos, _targetLocalPos, () =>
            {
                OnVaultOpened?.Invoke();
            }));
        }

        /// <summary>
        /// Reseals the vault barrier.
        /// </summary>
        public void CloseVault()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (_barrierCollider != null)
            {
                _barrierCollider.enabled = true;
            }

            SetVisualState(false);

            if (_audioSource != null && _closeSfx != null)
            {
                _audioSource.PlayOneShot(_closeSfx);
            }

            if (_movementRoutine != null) StopCoroutine(_movementRoutine);
            _movementRoutine = StartCoroutine(AnimateDoor(_doorSlab != null ? _doorSlab.localPosition : _targetLocalPos, _initialLocalPos, () =>
            {
                OnVaultClosed?.Invoke();
            }));
        }

        /// <summary>
        /// Instantly snaps the vault to closed state.
        /// </summary>
        public void ResetVault()
        {
            if (_movementRoutine != null)
            {
                StopCoroutine(_movementRoutine);
                _movementRoutine = null;
            }

            _isOpen = false;

            if (_doorSlab != null)
            {
                _doorSlab.localPosition = _initialLocalPos;
            }

            if (_barrierCollider != null)
            {
                _barrierCollider.enabled = true;
            }

            SetVisualState(false);
        }

        #endregion

        #region Helpers

        private void SetVisualState(bool unlocked)
        {
            if (_statusLight != null)
            {
                _statusLight.color = unlocked ? _colorUnlocked : _colorLocked;
            }
        }

        private IEnumerator AnimateDoor(Vector3 fromPos, Vector3 toPos, Action onComplete)
        {
            if (_doorSlab == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _openDuration);
                _doorSlab.localPosition = Vector3.Lerp(fromPos, toPos, t);
                yield return null;
            }

            _doorSlab.localPosition = toPos;
            onComplete?.Invoke();
        }

        #endregion
    }
}
