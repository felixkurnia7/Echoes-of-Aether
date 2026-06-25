using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace EchoesOfAether.Camera
{
    /// <summary>
    /// Cinemachine extension that prevents the camera from clipping through world geometry.
    /// Attach this to the same GameObject as your Cinemachine Camera (vcam).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echoes of Aether/Camera/Cinemachine Camera Collision Checker")]
    public sealed class CinemachineCameraCollisionChecker : CinemachineExtension
    {
        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool drawOnlyWhenSelected = false;
        [SerializeField] private Color gizmoCastColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color gizmoHitColor = new Color(1f, 0.25f, 0.25f, 0.95f);
        [SerializeField] private Color gizmoCorrectedColor = new Color(0.25f, 1f, 0.35f, 0.95f);

        [Header("Collision")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField, Min(0f)] private float cameraRadius = 0.25f;
        [SerializeField, Min(0f)] private float skin = 0.02f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Distance")]
        [SerializeField, Min(0f)] private float pushOutSpeed = 40f;
        [SerializeField, Min(0f)] private float returnSpeed = 80f;
        [Tooltip("Small extra distance kept from surfaces when blocking movement.")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;

        [Header("Ignore")]
        [Tooltip("Optional: colliders under this root will be ignored (e.g. Player root).")]
        [SerializeField] private Transform ignoreCollidersRoot;

        [Header("Penetration Recovery")]
        [SerializeField] private bool resolvePenetration = true;
        [SerializeField, Min(0)] private int maxPenetrationIterations = 6;
        [SerializeField, Min(0f)] private float maxPenetrationPush = 2f;

        private readonly HashSet<int> _ignoredColliderInstanceIds = new();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private SphereCollider _cameraProbeCollider;
        private Transform _cameraProbeTransform;
        private Vector3 _currentCorrection;
        private Vector3 _correctionVelocity;
        private bool _hasLastSafePos;
        private Vector3 _lastSafePos;

        // Gizmo state (updated at runtime)
        private Vector3 _gizmoOrigin;
        private Vector3 _gizmoDesired;
        private Vector3 _gizmoCorrected;
        private bool _gizmoHasHit;
        private Vector3 _gizmoHitPoint;

        protected override void Awake()
        {
            base.Awake();
            RebuildIgnoredColliders();
            EnsureProbeCollider();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_cameraProbeTransform != null)
            {
                var go = _cameraProbeTransform.gameObject;
                _cameraProbeCollider = null;
                _cameraProbeTransform = null;
                Destroy(go);
            }
        }

        private void OnValidate()
        {
            cameraRadius = Mathf.Max(0f, cameraRadius);
            skin = Mathf.Max(0f, skin);
            pushOutSpeed = Mathf.Max(0f, pushOutSpeed);
            returnSpeed = Mathf.Max(0f, returnSpeed);
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            maxPenetrationIterations = Mathf.Max(0, maxPenetrationIterations);
            maxPenetrationPush = Mathf.Max(0f, maxPenetrationPush);
        }

        public void RebuildIgnoredColliders()
        {
            _ignoredColliderInstanceIds.Clear();
            if (ignoreCollidersRoot == null) return;

            var colliders = ignoreCollidersRoot.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    _ignoredColliderInstanceIds.Add(colliders[i].GetInstanceID());
            }
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // Run after the camera body has computed the desired position.
            if (stage != CinemachineCore.Stage.Body) return;

            var desiredPos = state.RawPosition + state.PositionCorrection;

            if (!_hasLastSafePos)
            {
                _lastSafePos = desiredPos;
                _hasLastSafePos = true;
            }

            // Block camera movement into colliders (do NOT pull camera toward player).
            var correctedPos = BlockMovementIntoObstacles(_lastSafePos, desiredPos);

            if (resolvePenetration)
                correctedPos = ResolveOverlap(correctedPos);

            if (IsPositionFree(correctedPos))
                _lastSafePos = correctedPos;

            var desiredCorrection = correctedPos - desiredPos;

            // Apply as PositionCorrection (not RawPosition) to avoid fighting Cinemachine damping.
            if (deltaTime > 0f && (pushOutSpeed > 0f || returnSpeed > 0f))
            {
                // If we need to push out more, use pushOutSpeed. If returning (usually to zero), use returnSpeed.
                var speed = desiredCorrection.sqrMagnitude >= _currentCorrection.sqrMagnitude ? pushOutSpeed : returnSpeed;
                var maxStep = speed * deltaTime;

                _currentCorrection = Vector3.MoveTowards(_currentCorrection, desiredCorrection, maxStep);
                _correctionVelocity = Vector3.zero;

                // Snap tiny corrections to zero to prevent micro-jitter.
                if (_currentCorrection.sqrMagnitude < 0.000001f && desiredCorrection.sqrMagnitude < 0.000001f)
                    _currentCorrection = Vector3.zero;
            }
            else
            {
                _currentCorrection = desiredCorrection;
                _correctionVelocity = Vector3.zero;
            }

            state.PositionCorrection += _currentCorrection;

            // Cache for gizmo drawing.
            _gizmoOrigin = _lastSafePos;
            _gizmoDesired = desiredPos;
            _gizmoCorrected = desiredPos + _currentCorrection;
        }

        private Vector3 BlockMovementIntoObstacles(Vector3 fromPos, Vector3 desiredCameraPos)
        {
            var toDesired = desiredCameraPos - fromPos;
            var dist = toDesired.magnitude;
            if (dist <= Mathf.Epsilon) return desiredCameraPos;

            _gizmoHasHit = false;

            var dir = toDesired / dist;
            var castRadius = Mathf.Max(0f, cameraRadius);

            if (Physics.SphereCast(
                    fromPos,
                    castRadius,
                    dir,
                    out var hit,
                    dist,
                    collisionLayers,
                    triggerInteraction))
            {
                if (!IsIgnored(hit.collider))
                {
                    _gizmoHasHit = true;
                    _gizmoHitPoint = hit.point;
                    var allowed = Mathf.Max(0f, hit.distance - Mathf.Max(skin, surfaceOffset));
                    return fromPos + dir * allowed;
                }
            }

            return desiredCameraPos;
        }

        private bool IsPositionFree(Vector3 cameraPos)
        {
            var r = Mathf.Max(0f, cameraRadius);
            if (r <= 0f) return true;

            var count = Physics.OverlapSphereNonAlloc(cameraPos, r, _overlapBuffer, collisionLayers, triggerInteraction);
            for (var i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null || IsIgnored(col)) continue;
                return false;
            }
            return true;
        }

        private Vector3 ResolveOverlap(Vector3 cameraPos)
        {
            var pos = cameraPos;
            var radius = Mathf.Max(0f, cameraRadius);
            if (radius <= 0f) return pos;

            EnsureProbeCollider();
            if (_cameraProbeCollider == null || _cameraProbeTransform == null) return pos;

            _cameraProbeCollider.radius = radius;

            var remainingPush = maxPenetrationPush;

            for (var iter = 0; iter < maxPenetrationIterations; iter++)
            {
                _cameraProbeTransform.position = pos;

                var hitCount = Physics.OverlapSphereNonAlloc(pos, radius, _overlapBuffer, collisionLayers, triggerInteraction);
                if (hitCount <= 0) break;

                var pushedThisIter = false;

                for (var i = 0; i < hitCount; i++)
                {
                    var other = _overlapBuffer[i];
                    if (other == null || IsIgnored(other)) continue;

                    // Push camera out of overlap using ComputePenetration.
                    var otherTransform = other.transform;
                    if (Physics.ComputePenetration(
                            other, otherTransform.position, otherTransform.rotation,
                            _cameraProbeCollider, pos, Quaternion.identity,
                            out var direction, out var distance))
                    {
                        if (distance > 0f)
                        {
                            var push = Mathf.Min(distance + skin, remainingPush);
                            pos += direction * push;
                            remainingPush -= push;
                            pushedThisIter = true;

                            if (remainingPush <= 0f) return pos;
                        }
                    }
                }

                if (!pushedThisIter) break;
            }

            return pos;
        }

        private void EnsureProbeCollider()
        {
            if (_cameraProbeCollider != null && _cameraProbeTransform != null) return;

            var go = new GameObject("[CameraCollisionProbe]");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = gameObject.layer; // doesn't matter for queries, but keep consistent

            _cameraProbeTransform = go.transform;
            _cameraProbeTransform.position = Vector3.zero;
            _cameraProbeTransform.rotation = Quaternion.identity;

            _cameraProbeCollider = go.AddComponent<SphereCollider>();
            _cameraProbeCollider.isTrigger = true;
            _cameraProbeCollider.radius = Mathf.Max(0.01f, cameraRadius);
        }

        private bool IsIgnored(Collider col)
        {
            return col != null && _ignoredColliderInstanceIds.Contains(col.GetInstanceID());
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (drawOnlyWhenSelected) return;
            DrawGizmosInternal();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (!drawOnlyWhenSelected) return;
            DrawGizmosInternal();
        }

        private void DrawGizmosInternal()
        {
            var r = Mathf.Max(0f, cameraRadius);
            if (r <= 0f) return;

            // If not playing yet, provide a "preview" using current transform position.
            var origin = Application.isPlaying ? _gizmoOrigin : transform.position;
            var desired = Application.isPlaying ? _gizmoDesired : transform.position;
            var corrected = Application.isPlaying ? _gizmoCorrected : transform.position;

            Gizmos.color = gizmoCastColor;
            Gizmos.DrawLine(origin, desired);
            Gizmos.DrawWireSphere(origin, r);
            Gizmos.DrawWireSphere(desired, r);

            Gizmos.color = gizmoCorrectedColor;
            Gizmos.DrawWireSphere(corrected, r);

            if (Application.isPlaying && _gizmoHasHit)
            {
                Gizmos.color = gizmoHitColor;
                Gizmos.DrawSphere(_gizmoHitPoint, Mathf.Max(0.03f, r * 0.15f));
            }
        }
    }
}

