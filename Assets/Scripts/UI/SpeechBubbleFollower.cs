using Fungus;
using UnityEngine;

/// <summary>
/// Keeps a UI element (a speech bubble) positioned above a world target in
/// screen space. Designed to sit on a cloned Fungus SayDialog / MenuDialog so
/// the dialog floats over the speaking character's head instead of the bottom
/// of the screen.
///
/// The bubble is kept on a screen-space canvas (so it stays crisp and a
/// constant size) and is clamped to remain on screen.
/// </summary>
[DisallowMultipleComponent]
public class SpeechBubbleFollower : MonoBehaviour
{
    public enum FollowMode
    {
        /// <summary>Follow whoever is currently speaking (read from the SayDialog).</summary>
        SpeakingCharacter,
        /// <summary>Follow the player (used for the choice bubble).</summary>
        Player,
    }

    [Header("What to follow")]
    [SerializeField] private FollowMode mode = FollowMode.SpeakingCharacter;

    [Tooltip("The bubble rect to move. If empty, this object's RectTransform is used.")]
    [SerializeField] private RectTransform bubbleRect;

    [Header("Placement")]
    [Tooltip("World-space height above the target when no SpeechBubbleAnchor is found.")]
    [SerializeField] private float defaultHeadHeight = 2.2f;

    [Tooltip("Extra vertical gap (in canvas units) between the head and the bottom of the bubble.")]
    [SerializeField] private float verticalGap = 12f;

    [Tooltip("Keep the bubble fully inside the screen edges by this padding (canvas units).")]
    [SerializeField] private float screenPadding = 16f;

    [Tooltip("Camera used to project the world position. Falls back to Camera.main.")]
    [SerializeField] private Camera worldCamera;

    RectTransform canvasRect;
    Canvas canvas;
    SayDialog sayDialog;

    void Awake()
    {
        if (bubbleRect == null)
            bubbleRect = GetComponent<RectTransform>();

        sayDialog = GetComponent<SayDialog>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = canvas.rootCanvas.transform as RectTransform;

        // The math below assumes the bubble is anchored to the canvas
        // bottom-left, so its anchoredPosition is measured from there.
        if (bubbleRect != null)
        {
            bubbleRect.anchorMin = Vector2.zero;
            bubbleRect.anchorMax = Vector2.zero;
        }
    }

    void LateUpdate()
    {
        if (bubbleRect == null || canvas == null || canvasRect == null)
            return;

        if (!TryGetWorldPoint(out Vector3 worldPoint))
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);
        if (screenPoint.z < 0f)
            return; // Target is behind the camera; leave the bubble where it was.

        float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        Vector2 anchored = new Vector2(screenPoint.x, screenPoint.y) / scale;
        anchored.y += verticalGap;

        bubbleRect.anchoredPosition = ClampToCanvas(anchored);
    }

    bool TryGetWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;

        Transform target = null;
        Character speaker = null;

        if (mode == FollowMode.Player)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                target = player.transform;
        }
        else
        {
            speaker = sayDialog != null ? sayDialog.SpeakingCharacter : null;
            if (speaker != null)
                target = speaker.transform;
        }

        // Prefer an explicit anchor if one is registered for the speaker.
        if (speaker != null)
        {
            SpeechBubbleAnchor anchor = SpeechBubbleAnchor.Find(speaker);
            if (anchor != null)
            {
                worldPoint = anchor.WorldPoint;
                return true;
            }
        }

        if (target == null)
            return false;

        worldPoint = target.position + Vector3.up * defaultHeadHeight;
        return true;
    }

    Vector2 ClampToCanvas(Vector2 anchored)
    {
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 bubbleSize = bubbleRect.rect.size;
        Vector2 pivot = bubbleRect.pivot;

        float minX = screenPadding + bubbleSize.x * pivot.x;
        float maxX = canvasSize.x - screenPadding - bubbleSize.x * (1f - pivot.x);
        float minY = screenPadding + bubbleSize.y * pivot.y;
        float maxY = canvasSize.y - screenPadding - bubbleSize.y * (1f - pivot.y);

        if (minX <= maxX)
            anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
        if (minY <= maxY)
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);

        return anchored;
    }
}
