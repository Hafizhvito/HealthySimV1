using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Syncs FoodData catalog to icons in Assets/_Project/Art/FoodIcons.
/// Run via HealthySim/Food/Sync Catalog From Food Icons.
/// </summary>
public static class FoodIconCatalogSync
{
    private const string IconFolder = "Assets/_Project/Art/FoodIcons";
    private const string FoodFolder = "Assets/_Project/Data/Foods";

    private readonly struct FoodIconEntry
    {
        public readonly string IconFileName;
        public readonly string AssetName;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly float Calories;
        public readonly float Energy;
        public readonly float Mood;
        public readonly float Fat;
        public readonly float Protein;
        public readonly float Carbohydrate;
        public readonly float Sugar;
        public readonly FoodData.FoodCategory Category;
        public readonly bool IsHealthy;
        public readonly bool Morning;
        public readonly bool Afternoon;
        public readonly bool Evening;
        public readonly bool Night;
        public readonly int Price;

        public FoodIconEntry(
            string iconFileName,
            string assetName,
            string displayName,
            string description,
            float calories,
            float energy,
            float mood,
            float fat,
            float protein,
            float carbohydrate,
            float sugar,
            FoodData.FoodCategory category,
            bool isHealthy,
            bool morning,
            bool afternoon,
            bool evening,
            bool night,
            int price)
        {
            IconFileName = iconFileName;
            AssetName = assetName;
            DisplayName = displayName;
            Description = description;
            Calories = calories;
            Energy = energy;
            Mood = mood;
            Fat = fat;
            Protein = protein;
            Carbohydrate = carbohydrate;
            Sugar = sugar;
            Category = category;
            IsHealthy = isHealthy;
            Morning = morning;
            Afternoon = afternoon;
            Evening = evening;
            Night = night;
            Price = price;
        }
    }

    // Nutrition approximations aligned with typical Indonesian portion references (DKBM-style, per serving).
    private static readonly FoodIconEntry[] Catalog =
    {
        // --- Healthy ---
        new FoodIconEntry("Air_Mineral.jpg", "Healthy_AirMineral", "Air Mineral", "Hidrasi tanpa kalori.", 0f, 3f, 1f, 0f, 0f, 0f, 0f, FoodData.FoodCategory.Minuman, true, true, true, true, true, 5),
        new FoodIconEntry("gado-gado.jpg", "Healthy_GadoGado", "Gado-Gado", "Sayuran rebus dengan bumbu kacang, serat dan protein nabati.", 320f, 18f, 7f, 12f, 12f, 35f, 6f, FoodData.FoodCategory.MakananBerat, true, false, true, true, false, 28),
        new FoodIconEntry("jusjeruk.jpg", "Healthy_JusJerukSegar", "Jus Jeruk Segar", "Vitamin C alami tanpa pemanis tambahan berlebihan.", 110f, 10f, 5f, 0.5f, 1.5f, 26f, 22f, FoodData.FoodCategory.Minuman, true, true, true, true, true, 14),
        new FoodIconEntry("NasiMerah_AyamPanggang.jpg", "Healthy_NasiMerahAyamPanggang", "Nasi Merah + Ayam Panggang", "Karbo kompleks dan protein tanpa gorengan.", 430f, 24f, 6f, 10f, 28f, 55f, 2f, FoodData.FoodCategory.MakananBerat, true, true, true, true, false, 32),
        new FoodIconEntry("Nasi_Merah_Tempe.jpg", "Healthy_NasiMerahTempe", "Nasi Merah Tempe", "Nasi merah dengan tempe, protein nabati dan serat.", 380f, 20f, 5f, 12f, 18f, 52f, 2f, FoodData.FoodCategory.MakananBerat, true, true, true, true, false, 26),
        new FoodIconEntry("Pecel_Madiun.jpg", "Healthy_PecelMadiun", "Pecel Madiun", "Sayuran segar dengan sambal kacang khas.", 290f, 16f, 6f, 10f, 10f, 38f, 5f, FoodData.FoodCategory.MakananBerat, true, false, true, true, false, 24),
        new FoodIconEntry("Pepes_Ikan.jpg", "Healthy_PepesIkan", "Pepes Ikan", "Ikan kukus dalam daun, rendah lemak jenuh.", 250f, 17f, 6f, 8f, 24f, 18f, 1f, FoodData.FoodCategory.MakananBerat, true, false, true, true, false, 30),
        new FoodIconEntry("Soto_Ayam_Bening.jpg", "Healthy_SotoAyamBening", "Soto Ayam Bening", "Kuah bening dengan protein ayam dan sayuran.", 210f, 15f, 5f, 6f, 16f, 22f, 2f, FoodData.FoodCategory.MakananBerat, true, true, true, true, false, 26),
        new FoodIconEntry("Sup_Jagung.jpg", "Healthy_SupJagung", "Sup Jagung", "Sup jagung dengan sayuran, porsi sedang.", 190f, 13f, 4f, 7f, 6f, 28f, 4f, FoodData.FoodCategory.MakananRingan, true, false, true, true, true, 20),
        new FoodIconEntry("Sup_Sayur.jpg", "Healthy_SupSayur", "Sup Sayur", "Sup sayuran ringan, rendah kalori.", 120f, 12f, 4f, 3f, 5f, 18f, 3f, FoodData.FoodCategory.MakananRingan, true, false, true, true, true, 18),
        new FoodIconEntry("Es_Teh_Tawar.jpg", "Healthy_EsTehTawar", "Es Teh Tawar", "Teh tanpa gula, hidrasi ringan.", 5f, 4f, 2f, 0f, 0f, 1f, 0f, FoodData.FoodCategory.Minuman, true, true, true, true, true, 6),
        new FoodIconEntry("Tumis_Kangkung_Tahu.jpg", "Healthy_TumisKangkungTofu", "Tumis Kangkung Tahu", "Sayuran hijau dan tahu, rendah kalori.", 200f, 14f, 5f, 9f, 12f, 20f, 3f, FoodData.FoodCategory.MakananBerat, true, false, true, true, false, 22),

        // --- Less healthy ---
        new FoodIconEntry("Ayam_Goreng.jpg", "LessHealthy_AyamGoreng", "Ayam Goreng", "Gorengan tinggi lemak jenuh dan kalori.", 450f, 18f, 6f, 28f, 22f, 18f, 1f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 28),
        new FoodIconEntry("Burger.jpg", "LessHealthy_Burger", "Burger", "Fast food tinggi lemak dan karbohidrat olahan.", 480f, 17f, 7f, 24f, 20f, 45f, 6f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 32),
        new FoodIconEntry("Burger_Keju.jpg", "LessHealthy_BurgerKeju", "Burger Keju", "Burger dengan keju extra, kalori lebih tinggi.", 520f, 19f, 8f, 28f, 22f, 46f, 7f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 36),
        new FoodIconEntry("Donat_Cokelat.jpg", "LessHealthy_DonatCokelat", "Donat Cokelat", "Camilan manis tinggi gula dan lemak.", 300f, 10f, 6f, 14f, 4f, 38f, 22f, FoodData.FoodCategory.Dessert, false, true, true, true, true, 21),
        new FoodIconEntry("Es_Kopi_Susu.jpg", "LessHealthy_EsKopiSusu", "Es Kopi Susu", "Kopi susu manis, kafein dan gula tinggi.", 210f, 12f, 5f, 7f, 4f, 32f, 28f, FoodData.FoodCategory.Minuman, false, true, true, true, true, 18),
        new FoodIconEntry("Es_Teh_Manis.jpg", "LessHealthy_EsTehManis", "Es Teh Manis", "Teh manis, gula tinggi per gelas.", 90f, 8f, 4f, 0f, 0f, 23f, 22f, FoodData.FoodCategory.Minuman, false, true, true, true, true, 8),
        new FoodIconEntry("Indomie_Goreng.jpg", "LessHealthy_IndomieGoreng", "Indomie Goreng", "Mie instan goreng, sodium dan lemak tinggi.", 380f, 16f, 4f, 14f, 9f, 52f, 3f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 22),
        new FoodIconEntry("Kentang_Goreng.jpg", "LessHealthy_KentangGoreng", "Kentang Goreng", "Gorengan tinggi minyak dan garam.", 380f, 14f, 4f, 19f, 4f, 45f, 0.5f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 24),
        new FoodIconEntry("Mie_Goreng.jpg", "LessHealthy_MieGoreng", "Mie Goreng", "Mie goreng kaki lima, minyak dan garam tinggi.", 480f, 18f, 5f, 18f, 12f, 62f, 4f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 26),
        new FoodIconEntry("Minuman_Berenergi.png", "LessHealthy_MinumanBerenergi", "Minuman Berenergi", "Minuman energi tinggi gula dan kafein.", 110f, 14f, 3f, 0f, 0f, 28f, 27f, FoodData.FoodCategory.Minuman, false, true, true, true, true, 16),
        new FoodIconEntry("Nasi_Goreng.jpg", "LessHealthy_NasiGoreng", "Nasi Goreng", "Nasi goreng khas warung, minyak tinggi.", 500f, 20f, 7f, 20f, 14f, 65f, 3f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 28),
        new FoodIconEntry("Pisang_Coklat.jpg", "LessHealthy_PisangCoklat", "Pisang Coklat", "Pisang goreng dengan cokelat, tinggi gula.", 280f, 11f, 7f, 12f, 3f, 42f, 28f, FoodData.FoodCategory.Dessert, false, false, true, true, true, 18),
        new FoodIconEntry("Pizza.jpg", "LessHealthy_Pizza", "Pizza", "Fast food tinggi lemak, garam, dan karbo olahan.", 560f, 16f, 8f, 22f, 20f, 58f, 5f, FoodData.FoodCategory.FastFood, false, false, true, true, true, 38),
        new FoodIconEntry("Soda_Lemon.jpg", "LessHealthy_SodaLemon", "Soda Lemon", "Minuman soda manis, gula tinggi.", 140f, 6f, 3f, 0f, 0f, 36f, 35f, FoodData.FoodCategory.Minuman, false, true, true, true, true, 12),
        new FoodIconEntry("Teh_Boba.jpg", "LessHealthy_TehBoba", "Teh Boba", "Teh susu boba, gula dan karbo tinggi.", 260f, 8f, 7f, 6f, 2f, 48f, 38f, FoodData.FoodCategory.Minuman, false, false, true, true, true, 20),
    };

    private static readonly (string OldName, string NewName)[] AssetRenames =
    {
        ("AyamGoreng", "LessHealthy_AyamGoreng"),
        ("Burger", "LessHealthy_Burger"),
        ("EsTehManis", "LessHealthy_EsTehManis"),
        ("IndomieGoreng", "LessHealthy_IndomieGoreng"),
        ("NasiGoreng", "LessHealthy_NasiGoreng"),
    };

    [MenuItem("HealthySim/Food/Sync Catalog From Food Icons")]
    public static void SyncCatalogFromIcons()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
        {
            Debug.LogError($"[FoodIconCatalogSync] Folder tidak ditemukan: {IconFolder}");
            return;
        }

        MigrateLegacyAssetNames();
        ConfigureIconImportSettings();
        AssetDatabase.Refresh();

        var canonicalAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int created = 0;
        int updated = 0;
        int iconsAssigned = 0;

        for (int i = 0; i < Catalog.Length; i++)
        {
            FoodIconEntry entry = Catalog[i];
            canonicalAssets.Add(entry.AssetName);

            string assetPath = $"{FoodFolder}/{entry.AssetName}.asset";
            FoodData food = AssetDatabase.LoadAssetAtPath<FoodData>(assetPath);
            if (food == null)
            {
                food = ScriptableObject.CreateInstance<FoodData>();
                AssetDatabase.CreateAsset(food, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            ApplyEntry(food, entry);

            Sprite sprite = LoadIconSprite(entry.IconFileName);
            if (sprite == null && entry.IconFileName.Contains("Donat", StringComparison.OrdinalIgnoreCase))
                sprite = LoadIconSpriteByPrefix("Donat");
            if (sprite != null)
            {
                food.icon = sprite;
                iconsAssigned++;
            }
            else
            {
                Debug.LogWarning($"[FoodIconCatalogSync] Icon tidak ditemukan: {entry.IconFileName}");
            }

            EditorUtility.SetDirty(food);
        }

        int deleted = DeleteOrphanFoodAssets(canonicalAssets);
        deleted += DeleteOrphanAssetFilesOnDisk(canonicalAssets);

        var catalogFoods = LoadCanonicalFoods();
        RefreshFoodCatalogProviders(catalogFoods);
        RefreshBazaarManagers(catalogFoods);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FoodIconCatalogSync] Selesai. Created={created}, Updated={updated}, Icons={iconsAssigned}, Deleted={deleted}, Total={Catalog.Length}");
    }

    private static void MigrateLegacyAssetNames()
    {
        for (int i = 0; i < AssetRenames.Length; i++)
        {
            (string oldName, string newName) = AssetRenames[i];
            string oldPath = $"{FoodFolder}/{oldName}.asset";
            string newPath = $"{FoodFolder}/{newName}.asset";

            if (!File.Exists(oldPath))
                continue;

            if (File.Exists(newPath))
            {
                ForceDeleteAssetFile(oldPath);
                continue;
            }

            string error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[FoodIconCatalogSync] Gagal rename {oldName} -> {newName}: {error}. Menghapus file legacy.");
                ForceDeleteAssetFile(oldPath);
            }
        }
    }

    private static void ApplyEntry(FoodData food, FoodIconEntry entry)
    {
        food.foodName = entry.DisplayName;
        food.description = entry.Description;
        food.calories = entry.Calories;
        food.energyRestored = entry.Energy;
        food.moodEffect = entry.Mood;
        food.fat = entry.Fat;
        food.protein = entry.Protein;
        food.carbohydrate = entry.Carbohydrate;

        SerializedObject so = new SerializedObject(food);
        SerializedProperty sugarProp = so.FindProperty("sugar");
        if (sugarProp != null)
            sugarProp.floatValue = entry.Sugar;
        so.ApplyModifiedPropertiesWithoutUndo();

        food.category = entry.Category;
        food.isHealthy = entry.IsHealthy;
        food.availableMorning = entry.Morning;
        food.availableAfternoon = entry.Afternoon;
        food.availableEvening = entry.Evening;
        food.availableNight = entry.Night;
        food.price = Mathf.Max(0, entry.Price);
    }

    private static Sprite LoadIconSprite(string iconFileName)
    {
        string path = $"{IconFolder}/{iconFileName}";
        return LoadSpriteAtAssetPath(path);
    }

    private static Sprite LoadIconSpriteByPrefix(string prefix)
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
            return null;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { IconFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileName(path);
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                continue;

            Sprite sprite = LoadSpriteAtAssetPath(path);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static Sprite LoadSpriteAtAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        return null;
    }

    private static void ConfigureIconImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { IconFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.maxTextureSize > 512)
            {
                importer.maxTextureSize = 512;
                changed = true;
            }

            if (importer.alphaIsTransparency && ext is ".jpg" or ".jpeg")
            {
                importer.alphaIsTransparency = false;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }
    }

    private static int DeleteOrphanFoodAssets(HashSet<string> canonicalAssets)
    {
        string[] guids = AssetDatabase.FindAssets("t:FoodData", new[] { FoodFolder });
        int deleted = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string assetName = Path.GetFileNameWithoutExtension(path);
            if (canonicalAssets.Contains(assetName))
                continue;

            if (AssetDatabase.DeleteAsset(path))
            {
                deleted++;
                Debug.Log($"[FoodIconCatalogSync] Dihapus (tanpa icon): {path}");
                continue;
            }

            if (ForceDeleteAssetFile(path))
            {
                deleted++;
                Debug.Log($"[FoodIconCatalogSync] Dihapus paksa (tanpa icon): {path}");
            }
        }

        return deleted;
    }

    private static int DeleteOrphanAssetFilesOnDisk(HashSet<string> canonicalAssets)
    {
        if (!Directory.Exists(FoodFolder))
            return 0;

        string[] files = Directory.GetFiles(FoodFolder, "*.asset", SearchOption.TopDirectoryOnly);
        int deleted = 0;

        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i].Replace('\\', '/');
            string assetName = Path.GetFileNameWithoutExtension(path);
            if (canonicalAssets.Contains(assetName))
                continue;

            if (ForceDeleteAssetFile(path))
            {
                deleted++;
                Debug.Log($"[FoodIconCatalogSync] Dihapus dari disk (orphan/corrupt): {path}");
            }
        }

        return deleted;
    }

    private static bool ForceDeleteAssetFile(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            return false;

        try
        {
            File.Delete(assetPath);
            string metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FoodIconCatalogSync] Gagal hapus file {assetPath}: {ex.Message}");
            return false;
        }
    }

    private static List<FoodData> LoadCanonicalFoods()
    {
        var catalogFoods = new List<FoodData>();
        for (int i = 0; i < Catalog.Length; i++)
        {
            string path = $"{FoodFolder}/{Catalog[i].AssetName}.asset";
            FoodData food = AssetDatabase.LoadAssetAtPath<FoodData>(path);
            if (food != null)
                catalogFoods.Add(food);
        }

        return catalogFoods;
    }

    private static void RefreshFoodCatalogProviders(List<FoodData> catalogFoods)
    {
        FoodCatalogProvider[] providers = UnityEngine.Object.FindObjectsByType<FoodCatalogProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < providers.Length; i++)
        {
            FoodCatalogProvider provider = providers[i];
            if (provider == null)
                continue;

            provider.SetFoods(catalogFoods);
            EditorUtility.SetDirty(provider);
        }

        if (providers.Length == 0)
            Debug.Log("[FoodIconCatalogSync] FoodCatalogProvider tidak ditemukan di scene — catalog folder sudah disinkronkan.");
    }

    private static void RefreshBazaarManagers(List<FoodData> catalogFoods)
    {
        var bazaarPool = new List<FoodData>();
        for (int i = 0; i < catalogFoods.Count; i++)
        {
            FoodData food = catalogFoods[i];
            if (food != null && food.isHealthy)
                bazaarPool.Add(food);
        }

        ApplyBazaarPoolToObject(UnityEngine.Object.FindFirstObjectByType<BazaarManager>(), bazaarPool);
        ApplyBazaarPoolToPrefab("Assets/_Project/Prefabs/UI/MobileJoystickUI.prefab", bazaarPool);
    }

    private static void ApplyBazaarPoolToObject(BazaarManager manager, List<FoodData> bazaarPool)
    {
        if (manager == null)
            return;

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty pool = so.FindProperty("bazaarFoodPool");
        if (pool == null)
            return;

        pool.arraySize = bazaarPool.Count;
        for (int i = 0; i < bazaarPool.Count; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = bazaarPool[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static void ApplyBazaarPoolToPrefab(string prefabPath, List<FoodData> bazaarPool)
    {
        if (!File.Exists(prefabPath))
            return;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
            return;

        try
        {
            BazaarManager[] managers = prefabRoot.GetComponentsInChildren<BazaarManager>(true);
            for (int i = 0; i < managers.Length; i++)
                ApplyBazaarPoolToObject(managers[i], bazaarPool);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
