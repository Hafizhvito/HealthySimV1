#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcVisualModelSlotSceneSetup
{
    private static readonly string[] DefaultNpcNames =
    {
        "Interactable_NPC",
        "Interactable_NPC_Restoran"
    };

    [MenuItem("HealthySim/Setup NPC Visual Slot (Selected)")]
    public static void SetupSelected()
    {
        Transform[] selection = Selection.transforms;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("[HealthySim] Pilih NPC di Hierarchy dulu.");
            return;
        }

        int count = 0;
        for (int i = 0; i < selection.Length; i++)
        {
            if (selection[i] == null)
                continue;

            if (EnsureSlot(selection[i].gameObject))
                count++;
        }

        Debug.Log($"[HealthySim] NPC Visual Slot ditambahkan ke {count} objek. Drag prefab karakter lalu Apply.");
    }

    [MenuItem("HealthySim/Setup SampleScene NPC Visual Slots")]
    public static void SetupSampleSceneNpcs()
    {
        if (!string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "SampleScene"))
        {
            Debug.LogWarning("[HealthySim] Buka SampleScene dulu.");
            return;
        }

        int count = 0;
        for (int i = 0; i < DefaultNpcNames.Length; i++)
        {
            GameObject npc = FindInactiveByName(DefaultNpcNames[i]);
            if (npc == null)
                continue;

            if (EnsureSlot(npc))
                count++;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[HealthySim] NPC Visual Slot siap di {count} NPC. Drag prefab dari Assets/DavidJalbert/LowPolyPeople/Prefabs lalu Apply.");
    }

    private static bool EnsureSlot(GameObject npc)
    {
        if (npc == null)
            return false;

        Undo.RegisterFullObjectHierarchyUndo(npc, "Setup NPC Visual Slot");

        NpcVisualModelSlot slot = npc.GetComponent<NpcVisualModelSlot>();
        if (slot == null)
            slot = npc.AddComponent<NpcVisualModelSlot>();

        slot.ApplyVisual();
        EditorUtility.SetDirty(npc);
        return true;
    }

    private static GameObject FindInactiveByName(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
                return transforms[i].gameObject;
        }

        return null;
    }
}
#endif
