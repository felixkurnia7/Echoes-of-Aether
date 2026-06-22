using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(InteractionDetector))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Move,
        Interact,
        Cutscene,
        Battle
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private LayerMask groundMask;

    [Header("References")]
    [SerializeField] private CharacterVisual characterVisual;

    public PlayerState CurrentPlayerState { get; private set; } = PlayerState.Idle;

    CharacterController characterController;
    PlayerInputHandler inputHandler;
    InteractionDetector interactionDetector;
    Vector3 velocity;
    Vector3 lastMoveDirection;

    void OnEnable()
    {
        GameEvents.OnDialogueFinished += HandleDialogueFinished;
    }

    void OnDisable()
    {
        GameEvents.OnDialogueFinished -= HandleDialogueFinished;
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        inputHandler = GetComponent<PlayerInputHandler>();
        interactionDetector = GetComponent<InteractionDetector>();

        if (characterVisual == null)
            characterVisual = GetComponent<CharacterVisual>();
    }

    void Update()
    {
        SyncStateFromGameManager();
        if (!CanProcessGameplayInput())
        {
            inputHandler.ConsumeInteract();
            return;
        }

        HandleMovement();
        HandleInteraction();
    }
    void HandleMovement()
    {
        Vector2 input = inputHandler.MoveInput;
        Vector3 direction = new Vector3(input.x, 0f, input.y);

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        bool IsMoving = direction.sqrMagnitude > 0.01f;

        if (IsMoving)
        {
            lastMoveDirection = direction;
            CurrentPlayerState = PlayerState.Move;
        }
        else
        {
            CurrentPlayerState = PlayerState.Idle;
        }

        characterController.Move(direction * moveSpeed * Time.deltaTime);
        ApplyGravity();

        if (characterVisual != null)
            characterVisual.SetMovement(direction, IsMoving ? moveSpeed : 0f);
    }

    void ApplyGravity()
    {
        if (characterController.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    void SyncStateFromGameManager()
    {
        if (GameManager.Instance == null) return;

        switch (GameManager.Instance.CurrentState)
        {
            case GameState.Exploring:
            case GameState.Paused:
                if (CurrentPlayerState == PlayerState.Cutscene ||
                    CurrentPlayerState == PlayerState.Battle ||
                    CurrentPlayerState == PlayerState.Interact)
                    CurrentPlayerState = PlayerState.Idle;
                break;
            case GameState.Dialogue:
            case GameState.Cutscene:
                CurrentPlayerState = PlayerState.Cutscene;
                break;
            case GameState.Battle:
                CurrentPlayerState = PlayerState.Battle;
                break;
        }
    }

    void HandleDialogueFinished()
    {
        inputHandler.ConsumeInteract();
    }

    void HandleInteraction()
    {
        if (!inputHandler.InteractPressed)
            return;

        inputHandler.ConsumeInteract();

        IInteractable target = interactionDetector.currentTarget;
        if (target == null || !target.CanInteract(this))
            return;

        CurrentPlayerState = PlayerState.Interact;
        target.Interact(this);
    }

    bool CanProcessGameplayInput()
    {
        if (GameManager.Instance == null) return true;
        
        return GameManager.Instance.CurrentState == GameState.Exploring
            && !GameManager.Instance.IsPaused;
    }

    public void SetPlayerState(PlayerState state)
    {
        CurrentPlayerState = state;
    }
}
