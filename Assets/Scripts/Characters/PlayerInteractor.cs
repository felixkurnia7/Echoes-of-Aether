using UnityEngine;

/// <summary>
/// Handles "press Interact → trigger the nearest IInteractable" independently of
/// <see cref="PlayerController"/>. Because it lives on its own always-on component,
/// interaction keeps working even when movement control is disabled (e.g. while a
/// PlayerCutsceneMover is driving the player). Interaction is still gated to the
/// Exploring game state so it can't fire during battle, dialogue, or while paused.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(InteractionDetector))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private InteractionDetector interactionDetector;
    [Tooltip("Passed to IInteractable.Interact / CanInteract. Auto-found if left empty.")]
    [SerializeField] private PlayerController player;

    void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponent<PlayerInputHandler>();

        if (interactionDetector == null)
            interactionDetector = GetComponent<InteractionDetector>();

        if (player == null)
            player = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        GameEvents.OnDialogueFinished += HandleDialogueFinished;
    }

    void OnDisable()
    {
        GameEvents.OnDialogueFinished -= HandleDialogueFinished;
    }

    void Update()
    {
        if (inputHandler == null)
            return;

        if (!CanInteractNow())
        {
            inputHandler.ConsumeInteract();
            return;
        }

        if (!inputHandler.InteractPressed)
            return;

        inputHandler.ConsumeInteract();

        IInteractable target = interactionDetector != null ? interactionDetector.currentTarget : null;
        if (target == null || !target.CanInteract(player))
            return;

        if (player != null)
            player.SetPlayerState(PlayerController.PlayerState.Interact);

        target.Interact(player);
    }

    bool CanInteractNow()
    {
        if (GameManager.Instance == null)
            return true;

        return GameManager.Instance.CurrentState == GameState.Exploring
            && !GameManager.Instance.IsPaused;
    }

    void HandleDialogueFinished()
    {
        if (inputHandler != null)
            inputHandler.ConsumeInteract();
    }
}
