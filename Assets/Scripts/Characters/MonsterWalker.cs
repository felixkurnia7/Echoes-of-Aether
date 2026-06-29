using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a monster around the world. By default it patrols an ordered list of
/// points (loop or ping-pong). Optionally it can chase the player when they come
/// within range and return to patrolling when the player gets away. All movement
/// and Animator handling is inherited from <see cref="CharacterMover"/>; for the
/// Bear, set the inherited Move/Idle Bool Param to "WalkForward" / "Idle".
/// </summary>
public class MonsterWalker : CharacterMover
{
    [Header("Patrol")]
    [SerializeField] private List<Transform> patrolPoints = new();
    [Tooltip("Loop back to the first point after the last (otherwise ping-pong back and forth).")]
    [SerializeField] private bool loop = true;
    [Tooltip("Seconds to pause at each patrol point.")]
    [SerializeField] private float waitAtPoint = 1f;
    [SerializeField] private bool patrolOnStart = true;

    [Header("Chase (optional)")]
    [SerializeField] private bool canChase = false;
    [Tooltip("Player transform. Auto-found at runtime if left empty.")]
    [SerializeField] private Transform player;
    [Tooltip("Start chasing when the player is within this distance.")]
    [SerializeField] private float detectionRadius = 6f;
    [Tooltip("Give up the chase once the player is beyond this distance.")]
    [SerializeField] private float loseInterestRadius = 10f;
    [Tooltip("How close the monster gets before stopping in front of the player.")]
    [SerializeField] private float stopDistance = 1.5f;

    public bool IsActive { get; private set; }

    Coroutine routine;
    int patrolIndex;
    int patrolDirection = 1;

    void Start()
    {
        if (patrolOnStart)
            StartPatrol();
    }

    public override void BeginMovement(Action onComplete)
    {
        StartPatrol();
        onComplete?.Invoke();
    }

    public override void StopMovement() => StopPatrol();

    public void StartPatrol()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(BehaviourRoutine());
    }

    public void StopPatrol()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = null;
        IsActive = false;
        ApplyAnimation(false);
    }

    IEnumerator BehaviourRoutine()
    {
        IsActive = true;

        while (true)
        {
            if (canChase && PlayerInRange(detectionRadius))
                yield return ChaseRoutine();
            else
                yield return PatrolStep();
        }
    }

    IEnumerator PatrolStep()
    {
        if (patrolPoints == null || patrolPoints.Count == 0)
        {
            ApplyAnimation(false);
            yield return null;
            yield break;
        }

        Transform target = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Count - 1)];
        if (target != null)
            yield return MoveTo(target);

        if (waitAtPoint > 0f)
        {
            ApplyAnimation(false);
            float timer = 0f;
            while (timer < waitAtPoint)
            {
                if (canChase && PlayerInRange(detectionRadius))
                    yield break;

                timer += Time.deltaTime;
                yield return null;
            }
        }

        AdvancePatrolIndex();
    }

    void AdvancePatrolIndex()
    {
        int count = patrolPoints.Count;
        if (count <= 1)
        {
            patrolIndex = 0;
            return;
        }

        if (loop)
        {
            patrolIndex = (patrolIndex + 1) % count;
            return;
        }

        if (patrolIndex + patrolDirection < 0 || patrolIndex + patrolDirection >= count)
            patrolDirection = -patrolDirection;

        patrolIndex += patrolDirection;
    }

    IEnumerator ChaseRoutine()
    {
        EnsurePlayer();
        if (player == null)
            yield break;

        while (PlayerInRange(loseInterestRadius))
        {
            if (PlanarDistanceTo(player.position) > stopDistance)
                StepToward(player.position);
            else
                ApplyAnimation(false);

            yield return null;
        }

        ApplyAnimation(false);
    }

    bool PlayerInRange(float radius)
    {
        EnsurePlayer();
        if (player == null)
            return false;

        return PlanarDistanceTo(player.position) <= radius;
    }

    float PlanarDistanceTo(Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    void EnsurePlayer()
    {
        if (player != null)
            return;

        PlayerController found = FindFirstObjectByType<PlayerController>();
        if (found != null)
            player = found.transform;
    }

    void OnDrawGizmosSelected()
    {
        if (patrolPoints != null)
        {
            Gizmos.color = Color.red;
            Vector3 previous = transform.position;
            foreach (Transform p in patrolPoints)
            {
                if (p == null)
                    continue;

                Gizmos.DrawSphere(p.position, 0.2f);
                Gizmos.DrawLine(previous, p.position);
                previous = p.position;
            }
        }

        if (canChase)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, loseInterestRadius);
        }
    }
}
