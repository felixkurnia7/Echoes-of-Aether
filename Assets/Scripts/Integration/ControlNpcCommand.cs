using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Control Mover", "Starts or stops any CharacterMover (NPCWalker, MonsterWalker, PlayerCutsceneMover) moving along its route.")]
[AddComponentMenu("")]
public class ControlNpcCommand : Command
{
    public enum Action
    {
        StartWalk,
        StopWalk
    }

    [Tooltip("The character to control. Accepts any CharacterMover: NPCWalker, MonsterWalker, or PlayerCutsceneMover.")]
    [SerializeField] private CharacterMover target;

    [SerializeField] private Action action = Action.StartWalk;

    [Tooltip("If enabled (StartWalk only), the flowchart waits until the mover finishes its route before continuing. Looping movers (e.g. patrol) continue immediately.")]
    [SerializeField] private bool waitUntilArrived = false;

    public override void OnEnter()
    {
        if (target == null)
        {
            Debug.LogError("[ControlMover] No CharacterMover target assigned.");
            Continue();
            return;
        }

        if (action == Action.StopWalk)
        {
            target.StopMovement();
            Continue();
            return;
        }

        if (waitUntilArrived)
        {
            target.BeginMovement(Continue);
            return;
        }

        target.BeginMovement();
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
