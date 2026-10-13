using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Interactive blueprint terminal / research pad containing DeathStorm crafting instructions
    /// and boss weak-point strike vulnerability requirements.
    /// Implements IInteractable contract.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class WeaponLoreNote : MonoBehaviour, IInteractable
    {
        [Header("Document Content")]
        [SerializeField] private string _documentTitle = "PROJECT DEATHSTORM — SCHEMATIC OVERVIEW";
        [TextArea(4, 8)]
        [SerializeField] private string _documentBody =
            "WEAPON SPECIFICATION: 'DEATHSTORM' VOID-CLEAVING AXE\n" +
            "1. EDGE FRAGMENT: Located in the Upper Resonator Lab (North Wing).\n" +
            "2. HANDLE FRAGMENT: Secured in the Lower Power Vault (South Wing).\n" +
            "3. VOID CORE: Trapped inside the Alien Nest at the center of the Hive.\n\n" +
            "FORGE WARNING: Extracting the Core triggers nest collapse and enrages the beast. " +
            "You will have exactly 60 SECONDS to complete three-stage forging at the Forge Altar.\n\n" +
            "COMBAT PROTOCOL: The beast is impervious to standard strikes. Wait until it is stunned " +
            "in VulnerableStun state, aim true toward its glowing cranial weak-point, and strike!";

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _glowIndicator;
        [SerializeField] private Color _unreadGlowColor = Color.yellow;
        [SerializeField] private Color _readGlowColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        [Header("State")]
        [SerializeField] private bool _isRead;

        public string InteractionPromptText => _isRead ? "Re-read Weapon Note [E]" : "Read Weapon Note [E]";
        public InteractionType Type => InteractionType.Inspection;
        public float HoldDurationRequired => 0f;

        public string DocumentTitle => _documentTitle;
        public string DocumentBody => _documentBody;
        public bool IsRead => _isRead;

        public event Action<GameObject> OnInteracted;
        public event Action<string, string> OnNoteInspected; // (title, body)

        private void Start()
        {
            UpdateVisuals();
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            _isRead = true;
            UpdateVisuals();

            OnInteracted?.Invoke(interactor);
            OnNoteInspected?.Invoke(_documentTitle, _documentBody);
            Debug.Log($"[WeaponLoreNote] {_documentTitle}\n{_documentBody}");
        }

        public void CancelInteraction(GameObject interactor) { }

        private void UpdateVisuals()
        {
            if (_glowIndicator != null)
            {
                _glowIndicator.color = _isRead ? _readGlowColor : _unreadGlowColor;
            }
        }
    }
}
