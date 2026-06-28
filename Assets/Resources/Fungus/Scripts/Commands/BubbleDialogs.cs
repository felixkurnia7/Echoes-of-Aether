// --- Echoes of Aether customization ---
// Lives inside the Fungus assembly so the Say command can reference it.

using UnityEngine;

namespace Fungus
{
    /// <summary>
    /// Dialog presentation styles selectable from a Fungus Say command dropdown.
    /// </summary>
    public enum SayBubbleStyle
    {
        /// <summary>The normal Fungus dialog box at the bottom of the screen.</summary>
        Default,
        /// <summary>A floating speech bubble above the speaker's head.</summary>
        Bubble,
    }

    /// <summary>
    /// Resolves a <see cref="SayBubbleStyle"/> to an actual <see cref="SayDialog"/>
    /// instance so that Say commands can pick a style from a dropdown instead of
    /// having a Say Dialog reference dragged onto every command.
    ///
    /// Dialogs are found in the scene if present, otherwise spawned once from
    /// Resources and kept alive across scene loads. No manual setup required
    /// beyond running the bubble builder once.
    /// </summary>
    public static class BubbleDialogs
    {
        const string BoxResourcePath = "Prefabs/SayDialog";
        const string BubbleResourcePath = "Prefabs/BubbleSayDialog";

        // Bubble dialog GameObjects are identified by name (so this file does not
        // need a reference to the SpeechBubbleFollower type in another assembly).
        const string BubbleNameMarker = "Bubble";

        static SayDialog cachedBox;
        static SayDialog cachedBubble;

        public static SayDialog Resolve(SayBubbleStyle style)
        {
            return style == SayBubbleStyle.Bubble ? GetBubble() : GetBox();
        }

        static SayDialog GetBox()
        {
            if (cachedBox != null)
                return cachedBox;

            cachedBox = FindExisting(wantBubble: false);
            if (cachedBox == null)
                cachedBox = Spawn(BoxResourcePath, "SayDialog");

            return cachedBox;
        }

        static SayDialog GetBubble()
        {
            if (cachedBubble != null)
                return cachedBubble;

            cachedBubble = FindExisting(wantBubble: true);
            if (cachedBubble == null)
                cachedBubble = Spawn(BubbleResourcePath, "BubbleSayDialog");

            return cachedBubble;
        }

        static SayDialog FindExisting(bool wantBubble)
        {
            SayDialog[] all = Object.FindObjectsByType<SayDialog>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                bool isBubble = all[i].name.IndexOf(BubbleNameMarker, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isBubble == wantBubble)
                    return all[i];
            }

            return null;
        }

        static SayDialog Spawn(string resourcePath, string objectName)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[BubbleDialogs] Could not load '{resourcePath}'. " +
                               "Run Tools > Echoes of Aether > Build Placeholder Bubble Dialogs first.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = objectName;
            Object.DontDestroyOnLoad(instance);
            return instance.GetComponent<SayDialog>();
        }
    }
}
