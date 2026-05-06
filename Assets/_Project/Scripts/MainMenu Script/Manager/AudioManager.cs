using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager _Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Fade Settings")]
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1f;

    private void Awake()
    {
        if (_Instance != null && _Instance != this)
        {
            Debug.Log("AudioManager duplikat ditemukan, destroying...");
            Destroy(gameObject);
            return;
        }

        _Instance = this;
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        PlayerPrefs.DeleteKey("Music");
        PlayerPrefs.DeleteKey("SFX");
        LoadVolumeSettings();
    }

    // ── Volume Settings ───────────────────────────────────────
    private void LoadVolumeSettings()
    {
        float savedMusic = PlayerPrefs.GetFloat("Music", 0.8f);
        float savedSFX = PlayerPrefs.GetFloat("SFX", 0.8f);

        mainMixer.SetFloat("Music", LinearToDecibel(savedMusic));
        mainMixer.SetFloat("SFX", LinearToDecibel(savedSFX));
    }

    public void SetMusicVolume(float value)
    {
        mainMixer.SetFloat("Music", LinearToDecibel(value));
        PlayerPrefs.SetFloat("Music", value);
    }

    public void SetSFXVolume(float value)
    {
        mainMixer.SetFloat("SFX", LinearToDecibel(value));
        PlayerPrefs.SetFloat("SFX", value);
    }

    private float LinearToDecibel(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }

    // ── Music Control ─────────────────────────────────────────
    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip) return; // hindari restart jika sama

        musicSource.clip = clip;
        musicSource.volume = 0f;
        musicSource.Play();

        musicSource.DOFade(0.8f, fadeInDuration).SetEase(Ease.OutCubic);
    }

    public void StopMusic(System.Action onComplete = null)
    {
        musicSource.DOFade(0f, fadeOutDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                musicSource.Stop();
                onComplete?.Invoke();
            });
    }

    // ── SFX Control ───────────────────────────────────────────
    // Putar SFX sekali (one shot) — cocok untuk klik tombol, dll
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // Putar SFX dengan volume custom
    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }
}
