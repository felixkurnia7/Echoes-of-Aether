using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Control NPC", "Starts or stops an NPCWalker moving along its waypoints.")]
[AddComponentMenu("")]
public class ControlNpcCommand : Command
{
    public enum Action
    {
        StartWalk,
        StopWalk
    }

    [Tooltip("The NPC (with an NPCWalker component) to control.")]
    [SerializeField] private NPCWalker target;

    [SerializeField] private Action action = Action.StartWalk;

    [Tooltip("If enabled (StartWalk only), the flowchart waits until the NPC reaches its destination before continuing.")]
    [SerializeField] private bool waitUntilArrived = false;

    public override void OnEnter()
    {
        if (target == null)
        {
            Debug.LogError("[ControlNpc] No NPCWalker target assigned.");
            Continue();
            return;
        }

        if (action == Action.StopWalk)
        {
            target.StopWalk();
            Continue();
            return;
        }

        if (waitUntilArrived)
        {
            target.StartWalk(Continue);
            return;
        }

        target.StartWalk();
        Continue();
    }

    public override string GetSummary()
    {
        string targetName = target != null ? target.name : "<none>";
        return $"{action} -> {targetName}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
