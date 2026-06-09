#if UNITY_EDITOR

using System;

using System.Text;

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;

using UnityEngine.SceneManagement;



/// <summary>

/// Wires SampleScene for designer-friendly setup: manager host, bootstrap, Main Camera audio.

/// Open SampleScene, run menu, save scene (Ctrl+S).

/// </summary>

public static class SampleSceneCoreSetup

{

    static readonly Type[] ManagerComponentTypes =

    {

        typeof(SessionSeedManager),

        typeof(PlayerActionTracker),

        typeof(StoryIntroManager),

        typeof(StoryManager),

        typeof(IntroCutsceneController),

        typeof(SessionFlowController),

        typeof(FoodChoiceMenuController),

        typeof(FoodStashMenuController),

        typeof(NpcDialogueMenuController),

        typeof(FoodCatalogProvider),

        typeof(DialogueCatalogProvider),

        typeof(SessionFoodStash),

        typeof(ModalStateManager),

        typeof(WorkReminderUI),

        typeof(WorkSessionManager),

        typeof(FadeManager),

        typeof(MobileInputController),

        typeof(BazaarManager),

        typeof(EndingManager),

    };



    [MenuItem("HealthySim/Setup SampleScene Core")]

    public static void SetupSampleSceneCore()

    {

        Scene scene = SceneManager.GetActiveScene();

        if (!string.Equals(scene.name, "SampleScene", StringComparison.Ordinal))

        {

            Debug.LogWarning("[HealthySim] Open SampleScene first, then run HealthySim/Setup SampleScene Core.");

            return;

        }



        GameObject managerHost = FindManagerHost();

        if (managerHost == null)

        {

            managerHost = new GameObject("GameManager");

            Undo.RegisterCreatedObjectUndo(managerHost, "Create GameManager");

            Debug.LogWarning("[HealthySim] MobileInputController not found — created empty GameManager root.");

        }



        int addedComponents = EnsureManagerComponents(managerHost);

        int addedBootstrap = EnsureSceneBootstrap(managerHost);

        int addedKeeper = EnsureGameplayAudioListenerKeeper(managerHost);

        int addedAudioGuards = EnsureMainCameraAudioGuards();

        EnsureIntroDefaults(managerHost);



        EditorSceneManager.MarkSceneDirty(scene);



        var report = new StringBuilder();

        report.AppendLine("[HealthySim] SampleScene core setup complete.");

        report.AppendLine($"Manager host: '{managerHost.name}'");

        report.AppendLine($"Components added to host: {addedComponents}");

        report.AppendLine($"Bootstrap added: {(addedBootstrap > 0 ? "yes" : "already present")}");

        report.AppendLine($"Audio listener keeper added: {(addedKeeper > 0 ? "yes" : "already present")}");

        report.AppendLine($"Main Camera audio guards: {addedAudioGuards}");

        report.AppendLine("Save scene (Ctrl+S). Runtime GameManager spawn should no longer occur.");

        Debug.Log(report.ToString());

    }



    [MenuItem("HealthySim/Setup SampleScene Core", true)]

    public static bool ValidateSetupSampleSceneCore()

    {

        return SceneManager.GetActiveScene().name == "SampleScene";

    }



    static GameObject FindManagerHost()

    {

        MobileInputController mobileInput = UnityEngine.Object.FindFirstObjectByType<MobileInputController>(FindObjectsInactive.Include);

        if (mobileInput != null)

            return mobileInput.gameObject;



        GameObject gameManager = GameObject.Find("GameManager");

        return gameManager;

    }



    static int EnsureManagerComponents(GameObject host)

    {

        int added = 0;

        for (int i = 0; i < ManagerComponentTypes.Length; i++)

        {

            Type type = ManagerComponentTypes[i];

            if (host.GetComponent(type) != null)

                continue;



            Undo.AddComponent(host, type);

            added++;

        }



        return added;

    }



    static void EnsureIntroDefaults(GameObject host)
    {
        SampleSceneBootstrap bootstrap = host.GetComponent<SampleSceneBootstrap>();
        if (bootstrap != null)
        {
            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            SerializedProperty forceIntro = serializedBootstrap.FindProperty("forceIntroEveryPlay");
            if (forceIntro != null)
            {
                forceIntro.boolValue = true;
                serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
            }
        }

        StoryIntroManager intro = host.GetComponent<StoryIntroManager>();
        if (intro != null)
        {
            SerializedObject serializedIntro = new SerializedObject(intro);
            SerializedProperty forcePlay = serializedIntro.FindProperty("forcePlayIntro");
            if (forcePlay != null)
            {
                forcePlay.boolValue = true;
                serializedIntro.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(intro);
            }
        }
    }

    static int EnsureGameplayAudioListenerKeeper(GameObject host)
    {
        if (host.GetComponent<GameplayAudioListenerKeeper>() != null)
            return 0;

        Undo.AddComponent(host, typeof(GameplayAudioListenerKeeper));
        return 1;
    }

    static int EnsureSceneBootstrap(GameObject host)

    {

        SampleSceneBootstrap existing = UnityEngine.Object.FindFirstObjectByType<SampleSceneBootstrap>(FindObjectsInactive.Include);

        if (existing != null)

            return 0;



        Undo.AddComponent(host, typeof(SampleSceneBootstrap));

        return 1;

    }



    static int EnsureMainCameraAudioGuards()

    {

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int added = 0;



        for (int i = 0; i < cameras.Length; i++)

        {

            Camera camera = cameras[i];

            if (camera == null || !string.Equals(camera.gameObject.name, "Main Camera", StringComparison.Ordinal))

                continue;



            if (camera.GetComponent<SceneAudioListenerGuard>() != null)

                continue;



            Undo.AddComponent(camera.gameObject, typeof(SceneAudioListenerGuard));

            added++;

        }



        return added;

    }

}

#endif


