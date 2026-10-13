using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Legendary DeathStorm Forge Altar integrated directly inside the alien arena.
    /// Emits blue-violet resonance lighting and acts as the workstation for forging the axe.
    /// Implements IInteractable.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class DeathStormForgeAltar : MonoBehaviour, IInteractable
    {
        [Header("Forge Atmospheric Effects")]
        [SerializeField] private AtmosphericLighting _resonanceLighting;
        [SerializeField] private ParticleSystem _etherealParticles;
        [SerializeField] private SpriteRenderer _anvilGlowSprite;
        [SerializeField] private Color _idleVioletColor = new Color(0.48f, 0.12f, 0.88f, 0.8f);
        [SerializeField] private Color _activeVioletColor = new Color(0.75f, 0.35f, 1.0f, 1.0f);

        [Header("Forge State")]
        [SerializeField] private bool _isForgeActive;

        public string InteractionPromptText => "Access DeathStorm Forge Altar [E]";
        public InteractionType Type => InteractionType.CraftingWorkbench;
        public float HoldDurationRequired => 0f;

        public bool IsForgeActive => _isForgeActive;

        public event Action<GameObject> OnInteracted;
        public event Action OnForgeEngaged;

        private void Start()
        {
            ApplyAtmosphere(false);
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            _isForgeActive = true;
            ApplyAtmosphere(true);

            OnInteracted?.Invoke(interactor);
            OnForgeEngaged?.Invoke();

            Debug.Log("[DeathStormForgeAltar] Player engaged the DeathStorm Forge Altar!");
        }

        public void CancelInteraction(GameObject interactor) { }

        public void SetForgeActive(bool active)
        {
            _isForgeActive = active;
            ApplyAtmosphere(active);
        }

        private void ApplyAtmosphere(bool active)
        {
            if (_resonanceLighting != null)
            {
                _resonanceLighting.ActiveMode = LightingMode.BlueVioletDeathStorm;
            }

            if (_anvilGlowSprite != null)
            {
                _anvilGlowSprite.color = active ? _activeVioletColor : _idleVioletColor;
            }

            if (_etherealParticles != null)
            {
                if (active && !_etherealParticles.isPlaying)
                {
                    _etherealParticles.Play();
                }
                else if (!active && _etherealParticles.isPlaying)
                {
                    _etherealParticles.Stop();
                }
            }
        }
    }
}
