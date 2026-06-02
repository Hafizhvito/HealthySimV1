#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NoRigCharacterPipeline
{
    const string MixamoAvatarResourcePath = "Assets/_Project/Resources/MixamoAvatarReference.asset";

    [MenuItem("HealthySim/No-Rig Pipeline/1. Create Mixamo Avatar Reference")]
    public static void CreateMixamoAvatarReferenceAsset()
    {
        string idleFbx = "Assets/_Project/Art/Animations/Animation for character/Idle.fbx";
        Avatar avatar = CharacterModelSwapperSceneSetup.GetHumanoidAvatarFromFbxPublic(idleFbx);
        if (avatar == null)
        {
            Debug.LogError("[NoRigPipeline] No Humanoid Avatar on Mixamo Idle.fbx. Run Configure Mixamo Animation FBX first.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        }

        MixamoAvatarReference existing = AssetDatabase.LoadAssetAtPath<MixamoAvatarReference>(MixamoAvatarResourcePath);
        if (existing == null)
        {
            MixamoAvatarReference created = ScriptableObject.CreateInstance<MixamoAvatarReference>();
            created.avatar = avatar;
            AssetDatabase.CreateAsset(created, MixamoAvatarResourcePath);
        }
        else
        {
            existing.avatar = avatar;
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[NoRigPipeline] Mixamo avatar reference saved: {MixamoAvatarResourcePath}");
    }

    [MenuItem("HealthySim/No-Rig Pipeline/2. Configure Body FBX (Humanoid + Mixamo Avatar)")]
    public static void ConfigureBodyFbxHumanoid()
    {
        CharacterModelSwapperSceneSetup.ConfigureNewCharacterFbxAsHumanoid();
        CharacterModelSwapperSceneSetup.ConfigureBodyModelsCopyMixamoAvatar();
    }

    [MenuItem("HealthySim/No-Rig Pipeline/3. Validate Body Models (Skinned Mesh)")]
    public static void ValidateBodyModels()
    {
        int ok = 0;
        int fail = 0;

        for (int i = 0; i < CharacterModelSwapperSceneSetup.ModelDefinitionCount; i++)
        {
            CharacterModelSwapperSceneSetup.GetModelDefinition(i, out string field, out string path, out string childName, out _);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[NoRigPipeline] FAIL {field}: missing {path}");
                fail++;
                continue;
            }

            bool skinned = CharacterModelSwapperSceneSetup.HasSkinnedMeshInAsset(prefab);
            bool avatarOk = CharacterModelSwapperSceneSetup.GetHumanoidAvatarFromFbxPublic(path) != null;

            if (skinned && avatarOk)
            {
                Debug.Log($"[NoRigPipeline] OK {childName}: skinned mesh + humanoid avatar.");
                ok++;
            }
            else
            {
                Debug.LogWarning(
                    $"[NoRigPipeline] FAIL {childName}: skinned={skinned} avatar={avatarOk}. " +
                    "Upload this mesh to mixamo.com → Auto-Rig → download FBX With Skin → replace file in folder.");
                fail++;
            }
        }

        if (fail > 0)
        {
            EditorUtility.DisplayDialog(
                "No-Rig Characters Need Mixamo Auto-Rig",
                "Model 'tanpa rigging' masih mesh statis (tidak ada tulang + skin weight).\n\n" +
                "Unity tidak bisa auto-rig mesh Tripo.\n\n" +
                "Langkah:\n" +
                "1. Buka mixamo.com\n" +
                "2. Upload setiap FBX (Female/Male Kurus, Ideal, Gemuk)\n" +
                "3. Auto-Rig → download FBX for Unity (With Skin)\n" +
                "4. Replace file di folder 'Character With No Rigging'\n" +
                "5. Jalankan lagi menu 2 dan 3, lalu Setup Player Body Models",
                "OK");
        }
        else
        {
            Debug.Log($"[NoRigPipeline] All {ok} body models ready for animation.");
        }
    }

    [MenuItem("HealthySim/No-Rig Pipeline/4. Full Setup (Anim + Body + Scene)")]
    public static void FullSetup()
    {
        CreateMixamoAvatarReferenceAsset();
        ConfigureBodyFbxHumanoid();
        if (!CharacterModelSwapperSceneSetup.RebuildPlayerAnimatorOverrides())
            return;

        CharacterModelSwapperSceneSetup.SetupPlayerBodyModels();
        ValidateBodyModels();
    }
}
#endif
