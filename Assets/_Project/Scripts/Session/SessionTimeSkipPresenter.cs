using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the full-screen clock skip animation after returning to SampleScene,
/// then advances <see cref="TimeManager"/> so the HUD jump matches what the player sees.
/// </summary>
public class SessionTimeSkipPresenter : MonoBehaviour
{
    public static SessionTimeSkipPresenter Instance { get; private set; }

    private struct PendingSkip
    {
        public bool active;
        public float startHour;
        public float endHour;
        public string title;
        public float duration;
    }

    private PendingSkip pending;
    private Coroutine playRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void Queue(float startHour, float endHour, string title, float duration = 3.5f)
    {
        if (Mathf.Approximately(startHour, endHour))
            return;

        pending = new PendingSkip
        {
            active = true,
            startHour = startHour,
            endHour = endHour,
            title = string.IsNullOrWhiteSpace(title) ? "Melewati waktu..." : title,
            duration = Mathf.Max(0.8f, duration)
        };
    }

    public void ClearPending()
    {
        pending.active = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pending.active || scene.name != "SampleScene")
            return;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayPendingWhenReady());
    }

    private IEnumerator PlayPendingWhenReady()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        if (FadeManager.Instance != null)
        {
            float waited = 0f;
            const float timeout = 3f;
            while (waited < timeout)
            {
                CanvasGroup fadeGroup = FadeManager.Instance.GetComponentInChildren<CanvasGroup>(true);
                if (fadeGroup == null || fadeGroup.alpha < 0.05f)
                    break;

                yield return new WaitForSecondsRealtime(0.05f);
                waited += 0.05f;
            }
        }

        if (!pending.active)
        {
            playRoutine = null;
            yield break;
        }

        PendingSkip skip = pending;
        pending.active = false;

        ClockAnimationUI clock = ClockAnimationUI.EnsureInstance();
        if (clock == null)
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.SetTimeByHour(skip.endHour);

            playRoutine = null;
            yield break;
        }

        bool done = false;
        clock.PlayTimeSkipAnimation(
            skip.title,
            skip.startHour,
            skip.endHour,
            skip.duration,
            () => done = true);

        while (!done)
            yield return null;

        if (TimeManager.Instance != null)
            TimeManager.Instance.SetTimeByHour(skip.endHour);

        playRoutine = null;
    }
}
