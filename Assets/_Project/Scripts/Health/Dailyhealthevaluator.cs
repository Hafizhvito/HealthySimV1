using UnityEngine;

/// <summary>
/// Evaluates a player's daily health score at sleep time.
/// Menerima snapshot boolean (workedToday, trainedToday) dari SleepBedInteractable
/// supaya tidak terbaca setelah NotifyDayResetFromSleep() dipanggil.
/// </summary>
public static class DailyHealthEvaluator
{
    // ─── Score weights ───────────────────────────────────────────────
    // Diet
    private const float DietMaxBonus    =  4.0f;
    private const float DietJunkPenalty = -3.0f;
    private const float DietCalorieLow  = -1.0f;   // < 50% of target
    private const float DietCalorieHigh = -1.0f;   // > 130% of target
    private const float DietNoFoodPen   = -1.5f;   // tidak makan sama sekali

    // Sleep — baseline kecil, bukan reward utama
    private const float SleepNormalBonus    =  1.0f;
    private const float SleepDisturbedBonus =  0.0f;
    private const float SleepLowEnergyPen   = -1.0f;  // tidur dengan energi < 20%

    // Gym
    private const float GymExcellent      =  3.0f;
    private const float GymSolid          =  2.0f;
    private const float GymStrained       =  1.0f;
    private const float GymStreakPenalty  = -1.5f;   // per hari setelah threshold
    private const int   GymStreakThreshold =  3;

    // Work
    private const float WorkFullBonus     =  2.0f;
    private const float WorkFullBonusPay  =  2.0f;
    private const float WorkPartial       =  0.5f;
    private const float WorkFailed        = -1.5f;
    private const float WorkStreakPenalty = -2.0f;   // per hari setelah threshold
    private const int   WorkStreakThreshold = 2;

    // ─── Public entry point ──────────────────────────────────────────

    /// <summary>
    /// Dipanggil dari SleepBedInteractable.ApplyRecovery().
    /// workedToday dan trainedToday adalah SNAPSHOT yang diambil sebelum fade/reset.
    /// </summary>
    public static DailyHealthResult Evaluate(
        bool disturbedSleep,
        float energyBeforeSleep,
        bool workedToday,
        WorkSessionData lastWorkSession,
        bool lastWorkHadBonus,
        bool trainedToday,
        GymSessionData lastGymSession,
        PlayerActionTracker tracker,
        PlayerStats stats)
    {
        var result = new DailyHealthResult();

        result.dietScore  = EvaluateDiet(tracker, stats, ref result);
        result.sleepScore = EvaluateSleep(disturbedSleep, energyBeforeSleep);
        result.gymScore   = EvaluateGym(trainedToday, lastGymSession, stats, ref result);
        result.workScore  = EvaluateWork(workedToday, lastWorkSession, lastWorkHadBonus, stats, ref result);

        result.totalDelta = result.dietScore
                          + result.sleepScore
                          + result.gymScore
                          + result.workScore;

        // Clamp: satu hari buruk tidak langsung katastrofik, tapi tetap terasa
        result.totalDelta = Mathf.Clamp(result.totalDelta, -8f, 12f);

        BuildSummary(result);
        return result;
    }

    // ─── Diet ────────────────────────────────────────────────────────

    private static float EvaluateDiet(
        PlayerActionTracker tracker,
        PlayerStats stats,
        ref DailyHealthResult result)
    {
        if (tracker == null)
            return 0f;

        int healthy   = tracker.GetCount(PlayerActionTracker.ActionType.HealthyFoodTaken);
        int unhealthy = tracker.GetCount(PlayerActionTracker.ActionType.UnhealthyFoodTaken);
        int total     = healthy + unhealthy;

        result.healthyFoodCount   = healthy;
        result.unhealthyFoodCount = unhealthy;

        float score = 0f;

        if (total == 0)
        {
            score = DietNoFoodPen;
            result.dietNote = "Tidak ada catatan makan hari ini.";
            return score;
        }

        float healthyRatio = (float)healthy / total;

        if (healthyRatio >= 0.80f)
        {
            score = DietMaxBonus;
            result.dietNote = "Pola makan sangat baik hari ini.";
        }
        else if (healthyRatio >= 0.50f)
        {
            score = Mathf.Lerp(2.0f, DietMaxBonus, (healthyRatio - 0.5f) / 0.3f);
            result.dietNote = "Pola makan cukup seimbang.";
        }
        else if (healthyRatio > 0f)
        {
            score = Mathf.Lerp(0f, 2.0f, healthyRatio / 0.5f);
            result.dietNote = "Lebih banyak makanan tidak sehat hari ini.";
        }
        else
        {
            score = DietJunkPenalty;
            result.dietNote = "Semua makanan hari ini tidak sehat.";
        }

        // Cek kalori vs target
        if (stats != null && stats.DailyCalorieTarget > 0f)
        {
            float calorieRatio = stats.TotalCalories / stats.DailyCalorieTarget;
            if (calorieRatio < 0.50f)
            {
                score += DietCalorieLow;
                result.dietNote += " Asupan kalori terlalu sedikit.";
            }
            else if (calorieRatio > 1.30f)
            {
                score += DietCalorieHigh;
                result.dietNote += " Asupan kalori berlebihan.";
            }
        }

        return score;
    }

    // ─── Sleep ───────────────────────────────────────────────────────

    private static float EvaluateSleep(bool disturbedSleep, float energyBeforeSleep)
    {
        float score = disturbedSleep ? SleepDisturbedBonus : SleepNormalBonus;

        if (energyBeforeSleep < 0.20f)
            score += SleepLowEnergyPen;

        return score;
    }

    // ─── Gym ─────────────────────────────────────────────────────────

    private static float EvaluateGym(
        bool trainedToday,
        GymSessionData session,
        PlayerStats stats,
        ref DailyHealthResult result)
    {
        float score = 0f;

        if (trainedToday)
        {
            if (session != null)
            {
                switch (session.result)
                {
                    case GymSessionResult.Excellent:
                        score = GymExcellent;
                        result.gymNote = "Sesi gym luar biasa hari ini!";
                        break;
                    case GymSessionResult.Solid:
                        score = GymSolid;
                        result.gymNote = "Sesi gym solid.";
                        break;
                    case GymSessionResult.Strained:
                        score = GymStrained;
                        result.gymNote = "Berlatih tapi terlalu terforsir.";
                        break;
                    default:
                        score = 0f;
                        result.gymNote = "Sesi gym gagal.";
                        break;
                }
            }
            else
            {
                score = GymSolid;
                result.gymNote = "Gym selesai.";
            }

            if (stats != null)
                stats.ResetGymSkipStreak();
        }
        else
        {
            if (stats != null)
            {
                stats.IncrementGymSkipStreak();
                int streak = stats.GymSkipStreak;
                if (streak >= GymStreakThreshold)
                {
                    int penaltyDays = streak - GymStreakThreshold + 1;
                    score = GymStreakPenalty * penaltyDays;
                    result.gymNote = $"Sudah {streak} hari tidak gym. Tubuh mulai menurun.";
                }
                else
                {
                    result.gymNote = "Tidak gym hari ini.";
                }
            }
        }

        return score;
    }

    // ─── Work ────────────────────────────────────────────────────────

    private static float EvaluateWork(
        bool workedToday,
        WorkSessionData session,
        bool hadBonus,
        PlayerStats stats,
        ref DailyHealthResult result)
    {
        float score = 0f;

        if (workedToday)
        {
            if (session != null)
            {
                switch (session.result)
                {
                    case WorkResult.Full:
                        score = hadBonus ? WorkFullBonusPay : WorkFullBonus;
                        result.workNote = hadBonus
                            ? "Kerja penuh dengan bonus energi!"
                            : "Kerja penuh hari ini.";
                        break;
                    case WorkResult.Partial:
                        score = WorkPartial;
                        result.workNote = "Kerja tidak optimal hari ini.";
                        break;
                    case WorkResult.Failed:
                        score = WorkFailed;
                        result.workNote = "Sesi kerja gagal karena energi habis.";
                        break;
                }
            }
            else
            {
                score = WorkPartial;
                result.workNote = "Kerja selesai.";
            }

            if (stats != null)
                stats.ResetWorkSkipStreak();
        }
        else
        {
            if (stats != null)
            {
                stats.IncrementWorkSkipStreak();
                int streak = stats.WorkSkipStreak;
                if (streak >= WorkStreakThreshold)
                {
                    int penaltyDays = streak - WorkStreakThreshold + 1;
                    score = WorkStreakPenalty * penaltyDays;
                    result.workNote = $"Sudah {streak} hari tidak kerja. Tekanan finansial meningkat.";
                }
                else
                {
                    result.workNote = "Tidak kerja hari ini.";
                }
            }
        }

        return score;
    }

    // ─── Summary ─────────────────────────────────────────────────────

    private static void BuildSummary(DailyHealthResult r)
    {
        float total = r.totalDelta;

        if (total >= 7f)
            r.overallNote = "Hari yang luar biasa! Tubuhmu berterima kasih.";
        else if (total >= 3f)
            r.overallNote = "Hari yang baik. Terus konsisten.";
        else if (total >= 0f)
            r.overallNote = "Hari yang biasa. Ada ruang untuk perbaikan.";
        else if (total >= -4f)
            r.overallNote = "Hari yang kurang baik. Tubuhmu merasakannya.";
        else
            r.overallNote = "Hari yang berat. Perhatikan pola hidupmu.";
    }
}

/// <summary>
/// Value object hasil evaluasi harian.
/// </summary>
[System.Serializable]
public class DailyHealthResult
{
    public float dietScore;
    public float sleepScore;
    public float gymScore;
    public float workScore;
    public float totalDelta;

    public int healthyFoodCount;
    public int unhealthyFoodCount;

    public string dietNote    = string.Empty;
    public string sleepNote   = string.Empty;
    public string gymNote     = string.Empty;
    public string workNote    = string.Empty;
    public string overallNote = string.Empty;

    public override string ToString()
    {
        return $"[DailyHealth] total={totalDelta:+0.0;-0.0} " +
               $"diet={dietScore:+0.0;-0.0} " +
               $"sleep={sleepScore:+0.0;-0.0} " +
               $"gym={gymScore:+0.0;-0.0} " +
               $"work={workScore:+0.0;-0.0}";
    }
}