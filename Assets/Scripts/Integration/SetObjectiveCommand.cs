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

    [Tooltip("Objective asset. When assigned, Set uses this instead of the text below (drag & drop).")]
    [SerializeField] private ObjectiveData objectiveData;

    [Tooltip("Sub-objective asset. When assigned, AddSubObjective/CompleteSubObjective use this instead of the id/text below (drag & drop).")]
    [SerializeField] private SubObjectiveData subObjectiveData;

    [Tooltip("When Set uses an Objective asset, also add the asset's sub-objectives.")]
    [SerializeField] private bool includeSubObjectives = true;

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
                if (objectiveData == null)
                    Debug.LogWarning("[SetObjective] No Objective asset assigned.");
                manager.SetObjective(objectiveData, includeSubObjectives);
                break;
            case Mode.Complete:
                manager.CompleteObjective();
                break;
            case Mode.Hide:
                manager.HideObjective();
                break;
            case Mode.AddSubObjective:
                if (subObjectiveData == null)
                    Debug.LogWarning("[SetObjective] No Sub-objective asset assigned.");
                manager.AddSubObjective(subObjectiveData);
                break;
            case Mode.CompleteSubObjective:
                if (subObjectiveData == null)
                    Debug.LogWarning("[SetObjective] No Sub-objective asset assigned.");
                manager.CompleteSubObjective(subObjectiveData);
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
                return objectiveData != null ? "Set: " + objectiveData.name : "Set: <none>";
            case Mode.AddSubObjective:
                return subObjectiveData != null ? "+ Sub: " + subObjectiveData.name : "+ Sub: <none>";
            case Mode.CompleteSubObjective:
                return subObjectiveData != null ? "Done Sub: " + subObjectiveData.name : "Done Sub: <none>";
            default:
                return mode.ToString();
        }
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
