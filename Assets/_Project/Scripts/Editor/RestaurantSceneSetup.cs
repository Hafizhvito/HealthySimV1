#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RestaurantSceneSetup
{
    private const string FoodObjectName = "Interactable_Food_Restaurant";

    [MenuItem("HealthySim/Restaurant/Setup Invisible Food Counter")]
    public static void SetupInvisibleFoodCounter()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[Restaurant] Buka SampleScene dulu.");
            return;
        }

        GameObject food = FindByName(FoodObjectName);
        if (food == null)
        {
            Debug.LogError($"[Restaurant] {FoodObjectName} tidak ditemukan.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(food, "Setup invisible food counter");

        if (food.GetComponent<FoodPickupInteractable>() == null)
            food.AddComponent<FoodPickupInteractable>();

        InvisibleInteractableVisual visual = food.GetComponent<InvisibleInteractableVisual>();
        if (visual == null)
            visual = food.AddComponent<InvisibleInteractableVisual>();

        visual.Apply();

        BoxCollider box = food.GetComponent<BoxCollider>();
        if (box == null)
            box = food.AddComponent<BoxCollider>();

        box.isTrigger = false;

        EditorUtility.SetDirty(food);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Restaurant] Interactable_Food_Restaurant: mesh hidden, collider tetap aktif.");
    }

    private static bool IsSampleSceneActive()
    {
        return string.Equals(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            "SampleScene",
            StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject FindByName(string objectName)
    {
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i].gameObject;
        }

        return null;
    }
}
#endif
