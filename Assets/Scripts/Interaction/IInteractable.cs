public interface IInteractable
{
    bool CanInteract(PlayerController player);
    void Interact(PlayerController player);
    string GetPromptText();
}
