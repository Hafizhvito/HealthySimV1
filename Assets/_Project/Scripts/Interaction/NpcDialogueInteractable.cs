using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class NpcDialogueInteractable : MonoBehaviour, IInteractable, IDialogueActor
{
    [SerializeField] private string npcName = "Warga Kota";
    [SerializeField] private List<DialogueGraphData> dialogueOptions = new List<DialogueGraphData>();
    [SerializeField] private bool useGlobalDialogueCatalogWhenEmpty = true;
    [SerializeField] [Range(0f, 100f)] private float trustScore = 50f;
    [SerializeField] private bool avoidImmediateRepeat = true;
    [SerializeField] [Range(0, 3)] private int trustLevel;

    [Header("Balon Hint (World Space)")]
    [SerializeField] private Vector2 hintBubbleSize = new Vector2(220f, 72f);
    [SerializeField] private float hintHeight = 2.2f;
    [SerializeField] private float hintDuration = 2.8f;
    [SerializeField] private float speechCooldown = 0.3f;

    [Header("Social Recovery")]
    [SerializeField] private float socialEnergyGain = 1.5f;
    [SerializeField] private float socialMoodGain = 0.5f;
    [SerializeField] private float socialEnergyCooldown = 45f;
    [SerializeField] private string socialEnergyHintTemplate = "Ngobrol bikin kamu agak fresh. Energi +{0:0.#}";

    [Header("Optional References")]
    [SerializeField] private DialogueCatalogProvider dialogueCatalogProviderOverride;
    [SerializeField] private Transform playerOverride;

    private Collider cachedCollider;
    private Canvas hintCanvas;
    private TextMeshProUGUI hintText;
    private float nextSpeechAllowedTime;
    private Coroutine hintRoutine;
    private DialogueCatalogProvider dialogueCatalogProvider;
    private CameraSystem cameraSystem;
    private bool isMenuCloseSubscribed;
    private string lastDialogueId = string.Empty;
    private float nextSocialRewardTime = -999f;
    private readonly Dictionary<string, int> usageByDialogueId = new Dictionary<string, int>();
    private readonly Queue<string> recentDialogueQueue = new Queue<string>();
    private const int RecentDialogueHistorySize = 2;
    private TimeManager.TimePeriod? lastGreetingPeriod;
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        cameraSystem = FindFirstObjectByType<CameraSystem>();
        dialogueCatalogProvider = dialogueCatalogProviderOverride;
        trustLevel = Mathf.Clamp(Mathf.RoundToInt(trustScore / 33.4f), 0, 3);
    }

    void OnEnable()
    {
        InteractableRegistry.Register(this, cachedCollider, transform);
        TrySubscribeMenuClose();
    }

    void OnDisable()
    {
        TryUnsubscribeMenuClose();

        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText()
    {
        return $"Tekan E untuk berdialog dengan {npcName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return PlayerStats.Instance != null;
    }

    public void Interact(GameObject interactor)
    {
        if (cameraSystem == null)
            cameraSystem = FindFirstObjectByType<CameraSystem>();

        DialogueGraphData selectedDialogue = SelectDialogueForCurrentPeriod();
        if (selectedDialogue == null)
        {
            ShowHint("Aku belum punya topik obrolan sekarang.");
            return;
        }

        if (string.IsNullOrEmpty(selectedDialogue.npcDisplayName))
            selectedDialogue.npcDisplayName = npcName;

        if (selectedDialogue.initialTrust > 0f)
            trustScore = Mathf.Max(trustScore, selectedDialogue.initialTrust);

        if (NpcDialogueMenuController.Instance != null)
        {
            TrySubscribeMenuClose();

            if (cameraSystem != null)
                cameraSystem.DialogueZoomIn();

            if (!NpcDialogueMenuController.Instance.OpenDialogue(this, selectedDialogue))
            {
                if (cameraSystem != null)
                    cameraSystem.DialogueZoomOut();

                Debug.Log("[NPCMenu] Gagal dibuka (menu lain aktif).");
            }
            else
            {
                NpcWanderController wanderCtrl = GetComponent<NpcWanderController>();
                if (wanderCtrl != null)
                {
                    wanderCtrl.PauseWander();
                    wanderCtrl.FaceTarget(interactor.transform);
                }

                TutorialContextualUI.HasTalkedToNPC = true;
            }

            return;
        }

        ShowHint("Menu dialog belum tersedia.");
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
        if (cameraSystem == null)
            cameraSystem = FindFirstObjectByType<CameraSystem>();

        if (cameraSystem != null)
            cameraSystem.DialogueZoomOut();

        NpcWanderController wanderCtrl = GetComponent<NpcWanderController>();
        if (wanderCtrl != null)
            wanderCtrl.ResumeWander();

        TryGrantSocialRecovery();
    }

    private void TryGrantSocialRecovery()
    {
        if (PlayerStats.Instance == null)
            return;

        if (socialEnergyGain <= 0f && socialMoodGain <= 0f)
            return;

        if (Time.unscaledTime < nextSocialRewardTime)
            return;

        nextSocialRewardTime = Time.unscaledTime + Mathf.Max(3f, socialEnergyCooldown);

        float energyGain = Mathf.Max(0f, socialEnergyGain);
        float moodGain = Mathf.Max(0f, socialMoodGain);
        PlayerStats.Instance.AddFood(energyGain, 0f, moodGain, 0f, 0f);

        if (energyGain > 0f && !string.IsNullOrWhiteSpace(socialEnergyHintTemplate))
            ShowHint(string.Format(socialEnergyHintTemplate, energyGain));
    }

    public List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node)
    {
        List<DialogueChoiceData> result = new List<DialogueChoiceData>();
        if (node == null || node.choices == null)
            return result;

        float energyPercent = PlayerStats.Instance != null ? PlayerStats.Instance.EnergyPercent : 0f;
        float moodPercent = PlayerStats.Instance != null ? PlayerStats.Instance.MoodPercent : 0f;
        TimeManager.TimePeriod currentPeriod = TimeManager.Instance != null ? TimeManager.Instance.CurrentPeriod : TimeManager.TimePeriod.Morning;

        for (int i = 0; i < node.choices.Count; i++)
        {
            DialogueChoiceData choice = node.choices[i];
            if (choice == null)
                continue;

            if (choice.gate != null && !choice.gate.CanPass(energyPercent, moodPercent, trustScore, currentPeriod))
                continue;

            result.Add(choice);
        }

        return result;
    }

    public void ApplyConsequence(DialogueConsequence consequence)
    {
        if (consequence == null)
            return;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.AddFood(consequence.energyDelta, consequence.calorieDelta, consequence.moodDelta, 0f, 0f);

        trustScore = Mathf.Clamp(trustScore + consequence.trustDelta, 0f, 100f);

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(consequence.trackerAction, gameObject.name);

        string feedback = $"Energi {consequence.energyDelta:+0.#;-0.#;0} | Mood {consequence.moodDelta:+0.#;-0.#;0} | Trust {consequence.trustDelta:+0.#;-0.#;0}";
        ShowHint($"Kepercayaan: {trustScore:0}");
        Debug.Log($"[NPC] {npcName} -> {feedback}");
    }

    void LateUpdate()
    {
        MaybeShowPeriodGreeting();

        if (hintCanvas == null || !hintCanvas.gameObject.activeSelf)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        hintCanvas.transform.position = transform.position + Vector3.up * hintHeight;
        hintCanvas.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    private void ShowHint(string line)
    {
        if (Time.time < nextSpeechAllowedTime)
            return;

        nextSpeechAllowedTime = Time.time + speechCooldown;
        EnsureHintBubble();

        if (hintRoutine != null)
            StopCoroutine(hintRoutine);

        hintRoutine = StartCoroutine(HintRoutine(line));
    }

    private IEnumerator HintRoutine(string line)
    {
        hintText.text = line;
        hintCanvas.gameObject.SetActive(true);
        yield return new WaitForSeconds(hintDuration);
        hintCanvas.gameObject.SetActive(false);
        hintRoutine = null;
    }

    private void EnsureHintBubble()
    {
        if (hintCanvas != null && hintText != null)
            return;

        GameObject canvasObj = new GameObject($"NPCHintBubble_{gameObject.name}");
        hintCanvas = canvasObj.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.WorldSpace;
        hintCanvas.sortingOrder = 25;
        hintCanvas.transform.localScale = Vector3.one * 0.003f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        RectTransform canvasRect = hintCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = hintBubbleSize;

        GameObject bgObj = new GameObject("SpeechBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.sizeDelta = hintBubbleSize;

        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.1f, 0.82f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("SpeechText");
        textObj.transform.SetParent(bgObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.07f, 0.1f);
        textRect.anchorMax = new Vector2(0.93f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        hintText = textObj.AddComponent<TextMeshProUGUI>();
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.fontSize = 26f;
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.color = new Color(0.98f, 0.96f, 0.9f, 1f);

        hintCanvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (hintCanvas != null)
            Destroy(hintCanvas.gameObject);
    }

    private DialogueGraphData SelectDialogueForCurrentPeriod()
    {
        if (dialogueCatalogProvider == null)
        {
            GameObject manager = GameObject.Find("GameManager");
            if (manager != null)
            {
                dialogueCatalogProvider = manager.GetComponent<DialogueCatalogProvider>();
                if (dialogueCatalogProvider != null)
                    Debug.LogWarning($"{FallbackLogPrefix} Resolved DialogueCatalogProvider via GameManager lookup on {gameObject.name}.");
            }
        }

        if (dialogueOptions.Count == 0 && dialogueCatalogProvider != null)
            dialogueOptions.AddRange(dialogueCatalogProvider.GetDialogues());

        if (dialogueOptions.Count == 0 && useGlobalDialogueCatalogWhenEmpty)
            dialogueOptions.AddRange(Resources.LoadAll<DialogueGraphData>("Dialogue"));

        TimeManager.TimePeriod currentPeriod = TimeManager.Instance != null
            ? TimeManager.Instance.CurrentPeriod
            : TimeManager.TimePeriod.Morning;

        List<DialogueGraphData> available = new List<DialogueGraphData>();
        float energyPercent = PlayerStats.Instance != null ? PlayerStats.Instance.EnergyPercent : 0f;
        float moodPercent = PlayerStats.Instance != null ? PlayerStats.Instance.MoodPercent : 0f;

        for (int i = 0; i < dialogueOptions.Count; i++)
        {
            DialogueGraphData data = dialogueOptions[i];
            if (data == null)
                continue;

            DialogueNodeData startNode = data.GetNode(data.startNodeId);
            if (startNode == null)
                continue;

            bool hasAccessible = HasChoiceForCurrentState(startNode, currentPeriod, energyPercent, moodPercent);
            if (hasAccessible)
                available.Add(data);
        }

        if (available.Count == 0)
            return null;

        DialogueGraphData selected = PickWeightedDialogue(available, currentPeriod);
        if (selected == null)
            return null;

        string key = GetDialogueKey(selected);
        if (!usageByDialogueId.ContainsKey(key))
            usageByDialogueId[key] = 0;

        string periodKey = GetPeriodUsageKey(key, currentPeriod);
        if (!usageByDialogueId.ContainsKey(periodKey))
            usageByDialogueId[periodKey] = 0;

        usageByDialogueId[key] += 1;
        usageByDialogueId[periodKey] += 1;
        lastDialogueId = key;
        EnqueueRecentDialogue(key);
        return selected;
    }

    private bool HasChoiceForCurrentState(DialogueNodeData node, TimeManager.TimePeriod period, float energyPercent, float moodPercent)
    {
        if (node == null || node.choices == null || node.choices.Count == 0)
            return false;

        for (int i = 0; i < node.choices.Count; i++)
        {
            DialogueChoiceData choice = node.choices[i];
            if (choice == null || choice.gate == null || !choice.gate.useGate)
                return true;

            if (choice.gate.CanPass(energyPercent, moodPercent, trustScore, period))
                return true;
        }

        return false;
    }

    private DialogueGraphData PickWeightedDialogue(List<DialogueGraphData> available, TimeManager.TimePeriod currentPeriod)
    {
        if (available == null || available.Count == 0)
            return null;

        float totalWeight = 0f;
        float[] weights = new float[available.Count];

        for (int i = 0; i < available.Count; i++)
        {
            string key = GetDialogueKey(available[i]);
            usageByDialogueId.TryGetValue(key, out int used);
            usageByDialogueId.TryGetValue(GetPeriodUsageKey(key, currentPeriod), out int periodUsed);

            float weight = 1f / (1f + (used * 1.35f));
            weight *= 1f / (1f + (periodUsed * 1.7f));

            if (avoidImmediateRepeat && available.Count > 1 && string.Equals(lastDialogueId, key, StringComparison.OrdinalIgnoreCase))
                weight *= 0.05f;

            if (available.Count > 1 && recentDialogueQueue.Contains(key))
                weight *= 0.08f;

            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return available[0];

        float pick = SessionSeedManager.Instance != null
            ? SessionSeedManager.Instance.NextFloat01() * totalWeight
            : UnityEngine.Random.value * totalWeight;

        float running = 0f;
        for (int i = 0; i < available.Count; i++)
        {
            running += weights[i];
            if (pick <= running)
                return available[i];
        }

        return available[available.Count - 1];
    }

    private string GetDialogueKey(DialogueGraphData data)
    {
        if (data == null)
            return string.Empty;

        return string.IsNullOrEmpty(data.npcId) ? data.name : data.npcId;
    }

    private string GetPeriodUsageKey(string dialogueKey, TimeManager.TimePeriod period)
    {
        return $"{period}:{dialogueKey}";
    }

    private void EnqueueRecentDialogue(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId))
            return;

        recentDialogueQueue.Enqueue(dialogueId);
        while (recentDialogueQueue.Count > RecentDialogueHistorySize)
            recentDialogueQueue.Dequeue();
    }

    public void ApplyTrustDelta(float delta)
    {
        trustScore = Mathf.Clamp(trustScore + delta, 0f, 100f);
        trustLevel = Mathf.Clamp(Mathf.RoundToInt(trustScore / 33.4f), 0, 3);
        Debug.Log($"[NPC] Trust level {trustLevel} ({trustScore:0})");
    }

    private void MaybeShowPeriodGreeting()
    {
        if (TimeManager.Instance == null)
            return;

        TimeManager.TimePeriod period = TimeManager.Instance.CurrentPeriod;
        if (lastGreetingPeriod.HasValue && lastGreetingPeriod.Value == period)
            return;

        GameObject player = playerOverride != null ? playerOverride.gameObject : GameObject.FindWithTag("Player");
        if (player == null)
            return;

        if (Vector3.Distance(player.transform.position, transform.position) > 4f)
            return;

        lastGreetingPeriod = period;
        switch (period)
        {
            case TimeManager.TimePeriod.Morning:
                ShowHint("Selamat pagi! Sudah sarapan belum?");
                break;
            case TimeManager.TimePeriod.Afternoon:
                ShowHint("Sudah makan siang? Jangan sampai skip ya.");
                break;
            case TimeManager.TimePeriod.Evening:
                ShowHint("Sore nih, jaga energimu untuk malam ini.");
                break;
            case TimeManager.TimePeriod.Night:
                ShowHint("Hampir selesai! Gimana hari ini menurutmu?");
                break;
        }
    }

    public void SetDialogueOptions(List<DialogueGraphData> options, bool useGlobalFallbackWhenEmpty)
    {
        dialogueOptions.Clear();
        if (options != null)
            dialogueOptions.AddRange(options);

        useGlobalDialogueCatalogWhenEmpty = useGlobalFallbackWhenEmpty;
    }
}
