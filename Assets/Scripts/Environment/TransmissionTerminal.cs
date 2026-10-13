using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Interactive comms terminal in the starting corridor playing the teammate distress transmission.
    /// Implements IInteractable contract from OblivionsEdge.Core (Member 1).
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class TransmissionTerminal : MonoBehaviour, IInteractable
    {
        [Header("Transmission Content")]
        [SerializeField] private string _senderName = "Cpl. Hayes (Forward Recon)";
        [TextArea(3, 6)]
        [SerializeField] private string _transmissionMessage =
            "MAYDAY... If anyone can hear this, the specimen breached the reactor sector! " +
            "Conventional ballistics won't pierce its carapace. Find the weapon note in the next bay. " +
            "Retrieve the Edge and Handle fragments, extract the Core from the nest, and forge the DeathStorm! " +
            "Hurry... I can hear it in the ventilation shafts!";

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _staticChimeClip;

        [Header("Interaction State")]
        [SerializeField] private bool _hasPlayedTransmission;
        [SerializeField] private bool _autoPlayOnTrigger = true;

        public string InteractionPromptText => _hasPlayedTransmission ? "Replay Transmission [E]" : "Play Transmission [E]";
        public InteractionType Type => InteractionType.DirectPrompt;
        public float HoldDurationRequired => 0f;

        public string SenderName => _senderName;
        public string TransmissionMessage => _transmissionMessage;
        public bool HasPlayedTransmission => _hasPlayedTransmission;

        public event Action<GameObject> OnInteracted;
        public event Action<string, string> OnTransmissionPlayed; // (sender, message)

        private void Awake()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_autoPlayOnTrigger && !_hasPlayedTransmission && other.CompareTag("Player"))
            {
                Interact(other.gameObject);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            _hasPlayedTransmission = true;

            if (_audioSource != null && _staticChimeClip != null)
            {
                _audioSource.PlayOneShot(_staticChimeClip);
            }

            OnInteracted?.Invoke(interactor);
            OnTransmissionPlayed?.Invoke(_senderName, _transmissionMessage);
            Debug.Log($"[TransmissionTerminal] {_senderName}: {_transmissionMessage}");
        }

        public void CancelInteraction(GameObject interactor)
        {
            // Direct prompt has no hold state to cancel
        }
    }
}
