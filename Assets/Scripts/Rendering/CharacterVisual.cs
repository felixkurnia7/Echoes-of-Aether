using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform modelTransform;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    void Awake()
    {
        if (modelTransform == null)
            modelTransform = transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void SetMovement(Vector3 worldDirection, float speed)
    {
        bool moving = speed > 0.01f;

        if (moving && worldDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(worldDirection);
            modelTransform.rotation = Quaternion.Slerp(
                modelTransform.rotation, targetRot, Time.deltaTime * 12f);
            Vector3 animDir = worldDirection.normalized;
            animator.SetFloat(MoveX, animDir.x);
            animator.SetFloat(MoveY, animDir.z);
            animator.SetFloat(Speed, speed);
        }
        else
        {
            animator.SetFloat(MoveX, 0f);
            animator.SetFloat(MoveY, 0f);
            animator.SetFloat(Speed, 0f);
        }

        animator.SetBool(IsMoving, moving);
    }
}
