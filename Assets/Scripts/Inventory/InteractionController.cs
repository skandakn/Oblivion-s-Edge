using System.Collections.Generic;
using OblivionsEdge.Core;
using OblivionsEdge.UI;
using UnityEngine;

namespace OblivionsEdge.Inventory
{
    /// <summary>Finds the nearest in-range IInteractable and invokes it on E.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _interactionRange = 1.6f;
        [SerializeField] private LayerMask _interactableLayers = ~0;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private MonoBehaviour _hudSource;

        private IHUDView _hud;
        private IInteractable _current;
        private string _currentPrompt;
        private readonly List<IInteractable> _candidates = new List<IInteractable>();

        public float InteractionRange => _interactionRange;
        public IInteractable CurrentTarget => _current;

        private void Awake()
        {
            _hud = _hudSource as IHUDView;
            if (_hud == null)
            {
                MonoBehaviour[] siblings = GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour component in siblings)
                    if (component is IHUDView view) { _hud = view; break; }
            }
        }

        private void Update()
        {
            RefreshTarget();
            if (Input.GetKeyDown(_interactKey)) TryInteract();
        }

        /// <summary>Refreshes target selection; public for deterministic scene/test setup.</summary>
        public void RefreshTarget()
        {
            _candidates.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _interactionRange, _interactableLayers);
            float closestDistance = float.PositiveInfinity;
            IInteractable closest = null;
            foreach (Collider2D hit in hits)
            {
                MonoBehaviour[] components = hit.GetComponentsInParent<MonoBehaviour>();
                foreach (MonoBehaviour component in components)
                {
                    if (!(component is IInteractable interactable) || _candidates.Contains(interactable)) continue;
                    _candidates.Add(interactable);
                    float distance = Vector2.Distance(transform.position, hit.ClosestPoint(transform.position));
                    if (distance < closestDistance) { closestDistance = distance; closest = interactable; }
                }
            }

            _current = closest;
            UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            if (_hud == null) return;
            if (_current == null)
            {
                if (_currentPrompt != null) _hud.HideInteractionPrompt();
                _currentPrompt = null;
                return;
            }

            string prompt = _current is IInteractionPromptSource contextual
                ? contextual.GetInteractionPrompt(gameObject)
                : _current.InteractionPromptText;
            if (_currentPrompt == prompt) return;
            _currentPrompt = prompt;
            if (string.IsNullOrEmpty(prompt)) _hud.HideInteractionPrompt();
            else _hud.ShowInteractionPrompt(prompt);
        }

        public bool TryInteract()
        {
            RefreshTarget();
            if (_current == null || !_current.CanInteract(gameObject)) return false;
            _current.Interact(gameObject);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}
