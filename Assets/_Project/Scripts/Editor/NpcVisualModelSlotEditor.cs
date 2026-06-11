#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcVisualModelSlot))]
public class NpcVisualModelSlotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        NpcVisualModelSlot slot = (NpcVisualModelSlot)target;

        EditorGUILayout.HelpBox(
            "Cara pakai:\n" +
            "1. Drag prefab karakter (mis. LowPolyPeople) ke field Character Prefab, lalu klik Apply.\n" +
            "2. Atau buat child 'NpcVisual' lalu drag prefab ke situ di Hierarchy.\n" +
            "Collider & script dialog tetap di root NPC.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Apply Visual Now"))
        {
            Undo.RegisterFullObjectHierarchyUndo(slot.gameObject, "Apply NPC Visual");
            slot.ApplyVisual();
            EditorUtility.SetDirty(slot);
        }

        if (GUILayout.Button("Create / Find NpcVisual Child"))
        {
            Undo.RegisterFullObjectHierarchyUndo(slot.gameObject, "Ensure NPC Visual Root");
            slot.ApplyVisual();
            EditorUtility.SetDirty(slot);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        NpcVisualModelSlot slot = (NpcVisualModelSlot)target;
        if (slot == null)
            return;

        SerializedProperty prefabProp = serializedObject.FindProperty("characterPrefab");
        if (prefabProp != null && prefabProp.objectReferenceValue != null)
            slot.ApplyVisual();
    }
}
#endif
