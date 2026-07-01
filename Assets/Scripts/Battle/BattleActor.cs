using System;
using System.Collections;
using UnityEngine;

public enum BattleSide
{
    Player,
    Enemy
}

/// <summary>
/// Visual representation of one combatant in the Battle scene. Lives on the
/// pre-placed battle prefab (e.g. Player_Battle, Enemy_Bear_Battle) and is
/// driven by <see cref="BattleManager"/>. Handles the simple "lunge forward,
/// play attack, return" motion plus hit/death animation playback. All timing
/// uses plain delays (no animation events), as requested.
/// </summary>
[DisallowMultipleComponent]
public class BattleActor : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Which side this actor belongs to. BattleManager matches actors to combatants by side.")]
    [SerializeField] private BattleSide side = BattleSide.Player;

    [Header("Animator")]
    [Tooltip("Animator to drive. Auto-found on this object or its children if left empty.")]
    [SerializeField] private Animator animator;

    [Header("Animation Parameters (leave empty to skip)")]
    [Tooltip("Trigger fired when attacking.")]
    [SerializeField] private string attackTrigger = "Attack";
    [Tooltip("Trigger fired when taking a hit.")]
    [SerializeField] private string hitTrigger = "Hit";
    [Tooltip("Parameter that plays the death animation.")]
    [SerializeField] private string deathParam = "Death";
    [Tooltip("ON = death parameter is a Bool (SetBool true). OFF = it's a Trigger (SetTrigger).")]
    [SerializeField] private bool deathIsBool;
    [Tooltip("Float speed parameter pushed while moving (blend trees). Leave empty if unused.")]
    [SerializeField] private string speedParam = "Speed";
    [Tooltip("Bool 'is moving' parameter set while lunging. Leave empty if unused.")]
    [SerializeField] private string moveBool = "IsMoving";
    [Tooltip("Value written to the speed parameter while moving.")]
    [SerializeField] private float moveSpeedValue = 1f;

    [Header("Lunge")]
    [Tooltip("Distance (world units) to stop in front of the target when attacking.")]
    [SerializeField] private float attackGap = 1.6f;
    [Tooltip("Travel speed of the lunge, in units per second.")]
    [SerializeField] private float moveSpeed = 6f;
    [Tooltip("Turn speed when facing the target, in degrees per second.")]
    [SerializeField] private float rotateSpeed = 720f;

    [Header("Timing (simple delays)")]
    [Tooltip("Delay after the attack trigger before damage lands.")]
    [SerializeField] private float impactDelay = 0.35f;
    [Tooltip("Delay after impact before returning to the home position.")]
    [SerializeField] private float postAttackDelay = 0.45f;
    [Tooltip("Optional animator state name to wait for before walking home. Leave empty to auto-detect.")]
    [SerializeField] private string attackStateName = "";

    Vector3 homePosition;
    Quaternion homeRotation;
    bool isDead;
    int attackStateHash;

    public BattleSide Side => side;
    public bool IsDead => isDead;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (!string.IsNullOrEmpty(attackStateName))
            attackStateHash = Animator.StringToHash(attackStateName);

        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    /// <summary>Snaps the actor to a spawn point and records it as the home pose.</summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        homePosition = position;
        homeRotation = rotation;
    }

    /// <summary>Instantly rotates to face a world position (horizontal only).</summary>
    public void FaceInstant(Vector3 worldPosition)
    {
        Vector3 dir = worldPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir);
        homeRotation = transform.rotation;
    }

    /// <summary>
    /// Walk up to <paramref name="target"/>, play the attack animation, invoke
    /// <paramref name="onImpact"/> at the impact moment, then walk back home.
    /// </summary>
    public IEnumerator AttackRoutine(BattleActor target, Action onImpact)
    {
        if (isDead || target == null)
        {
            onImpact?.Invoke();
            yield break;
        }

        Vector3 targetPos = target.transform.position;
        Vector3 dir = targetPos - homePosition;
        dir.y = 0f;
        float dist = dir.magnitude;
        dir = dist > 0.001f ? dir / dist : transform.forward;

        Vector3 attackPos = targetPos - dir * attackGap;
        attackPos.y = homePosition.y;

        yield return FaceDirection(dir);

        SetMoving(true);
        yield return MoveTo(attackPos);
        SetMoving(false);

        FireAttack();
        yield return new WaitForSeconds(impactDelay);
        onImpact?.Invoke();
        yield return WaitForAttackToFinish();

        ClearAttackParameter();

        if (isDead)
            yield break;

        SetMoving(true);
        yield return MoveTo(homePosition);
        SetMoving(false);

        yield return FaceRotation(homeRotation);
    }

    void FireAttack()
    {
        if (animator == null || string.IsNullOrEmpty(attackTrigger))
            return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name != attackTrigger)
                continue;

            switch (param.type)
            {
                case AnimatorControllerParameterType.Trigger:
                    animator.SetTrigger(attackTrigger);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(attackTrigger, true);
                    break;
            }

            return;
        }
    }

    void ClearAttackParameter()
    {
        if (animator == null || string.IsNullOrEmpty(attackTrigger))
            return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name != attackTrigger)
                continue;

            if (param.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(attackTrigger);
            else if (param.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(attackTrigger, false);

            return;
        }
    }

    IEnumerator WaitForAttackToFinish()
    {
        if (animator == null)
        {
            if (postAttackDelay > 0f)
                yield return new WaitForSeconds(postAttackDelay);
            yield break;
        }

        int preAttackHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        yield return null;

        float elapsed = 0f;
        const float enterWindow = 0.5f;
        int attackHash = 0;
        bool foundAttack = false;

        while (elapsed < enterWindow)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0))
            {
                if (attackStateHash != 0 && state.shortNameHash == attackStateHash)
                {
                    attackHash = attackStateHash;
                    foundAttack = true;
                    break;
                }

                if (state.shortNameHash != preAttackHash)
                {
                    attackHash = state.shortNameHash;
                    foundAttack = true;
                    break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!foundAttack)
        {
            if (postAttackDelay > 0f)
                yield return new WaitForSeconds(postAttackDelay);
            yield break;
        }

        elapsed = 0f;
        const float maxFinish = 2f;
        while (elapsed < maxFinish)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0))
            {
                if (state.shortNameHash != attackHash)
                    yield break;

                if (state.normalizedTime >= 0.88f)
                    yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public void PlayHit()
    {
        if (isDead || animator == null || string.IsNullOrEmpty(hitTrigger))
            return;

        animator.SetTrigger(hitTrigger);
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        SetMoving(false);

        if (animator == null || string.IsNullOrEmpty(deathParam))
            return;

        if (deathIsBool)
            animator.SetBool(deathParam, true);
        else
            animator.SetTrigger(deathParam);
    }

    void SetMoving(bool moving)
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(moveBool))
            animator.SetBool(moveBool, moving);

        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, moving ? moveSpeedValue : 0f);
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        while ((transform.position - destination).sqrMagnitude > 0.0025f)
        {
            Vector3 step = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);

            Vector3 look = destination - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(look), rotateSpeed * Time.deltaTime);

            transform.position = step;
            yield return null;
        }

        transform.position = destination;
    }

    IEnumerator FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            yield break;

        yield return FaceRotation(Quaternion.LookRotation(dir));
    }

    IEnumerator FaceRotation(Quaternion targetRotation)
    {
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = targetRotation;
    }
}
