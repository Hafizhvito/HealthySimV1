using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UiPanelSceneSetup
{
    private const string HudCanvasName = "HUD_Canvas";

    [MenuItem("HealthSim/Setup/Ensure UI Panels")]
    private static void EnsureUiPanels()
    {
        Transform hudCanvas = FindHudCanvas();
        if (hudCanvas == null)
        {
            Debug.LogWarning("[HealthSim/Setup] HUD_Canvas not found in scene.");
            return;
        }

        EnsureEndingPanel(hudCanvas);
        EnsureAgingNotificationPanel(hudCanvas);
        EnsureSleepWakeCanvas();
        EnsureSleepPreCinematicCanvas();
    }

    private static Transform FindHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.name == HudCanvasName)
                return canvases[i].transform;
        }

        return null;
    }

    private static void EnsureEndingPanel(Transform parent)
    {
        if (parent == null)
            return;

        if (parent.Find("EndingPanel") != null)
            return;

        GameObject root = new GameObject("EndingPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        RectTransform panelRoot = root.GetComponent<RectTransform>();
        panelRoot.anchorMin = Vector2.zero;
        panelRoot.anchorMax = Vector2.one;
        panelRoot.offsetMin = Vector2.zero;
        panelRoot.offsetMax = Vector2.zero;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        EndingPanelBinder binder = root.AddComponent<EndingPanelBinder>();
        binder.panelRoot = panelRoot;
        binder.panelGroup = group;

        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(root.transform, false);
        RectTransform ovR = overlay.GetComponent<RectTransform>();
        ovR.anchorMin = Vector2.zero;
        ovR.anchorMax = Vector2.one;
        ovR.offsetMin = Vector2.zero;
        ovR.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.92f);

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        RectTransform cardR = card.GetComponent<RectTransform>();
        cardR.anchorMin = new Vector2(0.5f, 0.5f);
        cardR.anchorMax = new Vector2(0.5f, 0.5f);
        cardR.pivot = new Vector2(0.5f, 0.5f);
        cardR.anchoredPosition = Vector2.zero;
        cardR.sizeDelta = new Vector2(820f, 480f);
        card.GetComponent<Image>().color = Color.white;

        GameObject acc = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
        acc.transform.SetParent(card.transform, false);
        RectTransform acR = acc.GetComponent<RectTransform>();
        acR.anchorMin = new Vector2(0f, 0f);
        acR.anchorMax = new Vector2(0f, 1f);
        acR.pivot = new Vector2(0f, 0.5f);
        acR.offsetMin = Vector2.zero;
        acR.offsetMax = new Vector2(6f, 0f);
        acR.sizeDelta = new Vector2(6f, 0f);
        Image accImage = acc.GetComponent<Image>();
        accImage.color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        GameObject tGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGo.transform.SetParent(card.transform, false);
        RectTransform tR = tGo.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0.5f, 1f);
        tR.anchorMax = new Vector2(0.5f, 1f);
        tR.pivot = new Vector2(0.5f, 1f);
        tR.anchoredPosition = new Vector2(0f, -40f);
        tR.sizeDelta = new Vector2(740f, 60f);
        TextMeshProUGUI title = tGo.GetComponent<TextMeshProUGUI>();
        title.fontSize = 36f;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.Normal;

        GameObject dv = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dv.transform.SetParent(card.transform, false);
        RectTransform dvR = dv.GetComponent<RectTransform>();
        dvR.anchorMin = new Vector2(0.5f, 1f);
        dvR.anchorMax = new Vector2(0.5f, 1f);
        dvR.pivot = new Vector2(0.5f, 1f);
        dvR.anchoredPosition = new Vector2(0f, -108f);
        dvR.sizeDelta = new Vector2(700f, 2f);
        dv.GetComponent<Image>().color = new Color32(0xE0, 0xE0, 0xE0, 0xFF);

        GameObject bGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bGo.transform.SetParent(card.transform, false);
        RectTransform bR = bGo.GetComponent<RectTransform>();
        bR.anchorMin = new Vector2(0.5f, 1f);
        bR.anchorMax = new Vector2(0.5f, 1f);
        bR.pivot = new Vector2(0.5f, 1f);
        bR.anchoredPosition = new Vector2(0f, -126f);
        bR.sizeDelta = new Vector2(740f, 280f);
        TextMeshProUGUI body = bGo.GetComponent<TextMeshProUGUI>();
        body.fontSize = 21f;
        body.color = new Color32(0x33, 0x33, 0x33, 0xFF);
        body.alignment = TextAlignmentOptions.Center;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 5f;

        GameObject btnGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(card.transform, false);
        RectTransform btnR = btnGo.GetComponent<RectTransform>();
        btnR.anchorMin = new Vector2(1f, 0f);
        btnR.anchorMax = new Vector2(1f, 0f);
        btnR.pivot = new Vector2(1f, 0f);
        btnR.anchoredPosition = new Vector2(-32f, 32f);
        btnR.sizeDelta = new Vector2(200f, 52f);
        btnGo.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        GameObject btnTGo = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTGo.transform.SetParent(btnGo.transform, false);
        RectTransform btnTR = btnTGo.GetComponent<RectTransform>();
        btnTR.anchorMin = Vector2.zero;
        btnTR.anchorMax = Vector2.one;
        btnTR.offsetMin = Vector2.zero;
        btnTR.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTmp = btnTGo.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Selesai";
        btnTmp.fontSize = 22f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        binder.titleText = title;
        binder.bodyText = body;
        binder.closeButton = btnGo.GetComponent<Button>();
        binder.accentBar = accImage;

        root.SetActive(false);
        Debug.Log("[HealthSim/Setup] EndingPanel created under HUD_Canvas.");
    }

    private static void EnsureAgingNotificationPanel(Transform parent)
    {
        if (parent == null)
            return;

        if (parent.Find("AgingNotificationPanel") != null)
            return;

        GameObject root = new GameObject("AgingNotificationPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(parent, false);
        RectTransform panelRoot = root.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.sizeDelta = new Vector2(860f, 360f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        AgingNotificationPanelBinder binder = root.AddComponent<AgingNotificationPanelBinder>();
        binder.panelRoot = panelRoot;
        binder.panelGroup = group;

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(root.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(760f, 50f);
        TextMeshProUGUI title = titleObj.GetComponent<TextMeshProUGUI>();
        title.fontSize = 28f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.95f, 0.92f, 0.84f, 1f);

        GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(root.transform, false);
        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -82f);
        bodyRect.sizeDelta = new Vector2(760f, 190f);
        TextMeshProUGUI body = bodyObj.GetComponent<TextMeshProUGUI>();
        body.fontSize = 20f;
        body.alignment = TextAlignmentOptions.Center;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 6f;
        body.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        GameObject btnGo = new GameObject("LanjutButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(root.transform, false);
        RectTransform btnR = btnGo.GetComponent<RectTransform>();
        btnR.anchorMin = new Vector2(1f, 0f);
        btnR.anchorMax = new Vector2(1f, 0f);
        btnR.pivot = new Vector2(1f, 0f);
        btnR.anchoredPosition = new Vector2(-36f, 24f);
        btnR.sizeDelta = new Vector2(180f, 48f);
        btnGo.GetComponent<Image>().color = new Color32(0x21, 0x96, 0xF3, 0xFF);

        GameObject btnText = new GameObject("ButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnText.transform.SetParent(btnGo.transform, false);
        RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        TextMeshProUGUI btnTmp = btnText.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "Lanjut";
        btnTmp.fontSize = 20f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        binder.titleText = title;
        binder.bodyText = body;
        binder.continueButton = btnGo.GetComponent<Button>();

        root.SetActive(false);
        Debug.Log("[HealthSim/Setup] AgingNotificationPanel created under HUD_Canvas.");
    }

    private static void EnsureSleepWakeCanvas()
    {
        if (GameObject.Find("SleepWakeCanvas") != null)
            return;

        GameObject canvasObj = new GameObject("SleepWakeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        Canvas wakeCanvas = canvasObj.GetComponent<Canvas>();
        wakeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        wakeCanvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
        CanvasGroup group = canvasObj.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        GameObject panelObj = new GameObject("WakePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.87f);
        panelRect.anchorMax = new Vector2(0.5f, 0.87f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 140f);
        panelObj.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.78f);

        GameObject textObj = new GameObject("WakeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelObj.transform, false);
        textRect.anchorMin = new Vector2(0.05f, 0.12f);
        textRect.anchorMax = new Vector2(0.95f, 0.88f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI wakeText = textObj.GetComponent<TextMeshProUGUI>();
        wakeText.alignment = TextAlignmentOptions.Center;
        wakeText.fontSize = 26f;
        wakeText.color = new Color(0.98f, 0.94f, 0.84f, 1f);
        wakeText.textWrappingMode = TextWrappingModes.Normal;
        wakeText.text = string.Empty;

        SleepWakeCanvasBinder binder = canvasObj.AddComponent<SleepWakeCanvasBinder>();
        binder.canvas = wakeCanvas;
        binder.canvasGroup = group;
        binder.wakeText = wakeText;
        binder.background = panelObj.GetComponent<Image>();

        canvasObj.SetActive(false);
        Debug.Log("[HealthSim/Setup] SleepWakeCanvas created in scene.");
    }

    private static void EnsureSleepPreCinematicCanvas()
    {
        if (GameObject.Find("SleepPreCinematicCanvas") != null)
            return;

        GameObject canvasObj = new GameObject("SleepPreCinematicCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));

        Canvas sleepCanvas = canvasObj.GetComponent<Canvas>();
        sleepCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sleepCanvas.sortingOrder = 998;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.GetComponent<GraphicRaycaster>().enabled = false;
        CanvasGroup group = canvasObj.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        GameObject panelObj = new GameObject("SleepEyeClosePanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObj.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

        GameObject textObj = new GameObject("SleepWarmLine", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(panelObj.transform, false);
        textRect.anchorMin = new Vector2(0.12f, 0.18f);
        textRect.anchorMax = new Vector2(0.88f, 0.36f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI warmText = textObj.GetComponent<TextMeshProUGUI>();
        warmText.alignment = TextAlignmentOptions.Center;
        warmText.fontSize = 30f;
        warmText.color = new Color(0.98f, 0.95f, 0.86f, 1f);
        warmText.textWrappingMode = TextWrappingModes.Normal;
        warmText.text = string.Empty;

        SleepPreCinematicCanvasBinder binder = canvasObj.AddComponent<SleepPreCinematicCanvasBinder>();
        binder.canvas = sleepCanvas;
        binder.canvasGroup = group;
        binder.warmText = warmText;
        binder.background = panelObj.GetComponent<Image>();

        canvasObj.SetActive(false);
        Debug.Log("[HealthSim/Setup] SleepPreCinematicCanvas created in scene.");
    }
}
