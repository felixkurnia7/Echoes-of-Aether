using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Moves an NPC along an ordered list of waypoints. Each waypoint can run an
/// optional action when reached (a Fungus block and/or a UnityEvent) and the
/// NPC will pause there until that action finishes before walking to the next
/// waypoint. Rotates to face the direction of travel and (optionally) drives
/// an Animator.
/// </summary>
[DisallowMultipleComponent]
public class NPCWalker : MonoBehaviour
{
    [Serializable]
    public class Waypoint
    {
        [Tooltip("Where the NPC walks to.")]
        public Transform point;

        [Tooltip("Wait until the player is nearby before running this waypoint's action. The NPC idles here while the player is still exploring.")]
        public bool waitForPlayerNearby = true;

        [Header("Action on reaching this point (optional)")]
        [Tooltip("Fungus block to run when the NPC arrives here (e.g. a Say or a battle trigger).")]
        public Flowchart flowchart;
        public string blockName;

        [Tooltip("Pause walking until the Fungus block above finishes before moving on.")]
        public bool waitForBlock = true;

        [Tooltip("Optional: after the block, wait here until the sub-objective with this id is completed (e.g. 'signboard'). Leave empty to skip.")]
        public string waitForSubObjectiveId = "";

        [Tooltip("Optional: after the block, wait here until this GameManager story flag becomes true. Leave empty to skip.")]
        public string waitForStoryFlag = "";

        [Tooltip("Extra seconds to wait at this point (applied after the block / flag, if any).")]
        public float pauseSeconds = 0f;

        [Tooltip("Anything else to fire when the NPC reaches this point.")]
        public UnityEvent onReached;
    }

    [Header("Path")]
    [SerializeField] private List<Waypoint> waypoints = new();
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float turnSpeed = 10f;
    [Tooltip("How close (meters) the NPC must get to a waypoint before it counts as reached.")]
    [SerializeField] private float arriveThreshold = 0.15f;
    [Tooltip("Keep the NPC at its current height while walking (recommended for flat ground).")]
    [SerializeField] private bool keepCurrentHeight = true;
    [SerializeField] private bool walkOnStart = false;

    [Header("Player Proximity")]
    [Tooltip("Player transform used for the 'wait for player nearby' gate. Auto-found if left empty.")]
    [SerializeField] private Transform player;
    [Tooltip("How close (meters) the player must be to trigger a waypoint's action.")]
    [SerializeField] private float playerNearbyRadius = 3f;

    [Header("Animation (optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isMovingParam = "IsMoving";
    [Tooltip("Forward/back blend parameter. Left empty = ignored.")]
    [SerializeField] private string moveYParam = "MoveY";
    [Tooltip("Strafe blend parameter. Left empty = ignored.")]
    [SerializeField] private string moveXParam = "MoveX";

    [Header("On Finished (after the last waypoint)")]
    [SerializeField] private UnityEvent onFinished;

    public bool IsWalking { get; private set; }

    Coroutine routine;
    int speedHash, isMovingHash, moveXHash, moveYHash;
    Action finishedCallback;

    Block awaitedBlock;
    bool awaitedBlockFinished;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        speedHash = Animator.StringToHash(speedParam);
        isMovingHash = Animator.StringToHash(isMovingParam);
        moveXHash = Animator.StringToHash(moveXParam);
        moveYHash = Animator.StringToHash(moveYParam);
    }

    void OnEnable()
    {
        BlockSignals.OnBlockEnd += HandleBlockEnd;
    }

    void OnDisable()
    {
        BlockSignals.OnBlockEnd -= HandleBlockEnd;
    }

    void Start()
    {
        if (walkOnStart)
            StartWalk();
    }

    public void StartWalk() => StartWalk(null);

    public void StartWalk(Action onComplete)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning($"[NPCWalker] '{name}' has no waypoints assigned.");
            onComplete?.Invoke();
            return;
        }

        finishedCallback = onComplete;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(WalkRoutine());
    }

    public void StopWalk()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = null;
        IsWalking = false;
        awaitedBlock = null;
        ApplyAnimation(false);
    }

    IEnumerator WalkRoutine()
    {
        IsWalking = true;

        foreach (Waypoint waypoint in waypoints)
        {
            if (waypoint == null || waypoint.point == null)
                continue;

            yield return MoveTo(waypoint.point);

            if (waypoint.waitForPlayerNearby)
                yield return WaitForPlayerNearby();

            waypoint.onReached?.Invoke();

            if (waypoint.flowchart != null && !string.IsNullOrEmpty(waypoint.blockName))
                yield return RunBlock(waypoint.flowchart, waypoint.blockName, waypoint.waitForBlock);

            if (!string.IsNullOrEmpty(waypoint.waitForSubObjectiveId))
                yield return WaitForSubObjective(waypoint.waitForSubObjectiveId);

            if (!string.IsNullOrEmpty(waypoint.waitForStoryFlag))
                yield return WaitForStoryFlag(waypoint.waitForStoryFlag);

            if (waypoint.pauseSeconds > 0f)
                yield return new WaitForSeconds(waypoint.pauseSeconds);
        }

        IsWalking = false;
        ApplyAnimation(false);
        routine = null;

        onFinished?.Invoke();

        Action cb = finishedCallback;
        finishedCallback = null;
        cb?.Invoke();
    }

    IEnumerator MoveTo(Transform target)
    {
        while (true)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance <= arriveThreshold)
                break;

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
            yield return null;
        }

        ApplyAnimation(false);
    }

    IEnumerator WaitForPlayerNearby()
    {
        EnsurePlayer();
        if (player == null)
            yield break;

        ApplyAnimation(false);
        float sqrRadius = playerNearbyRadius * playerNearbyRadius;

        while (true)
        {
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= sqrRadius)
                break;

            yield return null;
        }
    }

    IEnumerator WaitForSubObjective(string id)
    {
        ApplyAnimation(false);

        while (ObjectiveManager.Instance == null || !ObjectiveManager.Instance.IsSubObjectiveCompleted(id))
            yield return null;
    }

    IEnumerator WaitForStoryFlag(string flag)
    {
        ApplyAnimation(false);

        while (GameManager.Instance == null || !GameManager.Instance.GetStoryFlag(flag))
            yield return null;
    }

    void EnsurePlayer()
    {
        if (player != null)
            return;

        PlayerController found = FindFirstObjectByType<PlayerController>();
        if (found != null)
            player = found.transform;
    }

    IEnumerator RunBlock(Flowchart flowchart, string blockName, bool wait)
    {
        if (!flowchart.HasBlock(blockName))
        {
            Debug.LogError($"[NPCWalker] Block '{blockName}' not found on '{flowchart.name}'.");
            yield break;
        }

        Block block = flowchart.FindBlock(blockName);
        awaitedBlock = wait ? block : null;
        awaitedBlockFinished = false;

        flowchart.ExecuteBlock(blockName);

        if (!wait)
            yield break;

        while (!awaitedBlockFinished)
            yield return null;

        awaitedBlock = null;
    }

    void HandleBlockEnd(Block block)
    {
        if (awaitedBlock != null && block == awaitedBlock)
            awaitedBlockFinished = true;
    }

    void ApplyAnimation(bool moving)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedHash, moving ? moveSpeed : 0f);
        if (!string.IsNullOrEmpty(isMovingParam))
            animator.SetBool(isMovingHash, moving);
        if (!string.IsNullOrEmpty(moveYParam))
            animator.SetFloat(moveYHash, moving ? 1f : 0f);
        if (!string.IsNullOrEmpty(moveXParam))
            animator.SetFloat(moveXHash, 0f);
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints == null)
            return;

        Gizmos.color = Color.cyan;
        Vector3 previous = transform.position;
        foreach (Waypoint waypoint in waypoints)
        {
            if (waypoint == null || waypoint.point == null)
                continue;

            Gizmos.DrawSphere(waypoint.point.position, 0.2f);
            Gizmos.DrawLine(previous, waypoint.point.position);

            if (waypoint.waitForPlayerNearby)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
                Gizmos.DrawWireSphere(waypoint.point.position, playerNearbyRadius);
                Gizmos.color = Color.cyan;
            }

            previous = waypoint.point.position;
        }
    }
}
