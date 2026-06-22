using UnityEngine;
using System;

public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private Vector3 detectOffset = new(0f, 1f, 0f);
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private bool preferTargetInFront = true;

    static readonly Collider[] OverlapResults = new Collider[10];

    public IInteractable currentTarget { get; private set; }
    public event Action<IInteractable> OnTargetChanged;

    void Update()
    {
        IInteractable nearest = FindNearestInteractable();
        if (nearest == currentTarget)
            return;

        currentTarget = nearest;
        OnTargetChanged?.Invoke(currentTarget);
    }

    IInteractable FindNearestInteractable()
    {
        Vector3 origin = transform.position + detectOffset;
        int count = Physics.OverlapSphereNonAlloc(
            origin, detectionRadius, OverlapResults, interactableMask, QueryTriggerInteraction.Collide
        );
        IInteractable best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = OverlapResults[i];
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;
            IInteractable interactable = hit.GetComponent<IInteractable>();
            if (interactable == null)
                interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null)
                continue;
            if (interactable is MonoBehaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;
            float distance = Vector3.Distance(origin, hit.ClosestPoint(origin));
            float score = distance;
            if (preferTargetInFront)
            {
                Vector3 toTarget = hit.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    float facing = Vector3.Dot(transform.forward, toTarget.normalized);
                    if (facing < 0f)
                        score += 100f;
                }
            }
            if (score < bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }
        return best;
    }
}
