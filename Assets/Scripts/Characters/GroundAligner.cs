using UnityEngine;

/// <summary>
/// Tilts a character to match the slope of the ground beneath it (and optionally
/// snaps it to the ground height) so that rigid four-legged models like the bear
/// keep all feet planted on inclines instead of floating.
///
/// Runs in LateUpdate so it applies AFTER movers such as <see cref="NPCWalker"/>
/// set the facing (yaw): this script keeps that yaw and only adds the pitch/roll
/// needed to follow the surface normal.
///
/// Works well together with a Rigidbody that has its rotation frozen, because the
/// tilt is driven here by script rather than by physics.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class GroundAligner : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Which layers count as ground. Set this to your terrain/ground layer for best results.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("How high above this object to start the downward ray (meters).")]
    [SerializeField] private float rayStartHeight = 1.5f;

    [Tooltip("How far down to search for ground from the ray start (meters).")]
    [SerializeField] private float rayDistance = 5f;

    [Header("Alignment")]
    [Tooltip("Rotate the body to match the ground slope.")]
    [SerializeField] private bool alignToNormal = true;

    [Tooltip("Ignore slopes steeper than this (degrees); the body stays upright on them.")]
    [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 50f;

    [Tooltip("How quickly the tilt eases toward the slope. Higher = snappier. 0 = instant.")]
    [SerializeField] private float rotationLerpSpeed = 10f;

    [Header("Height")]
    [Tooltip("Snap the object's height onto the ground so it follows hills up and down.")]
    [SerializeField] private bool snapToGround = false;

    [Tooltip("Vertical offset applied when snapping (use if the pivot is not exactly at the feet).")]
    [SerializeField] private float groundOffset = 0f;

    static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

    void Reset()
    {
        // Try to default the ground mask to a layer literally named "Ground".
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            groundMask = 1 << groundLayer;
    }

    void LateUpdate()
    {
        if (!TryGetGround(out RaycastHit hit))
            return;

        if (snapToGround)
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + groundOffset;
            transform.position = pos;
        }

        if (alignToNormal)
            ApplyAlignment(hit.normal);
    }

    bool TryGetGround(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        float distance = rayDistance + rayStartHeight;

        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, HitBuffer, distance, groundMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearest = float.MaxValue;
        hit = default;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = HitBuffer[i];

            // Skip our own colliders.
            if (candidate.collider != null && candidate.collider.transform.IsChildOf(transform))
                continue;

            if (candidate.distance < nearest)
            {
                nearest = candidate.distance;
                hit = candidate;
                found = true;
            }
        }

        return found;
    }

    void ApplyAlignment(Vector3 groundNormal)
    {
        // On steep surfaces, stay upright to avoid the body lying down.
        float slope = Vector3.Angle(Vector3.up, groundNormal);
        Vector3 targetUp = slope > maxSlopeAngle ? Vector3.up : groundNormal;

        // Keep the current facing (yaw set by the mover) but re-base it on the slope.
        Vector3 forwardOnSlope = Vector3.ProjectOnPlane(transform.forward, targetUp);
        if (forwardOnSlope.sqrMagnitude < 0.0001f)
            forwardOnSlope = Vector3.ProjectOnPlane(transform.right, targetUp);
        if (forwardOnSlope.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(forwardOnSlope.normalized, targetUp);

        if (rotationLerpSpeed <= 0f)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime));
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        Vector3 end = origin + Vector3.down * (rayDistance + rayStartHeight);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }
}
