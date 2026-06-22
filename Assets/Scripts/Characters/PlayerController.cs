using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
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
    Vector3 velocity;
    Vector3 lastMoveDirection;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        inputHandler = GetComponent<PlayerInputHandler>();

        if (characterVisual == null)
            characterVisual = GetComponent<CharacterVisual>();
    }

    void Update()
    {
        SyncStateFromGameManager();
        if (!CanProcessGameplayInput()) return;

        HandleMovement();
        // INTERACT - FASE 4
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
                    CurrentPlayerState == PlayerState.Battle)
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
