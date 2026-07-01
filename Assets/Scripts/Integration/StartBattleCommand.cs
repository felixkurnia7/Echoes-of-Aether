using System.Collections.Generic;
using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Start Battle", "Starts a turn-based battle with one or more enemies and optionally sets a story flag on victory.")]
[AddComponentMenu("")]
public class StartBattleCommand : Command
{
    [Tooltip("Enemies to fight (drag one or more EnemyData assets).")]
    [SerializeField] private List<EnemyData> enemies = new();

    [Tooltip("Legacy single-enemy slot. If the list above is empty, this asset is used instead.")]
    [SerializeField] private EnemyData enemy;

    [Tooltip("Optional: story flag set to true when the player wins (e.g. 'bear_defeated'). Used to resume the NPC walk afterwards.")]
    [SerializeField] private string victoryFlag = "";

    [Tooltip("Optional: sub-objective marked complete on the HUD when the player wins (drag & drop, e.g. 'encounter_bear').")]
    [SerializeField] private SubObjectiveData completeOnVictory;

    [Tooltip("Show NPC Knight battle tutorial popups during this fight (e.g. the first bear encounter).")]
    [SerializeField] private bool battleTutorial;

    public override void OnEnter()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[StartBattle] GameManager not found. Play from the Bootstrap scene.");
            Continue();
            return;
        }

        EnemyData[] lineup = BuildEnemyLineup();
        if (lineup.Length == 0)
        {
            Debug.LogError("[StartBattle] No enemy assigned.");
            Continue();
            return;
        }

        GameManager.Instance.StartBattle(lineup, victoryFlag, completeOnVictory, battleTutorial);
        // Scene changes to Battle; no Continue() needed.
    }

    EnemyData[] BuildEnemyLineup()
    {
        var lineup = new List<EnemyData>();
        foreach (EnemyData data in enemies)
        {
            if (data != null)
                lineup.Add(data);
        }

        if (lineup.Count == 0 && enemy != null)
            lineup.Add(enemy);

        return lineup.ToArray();
    }

    public override string GetSummary()
    {
        EnemyData[] lineup = BuildEnemyLineup();
        if (lineup.Length == 0)
            return "Error: no enemy";

        string names = lineup.Length == 1
            ? lineup[0].name
            : $"{lineup.Length} enemies";

        string suffix = "";
        if (!string.IsNullOrEmpty(victoryFlag))
            suffix += $" (flag: {victoryFlag})";
        if (completeOnVictory != null)
            suffix += $" (done: {completeOnVictory.name})";
        if (battleTutorial)
            suffix += " (tutorial)";

        return names + suffix;
    }

    public override Color GetButtonColor()
    {
        return new Color32(235, 170, 170, 255);
    }
}
