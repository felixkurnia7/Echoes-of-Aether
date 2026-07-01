using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// A trigger volume that fires when the player walks into it. It invokes a UnityEvent
/// (wire fades, sounds, flags, etc.) and, if a target scene is set, loads that scene.
/// Drop it on an empty GameObject with a Collider (auto-set to "Is Trigger") and size
/// the collider to cover the doorway/edge that should move the player to another scene.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Only objects with this tag fire the trigger. Leave empty to accept anything.")]
    [SerializeField] private string requiredTag = "Player";

    [Tooltip("Fire only the first time something enters.")]
    [SerializeField] private bool triggerOnce = true;

    [Tooltip("If Trigger Once is enabled, disable this Collider after firing (prevents re-trigger from overlaps).")]
    [SerializeField] private bool disableColliderAfterUse = true;

    [Tooltip("Optional: only fire if this GameManager story flag is set. Leave empty to always allow.")]
    [SerializeField] private string requiredStoryFlag = "";

    [Header("Scene Transition")]
    [Tooltip("Scene to load when triggered. Leave empty to only invoke the event.")]
    [SerializeField] private string targetScene = "";

    [Tooltip("Seconds to wait before loading the scene (lets the event play a fade/sound first).")]
    [SerializeField] private float loadDelay = 0f;

    [Header("Events")]
    [Tooltip("Invoked when the trigger fires, before the scene loads. Wire fades, sounds, flags, etc.")]
    [SerializeField] private UnityEvent onTriggered;

    bool used;

    void Reset()
    {
        if (TryGetComponent(out Collider col))
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (used && triggerOnce)
            return;

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        if (!string.IsNullOrEmpty(requiredStoryFlag)
            && (GameManager.Instance == null || !GameManager.Instance.GetStoryFlag(requiredStoryFlag)))
            return;

        used = true;
        onTriggered?.Invoke();

        if (triggerOnce && disableColliderAfterUse && TryGetComponent(out Collider col))
            col.enabled = false;

        if (string.IsNullOrEmpty(targetScene))
            return;

        if (loadDelay > 0f)
            Invoke(nameof(LoadTargetScene), loadDelay);
        else
            LoadTargetScene();
    }

    void LoadTargetScene()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadScene(targetScene);
        else
            SceneManager.LoadScene(targetScene);
    }

    void OnDrawGizmos()
    {
        if (!TryGetComponent(out Collider col))
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Color fill = new(0.3f, 0.8f, 1f, 0.18f);
        Color line = new(0.3f, 0.8f, 1f, 0.8f);

        if (col is BoxCollider box)
        {
            Gizmos.color = fill;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = line;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.color = line;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
