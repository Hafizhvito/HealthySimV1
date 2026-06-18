using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FaintNotificationController : MonoBehaviour
{
    private const string DefaultTitle = "Pingsan";
    private const string DefaultBody =
        "Kamu pingsan karena kehabisan energi. Pastikan kamu selalu makan dan istirahat yang cukup agar tetap bisa beraktivitas.";

    [Header("Bindings")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button continueButton;

    private bool isShowing;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        TryAutoBind();
        BindContinueButton();
    }

    private void Start()
    {
        if (!isShowing)
            HideImmediate();
    }

    public void ShowPanel()
    {
        TryAutoBind();
        BindContinueButton();

        if (panelRoot == null)
        {
            Debug.LogWarning("[FaintNotificationController] ShowPanel dibatalkan: panelRoot belum terikat.");
            return;
        }

        if (titleText != null)
            titleText.text = DefaultTitle;

        if (bodyText != null)
            bodyText.text = DefaultBody;

        isShowing = true;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();

        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        Time.timeScale = 0f;
    }

    public void ClosePanel()
    {
        Time.timeScale = 1f;
        HideImmediate();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.ResetFaintState();

        isShowing = false;
    }

    /// <summary>
    /// Hides the faint panel and resumes scaled time without requiring a button click.
    /// Used when sleep/wake already restores the player.
    /// </summary>
    public void DismissForSleepWake()
    {
        Time.timeScale = 1f;
        HideImmediate();
        isShowing = false;
    }

    private void BindContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.onClick.RemoveListener(ClosePanel);
        continueButton.onClick.AddListener(ClosePanel);
    }

    private void TryAutoBind()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelGroup == null && panelRoot != null)
            panelGroup = panelRoot.GetComponent<CanvasGroup>();

        Transform card = panelRoot != null ? panelRoot.transform.Find("Card") : null;
        if (card == null)
            return;

        if (titleText == null)
        {
            Transform named = card.Find("CharacterNameText");
            if (named == null)
                named = card.Find("TitleText");
            if (named != null)
                titleText = named.GetComponent<TextMeshProUGUI>();
        }

        if (bodyText == null)
        {
            Transform named = card.Find("BackstoryBodyText");
            if (named == null)
                named = card.Find("BodyText");
            if (named != null)
                bodyText = named.GetComponent<TextMeshProUGUI>();
        }

        if (continueButton == null)
        {
            Transform named = card.Find("ContinueButton");
            if (named == null)
                named = card.Find("LanjutButton");
            if (named != null)
                continueButton = named.GetComponent<Button>();
        }
    }

    private void HideImmediate()
    {
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
