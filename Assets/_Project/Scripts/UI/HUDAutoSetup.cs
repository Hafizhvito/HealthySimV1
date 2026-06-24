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

        RemoveChildPanelIfExists(canvas.transform, "MoodBar_Panel");

        RectTransform energyPanel = EnsurePanel(canvas.transform, "EnergyBar_Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -32f), new Vector2(420f, 52f));
        ApplyPanelChrome(energyPanel, new Color32(10, 14, 20, 225));

        Image energyBg = EnsureImage(energyPanel, "EnergyBar_BG", new Color32(34, 40, 50, 255), true, 1f);
        ConfigureBarArea(energyBg.rectTransform, 4f, 24f);
        Image energyFill = EnsureImage(energyPanel, "EnergyBar_Fill", new Color32(88, 210, 104, 255), false, 1f);
        ConfigureBarArea(energyFill.rectTransform, 6f, 24f);
        Image energyWarning = EnsureImage(energyPanel, "EnergyBar_Warning_Fill", new Color32(255, 255, 255, 90), false, 0f);
        ConfigureBarArea(energyWarning.rectTransform, 6f, 24f);
        energyBg.rectTransform.SetSiblingIndex(0);
        energyFill.rectTransform.SetSiblingIndex(1);
        energyWarning.rectTransform.SetSiblingIndex(2);
        energyFill.type = Image.Type.Filled;
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillOrigin = 0;
        energyFill.fillAmount = 1f;
        SetupFillImage(energyFill, 1f);
        SetupFillImage(energyWarning, 0f);

        TextMeshProUGUI energyLabel = EnsureText(energyPanel, "EnergyBar_Label", "Energi", 18, new Color32(245, 248, 252, 255), TextAlignmentOptions.Left);
        ApplyHudTextStyle(energyLabel, 0.18f);
        SetupLabelRect(energyLabel.rectTransform, new Vector2(12f, -2f), new Vector2(104f, 24f));
        energyLabel.fontStyle = FontStyles.Bold;

        TextMeshProUGUI energyValue = EnsureText(energyPanel, "EnergyBar_Value", "100%", 18, new Color32(245, 248, 252, 255), TextAlignmentOptions.Right);
        ApplyHudTextStyle(energyValue, 0.18f);
        SetupValueRect(energyValue.rectTransform, new Vector2(-12f, -2f), new Vector2(80f, 24f));
        energyValue.fontStyle = FontStyles.Bold;

        RectTransform caloriesPanel = EnsurePanel(canvas.transform, "Calories_Panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -92f), new Vector2(420f, 48f));
        ApplyPanelChrome(caloriesPanel, new Color32(10, 14, 20, 225));

        Image caloriesBg = EnsureImage(caloriesPanel, "Calories_BG", new Color32(34, 40, 50, 255), true, 1f);
        ConfigureBarArea(caloriesBg.rectTransform, 4f, 4f);
        caloriesBg.type = Image.Type.Simple;
        Image caloriesFill = EnsureImage(caloriesPanel, "Calories_Fill", new Color32(88, 210, 104, 0), false, 0f);
        ConfigureBarArea(caloriesFill.rectTransform, 6f, 6f);
        SetupFillImage(caloriesFill, 0f);
        caloriesFill.fillAmount = 0f;
        caloriesFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        caloriesFill.rectTransform.localScale = Vector3.one;
        caloriesBg.rectTransform.SetSiblingIndex(0);
        caloriesFill.rectTransform.SetSiblingIndex(1);

        TextMeshProUGUI caloriesText = EnsureText(caloriesPanel, "Calories_Text", "Kalori 0 / 2100 KCAL", 20, new Color32(238, 242, 248, 255), TextAlignmentOptions.Center);
        ApplyHudTextStyle(caloriesText, 0.20f);
        SetupInfoTextRect(caloriesText.rectTransform, 0f, 0f);
        caloriesText.rectTransform.anchorMin = Vector2.zero;
        caloriesText.rectTransform.anchorMax = Vector2.one;
        caloriesText.rectTransform.offsetMin = new Vector2(10f, 5f);
        caloriesText.rectTransform.offsetMax = new Vector2(-10f, -5f);
        caloriesText.rectTransform.SetSiblingIndex(2);
        caloriesText.fontStyle = FontStyles.Bold;

        RectTransform timePanel = EnsurePanel(canvas.transform, "Time_Panel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(340f, 72f));
        ApplyPanelChrome(timePanel, new Color32(10, 14, 20, 225));
        TextMeshProUGUI periodText = EnsureText(timePanel, "Period_Text", "Pagi", 22, Color.white, TextAlignmentOptions.Center);
        SetTextStyle(periodText, FontStyles.Bold);
        ApplyHudTextStyle(periodText, 0.16f);
        SetupSubTextRect(periodText.rectTransform, new Vector2(0f, -14f), new Vector2(320f, 30f));
        TextMeshProUGUI timeText = EnsureText(timePanel, "TimeRemaining_Text", "06:00", 18, new Color32(255, 240, 150, 255), TextAlignmentOptions.Center);
        ApplyHudTextStyle(timeText, 0.16f);
        SetupSubTextRect(timeText.rectTransform, new Vector2(0f, -42f), new Vector2(320f, 26f));

        RectTransform warningPanel = EnsurePanel(canvas.transform, "Warning_Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(500f, 60f));
        TextMeshProUGUI warningText = EnsureText(warningPanel, "Warning_Text", "ENERGI RENDAH! Segera makan!", 28, new Color32(255, 140, 0, 255), TextAlignmentOptions.Center);
        SetTextStyle(warningText, FontStyles.Bold);
        ApplyHudTextStyle(warningText);
        StretchRect(warningText.rectTransform);
        warningPanel.gameObject.SetActive(false);

        HUDManager hudManager = EnsureHUDManager();
        WireHUDManager(hudManager, energyFill, energyWarning, warningPanel.gameObject, null, caloriesFill, caloriesText, periodText, timeText, energyValue);

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
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
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

    private void SetupInfoTextRect(RectTransform rt, float leftPadding, float yOffset)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftPadding, yOffset);
        rt.sizeDelta = new Vector2(-leftPadding - 8f, 24f);
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

    private void SetupValueRect(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void ConfigureInsetBar(RectTransform rt, float horizontalInset, float verticalInset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(horizontalInset, verticalInset);
        rt.offsetMax = new Vector2(-horizontalInset, -verticalInset);
    }

    private void ConfigureBarArea(RectTransform rt, float horizontalInset, float topReserved)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(horizontalInset, 6f);
        rt.offsetMax = new Vector2(-horizontalInset, -topReserved);
    }

    private void ApplyPanelChrome(RectTransform panel, Color32 fillColor)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null)
            image = panel.gameObject.AddComponent<Image>();
        image.color = fillColor;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
            outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.14f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void ApplyHudTextStyle(TextMeshProUGUI text, float outlineWidth = 0.12f)
    {
        if (text == null)
            return;

        text.outlineColor = new Color(0f, 0f, 0f, 0.88f);
        text.outlineWidth = outlineWidth;
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

    private void WireHUDManager(HUDManager manager, Image energyFill, Image energyWarn, GameObject warningPanel, Image moodFill, Image caloriesFill, TextMeshProUGUI calories, TextMeshProUGUI period, TextMeshProUGUI timeRemaining, TextMeshProUGUI energyValue)
    {
        SetPrivateField(manager, "energyBarFill", energyFill);
        SetPrivateField(manager, "energyBarWarningFill", energyWarn);
        SetPrivateField(manager, "energyValueText", energyValue);
        SetPrivateField(manager, "warningPanel", warningPanel);
        SetPrivateField(manager, "moodBarFill", moodFill);
        SetPrivateField(manager, "caloriesBarFill", caloriesFill);
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

    private void RemoveChildPanelIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);
    }
}
