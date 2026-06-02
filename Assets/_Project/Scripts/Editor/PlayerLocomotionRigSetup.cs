#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public static class PlayerLocomotionRigSetup
{
    const string RigRootName = "Rig";
    const string LocomotionRigName = "Rig_Locomotion";
    const string TargetsRootName = "LocomotionRigTargets";
    const string UnityRigSetupMenu = "Animation Rigging/Rig Setup";
    const string UnityBoneRendererMenu = "Animation Rigging/Bone Renderer Setup";

    [MenuItem("HealthySim/Remove Player Foot IK Rig (Fix Console Errors)")]
    public static void RemovePlayerLocomotionRigMenu()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[PlayerLocomotionRigSetup] Player tagged GameObject not found.");
            return;
        }

        RemovePlayerLocomotionRigFromPlayer(player, log: true);
    }

    public static void RemovePlayerLocomotionRigFromPlayer(GameObject player, bool log = false)
    {
        if (player == null)
            return;

        string[] rigChildNames = { RigRootName, LocomotionRigName, "Rig 1", "Rig 2", TargetsRootName };
        for (int i = 0; i < rigChildNames.Length; i++)
        {
            Transform child = player.transform.Find(rigChildNames[i]);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        BoneRenderer[] boneRenderers = player.GetComponents<BoneRenderer>();
        for (int i = 0; i < boneRenderers.Length; i++)
            UnityEngine.Object.DestroyImmediate(boneRenderers[i]);

        RigBuilder rigBuilder = player.GetComponent<RigBuilder>();
        if (rigBuilder != null)
            UnityEngine.Object.DestroyImmediate(rigBuilder);

        PlayerLocomotionRig locomotionRig = player.GetComponent<PlayerLocomotionRig>();
        if (locomotionRig != null)
            UnityEngine.Object.DestroyImmediate(locomotionRig);

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);

        PlayerAnimationRiggingGuard guard = player.GetComponent<PlayerAnimationRiggingGuard>();
        if (guard == null)
            guard = player.AddComponent<PlayerAnimationRiggingGuard>();

        SerializedObject guardSo = new SerializedObject(guard);
        SerializedProperty suppress = guardSo.FindProperty("suppressFootIkRigging");
        if (suppress != null)
            suppress.boolValue = true;
        guardSo.ApplyModifiedPropertiesWithoutUndo();
        guard.ApplySuppression();

        if (log)
            Debug.Log("[PlayerLocomotionRigSetup] Removed foot IK / Animation Rigging from Player. Save scene.");
    }

    [MenuItem("HealthySim/Setup Player Locomotion Rig (Animation Rigging)")]
    public static void SetupPlayerLocomotionRig()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[PlayerLocomotionRigSetup] Player tagged GameObject not found.");
            return;
        }

        if (player.GetComponent<Animator>() == null)
        {
            Debug.LogError("[PlayerLocomotionRigSetup] Player needs an Animator on the root.");
            return;
        }

        Transform existingLocomotionRig = player.transform.Find(LocomotionRigName);
        if (existingLocomotionRig == null)
            RunUnityAnimationRiggingMenu(player, UnityRigSetupMenu);

        RunUnityAnimationRiggingMenu(player, UnityBoneRendererMenu);

        RigBuilder rigBuilder = player.GetComponent<RigBuilder>();
        if (rigBuilder == null)
            rigBuilder = player.AddComponent<RigBuilder>();

        PlayerLocomotionRig locomotionRig = player.GetComponent<PlayerLocomotionRig>();
        if (locomotionRig == null)
            locomotionRig = player.AddComponent<PlayerLocomotionRig>();

        GameObject targetsRoot = GetOrCreateChildObject(player.transform, TargetsRootName);
        Transform leftFootTarget = GetOrCreateChild(targetsRoot.transform, "LeftFootTarget");
        Transform rightFootTarget = GetOrCreateChild(targetsRoot.transform, "RightFootTarget");
        Transform leftKneeHint = GetOrCreateChild(targetsRoot.transform, "LeftKneeHint");
        Transform rightKneeHint = GetOrCreateChild(targetsRoot.transform, "RightKneeHint");

        GameObject locomotionRigRoot = GetOrCreateChildObject(player.transform, LocomotionRigName);
        Rig rig = locomotionRigRoot.GetComponent<Rig>();
        if (rig == null)
            rig = locomotionRigRoot.AddComponent<Rig>();
        rig.weight = 1f;

        TwoBoneIKConstraint leftLegIk = GetOrCreateIkConstraint(locomotionRigRoot.transform, "LeftLegIK");
        TwoBoneIKConstraint rightLegIk = GetOrCreateIkConstraint(locomotionRigRoot.transform, "RightLegIK");

        WireRigBuilderLayer(rigBuilder, rig);

        SerializedObject serializedRig = new SerializedObject(locomotionRig);
        SetReference(serializedRig, "locomotionRig", rig);
        SetReference(serializedRig, "leftLegIk", leftLegIk);
        SetReference(serializedRig, "rightLegIk", rightLegIk);
        SetReference(serializedRig, "leftFootTarget", leftFootTarget);
        SetReference(serializedRig, "rightFootTarget", rightFootTarget);
        SetReference(serializedRig, "leftKneeHint", leftKneeHint);
        SetReference(serializedRig, "rightKneeHint", rightKneeHint);
        serializedRig.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);

        SerializedObject enableIk = new SerializedObject(locomotionRig);
        SerializedProperty footIkEnabled = enableIk.FindProperty("footIkEnabled");
        if (footIkEnabled != null)
            footIkEnabled.boolValue = true;
        enableIk.ApplyModifiedPropertiesWithoutUndo();
        locomotionRig.enabled = true;

        locomotionRig.RefreshAfterModelSwap();
        rigBuilder.Build();

        PlayerAnimationRiggingGuard guard = player.GetComponent<PlayerAnimationRiggingGuard>();
        if (guard != null)
        {
            SerializedObject guardSo = new SerializedObject(guard);
            SerializedProperty suppress = guardSo.FindProperty("suppressFootIkRigging");
            if (suppress != null)
                suppress.boolValue = false;
            guardSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[PlayerLocomotionRigSetup] Unity Animation Rigging + foot IK wired on Player.");
    }

    [MenuItem("HealthySim/Reimport Body Models (Humanoid)")]
    public static void ReimportBodyModelsHumanoid()
    {
        CharacterModelSwapperSceneSetup.ConfigureNewCharacterFbxAsHumanoid();
        Debug.Log("[PlayerLocomotionRigSetup] Body model FBX reimported as Humanoid.");
    }

    static void RunUnityAnimationRiggingMenu(GameObject player, string menuPath)
    {
        GameObject previousSelection = Selection.activeGameObject;
        try
        {
            Selection.activeGameObject = player;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
                Debug.LogWarning($"[PlayerLocomotionRigSetup] Could not run menu '{menuPath}'. Run it manually from Animation Rigging menu.");
        }
        finally
        {
            Selection.activeGameObject = previousSelection;
        }
    }

    static GameObject GetOrCreateChildObject(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child.gameObject;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go;
    }

    static Transform GetOrCreateChild(Transform parent, string childName)
    {
        return GetOrCreateChildObject(parent, childName).transform;
    }

    static TwoBoneIKConstraint GetOrCreateIkConstraint(Transform rigRoot, string name)
    {
        Transform existing = rigRoot.Find(name);
        GameObject ikGo = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
            ikGo.transform.SetParent(rigRoot, false);

        TwoBoneIKConstraint ik = ikGo.GetComponent<TwoBoneIKConstraint>();
        if (ik == null)
            ik = ikGo.AddComponent<TwoBoneIKConstraint>();

        ik.weight = 1f;
        return ik;
    }

    static void WireRigBuilderLayer(RigBuilder rigBuilder, Rig rig)
    {
        SerializedObject serializedBuilder = new SerializedObject(rigBuilder);
        SerializedProperty layers = serializedBuilder.FindProperty("m_RigLayers");
        if (layers == null)
        {
            Debug.LogError("[PlayerLocomotionRigSetup] RigBuilder has no m_RigLayers property.");
            return;
        }

        int index = -1;
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty rigProp = layers.GetArrayElementAtIndex(i).FindPropertyRelative("m_Rig");
            if (rigProp.objectReferenceValue == rig)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
        }

        SerializedProperty layer = layers.GetArrayElementAtIndex(index);
        layer.FindPropertyRelative("m_Rig").objectReferenceValue = rig;
        layer.FindPropertyRelative("m_Active").boolValue = true;

        serializedBuilder.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetReference(SerializedObject obj, string propertyName, Object value)
    {
        SerializedProperty prop = obj.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }
}
#endif
