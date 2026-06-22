using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class WorldInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptText = "Interact";
    [SerializeField] private bool interactOnce;
    [SerializeField] private UnityEvent onInteract;

    bool hasBeenUsed;

    void Reset()
    {
        if (TryGetComponent(out Collider col))
            col.isTrigger = true;
    }

    public bool CanInteract(PlayerController player)
    {
        return !interactOnce || !hasBeenUsed;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
            return;

        hasBeenUsed = true;
        onInteract?.Invoke();
    }

    public string GetPromptText() => promptText;
}
