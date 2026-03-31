using System;
using System.Collections.Generic;
using UnityEngine;

public class NpcRestaurantInteractable : MonoBehaviour, IInteractable, IDialogueActor
{
    [Header("Identitas")]
    [SerializeField] private string npcName = "Pak Yono";

    [Header("Percakapan")]
    [SerializeField] [Range(0f, 100f)] private float trustScore = 45f;
    [SerializeField] private bool hasVisitedRestaurant;
    [SerializeField] private string firstVisitNpcId = "npc_restoran_first";
    [SerializeField] private string returnVisitNpcId = "npc_restoran_return";

    private Collider cachedCollider;
    private CameraSystem cameraSystem;
    private DialogueCatalogProvider dialogueCatalogProvider;
    private DialogueGraphData firstVisitFallbackGraph;
    private DialogueGraphData returnVisitFallbackGraph;
    private bool isMenuCloseSubscribed;

    void Awake()
    {
        cachedCollider = GetComponent<Collider>();
        cameraSystem = FindFirstObjectByType<CameraSystem>();

        GameObject manager = GameObject.Find("GameManager");
        if (manager != null)
            dialogueCatalogProvider = manager.GetComponent<DialogueCatalogProvider>();

        firstVisitFallbackGraph = BuildFirstVisitGraph();
        returnVisitFallbackGraph = BuildReturnVisitGraph();
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
        return $"Tekan E untuk ngobrol dengan {npcName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return PlayerStats.Instance != null;
    }

    public void Interact(GameObject interactor)
    {
        if (NpcDialogueMenuController.Instance == null)
        {
            Debug.LogWarning("[NPCRestoran] NpcDialogueMenuController belum tersedia.");
            return;
        }

        if (NpcDialogueMenuController.Instance.IsOpen)
            return;

        TrySubscribeMenuClose();

        DialogueGraphData graph = ResolveDialogueGraph();
        if (graph == null)
        {
            Debug.LogWarning("[NPCRestoran] Dialog restoran tidak ditemukan.");
            return;
        }

        if (cameraSystem != null)
            cameraSystem.DialogueZoomIn();

        if (!NpcDialogueMenuController.Instance.OpenDialogue(this, graph))
        {
            if (cameraSystem != null)
                cameraSystem.DialogueZoomOut();

            Debug.Log("[NPCRestoran] Gagal membuka menu dialog.");
            return;
        }

        hasVisitedRestaurant = true;
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
            PlayerStats.Instance.AddFood(consequence.energyDelta, consequence.calorieDelta, consequence.moodDelta);

        trustScore = Mathf.Clamp(trustScore + consequence.trustDelta, 0f, 100f);

        if (PlayerActionTracker.Instance != null)
            PlayerActionTracker.Instance.Track(consequence.trackerAction, gameObject.name);
    }

    private DialogueGraphData ResolveDialogueGraph()
    {
        string targetId = hasVisitedRestaurant ? returnVisitNpcId : firstVisitNpcId;

        if (dialogueCatalogProvider != null)
        {
            List<DialogueGraphData> catalog = dialogueCatalogProvider.GetDialogues();
            for (int i = 0; i < catalog.Count; i++)
            {
                DialogueGraphData data = catalog[i];
                if (data == null)
                    continue;

                if (!string.Equals(data.npcId, targetId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(data.npcDisplayName))
                    data.npcDisplayName = npcName;

                return data;
            }
        }

        DialogueGraphData fallback = hasVisitedRestaurant ? returnVisitFallbackGraph : firstVisitFallbackGraph;
        if (fallback != null)
            fallback.npcDisplayName = npcName;

        return fallback;
    }

    private DialogueGraphData BuildFirstVisitGraph()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = firstVisitNpcId;
        graph.npcDisplayName = npcName;
        graph.initialTrust = trustScore;
        graph.startNodeId = "start";
        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "start",
                fallbackLine = "Eh, silakan silakan- duduk dulu. Lagi ngebersihin ini sebentar. Pertama kali ke sini? Biasanya kalau orang baru, bingung mau pesen apa. Tenang, nanti aku bantu.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Yang paling laku apa?", "n1_bestseller", 0f, 0.5f, 2f),
                    CreateChoice("Yang paling murah aja.", "n1_cheap", 0f, 0f, 1f),
                    CreateChoice("Rekomendasiin yang enak menurut kamu.", "n1_rec", 0f, 1f, 3f)
                },
                npcFollowUpText = string.Empty,
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n1_bestseller",
                fallbackLine = "Nasi Goreng. Dari dulu sampai sekarang. Ada pelanggan saya- tiap pagi mesen ini, udah bertahun-tahun. Katanya kalau ga sarapan Nasi Goreng sini, harinya ga bener.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Iya, pesen itu.", "end_order", 2f, 1f, 2f),
                    CreateChoice("Ntar dulu, lihat-lihat dulu.", "n2_browse", 0f, 0f, 0f)
                },
                npcFollowUpText = "Entah karena emang enak, atau udah jadi ritual. Mau coba?",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n1_cheap",
                fallbackLine = "Haha, jujur ya. Suka yang jujur. Pisang sama Es Teh Manis paling terjangkau. Tapi kalau mau yang lebih nahan lapar, Tempe Goreng biasanya lebih worth it.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Pesen Tempe Goreng.", "end_order", 2f, 1f, 2f),
                    CreateChoice("Pisang aja dulu.", "end_order", 1f, 1f, 1f)
                },
                npcFollowUpText = "Banyak mahasiswa yang langganan combo itu. Murah, cukup buat lanjut aktivitas setengah hari.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n1_rec",
                fallbackLine = "Kalau aku yang milih... Gado-Gado. Bukan yang paling rame dipesan, tapi yang paling sering bikin orang kaget enak. Bahan-bahannya fresh, aku ga kompromi soal itu.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Oke, pesen Gado-Gado.", "end_order", 2f, 2f, 3f),
                    CreateChoice("Boleh lihat pilihan lain dulu?", "n2_browse", 0f, 0.5f, 1f)
                },
                npcFollowUpText = "Pelanggan yang pertama kali nyoba biasanya langsung balik lagi. Itu ukuran paling jujur.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n2_browse",
                fallbackLine = "Silakan. Mau yang berat atau yang ringan? Kalau habis aktivitas banyak, mending yang ada nasinya. Kalau mau santai aja, yang sayur atau buah juga ada.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Yang berat sekalian.", "n3_heavy", 0f, 0f, 0f),
                    CreateChoice("Yang ringan aja.", "n3_light", 0f, 0.5f, 0f)
                },
                npcFollowUpText = "Kalau bingung, sebut aja lagi pengen rasa apa, nanti aku cocokin.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n3_heavy",
                fallbackLine = "Ayam Goreng atau Nasi Goreng. Dua itu yang paling sering dipesan sama yang habis olahraga pagi.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Pesen Ayam Goreng.", "end_order", 3f, 1f, 1f),
                    CreateChoice("Nasi Goreng aja.", "end_order", 3f, 1f, 1f)
                },
                npcFollowUpText = "Kalau lagi laper berat, dua menu itu paling aman buat ngisi tenaga.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n3_light",
                fallbackLine = "Salad Sayur, Buah Apel, atau Jus Jeruk Segar. Yang Jus Jeruk Segar itu aku peres sendiri, ga pake sirup. Beda rasanya.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Jus Jeruk Segar.", "end_order", 1f, 2f, 1f),
                    CreateChoice("Salad Sayur.", "end_order", 1f, 2f, 1f)
                },
                npcFollowUpText = "Pelanggan langgananku yang lagi jaga pola makan sering minta itu. Katanya ga bikin eneg.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "end_order",
                fallbackLine = "Oke, sebentar ya.",
                choices = new List<DialogueChoiceData>(),
                npcFollowUpText = string.Empty,
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private DialogueGraphData BuildReturnVisitGraph()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = returnVisitNpcId;
        graph.npcDisplayName = npcName;
        graph.initialTrust = trustScore;
        graph.startNodeId = "start";
        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "start",
                fallbackLine = "Oh, yang kemarin. Mau yang sama lagi atau coba yang lain?",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Yang sama.", "end_order", 1f, 1f, 1f),
                    CreateChoice("Coba yang lain.", "n2_browse", 0f, 1f, 1f),
                    CreateChoice("Tanya dulu boleh?", "node_question", 0f, 0.5f, 1f)
                },
                npcFollowUpText = string.Empty,
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "node_question",
                fallbackLine = "Boleh, boleh. Mau tanya apa?",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Makanan sini fresh semua?", "node_q_fresh", 0f, 0.5f, 1f),
                    CreateChoice("Yang paling sehat apa?", "node_q_health", 0f, 0.5f, 1f)
                },
                npcFollowUpText = string.Empty,
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "node_q_fresh",
                fallbackLine = "Aku belanja subuh. Jadi yang pagi sampai siang masih fresh. Sore kadang tinggal beberapa. Makanya yang paling enak emang makan di sini pas pagi atau siang.",
                choices = new List<DialogueChoiceData>(),
                npcFollowUpText = string.Empty,
                isConversationEnd = true,
                isTerminal = true
            },
            new DialogueNodeData
            {
                nodeId = "node_q_health",
                fallbackLine = "Hm... susah jawabnya. Tergantung kondisi kamu juga sih. Tapi kalau aku lihat dari yang beli, yang paling jarang tumbang itu yang makannya beragam. Ga cuma satu jenis terus.",
                choices = new List<DialogueChoiceData>(),
                npcFollowUpText = "Itu bukan teori, itu pengamatan bertahun-tahun jualin makanan.",
                isConversationEnd = true,
                isTerminal = true
            },
            new DialogueNodeData
            {
                nodeId = "n2_browse",
                fallbackLine = "Silakan. Mau yang berat atau yang ringan? Kalau habis aktivitas banyak, mending yang ada nasinya. Kalau mau santai aja, yang sayur atau buah juga ada.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Yang berat sekalian.", "n3_heavy", 0f, 0f, 0f),
                    CreateChoice("Yang ringan aja.", "n3_light", 0f, 0.5f, 0f)
                },
                npcFollowUpText = "Kalau bingung, sebut aja lagi pengen rasa apa, nanti aku cocokin.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n3_heavy",
                fallbackLine = "Ayam Goreng atau Nasi Goreng. Dua itu yang paling sering dipesan sama yang habis olahraga pagi.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Pesen Ayam Goreng.", "end_order", 3f, 1f, 1f),
                    CreateChoice("Nasi Goreng aja.", "end_order", 3f, 1f, 1f)
                },
                npcFollowUpText = "Kalau lagi laper berat, dua menu itu paling aman buat ngisi tenaga.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "n3_light",
                fallbackLine = "Salad Sayur, Buah Apel, atau Jus Jeruk Segar. Yang Jus Jeruk Segar itu aku peres sendiri, ga pake sirup. Beda rasanya.",
                choices = new List<DialogueChoiceData>
                {
                    CreateChoice("Jus Jeruk Segar.", "end_order", 1f, 2f, 1f),
                    CreateChoice("Salad Sayur.", "end_order", 1f, 2f, 1f)
                },
                npcFollowUpText = "Pelanggan langgananku yang lagi jaga pola makan sering minta itu. Katanya ga bikin eneg.",
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "end_order",
                fallbackLine = "Oke, sebentar ya.",
                choices = new List<DialogueChoiceData>(),
                npcFollowUpText = string.Empty,
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private DialogueChoiceData CreateChoice(string text, string nextNodeId, float energyDelta, float moodDelta, float trustDelta)
    {
        return new DialogueChoiceData
        {
            choiceText = text,
            nextNodeId = nextNodeId,
            consequence = new DialogueConsequence
            {
                energyDelta = energyDelta,
                moodDelta = moodDelta,
                trustDelta = trustDelta,
                calorieDelta = 0f,
                trackerAction = trustDelta > 0f
                    ? PlayerActionTracker.ActionType.PositiveNpcTalk
                    : PlayerActionTracker.ActionType.GenericInteraction
            },
            gate = new DialogueCondition
            {
                useGate = false,
                minEnergyPercent = 0f,
                minMoodPercent = 0f,
                minTrust = 0f,
                allowedPeriods = new List<TimeManager.TimePeriod>()
            }
        };
    }

    private void HandleDialogueClosed()
    {
        if (cameraSystem != null)
            cameraSystem.DialogueZoomOut();
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

    public void SetDialogueIds(string firstVisitId, string returnVisitId)
    {
        if (!string.IsNullOrWhiteSpace(firstVisitId))
            firstVisitNpcId = firstVisitId;

        if (!string.IsNullOrWhiteSpace(returnVisitId))
            returnVisitNpcId = returnVisitId;
    }
}
