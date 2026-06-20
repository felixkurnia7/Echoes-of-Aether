using UnityEngine;

[RequireComponent(typeof(CharacterController))]
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

    [SerializeField] private CharacterVisual characterVisual;
    public PlayerState CurrentPlayerState { get; private set; } = PlayerState.Idle;
    CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        SyncStateFromGameManager();
        if (!CanProcessGameplayInput()) return;

        // Movement & interact — Fase 3
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
