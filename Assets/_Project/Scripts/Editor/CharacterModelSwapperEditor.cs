#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterModelSwapper))]
public class CharacterModelSwapperEditor : Editor
{
    static readonly string[] ModelFieldNames =
    {
        "femaleKurus",
        "femaleIdeal",
        "femaleOverweight",
        "maleKurus",
        "maleIdeal",
        "maleOverweight"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Ensure Model Animators + Avatars"))
        {
            EnsureModelAnimators((CharacterModelSwapper)target);
        }
    }

    void OnEnable()
    {
        EnsureModelAnimators((CharacterModelSwapper)target);
    }

    static void EnsureModelAnimators(CharacterModelSwapper swapper)
    {
        if (swapper == null)
            return;

        SerializedObject serialized = new SerializedObject(swapper);
        bool changed = false;

        for (int i = 0; i < ModelFieldNames.Length; i++)
        {
            SerializedProperty modelProp = serialized.FindProperty(ModelFieldNames[i]);
            if (modelProp == null || modelProp.objectReferenceValue == null)
                continue;

            GameObject model = modelProp.objectReferenceValue as GameObject;
            if (model == null)
                continue;

            if (EnsureAnimatorOnModel(model))
                changed = true;
        }

        if (changed)
        {
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(swapper);
        }
    }

    static bool EnsureAnimatorOnModel(GameObject model)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        if (animator.avatar != null && animator.avatar.isValid)
            return false;

        Avatar avatar = FindAvatarForGameObject(model);
        if (avatar == null)
        {
            Debug.LogWarning($"[CharacterModelSwapper] Avatar tidak ditemukan untuk '{model.name}'.");
            return false;
        }

        animator.avatar = avatar;
        animator.enabled = false;
        EditorUtility.SetDirty(model);
        return true;
    }

    static Avatar FindAvatarForGameObject(GameObject model)
    {
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model);
        if (string.IsNullOrEmpty(assetPath))
            assetPath = AssetDatabase.GetAssetPath(model);

        if (string.IsNullOrEmpty(assetPath))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Avatar avatar && avatar.isValid)
                return avatar;
        }

        return null;
    }
}
#endif
