#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorkFeatureSceneSetup
{
    private const string OfficeScenePath = "Assets/Scenes/OfficeScene.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("HealthSim/Setup Work Feature Scenes")]
    public static void SetupWorkFeatureScenes()
    {
        DialogueAssetGenerator.GenerateBossDialogue();
        SetupOfficeScene();
        SetupSampleSceneDoor();
        EnsureBuildSettingsIncludesOfficeScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WorkFeatureSceneSetup] OfficeScene and KantorDoor setup completed.");
    }

    [InitializeOnLoadMethod]
    private static void AutoSetupIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OfficeScenePath) == null)
                SetupWorkFeatureScenes();
        };
    }

    private static void SetupOfficeScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "OfficeScene";

        Material floorMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Floor.mat", new Color(0.55f, 0.55f, 0.55f));
        Material wallMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Wall.mat", new Color(0.91f, 0.87f, 0.78f));
        Material whiteMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_White.mat", new Color(0.98f, 0.98f, 0.98f));
        Material brownMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Brown.mat", new Color(0.45f, 0.28f, 0.15f));
        Material darkGrayMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_DarkGray.mat", new Color(0.2f, 0.2f, 0.2f));
        Material blueMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Blue.mat", new Color(0.2f, 0.38f, 0.78f));

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "OfficeFloor";
        floor.transform.position = new Vector3(0f, 0f, 0f);
        floor.transform.localScale = new Vector3(5f, 1f, 4f);
        ApplyMaterial(floor, floorMat);

        GameObject backWall = CreateCube("BackWall", new Vector3(0f, 2f, -4f), new Vector3(10f, 4f, 0.2f), wallMat);
        GameObject leftWall = CreateCube("LeftWall", new Vector3(-5f, 2f, 0f), new Vector3(0.2f, 4f, 8f), wallMat);
        GameObject rightWall = CreateCube("RightWall", new Vector3(5f, 2f, 0f), new Vector3(0.2f, 4f, 8f), wallMat);
        _ = backWall;
        _ = leftWall;
        _ = rightWall;

        GameObject ceiling = CreateCube("Ceiling", new Vector3(0f, 4f, 0f), new Vector3(10f, 0.2f, 8f), whiteMat);
        _ = ceiling;

        GameObject desk = CreateCube("Desk", new Vector3(0f, 0.75f, -2f), new Vector3(2f, 0.1f, 1f), brownMat);
        GameObject chair = CreateCube("Chair", new Vector3(0f, 0.5f, -1f), new Vector3(0.6f, 0.1f, 0.6f), wallMat);
        GameObject monitor = CreateCube("Monitor", new Vector3(0f, 1.1f, -2.3f), new Vector3(0.8f, 0.6f, 0.05f), darkGrayMat);
        GameObject sideTableLeft = CreateCube("SideTableLeft", new Vector3(-1.6f, 0.55f, -1.8f), new Vector3(0.6f, 0.7f, 0.6f), brownMat);
        GameObject sideTableRight = CreateCube("SideTableRight", new Vector3(1.6f, 0.55f, -1.8f), new Vector3(0.6f, 0.7f, 0.6f), brownMat);
        _ = desk;
        _ = chair;
        _ = monitor;
        _ = sideTableLeft;
        _ = sideTableRight;

        GameObject boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.name = "NPCBoss";
        boss.transform.position = new Vector3(0f, 1f, -2.8f);
        boss.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        ApplyMaterial(boss, blueMat);
        EnsureTagExists("NPCBoss");
        boss.tag = "NPCBoss";
        if (boss.GetComponent<NpcDialogueInteractable>() == null)
            boss.AddComponent<NpcDialogueInteractable>();
        NpcDialogueInteractable bossInteractable = boss.GetComponent<NpcDialogueInteractable>();

        GameObject spawn = new GameObject("PlayerSpawnPoint");
        spawn.transform.position = new Vector3(0f, 0.1f, 2f);
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        GameObject officePlayer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        officePlayer.name = "Player";
        officePlayer.transform.position = spawn.transform.position;
        officePlayer.transform.rotation = spawn.transform.rotation;
        EnsureTagExists("Player");
        officePlayer.tag = "Player";

        Rigidbody playerRb = officePlayer.GetComponent<Rigidbody>();
        if (playerRb == null)
            playerRb = officePlayer.AddComponent<Rigidbody>();
        playerRb.constraints = RigidbodyConstraints.FreezeRotation;

        if (officePlayer.GetComponent<UniversalInteractionController>() == null)
            officePlayer.AddComponent<UniversalInteractionController>();

        GameObject manager = new GameObject("OfficeSceneManagers");
        if (manager.GetComponent<WorkSessionManager>() == null)
            manager.AddComponent<WorkSessionManager>();
        if (manager.GetComponent<FadeManager>() == null)
            manager.AddComponent<FadeManager>();
        if (manager.GetComponent<DialogueCatalogProvider>() == null)
            manager.AddComponent<DialogueCatalogProvider>();
        if (manager.GetComponent<NpcDialogueMenuController>() == null)
            manager.AddComponent<NpcDialogueMenuController>();

        OfficeBossDialogueController existingBossController = manager.GetComponent<OfficeBossDialogueController>();
        if (existingBossController != null)
            Object.DestroyImmediate(existingBossController);

        DialogueGraphData preWork = AssetDatabase.LoadAssetAtPath<DialogueGraphData>("Assets/_Project/Data/Dialogues/NPCBoss_PreWork.asset");
        DialogueGraphData postWork = AssetDatabase.LoadAssetAtPath<DialogueGraphData>("Assets/_Project/Data/Dialogues/NPCBoss_PostWork.asset");

        if (bossInteractable != null && preWork != null)
            bossInteractable.SetDialogueOptions(new System.Collections.Generic.List<DialogueGraphData> { preWork }, false);

        GameObject clockObj = GameObject.Find("ClockAnimationUI");
        if (clockObj == null)
            clockObj = new GameObject("ClockAnimationUI");
        ClockAnimationUI clockUi = clockObj.GetComponent<ClockAnimationUI>();
        if (clockUi == null)
            clockUi = clockObj.AddComponent<ClockAnimationUI>();

        GameObject workControllerObj = GameObject.Find("WorkSessionController");
        if (workControllerObj == null)
            workControllerObj = new GameObject("WorkSessionController");

        WorkSessionController workController = workControllerObj.GetComponent<WorkSessionController>();
        if (workController == null)
            workController = workControllerObj.AddComponent<WorkSessionController>();

        SerializedObject so = new SerializedObject(workController);
        so.FindProperty("_clockUI").objectReferenceValue = clockUi;
        so.FindProperty("_dialogueUI").objectReferenceValue = manager.GetComponent<NpcDialogueMenuController>();
        so.FindProperty("_mainSceneName").stringValue = "SampleScene";
        so.FindProperty("_postWorkDialogueId").stringValue = "npc_boss_post";
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject camObj = new GameObject("OfficeCamera");
        Camera cam = camObj.AddComponent<Camera>();
        camObj.transform.position = new Vector3(0f, 2f, 4f);
        camObj.transform.rotation = Quaternion.Euler(15f, 180f, 0f);
        _ = cam;

        GameObject dirLightObj = new GameObject("Directional Light");
        Light dirLight = dirLightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 1f;
        dirLight.color = new Color(1f, 0.96f, 0.9f);
        dirLightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject pointLightObj = new GameObject("DeskPointLight");
        Light point = pointLightObj.AddComponent<Light>();
        point.type = LightType.Point;
        point.intensity = 1.5f;
        point.range = 5f;
        point.color = new Color(1f, 0.95f, 0.86f);
        pointLightObj.transform.position = new Vector3(0f, 2.7f, -2f);

        EditorSceneManager.SaveScene(scene, OfficeScenePath);
    }

    private static void SetupSampleSceneDoor()
    {
        Scene sample = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("KantorDoor");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject root = new GameObject("KantorDoor");
        root.transform.position = new Vector3(-45f, 0f, 42f);
        root.tag = "Untagged";

        Material brownMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Brown.mat", new Color(0.45f, 0.28f, 0.15f));
        Material frameMat = CreateOrUpdateUrpLitMaterial("Assets/_Project/Materials/WorkOffice_Frame.mat", new Color(0.85f, 0.83f, 0.78f));

        CreateChildCube(root.transform, "FrameLeft", new Vector3(-0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f), frameMat);
        CreateChildCube(root.transform, "FrameRight", new Vector3(0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 0.2f), frameMat);
        CreateChildCube(root.transform, "FrameTop", new Vector3(0f, 3.1f, 0f), new Vector3(1.4f, 0.2f, 0.2f), frameMat);
        CreateChildCube(root.transform, "DoorPanel", new Vector3(0f, 1.4f, 0f), new Vector3(1.0f, 2.8f, 0.1f), brownMat);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.5f, 0f);
        col.size = new Vector3(1.5f, 3.3f, 0.6f);

        if (root.GetComponent<WorkDoorInteractable>() == null)
            root.AddComponent<WorkDoorInteractable>();

        EnsureTagExists("Interactable");
        root.tag = "Interactable";

        EditorSceneManager.MarkSceneDirty(sample);
        EditorSceneManager.SaveScene(sample, SampleScenePath);
    }

    private static void EnsureBuildSettingsIncludesOfficeScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        bool foundOffice = false;
        bool foundSample = false;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == OfficeScenePath)
                foundOffice = true;
            if (scenes[i].path == SampleScenePath)
                foundSample = true;
        }

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
        if (!foundSample)
            list.Add(new EditorBuildSettingsScene(SampleScenePath, true));
        if (!foundOffice)
            list.Add(new EditorBuildSettingsScene(OfficeScenePath, true));

        EditorBuildSettings.scenes = list.ToArray();
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        ApplyMaterial(obj, material);
        return obj;
    }

    private static GameObject CreateChildCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = localScale;
        ApplyMaterial(obj, material);
        return obj;
    }

    private static void ApplyMaterial(GameObject obj, Material material)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null && material != null)
            r.sharedMaterial = material;
    }

    private static Material CreateOrUpdateUrpLitMaterial(string path, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[WorkFeatureSceneSetup] URP/Lit shader not found. Material creation skipped.");
            return null;
        }

        Material mat = new Material(shader);
        mat.name = System.IO.Path.GetFileNameWithoutExtension(path);
        mat.color = color;
        return mat;
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
