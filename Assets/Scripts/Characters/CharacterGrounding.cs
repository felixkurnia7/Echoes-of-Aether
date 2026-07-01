using UnityEngine;

/// <summary>
/// Shared helpers for planting a <see cref="CharacterController"/> on walkable
/// geometry without desyncing the capsule (always use <see cref="CharacterController.Move"/>
/// for vertical corrections, never <see cref="Transform.position"/>).
/// </summary>
public static class CharacterGrounding
{
    public static float GetPivotYAboveGround(CharacterController cc)
    {
        return cc.height * 0.5f - cc.center.y;
    }

    /// <summary>
    /// Nudges the controller onto the ground below its pivot when within
    /// <paramref name="maxSnapDistance"/> vertically.
    /// </summary>
    public static bool TrySnapToGround(
        CharacterController cc,
        Transform transform,
        LayerMask groundMask,
        float groundOffset,
        float maxSnapDistance,
        float rayStartHeight = 0.5f)
    {
        if (cc == null || !cc.enabled)
            return false;

        Vector3 origin = transform.position + Vector3.up * rayStartHeight;
        float rayLength = maxSnapDistance + rayStartHeight;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return false;

        float targetY = hit.point.y + GetPivotYAboveGround(cc) + groundOffset;
        float deltaY = targetY - transform.position.y;

        if (Mathf.Abs(deltaY) > maxSnapDistance)
            return false;

        if (Mathf.Abs(deltaY) > 0.001f)
            cc.Move(Vector3.up * deltaY);

        return true;
    }
}
