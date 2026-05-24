using System.Collections;
using TMPro;
using UnityEngine;

public class WorkDoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string _officeSceneName = "OfficeScene";
    [SerializeField] private string _promptText = "Masuk Kantor";
    [SerializeField] private string _blockedText = "Terlalu lelah untuk kerja";
    [SerializeField] private string _nightText = "Kantor sudah tutup";
    [SerializeField] private WorkSessionManager _workSessionManagerOverride;
    [SerializeField] private FadeManager _fadeManagerOverride;

    private Coroutine _floatingTextRoutine;
    private Collider _cachedCollider;

    private void Awake()
    {
        _cachedCollider = GetComponent<Collider>();
        if (_cachedCollider == null)
            _cachedCollider = GetComponentInChildren<Collider>();
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this, _cachedCollider, transform);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    public string GetInteractionText()
    {
        return _promptText;
    }

    // Compatibility helper with requested naming.
    public string GetInteractPrompt()
    {
        return _promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    // Compatibility helper with requested naming.
    public void OnInteract(GameObject player)
    {
        TimeManager timeManager = TimeManager.Instance;
        PlayerStats playerStats = PlayerStats.Instance;
        WorkSessionManager workSessionManager = _workSessionManagerOverride != null
            ? _workSessionManagerOverride
            : WorkSessionManager.Instance;
        FadeManager fadeManager = _fadeManagerOverride != null
            ? _fadeManagerOverride
            : FadeManager.Instance;

        if (workSessionManager == null)
        {
            GameObject managerObj = GameObject.Find("GameManager");
            if (managerObj != null)
            {
                WorkSessionManager existing = managerObj.GetComponent<WorkSessionManager>();
                if (existing == null)
                    Debug.LogWarning("[WorkDoorInteractable] WorkSessionManager tidak ditemukan di GameManager. Menambahkan fallback runtime.");

                workSessionManager = existing ?? managerObj.AddComponent<WorkSessionManager>();
            }
            else
            {
                Debug.LogWarning("[WorkDoorInteractable] GameManager tidak ditemukan saat mencari WorkSessionManager fallback.");
            }
        }

        if (fadeManager == null)
        {
            GameObject managerObj = GameObject.Find("GameManager");
            if (managerObj != null)
            {
                FadeManager existing = managerObj.GetComponent<FadeManager>();
                if (existing == null)
                    Debug.LogWarning("[WorkDoorInteractable] FadeManager tidak ditemukan di GameManager. Menambahkan fallback runtime.");

                fadeManager = existing ?? managerObj.AddComponent<FadeManager>();
            }
            else
            {
                Debug.LogWarning("[WorkDoorInteractable] GameManager tidak ditemukan saat mencari FadeManager fallback.");
            }
        }

        if (timeManager == null || playerStats == null)
        {
            ShowFloatingText("Sistem belum siap", 2f);
            return;
        }

        if (workSessionManager == null)
        {
            ShowFloatingText("Sistem kerja belum siap", 2f);
            return;
        }

        float hour = timeManager.CurrentHour;
        float energy = playerStats.EnergyPercent;

        if (hour < 7f || hour >= 15f)
        {
            ShowFloatingText("Kantor sudah tutup. Jam kerja 07.00 - 15.00.", 2f);
            return;
        }

        TimeManager.TimePeriod period = timeManager.CurrentPeriod;

        if (!workSessionManager.CanWork(energy))
        {
            string reason = _blockedText;
            ShowFloatingText(reason, 2f);
            return;
        }

        WorkSessionData data = workSessionManager.BuildSession(period, energy);
        if (data == null)
        {
            ShowFloatingText(_nightText, 2f);
            return;
        }

        if (fadeManager == null)
        {
            ShowFloatingText("Transisi belum siap", 2f);
            return;
        }

        SpawnPlayerManager.TargetSpawnID = "officedoor";
        fadeManager.FadeToBlackAndLoad(_officeSceneName, 0.5f);
    }

    private void ShowFloatingText(string text, float duration)
    {
        if (_floatingTextRoutine != null)
            StopCoroutine(_floatingTextRoutine);

        _floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("WorkDoorFloatingText");

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.68f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            Camera cam = Camera.main;
            Vector3 displayPos = transform.position + Vector3.up * 1.25f;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.001f)
                    toCam = -transform.forward;

                toCam.Normalize();
                displayPos = transform.position + toCam * 0.75f + Vector3.up * 1.25f;
                textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - cam.transform.position);
            }

            textObj.transform.position = displayPos;

            yield return null;
        }

        Destroy(textObj);
        _floatingTextRoutine = null;
    }
}
