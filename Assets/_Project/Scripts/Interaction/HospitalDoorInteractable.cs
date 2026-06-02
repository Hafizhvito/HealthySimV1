using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HospitalDoorInteractable : MonoBehaviour, IInteractable, IDialogueActor
{
    [SerializeField] private string proximityHintText = "Periksakan kondisi kesehatanmu ke dokter.";
    [SerializeField] private string npcName = "dr. Sri Wuryanti";
    [SerializeField] private DoctorSriDialogueController dialogueController;
    [SerializeField] private GameObject hospitalDoorModel;
    [SerializeField] private Sprite doctorPortraitSprite;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private GameObject hospitalOverlay;
    private Collider cachedCollider;
    private Coroutine floatingTextRoutine;
    private bool isMenuCloseSubscribed;
    private bool awaitingConsultationClose;

    private void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        if (cachedCollider == null)
            cachedCollider = GetComponentInChildren<Collider>();

        if (string.IsNullOrWhiteSpace(npcName))
            npcName = "dr. Sri Wuryanti";
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
        TrySubscribeMenuClose();
    }

    private void Update()
    {
        if (!awaitingConsultationClose)
            return;

        if (NpcDialogueMenuController.Instance == null)
            return;

        if (!NpcDialogueMenuController.Instance.IsOpen)
            HandleDialogueClosed();
    }

    private void OnDisable()
    {
        TryUnsubscribeMenuClose();
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText()
    {
        return proximityHintText;
    }

    public string GetInteractPrompt()
    {
        return proximityHintText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    public void OnInteract(GameObject interactor)
    {
        PlayerStats playerStats = PlayerStats.Instance;
        if (playerStats == null)
        {
            ShowFloatingText("Klinik belum siap.", 2f);
            return;
        }

        if (dialogueController == null)
        {
            ShowFloatingText("Dokter belum siap.", 2f);
            return;
        }

        if (NpcDialogueMenuController.Instance == null)
        {
            ShowFloatingText("Dialog belum siap.", 2f);
            return;
        }

        if (doorOpenSound != null)
            AudioSource.PlayClipAtPoint(doorOpenSound, transform.position);

        if (hospitalDoorModel != null && !hospitalDoorModel.activeSelf)
            hospitalDoorModel.SetActive(true);

        if (doctorPortraitSprite != null)
        {
            _ = doctorPortraitSprite.texture;
        }

        float healthScore = playerStats.HealthScoreThisPhase;
        float dailyFat = playerStats.DailyFat;
        float dailyProtein = playerStats.DailyProtein;
        DialogueGraphData graph = dialogueController.BuildConsultationDialogue(healthScore, dailyFat, dailyProtein);

        if (graph == null)
        {
            ShowFloatingText("Dialog belum siap.", 2f);
            return;
        }

        bool opened = NpcDialogueMenuController.Instance.OpenDialogue(this, graph);
        if (opened)
        {
            TrySubscribeMenuClose();
            awaitingConsultationClose = true;
            if (hospitalOverlay != null) hospitalOverlay.SetActive(true);
        }
        else
        {
            ShowFloatingText("Dialog belum siap.", 2f);
        }
    }

    private void TrySubscribeMenuClose()
    {
        if (isMenuCloseSubscribed)
            return;

        if (NpcDialogueMenuController.Instance == null)
            return;

        NpcDialogueMenuController.Instance.OnDialogueClosed += HandleDialogueClosed;
        isMenuCloseSubscribed = true;
    }

    private void TryUnsubscribeMenuClose()
    {
        if (!isMenuCloseSubscribed)
            return;

        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.OnDialogueClosed -= HandleDialogueClosed;

        isMenuCloseSubscribed = false;
    }

    private void HandleDialogueClosed()
    {
        if (!awaitingConsultationClose)
            return;

        awaitingConsultationClose = false;

        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.ForceCleanupAfterSceneLoad();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.SetVisitedHospital();
        if (hospitalOverlay != null)
            hospitalOverlay.SetActive(false);
    }

    public List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node)
    {
        if (node == null)
            return new List<DialogueChoiceData>();

        return node.choices ?? new List<DialogueChoiceData>();
    }

    public void ApplyConsequence(DialogueConsequence consequence)
    {
    }

    private void ShowFloatingText(string text, float duration)
    {
        if (floatingTextRoutine != null)
            StopCoroutine(floatingTextRoutine);

        floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("HospitalDoorFloatingText");

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.68f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            Camera cam = Camera.main;
            Vector3 displayPos = transform.position + Vector3.up * 1.25f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.001f)
                    toCam = -transform.forward;

                toCam.Normalize();
                displayPos = transform.position + toCam * 0.75f + Vector3.up * 1.25f;
                textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - cam.transform.position);
            }

            textObj.transform.position = displayPos;

            yield return null;
        }

        Destroy(textObj);
        floatingTextRoutine = null;
    }
}
