using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class CreateAgingPanel
{
    [MenuItem("HealthSim/Create Aging Notification Panel")]
    public static void Create()
    {
        // Find HUD_Canvas
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform hudCanvas = null;
        foreach (var c in allCanvases)
        {
            if (c.gameObject.name == "HUD_Canvas")
            {
                hudCanvas = c.transform;
                break;
            }
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[CreateAgingPanel] HUD_Canvas not found in scene. Open the correct scene first.");
            return;
        }

        // Remove existing if any
        Transform existing = hudCanvas.Find("AgingNotificationPanel");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Root panel
        GameObject root = new GameObject("AgingNotificationPanel",
            typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create AgingNotificationPanel");
        root.transform.SetParent(hudCanvas, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Overlay
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        overlay.GetComponent<Image>().raycastTarget = true;

        // Shadow
        GameObject shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
        shadow.transform.SetParent(root.transform, false);
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = new Vector2(6f, -6f);
        shadowRect.sizeDelta = new Vector2(764f, 424f);
        shadow.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
        shadow.GetComponent<Image>().raycastTarget = false;

        // Card
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(760f, 420f);
        card.GetComponent<Image>().color = Color.white;
        card.GetComponent<Image>().raycastTarget = true;

        // Accent bar
        GameObject accent = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(card.transform, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = new Vector2(6f, 0f);
        accentRect.sizeDelta = new Vector2(6f, 0f);
        accent.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);
        accent.GetComponent<Image>().raycastTarget = false;

        // Title text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(card.transform, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(680f, 56f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 34f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.textWrappingMode = TextWrappingModes.Normal;
        titleTmp.text = string.Empty;

        // Divider
        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(card.transform, false);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 1f);
        dividerRect.anchorMax = new Vector2(0.5f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -100f);
        dividerRect.sizeDelta = new Vector2(640f, 2f);
        divider.GetComponent<Image>().color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);
        divider.GetComponent<Image>().raycastTarget = false;

        // Body text
        GameObject bodyGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(card.transform, false);
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -118f);
        bodyRect.sizeDelta = new Vector2(680f, 230f);
        TextMeshProUGUI bodyTmp = bodyGo.GetComponent<TextMeshProUGUI>();
        bodyTmp.fontSize = 20f;
        bodyTmp.color = new Color32(0x33, 0x33, 0x33, 0xFF);
        bodyTmp.alignment = TextAlignmentOptions.Center;
        bodyTmp.textWrappingMode = TextWrappingModes.Normal;
        bodyTmp.lineSpacing = 4f;
        bodyTmp.text = string.Empty;

        // Lanjut button
        GameObject btnGo = new GameObject("LanjutButton",
            typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        RectTransform btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-30f, 28f);
        btnRect.sizeDelta = new Vector2(180f, 52f);
        btnGo.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        GameObject btnText = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnText.transform.SetParent(btnGo.transform, false);
        RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTmp = btnText.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Lanjut →";
        btnTmp.fontSize = 20f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        root.SetActive(false);

        EditorUtility.SetDirty(hudCanvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            hudCanvas.gameObject.scene);

        Selection.activeGameObject = root;
        Debug.Log("[CreateAgingPanel] AgingNotificationPanel created under HUD_Canvas. Save the scene (Ctrl+S).");
    }
}
