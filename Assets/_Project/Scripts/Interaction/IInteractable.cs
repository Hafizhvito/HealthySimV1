using UnityEngine;

public interface IInteractable
{
    string GetInteractionText();
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
