using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;

using UnityEngine.UI;

using TMPro;



/// <summary>

/// Designer-facing fixes for InputMenu ringkasan: card layout, Mulai touch target, raycast order.

/// Run once with InputMenu open, then save the scene (Ctrl+S).

/// </summary>

public static class InputMenuSceneSetup

{

    private const float DefaultCardWidth = 1500f;

    private const float DefaultCardHeight = 90f;

    private const float MinStartButtonWidth = 320f;

    private const float MinStartButtonHeight = 100f;



    [MenuItem("HealthySim/Fix Input Menu Ringkasan Layout")]

    public static void FixInputMenuRingkasanLayout()

    {

        InputFormManager formManager = Object.FindFirstObjectByType<InputFormManager>(FindObjectsInactive.Include);

        if (formManager == null)

        {

            Debug.LogWarning("[HealthySim] InputFormManager not found in the active scene.");

            return;

        }



        SerializedObject serialized = new SerializedObject(formManager);

        SerializedProperty cardsProp = serialized.FindProperty("dataCards");

        SerializedProperty mulaiProp = serialized.FindProperty("btnMulai");



        if (cardsProp != null && cardsProp.isArray)

        {

            Transform cardGroup = null;

            for (int i = 0; i < cardsProp.arraySize; i++)

            {

                RectTransform card = cardsProp.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;

                if (card == null)

                    continue;



                if (cardGroup == null)

                    cardGroup = card.parent;



                LayoutElement layoutElement = card.GetComponent<LayoutElement>();

                if (layoutElement != null)

                    Object.DestroyImmediate(layoutElement, true);



                card.anchorMin = Vector2.zero;

                card.anchorMax = Vector2.zero;

                card.pivot = new Vector2(0.5f, 0.5f);

                card.anchoredPosition = Vector2.zero;

                card.sizeDelta = new Vector2(DefaultCardWidth, DefaultCardHeight);

                card.localScale = Vector3.one;



                CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();

                if (canvasGroup != null)

                    canvasGroup.alpha = 1f;



                SetDisplayOnlyRaycasts(card, false);

            }



            if (cardGroup != null)

            {

                VerticalLayoutGroup layout = cardGroup.GetComponent<VerticalLayoutGroup>();

                if (layout != null)

                {

                    layout.childAlignment = TextAnchor.MiddleCenter;

                    layout.childControlWidth = false;

                    layout.childControlHeight = false;

                    layout.childForceExpandWidth = true;

                    layout.childForceExpandHeight = true;

                }



                SetDisplayOnlyRaycasts(cardGroup, false);

            }

        }



        GameObject panelRingkasan = serialized.FindProperty("panelRingkasan")?.objectReferenceValue as GameObject;

        if (panelRingkasan != null)

        {

            Image panelBackground = panelRingkasan.GetComponent<Image>();

            if (panelBackground != null)

                panelBackground.raycastTarget = false;



            Transform buttonRow = panelRingkasan.transform.Find("Button");

            if (buttonRow != null)

                buttonRow.SetAsLastSibling();



            Transform title = panelRingkasan.transform.Find("Txt_Title");

            if (title != null)

                SetDisplayOnlyRaycasts(title, false);

        }



        Button mulaiButton = null;

        Button[] buttons = formManager.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)

        {

            if (buttons[i] == null || !buttons[i].gameObject.name.Contains("Mulai"))

                continue;



            mulaiButton = buttons[i];

            RectTransform rect = mulaiButton.GetComponent<RectTransform>();

            if (rect != null)

            {

                Vector2 size = rect.sizeDelta;

                size.x = Mathf.Max(size.x, MinStartButtonWidth);

                size.y = Mathf.Max(size.y, MinStartButtonHeight);

                rect.sizeDelta = size;

            }



            EnsureLayoutElement(mulaiButton.gameObject, MinStartButtonWidth, MinStartButtonHeight);



            Image targetGraphic = mulaiButton.targetGraphic as Image;

            if (targetGraphic != null)

                targetGraphic.raycastTarget = true;



            break;

        }



        if (mulaiProp != null && mulaiButton != null)

        {

            mulaiProp.objectReferenceValue = mulaiButton;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(formManager);

        }



        EditorSceneManager.MarkSceneDirty(formManager.gameObject.scene);

        Debug.Log("[HealthySim] InputMenu ringkasan fixed: cards layout, Mulai touch target, raycast order. Save scene (Ctrl+S).");

    }



    private static void SetDisplayOnlyRaycasts(Transform root, bool enabled)

    {

        if (root == null)

            return;



        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)

        {

            Graphic graphic = graphics[i];

            if (graphic == null || graphic.GetComponentInParent<Button>(true) != null)

                continue;



            graphic.raycastTarget = enabled;

        }

    }



    private static void EnsureLayoutElement(GameObject target, float minWidth, float minHeight)

    {

        if (target == null)

            return;



        LayoutElement layout = target.GetComponent<LayoutElement>();

        if (layout == null)

            layout = target.AddComponent<LayoutElement>();



        layout.minWidth = minWidth;

        layout.minHeight = minHeight;

        layout.preferredWidth = minWidth;

        layout.preferredHeight = minHeight;

    }

}


