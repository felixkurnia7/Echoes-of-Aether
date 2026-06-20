using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    private void Awake() 
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetMovement(Vector3 wordlDirection, float speed)
    {
        bool moving = wordlDirection.magnitude > 0.01f;

        if (moving)
        {
            if (wordlDirection.x < -0.01f)
                spriteRenderer.flipX = true;
            else if (wordlDirection.x > 0.01f)
                spriteRenderer.flipX = false;

            Vector3 animatorDirection = wordlDirection.normalized;
            animator.SetFloat(MoveX, animatorDirection.x);
            animator.SetFloat(MoveY, animatorDirection.z);
            animator.SetFloat(Speed, speed);
        }

        animator.SetBool(IsMoving, moving);
    }
}
