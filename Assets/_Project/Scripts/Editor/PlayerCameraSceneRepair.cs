#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Unity.Cinemachine;

public static class PlayerCameraSceneRepair
{
    static readonly string[] AlwaysActiveChildNames =
    {
        "Main Camera",
        "CM_TPP",
        "CM_FPP"
    };

    [MenuItem("HealthySim/Fix Player Camera And Rig Gizmos")]
    public static void FixPlayerCameraAndRigGizmos()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[PlayerCameraRepair] Player not found.");
            return;
        }

        int fixedCount = 0;

        for (int i = 0; i < AlwaysActiveChildNames.Length; i++)
        {
            Transform child = player.transform.Find(AlwaysActiveChildNames[i]);
            if (child == null)
                continue;

            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
                fixedCount++;
            }
        }

        Transform mainCam = player.transform.Find("Main Camera");
        if (mainCam != null)
        {
            Camera cam = mainCam.GetComponent<Camera>();
            if (cam != null && !cam.enabled)
            {
                cam.enabled = true;
                fixedCount++;
            }

            CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.enabled = true;
                SerializedObject so = new SerializedObject(brain);
                SerializedProperty frustum = so.FindProperty("ShowCameraFrustum");
                if (frustum != null && frustum.boolValue)
                {
                    frustum.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    fixedCount++;
                }
            }
        }

        BoneRenderer[] boneRenderers = player.GetComponents<BoneRenderer>();
        for (int i = 0; i < boneRenderers.Length; i++)
        {
            SerializedObject so = new SerializedObject(boneRenderers[i]);
            SerializedProperty drawBones = so.FindProperty("drawBones");
            if (drawBones != null && drawBones.boolValue)
            {
                drawBones.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                fixedCount++;
            }
        }

        ConsolidateRigLayers(player);

        CameraSystem cameraSystem = Object.FindFirstObjectByType<CameraSystem>();
        if (cameraSystem != null)
        {
            SerializedObject so = new SerializedObject(cameraSystem);
            SerializedProperty root = so.FindProperty("playerRoot");
            if (root != null && root.objectReferenceValue == null)
            {
                root.objectReferenceValue = player.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                fixedCount++;
            }
        }

        EditorUtility.SetDirty(player);
        if (cameraSystem != null)
            EditorUtility.SetDirty(cameraSystem);

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log($"[PlayerCameraRepair] Applied {fixedCount} fixes. Save scene and press Play.");
    }

    static void ConsolidateRigLayers(GameObject player)
    {
        RigBuilder rigBuilder = player.GetComponent<RigBuilder>();
        if (rigBuilder == null)
            return;

        Rig locomotionRig = null;
        Transform locomotion = player.transform.Find("Rig_Locomotion");
        if (locomotion != null)
            locomotionRig = locomotion.GetComponent<Rig>();

        if (locomotionRig == null)
            locomotionRig = player.GetComponentInChildren<Rig>(true);

        SerializedObject so = new SerializedObject(rigBuilder);
        SerializedProperty layers = so.FindProperty("m_RigLayers");
        if (layers == null)
            return;

        if (locomotionRig != null)
        {
            layers.arraySize = 1;
            layers.GetArrayElementAtIndex(0).FindPropertyRelative("m_Rig").objectReferenceValue = locomotionRig;
            layers.GetArrayElementAtIndex(0).FindPropertyRelative("m_Active").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        DestroyChildIfPresent(player.transform, "Rig 1");
        DestroyChildIfPresent(player.transform, "Rig 2");
        Transform legacyRig = player.transform.Find("Rig");
        if (legacyRig != null && legacyRig.name == "Rig" && locomotion != null)
            Object.DestroyImmediate(legacyRig.gameObject);
    }

    static void DestroyChildIfPresent(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }
}
#endif
