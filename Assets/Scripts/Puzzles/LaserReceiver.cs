using System;
using UnityEngine;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Target terminal sensor for laser puzzle in OBLIVION'S EDGE.
    /// Detects when the laser beam strikes its receptor aperture, validates incoming angle,
    /// and activates visual and sound telemetry feedback.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Laser Receiver")]
    public class LaserReceiver : MonoBehaviour
    {
        #region Inspector Configuration

        [Header("Receiver Identification")]
        [Tooltip("Identifier for this receiver terminal.")]
        [SerializeField] private string _receiverId = "receiver_vault_primary";

        [Header("Acceptance Angle Constraints")]
        [Tooltip("If true, incoming laser must enter within acceptance cone of the aperture normal.")]
        [SerializeField] private bool _requireDirectionalAperture = true;

        [Tooltip("Outward pointing normal direction of the sensor aperture in local space.")]
        [SerializeField] private Vector2 _localApertureNormal = Vector2.right;

        [Tooltip("Maximum allowable angular divergence in degrees between incoming beam and sensor intake.")]
        [SerializeField] [Range(5f, 90f)] private float _acceptanceAngleDegrees = 45f;

        [Header("Visual Feedback Indicators")]
        [Tooltip("Main indicator sprite or light.")]
        [SerializeField] private SpriteRenderer _indicatorRenderer;

        [Tooltip("Color displayed when receiver is unpowered / beam misaligned.")]
        [SerializeField] private Color _colorInactive = new Color(0.8f, 0.15f, 0.15f, 0.9f); // Dim Red

        [Tooltip("Color displayed when receiver receives valid laser beam.")]
        [SerializeField] private Color _colorActive = new Color(0.2f, 0.95f, 0.4f, 1f); // Bright Green

        [Tooltip("Continuous particle system or energy loop activated upon power-on.")]
        [SerializeField] private ParticleSystem _powerParticles;

        [Tooltip("Glow GameObject toggled on when active.")]
        [SerializeField] private GameObject _powerGlowObject;

        [Header("Audio Feedback")]
        [Tooltip("Audio source for sound cues.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Sound clip played on initial laser contact.")]
        [SerializeField] private AudioClip _beamConnectedSfx;

        [Tooltip("Sound clip played when laser beam is lost.")]
        [SerializeField] private AudioClip _beamLostSfx;

        #endregion

        #region Private State

        private bool _isReceivingBeamThisFrame = false;
        private bool _isActive = false;
        private float _lastHitTime = -1f;

        #endregion

        #region Events

        public event Action OnBeamReceived;
        public event Action OnBeamLost;
        public event Action<bool> OnReceiverStateChanged;

        #endregion

        #region Public Properties

        public string ReceiverId => _receiverId;
        public bool IsReceivingBeam => _isActive;

        /// <summary>
        /// World-space outward normal of the receiver aperture.
        /// </summary>
        public Vector2 WorldApertureNormal => transform.TransformDirection(_localApertureNormal).normalized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            UpdateVisuals(false, playAudio: false);
        }

        private void LateUpdate()
        {
            // Evaluate state transition based on whether laser struck this frame
            bool currentlyHit = _isReceivingBeamThisFrame;

            if (currentlyHit != _isActive)
            {
                SetReceiverActive(currentlyHit);
            }

            // Reset frame accumulator for next frame's raycasts
            _isReceivingBeamThisFrame = false;
        }

        #endregion

        #region Beam Detection API

        /// <summary>
        /// Called by LaserEmitter each frame when a laser beam raycast hits this receiver.
        /// </summary>
        /// <param name="hitPoint">Impact coordinate in world space.</param>
        /// <param name="incomingDirection">Direction vector of the arriving laser.</param>
        /// <returns>True if the beam is accepted and powers the receiver.</returns>
        public bool ReceiveBeam(Vector2 hitPoint, Vector2 incomingDirection)
        {
            if (_requireDirectionalAperture)
            {
                // Aperture accepts incoming beams traveling towards it (i.e. opposite to aperture normal)
                Vector2 apertureNormal = WorldApertureNormal;
                Vector2 beamDir = incomingDirection.normalized;

                // Angle between beam direction and inward aperture (-apertureNormal)
                float angle = Vector2.Angle(beamDir, -apertureNormal);
                if (angle > _acceptanceAngleDegrees)
                {
                    // Beam struck outside the valid intake cone
                    return false;
                }
            }

            _isReceivingBeamThisFrame = true;
            _lastHitTime = Time.time;
            return true;
        }

        private void SetReceiverActive(bool active)
        {
            _isActive = active;
            UpdateVisuals(_isActive, playAudio: true);

            if (_isActive)
            {
                OnBeamReceived?.Invoke();
            }
            else
            {
                OnBeamLost?.Invoke();
            }

            OnReceiverStateChanged?.Invoke(_isActive);
        }

        private void UpdateVisuals(bool active, bool playAudio)
        {
            if (_indicatorRenderer != null)
            {
                _indicatorRenderer.color = active ? _colorActive : _colorInactive;
            }

            if (_powerGlowObject != null)
            {
                _powerGlowObject.SetActive(active);
            }

            if (_powerParticles != null)
            {
                if (active) _powerParticles.Play();
                else _powerParticles.Stop();
            }

            if (playAudio && _audioSource != null)
            {
                AudioClip clipToPlay = active ? _beamConnectedSfx : _beamLostSfx;
                if (clipToPlay != null)
                {
                    _audioSource.PlayOneShot(clipToPlay);
                }
            }
        }

        #endregion

        #region Reset API

        /// <summary>
        /// Resets the receiver state to inactive.
        /// </summary>
        public void ResetReceiver()
        {
            _isReceivingBeamThisFrame = false;
            SetReceiverActive(false);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isActive ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            if (_requireDirectionalAperture)
            {
                Vector3 normal = WorldApertureNormal;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, normal * 1.0f);
            }
        }

        #endregion
    }
}
