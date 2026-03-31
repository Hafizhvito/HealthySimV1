using UnityEngine;

public class SessionFlowController : MonoBehaviour
{
    private bool sessionEnded;
    private bool subscribed;

    void OnEnable()
    {
        TrySubscribe();
    }

    void Update()
    {
        if (!subscribed)
            TrySubscribe();
    }

    void OnDisable()
    {
        if (subscribed && TimeManager.Instance != null)
            TimeManager.Instance.OnGameTimeUp -= HandleGameTimeUp;

        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || TimeManager.Instance == null)
            return;

        TimeManager.Instance.OnGameTimeUp += HandleGameTimeUp;
        subscribed = true;
    }

    private void HandleGameTimeUp()
    {
        if (sessionEnded)
            return;

        sessionEnded = true;

        PlayerActionTracker.BranchOutcome outcome = PlayerActionTracker.BranchOutcome.MixedPath;
        string trackerSummary = "tracker=unavailable";
        if (PlayerActionTracker.Instance != null)
        {
            outcome = PlayerActionTracker.Instance.EvaluateBranchOutcome();
            trackerSummary = PlayerActionTracker.Instance.GetDebugSummary();
        }

        string outcomeText = outcome switch
        {
            PlayerActionTracker.BranchOutcome.HealthyPath => "Akhir sesi: Jalur Sehat",
            PlayerActionTracker.BranchOutcome.RiskyPath => "Akhir sesi: Jalur Berisiko",
            _ => "Akhir sesi: Jalur Campuran"
        };

        Debug.Log($"[Session] {outcomeText} | {trackerSummary}");
    }
}
