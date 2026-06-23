using UnityEngine;

/// <summary>
/// Estimates soft-educational premature-death risk at end-of-day sleep.
/// Youth days are protected; hospital visit reduces but does not remove risk.
/// </summary>
public static class LifestyleMortalityEvaluator
{
    public const int YouthProtectionUntilDay = 4;
    public const int DefaultEndingDay = 12;
    public const float HospitalChanceMultiplier = 0.45f;
    public const float MinimumRiskChance = 0.01f;
    public const float MaximumRiskChance = 0.18f;

    public struct MortalityAssessment
    {
        public float RollChance;
        public bool IsWarningZone;
        public bool IsRiskZone;
        public string PrimaryCauseLabel;
        public string WarningText;
    }

    public static MortalityAssessment Assess(
        int currentDayNumber,
        int endingDay,
        PlayerStats stats,
        PlayerActionTracker tracker)
    {
        var result = new MortalityAssessment();

        if (stats == null || currentDayNumber <= YouthProtectionUntilDay || currentDayNumber >= endingDay)
            return result;

        float phaseScore = stats.HealthScoreThisPhase;
        result.IsWarningZone = phaseScore < 45f;
        result.WarningText = BuildWarningText(phaseScore);

        if (phaseScore >= 35f && !HasChronicPoorPattern(stats))
            return result;

        float chance = ComputeBaseChance(phaseScore, stats, tracker);
        if (stats.CurrentAgeStage == PlayerStats.AgeStage.Senior)
            chance *= 1.65f;

        chance = Mathf.Clamp(chance, 0f, MaximumRiskChance);

        if (stats.VisitedHospitalToday && chance > 0f)
            chance = Mathf.Clamp(Mathf.Max(MinimumRiskChance, chance * HospitalChanceMultiplier), MinimumRiskChance, MaximumRiskChance);

        result.RollChance = chance;
        result.IsRiskZone = chance > 0f;
        result.PrimaryCauseLabel = ResolvePrimaryCause(stats);
        return result;
    }

    public static bool RollMortality(float chance)
    {
        if (chance <= 0f)
            return false;

        return Random.value < chance;
    }

    private static float ComputeBaseChance(float phaseScore, PlayerStats stats, PlayerActionTracker tracker)
    {
        if (phaseScore >= 35f)
            return HasChronicPoorPattern(stats) ? 0.04f : 0f;

        float chance = phaseScore < 25f
            ? Mathf.Lerp(0.12f, 0.08f, Mathf.Clamp01(phaseScore / 25f))
            : Mathf.Lerp(0.08f, 0.03f, Mathf.Clamp01((phaseScore - 25f) / 10f));

        if (HasChronicPoorPattern(stats))
            chance += 0.02f;

        if (tracker != null)
        {
            if (tracker.FaintEventCount >= 2)
                chance += 0.03f;
            if (tracker.CriticalEventCount >= 3)
                chance += 0.02f;
        }

        int lowEnergySleeps = stats.LowEnergySleepDays;
        int daysEvaluated = Mathf.Max(1, stats.TotalDaysEvaluated);
        if ((float)lowEnergySleeps / daysEvaluated >= 0.35f)
            chance += 0.02f;

        return chance;
    }

    private static bool HasChronicPoorPattern(PlayerStats stats)
    {
        int days = Mathf.Max(1, stats.TotalDaysEvaluated);
        float poorDietRatio = stats.PoorDietDays / (float)days;
        float skipGymRatio = stats.SkippedGymDays / (float)days;
        float disturbedSleepRatio = stats.DisturbedSleepDays / (float)days;
        return poorDietRatio >= 0.4f || skipGymRatio >= 0.45f || disturbedSleepRatio >= 0.35f;
    }

    private static string BuildWarningText(float phaseScore)
    {
        if (phaseScore >= 45f)
            return string.Empty;

        if (phaseScore >= 35f)
            return "Tubuhmu mulai terbebani. Perbaiki pola makan, istirahat, dan aktivitas sebelum kondisi memburuk.";

        return "Kondisimu rentan. Segera perbaiki kebiasaan harian dan pertimbangkan periksa ke klinik.";
    }

    private static string ResolvePrimaryCause(PlayerStats stats)
    {
        int days = Mathf.Max(1, stats.TotalDaysEvaluated);
        float poorDiet = stats.PoorDietDays / (float)days;
        float skipGym = stats.SkippedGymDays / (float)days;
        float disturbed = stats.DisturbedSleepDays / (float)days;
        float lowEnergySleep = stats.LowEnergySleepDays / (float)days;

        if (disturbed >= poorDiet && disturbed >= skipGym && disturbed >= lowEnergySleep)
            return "gangguan tidur dan kelelahan kronis";

        if (poorDiet >= skipGym)
            return "pola makan tidak seimbang";

        if (skipGym >= lowEnergySleep)
            return "kurangnya aktivitas fisik";

        return "beban kesehatan yang menumpuk";
    }
}
