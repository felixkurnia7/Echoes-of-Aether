using UnityEngine;
using Fungus;

public class FungusBridge : MonoBehaviour
{
    public static FungusBridge Instance { get; private set; }
    [SerializeField] private GameManager gameManager;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    void OnEnable()
    {
        BlockSignals.OnBlockStart += HandleBlockStart;
        BlockSignals.OnBlockEnd += HandleBlockEnd;
    }
    void OnDisable()
    {
        BlockSignals.OnBlockStart -= HandleBlockStart;
        BlockSignals.OnBlockEnd -= HandleBlockEnd;
    }
    public void TriggerFlowchartBlock(Flowchart flowchart, string blockName)
    {
        if (flowchart == null) return;
        flowchart.ExecuteBlock(blockName);
    }
    void HandleBlockStart(Block block)
    {
        if (gameManager == null) return;
        gameManager.SetState(GameState.Dialogue);
        GameEvents.RaiseDialogueStarted();
    }
    void HandleBlockEnd(Block block)
    {
        if (gameManager == null) return;
        gameManager.SetState(GameState.Exploring);
        GameEvents.RaiseDialogueFinished();
    }
}
