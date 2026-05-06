using UnityEngine;
using UnityEditor;

public static class CreateCharacterDataAssets
{
    [MenuItem("HealthSim/Create Character Data Assets")]
    public static void Create()
    {
        string folder = "Assets/_Project/Data/Characters";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Data", "Characters");
        }

        CreateAsset(folder, "Char_Male_Kurus",
            characterName: "Rafi",
            startingAge: 18,
            backstory:
                "Rafi tumbuh terbiasa kerja keras sejak SMA — makan sering tertunda, " +
                "bahkan dilupakan. Bukan karena ingin kurus, tapi karena waktu tidak pernah cukup.\n\n" +
                "Kini tubuhnya yang kurus mulai terasa seperti beban. " +
                "Sudah waktunya makan bukan sekadar kenyang.",
            summary: "Kerja keras sejak muda, sering skip makan karena sibuk.");

        CreateAsset(folder, "Char_Male_Normal",
            characterName: "Dimas",
            startingAge: 18,
            backstory:
                "Dimas punya fondasi yang cukup baik — makan teratur dan aktif olahraga semasa sekolah. " +
                "Tapi sejak pindah kerja ke kota baru, ritme itu perlahan goyah.\n\n" +
                "Ia tahu masih di titik aman — tapi juga tahu, kalau dibiarkan, aman bisa cepat berubah.",
            summary: "Hidup teratur tapi mulai goyah sejak pindah kerja.");

        CreateAsset(folder, "Char_Male_Gemuk",
            characterName: "Bagas",
            startingAge: 18,
            backstory:
                "Stres di kantor selalu Bagas lampiaskan ke makanan: mie instan tengah malam, " +
                "minuman manis, camilan di laci meja. Gerak? Minimal.\n\n" +
                "Belakangan napas terasa berat naik dua anak tangga. " +
                "Tubuhnya mulai berbicara dengan cara yang tidak bisa diabaikan.",
            summary: "Stres kerja bikin makan jadi pelarian. Tubuhnya mulai memberi sinyal.");

        CreateAsset(folder, "Char_Female_Kurus",
            characterName: "Nisa",
            startingAge: 18,
            backstory:
                "Nisa selalu terkontrol soal makan — tapi lama-lama berubah terlalu ketat. " +
                "Hindari nasi, takut minyak, merasa bersalah kalau makan lebih dari rencana.\n\n" +
                "Hasilnya tubuhnya kekurangan banyak hal. " +
                "Sudah waktunya belajar makan dengan benar, bukan sekadar sedikit.",
            summary: "Terlalu ketat soal makan, tanpa sadar tubuhnya kekurangan nutrisi penting.");

        CreateAsset(folder, "Char_Female_Normal",
            characterName: "Ayu",
            startingAge: 18,
            backstory:
                "Ayu sadar gizi, suka sayur, dan tidak punya kebiasaan makan yang ekstrem. " +
                "Masalahnya bukan pengetahuan, tapi waktu — jadwal padat bikin sarapan sering dilewat.\n\n" +
                "Ayu tahu apa yang benar. Ia hanya perlu membuktikan bisa konsisten.",
            summary: "Sadar gizi tapi jadwal padat bikin pola makan tidak selalu ideal.");

        CreateAsset(folder, "Char_Female_Gemuk",
            characterName: "Dina",
            startingAge: 18,
            backstory:
                "Sejak kuliah, mi instan dan junk food jadi makanan sehari-hari Dina. " +
                "Kebiasaan itu tidak berhenti setelah lulus — makanan jadi cara bertahan dari tekanan kerja.\n\n" +
                "Dina tidak butuh diet ekstrem. Ia butuh pola hidup yang bisa dipertahankan.",
            summary: "Pola makan tidak teratur sejak kuliah, kini dampaknya mulai terasa.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateCharacterDataAssets] 6 CharacterData assets created in " + folder);
    }

    private static void CreateAsset(string folder, string fileName, string characterName,
        int startingAge, string backstory, string summary)
    {
        string path = $"{folder}/{fileName}.asset";

        CharacterData existing = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (existing != null)
        {
            Debug.Log($"[CreateCharacterDataAssets] Skipped (already exists): {path}");
            return;
        }

        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        data.characterName = characterName;
        data.startingAge = startingAge;
        data.backstoryText = backstory;
        data.backstorySummary = summary;
        data.additionalBackstoryLines = new string[0];

        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"[CreateCharacterDataAssets] Created: {path}");
    }
}