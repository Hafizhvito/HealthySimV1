using System;
using UnityEngine;

public class SessionSeedManager : MonoBehaviour
{
    public static SessionSeedManager Instance { get; private set; }

    [Header("Seed Settings")]
    [SerializeField] private bool useManualSeed = false;
    [SerializeField] private int manualSeed = 123456;
    [SerializeField] private bool logSeedOnStart = true;

    public int SessionSeed { get; private set; }
    public bool UsingManualSeed => useManualSeed;

    private System.Random rng;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SessionSeed = useManualSeed ? manualSeed : BuildSessionSeed();
        rng = new System.Random(SessionSeed);

        if (logSeedOnStart)
            Debug.Log($"[SessionSeed] Active seed: {SessionSeed} (manual={useManualSeed})");
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;

        return rng.Next(minInclusive, maxExclusive);
    }

    public float NextFloat01()
    {
        return (float)rng.NextDouble();
    }

    private int BuildSessionSeed()
    {
        var now = DateTime.UtcNow;
        int datePart = now.Year * 10000 + now.Month * 100 + now.Day;
        int minutePart = now.Hour * 60 + now.Minute;

        // Deterministic for this run, varied between sessions.
        return (datePart * 397) ^ minutePart;
    }
}
