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
    [SerializeField] private float floatingTextCooldownSeconds = 2f;

    private Coroutine _floatingTextRoutine;
    private GameObject _floatingTextObject;
    private float _nextFloatingTextAllowedTime;
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
        ClearFloatingText();
    }

    public string GetInteractionText()
    {
        return _promptText;
    }

    public string GetInteractPrompt()
    {
        return _promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        TimeManager timeManager = TimeManager.Instance;
        PlayerStats playerStats = PlayerStats.Instance;
        WorkSessionManager workSessionManager = ResolveWorkSessionManager();

        if (timeManager == null || playerStats == null || workSessionManager == null)
            return false;

        if (!FacilityHours.IsWorkOpen(timeManager))
            return false;

        float energy = playerStats.EnergyPercent;
        if (!workSessionManager.CanWork(energy))
            return false;

        return workSessionManager.BuildSession(timeManager.CurrentPeriod, energy) != null;
    }

    public void Interact(GameObject interactor)
    {
        OnInteract(interactor);
    }

    public void OnInteract(GameObject player)
    {
        TimeManager timeManager = TimeManager.Instance;
        PlayerStats playerStats = PlayerStats.Instance;
        WorkSessionManager workSessionManager = ResolveWorkSessionManager();
        FadeManager fadeManager = ResolveFadeManager();

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

        if (!FacilityHours.IsWorkOpen(hour))
        {
            ShowFloatingText($"Kantor sudah tutup. Jam kerja {FacilityHours.WorkHoursLabel}.", 2f);
            return;
        }

        TimeManager.TimePeriod period = timeManager.CurrentPeriod;

        if (!workSessionManager.CanWork(energy))
        {
            ShowFloatingText(_blockedText, 2f);
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

    private WorkSessionManager ResolveWorkSessionManager()
    {
        if (_workSessionManagerOverride != null)
            return _workSessionManagerOverride;

        if (WorkSessionManager.Instance != null)
            return WorkSessionManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
            return null;

        WorkSessionManager existing = managerObj.GetComponent<WorkSessionManager>();
        return existing != null ? existing : managerObj.AddComponent<WorkSessionManager>();
    }

    private FadeManager ResolveFadeManager()
    {
        if (_fadeManagerOverride != null)
            return _fadeManagerOverride;

        if (FadeManager.Instance != null)
            return FadeManager.Instance;

        GameObject managerObj = GameObject.Find("GameManager");
        if (managerObj == null)
            return null;

        FadeManager existing = managerObj.GetComponent<FadeManager>();
        return existing != null ? existing : managerObj.AddComponent<FadeManager>();
    }

    private void ShowFloatingText(string text, float duration)
    {
        if (Time.unscaledTime < _nextFloatingTextAllowedTime)
            return;

        _nextFloatingTextAllowedTime = Time.unscaledTime + Mathf.Max(0.25f, floatingTextCooldownSeconds);
        ClearFloatingText();
        _floatingTextRoutine = StartCoroutine(FloatingTextRoutine(text, duration));
    }

    private void ClearFloatingText()
    {
        if (_floatingTextRoutine != null)
        {
            StopCoroutine(_floatingTextRoutine);
            _floatingTextRoutine = null;
        }

        if (_floatingTextObject != null)
        {
            Destroy(_floatingTextObject);
            _floatingTextObject = null;
        }
    }

    private IEnumerator FloatingTextRoutine(string text, float duration)
    {
        GameObject textObj = new GameObject("WorkDoorFloatingText");
        _floatingTextObject = textObj;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 3.8f;
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

        if (_floatingTextObject == textObj)
            _floatingTextObject = null;

        Destroy(textObj);
        _floatingTextRoutine = null;
    }
}
