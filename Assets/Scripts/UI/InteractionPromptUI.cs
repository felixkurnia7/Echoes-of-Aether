using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(InteractionDetector))]
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private InteractionDetector detector;
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private string interactKeyLabel = "Space";
    [SerializeField] private bool createRuntimeUIIfMissing = true;

    void Awake()
    {
        if (detector == null)
            detector = GetComponent<InteractionDetector>();

        if (player == null)
            player = GetComponent<PlayerController>();

        if (promptRoot == null && createRuntimeUIIfMissing)
            CreateRuntimeUI();

        Hide();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (detector == null || player == null || promptRoot == null)
            return;

        if (!IsExplorationActive())
        {
            Hide();
            return;
        }

        IInteractable target = detector.currentTarget;
        if (target != null && target.CanInteract(player))
            Show(target.GetPromptText());
        else
            Hide();
    }

    static bool IsExplorationActive()
    {
        if (GameManager.Instance == null)
            return true;

        return GameManager.Instance.CurrentState == GameState.Exploring
            && !GameManager.Instance.IsPaused;
    }

    void Show(string actionText)
    {
        promptRoot.SetActive(true);

        if (promptLabel != null)
            promptLabel.text = $"[{interactKeyLabel}] {actionText}";
    }

    void Hide()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    void CreateRuntimeUI()
    {
        var canvasGo = new GameObject("InteractionPromptCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        promptRoot = new GameObject("PromptPanel", typeof(RectTransform), typeof(Image));
        promptRoot.transform.SetParent(canvasGo.transform, false);

        var panelRect = promptRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 48f);
        panelRect.sizeDelta = new Vector2(420f, 56f);

        var panelImage = promptRoot.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.65f);

        var textGo = new GameObject("PromptText", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(promptRoot.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        promptLabel = textGo.GetComponent<TMP_Text>();
        promptLabel.font = Resources.GetBuiltinResource<TMP_FontAsset>("LegacyRuntime.ttf");
        promptLabel.fontSize = 22;
        promptLabel.alignment = TMPro.TextAlignmentOptions.Center;
        promptLabel.color = Color.white;
    }
}
