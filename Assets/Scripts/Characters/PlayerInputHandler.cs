using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool InteractPressed { get; private set; }

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }
    
    public  void OnInteract(InputValue value)
    {
        if (value.isPressed)
            InteractPressed = true;
    }

    public void ConsumeInteract()
    {
        InteractPressed = false;
    }
}
