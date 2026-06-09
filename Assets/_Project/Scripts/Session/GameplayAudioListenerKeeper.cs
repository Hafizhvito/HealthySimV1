using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures exactly one active <see cref="AudioListener"/> during gameplay.
/// Attach to the persistent manager host (MobileInputController / GameManager).
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameplayAudioListenerKeeper : MonoBehaviour
{
    private static GameplayAudioListenerKeeper _instance;
    private float _nextCheckTime;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AudioListenerEnforcer.EnforceSingleListener();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioListenerEnforcer.EnforceSingleListener();
    }

    void LateUpdate()
    {
        if (Time.unscaledTime < _nextCheckTime)
            return;

        _nextCheckTime = Time.unscaledTime + 0.5f;

        if (!AudioListenerEnforcer.HasActiveListener())
            AudioListenerEnforcer.EnforceSingleListener();
    }
}
