using Fungus;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NPCInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Flowchart flowchart;
    [SerializeField] private string startBlockName = "DLG_Start";
    [SerializeField] private string promptText = "Talk";
    [SerializeField] private string requiredStoryFlag;
    [SerializeField] private bool requireFlagActive = true;

    void Reset()
    {
        if (TryGetComponent(out Collider col))
            col.isTrigger = true;
    }

    public bool CanInteract(PlayerController player)
    {
        if (flowchart == null || string.IsNullOrEmpty(startBlockName))
            return false;

        if (!string.IsNullOrEmpty(requiredStoryFlag) && GameManager.Instance != null)
        {
            bool hasFlag = GameManager.Instance.GetStoryFlag(requiredStoryFlag);
            if (requireFlagActive ? !hasFlag : hasFlag)
                return false;
        }

        return true;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        if (FungusBridge.Instance == null)
        {
            Debug.LogError("[NPCInteractable] FungusBridge not found. Play from Bootstrap scene first.");
            return;
        }

        if (!flowchart.HasBlock(startBlockName))
        {
            Debug.LogError($"[NPCInteractable] Block '{startBlockName}' not found on '{name}'. Check Flowchart block name.");
            return;
        }

        Block block = flowchart.FindBlock(startBlockName);
        if (block != null && block.CommandList.Count == 0)
        {
            Debug.LogWarning($"[NPCInteractable] Block '{startBlockName}' on '{name}' has no commands. Add a Say command in Fungus.");
        }

        player.SetPlayerState(PlayerController.PlayerState.Interact);
        FungusBridge.Instance.TriggerFlowchartBlock(flowchart, startBlockName);
    }

    public string GetPromptText() => promptText;
}
