using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorkSessionController : MonoBehaviour
{
    [SerializeField] private ClockAnimationUI _clockUI;
    [SerializeField] private NpcDialogueMenuController _dialogueUI;
    [SerializeField] private string _mainSceneName = "SampleScene";

    [Header("Optional Scene References")]
    [SerializeField] private NpcDialogueInteractable _bossInteractableOverride;
    [SerializeField] private Transform _playerSpawnPointOverride;
    [SerializeField] private GameObject _playerOverride;

    [Header("Flow Timings")]
    [SerializeField] private float _postLoadDelay = 0.5f;
    [SerializeField] private float _dialoguePollInterval = 0.05f;

    private WorkSessionData _activeSession;
    private NpcDialogueInteractable _bossInteractable;
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";

    private void Start()
    {
        StartCoroutine(RunFlow());
    }

    private IEnumerator RunFlow()
    {
        if (WorkSessionManager.Instance == null || WorkSessionManager.Instance.PendingSession == null)
        {
            if (FadeManager.Instance != null)
                FadeManager.Instance.FadeToBlackAndLoad(_mainSceneName, 0.5f);
            yield break;
        }

        _activeSession = WorkSessionManager.Instance.PendingSession;

        MovePlayerToSpawnPoint();

        yield return new WaitForSecondsRealtime(_postLoadDelay);

        if (!ResolveSceneDependencies())
        {
            if (FadeManager.Instance != null)
                FadeManager.Instance.FadeToBlackAndLoad(_mainSceneName, 0.5f);
            yield break;
        }

        bool preWorkDone = false;
        DialogueGraphData preWorkDialogue = BuildPreWorkDialogueForEnergy(_activeSession.energyAtStart);
        yield return StartCoroutine(PlayDialogueAndWait(preWorkDialogue, () => preWorkDone = true));
        if (!preWorkDone)
        {
            if (FadeManager.Instance != null)
                FadeManager.Instance.FadeToBlackAndLoad(_mainSceneName, 0.5f);
            yield break;
        }

        bool animationCompleted = false;
        _clockUI.PlayWorkAnimation(_activeSession, _ =>
        {
            animationCompleted = true;
        });

        while (!animationCompleted)
            yield return null;

        WorkSessionManager.Instance.ApplyResult(_activeSession);

        DialogueGraphData postDialogue = BuildPostWorkDialogueForResult();
        yield return StartCoroutine(PlayDialogueAndWait(postDialogue, null));

        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToBlackAndLoad(_mainSceneName, 0.5f);
    }

    private bool ResolveSceneDependencies()
    {
        if (_dialogueUI == null)
            _dialogueUI = NpcDialogueMenuController.Instance;

        if (_dialogueUI == null)
            _dialogueUI = FindFirstObjectByType<NpcDialogueMenuController>(FindObjectsInactive.Include);

        if (_dialogueUI == null)
        {
            GameObject dialogueObj = new GameObject("NpcDialogueMenuController_Runtime");
            _dialogueUI = dialogueObj.AddComponent<NpcDialogueMenuController>();
            Debug.LogWarning($"{FallbackLogPrefix} Created runtime NpcDialogueMenuController in OfficeScene.");
        }

        if (_dialogueUI == null)
            return false;

        if (_clockUI == null)
            _clockUI = FindFirstObjectByType<ClockAnimationUI>();

        if (_clockUI == null)
            _clockUI = FindFirstObjectByType<ClockAnimationUI>(FindObjectsInactive.Include);

        if (_clockUI == null)
        {
            GameObject clockObj = new GameObject("ClockAnimationUI");
            _clockUI = clockObj.AddComponent<ClockAnimationUI>();
            Debug.LogWarning($"{FallbackLogPrefix} Created runtime ClockAnimationUI in OfficeScene.");
        }

        if (_bossInteractable == null && _bossInteractableOverride != null)
            _bossInteractable = _bossInteractableOverride;

        if (_bossInteractable == null)
        {
            GameObject bossObj = GameObject.FindWithTag("NPCBoss");
            if (bossObj != null)
            {
                _bossInteractable = bossObj.GetComponent<NpcDialogueInteractable>();
                Debug.Log($"{FallbackLogPrefix} Resolved boss via NPCBoss tag lookup.");
            }
        }

        if (_bossInteractable == null)
        {
            Debug.LogWarning($"{FallbackLogPrefix} Boss interactable missing in OfficeScene. Set optional reference on WorkSessionController for stable swaps.");
            return false;
        }

        return true;
    }

    private IEnumerator PlayDialogueAndWait(DialogueGraphData graph, System.Action onClosed)
    {
        if (graph == null || _dialogueUI == null || _bossInteractable == null)
        {
            onClosed?.Invoke();
            yield break;
        }

        bool opened = _dialogueUI.OpenDialogue(_bossInteractable, graph);
        if (!opened)
        {
            onClosed?.Invoke();
            yield break;
        }

        while (_dialogueUI != null && _dialogueUI.IsOpen)
            yield return new WaitForSecondsRealtime(_dialoguePollInterval);

        onClosed?.Invoke();
    }

    private DialogueGraphData BuildPostWorkDialogueForResult()
    {
        if (WorkSessionManager.Instance == null || WorkSessionManager.Instance.LastSession == null)
            return CreatePostWorkDialogueForFailedState();

        WorkSessionData session = WorkSessionManager.Instance.LastSession;

        if (session.result == WorkResult.Failed)
            return CreatePostWorkDialogueForFailedState();

        if (session.result == WorkResult.Partial || session.performanceDropped)
            return CreatePostWorkDialogueForPartialState(session);

        return CreatePostWorkDialogueForFullState(session);
    }

    private static DialogueGraphData BuildPreWorkDialogueForEnergy(float energy)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";

        float e = Mathf.Clamp01(energy);
        if (e >= 0.65f)
        {
            graph.nodes = new List<DialogueNodeData>
            {
                new DialogueNodeData
                {
                    nodeId = "node_0",
                    fallbackLine = "Nah, ini baru aura juara. Energi kamu lagi tinggi, gas pol tapi tetap rapi ya.",
                    variationPool = new List<string>
                    {
                        "Mantap, kelihatan siap tempur. Kalau ritme kerja konsisten, hari ini kamu bisa pulang bawa bonus.",
                        "Senyum kamu beda hari ini, positif banget. Jaga tempo, kualitas jangan turun.",
                        "Good vibe. Kalau fokus sampai akhir shift, aku catat performa kamu sebagai yang paling stabil."
                    },
                    choices = new List<DialogueChoiceData>
                    {
                        new DialogueChoiceData { choiceText = "(Percaya Diri) Siap, Pak. Saya kasih hasil terbaik.", nextNodeId = "node_1" },
                        new DialogueChoiceData { choiceText = "(Antusias) Saya pengen kejar bonus hari ini.", nextNodeId = "node_1" }
                    },
                    isConversationEnd = false,
                    isTerminal = false
                },
                new DialogueNodeData
                {
                    nodeId = "node_1",
                    fallbackLine = "Itu semangat yang aku mau. Oke, mulai kerja sekarang.",
                    npcFollowUpText = "Bikin hari ini jadi shift paling mulus minggu ini.",
                    choices = new List<DialogueChoiceData>(),
                    isConversationEnd = true,
                    isTerminal = true
                }
            };
        }
        else if (e >= 0.35f)
        {
            graph.nodes = new List<DialogueNodeData>
            {
                new DialogueNodeData
                {
                    nodeId = "node_0",
                    fallbackLine = "Kamu masih oke, tapi jangan terlalu ngebut. Jaga napas, jaga fokus.",
                    variationPool = new List<string>
                    {
                        "Shift ini butuh ritme, bukan sprint. Main aman tapi jangan lambat.",
                        "Kondisi kamu cukup, jadi kerja cerdas. Salah dikit bisa numpuk.",
                        "Aku lihat kamu agak capek, tapi masih bisa kejar target kalau disiplin."
                    },
                    choices = new List<DialogueChoiceData>
                    {
                        new DialogueChoiceData { choiceText = "(Tenang) Saya kerja stabil, Pak.", nextNodeId = "node_1" },
                        new DialogueChoiceData { choiceText = "(Fokus) Siap, saya jaga kualitas.", nextNodeId = "node_1" }
                    },
                    isConversationEnd = false,
                    isTerminal = false
                },
                new DialogueNodeData
                {
                    nodeId = "node_1",
                    fallbackLine = "Sip. Kalau mulai goyah, mending turunin tempo daripada berantakan.",
                    npcFollowUpText = "Mulai sekarang. Jangan kasih celah buat komplain pelanggan.",
                    choices = new List<DialogueChoiceData>(),
                    isConversationEnd = true,
                    isTerminal = true
                }
            };
        }
        else
        {
            graph.nodes = new List<DialogueNodeData>
            {
                new DialogueNodeData
                {
                    nodeId = "node_0",
                    fallbackLine = "Kondisi kamu lagi drop, ini bahaya kalau dipaksa kerja penuh.",
                    variationPool = new List<string>
                    {
                        "Jujur, energi kamu rendah. Kalau kualitas jeblok, aku yang kena semprot juga.",
                        "Kamu keliatan capek berat. Kalau tetap maksa, jam kerja bisa aku stop sebelum selesai.",
                        "Saya kasih kesempatan, tapi ingat: performa turun = shift dipotong dan gaji ikut turun."
                    },
                    choices = new List<DialogueChoiceData>
                    {
                        new DialogueChoiceData { choiceText = "(Ngotot) Saya tetap kerja, Pak. Saya butuh uangnya.", nextNodeId = "node_1" },
                        new DialogueChoiceData { choiceText = "(Pasrah) Saya paham risikonya, tetap saya jalanin.", nextNodeId = "node_1" }
                    },
                    isConversationEnd = false,
                    isTerminal = false
                },
                new DialogueNodeData
                {
                    nodeId = "node_1",
                    fallbackLine = "Baik, kamu sendiri yang minta. Kalau performa jatuh, shift langsung saya hentikan.",
                    npcFollowUpText = "Buktikan kamu masih bisa jaga kualitas.",
                    choices = new List<DialogueChoiceData>(),
                    isConversationEnd = true,
                    isTerminal = true
                }
            };
        }

        return graph;
    }

    private static DialogueGraphData CreatePostWorkDialogueForFullState(WorkSessionData session)
    {
        int pay = Mathf.Max(0, session.moneyEarned);
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss_post";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = "Nah begini! Kerjamu rapi, cepat, dan minim revisi. Ini standar yang aku suka.",
                npcFollowUpText = $"Gaji hari ini Rp{pay}. Pertahankan, bonus bakal rutin.",
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private static DialogueGraphData CreatePostWorkDialogueForPartialState(WorkSessionData session)
    {
        int pay = Mathf.Max(0, session.moneyEarned);
        int completion = Mathf.RoundToInt(Mathf.Clamp01(session.completionRatio) * 100f);

        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss_post";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = "Saya udah bilang, kualitasmu drop di tengah shift. Jam kerja terpaksa saya stop.",
                npcFollowUpText = $"Kerjaan beres {completion}%. Gaji kamu hari ini Rp{pay}, dipotong karena performa turun. Besok datang lebih fit.",
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private static DialogueGraphData CreatePostWorkDialogueForFailedState()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss_post";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = "Kondisi kamu bener-bener ga memungkinkan buat lanjut kerja.",
                npcFollowUpText = "Hari ini ga ada upah. Pulang, istirahat, dan balik kalau energimu udah pulih.",
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private void MovePlayerToSpawnPoint()
    {
        Transform spawnTransform = _playerSpawnPointOverride;
        if (spawnTransform == null)
        {
            GameObject spawn = GameObject.Find("PlayerSpawnPoint");
            if (spawn != null)
                spawnTransform = spawn.transform;
        }

        if (spawnTransform == null)
        {
            Debug.LogWarning($"{FallbackLogPrefix} Missing PlayerSpawnPoint in OfficeScene.");
            return;
        }

        GameObject player = _playerOverride;
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning($"{FallbackLogPrefix} Missing Player in OfficeScene when moving to spawn point.");
            return;
        }

        player.transform.position = spawnTransform.position;
        player.transform.rotation = spawnTransform.rotation;
    }
}
