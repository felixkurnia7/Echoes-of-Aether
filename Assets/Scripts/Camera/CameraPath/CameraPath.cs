using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfAether.Camera
{
    public enum CameraPathProjection
    {
        /// <summary>Closest point uses full 3D distance.</summary>
        World3D,
        /// <summary>Closest point ignores Y (good for side-view rails above/beside the play area).</summary>
        FlatXZ
    }

    /// <summary>
    /// Simple 3D camera path defined by waypoint transforms (polyline).
    /// Place waypoints at the exact side-view camera positions you want along the level.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echoes of Aether/Camera/Camera Path")]
    public sealed class CameraPath : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints = new();
        [SerializeField, Min(0.001f)] private float minSegmentLength = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.9f, 0.2f, 0.9f);
        [SerializeField] private float gizmoPointRadius = 0.25f;

        private readonly List<Vector3> _points = new();
        private readonly List<float> _cumulative = new(); // cumulative distance at each point

        public IReadOnlyList<Transform> Waypoints => waypoints;
        public float TotalLength { get; private set; }

        private void OnValidate()
        {
            minSegmentLength = Mathf.Max(0.001f, minSegmentLength);
            gizmoPointRadius = Mathf.Max(0.01f, gizmoPointRadius);
            RebuildCache();
        }

        private void Awake()
        {
            RebuildCache();
        }

        public void RebuildCache()
        {
            _points.Clear();
            _cumulative.Clear();
            TotalLength = 0f;

            for (var i = 0; i < waypoints.Count; i++)
            {
                var t = waypoints[i];
                if (t == null) continue;
                _points.Add(t.position);
            }

            if (_points.Count < 2) return;

            _cumulative.Add(0f);
            for (var i = 1; i < _points.Count; i++)
            {
                var segLen = Vector3.Distance(_points[i - 1], _points[i]);
                if (segLen < minSegmentLength)
                {
                    // Keep point but don't add tiny length (stabilizes closest-point math)
                    _cumulative.Add(TotalLength);
                    continue;
                }

                TotalLength += segLen;
                _cumulative.Add(TotalLength);
            }
        }

        public Vector3 EvaluateByDistance(float distance)
        {
            if (_points.Count == 0) return transform.position;
            if (_points.Count == 1) return _points[0];
            if (TotalLength <= 0.0001f) return _points[0];

            distance = Mathf.Clamp(distance, 0f, TotalLength);

            // Find segment containing distance.
            var segIndex = 0;
            while (segIndex < _cumulative.Count - 1 && _cumulative[segIndex + 1] < distance)
                segIndex++;

            var a = _points[segIndex];
            var b = _points[Mathf.Min(segIndex + 1, _points.Count - 1)];

            var segStart = _cumulative[segIndex];
            var segEnd = _cumulative[Mathf.Min(segIndex + 1, _cumulative.Count - 1)];
            var segLen = Mathf.Max(0.0001f, segEnd - segStart);
            var t = Mathf.Clamp01((distance - segStart) / segLen);

            return Vector3.Lerp(a, b, t);
        }

        public Vector3 EvaluateTangentByDistance(float distance)
        {
            if (_points.Count < 2) return transform.forward;

            distance = Mathf.Clamp(distance, 0f, TotalLength);

            var segIndex = 0;
            while (segIndex < _cumulative.Count - 1 && _cumulative[segIndex + 1] < distance)
                segIndex++;

            var a = _points[segIndex];
            var b = _points[Mathf.Min(segIndex + 1, _points.Count - 1)];
            var tangent = b - a;

            tangent.y = 0f;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.forward;
        }

        public bool TryGetClosestDistance(Vector3 worldPoint, out float closestDistance)
            => TryGetClosestDistance(worldPoint, CameraPathProjection.FlatXZ, out closestDistance);

        public bool TryGetClosestDistance(Vector3 worldPoint, CameraPathProjection projection, out float closestDistance)
        {
            closestDistance = 0f;
            if (_points.Count < 2 || TotalLength <= 0.0001f) return false;

            var bestSqr = float.PositiveInfinity;
            var bestDist = 0f;

            for (var i = 0; i < _points.Count - 1; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];

                var t = projection == CameraPathProjection.FlatXZ
                    ? ClosestPointOnSegmentXZ(worldPoint, a, b)
                    : ClosestPointOnSegment3D(worldPoint, a, b);

                var p = Vector3.Lerp(a, b, t);
                var sqr = projection == CameraPathProjection.FlatXZ
                    ? SqrDistanceXZ(worldPoint, p)
                    : (worldPoint - p).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    var segLen = Vector3.Distance(a, b);
                    bestDist = _cumulative[i] + segLen * t;
                }
            }

            closestDistance = Mathf.Clamp(bestDist, 0f, TotalLength);
            return true;
        }

        private static float ClosestPointOnSegment3D(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var abLenSqr = ab.sqrMagnitude;
            if (abLenSqr <= 0.000001f) return 0f;
            return Mathf.Clamp01(Vector3.Dot(point - a, ab) / abLenSqr);
        }

        private static float ClosestPointOnSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            var ax = a.x;
            var az = a.z;
            var bx = b.x;
            var bz = b.z;
            var px = point.x;
            var pz = point.z;

            var abx = bx - ax;
            var abz = bz - az;
            var abLenSqr = abx * abx + abz * abz;
            if (abLenSqr <= 0.000001f) return 0f;

            return Mathf.Clamp01(((px - ax) * abx + (pz - az) * abz) / abLenSqr);
        }

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Build a lightweight preview from current waypoint positions (safe in edit mode).
            var preview = new List<Vector3>();
            for (var i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null) preview.Add(waypoints[i].position);
            }
            if (preview.Count == 0) return;

            Gizmos.color = gizmoColor;
            for (var i = 0; i < preview.Count; i++)
            {
                Gizmos.DrawWireSphere(preview[i], gizmoPointRadius);
                if (i < preview.Count - 1)
                    Gizmos.DrawLine(preview[i], preview[i + 1]);
            }
        }
    }
}

