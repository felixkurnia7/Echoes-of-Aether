using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Full-screen ending credits panel. Auto-creates before the first scene loads and
/// persists across scene loads. Call from Fungus via <see cref="ShowEndingScreenCommand"/>.
/// </summary>
public class EndingScreen : MonoBehaviour
{
    public static EndingScreen Instance { get; private set; }

    static readonly Color BackdropColor = Color.black;
    static readonly Color TitleColor = new Color(1f, 0.83f, 0.36f);
    static readonly Color SubtitleColor = new Color(0.93f, 0.93f, 0.93f);
    static readonly Color EpilogueColor = new Color(0.82f, 0.82f, 0.82f);
    static readonly Color FooterColor = new Color(0.75f, 0.75f, 0.75f);

    const string DefaultEpilogue =
        "The Aether Crystal has been restored.\nPeace returns to the outpost...\n\n...for now.";

    Canvas canvas;
    CanvasGroup backdropGroup;
    CanvasGroup titleGroup;
    CanvasGroup subtitleGroup;
    CanvasGroup epilogueGroup;
    CanvasGroup footerGroup;
    TMP_Text titleLabel;
    TMP_Text subtitleLabel;
    TMP_Text epilogueLabel;
    TMP_Text footerLabel;
    TMP_Text hintLabel;

    Coroutine showRoutine;
    Action onDismiss;
    bool isShowing;
    bool quitOnDismiss;

    public bool IsShowing => isShowing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("[EndingScreen]");
        go.AddComponent<EndingScreen>();
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

    public void Show(string title, string subtitle, string footer, bool waitForInput, Action dismissCallback)
    {
        Show(title, subtitle, DefaultEpilogue, footer, waitForInput, quitOnDismiss: true, dismissCallback);
    }

    public void Show(string title, string subtitle, string footer, bool waitForInput, bool quitOnDismiss, Action dismissCallback)
    {
        Show(title, subtitle, DefaultEpilogue, footer, waitForInput, quitOnDismiss, dismissCallback);
    }

    public void Show(string title, string subtitle, string epilogue, string footer, bool waitForInput, bool quitOnDismiss, Action dismissCallback)
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        this.quitOnDismiss = quitOnDismiss;
        onDismiss = dismissCallback;
        isShowing = true;
        gameObject.SetActive(true);

        titleLabel.text = title ?? string.Empty;
        subtitleLabel.text = subtitle ?? string.Empty;
        epilogueLabel.text = epilogue ?? string.Empty;
        footerLabel.text = footer ?? string.Empty;
        hintLabel.gameObject.SetActive(waitForInput);

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.Cutscene);

        PrepareForDisplay();

        showRoutine = StartCoroutine(ShowRoutine(waitForInput));
    }

    /// <summary>
    /// Renders above Fungus' "Fade Canvas" (sorting order 999) and clears that overlay
    /// so ending text is not hidden behind an opaque black fade.
    /// </summary>
    void PrepareForDisplay()
    {
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1500;
        }

        SuppressFungusFadeOverlay();
    }

    static void SuppressFungusFadeOverlay()
    {
        GameObject fadeCanvas = GameObject.Find("Fade Canvas");
        if (fadeCanvas == null)
            return;

        if (fadeCanvas.TryGetComponent(out CanvasGroup group))
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

    public void ShowDefault(bool waitForInput, bool quitOnDismiss, Action dismissCallback)
    {
        Show("Echoes of Aether", "The Lost Crystal", DefaultEpilogue, "Thank You For Playing", waitForInput, quitOnDismiss, dismissCallback);
    }

    public void ShowDefault(bool waitForInput, Action dismissCallback)
    {
        ShowDefault(waitForInput, true, dismissCallback);
    }

    IEnumerator ShowRoutine(bool waitForInput)
    {
        backdropGroup.alpha = 1f;
        titleGroup.alpha = 0f;
        subtitleGroup.alpha = 0f;
        epilogueGroup.alpha = 0f;
        footerGroup.alpha = 0f;
        hintLabel.alpha = 0f;

        yield return FadeGroup(titleGroup, 1f, 1.2f);
        yield return new WaitForSecondsRealtime(0.35f);
        yield return FadeGroup(subtitleGroup, 1f, 1f);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return FadeGroup(epilogueGroup, 1f, 1.2f);
        yield return new WaitForSecondsRealtime(0.8f);
        yield return FadeGroup(footerGroup, 1f, 1f);

        if (waitForInput)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return FadeGroup(hintLabel, 0.65f, 0.8f);

            while (!WasDismissPressed())
                yield return null;
        }

        if (quitOnDismiss)
            ExitGame();
        else
            Dismiss();
    }

    static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static bool WasDismissPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            return true;

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null
            && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        return false;
    }

    void Dismiss()
    {
        if (!isShowing)
            return;

        isShowing = false;
        onDismiss?.Invoke();
        onDismiss = null;
        showRoutine = null;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.Exploring);

        HideInstant();
    }

    IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }

        group.alpha = target;
    }

    IEnumerator FadeGroup(TMP_Text label, float target, float duration)
    {
        float start = label.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            label.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }

        label.alpha = target;
        yield break;
    }

    void HideInstant()
    {
        backdropGroup.alpha = 0f;
        titleGroup.alpha = 0f;
        subtitleGroup.alpha = 0f;
        epilogueGroup.alpha = 0f;
        footerGroup.alpha = 0f;
        hintLabel.alpha = 0f;
        gameObject.SetActive(false);
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("EndingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        this.canvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        backdropGo.transform.SetParent(canvasGo.transform, false);

        var backdropRect = backdropGo.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        backdropGo.GetComponent<Image>().color = BackdropColor;
        backdropGroup = backdropGo.GetComponent<CanvasGroup>();

        var stackGo = new GameObject("TextStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        stackGo.transform.SetParent(backdropGo.transform, false);

        var stackRect = stackGo.GetComponent<RectTransform>();
        stackRect.anchorMin = new Vector2(0.5f, 0.5f);
        stackRect.anchorMax = new Vector2(0.5f, 0.5f);
        stackRect.pivot = new Vector2(0.5f, 0.5f);
        stackRect.anchoredPosition = Vector2.zero;
        stackRect.sizeDelta = new Vector2(1200f, 720f);

        var layout = stackGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleLabel = CreateLine(stackGo.transform, "Title", 80f, FontStyles.Bold, TitleColor, out titleGroup);
        subtitleLabel = CreateLine(stackGo.transform, "Subtitle", 42f, FontStyles.Italic, SubtitleColor, out subtitleGroup);

        CreateSpacer(stackGo.transform, 28f);

        epilogueLabel = CreateBodyBlock(stackGo.transform, "Epilogue", 30f, FontStyles.Normal, EpilogueColor, out epilogueGroup);

        CreateSpacer(stackGo.transform, 48f);

        footerLabel = CreateLine(stackGo.transform, "Footer", 36f, FontStyles.Normal, FooterColor, out footerGroup);

        CreateSpacer(stackGo.transform, 40f);

        hintLabel = CreateLabel(stackGo.transform, "Hint", 22f, FontStyles.Normal, FooterColor);
        hintLabel.text = "Press any key to exit";
        var hintLayout = hintLabel.gameObject.AddComponent<LayoutElement>();
        hintLayout.minHeight = 32f;
        hintLayout.preferredHeight = 32f;
        hintLabel.alpha = 0f;
    }

    static void CreateSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var element = go.GetComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    static TMP_Text CreateLine(Transform parent, string name, float size, FontStyles style, Color color, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        group = go.GetComponent<CanvasGroup>();

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = size * 1.25f;
        element.preferredHeight = size * 1.25f;

        var label = CreateLabel(go.transform, "Text", size, style, color);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    static TMP_Text CreateBodyBlock(Transform parent, string name, float size, FontStyles style, Color color, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        group = go.GetComponent<CanvasGroup>();

        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 140f;
        element.preferredHeight = 140f;

        var label = CreateLabel(go.transform, "Text", size, style, color);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.lineSpacing = 8f;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    static TMP_Text CreateLabel(Transform parent, string name, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }
}
