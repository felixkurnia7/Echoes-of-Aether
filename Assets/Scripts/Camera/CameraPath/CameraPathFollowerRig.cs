using UnityEngine;

namespace EchoesOfAether.Camera
{
    /// <summary>
    /// Moves this transform along a CameraPath based on the player's closest point on the path.
    /// Side-view setup: place waypoints at the camera positions you want, set Cinemachine Follow
    /// to this rig with FollowOffset (0,0,0), and LookAt to the player.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echoes of Aether/Camera/Camera Path Follower Rig")]
    [DefaultExecutionOrder(-100)]
    public sealed class CameraPathFollowerRig : MonoBehaviour
    {
        [SerializeField] private CameraPath path;
        [SerializeField] private Transform player;

        [Header("Side View")]
        [Tooltip("FlatXZ ignores height when matching player to the path (recommended for side view).")]
        [SerializeField] private CameraPathProjection projection = CameraPathProjection.FlatXZ;

        [Header("Offsets")]
        [Tooltip("Optional fine-tune. Waypoints should already be at the desired side-view camera positions.")]
        [SerializeField] private Vector3 worldOffset = Vector3.zero;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float moveSpeed = 25f;

        [Header("Orientation")]
        [Tooltip("Align rig forward to the path tangent. Used as a stable movement reference for the player.")]
        [SerializeField] private bool alignRotationToPath = true;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoRigColor = new Color(0.25f, 1f, 0.35f, 0.95f);

        private bool _initialized;
        private float _currentDistance;

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
        }

        private void Update()
        {
            if (path == null || player == null) return;

            // Refresh cached points in case waypoints moved.
            path.RebuildCache();
            if (path.TotalLength <= 0.0001f) return;

            if (!path.TryGetClosestDistance(player.position, projection, out var desiredDistance))
                return;

            if (!_initialized)
            {
                _currentDistance = desiredDistance;
                _initialized = true;
            }

            if (moveSpeed > 0f)
                _currentDistance = Mathf.MoveTowards(_currentDistance, desiredDistance, moveSpeed * Time.deltaTime);
            else
                _currentDistance = desiredDistance;

            var pos = path.EvaluateByDistance(_currentDistance) + worldOffset;
            transform.position = pos;

            if (alignRotationToPath)
            {
                var tangent = path.EvaluateTangentByDistance(_currentDistance);
                if (tangent.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = gizmoRigColor;
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}

