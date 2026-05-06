#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StorySimV2Setup
{
    private const string FoodItemPrefabPath = "Assets/_Project/Prefabs/UI/FoodListItem.prefab";
    private const string DialogueAssetPath = "Assets/_Project/Data/Dialogues/NPCUtama_Dialogue.asset";

    [MenuItem("Tools/HealthySim/StorySimV2/Setup Scene + UI")]
    public static void SetupSceneAndUi()
    {
        EnsureFolders();

        Canvas hudCanvas = EnsureHudCanvas();
        EnsureEventSystem();

        FoodListItemView foodListPrefab = EnsureFoodListItemPrefab();
        SetupFoodPanel(hudCanvas.transform, foodListPrefab);
        SetupStashPanel(hudCanvas.transform);
        SetupNpcDialoguePanel(hudCanvas.transform);

        SetupProvidersAndDialogueAsset();
        WireControllers();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[StorySimV2Setup] Scene dan UI berhasil dikonfigurasi.");
    }

    [MenuItem("Tools/HealthySim/StorySimV2/Create Food List Prefab")]
    public static void CreateFoodListPrefabOnly()
    {
        EnsureFolders();
        EnsureFoodListItemPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StorySimV2Setup] FoodListItem prefab siap.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Dialogues"))
            AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogues");
    }

    private static Canvas EnsureHudCanvas()
    {
        GameObject hud = GameObject.Find("HUD_Canvas");
        if (hud == null)
            hud = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static FoodListItemView EnsureFoodListItemPrefab()
    {
        FoodListItemView existing = AssetDatabase.LoadAssetAtPath<FoodListItemView>(FoodItemPrefabPath);
        if (existing != null)
            return existing;

        GameObject root = new GameObject("FoodListItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(FoodListItemView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(804f, 92f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color32(42, 42, 48, 255);

        Button rowButton = root.GetComponent<Button>();
        ColorBlock colors = rowButton.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        rowButton.colors = colors;

        RectTransform healthStrip = CreateUiRect("HealthBarStrip", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(8f, 92f));
        Image healthStripImage = healthStrip.gameObject.AddComponent<Image>();
        healthStripImage.color = new Color(0.42f, 0.85f, 0.38f, 1f);

        RectTransform icon = CreateUiRect("Icon", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(60f, 60f));
        icon.gameObject.AddComponent<Image>();

        TextMeshProUGUI nameText = CreateTmp("Name", root.transform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(96f, -8f), new Vector2(-250f, 36f), 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        TextMeshProUGUI metaText = CreateTmp("Meta", root.transform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(96f, -44f), new Vector2(-250f, 22f), 16f, FontStyles.Normal, new Color(0.86f, 0.86f, 0.9f, 1f), TextAlignmentOptions.Left);
        TextMeshProUGUI statText = CreateTmp("Stat", root.transform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(96f, 10f), new Vector2(-250f, 24f), 16f, FontStyles.Normal, new Color(0.76f, 0.9f, 0.3f, 1f), TextAlignmentOptions.Left);

        RectTransform selectButtonRect = CreateUiRect("SelectButton", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Button selectButton = selectButtonRect.gameObject.AddComponent<Button>();
        Image selectImage = selectButtonRect.gameObject.AddComponent<Image>();
        selectImage.color = new Color(1f, 1f, 1f, 0f);

        FoodListItemView view = root.GetComponent<FoodListItemView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("healthBarStrip").objectReferenceValue = healthStripImage;
        so.FindProperty("iconImage").objectReferenceValue = icon.GetComponent<Image>();
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("metaText").objectReferenceValue = metaText;
        so.FindProperty("statText").objectReferenceValue = statText;
        so.FindProperty("selectButton").objectReferenceValue = selectButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabRoot = PrefabUtility.SaveAsPrefabAsset(root, FoodItemPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        return prefabRoot.GetComponent<FoodListItemView>();
    }

    private static void SetupFoodPanel(Transform canvas, FoodListItemView itemPrefab)
    {
        RectTransform panel = EnsurePanel("FoodSelection_Panel", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 620f), new Color32(18, 18, 22, 238));
        panel.gameObject.SetActive(false);
        RemoveExtraChildren(panel, "FoodSelection_Header");
        RemoveExtraChildren(panel, "FilterRow");
        RemoveExtraChildren(panel, "FoodListArea");
        RemoveExtraChildren(panel, "DetailPanel");

        RectTransform header = EnsurePanel("FoodSelection_Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 76f), new Color32(24, 24, 30, 255));
        TextMeshProUGUI title = CreateTmp("Title", header, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-90f, 0f), 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        title.text = "Pilih Makanan";

        Button stashButton = EnsureButton("OpenStashButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-126f, 0f), new Vector2(106f, 52f), "Stash", 16f);
        Button closeButton = EnsureButton("CloseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(52f, 52f), "X", 28f);

        RectTransform filterRow = EnsurePanel("FilterRow", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(820f, 44f), new Color32(0, 0, 0, 0));
        TMP_Dropdown categoryDropdown = EnsureDropdown("CategoryDropdown", filterRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 44f));
        TMP_Dropdown sortDropdown = EnsureDropdown("SortDropdown", filterRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(238f, 0f), new Vector2(220f, 44f));
        TMP_InputField searchInput = EnsureInput("SearchInput", filterRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(280f, 44f), "Cari makanan");

        RectTransform listViewport = EnsureScrollView(panel, out RectTransform content);
        RectTransform detailPanel = EnsurePanel("DetailPanel", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(820f, 130f), new Color32(26, 26, 33, 255));

        TextMeshProUGUI detailText = CreateTmp("DetailText", detailPanel, new Vector2(0f, 0.36f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(-20f, -12f), 18f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
        detailText.text = "Nama: -";

        TextMeshProUGUI stashStatus = CreateTmp("StashStatus", detailPanel, new Vector2(0f, 0.04f), new Vector2(0.58f, 0.28f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-10f, 0f), 16f, FontStyles.Normal, new Color32(208, 224, 255, 255), TextAlignmentOptions.Left);
        stashStatus.text = "Simpanan saat ini: 0 item";

        RectTransform buttonRow = EnsurePanel("ButtonRow", detailPanel, new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(792f, 42f), new Color32(0, 0, 0, 0));
        Button confirmButton = EnsureButton("ConfirmButton", buttonRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(256f, 38f), "Makan Sekarang", 15f);
        Button saveButton = EnsureButton("SaveButton", buttonRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(268f, 0f), new Vector2(256f, 38f), "Simpan Nanti", 15f);
        Button cancelButton = EnsureButton("CancelButton", buttonRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(536f, 0f), new Vector2(256f, 38f), "Batal", 15f);

        GameObject manager = EnsureManagerObject();
        FoodChoiceMenuController controller = manager.GetComponent<FoodChoiceMenuController>();
        if (controller == null)
            controller = manager.AddComponent<FoodChoiceMenuController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("foodSelectionPanel").objectReferenceValue = panel;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("categoryDropdown").objectReferenceValue = categoryDropdown;
        so.FindProperty("sortDropdown").objectReferenceValue = sortDropdown;
        so.FindProperty("searchInput").objectReferenceValue = searchInput;
        so.FindProperty("listContent").objectReferenceValue = content;
        so.FindProperty("listItemPrefab").objectReferenceValue = itemPrefab;
        so.FindProperty("detailText").objectReferenceValue = detailText;
        so.FindProperty("confirmEatButton").objectReferenceValue = confirmButton;
        so.FindProperty("saveForLaterButton").objectReferenceValue = saveButton;
        so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
        so.FindProperty("openStashButton").objectReferenceValue = stashButton;
        so.FindProperty("stashStatusText").objectReferenceValue = stashStatus;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupStashPanel(Transform canvas)
    {
        RectTransform panel = EnsurePanel("FoodStash_Panel", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(760f, 520f), new Color32(14, 18, 26, 240));
        panel.gameObject.SetActive(false);
        RemoveExtraChildren(panel, "Header");
        RemoveExtraChildren(panel, "Explanation");
        RemoveExtraChildren(panel, "ListArea");
        RemoveExtraChildren(panel, "Footer");

        RectTransform header = EnsurePanel("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 64f), new Color32(30, 36, 48, 255));
        TextMeshProUGUI headerText = CreateTmp("HeaderText", header, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(-140f, 0f), 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        headerText.text = "Simpanan Makanan";

        Button closeButton = EnsureButton("CloseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(108f, 42f), "Tutup", 16f);

        TextMeshProUGUI explanationText = CreateTmp("Explanation", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -80f), new Vector2(-32f, 76f), 18f, FontStyles.Normal, new Color32(220, 230, 240, 255), TextAlignmentOptions.Left);
        explanationText.text = "Simpanan berlaku untuk sesi permainan aktif. Klik item untuk konsumsi langsung, atau hapus jika tidak diperlukan.";

        RectTransform listRoot = EnsurePanel("ListArea", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(712f, 292f), new Color32(26, 30, 42, 255));
        VerticalLayoutGroup listLayout = listRoot.GetComponent<VerticalLayoutGroup>();
        if (listLayout == null)
            listLayout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(12, 12, 12, 12);
        listLayout.spacing = 10f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandHeight = false;

        Button rowPrefab = EnsureChoiceCardPrefab(listRoot);
        rowPrefab.gameObject.name = "StashRowTemplate";
        rowPrefab.gameObject.SetActive(false);

        RectTransform footer = EnsurePanel("Footer", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 62f), new Color32(22, 26, 36, 255));
        Button clearButton = EnsureButton("ClearButton", footer, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(162f, 40f), "Hapus Semua", 16f);

        GameObject manager = EnsureManagerObject();
        FoodStashMenuController controller = manager.GetComponent<FoodStashMenuController>();
        if (controller == null)
            controller = manager.AddComponent<FoodStashMenuController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("stashPanel").objectReferenceValue = panel;
        so.FindProperty("headerText").objectReferenceValue = headerText;
        so.FindProperty("explanationText").objectReferenceValue = explanationText;
        so.FindProperty("listContainer").objectReferenceValue = listRoot;
        so.FindProperty("rowButtonPrefab").objectReferenceValue = rowPrefab;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("clearButton").objectReferenceValue = clearButton;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupNpcDialoguePanel(Transform canvas)
    {
        RectTransform panel = EnsurePanel("NPCDialogue_Panel", canvas, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 300f), new Color32(10, 10, 14, 230));
        panel.gameObject.SetActive(false);
        RemoveExtraChildren(panel, "NPC_Name");
        RemoveExtraChildren(panel, "NPC_Dialogue");
        RemoveExtraChildren(panel, "ChoiceContainer");
        RemoveExtraChildren(panel, "CloseDialogue");

        TextMeshProUGUI nameText = CreateTmp("NPC_Name", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(-140f, 36f), 26f, FontStyles.Bold, new Color32(255, 216, 74, 255), TextAlignmentOptions.Left);
        nameText.text = "NPC";

        TextMeshProUGUI lineText = CreateTmp("NPC_Dialogue", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -64f), new Vector2(-40f, 84f), 22f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
        TextMeshProUGUI feedbackText = CreateTmp("NPC_Feedback", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(28f, 152f), new Vector2(-52f, 30f), 16f, FontStyles.Italic, new Color32(176, 220, 255, 255), TextAlignmentOptions.Left);

        RectTransform container = EnsurePanel("ChoiceContainer", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(780f, 132f), new Color32(0, 0, 0, 0));
        VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(10, 10, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = container.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button choiceCardPrefab = EnsureChoiceCardPrefab(container);
        Button closeButton = EnsureButton("CloseDialogue", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -18f), new Vector2(96f, 40f), "Tutup", 16f);

        GameObject manager = EnsureManagerObject();
        NpcDialogueMenuController controller = manager.GetComponent<NpcDialogueMenuController>();
        if (controller == null)
            controller = manager.AddComponent<NpcDialogueMenuController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("dialoguePanel").objectReferenceValue = panel;
        so.FindProperty("npcNameText").objectReferenceValue = nameText;
        so.FindProperty("dialogueText").objectReferenceValue = lineText;
        so.FindProperty("choiceContainer").objectReferenceValue = container;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("choiceCardPrefab").objectReferenceValue = choiceCardPrefab;
        so.FindProperty("feedbackText").objectReferenceValue = feedbackText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button EnsureChoiceCardPrefab(Transform parent)
    {
        Transform existing = parent.Find("ChoiceCardTemplate");
        GameObject card = existing != null ? existing.gameObject : new GameObject("ChoiceCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(760f, 52f);

        Image bg = card.GetComponent<Image>();
        bg.color = new Color32(40, 42, 50, 255);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredHeight = 52f;

        Button button = card.GetComponent<Button>();
        ColorBlock cb = button.colors;
        cb.normalColor = new Color32(40, 42, 50, 255);
        cb.highlightedColor = new Color32(76, 78, 94, 255);
        cb.pressedColor = new Color32(96, 98, 120, 255);
        button.colors = cb;

        TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
            label = CreateTmp("Label", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(-20f, 0f), 20f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);

        label.text = "Pilihan";
        card.SetActive(false);
        return button;
    }

    private static void SetupProvidersAndDialogueAsset()
    {
        GameObject manager = EnsureManagerObject();

        FoodCatalogProvider foodProvider = manager.GetComponent<FoodCatalogProvider>();
        if (foodProvider == null)
            foodProvider = manager.AddComponent<FoodCatalogProvider>();
        EditorUtility.SetDirty(foodProvider);

        DialogueCatalogProvider dialogueProvider = manager.GetComponent<DialogueCatalogProvider>();
        if (dialogueProvider == null)
            dialogueProvider = manager.AddComponent<DialogueCatalogProvider>();

        DialogueGraphData graph = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(DialogueAssetPath);
        if (graph == null)
        {
            graph = ScriptableObject.CreateInstance<DialogueGraphData>();
            graph.npcId = "npc_utama";
            graph.npcDisplayName = "Warga Kota";
            graph.initialTrust = 50f;
            graph.startNodeId = "start";
            graph.nodes = BuildDefaultNodes();
            AssetDatabase.CreateAsset(graph, DialogueAssetPath);
        }

        SerializedObject dso = new SerializedObject(dialogueProvider);
        SerializedProperty prop = dso.FindProperty("dialogues");
        bool exists = false;
        for (int i = 0; i < prop.arraySize; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue == graph)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = graph;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        NpcDialogueInteractable npc = Object.FindFirstObjectByType<NpcDialogueInteractable>();
        if (npc != null)
            EditorUtility.SetDirty(npc);
    }

    private static List<DialogueNodeData> BuildDefaultNodes()
    {
        DialogueNodeData start = new DialogueNodeData
        {
            nodeId = "start",
            fallbackLine = "Kamu tampak ragu. Mau pilih jalur sehat hari ini?",
            variationPool = new List<string>
            {
                "Kamu tampak ragu. Mau pilih jalur sehat hari ini?",
                "Kondisimu cukup baik. Mau lanjutkan kebiasaan sehat?",
                "Waktumu sempit, tapi masih bisa memilih lebih baik."
            },
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData
                {
                    choiceText = "(Suportif) Ya, saya ingin menjaga kebiasaan sehat.",
                    nextNodeId = "supportive",
                    consequence = new DialogueConsequence { moodDelta = 4f, trustDelta = 8f, trackerAction = PlayerActionTracker.ActionType.PositiveNpcTalk },
                    gate = new DialogueCondition { useGate = false }
                },
                new DialogueChoiceData
                {
                    choiceText = "(Netral) Saya lihat dulu situasinya.",
                    nextNodeId = "neutral",
                    consequence = new DialogueConsequence { moodDelta = 1f, trustDelta = 2f, trackerAction = PlayerActionTracker.ActionType.GenericInteraction },
                    gate = new DialogueCondition { useGate = false }
                },
                new DialogueChoiceData
                {
                    choiceText = "(Resisten) Tidak, saya pilih yang instan saja.",
                    nextNodeId = "resistant",
                    consequence = new DialogueConsequence { moodDelta = -3f, trustDelta = -7f, trackerAction = PlayerActionTracker.ActionType.NegativeNpcTalk },
                    gate = new DialogueCondition { useGate = false }
                }
            }
        };

        DialogueNodeData supportive = new DialogueNodeData
        {
            nodeId = "supportive",
            fallbackLine = "Bagus. Mulai dari satu pilihan makan yang lebih baik sekarang.",
            isTerminal = true,
            choices = new List<DialogueChoiceData>()
        };

        DialogueNodeData neutral = new DialogueNodeData
        {
            nodeId = "neutral",
            fallbackLine = "Tidak apa-apa. Pilih satu langkah kecil dulu supaya ritmemu tetap stabil.",
            isTerminal = true,
            choices = new List<DialogueChoiceData>()
        };

        DialogueNodeData resistant = new DialogueNodeData
        {
            nodeId = "resistant",
            fallbackLine = "Oke, tapi konsekuensinya energimu bisa cepat turun nanti.",
            isTerminal = true,
            choices = new List<DialogueChoiceData>()
        };

        return new List<DialogueNodeData> { start, supportive, neutral, resistant };
    }

    private static void WireControllers()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<UniversalInteractionController>() == null)
            player.AddComponent<UniversalInteractionController>();

        GameObject manager = EnsureManagerObject();
        if (manager.GetComponent<FoodChoiceMenuController>() == null)
            manager.AddComponent<FoodChoiceMenuController>();

        if (manager.GetComponent<FoodStashMenuController>() == null)
            manager.AddComponent<FoodStashMenuController>();

        if (manager.GetComponent<NpcDialogueMenuController>() == null)
            manager.AddComponent<NpcDialogueMenuController>();

        if (manager.GetComponent<SessionFoodStash>() == null)
            manager.AddComponent<SessionFoodStash>();

        if (manager.GetComponent<StoryManager>() == null)
            manager.AddComponent<StoryManager>();

        if (manager.GetComponent<ModalStateManager>() == null)
            manager.AddComponent<ModalStateManager>();

        if (manager.GetComponent<ModalUIStateManager>() == null)
            manager.AddComponent<ModalUIStateManager>();
    }

    private static GameObject EnsureManagerObject()
    {
        GameObject manager = GameObject.Find("GameManager");
        if (manager == null)
            manager = new GameObject("GameManager");

        return manager;
    }

    private static RectTransform EnsureScrollView(Transform parent, out RectTransform content)
    {
        RectTransform root = EnsurePanel("FoodListArea", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -314f), new Vector2(820f, 340f), new Color32(23, 23, 29, 255));
        ScrollRect scrollRect = root.GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = root.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = EnsurePanel("Viewport", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f), new Color32(0, 0, 0, 0));
        Mask mask = viewport.GetComponent<Mask>();
        if (mask == null)
            mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

        content = EnsurePanel("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f), new Color32(0, 0, 0, 0));

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        return root;
    }

    private static RectTransform EnsurePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color32 bgColor)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image image = obj.GetComponent<Image>();
        if (image != null)
            image.color = bgColor;

        return rect;
    }

    private static RectTransform CreateUiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI CreateTmp(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        Transform existing = parent.Find(name);
        RectTransform rect;
        if (existing != null)
        {
            rect = existing.GetComponent<RectTransform>();
        }
        else
        {
            rect = CreateUiRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = rect.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();

        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void RemoveExtraChildren(Transform parent, string childName)
    {
        List<Transform> matches = new List<Transform>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                matches.Add(child);
        }

        for (int i = 1; i < matches.Count; i++)
            Object.DestroyImmediate(matches[i].gameObject);
    }

    private static Button EnsureButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string label, float fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image bg = obj.GetComponent<Image>();
        bg.color = new Color32(52, 56, 72, 255);

        Button button = obj.GetComponent<Button>();

        TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (txt == null)
            txt = CreateTmp("Label", obj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        txt.text = label;
        return button;
    }

    private static TMP_Dropdown EnsureDropdown(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image bg = obj.GetComponent<Image>();
        bg.color = new Color32(42, 42, 48, 255);

        TMP_Dropdown dropdown = obj.GetComponent<TMP_Dropdown>();
        TextMeshProUGUI label = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null)
            label = CreateTmp("Label", obj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-30f, 0f), 18f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);

        dropdown.captionText = label;
        return dropdown;
    }

    private static TMP_InputField EnsureInput(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string placeholderText)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image bg = obj.GetComponent<Image>();
        bg.color = new Color32(42, 42, 48, 255);

        TMP_InputField input = obj.GetComponent<TMP_InputField>();

        TextMeshProUGUI text = obj.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = CreateTmp("Text", obj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-20f, 0f), 18f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);

        TextMeshProUGUI placeholder = obj.transform.Find("Placeholder")?.GetComponent<TextMeshProUGUI>();
        if (placeholder == null)
            placeholder = CreateTmp("Placeholder", obj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-20f, 0f), 18f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.75f, 1f), TextAlignmentOptions.Left);

        placeholder.text = placeholderText;
        input.textComponent = text;
        input.placeholder = placeholder;

        return input;
    }
}
#endif
