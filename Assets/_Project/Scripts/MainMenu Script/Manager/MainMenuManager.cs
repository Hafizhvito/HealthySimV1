using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
using Unity.VisualScripting;

public class MainMenuManager : MonoBehaviour
{
    // Referensi Panel
    [Header("Panel References")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelOption;
    [SerializeField] private GameObject panelCredit;
    // [SerializeField] private GameObject panelLoadGame;

    // Scene
    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName = "GameScene";

    // Referensi untuk Pengaturan
    [Header("Option References")]
    [SerializeField] private Slider volumeSFX;
    [SerializeField] private Slider volumeMusic;

    // Animasi panel
    [Header("Panel Transitions")]
    [SerializeField] private UIPanelTransition transitionMain;
    [SerializeField] private UIPanelTransition transitionOptions;
    [SerializeField] private UIPanelTransition transitionCredits;
    // [SerializeField] private UIPanelTransition transitionLoadGame;

    // Audio
    [Header("Audio")]
    [SerializeField] private AudioClip menuMusic;

    private UIPanelTransition currentPanel;



    void Start()
    {
        panelMain.SetActive(true);
        panelOption.SetActive(false);
        panelCredit.SetActive(false);
        // panelLoadGame.SetActive(false);
        LoadOptions();
        currentPanel = transitionMain;
        AudioManager._Instance.PlayMusic(menuMusic);
    }

    private void LoadOptions()
    {
        float savedMusic = PlayerPrefs.GetFloat("Music", 0.8f);
        float savedSFX = PlayerPrefs.GetFloat("SFX", 0.8f);

        volumeMusic.value = savedMusic;
        volumeSFX.value = savedSFX;

    }

    // Navigasi Panel
    private void ShowPanel(UIPanelTransition nextPanel, int direction = 1)
    {
        if (currentPanel == nextPanel) { return; }

        currentPanel.Hide(direction, () => nextPanel.Show());
        currentPanel = nextPanel;
    }

    // Logika Load Game
    // public void OnLoadSlot(int slotIndex)
    // {
    //     string key = "SaveSlot_" + slotIndex;

    //     if (PlayerPrefs.HasKey(key))
    //     {
    //         Debug.Log($"Loading slot {slotIndex}...");
    //         // Nanti bisa dihubungkan ke sistem save game
    //         SceneManager.LoadScene(gameSceneName);
    //     }
    //     else
    //     {
    //         Debug.Log($"Slot {slotIndex} kosong.");
    //     }
    // }

    // Panggilan dari tombol
    public void OnPlayButton()
    {
        // AudioManager._Instance.StopMusic(() =>
        //     SceneLoader.LoadScene(gameSceneName));
        SceneLoader.LoadScene(gameSceneName);
    }

    // public void OnLoadGameButton()
    // {
    //     ShowPanel(transitionLoadGame);
    //     Debug.Log("Load Success");
    // }

    public void OnOptionsButton()
    {
        ShowPanel(transitionOptions);
        Debug.Log("Option Success");
    }

    public void OnCreditsButton()
    {
        ShowPanel(transitionCredits);
        Debug.Log("Credits Success");
    }

    public void OnQuitButton()
    {
        // #if UNITY_EDITOR
        //         UnityEditor.EditorApplication.isPlaying = false;
        // #else
        //         Application.Quit();
        // #endif
        Application.Quit();
    }

    public void OnBackButton()
    {
        ShowPanel(transitionMain);
    }
}
