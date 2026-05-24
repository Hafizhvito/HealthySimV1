using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FadeManager : MonoBehaviour
{
    private static FadeManager _instance;
    public static FadeManager Instance => _instance;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureFadeCanvas();

        // Startup safety: overlay must be transparent unless a fade is actively running.
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    public void FadeToBlack(float duration, Action onComplete)
    {
        StartFade(1f, duration, onComplete);
    }

    public void FadeFromBlack(float duration, Action onComplete)
    {
        StartFade(0f, duration, onComplete);
    }

    public void FadeToBlackAndLoad(string sceneName, float fadeDuration)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        // Instant black satu frame dulu biar tidak ada gap
        _canvasGroup.alpha = 0.5f;
        _canvasGroup.blocksRaycasts = true;

        FadeToBlack(fadeDuration, () =>
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(LoadThenFadeInRoutine(sceneName, fadeDuration));
        });
    }

    private void StartFade(float targetAlpha, float duration, Action onComplete)
    {
        EnsureFadeCanvas();

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
    {
        float clampedDuration = Mathf.Max(0f, duration);
        float startAlpha = _canvasGroup.alpha;

        if (clampedDuration <= 0f)
        {
            _canvasGroup.alpha = Mathf.Clamp01(targetAlpha);
            _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0f;
            onComplete?.Invoke();
            _fadeRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < clampedDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / clampedDuration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, Mathf.Clamp01(targetAlpha), k);
            _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0f;
            yield return null;
        }

        _canvasGroup.alpha = Mathf.Clamp01(targetAlpha);
        _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0f;
        onComplete?.Invoke();
        _fadeRoutine = null;
    }

    private IEnumerator LoadThenFadeInRoutine(string sceneName, float fadeDuration)
    {
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        if (loadOp != null)
        {
            while (!loadOp.isDone)
                yield return null;
        }

        bool shouldWaitForSpawn = !string.IsNullOrEmpty(SpawnPlayerManager.TargetSpawnID);
        if (shouldWaitForSpawn)
        {
            bool spawnComplete = false;
            Action onSpawnComplete = () => spawnComplete = true;
            SpawnPlayerManager.OnSpawnComplete += onSpawnComplete;

            float elapsed = 0f;
            const float timeout = 2f;
            const float tick = 0.05f;
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(tick);

            while (!spawnComplete && elapsed < timeout)
            {
                yield return wait;
                elapsed += tick;
            }

            SpawnPlayerManager.OnSpawnComplete -= onSpawnComplete;
        }

        yield return FadeRoutine(0f, fadeDuration, null);
    }

    private void EnsureFadeCanvas()
    {
        if (_canvas != null && _canvasGroup != null)
            return;

        Transform existing = transform.Find("FadeCanvas");
        if (existing != null)
        {
            _canvas = existing.GetComponent<Canvas>();
            _canvasGroup = existing.GetComponent<CanvasGroup>();
        }

        if (_canvas == null || _canvasGroup == null)
        {
            GameObject canvasObj = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _canvasGroup = canvasObj.GetComponent<CanvasGroup>();

            GameObject imageObj = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            imageObj.transform.SetParent(canvasObj.transform, false);
            RectTransform rect = imageObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObj.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        _canvas.sortingOrder = 999;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }
}
