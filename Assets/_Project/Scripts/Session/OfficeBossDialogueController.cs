using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OfficeBossDialogueController : MonoBehaviour
{
    [SerializeField] private NpcDialogueInteractable bossInteractable;
    [SerializeField] private DialogueGraphData preWorkDialogue;
    [SerializeField] private DialogueGraphData postWorkDialogue;
    [SerializeField] private string bossTag = "NPCBoss";

    private const string OfficeSceneName = "OfficeScene";
    private const string FallbackLogPrefix = "[SwapContract/Fallback]";
    private bool subscribed;

    public void Configure(NpcDialogueInteractable boss, DialogueGraphData preWork, DialogueGraphData postWork)
    {
        bossInteractable = boss;
        preWorkDialogue = preWork;
        postWorkDialogue = postWork;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != OfficeSceneName)
            return;

        if (bossInteractable == null)
        {
            GameObject bossObj = GameObject.FindWithTag(bossTag);
            if (bossObj != null)
            {
                bossInteractable = bossObj.GetComponent<NpcDialogueInteractable>();
                if (bossInteractable != null)
                    Debug.LogWarning($"{FallbackLogPrefix} Resolved Office boss via tag lookup ({bossTag}).");
            }
        }

        if (bossInteractable != null && preWorkDialogue != null)
        {
            DialogueGraphData selected = preWorkDialogue;
            if (WorkSessionManager.Instance != null && WorkSessionManager.Instance.HasWorkedToday)
                selected = BuildPostWorkDialogueForLastResult();

            if (selected != null)
                bossInteractable.SetDialogueOptions(new List<DialogueGraphData> { selected }, false);
        }

        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        if (NpcDialogueMenuController.Instance != null)
            NpcDialogueMenuController.Instance.OnDialogueClosed -= HandleDialogueClosed;

        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || NpcDialogueMenuController.Instance == null)
            return;

        NpcDialogueMenuController.Instance.OnDialogueClosed += HandleDialogueClosed;
        subscribed = true;
    }

    private void HandleDialogueClosed()
    {
        if (WorkSessionManager.Instance == null)
            return;

        if (WorkSessionManager.Instance.HasWorkedToday)
            return;

        WorkSessionManager.Instance.RequestWorkStartFromBossDialogue();
    }

    public DialogueGraphData BuildPostWorkDialogueForLastResult()
    {
        if (postWorkDialogue == null)
            return null;

        DialogueGraphData runtimeCopy = ScriptableObject.Instantiate(postWorkDialogue);

        if (WorkSessionManager.Instance == null || WorkSessionManager.Instance.LastSession == null)
        {
            runtimeCopy.startNodeId = "node_0_partial";
            return runtimeCopy;
        }

        WorkSessionData session = WorkSessionManager.Instance.LastSession;
        if (session.result == WorkResult.Failed)
            runtimeCopy.startNodeId = "node_0_failed";
        else if (session.result == WorkResult.Partial)
            runtimeCopy.startNodeId = "node_0_partial";
        else
            runtimeCopy.startNodeId = "node_0";

        return runtimeCopy;
    }
}
