using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class LoadingManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image barFill;
    [SerializeField] private TMP_Text txtPercentage;
    [SerializeField] private CanvasGroup panelFade;

    [Header("UI Elements")]
    [SerializeField] private CanvasGroup panelLoading;  // assign Panel_Loading
    [SerializeField] private float elementFadeIn = 0.4f;

    [Header("Loading Settings")]
    [SerializeField] private float minLoadTime = 2f;  // minimum tampil loading
    [SerializeField] private float fadeDuration = 0.5f;

    // Nama scene yang akan di-load (dikirim dari scene lain)
    public static string TargetScene;

    private Tween fadeTween;
    private Tween loadingTween;

    private void Start()
    {
        if (barFill == null || txtPercentage == null || panelFade == null || panelLoading == null)
        {
            Debug.LogWarning("[LoadingManager] Missing UI references; loading UI disabled.");
            return;
        }

        // Pastikan bar mulai dari 0
        barFill.fillAmount = 0f;
        txtPercentage.text = "0%";

        panelLoading.alpha = 0f;

        // Fade in dari hitam
        panelFade.alpha = 1f;
        panelFade.DOKill();
        panelLoading.DOKill();
        fadeTween = panelFade.DOFade(0f, fadeDuration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            loadingTween = panelLoading.DOFade(1f, elementFadeIn).SetEase(Ease.OutCubic)
                .OnComplete(() => StartCoroutine(LoadSceneAsync()));
        });
    }

    private System.Collections.IEnumerator LoadSceneAsync()
    {
        float elapsedTime = 0f;
        float displayedFill = 0f;

        // Mulai load scene secara async
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(TargetScene);

        // Jangan langsung pindah scene saat loading selesai
        asyncLoad.allowSceneActivation = false;

        while (true)
        {
            elapsedTime += Time.deltaTime;
            float targetFill = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            float timeFill = Mathf.Clamp01(elapsedTime / minLoadTime);
            float finalFill = Mathf.Min(targetFill, timeFill);
            displayedFill = Mathf.MoveTowards(displayedFill, finalFill, Time.deltaTime * 0.5f);

            // update UI
            barFill.fillAmount = displayedFill;
            txtPercentage.text = $"{Mathf.RoundToInt(displayedFill * 100)}%";

            // mengaktifkan scene jika sudah 100%
            if (displayedFill >= 0.99f &&
                asyncLoad.progress >= 0.9f &&
                elapsedTime >= minLoadTime)
            {
                // update UI ke 100%
                barFill.fillAmount = 1f;
                txtPercentage.text = "100%";

                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(FadeOutAndActivate(asyncLoad));
                yield break;
            }

            yield return null;
        }
    }

    private System.Collections.IEnumerator FadeOutAndActivate(AsyncOperation asyncLoad)
    {
        // Fade out musik via AudioManager
        // AudioManager._Instance?.StopMusic();

        // merubah layar ke hitam
        if (panelFade != null)
        {
            panelFade.DOKill();
            fadeTween = panelFade.DOFade(1f, fadeDuration).SetEase(Ease.InCubic);
        }

        yield return new WaitForSeconds(fadeDuration);

        // mengaktifkan scene yang sudah selesai di-load
        asyncLoad.allowSceneActivation = true;
    }

    private void OnDisable()
    {
        fadeTween?.Kill();
        loadingTween?.Kill();
        panelFade?.DOKill();
        panelLoading?.DOKill();
    }
}
