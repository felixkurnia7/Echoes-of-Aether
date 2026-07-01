using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// Place this on a world object (an NPC or the player) and assign the Fungus
/// <see cref="Character"/> that represents who is speaking. A speech-bubble
/// dialog can then look up where that character's head is in the world, even
/// when the Character component itself lives on a separate definition object.
///
/// If <see cref="headPoint"/> is left empty, the anchor uses this object's
/// position raised by <see cref="headHeight"/>.
/// </summary>
[DisallowMultipleComponent]
public class SpeechBubbleAnchor : MonoBehaviour
{
    [Tooltip("The Fungus Character this world object speaks as. Used to match the active speaker to this anchor.")]
    [SerializeField] private Character character;

    [Tooltip("Optional explicit point to anchor the bubble to (e.g. an empty above the head). If empty, this object's position + Head Height is used.")]
    [SerializeField] private Transform headPoint;

    [Tooltip("Height above this object used when Head Point is not assigned.")]
    [SerializeField] private float headHeight = 2.2f;

    static readonly List<SpeechBubbleAnchor> anchors = new List<SpeechBubbleAnchor>();

    public Character Character => character;

    public void Configure(Character target, Transform head = null, float height = 2.2f)
    {
        character = target;
        if (head != null)
            headPoint = head;
        headHeight = height;
    }

    public Vector3 WorldPoint
    {
        get
        {
            if (headPoint != null)
                return headPoint.position;

            return transform.position + Vector3.up * headHeight;
        }
    }

    void OnEnable()
    {
        if (!anchors.Contains(this))
            anchors.Add(this);
    }

    void OnDisable()
    {
        anchors.Remove(this);
    }

    /// <summary>
    /// Returns the anchor registered for the given character, or null if none.
    /// </summary>
    public static SpeechBubbleAnchor Find(Character target)
    {
        if (target == null)
            return null;

        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i] != null && anchors[i].character == target)
                return anchors[i];
        }

        return null;
    }
}
