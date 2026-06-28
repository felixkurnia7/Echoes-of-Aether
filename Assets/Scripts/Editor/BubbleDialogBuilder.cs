using System.IO;
using Fungus;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click builder for placeholder "speech bubble" dialogs. It generates
/// simple placeholder sprites (a rounded bubble body + a downward tail) and
/// clones Fungus' existing SayDialog and MenuDialog prefabs into floating
/// bubble versions that follow the speaker via <see cref="SpeechBubbleFollower"/>.
///
/// Run it from the menu: Tools > Echoes of Aether > Build Placeholder Bubble Dialogs.
/// Re-running it overwrites the generated assets, so it is safe to iterate.
/// </summary>
public static class BubbleDialogBuilder
{
    const string ArtFolder = "Assets/Art/UI/Bubble";
    const string PrefabFolder = "Assets/Resources/Prefabs";

    const string SayDialogSrc = "Assets/Resources/Fungus/Resources/Prefabs/SayDialog.prefab";
    const string MenuDialogSrc = "Assets/Resources/Fungus/Resources/Prefabs/MenuDialog.prefab";

    const string BodySpritePath = ArtFolder + "/BubbleBody.png";
    const string TailSpritePath = ArtFolder + "/BubbleTail.png";

    static readonly Color BubbleColor = new Color(1f, 1f, 1f, 0.97f);
    static readonly Color StoryColor = new Color(0.13f, 0.12f, 0.14f, 1f);

    [MenuItem("Tools/Echoes of Aether/Build Placeholder Bubble Dialogs")]
    public static void Build()
    {
        try
        {
            EnsureDirectory(ArtFolder);
            EnsureDirectory(PrefabFolder);

            Sprite body = CreateRoundedSprite(BodySpritePath, 256, 256, 56, border: 56);
            Sprite tail = CreateTailSprite(TailSpritePath, 64, 52);

            if (body == null || tail == null)
            {
                Debug.LogError("[BubbleDialogBuilder] Failed to create placeholder sprites. Aborting.");
                return;
            }

            bool say = BuildSayBubble(body, tail);
            bool menu = BuildMenuBubble(body);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (say && menu)
            {
                Debug.Log("[BubbleDialogBuilder] Done. Created:\n" +
                          " - " + PrefabFolder + "/BubbleSayDialog.prefab\n" +
                          " - " + PrefabFolder + "/BubbleMenuDialog.prefab\n" +
                          "Drag them into your scene and assign characters' 'Set Say Dialog' to the bubble.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BubbleDialogBuilder] Unexpected error: " + e);
        }
    }

    // ---------------------------------------------------------------- Say bubble

    static bool BuildSayBubble(Sprite body, Sprite tail)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(SayDialogSrc);
        if (src == null)
        {
            Debug.LogError("[BubbleDialogBuilder] Could not find source SayDialog prefab at " + SayDialogSrc);
            return false;
        }

        GameObject clone = (GameObject)Object.Instantiate(src);
        try
        {
            UnpackCompletely(clone);
            clone.name = "BubbleSayDialog";

            RectTransform panel = FindChildRect(clone.transform, "Panel");
            if (panel == null)
            {
                Debug.LogError("[BubbleDialogBuilder] 'Panel' not found in SayDialog clone.");
                return false;
            }

            // Bubble background.
            Image bg = panel.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = body;
                bg.type = Image.Type.Sliced;
                bg.color = BubbleColor;
                bg.pixelsPerUnitMultiplier = 1f;
            }

            // Float the panel; the follower drives its anchoredPosition.
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(720f, 230f);

            // Story text.
            RectTransform storyRT = FindChildRect(panel, "StoryText");
            Text story = storyRT != null ? storyRT.GetComponent<Text>() : null;
            if (story != null)
            {
                story.fontSize = 30;
                story.alignment = TextAnchor.MiddleCenter;
                story.horizontalOverflow = HorizontalWrapMode.Wrap;
                story.verticalOverflow = VerticalWrapMode.Overflow;
                story.color = StoryColor;
                story.resizeTextForBestFit = false;

                storyRT.anchorMin = Vector2.zero;
                storyRT.anchorMax = Vector2.one;
                storyRT.offsetMin = new Vector2(42f, 50f);
                storyRT.offsetMax = new Vector2(-42f, -58f);
            }

            // Name text.
            RectTransform nameRT = FindChildRect(panel, "NameText");
            Text nameText = nameRT != null ? nameRT.GetComponent<Text>() : null;
            if (nameText != null)
            {
                nameText.fontSize = 26;
                nameText.alignment = TextAnchor.UpperLeft;
                nameRT.anchorMin = new Vector2(0f, 1f);
                nameRT.anchorMax = new Vector2(1f, 1f);
                nameRT.pivot = new Vector2(0f, 1f);
                nameRT.sizeDelta = new Vector2(-60f, 44f);
                nameRT.anchoredPosition = new Vector2(34f, -10f);
            }

            // Hide the portrait image inside the small bubble.
            RectTransform portrait = FindChildRect(panel, "Image");
            if (portrait != null)
                portrait.gameObject.SetActive(false);

            AddTail(panel, tail);

            ConfigureFollower(clone, SpeechBubbleFollower.FollowMode.SpeakingCharacter, panel);

            string path = PrefabFolder + "/BubbleSayDialog.prefab";
            PrefabUtility.SaveAsPrefabAsset(clone, path);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    // --------------------------------------------------------------- Menu bubble

    static bool BuildMenuBubble(Sprite body)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(MenuDialogSrc);
        if (src == null)
        {
            Debug.LogError("[BubbleDialogBuilder] Could not find source MenuDialog prefab at " + MenuDialogSrc);
            return false;
        }

        GameObject clone = (GameObject)Object.Instantiate(src);
        try
        {
            UnpackCompletely(clone);
            clone.name = "BubbleMenuDialog";

            RectTransform group = FindChildRect(clone.transform, "ButtonGroup");
            if (group == null)
            {
                Debug.LogError("[BubbleDialogBuilder] 'ButtonGroup' not found in MenuDialog clone.");
                return false;
            }

            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.zero;
            group.pivot = new Vector2(0.5f, 0f);
            group.sizeDelta = new Vector2(520f, group.sizeDelta.y);

            // Restyle option buttons with the bubble sprite.
            foreach (Transform child in group)
            {
                if (!child.name.StartsWith("OptionButton"))
                    continue;

                Image img = child.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = body;
                    img.type = Image.Type.Sliced;
                    img.color = BubbleColor;
                    img.pixelsPerUnitMultiplier = 1f;
                }
            }

            ConfigureFollower(clone, SpeechBubbleFollower.FollowMode.Player, group);

            string path = PrefabFolder + "/BubbleMenuDialog.prefab";
            PrefabUtility.SaveAsPrefabAsset(clone, path);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    // ------------------------------------------------------------------- Helpers

    static void AddTail(RectTransform panel, Sprite tail)
    {
        var tailGO = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        var tailRT = tailGO.GetComponent<RectTransform>();
        tailRT.SetParent(panel, false);
        tailRT.anchorMin = new Vector2(0.5f, 0f);
        tailRT.anchorMax = new Vector2(0.5f, 0f);
        tailRT.pivot = new Vector2(0.5f, 1f);
        tailRT.sizeDelta = new Vector2(46f, 36f);
        tailRT.anchoredPosition = new Vector2(0f, 2f);

        var tailImg = tailGO.GetComponent<Image>();
        tailImg.sprite = tail;
        tailImg.color = BubbleColor;
        tailImg.preserveAspect = true;
        tailImg.raycastTarget = false;
    }

    static void ConfigureFollower(GameObject root, SpeechBubbleFollower.FollowMode mode, RectTransform bubbleRect)
    {
        SpeechBubbleFollower follower = root.GetComponent<SpeechBubbleFollower>();
        if (follower == null)
            follower = root.AddComponent<SpeechBubbleFollower>();

        var so = new SerializedObject(follower);
        var modeProp = so.FindProperty("mode");
        if (modeProp != null)
            modeProp.enumValueIndex = (int)mode;
        var rectProp = so.FindProperty("bubbleRect");
        if (rectProp != null)
            rectProp.objectReferenceValue = bubbleRect;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform FindChildRect(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        return t as RectTransform;
    }

    static void UnpackCompletely(GameObject instance)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
        {
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }

    static void EnsureDirectory(string assetFolder)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetFolder);
        if (!Directory.Exists(full))
            Directory.CreateDirectory(full);
    }

    // ---------------------------------------------------------- Sprite generation

    static Sprite CreateRoundedSprite(string assetPath, int width, int height, int radius, int border)
    {
        var pixels = new Color32[width * height];
        float cx0 = radius;
        float cy0 = radius;
        float cx1 = width - 1 - radius;
        float cy1 = height - 1 - radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // Distance outside the rounded-rect inner box.
                float dx = Mathf.Max(Mathf.Max(cx0 - px, px - cx1), 0f);
                float dy = Mathf.Max(Mathf.Max(cy0 - py, py - cy1), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        return WritePngAsSprite(assetPath, width, height, pixels, border);
    }

    static Sprite CreateTailSprite(string assetPath, int width, int height)
    {
        var pixels = new Color32[width * height];
        float halfW = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            // Apex at the bottom (y = 0), base at the top (y = height - 1).
            float t = (float)y / (height - 1);
            float allowed = halfW * t;

            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs((x + 0.5f) - halfW);
                float a = Mathf.Clamp01(allowed - d + 0.5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        return WritePngAsSprite(assetPath, width, height, pixels, border: 0);
    }

    static Sprite WritePngAsSprite(string assetPath, int width, int height, Color32[] pixels, int border)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[BubbleDialogBuilder] Failed to import sprite at " + assetPath);
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100;
        if (border > 0)
            importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
