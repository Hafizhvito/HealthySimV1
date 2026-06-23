using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared exit path from OfficeScene / GymScene back to SampleScene.
/// </summary>
public static class SubSceneReturnHelper
{
    public const string SampleSceneName = "SampleScene";
    public const float ReturnFadeDuration = 0.28f;
    public const float DialogueTimeoutSeconds = 90f;

    public static void ReturnToSampleScene(string spawnId, string sceneName = SampleSceneName)
    {
        if (!string.IsNullOrWhiteSpace(spawnId))
            SpawnPlayerManager.TargetSpawnID = spawnId;

        string target = string.IsNullOrWhiteSpace(sceneName) ? SampleSceneName : sceneName;

        if (FadeManager.Instance != null)
        {
            FadeManager.Instance.FadeToBlackAndLoad(target, ReturnFadeDuration);
            return;
        }

        Debug.LogWarning("[SubSceneReturn] FadeManager missing — loading scene directly.");
        SceneManager.LoadScene(target);
    }

    public static IEnumerator WaitForDialogue(
        NpcDialogueMenuController dialogue,
        float pollInterval,
        float timeoutSeconds = DialogueTimeoutSeconds)
    {
        if (dialogue == null)
            yield break;

        float poll = Mathf.Max(0.02f, pollInterval);
        float elapsed = 0f;

        while (dialogue.IsOpen)
        {
            yield return new WaitForSecondsRealtime(poll);
            elapsed += poll;

            if (elapsed < timeoutSeconds)
                continue;

            Debug.LogWarning($"[SubSceneReturn] Dialogue timeout after {timeoutSeconds:F0}s — forcing close.");
            dialogue.ForceCloseSession();
            yield break;
        }
    }
}
