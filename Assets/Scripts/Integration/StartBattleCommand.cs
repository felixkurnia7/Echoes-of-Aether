using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Start Battle", "Starts a turn-based battle with the given enemy and optionally sets a story flag on victory.")]
[AddComponentMenu("")]
public class StartBattleCommand : Command
{
    [Tooltip("Enemy to fight (EnemyData asset).")]
    [SerializeField] private EnemyData enemy;

    [Tooltip("Optional: story flag set to true when the player wins (e.g. 'bear_defeated'). Used to resume the NPC walk afterwards.")]
    [SerializeField] private string victoryFlag = "";

    public override void OnEnter()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[StartBattle] GameManager not found. Play from the Bootstrap scene.");
            Continue();
            return;
        }

        if (enemy == null)
        {
            Debug.LogError("[StartBattle] No enemy assigned.");
            Continue();
            return;
        }

        GameManager.Instance.StartBattle(enemy, victoryFlag);
        // Scene changes to Battle; no Continue() needed.
    }

    public override string GetSummary()
    {
        if (enemy == null)
            return "Error: no enemy";

        return string.IsNullOrEmpty(victoryFlag)
            ? enemy.name
            : $"{enemy.name} (flag: {victoryFlag})";
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 170, 170, 255);
    }
}
