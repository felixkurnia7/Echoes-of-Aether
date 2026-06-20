using UnityEngine;

public class GameFlowCommands : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemyData forestWolfEncounter;

    void Awake()
    {
        if (gameManager == null)
            gameManager = GameManager.Instance;
    }

    public void LoadVillage() => gameManager.LoadScene(SceneNames.Village);
    public void LoadForest() => gameManager.LoadScene(SceneNames.Forest);

    public void SetFlag(string flagName) => gameManager.SetStoryFlag(flagName, true);
    public void ClearFlag(string flagName) => gameManager.SetStoryFlag(flagName, false);

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
