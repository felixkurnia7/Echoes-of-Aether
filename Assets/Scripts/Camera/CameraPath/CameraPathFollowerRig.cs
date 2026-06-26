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
        [Tooltip("How quickly the rig rotation eases toward the path tangent. Higher = snappier, lower = smoother. 0 = instant snap (can feel jerky at corners).")]
        [SerializeField, Min(0f)] private float rotationSmoothing = 6f;

        [Header("Advanced")]
        [Tooltip("Rebuild the path cache every frame. Only needed if waypoints move at runtime.")]
        [SerializeField] private bool rebuildEveryFrame = false;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoRigColor = new Color(0.25f, 1f, 0.35f, 0.95f);

        private bool _initialized;
        private float _currentDistance;

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            rotationSmoothing = Mathf.Max(0f, rotationSmoothing);
        }

        private void Update()
        {
            if (path == null || player == null) return;

            // Rebuild only when needed: every frame in edit mode (live preview),
            // at runtime only if explicitly requested or the cache is empty.
            if (!Application.isPlaying || rebuildEveryFrame || path.TotalLength <= 0.0001f)
                path.RebuildCache();

            if (path.TotalLength <= 0.0001f) return;

            if (!path.TryGetClosestDistance(player.position, projection, out var desiredDistance))
                return;

            if (!_initialized)
            {
                _currentDistance = desiredDistance;
                _initialized = true;

                var initialPos = path.EvaluateByDistance(_currentDistance) + worldOffset;
                transform.position = initialPos;

                if (alignRotationToPath)
                {
                    var initialTangent = path.EvaluateTangentByDistance(_currentDistance);
                    if (initialTangent.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(initialTangent, Vector3.up);
                }
                return;
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
                {
                    var targetRotation = Quaternion.LookRotation(tangent, Vector3.up);

                    if (rotationSmoothing > 0f)
                    {
                        // Frame-rate independent smoothing so corners ease instead of snapping.
                        float k = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, k);
                    }
                    else
                    {
                        transform.rotation = targetRotation;
                    }
                }
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

