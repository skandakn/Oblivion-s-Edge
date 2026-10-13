using UnityEngine;

namespace OblivionsEdge.Core
{
    /// <summary>
    /// Smooth camera follow script for the 2D survivor viewport.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, -10f);
        [SerializeField, Range(1f, 20f)] private float _smoothSpeed = 8f;
        [SerializeField] private Vector2 _minBounds = new Vector2(-15f, -2f);
        [SerializeField] private Vector2 _maxBounds = new Vector2(15f, 10f);

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                // Auto-locate player if target lost
                var player = GameObject.FindWithTag(GameConstants.TagPlayer);
                if (player != null)
                {
                    _target = player.transform;
                }
                else
                {
                    return;
                }
            }

            Vector3 targetPosition = _target.position + _offset;
            targetPosition.x = Mathf.Clamp(targetPosition.x, _minBounds.x, _maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, _minBounds.y, _maxBounds.y);
            targetPosition.z = _offset.z;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                _smoothSpeed * Time.deltaTime
            );
        }
    }
}
