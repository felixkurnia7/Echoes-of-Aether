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
/// waypoint. Core locomotion and Animator handling come from
/// <see cref="CharacterMover"/>.
/// </summary>
public class NPCWalker : CharacterMover
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

        [Tooltip("Optional: after the block, wait here until this sub-objective asset is completed (drag & drop). Leave empty to skip.")]
        public SubObjectiveData waitForSubObjective;

        [Tooltip("Optional: after the block, wait here until this GameManager story flag becomes true. Leave empty to skip.")]
        public string waitForStoryFlag = "";

        [Tooltip("Extra seconds to wait at this point (applied after the block / flag, if any).")]
        public float pauseSeconds = 0f;

        [Tooltip("Anything else to fire when the NPC reaches this point.")]
        public UnityEvent onReached;
    }

    [Header("Path")]
    [SerializeField] private List<Waypoint> waypoints = new();
    [SerializeField] private bool walkOnStart = false;

    [Header("Player Proximity")]
    [Tooltip("Player transform used for the 'wait for player nearby' gate. Auto-found if left empty.")]
    [SerializeField] private Transform player;
    [Tooltip("How close (meters) the player must be to trigger a waypoint's action.")]
    [SerializeField] private float playerNearbyRadius = 3f;

    [Header("On Finished (after the last waypoint)")]
    [SerializeField] private UnityEvent onFinished;

    [Header("Resume After Battle (optional)")]
    [Tooltip("If this GameManager story flag is already true when the scene starts (e.g. 'bear_defeated'), the NPC auto-starts walking from 'Resume From Waypoint Index'.")]
    [SerializeField] private string resumeIfStoryFlag = "";
    [Tooltip("Waypoint the NPC had reached before the battle. On resume it teleports here, then walks to the next waypoint (its battle-trigger action is not re-run).")]
    [SerializeField] private int resumeFromWaypointIndex = 0;

    public bool IsWalking { get; private set; }

    Coroutine routine;
    Action finishedCallback;

    Block awaitedBlock;
    bool awaitedBlockFinished;

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
        {
            StartWalk();
            return;
        }

        if (!string.IsNullOrEmpty(resumeIfStoryFlag)
            && GameManager.Instance != null
            && GameManager.Instance.GetStoryFlag(resumeIfStoryFlag))
        {
            ResumeAfterBattle();
        }
    }

    // After a battle the scene reloads, so instead of walking all the way from
    // the spawn point we snap the NPC onto the waypoint it had already reached
    // and continue from the next one.
    void ResumeAfterBattle()
    {
        SnapToWaypoint(resumeFromWaypointIndex);

        int next = resumeFromWaypointIndex + 1;
        if (waypoints != null && next < waypoints.Count)
            StartWalk(next, null);
    }

    void SnapToWaypoint(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Count)
            return;

        Waypoint wp = waypoints[index];
        if (wp != null)
            TeleportTo(wp.point);
    }

    public override void BeginMovement(Action onComplete) => StartWalk(0, onComplete);

    public override void StopMovement() => StopWalk();

    public void StartWalk() => StartWalk(0, null);

    public void StartWalk(Action onComplete) => StartWalk(0, onComplete);

    public void StartWalk(int startIndex, Action onComplete)
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

        routine = StartCoroutine(WalkRoutine(Mathf.Max(0, startIndex)));
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

    IEnumerator WalkRoutine(int startIndex)
    {
        IsWalking = true;

        for (int i = startIndex; i < waypoints.Count; i++)
        {
            Waypoint waypoint = waypoints[i];
            if (waypoint == null || waypoint.point == null)
                continue;

            yield return MoveTo(waypoint.point);

            if (waypoint.waitForPlayerNearby)
                yield return WaitForPlayerNearby();

            waypoint.onReached?.Invoke();

            if (waypoint.flowchart != null && !string.IsNullOrEmpty(waypoint.blockName))
                yield return RunBlock(waypoint.flowchart, waypoint.blockName, waypoint.waitForBlock);

            if (waypoint.waitForSubObjective != null)
                yield return WaitForSubObjective(waypoint.waitForSubObjective.Id);

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
