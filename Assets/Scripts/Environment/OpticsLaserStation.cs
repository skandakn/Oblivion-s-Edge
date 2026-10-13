using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Interactive laser reflection puzzle station in Sector 2 (Optics Lab).
    /// Aligning the optical prisms reflects the laser beam to unlock the DeathStorm Edge fragment.
    /// Implements IInteractable.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class OpticsLaserStation : MonoBehaviour, IInteractable
    {
        [Header("Optics Configuration")]
        [SerializeField] private string _unsolvedPrompt = "Calibrate Optical Reflection Mirrors [E]";
        [SerializeField] private string _solvedPrompt = "Optics Aligned — Edge Fragment Unlocked";
        [SerializeField] private string _edgeItemId = "deathstorm_edge";

        [Header("Visuals & Lighting")]
        [SerializeField] private LineRenderer _laserBeam;
        [SerializeField] private Transform[] _mirrorPoints;
        [SerializeField] private SpriteRenderer _edgeFragmentVisual;
        [SerializeField] private Color _laserColor = new Color(0.1f, 0.9f, 0.85f, 1f); // Cyan laser

        [Header("State")]
        [SerializeField] private bool _isAligned;
        [SerializeField] private bool _isFragmentRetrieved;

        public string InteractionPromptText => _isFragmentRetrieved ? "Optics Lab Calibration Complete" :
                                               (_isAligned ? "Retrieve DeathStorm Edge [E]" : _unsolvedPrompt);
        public InteractionType Type => InteractionType.PuzzleTerminal;
        public float HoldDurationRequired => 0f;

        public bool IsAligned => _isAligned;
        public bool IsFragmentRetrieved => _isFragmentRetrieved;

        public event Action<GameObject> OnInteracted;
        public event Action OnOpticsAligned;
        public event Action<string> OnEdgeCollected;

        private void Start()
        {
            UpdateVisuals();
        }

        public bool CanInteract(GameObject interactor) => !_isFragmentRetrieved;

        public void Interact(GameObject interactor)
        {
            if (!_isAligned)
            {
                _isAligned = true;
                UpdateVisuals();
                OnInteracted?.Invoke(interactor);
                OnOpticsAligned?.Invoke();
                Debug.Log("[OpticsLaserStation] Laser beam reflected through all prisms! Edge socket unlocked.");
            }
            else if (!_isFragmentRetrieved)
            {
                _isFragmentRetrieved = true;
                UpdateVisuals();
                OnInteracted?.Invoke(interactor);
                OnEdgeCollected?.Invoke(_edgeItemId);
                Debug.Log("[OpticsLaserStation] DeathStorm Edge retrieved from Optics Lab!");
            }
        }

        public void CancelInteraction(GameObject interactor) { }

        private void UpdateVisuals()
        {
            if (_laserBeam != null && _mirrorPoints != null && _mirrorPoints.Length > 0)
            {
                _laserBeam.enabled = _isAligned;
                _laserBeam.startColor = _laserColor;
                _laserBeam.endColor = _laserColor;
                _laserBeam.positionCount = _mirrorPoints.Length;
                for (int i = 0; i < _mirrorPoints.Length; i++)
                {
                    if (_mirrorPoints[i] != null)
                    {
                        _laserBeam.SetPosition(i, _mirrorPoints[i].position);
                    }
                }
            }

            if (_edgeFragmentVisual != null)
            {
                _edgeFragmentVisual.enabled = !_isFragmentRetrieved;
            }
        }
    }
}
