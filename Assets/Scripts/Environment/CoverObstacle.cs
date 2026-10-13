using UnityEngine;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Physical cover barrier inside corridors and boss arena.
    /// Provides physical blocking colliders for Rigidbody2D physics and
    /// occludes line-of-sight for stealth evasion against alien attacks/pounces.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CoverObstacle : MonoBehaviour
    {
        [Header("Cover Properties")]
        [SerializeField] private string _coverId = "BulkheadCrate";
        [SerializeField] private bool _providesStealth = true;
        [SerializeField] private float _coverHeight = 1.8f;
        [SerializeField] private Collider2D _physicalCollider;
        [SerializeField] private Collider2D _hidingZoneTrigger;

        [Header("State")]
        [SerializeField] private bool _isPlayerTakingCover;

        public string CoverId => _coverId;
        public bool ProvidesStealth => _providesStealth;
        public float CoverHeight => _coverHeight;
        public bool IsPlayerTakingCover => _isPlayerTakingCover;
        public Collider2D PhysicalCollider => _physicalCollider;

        private void Awake()
        {
            if (_physicalCollider == null)
            {
                _physicalCollider = GetComponent<Collider2D>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerTakingCover = true;
                Debug.Log($"[CoverObstacle] Player in cover behind {_coverId}");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerTakingCover = false;
                Debug.Log($"[CoverObstacle] Player left cover behind {_coverId}");
            }
        }

        /// <summary>
        /// Evaluates whether this cover obstacle intersects the direct line-of-sight
        /// between the alien observer and the target.
        /// </summary>
        public bool BlocksLineOfSight(Vector2 observerPos, Vector2 targetPos)
        {
            if (_physicalCollider == null) return false;

            RaycastHit2D hit = Physics2D.Linecast(observerPos, targetPos, 1 << gameObject.layer);
            return hit.collider == _physicalCollider;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (_physicalCollider != null)
            {
                Gizmos.DrawWireCube(_physicalCollider.bounds.center, _physicalCollider.bounds.size);
            }
        }
    }
}
