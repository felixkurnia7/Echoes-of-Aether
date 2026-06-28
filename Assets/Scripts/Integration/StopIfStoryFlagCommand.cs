using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Stop If Story Flag", "Stops this block if a GameManager story flag matches the expected value. Use as the first command of an intro/cutscene block so it does not repeat after a scene reload.")]
[AddComponentMenu("")]
public class StopIfStoryFlagCommand : Command
{
    [Tooltip("GameManager story flag to check (persists across scene loads).")]
    [SerializeField] private string storyFlag = "";

    [Tooltip("Stop this block when the flag equals this value.")]
    [SerializeField] private bool stopWhenValueIs = true;

    public override void OnEnter()
    {
        if (string.IsNullOrEmpty(storyFlag) || GameManager.Instance == null)
        {
            Continue();
            return;
        }

        bool current = GameManager.Instance.GetStoryFlag(storyFlag);
        if (current == stopWhenValueIs)
            StopParentBlock();
        else
            Continue();
    }

    public override string GetSummary()
    {
        if (string.IsNullOrEmpty(storyFlag))
            return "Error: no flag";

        return $"stop if {storyFlag} == {stopWhenValueIs}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 200, 160, 255);
    }
}
