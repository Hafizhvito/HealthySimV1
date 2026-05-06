using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingPanelBinder : MonoBehaviour
{
    [Header("Root")]
    public RectTransform panelRoot;
    public CanvasGroup panelGroup;

    [Header("Content")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Button closeButton;
    public Image accentBar;
}
