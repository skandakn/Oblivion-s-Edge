using System;
using System.Collections.Generic;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Optical laser emitter for puzzle sequences in OBLIVION'S EDGE.
    /// Projects a continuous raycast beam, iteratively reflecting off mirrors using
    /// r = d - 2(d · n)n, and renders the beam trajectory via <see cref="LineRenderer"/>.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Laser Emitter")]
    public class LaserEmitter : MonoBehaviour
    {
        #region Inspector Configuration

        [Header("Emission Properties")]
        [Tooltip("Child transform marking laser origin nozzle. If null, uses emitter transform.")]
        [SerializeField] private Transform _nozzlePoint;

        [Tooltip("Local direction along which the laser is emitted.")]
        [SerializeField] private Vector2 _localEmissionDirection = Vector2.right;

        [Tooltip("Maximum allowed bounces/reflections.")]
        [SerializeField] [Range(1, 32)] private int _maxBounces = 16;

        [Tooltip("Maximum travel distance per laser segment in units.")]
        [SerializeField] [Range(1f, 100f)] private float _maxSegmentDistance = 30f;

        [Tooltip("Offset applied along reflection direction to prevent re-colliding with origin surface.")]
        [SerializeField] [Range(0.001f, 0.05f)] private float _skinWidth = 0.005f;

        [Tooltip("Layer mask filter for laser raycasts (Environment, Interactable mirrors, etc.).")]
        [SerializeField] private LayerMask _collisionMask;

        [Header("Beam Telemetry & Visuals")]
        [Tooltip("Color of the laser beam under standard searching/standby conditions.")]
        [SerializeField] private Color _colorStandby = new Color(0.1f, 0.75f, 1f, 0.95f); // Vibrant Cyan

        [Tooltip("Color of the laser beam when successfully striking the receiver.")]
        [SerializeField] private Color _colorSolved = new Color(0.15f, 1f, 0.45f, 1f); // Neon Green

        [Tooltip("Color of the laser beam when blocked or malfunctioning.")]
        [SerializeField] private Color _colorBlocked = new Color(1f, 0.35f, 0.1f, 0.95f); // Amber / Red

        [Tooltip("LineRenderer beam width.")]
        [SerializeField] [Range(0.02f, 0.5f)] private float _beamWidth = 0.08f;

        [Tooltip("If true, laser pulses slightly for enhanced visual dynamic feedback.")]
        [SerializeField] private bool _enablePulseAnimation = true;

        [Tooltip("Frequency of beam pulse pulsation.")]
        [SerializeField] [Range(1f, 20f)] private float _pulseFrequency = 6f;

        [Header("State")]
        [Tooltip("Whether the laser is actively projecting.")]
        [SerializeField] private bool _isEmitting = true;

        #endregion

        #region Private Fields

        private LineRenderer _lineRenderer;
        private readonly List<Vector3> _pathPoints = new List<Vector3>(32);
        private bool _isHittingReceiverThisFrame = false;
        private LaserReceiver _lastHitReceiver = null;

        #endregion

        #region Events

        public event Action<LaserReceiver> OnReceiverStruck;
        public event Action OnReceiverUnstruck;

        #endregion

        #region Public Properties

        public bool IsEmitting => _isEmitting;
        public bool IsHittingReceiver => _isHittingReceiverThisFrame;
        public LaserReceiver CurrentTargetReceiver => _lastHitReceiver;
        public IReadOnlyList<Vector3> CurrentPathPoints => _pathPoints;

        /// <summary>
        /// World space origin point of the laser.
        /// </summary>
        public Vector2 OriginPosition => _nozzlePoint != null ? (Vector2)_nozzlePoint.position : (Vector2)transform.position;

        /// <summary>
        /// World space direction vector of the emitted beam.
        /// </summary>
        public Vector2 WorldEmissionDirection
        {
            get
            {
                if (_nozzlePoint != null)
                {
                    return _nozzlePoint.TransformDirection(_localEmissionDirection).normalized;
                }
                return transform.TransformDirection(_localEmissionDirection).normalized;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            InitializeLineRenderer();

            // Default collision mask fallback to Environment and Interactable if unset
            if (_collisionMask.value == 0)
            {
                _collisionMask = LayerMask.GetMask(
                    GameConstants.LayerNameEnvironment,
                    GameConstants.LayerNameInteractable,
                    GameConstants.LayerNameDefault
                );
                if (_collisionMask.value == 0)
                {
                    _collisionMask = ~0; // fallback to all
                }
            }
        }

        private void Update()
        {
            if (!_isEmitting)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            TraceAndRenderBeam();
            AnimateBeamPulse();
        }

        #endregion

        #region Laser Tracing & Specular Reflection Pipeline

        /// <summary>
        /// Traces laser path with specular reflections r = d - 2(d · n)n and commits vertices to LineRenderer.
        /// </summary>
        public void TraceAndRenderBeam()
        {
            _pathPoints.Clear();

            Vector2 currentOrigin = OriginPosition;
            Vector2 currentDirection = WorldEmissionDirection;

            _pathPoints.Add(currentOrigin);

            bool struckReceiver = false;
            LaserReceiver struckReceiverComponent = null;

            for (int bounce = 0; bounce <= _maxBounces; bounce++)
            {
                RaycastHit2D hit = Physics2D.Raycast(
                    currentOrigin,
                    currentDirection,
                    _maxSegmentDistance,
                    _collisionMask
                );

                if (hit.collider != null)
                {
                    Vector2 hitPoint = hit.point;
                    _pathPoints.Add(hitPoint);

                    // Check for Rotatable Mirror
                    RotatableMirror mirror = hit.collider.GetComponent<RotatableMirror>() ??
                                             hit.collider.GetComponentInParent<RotatableMirror>();

                    if (mirror != null && mirror.IsActive)
                    {
                        // Calculate specular reflection using r = d - 2(d · n)n
                        if (mirror.TryReflectBeam(hitPoint, currentDirection, hit.normal, out Vector2 reflectedDirection))
                        {
                            currentOrigin = hitPoint + (reflectedDirection * _skinWidth);
                            currentDirection = reflectedDirection;
                            continue; // Proceed to next bounce
                        }
                        else
                        {
                            // Absorbed by non-reflective backing of the mirror
                            break;
                        }
                    }

                    // Check for Laser Receiver
                    LaserReceiver receiver = hit.collider.GetComponent<LaserReceiver>() ??
                                             hit.collider.GetComponentInParent<LaserReceiver>();

                    if (receiver != null)
                    {
                        if (receiver.ReceiveBeam(hitPoint, currentDirection))
                        {
                            struckReceiver = true;
                            struckReceiverComponent = receiver;
                        }
                        break; // Beam terminated and absorbed at receiver
                    }

                    // Wall / obstacle collision terminates beam
                    break;
                }
                else
                {
                    // No obstacle hit; project beam to maximum distance
                    Vector2 endPoint = currentOrigin + (currentDirection * _maxSegmentDistance);
                    _pathPoints.Add(endPoint);
                    break;
                }
            }

            // Update Receiver connection telemetry
            if (struckReceiver != _isHittingReceiverThisFrame || struckReceiverComponent != _lastHitReceiver)
            {
                _isHittingReceiverThisFrame = struckReceiver;
                _lastHitReceiver = struckReceiverComponent;

                if (_isHittingReceiverThisFrame)
                {
                    OnReceiverStruck?.Invoke(_lastHitReceiver);
                }
                else
                {
                    OnReceiverUnstruck?.Invoke();
                }
            }

            // Render path points
            ApplyPathToLineRenderer();
            ApplyBeamColor(struckReceiver);
        }

        private void InitializeLineRenderer()
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _beamWidth;
            _lineRenderer.endWidth = _beamWidth;
            _lineRenderer.sortingLayerName = GameConstants.SortingProjectiles;

            // Simple glowing material setup if material is default
            if (_lineRenderer.sharedMaterial == null)
            {
                Shader defaultShader = Shader.Find("Sprites/Default");
                if (defaultShader != null)
                {
                    _lineRenderer.material = new Material(defaultShader);
                }
            }
        }

        private void ApplyPathToLineRenderer()
        {
            _lineRenderer.positionCount = _pathPoints.Count;
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                _lineRenderer.SetPosition(i, _pathPoints[i]);
            }
        }

        private void ApplyBeamColor(bool connected)
        {
            Color targetColor = connected ? _colorSolved : _colorStandby;
            _lineRenderer.startColor = targetColor;
            _lineRenderer.endColor = targetColor;
        }

        private void AnimateBeamPulse()
        {
            if (!_enablePulseAnimation) return;

            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * _pulseFrequency);
            float dynamicWidth = _beamWidth * pulse;
            _lineRenderer.startWidth = dynamicWidth;
            _lineRenderer.endWidth = dynamicWidth;
        }

        #endregion

        #region Public Control API

        /// <summary>
        /// Enables or disables laser emission.
        /// </summary>
        public void SetEmitting(bool emitting)
        {
            _isEmitting = emitting;
            if (!_isEmitting)
            {
                _lineRenderer.positionCount = 0;
                _isHittingReceiverThisFrame = false;
                _lastHitReceiver = null;
            }
        }

        /// <summary>
        /// Reconfigures emission direction at runtime.
        /// </summary>
        public void SetEmissionDirection(Vector2 direction)
        {
            _localEmissionDirection = direction.normalized;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Vector3 start = OriginPosition;
            Vector3 dir = WorldEmissionDirection;
            Gizmos.DrawRay(start, dir * 2f);
            Gizmos.DrawWireSphere(start, 0.2f);
        }

        #endregion
    }
}
