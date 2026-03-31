using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class HUDAutoSetup : MonoBehaviour
{
    private const string CanvasName = "HUD_Canvas";

    void OnEnable()
    {
        BuildOrUpdateHUD();
    }

    private void BuildOrUpdateHUD()
    {
        Canvas canvas = EnsureCanvas();

        RectTransform energyPanel = EnsurePanel(canvas.transform, "EnergyBar_Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(300f, 30f));
        Image energyBg = EnsureImage(energyPanel, "EnergyBar_BG", new Color32(50, 50, 50, 180), true, 1f);
        Image energyFill = EnsureImage(energyPanel, "EnergyBar_Fill", new Color32(80, 200, 80, 255), false, 1f);
        Image energyWarning = EnsureImage(energyPanel, "EnergyBar_Warning_Fill", new Color32(255, 165, 0, 255), false, 0f);
        energyBg.rectTransform.SetSiblingIndex(0);
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillOrigin = 0;
        energyFill.fillAmount = 1f;
        SetupFillImage(energyFill, 1f);
        SetupFillImage(energyWarning, 0f);
        TextMeshProUGUI energyLabel = EnsureText(energyPanel, "EnergyBar_Label", "ENERGI", 14, Color.white, TextAlignmentOptions.Left);
        ApplyHudTextStyle(energyLabel);
        SetupLabelRect(energyLabel.rectTransform, new Vector2(-75f, 0f), new Vector2(70f, 30f));

        RectTransform moodPanel = EnsurePanel(canvas.transform, "MoodBar_Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -90f), new Vector2(200f, 20f));
        EnsureImage(moodPanel, "MoodBar_BG", new Color32(30, 30, 30, 200), true, 1f);
        Image moodFill = EnsureImage(moodPanel, "MoodBar_Fill", new Color32(255, 220, 50, 255), false, 0.5f);
        SetupFillImage(moodFill, 0.5f);
        TextMeshProUGUI moodLabel = EnsureText(moodPanel, "MoodBar_Label", "MOOD", 13, Color.white, TextAlignmentOptions.Left);
        ApplyHudTextStyle(moodLabel);
        SetupLabelRect(moodLabel.rectTransform, new Vector2(-60f, 0f), new Vector2(55f, 20f));

        RectTransform caloriesPanel = EnsurePanel(canvas.transform, "Calories_Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -130f), new Vector2(250f, 25f));
        TextMeshProUGUI caloriesText = EnsureText(caloriesPanel, "Calories_Text", "Kalori: 0 / 2000 kcal", 13, new Color32(200, 200, 200, 200), TextAlignmentOptions.Left);
        ApplyHudTextStyle(caloriesText);
        StretchRect(caloriesText.rectTransform);

        RectTransform timePanel = EnsurePanel(canvas.transform, "Time_Panel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 60f));
        TextMeshProUGUI periodText = EnsureText(timePanel, "Period_Text", "Pagi", 22, Color.white, TextAlignmentOptions.Center);
        SetTextStyle(periodText, FontStyles.Bold);
        ApplyHudTextStyle(periodText);
        SetupSubTextRect(periodText.rectTransform, new Vector2(0f, -12f), new Vector2(300f, 28f));
        TextMeshProUGUI timeText = EnsureText(timePanel, "TimeRemaining_Text", "05:00", 18, new Color32(255, 240, 150, 255), TextAlignmentOptions.Center);
        ApplyHudTextStyle(timeText);
        SetupSubTextRect(timeText.rectTransform, new Vector2(0f, -38f), new Vector2(300f, 24f));

        RectTransform warningPanel = EnsurePanel(canvas.transform, "Warning_Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(500f, 60f));
        TextMeshProUGUI warningText = EnsureText(warningPanel, "Warning_Text", "ENERGI RENDAH! Segera makan!", 24, new Color32(255, 140, 0, 255), TextAlignmentOptions.Center);
        SetTextStyle(warningText, FontStyles.Bold);
        ApplyHudTextStyle(warningText);
        StretchRect(warningText.rectTransform);
        warningPanel.gameObject.SetActive(false);

        HUDManager hudManager = EnsureHUDManager();
        WireHUDManager(hudManager, energyFill, energyWarning, warningPanel.gameObject, moodFill, caloriesText, periodText, timeText);

        if (Application.isPlaying)
            EnsureTutorialSystems(hudManager.gameObject);

        EnsureSkyboxTintController();
    }

    private Canvas EnsureCanvas()
    {
        GameObject obj = GameObject.Find(CanvasName);
        if (obj == null)
            obj = new GameObject(CanvasName, typeof(RectTransform));

        Canvas canvas = obj.GetComponent<Canvas>();
        if (canvas == null) canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (obj.GetComponent<GraphicRaycaster>() == null)
            obj.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private RectTransform EnsurePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject panelObj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (panelObj.transform.parent != parent)
            panelObj.transform.SetParent(parent, false);

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image EnsureImage(RectTransform parent, string name, Color color, bool stretch, float fillAmount)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (obj.transform.parent != parent)
            obj.transform.SetParent(parent, false);

        Image img = obj.GetComponent<Image>();
        if (img == null) img = obj.AddComponent<Image>();
        img.color = color;
        img.fillAmount = fillAmount;

        RectTransform rt = img.rectTransform;
        if (stretch)
        {
            StretchRect(rt);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
        return img;
    }

    private TextMeshProUGUI EnsureText(RectTransform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (obj.transform.parent != parent)
            obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        return tmp;
    }

    private void SetupFillImage(Image img, float fill)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = fill;
    }

    private void SetupLabelRect(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void SetupSubTextRect(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void StretchRect(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private void SetTextStyle(TextMeshProUGUI text, FontStyles style)
    {
        text.fontStyle = style;
    }

    private void ApplyHudTextStyle(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.outlineColor = new Color(0f, 0f, 0f, 200f / 255f);
        text.outlineWidth = 0.1f;
    }

    private HUDManager EnsureHUDManager()
    {
        GameObject hudObj = GameObject.Find("HUDManager");
        if (hudObj == null)
            hudObj = new GameObject("HUDManager");

        HUDManager manager = hudObj.GetComponent<HUDManager>();
        if (manager == null)
            manager = hudObj.AddComponent<HUDManager>();

        return manager;
    }

    private void WireHUDManager(HUDManager manager, Image energyFill, Image energyWarn, GameObject warningPanel, Image moodFill, TextMeshProUGUI calories, TextMeshProUGUI period, TextMeshProUGUI timeRemaining)
    {
        SetPrivateField(manager, "energyBarFill", energyFill);
        SetPrivateField(manager, "energyBarWarningFill", energyWarn);
        SetPrivateField(manager, "warningPanel", warningPanel);
        SetPrivateField(manager, "moodBarFill", moodFill);
        SetPrivateField(manager, "caloriesText", calories);
        SetPrivateField(manager, "periodText", period);
        SetPrivateField(manager, "timeRemainingText", timeRemaining);
    }

    private void EnsureSkyboxTintController()
    {
        GameObject gameManager = GameObject.Find("GameManager");
        if (gameManager == null) return;

        if (gameManager.GetComponent<SkyboxTintController>() == null)
            gameManager.AddComponent<SkyboxTintController>();
    }

    private void EnsureTutorialSystems(GameObject target)
    {
        if (target == null)
            return;

        if (target.GetComponent<TutorialSequentialUI>() == null)
            target.AddComponent<TutorialSequentialUI>();

        if (target.GetComponent<TutorialContextualUI>() == null)
            target.AddComponent<TutorialContextualUI>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);
    }
}
