using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives a character (typically the Player) along a scripted path during a
/// cutscene, when normal input control is suspended. It temporarily disables the
/// listed control behaviours (e.g. PlayerController, PlayerInputHandler,
/// CharacterController) while it walks, then restores them. Each point can stop
/// and wait for a sub-objective to complete before continuing. Movement and
/// Animator handling are inherited from <see cref="CharacterMover"/>.
/// Call <see cref="Play()"/> from a UnityEvent or a Fungus command.
/// </summary>
public class PlayerCutsceneMover : CharacterMover
{
    [Serializable]
    public class CutscenePoint
    {
        [Tooltip("Where the character walks to.")]
        public Transform point;

        [Tooltip("After arriving, stop and wait until this sub-objective asset is completed before continuing (drag & drop). Leave empty to skip.")]
        public SubObjectiveData waitForSubObjective;

        [Tooltip("While waiting for the sub-objective above, temporarily restore player control so the player can complete it, then suspend control again before continuing.")]
        public bool enableControlWhileWaiting = false;

        [Tooltip("Extra seconds to wait at this point (after any sub-objective wait).")]
        public float pauseSeconds = 0f;
    }

    [Header("Cutscene Path")]
    [SerializeField] private List<CutscenePoint> path = new();

    [Header("Control To Suspend While Moving")]
    [Tooltip("Behaviours disabled while the cutscene move runs and re-enabled afterwards (e.g. PlayerController, PlayerInputHandler, CharacterController).")]
    [SerializeField] private List<Behaviour> controlToDisable = new();

    [Header("Resume After Battle (optional)")]
    [Tooltip("If this GameManager story flag is already true when the scene starts (e.g. 'bear_defeated'), the player teleports to 'Resume From Point Index' and continues the path from the next point.")]
    [SerializeField] private string resumeIfStoryFlag = "";
    [Tooltip("Path point the player had reached before the battle. On resume it teleports here, then walks to the next point.")]
    [SerializeField] private int resumeFromPointIndex = 0;

    public bool IsPlaying { get; private set; }

    Coroutine routine;
    Action finishedCallback;

    public override void BeginMovement(Action onComplete) => Play(onComplete);

    public override void StopMovement() => Stop();

    void Start()
    {
        if (!string.IsNullOrEmpty(resumeIfStoryFlag)
            && GameManager.Instance != null
            && GameManager.Instance.GetStoryFlag(resumeIfStoryFlag))
        {
            ResumeAfterBattle();
        }
    }

    // After a battle the scene reloads, so instead of replaying the whole path
    // from the start we snap the player onto the point it had already reached
    // and continue from the next one.
    void ResumeAfterBattle()
    {
        SnapToPoint(resumeFromPointIndex);

        int next = resumeFromPointIndex + 1;
        if (path != null && next < path.Count)
            Play(next, null);
    }

    void SnapToPoint(int index)
    {
        if (path == null || index < 0 || index >= path.Count)
            return;

        CutscenePoint cp = path[index];
        if (cp != null)
            TeleportTo(cp.point);
    }

    /// <summary>Walk the assigned path.</summary>
    public void Play() => Play(null);

    /// <summary>Walk the assigned path, firing a callback when finished.</summary>
    public void Play(Action onComplete) => Play(0, onComplete);

    /// <summary>Walk the assigned path from a given point index, firing a callback when finished.</summary>
    public void Play(int startIndex, Action onComplete)
    {
        if (path == null || path.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        finishedCallback = onComplete;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PlayRoutine(Mathf.Clamp(startIndex, 0, path.Count)));
    }

    public void Stop()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = null;
        RestoreControl();
        IsPlaying = false;
        ApplyAnimation(false);
    }

    IEnumerator PlayRoutine(int startIndex)
    {
        IsPlaying = true;
        SuspendControl();

        for (int i = startIndex; i < path.Count; i++)
        {
            CutscenePoint waypoint = path[i];
            if (waypoint == null || waypoint.point == null)
                continue;

            yield return MoveTo(waypoint.point);

            if (waypoint.waitForSubObjective != null)
                yield return WaitForSubObjective(waypoint);

            if (waypoint.pauseSeconds > 0f)
            {
                ApplyAnimation(false);
                yield return new WaitForSeconds(waypoint.pauseSeconds);
            }
        }

        ApplyAnimation(false);
        RestoreControl();
        IsPlaying = false;
        routine = null;

        Action cb = finishedCallback;
        finishedCallback = null;
        cb?.Invoke();
    }

    IEnumerator WaitForSubObjective(CutscenePoint waypoint)
    {
        ApplyAnimation(false);

        bool controlRestored = false;
        if (waypoint.enableControlWhileWaiting)
        {
            RestoreControl();
            controlRestored = true;
        }

        while (ObjectiveManager.Instance == null
            || !ObjectiveManager.Instance.IsSubObjectiveCompleted(waypoint.waitForSubObjective))
            yield return null;

        if (controlRestored)
            SuspendControl();
    }

    void SuspendControl()
    {
        foreach (Behaviour b in controlToDisable)
            if (b != null)
                b.enabled = false;

        // Keep CharacterController enabled so StepToward can Move() with gravity.
        ResetVerticalVelocity();
    }

    void RestoreControl()
    {
        ResetVerticalVelocity();

        foreach (Behaviour b in controlToDisable)
            if (b != null)
                b.enabled = true;

        if (TryGetComponent(out PlayerController playerController))
            playerController.ResetMovementState();
    }

    void OnDrawGizmosSelected()
    {
        if (path == null)
            return;

        Gizmos.color = Color.green;
        Vector3 previous = transform.position;
        foreach (CutscenePoint waypoint in path)
        {
            if (waypoint == null || waypoint.point == null)
                continue;

            Gizmos.DrawSphere(waypoint.point.position, 0.2f);
            Gizmos.DrawLine(previous, waypoint.point.position);
            previous = waypoint.point.position;
        }
    }
}
