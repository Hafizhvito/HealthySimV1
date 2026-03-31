using UnityEngine;

public class SkyboxTintController : MonoBehaviour
{
    [Header("Period Colors")]
    [SerializeField] private Color morningColor =
        new Color(1f, 0.95f, 0.8f);
    [SerializeField] private Color afternoonColor =
        new Color(1f, 1f, 1f);
    [SerializeField] private Color eveningColor =
        new Color(1f, 0.7f, 0.4f);
    [SerializeField] private Color nightColor =
        new Color(0.2f, 0.2f, 0.4f);

    [SerializeField] private float transitionSpeed = 1f;
    private Color targetColor;
    private Light directionalLight;

    void Awake()
    {
        directionalLight = FindFirstObjectByType<Light>();
        targetColor = morningColor;
    }

    void Start()
    {
        TimeManager tm = TimeManager.Instance;
        if (tm != null)
            tm.OnPeriodChanged += HandlePeriodChanged;

        RenderSettings.ambientLight = morningColor;
        if (directionalLight != null)
            directionalLight.color = morningColor;
    }

    void Update()
    {
        RenderSettings.ambientLight = Color.Lerp(
            RenderSettings.ambientLight,
            targetColor,
            transitionSpeed * Time.deltaTime
        );
        if (directionalLight != null)
            directionalLight.color = Color.Lerp(
                directionalLight.color,
                targetColor,
                transitionSpeed * Time.deltaTime
            );
    }

    void HandlePeriodChanged(TimeManager.TimePeriod period)
    {
        targetColor = period switch
        {
            TimeManager.TimePeriod.Morning => morningColor,
            TimeManager.TimePeriod.Afternoon => afternoonColor,
            TimeManager.TimePeriod.Evening => eveningColor,
            TimeManager.TimePeriod.Night => nightColor,
            _ => afternoonColor
        };
    }

    void OnDestroy()
    {
        TimeManager tm = TimeManager.Instance;
        if (tm != null)
            tm.OnPeriodChanged -= HandlePeriodChanged;
    }
}
