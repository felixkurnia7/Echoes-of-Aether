using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for anything that walks a character GameObject around the world:
/// NPCs, monsters, and the player during cutscenes. It owns the shared work of
/// stepping toward a target, turning to face the direction of travel, keeping
/// height, and driving an Animator. Subclasses decide WHERE to go (waypoints,
/// patrol, chase, scripted cutscene path) by calling <see cref="MoveTo(Transform)"/>
/// or <see cref="StepToward"/>.
/// </summary>
[DisallowMultipleComponent]
public abstract class CharacterMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 2.5f;
    [SerializeField] protected float turnSpeed = 10f;
    [Tooltip("How close (meters) the mover must get to a target before it counts as reached.")]
    [SerializeField] protected float arriveThreshold = 0.15f;
    [Tooltip("Keep the character at its current height while moving (recommended for flat ground or when a GroundAligner handles Y).")]
    [SerializeField] protected bool keepCurrentHeight = true;

    [Header("Animation (optional)")]
    [SerializeField] protected Animator animator;
    [Tooltip("Float parameter set to moveSpeed while moving, 0 while idle. Left empty = ignored.")]
    [SerializeField] protected string speedParam = "Speed";
    [Tooltip("Bool parameter true while moving. Left empty = ignored.")]
    [SerializeField] protected string isMovingParam = "IsMoving";
    [Tooltip("Forward/back blend parameter. Left empty = ignored.")]
    [SerializeField] protected string moveYParam = "MoveY";
    [Tooltip("Strafe blend parameter. Left empty = ignored.")]
    [SerializeField] protected string moveXParam = "MoveX";
    [Tooltip("Bool set TRUE while moving and FALSE while idle. For controllers driven by named bools via AnyState (e.g. the Bear's 'WalkForward'). Left empty = ignored.")]
    [SerializeField] protected string moveBoolParam = "";
    [Tooltip("Bool set TRUE while idle and FALSE while moving (e.g. the Bear's 'Idle'). Left empty = ignored.")]
    [SerializeField] protected string idleBoolParam = "";

    /// <summary>True while the character is actively stepping toward a target.</summary>
    public bool IsMoving { get; protected set; }

    /// <summary>Speed (m/s) the character moves at.</summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// Start moving along this mover's configured route (waypoints, patrol, or
    /// cutscene path). <paramref name="onComplete"/> fires when the route finishes;
    /// for looping movers (e.g. patrol) it may fire immediately. This is the common
    /// entry point used by Fungus / UnityEvents to drive any character type.
    /// </summary>
    public abstract void BeginMovement(Action onComplete);

    /// <summary>Start moving along this mover's configured route.</summary>
    public void BeginMovement() => BeginMovement(null);

    /// <summary>Stop moving immediately and return to idle.</summary>
    public abstract void StopMovement();

    int speedHash, isMovingHash, moveXHash, moveYHash, moveBoolHash, idleBoolHash;
    readonly HashSet<string> animatorParams = new();

    protected virtual void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        speedHash = Animator.StringToHash(speedParam);
        isMovingHash = Animator.StringToHash(isMovingParam);
        moveXHash = Animator.StringToHash(moveXParam);
        moveYHash = Animator.StringToHash(moveYParam);
        moveBoolHash = Animator.StringToHash(moveBoolParam);
        idleBoolHash = Animator.StringToHash(idleBoolParam);

        RefreshAnimatorParams();
    }

    /// <summary>Re-reads the parameter names from the current Animator controller.</summary>
    protected void RefreshAnimatorParams()
    {
        animatorParams.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter p in animator.parameters)
            animatorParams.Add(p.name);
    }

    /// <summary>Walks toward a (possibly moving) target until within arriveThreshold.</summary>
    protected IEnumerator MoveTo(Transform target)
    {
        while (target != null && !StepToward(target.position))
            yield return null;

        ApplyAnimation(false);
    }

    /// <summary>Walks toward a fixed world position until within arriveThreshold.</summary>
    protected IEnumerator MoveTo(Vector3 worldPosition)
    {
        while (!StepToward(worldPosition))
            yield return null;

        ApplyAnimation(false);
    }

    /// <summary>
    /// Performs one movement step toward a world position (rotate + translate +
    /// animate). Returns true once the target is within arriveThreshold. Call this
    /// directly from custom per-frame logic (e.g. chasing a moving target).
    /// </summary>
    protected bool StepToward(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= arriveThreshold)
        {
            ApplyAnimation(false);
            return true;
        }

        Vector3 direction = toTarget / Mathf.Max(distance, 0.0001f);

        Quaternion targetRot = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, Time.deltaTime * turnSpeed);

        float step = Mathf.Min(moveSpeed * Time.deltaTime, distance);
        Vector3 next = transform.position + direction * step;
        if (keepCurrentHeight)
            next.y = transform.position.y;
        transform.position = next;

        ApplyAnimation(true);
        return false;
    }

    /// <summary>Drives the Animator. Override to customise behaviour per character type.</summary>
    protected virtual void ApplyAnimation(bool moving)
    {
        IsMoving = moving;

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (Has(speedParam)) animator.SetFloat(speedHash, moving ? moveSpeed : 0f);
        if (Has(isMovingParam)) animator.SetBool(isMovingHash, moving);
        if (Has(moveYParam)) animator.SetFloat(moveYHash, moving ? 1f : 0f);
        if (Has(moveXParam)) animator.SetFloat(moveXHash, 0f);
        if (Has(moveBoolParam)) animator.SetBool(moveBoolHash, moving);
        if (Has(idleBoolParam)) animator.SetBool(idleBoolHash, !moving);
    }

    /// <summary>True if the (non-empty) parameter exists on the current controller.</summary>
    protected bool Has(string paramName)
    {
        return !string.IsNullOrEmpty(paramName) && animatorParams.Contains(paramName);
    }

    /// <summary>
    /// Instantly moves the character to a target transform while keeping it on top of
    /// the environment. Safe for characters driven by a CharacterController or Rigidbody:
    /// those components are toggled/zeroed so the character does not fall through the
    /// world after a big positional jump (e.g. resuming a scene after a battle).
    /// </summary>
    protected void TeleportTo(Transform target)
    {
        if (target == null)
            return;

        CharacterController cc = GetComponent<CharacterController>();
        Collider col = GetComponent<Collider>();

        bool ccWasEnabled = cc != null && cc.enabled;
        bool colWasEnabled = col != null && col.enabled;

        // Disable our own colliders so the ground rays can't hit ourselves, and so a
        // CharacterController (which caches its own pose) can actually be repositioned.
        if (cc != null) cc.enabled = false;
        if (col != null) col.enabled = false;

        Vector3 pos = target.position;
        float offset = HeightAboveGround();

        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            pos.y = hit.point.y + offset;
        else if (keepCurrentHeight)
            pos.y = transform.position.y;

        transform.SetPositionAndRotation(pos, target.rotation);

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.position = pos;
            rb.rotation = target.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null) col.enabled = colWasEnabled;
        if (cc != null) cc.enabled = ccWasEnabled;
    }

    // How high the pivot currently sits above the ground (0 if no ground is found).
    float HeightAboveGround()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return Mathf.Max(0f, transform.position.y - hit.point.y);
        return 0f;
    }
}
