using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and animates the on-screen objective panel (top-left).
/// Driven entirely by <see cref="ObjectiveManager"/> events, so it never
/// needs to be wired up in a scene.
/// </summary>
public class ObjectiveHUD : MonoBehaviour
{
    static readonly Color AccentColor = new Color(1f, 0.83f, 0.36f);
    static readonly Color BodyColor = new Color(0.93f, 0.93f, 0.93f);
    static readonly Color DoneColor = new Color(0.55f, 0.92f, 0.55f);
    static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.1f, 0.82f);

    const float SlideOffset = 60f;
    const float FadeDuration = 0.25f;
    const float CompletedHoldTime = 1.6f;

    CanvasGroup group;
    RectTransform panel;
    Image accentBar;
    TMP_Text titleLabel;
    TMP_Text bodyLabel;
    RectTransform subContainer;
    Vector2 shownPosition;
    Coroutine routine;

    void Awake()
    {
        BuildUI();
        HideInstant();
    }

    void OnEnable()
    {
        var mgr = ObjectiveManager.Instance;
        if (mgr == null)
            return;

        mgr.OnObjectiveSet += HandleSet;
        mgr.OnObjectiveCompleted += HandleCompleted;
        mgr.OnObjectiveHidden += HandleHidden;
        mgr.OnSubObjectivesChanged += HandleSubsChanged;

        if (mgr.Status == ObjectiveStatus.Active)
        {
            HandleSet(mgr.CurrentObjective);
            RebuildSubObjectives();
        }
    }

    void OnDisable()
    {
        var mgr = ObjectiveManager.Instance;
        if (mgr == null)
            return;

        mgr.OnObjectiveSet -= HandleSet;
        mgr.OnObjectiveCompleted -= HandleCompleted;
        mgr.OnObjectiveHidden -= HandleHidden;
        mgr.OnSubObjectivesChanged -= HandleSubsChanged;
    }

    void HandleSet(string text)
    {
        titleLabel.text = "OBJECTIVE";
        titleLabel.color = AccentColor;
        accentBar.color = AccentColor;
        bodyLabel.text = text;
        bodyLabel.color = BodyColor;
        StartRoutine(ShowRoutine());
    }

    void HandleCompleted(string text)
    {
        titleLabel.text = "OBJECTIVE COMPLETE";
        titleLabel.color = DoneColor;
        accentBar.color = DoneColor;
        bodyLabel.text = "<s>" + text + "</s>";
        bodyLabel.color = DoneColor;
        StartRoutine(CompleteRoutine());
    }

    void HandleHidden()
    {
        StartRoutine(HideRoutine());
    }

    void HandleSubsChanged()
    {
        RebuildSubObjectives();

        bool hasContent = ObjectiveManager.Instance != null
            && (ObjectiveManager.Instance.Status == ObjectiveStatus.Active
                || ObjectiveManager.Instance.SubObjectives.Count > 0);

        if (hasContent && group.alpha < 0.99f)
            StartRoutine(ShowRoutine());
    }

    void RebuildSubObjectives()
    {
        for (int i = subContainer.childCount - 1; i >= 0; i--)
            Destroy(subContainer.GetChild(i).gameObject);

        var mgr = ObjectiveManager.Instance;
        if (mgr == null)
            return;

        foreach (SubObjective sub in mgr.SubObjectives)
        {
            TMP_Text label = CreateLabel(subContainer, "SubObjective", 21f, FontStyles.Normal, BodyColor);
            if (sub.Completed)
            {
                label.text = "<s>" + sub.Text + "</s>";
                label.color = DoneColor;
            }
            else
            {
                label.text = "- " + sub.Text;
                label.color = BodyColor;
            }
        }
    }

    void StartRoutine(IEnumerator next)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(next);
    }

    IEnumerator ShowRoutine()
    {
        yield return Animate(0f, 1f, shownPosition + Vector2.left * SlideOffset, shownPosition);
        routine = null;
    }

    IEnumerator CompleteRoutine()
    {
        group.alpha = 1f;
        panel.anchoredPosition = shownPosition;
        yield return new WaitForSeconds(CompletedHoldTime);
        yield return Animate(1f, 0f, shownPosition, shownPosition + Vector2.left * SlideOffset);

        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.HideObjective();

        routine = null;
    }

    IEnumerator HideRoutine()
    {
        yield return Animate(group.alpha, 0f, panel.anchoredPosition, shownPosition + Vector2.left * SlideOffset);
        routine = null;
    }

    IEnumerator Animate(float fromAlpha, float toAlpha, Vector2 fromPos, Vector2 toPos)
    {
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FadeDuration));
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
            panel.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
            yield return null;
        }
        group.alpha = toAlpha;
        panel.anchoredPosition = toPos;
    }

    void HideInstant()
    {
        group.alpha = 0f;
        panel.anchoredPosition = shownPosition + Vector2.left * SlideOffset;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("ObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var panelGo = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);

        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        shownPosition = new Vector2(40f, -40f);
        panel.anchoredPosition = shownPosition;
        panel.sizeDelta = new Vector2(460f, 100f);

        group = panelGo.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        var layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panelGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        accentBar = CreateAccentBar(panelGo.transform);
        titleLabel = CreateLabel(panelGo.transform, "Title", 20f, FontStyles.Bold, AccentColor);
        titleLabel.characterSpacing = 4f;
        bodyLabel = CreateLabel(panelGo.transform, "Body", 26f, FontStyles.Normal, BodyColor);
        subContainer = CreateSubContainer(panelGo.transform);
    }

    static RectTransform CreateSubContainer(Transform parent)
    {
        var go = new GameObject("SubObjectives", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 0, 2, 0);
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go.GetComponent<RectTransform>();
    }

    static Image CreateAccentBar(Transform parent)
    {
        var go = new GameObject("AccentBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var element = go.GetComponent<LayoutElement>();
        element.minHeight = 3f;
        element.preferredHeight = 3f;

        var image = go.GetComponent<Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
        return image;
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
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
}
