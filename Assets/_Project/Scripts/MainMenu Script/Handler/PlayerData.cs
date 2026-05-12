using Unity.VisualScripting;
using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static string PlayerName { get; set; } = "";
    public static string JenisKelamin { get; set; } = "";
    public static float TinggiBadan { get; set; } = 160f;
    public static float BeratBadan { get; set; } = 60f;

    void Start()
    {
        PlayerPrefs.DeleteKey("PlayerName");
        PlayerPrefs.DeleteKey("JenisKelamin");
        PlayerPrefs.DeleteKey("TinggiBadan");
        PlayerPrefs.DeleteKey("BeratBadan");
    }

    // Hitung BMI otomatis
    public static float BMI
    {
        get
        {
            float tinggiMeter = TinggiBadan / 100f;
            return BeratBadan / (tinggiMeter * tinggiMeter);
        }
    }

    // Kategori BMI
    public static string KategoriBMI
    {
        get
        {
            if (BMI < 18.5f) return "Berat Badan Kurang";
            if (BMI < 25f) return "Normal";
            if (BMI < 30f) return "Berat Badan Lebih";
            return "Obesitas";
        }
    }

    // Simpan ke PlayerPrefs
    public static void Save()
    {
        PlayerPrefs.SetString("PlayerName", PlayerName);
        PlayerPrefs.SetString("JenisKelamin", JenisKelamin);
        PlayerPrefs.SetFloat("TinggiBadan", TinggiBadan);
        PlayerPrefs.SetFloat("BeratBadan", BeratBadan);
        PlayerPrefs.Save();
    }

    // Load dari PlayerPrefs
    public static void Load()
    {
        PlayerName = PlayerPrefs.GetString("PlayerName", "");
        PlayerName = PlayerPrefs.GetString("JenisKelamin", "");
        TinggiBadan = PlayerPrefs.GetFloat("TinggiBadan", 160f);
        BeratBadan = PlayerPrefs.GetFloat("BeratBadan", 60f);
    }
}
