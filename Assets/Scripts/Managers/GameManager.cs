using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Bootstrap")]
    [SerializeField] private string initialScene = SceneNames.Village;

    [Header("Battle")]
    [SerializeField] private CharacterData defaultPlayerCharacter;
    [SerializeField] private string battleSceneName = SceneNames.Battle;
    public GameState CurrentState { get; private set; } = GameState.Exploring;
    public bool IsPaused {get; private set;}

    private readonly Dictionary<string, bool> storyFlags = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start() 
    {
        if (SceneManager.GetActiveScene().name == SceneNames.Bootstrap)
            LoadScene(initialScene);
    }

    private void Update() 
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }
    
    public void SetState(GameState state)
    {
        CurrentState = state;
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void SetStoryFlag(string flagName, bool value)
    {
        storyFlags[flagName] = value;
    }

    public bool GetStoryFlag(string flagName)
    {
        return storyFlags.TryGetValue(flagName, out bool value) && value;
    }

    public void StartBattle(EnemyData enemy)
    {
        StartBattle(new[] { enemy }, null);
    }

    public void StartBattle(EnemyData enemy, string victoryFlag, SubObjectiveData victorySubObjective = null)
    {
        StartBattle(new[] { enemy }, victoryFlag, victorySubObjective);
    }

    public void StartBattle(EnemyData[] enemies)
    {
        StartBattle(enemies, null);
    }

    public void StartBattle(EnemyData[] enemies, string victoryFlag, SubObjectiveData victorySubObjective = null, bool battleTutorial = false)
    {
        if (enemies == null || enemies.Length == 0) return;

        string returnScene = SceneManager.GetActiveScene().name;

        BattleSessionData.SetSession(defaultPlayerCharacter, enemies, returnScene, victoryFlag, victorySubObjective, battleTutorial);
        SetState(GameState.Battle);
        GameEvents.RaiseBattleStart();
        LoadScene(battleSceneName);
    }

    public void EndBattle(bool victory)
    {
        string returnScene = BattleSessionData.ReturnSceneName;
        string victoryFlag = BattleSessionData.VictoryFlag;
        SubObjectiveData victorySub = BattleSessionData.VictorySubObjective;

        if (victory)
        {
            if (!string.IsNullOrEmpty(victoryFlag))
                SetStoryFlag(victoryFlag, true);

            if (victorySub != null && ObjectiveManager.Instance != null)
                ObjectiveManager.Instance.CompleteSubObjective(victorySub);
        }

        BattleSessionData.Clear();
        SetState(GameState.Exploring);
        GameEvents.RaiseBattleEnd();

        if (!string.IsNullOrEmpty(returnScene))
            LoadScene(returnScene);
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Battle || CurrentState == GameState.Dialogue) return;

        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        SetState(IsPaused ? GameState.Paused : GameState.Exploring);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SetState(GameState.Exploring);
    }
}
