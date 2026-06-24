using UnityEngine;

public class GameFlowCommands : MonoBehaviour
{
    public static GameFlowCommands Instance { get; private set; }

    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemyData forestWolfEncounter;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (gameManager == null)
            gameManager = GameManager.Instance;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadVillage() => gameManager.LoadScene(SceneNames.Village);
    public void LoadForest() => gameManager.LoadScene(SceneNames.Forest);

    public void SetFlag(string flagName) => gameManager.SetStoryFlag(flagName, true);
    public void ClearFlag(string flagName) => gameManager.SetStoryFlag(flagName, false);

    public void ActivateQuestWolf() => SetFlag("quest_wolf_active");
    public void CompleteQuestWolf()
    {
        SetFlag("quest_wolf_done");
        ClearFlag("quest_wolf_active");
    }

    public void StartForestWolfBattle()
    {
        if (forestWolfEncounter == null)
            return;

        gameManager.StartBattle(forestWolfEncounter);
    }

    public void StartBattleWithEnemy(EnemyData enemy)
    {
        gameManager.StartBattle(enemy);
    }
}
