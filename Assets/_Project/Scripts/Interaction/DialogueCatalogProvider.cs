using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DialogueCatalogProvider : MonoBehaviour
{
    [SerializeField] private List<DialogueGraphData> dialogues = new List<DialogueGraphData>();
    [SerializeField] private bool includeResourcesFallback = true;

    public List<DialogueGraphData> GetDialogues()
    {
        dialogues.RemoveAll(item => item == null);

#if UNITY_EDITOR
        if (dialogues.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:DialogueGraphData", new[] { "Assets/_Project/Data/Dialogues" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DialogueGraphData data = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
                if (data != null && !dialogues.Contains(data))
                    dialogues.Add(data);
            }
        }
#endif

        if (dialogues.Count == 0 && includeResourcesFallback)
        {
            DialogueGraphData[] resourceDialogues = Resources.LoadAll<DialogueGraphData>("Dialogue");
            for (int i = 0; i < resourceDialogues.Length; i++)
            {
                if (resourceDialogues[i] != null && !dialogues.Contains(resourceDialogues[i]))
                    dialogues.Add(resourceDialogues[i]);
            }
        }

        return new List<DialogueGraphData>(dialogues);
    }

#if UNITY_EDITOR
    [ContextMenu("Muat Ulang Dari Data/Dialogues")]
    private void ReloadFromProjectFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueGraphData", new[] { "Assets/_Project/Data/Dialogues" });
        dialogues.Clear();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DialogueGraphData data = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
            if (data != null && !dialogues.Contains(data))
                dialogues.Add(data);
        }

        EditorUtility.SetDirty(this);
    }
#endif
}
