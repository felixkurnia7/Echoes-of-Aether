using UnityEngine;

/// <summary>
/// When the scene loads, checks a GameManager story flag and—if it matches—puts this
/// character into its death pose: it disables the listed movement behaviours (so the
/// character stays put) and drives the Animator's death parameter. Use it to show an
/// enemy already dead after the player returns from winning its battle (e.g. the bear).
/// </summary>
public class DeathOnStoryFlag : MonoBehaviour
{
    [Tooltip("GameManager story flag to check (e.g. 'bear_defeated'). Persists across scene loads.")]
    [SerializeField] private string storyFlag = "";

    [Tooltip("Play the death animation when the flag equals this value.")]
    [SerializeField] private bool whenValueIs = true;

    [Tooltip("Animator to drive. Auto-found in children if left empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("Bool parameter set TRUE to play death (e.g. 'Death'). Leave empty if not used.")]
    [SerializeField] private string deathBoolParam = "Death";

    [Tooltip("Trigger parameter fired to play death. Leave empty if not used.")]
    [SerializeField] private string deathTriggerParam = "";

    [Tooltip("Behaviours disabled so the character can't move/idle while dead (e.g. NPCWalker).")]
    [SerializeField] private Behaviour[] disableWhenDead;

    bool isDead;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Decide in Awake (before any Start runs) so movers never begin walking this frame.
        isDead = !string.IsNullOrEmpty(storyFlag)
            && GameManager.Instance != null
            && GameManager.Instance.GetStoryFlag(storyFlag) == whenValueIs;

        if (!isDead || disableWhenDead == null)
            return;

        foreach (Behaviour b in disableWhenDead)
            if (b != null)
                b.enabled = false;
    }

    void Start()
    {
        if (!isDead || animator == null)
            return;

        if (!string.IsNullOrEmpty(deathBoolParam))
            animator.SetBool(deathBoolParam, true);

        if (!string.IsNullOrEmpty(deathTriggerParam))
            animator.SetTrigger(deathTriggerParam);
    }
}
