using UnityEngine;
using UnityEngine.InputSystem;
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
        EnsureSayDialog();
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

    void Update()
    {
        if (!IsNarrativeActive() || Keyboard.current == null)
            return;

        if (!Keyboard.current.spaceKey.wasPressedThisFrame)
            return;

        AdvanceSayDialog();
    }

    static bool IsNarrativeActive()
    {
        if (GameManager.Instance == null)
            return false;

        return GameManager.Instance.CurrentState == GameState.Dialogue
            || GameManager.Instance.CurrentState == GameState.Cutscene;
    }

    static void AdvanceSayDialog()
    {
        SayDialog sayDialog = SayDialog.GetSayDialog();
        if (sayDialog == null)
            return;

        DialogInput dialogInput = sayDialog.GetComponent<DialogInput>();
        dialogInput?.SetNextLineFlag();
    }

    public void TriggerFlowchartBlock(Flowchart flowchart, string blockName)
    {
        if (flowchart == null)
        {
            Debug.LogError("[FungusBridge] Flowchart reference is missing.");
            return;
        }

        if (!flowchart.HasBlock(blockName))
        {
            Debug.LogError($"[FungusBridge] Block '{blockName}' not found on Flowchart '{flowchart.name}'.");
            return;
        }

        flowchart.ExecuteBlock(blockName);
    }

    static void EnsureSayDialog()
    {
        if (SayDialog.GetSayDialog() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>("Prefabs/SayDialog");
        if (prefab == null)
        {
            Debug.LogError("[FungusBridge] SayDialog prefab missing at Resources/Prefabs/SayDialog.");
            return;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "SayDialog";
        DontDestroyOnLoad(instance);
    }

    void HandleBlockStart(Block block)
    {
        if (gameManager == null) return;

        bool isCutscene = block.BlockName.StartsWith("CS_");
        gameManager.SetState(isCutscene ? GameState.Cutscene : GameState.Dialogue);
        GameEvents.RaiseDialogueStarted();
    }

    void HandleBlockEnd(Block block)
    {
        if (gameManager == null) return;
        
        gameManager.SetState(GameState.Exploring);
        GameEvents.RaiseDialogueFinished();
    }
}
