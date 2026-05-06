#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GymFeatureSceneSetup
{
    private const string GymScenePath = "Assets/Scenes/GymScene.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("HealthSim/Setup Gym Feature Scenes")]
    public static void SetupGymFeatureScenes()
    {
        SetupGymScene();
        SetupSampleSceneGymDoor();
        EnsureBuildSettingsIncludesGymScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GymFeatureSceneSetup] GymScene and GymDoor setup completed.");
    }

    private static void SetupGymScene()
    {
        Scene gymScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        gymScene.name = "GymScene";

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "GymFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(4f, 1f, 4f);

        CreateCube("BackWall", new Vector3(0f, 2f, -4f), new Vector3(9f, 4f, 0.2f));
        CreateCube("LeftWall", new Vector3(-4.5f, 2f, 0f), new Vector3(0.2f, 4f, 8f));
        CreateCube("RightWall", new Vector3(4.5f, 2f, 0f), new Vector3(0.2f, 4f, 8f));

        CreateCube("BenchPress", new Vector3(-1.2f, 0.55f, -1.6f), new Vector3(1.8f, 0.25f, 0.6f));
        CreateCube("DumbbellRack", new Vector3(1.6f, 0.8f, -2f), new Vector3(1.3f, 1.1f, 0.5f));
        CreateCube("TrainingMat", new Vector3(0f, 0.04f, 1.6f), new Vector3(2.2f, 0.06f, 1.2f));

        EnsureTagExists("NPCTrainer");
        GameObject trainerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        trainerObj.name = "NPCTrainer";
        trainerObj.tag = "NPCTrainer";
        trainerObj.transform.position = new Vector3(0f, 1f, -1f);
        NpcDialogueInteractable trainerInteractable = trainerObj.GetComponent<NpcDialogueInteractable>();
        if (trainerInteractable == null)
            trainerInteractable = trainerObj.AddComponent<NpcDialogueInteractable>();

        GameObject spawn = new GameObject("PlayerSpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.1f, 3f);
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        EnsureTagExists("Player");
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = spawn.transform.position;
        player.transform.rotation = spawn.transform.rotation;

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null)
            playerRb = player.AddComponent<Rigidbody>();
        playerRb.constraints = RigidbodyConstraints.FreezeRotation;

        if (player.GetComponent<UniversalInteractionController>() == null)
            player.AddComponent<UniversalInteractionController>();

        GameObject managers = new GameObject("GymSceneManagers");
        if (managers.GetComponent<GymProgressionSystem>() == null)
            managers.AddComponent<GymProgressionSystem>();
        if (managers.GetComponent<FadeManager>() == null)
            managers.AddComponent<FadeManager>();
        if (managers.GetComponent<ModalStateManager>() == null)
            managers.AddComponent<ModalStateManager>();

        NpcDialogueMenuController dialogueMenu = managers.GetComponent<NpcDialogueMenuController>();
        if (dialogueMenu == null)
            dialogueMenu = managers.AddComponent<NpcDialogueMenuController>();

        GameObject trainerDialogueObj = new GameObject("GymTrainerDialogueController");
        GymTrainerDialogueController trainerDialogue = trainerDialogueObj.AddComponent<GymTrainerDialogueController>();

        GameObject clockObj = new GameObject("ClockAnimationUI");
        ClockAnimationUI clockUi = clockObj.AddComponent<ClockAnimationUI>();

        GameObject sessionControllerObj = new GameObject("GymSessionController");
        GymSessionController sessionController = sessionControllerObj.AddComponent<GymSessionController>();

        SerializedObject controllerSo = new SerializedObject(sessionController);
        controllerSo.FindProperty("clockUI").objectReferenceValue = clockUi;
        controllerSo.FindProperty("dialogueUI").objectReferenceValue = dialogueMenu;
        controllerSo.FindProperty("trainerDialogueController").objectReferenceValue = trainerDialogue;
        controllerSo.FindProperty("mainSceneName").stringValue = "SampleScene";
        controllerSo.FindProperty("trainerInteractableOverride").objectReferenceValue = trainerInteractable;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0f, 3.2f, 6.2f);
        camObj.transform.rotation = Quaternion.Euler(18f, 180f, 0f);
        _ = cam;

        GameObject dirLightObj = new GameObject("Directional Light");
        Light dirLight = dirLightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 1f;
        dirLightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.SaveScene(gymScene, GymScenePath);
    }

    private static void SetupSampleSceneGymDoor()
    {
        Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("GymDoor");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject root = new GameObject("GymDoor");
        Vector3 fallbackPosition = new Vector3(-41f, 0f, 42f);

        GameObject kantorDoor = GameObject.Find("KantorDoor");
        if (kantorDoor != null)
        {
            root.transform.position = kantorDoor.transform.position + new Vector3(4f, 0f, 0f);
            root.transform.rotation = kantorDoor.transform.rotation;
        }
        else
        {
            root.transform.position = fallbackPosition;
        }

        CreateChildCube(root.transform, "FrameLeft", new Vector3(-0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f));
        CreateChildCube(root.transform, "FrameRight", new Vector3(0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f));
        CreateChildCube(root.transform, "FrameTop", new Vector3(0f, 3.1f, 0f), new Vector3(1.4f, 0.2f, 0.2f));
        CreateChildCube(root.transform, "DoorPanel", new Vector3(0f, 1.4f, 0f), new Vector3(1.0f, 2.8f, 0.1f));

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.5f, 0f);
        col.size = new Vector3(1.5f, 3.3f, 0.6f);

        GymDoorInteractable gymDoor = root.AddComponent<GymDoorInteractable>();
        SerializedObject doorSo = new SerializedObject(gymDoor);
        doorSo.FindProperty("gymSceneName").stringValue = "GymScene";
        doorSo.FindProperty("promptText").stringValue = "Masuk Gym";
        doorSo.ApplyModifiedPropertiesWithoutUndo();

        EnsureTagExists("Interactable");
        root.tag = "Interactable";

        EditorSceneManager.MarkSceneDirty(sampleScene);
        EditorSceneManager.SaveScene(sampleScene, SampleScenePath);
    }

    private static void EnsureBuildSettingsIncludesGymScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        bool foundSample = false;
        bool foundGym = false;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == SampleScenePath)
                foundSample = true;
            if (scenes[i].path == GymScenePath)
                foundGym = true;
        }

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
        if (!foundSample)
            list.Add(new EditorBuildSettingsScene(SampleScenePath, true));
        if (!foundGym)
            list.Add(new EditorBuildSettingsScene(GymScenePath, true));

        EditorBuildSettings.scenes = list.ToArray();
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        return obj;
    }

    private static GameObject CreateChildCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = localScale;
        return obj;
    }

    private static void EnsureTagExists(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty existingTag = tagsProp.GetArrayElementAtIndex(i);
            if (existingTag.stringValue == tag)
                return;
        }

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
#endif
