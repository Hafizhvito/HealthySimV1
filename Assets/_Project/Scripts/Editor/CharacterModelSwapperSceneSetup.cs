#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CharacterModelSwapperSceneSetup
{
    const string EditorPrefsBodySourceKey = "HealthySim.ActiveBodyModelSource";

    public enum BodyModelSource
    {
        LegacyFM = 0,
        FemaleMaleNew = 1
    }

    const string FemaleRoot = "Assets/_Project/Data/Characters/Female";
    const string MaleRoot = "Assets/_Project/Data/Characters/Male";
    const string FemaleNewRoot = "Assets/_Project/Data/Characters/Female (New)";
    const string MaleNewRoot = "Assets/_Project/Data/Characters/Male (New)";

    static readonly Vector3 LegacyBodyScale = Vector3.one;
    static readonly Vector3 NewBodyScale = new Vector3(2f, 2f, 2f);

    static readonly (string fieldName, string assetPath, string sceneChildName, string legacyChildName)[] LegacyModelDefinitions =
    {
        ("femaleKurus", $"{FemaleRoot}/F_Kurus/F_Kurus.fbx", "F_Kurus", "Female_Kurus"),
        ("femaleIdeal", $"{FemaleRoot}/F_Ideal/F_Ideal.fbx", "F_Ideal", "Female_Ideal"),
        ("femaleOverweight", $"{FemaleRoot}/F_Overweight/F_Overweight.fbx", "F_Overweight", "Female_Gemuk"),
        ("maleKurus", $"{MaleRoot}/M_Kurus/Karakter_kurus.fbx", "M_Kurus", "Karakter_kurus"),
        ("maleIdeal", $"{MaleRoot}/M_Ideal/Karakter_Ideal.fbx", "M_Ideal", "Karakter_Ideal"),
        ("maleOverweight", $"{MaleRoot}/M_Overweight/Karakter_Overweight.fbx", "M_Overweight", "Karakter_Overweight")
    };

    static readonly (string fieldName, string assetPath, string sceneChildName, string legacyChildName)[] NewModelDefinitions =
    {
        ("femaleKurus", $"{FemaleNewRoot}/Female_Kurus/Female_Kurus.fbx", "Female_Kurus", "F_Kurus"),
        ("femaleIdeal", $"{FemaleNewRoot}/Female_Ideal/Female_Ideal.fbx", "Female_Ideal", "F_Ideal"),
        ("femaleOverweight", $"{FemaleNewRoot}/Female_Gemuk/Female_Gemuk.fbx", "Female_Gemuk", "F_Overweight"),
        ("maleKurus", $"{MaleNewRoot}/Male_Kurus/tripo_convert_a3778048-aa33-4fc2-89dc-3044d1481d21.fbx", "Male_Kurus", "M_Kurus"),
        ("maleIdeal", $"{MaleNewRoot}/Male_Ideal/tripo_convert_d9912af0-b403-402a-8806-26116632ca8f.fbx", "Male_Ideal", "M_Ideal"),
        ("maleOverweight", $"{MaleNewRoot}/Male_Gemuk/tripo_convert_0b49d5f9-ff77-4488-a1fe-e81889cd8ce7.fbx", "Male_Gemuk", "M_Overweight")
    };

    static BodyModelSource activeBodyModelSource = LoadBodyModelSource();

    static (string fieldName, string assetPath, string sceneChildName, string legacyChildName)[] ActiveModelDefinitions =>
        activeBodyModelSource == BodyModelSource.FemaleMaleNew ? NewModelDefinitions : LegacyModelDefinitions;

    static Vector3 ActiveBodyScale =>
        activeBodyModelSource == BodyModelSource.FemaleMaleNew ? NewBodyScale : LegacyBodyScale;

    public static int ModelDefinitionCount => ActiveModelDefinitions.Length;

    public static void GetModelDefinition(int index, out string fieldName, out string assetPath, out string sceneChildName, out string legacyChildName)
    {
        (fieldName, assetPath, sceneChildName, legacyChildName) = ActiveModelDefinitions[index];
    }

    static BodyModelSource LoadBodyModelSource()
    {
        return (BodyModelSource)EditorPrefs.GetInt(EditorPrefsBodySourceKey, (int)BodyModelSource.LegacyFM);
    }

    static void SaveBodyModelSource(BodyModelSource source)
    {
        activeBodyModelSource = source;
        EditorPrefs.SetInt(EditorPrefsBodySourceKey, (int)source);
    }

    [MenuItem("HealthySim/Body Models/Setup Legacy F-M + Animation Pack", false, 10)]
    public static void SetupLegacyBodyModelsMenu()
    {
        SaveBodyModelSource(BodyModelSource.LegacyFM);
        RunBodyModelSetupPipeline("Legacy F/M");
    }

    [MenuItem("HealthySim/Body Models/Setup Female-Male (New) + Animation Pack", false, 11)]
    public static void SetupNewBodyModelsMenu()
    {
        SaveBodyModelSource(BodyModelSource.FemaleMaleNew);
        RunBodyModelSetupPipeline("Female/Male (New)");
    }

    [MenuItem("HealthySim/Body Models/Setup Legacy F-M + Animation Pack", true)]
    public static bool ValidateSetupLegacyBodyModelsMenu()
    {
        Menu.SetChecked("HealthySim/Body Models/Setup Legacy F-M + Animation Pack",
            activeBodyModelSource == BodyModelSource.LegacyFM);
        return true;
    }

    [MenuItem("HealthySim/Body Models/Setup Female-Male (New) + Animation Pack", true)]
    public static bool ValidateSetupNewBodyModelsMenu()
    {
        Menu.SetChecked("HealthySim/Body Models/Setup Female-Male (New) + Animation Pack",
            activeBodyModelSource == BodyModelSource.FemaleMaleNew);
        return true;
    }

    static void RunBodyModelSetupPipeline(string label)
    {
        ConfigureNewCharacterFbxAsHumanoid();
        CreateMixamoAvatarReferenceAsset();

        if (!RebuildPlayerAnimatorOverrides())
            return;

        SetupPlayerBodyModelsInternal();
        Debug.Log(
            $"[CharacterModelSwapperSetup] {label} body models wired (scale {ActiveBodyScale}) + Animation Pack locomotion. Save scene.");
    }

    static readonly string[] LegacyBodyModelChildNames =
    {
        "F_Kurus", "F_Ideal", "F_Overweight",
        "M_Kurus", "M_Ideal", "M_Overweight",
        "Karakter_kurus", "Karakter_Ideal", "Karakter_Overweight",
        "Female_Kurus", "Female_Ideal", "Female_Gemuk",
        "Male_Kurus", "Male_Ideal", "Male_Gemuk",
        "tripo_convert_a3778048-aa33-4fc2-89dc-3044d1481d21",
        "tripo_convert_d9912af0-b403-402a-8806-26116632ca8f",
        "tripo_convert_0b49d5f9-ff77-4488-a1fe-e81889cd8ce7",
        "tripo_convert_575d0011-d285-4ed1-9978-ff301e20f578",
        "tripo_convert_b71fd1fb-c82e-4400-b412-e6ace5b648d6",
        "tripo_convert_355a8c8a-7c5b-4872-8965-b4ce855a4050",
        "tripo_convert_7b2a9a92-7214-41bb-9b92-2685093ccc33",
        "tripo_convert_c1f30b77-5183-4bf2-8752-1787c22e6df1",
        "tripo_convert_5f4461ea-85f3-4dc2-8500-c0a9bada8431"
    };

    const string BaseControllerPath = "Assets/_Project/Art/Animations/PlayerAnimator.controller";
    const string MaleControllerPath = "Assets/_Project/Resources/PlayerAnimator_Male.overrideController";
    const string FemaleControllerPath = "Assets/_Project/Resources/PlayerAnimator_Female.overrideController";

    const string MaleIdlesFolder = "Assets/Animation Pack/Human Animations/Animations/Male/Idles";
    const string MaleWalkFolder = "Assets/Animation Pack/Human Animations/Animations/Male/Movement/Walk";
    const string MaleRunFolder = "Assets/Animation Pack/Human Animations/Animations/Male/Movement/Run";
    const string MaleJumpFolder = "Assets/Animation Pack/Human Animations/Animations/Male/Movement/Jump";

    const string FemaleIdlesFolder = "Assets/Animation Pack/Human Animations/Animations/Female/Idles";
    const string FemaleWalkFolder = "Assets/Animation Pack/Human Animations/Animations/Female/Movement/Walk";
    const string FemaleRunFolder = "Assets/Animation Pack/Human Animations/Animations/Female/Movement/Run";
    const string FemaleJumpFolder = "Assets/Animation Pack/Human Animations/Animations/Female/Movement/Jump";

    const string MixamoIdleFbx = "Assets/_Project/Art/Animations/Animation for character/Idle.fbx";
    const string MixamoWalkFbx = "Assets/_Project/Art/Animations/Animation for character/Walking.fbx";
    const string MixamoRunFbx = "Assets/_Project/Art/Animations/Animation for character/Running.fbx";

    static readonly string[] MixamoAnimationFbxPaths =
    {
        MixamoIdleFbx,
        MixamoWalkFbx,
        MixamoRunFbx
    };

    [MenuItem("HealthySim/Configure Mixamo Animation FBX (Humanoid)")]
    public static void ConfigureMixamoAnimationsAsHumanoid()
    {
        int configured = 0;
        for (int i = 0; i < MixamoAnimationFbxPaths.Length; i++)
        {
            string assetPath = MixamoAnimationFbxPaths[i];
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterModelSwapperSetup] Missing Mixamo FBX: {assetPath}");
                continue;
            }

            bool needsReimport =
                importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clipAnimations = importer.defaultClipAnimations;
            if (clipAnimations != null && clipAnimations.Length > 0)
            {
                for (int c = 0; c < clipAnimations.Length; c++)
                {
                    clipAnimations[c].loopTime = true;
                    clipAnimations[c].loopPose = true;
                }

                importer.clipAnimations = clipAnimations;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                configured++;
            }
        }

        Debug.Log($"[CharacterModelSwapperSetup] Mixamo Humanoid import configured ({configured} reimported).");
    }

    [MenuItem("HealthySim/Debug Mixamo Animation Clips")]
    public static void DebugMixamoAnimationClips()
    {
        LogMixamoClip(MixamoIdleFbx, "Idle");
        LogMixamoClip(MixamoWalkFbx, "Walk");
        LogMixamoClip(MixamoRunFbx, "Run");
    }

    static void LogMixamoClip(string fbxPath, string label)
    {
        AnimationClip clip = GetFirstAnimationClipFromFbx(fbxPath);
        if (clip == null)
            Debug.LogWarning($"[CharacterModelSwapperSetup] {label}: no clip in {fbxPath}");
        else
            Debug.Log($"[CharacterModelSwapperSetup] {label}: '{clip.name}' length={clip.length:0.00}s from {fbxPath}");
    }

    [MenuItem("HealthySim/Configure New Character FBX (Humanoid)")]
    public static void ConfigureNewCharacterFbxAsHumanoid()
    {
        int configured = 0;
        for (int i = 0; i < ActiveModelDefinitions.Length; i++)
        {
            string assetPath = ActiveModelDefinitions[i].assetPath;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterModelSwapperSetup] Missing FBX: {assetPath}");
                continue;
            }

            bool needsReimport =
                importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            if (needsReimport)
            {
                importer.SaveAndReimport();
                configured++;
            }
        }

        Debug.Log($"[CharacterModelSwapperSetup] Humanoid import configured ({configured} reimported).");
    }

    public static void ConfigureBodyModelsCopyMixamoAvatar()
    {
        Avatar sourceAvatar = GetHumanoidAvatarFromFbx(MixamoIdleFbx);
        if (sourceAvatar == null)
        {
            Debug.LogError("[CharacterModelSwapperSetup] Mixamo Idle avatar missing. Run Configure Mixamo Animation FBX first.");
            return;
        }

        int configured = 0;
        for (int i = 0; i < ActiveModelDefinitions.Length; i++)
        {
            string assetPath = ActiveModelDefinitions[i].assetPath;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            configured++;
        }

        Debug.Log($"[CharacterModelSwapperSetup] Copied Mixamo avatar to {configured} body FBX.");
    }

    public static Avatar GetHumanoidAvatarFromFbxPublic(string assetPath) => GetHumanoidAvatarFromFbx(assetPath);

    public static bool HasSkinnedMeshInAsset(GameObject prefabRoot)
    {
        if (prefabRoot == null)
            return false;

        return prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
    }

    [MenuItem("HealthySim/Full Rebuild Player Character System (uses last Body Models choice)")]
    public static void FullRebuildPlayerCharacterSystem()
    {
        activeBodyModelSource = LoadBodyModelSource();
        RunBodyModelSetupPipeline(activeBodyModelSource == BodyModelSource.FemaleMaleNew
            ? "Female/Male (New)"
            : "Legacy F/M");
    }

    [MenuItem("HealthySim/Setup Player Body Models (uses last Body Models choice)")]
    public static void SetupPlayerBodyModels()
    {
        activeBodyModelSource = LoadBodyModelSource();
        RunBodyModelSetupPipeline(activeBodyModelSource == BodyModelSource.FemaleMaleNew
            ? "Female/Male (New)"
            : "Legacy F/M");
    }

    static void SetupPlayerBodyModelsInternal()
    {

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[CharacterModelSwapperSetup] Player tagged GameObject not found.");
            return;
        }

        CharacterModelSwapper swapper = player.GetComponent<CharacterModelSwapper>();
        if (swapper == null)
            swapper = player.AddComponent<CharacterModelSwapper>();

        Transform anchor = player.transform.Find("model");
        Vector3 localPosition = anchor != null ? anchor.localPosition : Vector3.zero;
        Quaternion localRotation = anchor != null ? anchor.localRotation : Quaternion.identity;

        RemoveLegacyBodyModels(player.transform);

        SerializedObject serializedSwapper = new SerializedObject(swapper);

        for (int i = 0; i < ActiveModelDefinitions.Length; i++)
        {
            (string fieldName, string assetPath, string childName, string legacyChildName) = ActiveModelDefinitions[i];
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                Debug.LogError($"[CharacterModelSwapperSetup] Missing asset: {assetPath}");
                continue;
            }

            RemoveChildIfPresent(player.transform, legacyChildName);

            Transform existing = player.transform.Find(childName);
            GameObject instance;

            if (existing != null && IsInstanceFromAsset(existing.gameObject, source))
                instance = existing.gameObject;
            else
            {
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);

                instance = (GameObject)PrefabUtility.InstantiatePrefab(source, player.transform);
                instance.name = childName;
            }

            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = ActiveBodyScale;
            instance.SetActive(false);

            EnsureAnimator(instance, assetPath);

            Avatar avatar = GetHumanoidAvatarFromFbx(assetPath);
            if (avatar == null || !avatar.isValid)
                Debug.LogWarning(
                    $"[CharacterModelSwapperSetup] No valid Humanoid Avatar on {assetPath}. " +
                    "Open the FBX → Rig tab and fix bone mapping, then run Setup again.");

            SerializedProperty modelProp = serializedSwapper.FindProperty(fieldName);
            if (modelProp != null)
                modelProp.objectReferenceValue = instance;
        }

        AssignController(serializedSwapper, "maleOverrideController", MaleControllerPath);
        AssignController(serializedSwapper, "femaleOverrideController", FemaleControllerPath);

        if (anchor != null)
            anchor.gameObject.SetActive(false);

        serializedSwapper.ApplyModifiedPropertiesWithoutUndo();

        EnsurePlayerRootAnimator(player);
        HidePlaceholderPlayerMeshes(player.transform);

        EditorUtility.SetDirty(swapper);
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);

        PlayerLocomotionRigSetup.RemovePlayerLocomotionRigFromPlayer(player, log: true);
        PlayerAnimationRiggingGuard.EnsureOnPlayer(player);

        Debug.Log(
            $"[CharacterModelSwapperSetup] Player body models wired ({activeBodyModelSource}, scale {ActiveBodyScale}, Animation Pack).");
    }

    /// <summary>
    /// No-rig Tripo meshes: Humanoid rig copied from Mixamo idle. Used by No-Rig Pipeline menus only.
    /// </summary>
    public static void PrepareBodyModelsForAnimation()
    {
        ConfigureMixamoAnimationsAsHumanoid();
        CreateMixamoAvatarReferenceAsset();
        ConfigureBodyModelsCopyMixamoAvatar();
    }

    static void CreateMixamoAvatarReferenceAsset()
    {
        string idleFbx = MixamoIdleFbx;
        Avatar avatar = GetHumanoidAvatarFromFbx(idleFbx);
        if (avatar == null)
            return;

        const string assetPath = "Assets/_Project/Resources/MixamoAvatarReference.asset";
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        }

        MixamoAvatarReference reference = AssetDatabase.LoadAssetAtPath<MixamoAvatarReference>(assetPath);
        if (reference == null)
        {
            reference = ScriptableObject.CreateInstance<MixamoAvatarReference>();
            reference.avatar = avatar;
            AssetDatabase.CreateAsset(reference, assetPath);
        }
        else
        {
            reference.avatar = avatar;
            EditorUtility.SetDirty(reference);
        }

        AssetDatabase.SaveAssets();
    }

    static void EnsurePlayerRootAnimator(GameObject player)
    {
        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
            animator = player.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        RuntimeAnimatorController male = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MaleControllerPath);
        if (male != null && animator.runtimeAnimatorController != male)
            animator.runtimeAnimatorController = male;
    }

    static void HidePlaceholderPlayerMeshes(Transform player)
    {
        HashSet<string> keepNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < LegacyModelDefinitions.Length; i++)
            keepNames.Add(LegacyModelDefinitions[i].sceneChildName);
        for (int i = 0; i < NewModelDefinitions.Length; i++)
            keepNames.Add(NewModelDefinitions[i].sceneChildName);

        keepNames.Add("Main Camera");
        keepNames.Add("CM_TPP");
        keepNames.Add("CM_FPP");
        keepNames.Add("Rig");
        keepNames.Add("Rig_Locomotion");
        keepNames.Add("LocomotionRigTargets");

        for (int i = player.childCount - 1; i >= 0; i--)
        {
            Transform child = player.GetChild(i);
            if (keepNames.Contains(child.name))
                continue;

            child.gameObject.SetActive(false);
        }
    }

    [MenuItem("HealthySim/Rebuild Player Animator Overrides (Animation Pack)")]
    public static bool RebuildPlayerAnimatorOverridesMenu() => RebuildPlayerAnimatorOverrides();

    public static bool RebuildPlayerAnimatorOverrides()
    {
        AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            Debug.LogError($"[CharacterModelSwapperSetup] Failed to load base controller: {BaseControllerPath}");
            return false;
        }

        if (!TryLoadAnimationPackLocomotionClips(
                isFemale: false,
                out AnimationClip maleIdle,
                out AnimationClip maleWalk,
                out AnimationClip maleRun))
            return false;

        if (!TryLoadAnimationPackLocomotionClips(
                isFemale: true,
                out AnimationClip femaleIdle,
                out AnimationClip femaleWalk,
                out AnimationClip femaleRun))
            return false;

        AnimationClip maleJumpClip = LoadJumpClipFromFolder(MaleJumpFolder);
        AnimationClip femaleJumpClip = LoadJumpClipFromFolder(FemaleJumpFolder);

        bool allPassed = true;

        allPassed &= RebuildGenderOverride(
            "Male",
            MaleControllerPath,
            baseController,
            maleIdle,
            maleWalk,
            maleRun,
            maleJumpClip);

        allPassed &= RebuildGenderOverride(
            "Female",
            FemaleControllerPath,
            baseController,
            femaleIdle,
            femaleWalk,
            femaleRun,
            femaleJumpClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(allPassed
            ? "[CharacterModelSwapperSetup] All 8 clip assignments passed."
            : "[CharacterModelSwapperSetup] One or more clip assignments failed — see errors above.");

        return allPassed;
    }

    static bool RebuildGenderOverride(
        string genderLabel,
        string overridePath,
        AnimatorController baseController,
        AnimationClip idleClip,
        AnimationClip walkClip,
        AnimationClip runClip,
        AnimationClip jumpClip)
    {
        LogClipAssignment(genderLabel, "Idle", idleClip);
        LogClipAssignment(genderLabel, "Walk", walkClip);
        LogClipAssignment(genderLabel, "Run", runClip);
        LogClipAssignment(genderLabel, "Jump", jumpClip);

        if (idleClip == null || walkClip == null || runClip == null || jumpClip == null)
        {
            Debug.LogError($"[CharacterModelSwapperSetup] Failed to load all clips for {genderLabel}. Override not rebuilt.");
            return false;
        }

        AnimatorOverrideController overrideController =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);

        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController { name = Path.GetFileNameWithoutExtension(overridePath) };
            AssetDatabase.CreateAsset(overrideController, overridePath);
        }

        overrideController.runtimeAnimatorController = baseController;

        Dictionary<string, AnimationClip> replacements = new Dictionary<string, AnimationClip>
        {
            { "Idle", idleClip },
            { "Walk", walkClip },
            { "Run", runClip },
            { "Jump", jumpClip }
        };

        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (ChildAnimatorState childState in baseController.layers[0].stateMachine.states)
        {
            AnimationClip originalClip = childState.state.motion as AnimationClip;
            if (originalClip == null)
                continue;

            if (!replacements.TryGetValue(childState.state.name, out AnimationClip replacementClip))
                continue;

            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, replacementClip));
        }

        if (overrides.Count != 4)
        {
            Debug.LogError(
                $"[CharacterModelSwapperSetup] Expected 4 state overrides for {genderLabel}, mapped {overrides.Count}.");
            return false;
        }

        overrideController.ApplyOverrides(overrides);
        EditorUtility.SetDirty(overrideController);
        Debug.Log($"[CharacterModelSwapperSetup] Rebuilt {genderLabel} override controller at {overridePath}.");
        return true;
    }

    static void LogClipAssignment(string genderLabel, string stateName, AnimationClip clip)
    {
        if (clip == null)
        {
            Debug.LogError($"[CharacterModelSwapperSetup] FAIL {genderLabel} {stateName}: clip is null.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(clip);
        Debug.Log($"[CharacterModelSwapperSetup] PASS {genderLabel} {stateName}: '{clip.name}' from {assetPath}");
    }

    static bool TryLoadAnimationPackLocomotionClips(
        bool isFemale,
        out AnimationClip idleClip,
        out AnimationClip walkClip,
        out AnimationClip runClip)
    {
        string idleFolder = isFemale ? FemaleIdlesFolder : MaleIdlesFolder;
        string walkFolder = isFemale ? FemaleWalkFolder : MaleWalkFolder;
        string runFolder = isFemale ? FemaleRunFolder : MaleRunFolder;
        string label = isFemale ? "Female" : "Male";

        idleClip = LoadFirstIdleClipFromFolder(idleFolder);
        walkClip = LoadForwardClipFromFolder(walkFolder);
        runClip = LoadForwardClipFromFolder(runFolder);

        if (idleClip == null || walkClip == null || runClip == null)
        {
            Debug.LogError(
                $"[CharacterModelSwapperSetup] Missing Animation Pack clips for {label}. " +
                $"Idle={idleFolder}, Walk={walkFolder}, Run={runFolder}");
            return false;
        }

        Debug.Log(
            $"[CharacterModelSwapperSetup] {label} clips: Idle='{idleClip.name}', Walk='{walkClip.name}', Run='{runClip.name}'");
        return true;
    }

    static AnimationClip GetFirstAnimationClipFromFbx(string fbxPath)
    {
        List<AnimationClip> clips = GetAnimationClipsFromFbx(fbxPath);
        return clips.Count > 0 ? clips[0] : null;
    }

    static AnimationClip FindClipByExactName(List<AnimationClip> clips, string clipName)
    {
        return clips.FirstOrDefault(c =>
            string.Equals(c.name, clipName, StringComparison.Ordinal));
    }

    static List<AnimationClip> GetAnimationClipsFromFbx(string fbxPath)
    {
        if (string.IsNullOrEmpty(fbxPath))
            return new List<AnimationClip>();

        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static AnimationClip FindLocomotionClip(List<AnimationClip> clips, params string[] keywords)
    {
        for (int k = 0; k < keywords.Length; k++)
        {
            string keyword = keywords[k];
            AnimationClip match = clips.FirstOrDefault(c =>
                c.name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        return null;
    }

    static AnimationClip LoadClipFromFbx(string fbxPath, string nameFilter = null)
    {
        if (string.IsNullOrEmpty(fbxPath))
            return null;

        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .Where(c => nameFilter == null || c.name.Contains(nameFilter, StringComparison.Ordinal))
            .FirstOrDefault();
    }

    static AnimationClip LoadFirstIdleClipFromFolder(string folderPath)
    {
        foreach (string fbxPath in GetSortedFbxPaths(folderPath))
        {
            string fileName = Path.GetFileNameWithoutExtension(fbxPath);
            if (!fileName.EndsWith("@Idle01", StringComparison.Ordinal))
                continue;

            AnimationClip clip = LoadClipFromFbx(fbxPath);
            if (clip != null)
                return clip;
        }

        string fallbackPath = GetSortedFbxPaths(folderPath).FirstOrDefault();
        if (string.IsNullOrEmpty(fallbackPath))
        {
            Debug.LogError($"[CharacterModelSwapperSetup] No FBX files in {folderPath}");
            return null;
        }

        AnimationClip fallbackClip = LoadClipFromFbx(fallbackPath);
        if (fallbackClip == null)
            Debug.LogError($"[CharacterModelSwapperSetup] Failed to load idle clip from {fallbackPath}");

        return fallbackClip;
    }

    static AnimationClip LoadForwardClipFromFolder(string folderPath)
    {
        foreach (string fbxPath in GetSortedFbxPaths(folderPath))
        {
            if (fbxPath.Contains("/RootMotion/", StringComparison.OrdinalIgnoreCase) ||
                fbxPath.Contains("[RM]", StringComparison.Ordinal))
                continue;

            AnimationClip clip = LoadClipFromFbx(fbxPath, "Forward");
            if (clip == null)
                continue;

            if (clip.name.Contains("ForwardLeft", StringComparison.Ordinal) ||
                clip.name.Contains("ForwardRight", StringComparison.Ordinal) ||
                clip.name.Contains("[RM]", StringComparison.Ordinal))
                continue;

            return clip;
        }

        Debug.LogError($"[CharacterModelSwapperSetup] Failed to load forward clip from {folderPath}");
        return null;
    }

    static AnimationClip LoadJumpClipFromFolder(string folderPath)
    {
        foreach (string fbxPath in GetSortedFbxPaths(folderPath))
        {
            AnimationClip clip = LoadClipFromFbx(fbxPath, "Jump");
            if (clip == null)
                continue;

            if (clip.name.Contains("Fall", StringComparison.Ordinal) ||
                clip.name.Contains("Land", StringComparison.Ordinal) ||
                clip.name.Contains("Stumble", StringComparison.Ordinal) ||
                clip.name.Contains("Begin", StringComparison.Ordinal) ||
                clip.name.Contains("[RM]", StringComparison.Ordinal) ||
                clip.name.Contains("-", StringComparison.Ordinal))
                continue;

            return clip;
        }

        Debug.LogError($"[CharacterModelSwapperSetup] Failed to load jump clip from {folderPath}");
        return null;
    }

    static IEnumerable<string> GetSortedFbxPaths(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
            yield break;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
        Array.Sort(guids, (a, b) => string.Compare(
            AssetDatabase.GUIDToAssetPath(a),
            AssetDatabase.GUIDToAssetPath(b),
            StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                yield return path;
        }
    }

    static void AssignController(SerializedObject serializedSwapper, string propertyName, string assetPath)
    {
        SerializedProperty prop = serializedSwapper.FindProperty(propertyName);
        if (prop == null)
            return;

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(assetPath);
        if (controller == null)
            Debug.LogError($"[CharacterModelSwapperSetup] Failed to load controller: {assetPath}");

        prop.objectReferenceValue = controller;
    }

    static void RemoveChildIfPresent(Transform parent, string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return;

        Transform child = parent.Find(childName);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    static bool IsInstanceFromAsset(GameObject instance, GameObject sourceAsset)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (source == null)
            source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);

        return source == sourceAsset;
    }

    static void RemoveLegacyBodyModels(Transform player)
    {
        for (int i = player.childCount - 1; i >= 0; i--)
        {
            Transform child = player.GetChild(i);
            for (int j = 0; j < LegacyBodyModelChildNames.Length; j++)
            {
                if (child.name != LegacyBodyModelChildNames[j])
                    continue;

                UnityEngine.Object.DestroyImmediate(child.gameObject);
                break;
            }
        }
    }

    static Avatar GetHumanoidAvatarFromFbx(string assetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar avatar && avatar.isValid)
                return avatar;
        }

        return null;
    }

    static void EnsureAnimator(GameObject model, string assetPath)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        Avatar avatar = GetHumanoidAvatarFromFbx(assetPath);
        if (avatar != null)
            animator.avatar = avatar;

        animator.enabled = false;
    }
}
#endif
