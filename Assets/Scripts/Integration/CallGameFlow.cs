using Fungus;
using UnityEngine;

[CommandInfo("Scripting", "Call Game Flow", "Calls GameFlowCommands without assigning a scene target object.")]
[AddComponentMenu("")]
public class CallGameFlow : Command
{
    public enum ActionType
    {
        LoadForest,
        LoadVillage,
        ActivateQuestWolf,
        CompleteQuestWolf,
        StartForestWolfBattle
    }

    [SerializeField] private ActionType action = ActionType.LoadForest;

    public override void OnEnter()
    {
        GameFlowCommands flow = GameFlowCommands.Instance;
        if (flow == null)
        {
            Debug.LogError("[CallGameFlow] GameFlowCommands not found. Play from Bootstrap scene.");
            Continue();
            return;
        }

        switch (action)
        {
            case ActionType.LoadForest:
                flow.LoadForest();
                break;
            case ActionType.LoadVillage:
                flow.LoadVillage();
                break;
            case ActionType.ActivateQuestWolf:
                flow.ActivateQuestWolf();
                break;
            case ActionType.CompleteQuestWolf:
                flow.CompleteQuestWolf();
                break;
            case ActionType.StartForestWolfBattle:
                flow.StartForestWolfBattle();
                break;
        }

        Continue();
    }

    public override string GetSummary() => action.ToString();
}
