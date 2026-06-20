using UnityEngine;

/// <summary>
/// Builds personalized phase-transition narratives from actual player behaviour data.
/// Output is displayed in the AgingNotificationPanel when the player transitions between age stages.
/// </summary>
public static class PhaseReviewBuilder
{
    public struct PhaseReviewInput
    {
        public PlayerStats.AgeStage previousStage;
        public PlayerStats.AgeStage newStage;
        public float phaseScore;
        public PlayerStats.PhaseSnapshot snapshot;
        public float phaseCarryOverModifier;
        public PlayerStats.Gender gender;
    }

    public static string Build(PhaseReviewInput input)
    {
        string summary = BuildSummaryLine(input);
        string issues = BuildIssuesList(input);
        string consequence = BuildConsequence(input);

        string result = summary;
        if (!string.IsNullOrEmpty(issues))
            result += "\n\n" + issues;
        result += "\n\n" + consequence;
        return result;
    }

    private static string BuildSummaryLine(PhaseReviewInput input)
    {
        string fromLabel = PlayerStats.GetAgeStageLabelIndonesia(input.previousStage);
        string scoreLabel = GetScoreLabel(input.phaseScore);
        int days = input.snapshot.daysInPhase;

        return $"Fase {fromLabel} selesai dalam {days} hari dengan skor {input.phaseScore:0}/100, {scoreLabel}.";
    }

    private static string BuildIssuesList(PhaseReviewInput input)
    {
        var sb = new System.Text.StringBuilder();
        var snap = input.snapshot;
        int days = Mathf.Max(1, snap.daysInPhase);
        int issueCount = 0;

        if (snap.poorDietDays > 0 && (float)snap.poorDietDays / days >= 0.3f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Pola makan tidak sehat ({snap.poorDietDays} dari {days} hari)");
            issueCount++;
        }

        if (snap.highCalorieDays > 0 && (float)snap.highCalorieDays / days >= 0.3f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Asupan kalori sering berlebihan ({snap.highCalorieDays} hari)");
            issueCount++;
        }

        if (snap.skippedGymDays > 0 && (float)snap.skippedGymDays / days >= 0.4f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Jarang berolahraga ({snap.skippedGymDays} dari {days} hari tanpa gym)");
            issueCount++;
        }

        if (snap.overworkedDays > 0 && (float)snap.overworkedDays / days >= 0.3f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Terlalu sering memaksakan kerja ({snap.overworkedDays} hari)");
            issueCount++;
        }

        if (snap.disturbedSleepDays > 0 && (float)snap.disturbedSleepDays / days >= 0.3f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Tidur sering terganggu ({snap.disturbedSleepDays} hari)");
            issueCount++;
        }

        if (snap.skippedWorkDays > 0 && (float)snap.skippedWorkDays / days >= 0.4f)
        {
            sb.Append(IssuePrefix(issueCount));
            sb.Append($"Sering absen kerja ({snap.skippedWorkDays} hari). Tekanan finansial naik");
            issueCount++;
        }

        if (issueCount == 0 && input.phaseScore >= 65f)
            return "Tidak ada masalah berarti di fase ini. Kebiasaanmu cukup seimbang.";

        if (issueCount == 0)
            return "Tidak ada masalah besar, tapi fase ini belum optimal.";

        return "Yang perlu diperhatikan:\n" + sb.ToString();
    }

    private static string BuildConsequence(PhaseReviewInput input)
    {
        string toLabel = PlayerStats.GetAgeStageLabelIndonesia(input.newStage);

        if (input.phaseScore >= 70f)
        {
            return $"Tubuhmu memasuki fase {toLabel} dalam kondisi baik. " +
                   "Energi maksimum naik sedikit. Pertahankan kebiasaan ini.";
        }

        if (input.phaseScore >= 40f)
        {
            string genderNote = input.newStage == PlayerStats.AgeStage.Senior && input.gender == PlayerStats.Gender.Female
                ? " Perubahan hormonal di fase lansia butuh perhatian ekstra pada nutrisi dan istirahat."
                : "";

            return $"Fase {toLabel} dimulai dengan kondisi standar. " +
                   "Tubuhmu tidak lebih kuat, tapi juga belum terlalu lemah. " +
                   $"Setiap kebiasaan mulai sekarang akan menentukan akhir perjalananmu.{genderNote}";
        }

        string severity = input.newStage == PlayerStats.AgeStage.Senior
            ? "Fase lansia akan terasa lebih berat. Risiko penyakit meningkat jika pola ini berlanjut."
            : "Energi maksimummu turun. Tubuh mulai lebih sensitif terhadap kebiasaan buruk.";

        return $"Pola hidup di fase sebelumnya berdampak nyata. {severity} " +
               "Ingat: setiap pilihan kecil menumpuk. Makan, gerak, tidur, semuanya punya konsekuensi.";
    }

    private static string GetScoreLabel(float score)
    {
        if (score >= 75f) return "sangat baik";
        if (score >= 65f) return "baik";
        if (score >= 50f) return "cukup";
        if (score >= 40f) return "kurang konsisten";
        return "perlu perhatian serius";
    }

    private static string IssuePrefix(int index)
    {
        return index == 0 ? "• " : "\n• ";
    }
}
