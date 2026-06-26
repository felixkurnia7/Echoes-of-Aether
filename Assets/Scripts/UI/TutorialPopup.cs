using System;
using System.Collections;
using Fungus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modal tutorial popup with a title, body text and an OK button.
/// Auto-creates itself before the first scene loads (like ObjectiveManager/HUD),
/// builds its UI in code, and persists across scene loads. No manual scene wiring required.
/// </summary>
public class TutorialPopup : MonoBehaviour
{
    public static TutorialPopup Instance { get; private set; }

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.13f, 0.98f);
    static readonly Color AccentColor = new Color(1f, 0.83f, 0.36f);
    static readonly Color BodyColor = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color ButtonColor = new Color(1f, 0.83f, 0.36f);
    static readonly Color ButtonTextColor = new Color(0.08f, 0.08f, 0.1f);

    const float FadeDuration = 0.2f;

    CanvasGroup group;
    TMP_Text titleLabel;
    TMP_Text bodyLabel;
    TMP_Text buttonLabel;
    Button okButton;

    Action onClose;
    Coroutine fadeRoutine;
    bool isShowing;

    public bool IsShowing => isShowing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("[TutorialPopup]");
        go.AddComponent<TutorialPopup>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUI();
        HideInstant();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Shows the popup. <paramref name="closeCallback"/> is invoked when the player clicks OK.
    /// </summary>
    public void Show(string title, string body, string okLabel, Action closeCallback)
    {
        EnsureEventSystem();

        onClose = closeCallback;
        isShowing = true;

        titleLabel.text = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
        titleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
        bodyLabel.text = body ?? string.Empty;
        buttonLabel.text = string.IsNullOrWhiteSpace(okLabel) ? "OK" : okLabel;

        group.blocksRaycasts = true;
        group.interactable = true;

        gameObject.SetActive(true);
        StartFade(1f);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(okButton.gameObject);
    }

    void HandleOkClicked()
    {
        if (!isShowing)
            return;

        isShowing = false;
        group.blocksRaycasts = false;
        group.interactable = false;

        var callback = onClose;
        onClose = null;

        StartFade(0f);

        callback?.Invoke();
    }

    void StartFade(float target)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / FadeDuration));
            yield return null;
        }
        group.alpha = target;
        fadeRoutine = null;
    }

    void HideInstant()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        isShowing = false;
    }

    // There must be an EventSystem in the scene for the OK button to be clickable.
    // Reuse Fungus' EventSystem prefab (handles new Input System automatically).
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var prefab = Resources.Load<GameObject>(FungusConstants.EventSystemPrefabName);
        if (prefab != null)
        {
            var go = Instantiate(prefab);
            go.name = "EventSystem";
        }
        else
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above SayDialog and Objective HUD

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Full-screen backdrop that blocks clicks behind the popup.
        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        backdropGo.transform.SetParent(canvasGo.transform, false);

        var backdropRect = backdropGo.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        var backdropImage = backdropGo.GetComponent<Image>();
        backdropImage.color = BackdropColor;
        backdropImage.raycastTarget = true;

        group = backdropGo.GetComponent<CanvasGroup>();

        // Centered panel.
        var panelGo = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGo.transform.SetParent(backdropGo.transform, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(720f, 0f);

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = PanelColor;

        var layout = panelGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 32, 32);
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        titleLabel = CreateLabel(panelGo.transform, "Title", 30f, FontStyles.Bold, AccentColor, TextAlignmentOptions.Center);
        titleLabel.characterSpacing = 4f;

        bodyLabel = CreateLabel(panelGo.transform, "Body", 30f, FontStyles.Normal, BodyColor, TextAlignmentOptions.Center);

        okButton = CreateButton(panelGo.transform, out buttonLabel);
        okButton.onClick.AddListener(HandleOkClicked);
    }

    static TMP_Text CreateLabel(Transform parent, string name, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    static Button CreateButton(Transform parent, out TMP_Text label)
    {
        var go = new GameObject("OkButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var element = go.GetComponent<LayoutElement>();
        element.minHeight = 64f;
        element.preferredHeight = 64f;
        element.minWidth = 200f;
        element.preferredWidth = 240f;

        var image = go.GetComponent<Image>();
        image.color = ButtonColor;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        label = CreateLabel(go.transform, "Label", 28f, FontStyles.Bold, ButtonTextColor, TextAlignmentOptions.Center);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }
}
