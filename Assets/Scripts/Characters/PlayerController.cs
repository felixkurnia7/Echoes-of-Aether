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

    public enum MovementMode
    {
        World,
        CameraRelative,
        ReferenceTransform
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private MovementMode movementMode = MovementMode.ReferenceTransform;
    [Tooltip("Stable basis for side-view input. Assign CameraRig_SideView when using a camera path.")]
    [SerializeField] private Transform movementReference;

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

        if (movementReference == null && movementMode == MovementMode.ReferenceTransform)
            movementReference = FindFirstObjectByType<EchoesOfAether.Camera.CameraPathFollowerRig>()?.transform;
    }

    void Update()
    {
        SyncStateFromGameManager();
        if (!CanProcessGameplayInput())
            return;

        HandleMovement();
    }
    void HandleMovement()
    {
        Vector2 input = inputHandler.MoveInput;
        Vector3 direction = GetMoveDirection(input);

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

    Vector3 GetMoveDirection(Vector2 input)
    {
        switch (movementMode)
        {
            case MovementMode.CameraRelative:
                return GetCameraRelativeDirection(input);

            case MovementMode.ReferenceTransform:
                if (movementReference != null)
                    return GetReferenceRelativeDirection(input, movementReference);
                return GetCameraRelativeDirection(input);

            default:
                return new Vector3(input.x, 0f, input.y);
        }
    }

    static Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        var cam = UnityEngine.Camera.main;
        if (cam == null)
            return new Vector3(input.x, 0f, input.y);

        var forward = cam.transform.forward;
        var right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
            return new Vector3(input.x, 0f, input.y);

        forward.Normalize();
        right.Normalize();

        var direction = right * input.x + forward * input.y;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    static Vector3 GetReferenceRelativeDirection(Vector2 input, Transform reference)
    {
        var forward = reference.forward;
        var right = reference.right;
        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
            return new Vector3(input.x, 0f, input.y);

        forward.Normalize();
        right.Normalize();

        var direction = right * input.x + forward * input.y;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
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
