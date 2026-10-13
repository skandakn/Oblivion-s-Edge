using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    public enum PuzzleRoomType
    {
        EdgeResonatorRoom,
        HandlePowerRoom
    }

    /// <summary>
    /// Interactive puzzle pedestal and pickup container in the Edge and Handle puzzle rooms.
    /// Implements IInteractable contract.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class PuzzlePedestalStation : MonoBehaviour, IInteractable
    {
        [Header("Room & Fragment")]
        [SerializeField] private PuzzleRoomType _roomType = PuzzleRoomType.EdgeResonatorRoom;
        [SerializeField] private string _componentItemId = "deathstorm_edge";
        [SerializeField] private string _componentDisplayName = "DeathStorm Edge Fragment";

        [Header("Prompts & Descriptions")]
        [SerializeField] private string _unsolvedPrompt = "Solve Alignment Lock & Retrieve Fragment [E]";
        [SerializeField] private string _solvedPrompt = "Pedestal Empty (Fragment Retrieved)";

        [Header("Visual Indicators")]
        [SerializeField] private SpriteRenderer _fragmentVisual;
        [SerializeField] private SpriteRenderer _pedestalStatusGlow;
        [SerializeField] private Color _activeGlowColor = Color.cyan;
        [SerializeField] private Color _solvedGlowColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);

        [Header("State")]
        [SerializeField] private bool _isFragmentRetrieved;

        public string InteractionPromptText => _isFragmentRetrieved ? _solvedPrompt : _unsolvedPrompt;
        public InteractionType Type => InteractionType.PuzzleTerminal;
        public float HoldDurationRequired => 0.5f;

        public PuzzleRoomType RoomType => _roomType;
        public string ComponentItemId => _componentItemId;
        public string ComponentDisplayName => _componentDisplayName;
        public bool IsFragmentRetrieved => _isFragmentRetrieved;

        public event Action<GameObject> OnInteracted;
        public event Action<string> OnFragmentCollected; // (itemId)

        private void Start()
        {
            UpdateVisuals();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_isFragmentRetrieved;
        }

        public void Interact(GameObject interactor)
        {
            if (_isFragmentRetrieved) return;

            _isFragmentRetrieved = true;
            UpdateVisuals();

            OnInteracted?.Invoke(interactor);
            OnFragmentCollected?.Invoke(_componentItemId);

            Debug.Log($"[PuzzlePedestalStation] Player retrieved {_componentDisplayName} ({_componentItemId}) from {_roomType}!");
        }

        public void CancelInteraction(GameObject interactor) { }

        private void UpdateVisuals()
        {
            if (_fragmentVisual != null)
            {
                _fragmentVisual.enabled = !_isFragmentRetrieved;
            }

            if (_pedestalStatusGlow != null)
            {
                _pedestalStatusGlow.color = _isFragmentRetrieved ? _solvedGlowColor : _activeGlowColor;
            }
        }
    }
}
