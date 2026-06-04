using UnityEngine;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    // Referensi Panel
    [Header("Panel References")]
    [SerializeField] private GameObject panelPause;
    [SerializeField] private GameObject panelOption;

    // Scene
    [Header("Scene Settings")]
    [SerializeField] private string menuSceneName = "GameScene";

    // Referensi UI
    [Header("UI")]
    [SerializeField] private Button pauseButton;

    // Referensi untuk Pengaturan
    [Header("Option References")]
    [SerializeField] private Slider volumeSFX;
    [SerializeField] private Slider volumeMusic;

    // Audio
    [Header("Audio")]
    [SerializeField] private AudioClip gameMusic;

    void Start()
    {
        panelPause.SetActive(false);
        panelOption.SetActive(false);
        LoadOptions();
    }

    private void LoadOptions()
    {
        float savedMusic = PlayerPrefs.GetFloat("Music", 0.8f);
        float savedSFX = PlayerPrefs.GetFloat("SFX", 0.8f);

        volumeMusic.value = savedMusic;
        volumeSFX.value = savedSFX;
    }

    public void OnSetMusic()
    {
        AudioManager._Instance.SetMusicVolume(volumeMusic.value);
    }

    public void OnSetSFX()
    {
        AudioManager._Instance.SetSFXVolume(volumeSFX.value);
    }

    // private void ShowPanel(UIPanelTransition nextPanel, int direction = 1)
    // {
    //     // if (currentPanel == nextPanel) { return; }

    //     currentPanel.Hide(direction, () => nextPanel.Show());
    //     currentPanel = nextPanel;
    // }

    // private void SetPlayerInput(bool enabled)
    // {
    //     PlayerController playerMovement = FindFirstObjectByType<PlayerController>();
    //     if (playerMovement != null)
    //     {
    //         playerMovement.enabled = enabled;
    //     }
    // }

    public void OnPauseButton()
    {
        Time.timeScale = 0f;
        panelPause.SetActive(true);
        pauseButton.gameObject.SetActive(false);
    }

    public void OnResumeButton()
    {
        Time.timeScale = 1f;
        panelPause.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    public void OnSettingButton()
    {
        panelPause.SetActive(false);
        panelOption.SetActive(true);
    }

    public void OnBackToMenuButton()
    {
        Time.timeScale = 1f;
        panelPause.SetActive(false);
        pauseButton.gameObject.SetActive(true);
        AudioManager._Instance?.StopMusic(() => SceneLoader.LoadScene(menuSceneName));
    }

    public void OnExitGameButton()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnBackButton()
    {
        panelPause.SetActive(true);
        panelOption.SetActive(false);
    }
}
