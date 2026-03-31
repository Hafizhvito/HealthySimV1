using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FoodCatalogProvider : MonoBehaviour
{
    [Header("Sumber Makanan")]
    [SerializeField] private List<FoodData> foods = new List<FoodData>();
    [SerializeField] private bool includeResourcesFallback = true;

    public IReadOnlyList<FoodData> Foods => foods;

    void Awake()
    {
        RemoveNulls();
    }

    public List<FoodData> GetFoods()
    {
        RemoveNulls();

#if UNITY_EDITOR
        if (foods.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:FoodData", new[] { "Assets/_Project/Data/Foods" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                FoodData data = AssetDatabase.LoadAssetAtPath<FoodData>(path);
                TryAdd(data);
            }
        }
#endif

        if (foods.Count == 0 && includeResourcesFallback)
        {
            FoodData[] fromResources = Resources.LoadAll<FoodData>("FoodData");
            for (int i = 0; i < fromResources.Length; i++)
                TryAdd(fromResources[i]);
        }

        return new List<FoodData>(foods);
    }

    public void SetFoods(List<FoodData> items)
    {
        foods.Clear();
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
            TryAdd(items[i]);
    }

    private void TryAdd(FoodData food)
    {
        if (food == null || foods.Contains(food))
            return;

        foods.Add(food);
    }

    private void RemoveNulls()
    {
        foods.RemoveAll(item => item == null);
    }

#if UNITY_EDITOR
    [ContextMenu("Muat Ulang Dari Data/Foods")]
    private void ReloadFromProjectFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:FoodData", new[] { "Assets/_Project/Data/Foods" });
        foods.Clear();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FoodData data = AssetDatabase.LoadAssetAtPath<FoodData>(path);
            TryAdd(data);
        }

        EditorUtility.SetDirty(this);
    }
#endif
}
