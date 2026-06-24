using System.Collections.Generic;
using UnityEngine;

public class DoctorSriDialogueController : MonoBehaviour
{
    [SerializeField] private string doctorDisplayName = "dr. Sri Wuryanti, MS, Sp.GK";
    [SerializeField] private Sprite hospitalBackground;

    private const string BgResourcePath = "Backgrounds/bg_hospital";

    public bool IsHealthAlertActive => PlayerStats.Instance != null
        && PlayerStats.Instance.ShouldShowHealthGuidance(40f);

    public DialogueGraphData BuildConsultationDialogue(float healthScore, float dailyFat, float dailyProtein)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_doctor_sri";
        graph.npcDisplayName = doctorDisplayName;
        graph.startNodeId = "greeting";
        graph.backgroundSprite = hospitalBackground;
        if (hospitalBackground == null)
            graph.backgroundResourcePath = BgResourcePath;

        bool critical = healthScore < 30f;
        bool warning = healthScore < 50f;
        bool healthy = healthScore >= 70f;
        bool alertActive = IsHealthAlertActive;

        var nodes = new List<DialogueNodeData>();

        // Node 1: Greeting
        nodes.Add(new DialogueNodeData
        {
            nodeId = "greeting",
            fallbackLine = BuildGreeting(healthScore, critical, warning, healthy),
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Bagaimana kondisi saya, Dok?", nextNodeId = "diagnosis" }
            }
        });

        // Node 2: Diagnosis
        nodes.Add(new DialogueNodeData
        {
            nodeId = "diagnosis",
            fallbackLine = BuildDiagnosis(healthScore, dailyFat, dailyProtein, critical, warning),
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Apa yang harus saya perhatikan?", nextNodeId = "detail" }
            }
        });

        // Node 3: Detailed explanation
        nodes.Add(new DialogueNodeData
        {
            nodeId = "detail",
            fallbackLine = BuildDetailedExplanation(healthScore, dailyFat, dailyProtein, critical, warning),
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData
                {
                    choiceText = alertActive ? "Saya akan berusaha lebih baik, Dok." : "Terima kasih atas waktunya, Dok.",
                    nextNodeId = "closing"
                }
            }
        });

        // Node 4: Closing
        nodes.Add(new DialogueNodeData
        {
            nodeId = "closing",
            fallbackLine = BuildClosing(healthScore, healthy, alertActive),
            choices = new List<DialogueChoiceData>(),
            isConversationEnd = true,
            isTerminal = true
        });

        graph.nodes = nodes;
        return graph;
    }

    private string BuildGreeting(float score, bool critical, bool warning, bool healthy)
    {
        if (critical)
            return "Silakan duduk. Saya sudah melihat data terakhirmu dan... jujur, saya perlu bicara serius denganmu hari ini. " +
                   "Tubuhmu sedang memberikan sinyal yang tidak boleh diabaikan.";

        if (warning)
            return "Selamat datang. Aku sudah mereview catatan kesehatanmu belakangan ini. " +
                   "Ada beberapa hal yang perlu kita diskusikan. Bukan darurat, tapi penting untuk diperbaiki segera.";

        if (healthy)
            return "Ah, selamat datang! Senang bertemu denganmu lagi. " +
                   "Aku sudah lihat catatan terakhirmu, dan aku punya kabar baik.";

        return "Silakan masuk. Mari kita lihat bersama bagaimana perkembangan kesehatanmu. " +
               "Setiap kunjungan ini penting, meski rasanya belum ada keluhan besar.";
    }

    private string BuildDiagnosis(float score, float fat, float protein, bool critical, bool warning)
    {
        var sb = new System.Text.StringBuilder();

        if (critical)
        {
            sb.Append("Skor kesehatanmu saat ini sangat rendah. ");
            sb.Append("Ini berarti tubuhmu sedang dalam tekanan. Nutrisi yang masuk tidak cukup mendukung aktivitas harianmu. ");
        }
        else if (warning)
        {
            sb.Append("Kondisimu belum ideal. Beberapa indikator menunjukkan pola yang perlu diperbaiki. ");
        }
        else
        {
            sb.Append("Secara umum, kondisimu cukup stabil. ");
        }

        if (fat > 65f)
            sb.Append("Asupan lemak harianmu terlalu tinggi. Ini membebani organ pencernaanmu dan meningkatkan risiko jangka panjang. ");

        if (protein < 40f)
            sb.Append("Protein harianmu kurang. Tanpa protein yang cukup, otot dan daya tahan tubuhmu akan terus menurun. ");

        if (fat <= 65f && protein >= 40f && !critical && !warning)
            sb.Append("Asupan nutrisi makromu sudah dalam range yang baik. Lemak dan protein seimbang.");

        return sb.ToString();
    }

    private string BuildDetailedExplanation(float score, float fat, float protein, bool critical, bool warning)
    {
        if (critical)
        {
            return "Begini, tubuh manusia itu seperti mesin. Kalau bahan bakarnya salah atau kurang, " +
                   "lama-lama mesinnya rusak. Yang terjadi padamu sekarang: energimu menurun, " +
                   "daya tahan tubuh melemah, dan kalau pola ini berlanjut... " +
                   "risiko penyakit serius seperti diabetes atau hipertensi akan meningkat drastis. " +
                   "Ini bukan menakut-nakuti, ini fakta medis.";
        }

        if (warning)
        {
            return "Saya lihat polanya belum konsisten. Kadang makan baik, kadang tidak. " +
                   "Kadang gerak, kadang tidak. Tubuh butuh rutinitas, bukan kesempurnaan, " +
                   "tapi konsistensi. Mulai dari satu kebiasaan kecil yang bisa kamu jaga setiap hari. " +
                   "Misalnya, pastikan ada protein di setiap makan.";
        }

        return "Yang penting sekarang: pertahankan. Banyak orang merasa sudah sehat lalu lengah. " +
               "Kebiasaan baik itu seperti tabungan. Hasilnya baru terasa jangka panjang, " +
               "tapi kalau berhenti menabung, saldo akan turun lebih cepat dari yang dibayangkan.";
    }

    private string BuildClosing(float score, bool healthy, bool alertActive)
    {
        if (alertActive)
        {
            return "Baik. Aku akan buatkan catatan rekomendasi untukmu. " +
                   "Baca pelan-pelan dan coba terapkan mulai hari ini. " +
                   "Ingat: perubahan kecil yang konsisten jauh lebih powerful dari perubahan besar yang hanya sehari. " +
                   "Pintuku selalu terbuka kalau kamu butuh konsultasi lagi.";
        }

        if (healthy)
        {
            return "Terus pertahankan yang sudah kamu lakukan. Kamu membuktikan bahwa pilihan sehari-hari itu penting. " +
                   "Sampai jumpa di kunjungan berikutnya. Jaga kesehatanmu.";
        }

        return "Jaga dirimu baik-baik. Kalau merasa ada yang tidak beres, jangan tunda untuk datang kembali. " +
               "Kesehatan itu bukan tentang sempurna, tapi tentang sadar dan terus berusaha.";
    }
}
