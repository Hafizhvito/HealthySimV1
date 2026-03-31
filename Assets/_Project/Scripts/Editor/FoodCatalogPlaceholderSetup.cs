using UnityEditor;
using UnityEngine;

public static class FoodCatalogPlaceholderSetup
{
    private const string RootFolder = "Assets/_Project/Data";
    private const string FoodFolder = "Assets/_Project/Data/Foods";

    private struct FoodSeed
    {
        public string AssetName;
        public string DisplayName;
        public string Description;
        public float Calories;
        public float Energy;
        public float Mood;
        public float Fat;
        public float Protein;
        public float Carbohydrate;
        public FoodData.FoodCategory Category;
        public bool IsHealthy;
        public bool Morning;
        public bool Afternoon;
        public bool Evening;
        public bool Night;
        public int Price;
    }

    private static readonly FoodSeed[] Seeds =
    {
        new FoodSeed
        {
            AssetName = "Healthy_NasiMerahAyamPanggang",
            DisplayName = "Nasi Merah + Ayam Panggang",
            Description = "Karbo kompleks dan protein tinggi untuk energi stabil.",
            Calories = 430f,
            Energy = 24f,
            Mood = 6f,
            Fat = 10f,
            Protein = 28f,
            Carbohydrate = 55f,
            Category = FoodData.FoodCategory.MakananBerat,
            IsHealthy = true,
            Morning = true,
            Afternoon = true,
            Evening = true,
            Night = false,
            Price = 32
        },
        new FoodSeed
        {
            AssetName = "Healthy_GadoGado",
            DisplayName = "Gado-Gado",
            Description = "Sayuran segar dengan kacang, cocok untuk makan siang sehat.",
            Calories = 320f,
            Energy = 18f,
            Mood = 7f,
            Fat = 12f,
            Protein = 12f,
            Carbohydrate = 35f,
            Category = FoodData.FoodCategory.MakananBerat,
            IsHealthy = true,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = false,
            Price = 28
        },
        new FoodSeed
        {
            AssetName = "Healthy_SupSayur",
            DisplayName = "Sup Sayur",
            Description = "Pilihan ringan yang bantu recovery setelah aktivitas.",
            Calories = 180f,
            Energy = 14f,
            Mood = 5f,
            Fat = 5f,
            Protein = 8f,
            Carbohydrate = 22f,
            Category = FoodData.FoodCategory.MakananRingan,
            IsHealthy = true,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 20
        },
        new FoodSeed
        {
            AssetName = "Healthy_SaladBuah",
            DisplayName = "Salad Buah",
            Description = "Buah campur untuk camilan sehat dan segar.",
            Calories = 160f,
            Energy = 12f,
            Mood = 6f,
            Fat = 2f,
            Protein = 3f,
            Carbohydrate = 33f,
            Category = FoodData.FoodCategory.Buah,
            IsHealthy = true,
            Morning = true,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 17
        },
        new FoodSeed
        {
            AssetName = "Healthy_OatmealPisang",
            DisplayName = "Oatmeal Pisang",
            Description = "Sarapan praktis dengan serat tinggi.",
            Calories = 250f,
            Energy = 16f,
            Mood = 4f,
            Fat = 6f,
            Protein = 7f,
            Carbohydrate = 40f,
            Category = FoodData.FoodCategory.MakananRingan,
            IsHealthy = true,
            Morning = true,
            Afternoon = false,
            Evening = false,
            Night = false,
            Price = 19
        },
        new FoodSeed
        {
            AssetName = "Healthy_JusJerukSegar",
            DisplayName = "Jus Jeruk Segar",
            Description = "Minuman segar untuk boost ringan.",
            Calories = 120f,
            Energy = 10f,
            Mood = 5f,
            Fat = 1f,
            Protein = 1f,
            Carbohydrate = 27f,
            Category = FoodData.FoodCategory.Minuman,
            IsHealthy = true,
            Morning = true,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 14
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_NasiGorengSpesial",
            DisplayName = "Nasi Goreng Spesial",
            Description = "Lezat dan mengenyangkan, tapi minyaknya tinggi.",
            Calories = 540f,
            Energy = 22f,
            Mood = 8f,
            Fat = 22f,
            Protein = 15f,
            Carbohydrate = 68f,
            Category = FoodData.FoodCategory.FastFood,
            IsHealthy = false,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 34
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_BurgerKeju",
            DisplayName = "Burger Keju",
            Description = "Cepat dan enak, namun tinggi kalori dan lemak.",
            Calories = 520f,
            Energy = 20f,
            Mood = 8f,
            Fat = 26f,
            Protein = 18f,
            Carbohydrate = 48f,
            Category = FoodData.FoodCategory.FastFood,
            IsHealthy = false,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 36
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_KentangGoreng",
            DisplayName = "Kentang Goreng",
            Description = "Camilan gurih tinggi garam dan minyak.",
            Calories = 380f,
            Energy = 14f,
            Mood = 4f,
            Fat = 19f,
            Protein = 4f,
            Carbohydrate = 45f,
            Category = FoodData.FoodCategory.FastFood,
            IsHealthy = false,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 24
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_MieInstanTelur",
            DisplayName = "Mie Instan Telur",
            Description = "Praktis untuk malam, tapi sodium tinggi.",
            Calories = 460f,
            Energy = 19f,
            Mood = 5f,
            Fat = 18f,
            Protein = 12f,
            Carbohydrate = 60f,
            Category = FoodData.FoodCategory.MakananBerat,
            IsHealthy = false,
            Morning = false,
            Afternoon = false,
            Evening = true,
            Night = true,
            Price = 30
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_DonatCokelat",
            DisplayName = "Donat Cokelat",
            Description = "Manis dan bikin mood naik cepat, tapi sugar tinggi.",
            Calories = 300f,
            Energy = 10f,
            Mood = 6f,
            Fat = 14f,
            Protein = 4f,
            Carbohydrate = 38f,
            Category = FoodData.FoodCategory.Dessert,
            IsHealthy = false,
            Morning = true,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 21
        },
        new FoodSeed
        {
            AssetName = "LessHealthy_TehBoba",
            DisplayName = "Teh Boba",
            Description = "Minuman manis favorit, energi cepat tapi tidak tahan lama.",
            Calories = 260f,
            Energy = 8f,
            Mood = 7f,
            Fat = 6f,
            Protein = 2f,
            Carbohydrate = 48f,
            Category = FoodData.FoodCategory.Minuman,
            IsHealthy = false,
            Morning = false,
            Afternoon = true,
            Evening = true,
            Night = true,
            Price = 20
        }
    };

    [MenuItem("Tools/HealthySim/Food/Generate Placeholder Food Catalog")]
    public static void GeneratePlaceholderFoodCatalog()
    {
        EnsureFolderExists("Assets/_Project", "Data", RootFolder);
        EnsureFolderExists(RootFolder, "Foods", FoodFolder);

        int createdCount = 0;
        int updatedCount = 0;

        for (int i = 0; i < Seeds.Length; i++)
        {
            FoodSeed seed = Seeds[i];
            string assetPath = FoodFolder + "/" + seed.AssetName + ".asset";

            FoodData food = AssetDatabase.LoadAssetAtPath<FoodData>(assetPath);
            if (food == null)
            {
                food = ScriptableObject.CreateInstance<FoodData>();
                AssetDatabase.CreateAsset(food, assetPath);
                createdCount++;
            }
            else
            {
                updatedCount++;
            }

            ApplySeed(food, seed);
            EditorUtility.SetDirty(food);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FoodCatalogPlaceholderSetup] Selesai. Created: {createdCount}, Updated: {updatedCount}, Total Seeds: {Seeds.Length}");
    }

    private static void ApplySeed(FoodData food, FoodSeed seed)
    {
        food.foodName = seed.DisplayName;
        food.description = seed.Description;
        food.calories = seed.Calories;
        food.energyRestored = seed.Energy;
        food.moodEffect = seed.Mood;
        food.fat = seed.Fat;
        food.protein = seed.Protein;
        food.carbohydrate = seed.Carbohydrate;
        food.category = seed.Category;
        food.isHealthy = seed.IsHealthy;
        food.availableMorning = seed.Morning;
        food.availableAfternoon = seed.Afternoon;
        food.availableEvening = seed.Evening;
        food.availableNight = seed.Night;
        food.price = Mathf.Max(0, seed.Price);
    }

    private static void EnsureFolderExists(string parentPath, string folderName, string expectedPath)
    {
        if (AssetDatabase.IsValidFolder(expectedPath))
            return;

        AssetDatabase.CreateFolder(parentPath, folderName);
    }
}
