#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoreLoopSceneValidator
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OfficeScenePath = "Assets/Scenes/OfficeScene.unity";

    [MenuItem("HealthSim/Validate/Core Loop Scene Contract")]
    public static void ValidateCoreLoopSceneContract()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        int errors = 0;
        int warnings = 0;
        StringBuilder report = new StringBuilder();

        try
        {
            ValidateSampleScene(ref errors, ref warnings, report);
            ValidateOfficeScene(ref errors, ref warnings, report);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        string summary = $"[CoreLoopSceneValidator] Validation complete. Errors={errors}, Warnings={warnings}.";
        if (errors > 0)
            Debug.LogError(summary + "\n" + report);
        else if (warnings > 0)
            Debug.LogWarning(summary + "\n" + report);
        else
            Debug.Log(summary + " Scene contract checks passed.");
    }

    private static void ValidateSampleScene(ref int errors, ref int warnings, StringBuilder report)
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            errors++;
            report.AppendLine("- [SampleScene] Scene could not be opened.");
            return;
        }

        RequireObject("SampleScene", "GameManager", true, ref errors, ref warnings, report);
        RequireObject("SampleScene", "Directional Light", false, ref errors, ref warnings, report);
        RequireObject("SampleScene", "CM_TPP", false, ref errors, ref warnings, report);
        RequireObject("SampleScene", "CM_FPP", false, ref errors, ref warnings, report);
        RequireObject("SampleScene", "Interactable_FoodCube", false, ref errors, ref warnings, report);
        RequireObject("SampleScene", "Interactable_NPC", false, ref errors, ref warnings, report);
        RequireObject("SampleScene", "Interactable_NPC_Restoran", false, ref errors, ref warnings, report);

        GameObject gameManager = FindByName("GameManager");
        if (gameManager == null)
        {
            return;
        }

        RequireComponent<WorkSessionManager>("SampleScene", gameManager, false, ref errors, ref warnings, report);
        RequireComponent<FadeManager>("SampleScene", gameManager, false, ref errors, ref warnings, report);
        RequireComponent<ModalStateManager>("SampleScene", gameManager, false, ref errors, ref warnings, report);

        if (gameManager.GetComponent<ModalUIStateManager>() == null)
        {
            warnings++;
            report.AppendLine("- [SampleScene] ModalUIStateManager missing on GameManager (legacy compatibility path may be unavailable).");
        }
    }

    private static void ValidateOfficeScene(ref int errors, ref int warnings, StringBuilder report)
    {
        Scene scene = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            errors++;
            report.AppendLine("- [OfficeScene] Scene could not be opened.");
            return;
        }

        RequireObject("OfficeScene", "PlayerSpawnPoint", true, ref errors, ref warnings, report);
        RequireObject("OfficeScene", "Directional Light", false, ref errors, ref warnings, report);

        GameObject player = FindPlayerByTag();
        if (player == null)
        {
            warnings++;
            report.AppendLine("- [OfficeScene] Player tag not found. Runtime fallback can spawn a placeholder player.");
            return;
        }

        RequireComponent<Rigidbody>("OfficeScene", player, true, ref errors, ref warnings, report);
        RequireComponent<UniversalInteractionController>("OfficeScene", player, false, ref errors, ref warnings, report);
    }

    private static void RequireObject(string sceneName, string objectName, bool required, ref int errors, ref int warnings, StringBuilder report)
    {
        GameObject target = FindByName(objectName);
        if (target != null)
            return;

        if (required)
        {
            errors++;
            report.AppendLine($"- [{sceneName}] Missing required object: {objectName}");
        }
        else
        {
            warnings++;
            report.AppendLine($"- [{sceneName}] Missing optional object: {objectName}");
        }
    }

    private static void RequireComponent<T>(string sceneName, GameObject target, bool required, ref int errors, ref int warnings, StringBuilder report)
        where T : Component
    {
        if (target.GetComponent<T>() != null)
            return;

        string componentName = typeof(T).Name;
        if (required)
        {
            errors++;
            report.AppendLine($"- [{sceneName}] Missing required component {componentName} on {target.name}");
        }
        else
        {
            warnings++;
            report.AppendLine($"- [{sceneName}] Missing optional component {componentName} on {target.name}");
        }
    }

    private static GameObject FindByName(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject result = FindByNameRecursive(roots[i].transform, objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindByNameRecursive(Transform node, string objectName)
    {
        if (node.name == objectName)
            return node.gameObject;

        for (int i = 0; i < node.childCount; i++)
        {
            GameObject result = FindByNameRecursive(node.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static GameObject FindPlayerByTag()
    {
        try
        {
            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            if (taggedPlayers != null && taggedPlayers.Length > 0)
                return taggedPlayers[0];
        }
        catch (UnityException)
        {
            return null;
        }

        return null;
    }
}
#endif
