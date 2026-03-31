using UnityEngine;

public class StashPanelController : MonoBehaviour
{
    [SerializeField] private FoodStashMenuController stashMenu;

    void Start()
    {
        if (stashMenu == null)
            stashMenu = FindFirstObjectByType<FoodStashMenuController>();
    }

    public void OpenStashPanel()
    {
        if (stashMenu != null)
            stashMenu.Open();
    }

    public void CloseStashPanel()
    {
        if (stashMenu != null)
            stashMenu.Close();
    }
}
