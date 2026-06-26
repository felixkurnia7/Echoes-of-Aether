using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Set Objective", "Shows, updates, or completes the on-screen objective HUD.")]
[AddComponentMenu("")]
public class SetObjectiveCommand : Command
{
    public enum Mode
    {
        Set,
        Complete,
        Hide,
        AddSubObjective,
        CompleteSubObjective,
        ClearSubObjectives
    }

    [SerializeField] private Mode mode = Mode.Set;

    [Tooltip("Objective text. Used by Set and AddSubObjective.")]
    [TextArea(1, 3)]
    [SerializeField] private string objectiveText = "";

    [Tooltip("Unique id for the sub-objective. Used by AddSubObjective and CompleteSubObjective.")]
    [SerializeField] private string subObjectiveId = "";

    public override void OnEnter()
    {
        ObjectiveManager manager = ObjectiveManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[SetObjective] ObjectiveManager not found.");
            Continue();
            return;
        }

        switch (mode)
        {
            case Mode.Set:
                manager.SetObjective(objectiveText);
                break;
            case Mode.Complete:
                manager.CompleteObjective();
                break;
            case Mode.Hide:
                manager.HideObjective();
                break;
            case Mode.AddSubObjective:
                manager.AddSubObjective(subObjectiveId, objectiveText);
                break;
            case Mode.CompleteSubObjective:
                manager.CompleteSubObjective(subObjectiveId);
                break;
            case Mode.ClearSubObjectives:
                manager.ClearSubObjectives();
                break;
        }

        Continue();
    }

    public override string GetSummary()
    {
        switch (mode)
        {
            case Mode.Set:
                return string.IsNullOrEmpty(objectiveText) ? "Set: <empty>" : "Set: " + objectiveText;
            case Mode.AddSubObjective:
                return $"+ Sub [{subObjectiveId}]: {objectiveText}";
            case Mode.CompleteSubObjective:
                return $"Done Sub [{subObjectiveId}]";
            default:
                return mode.ToString();
        }
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
