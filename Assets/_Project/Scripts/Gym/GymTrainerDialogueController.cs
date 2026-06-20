using System.Collections.Generic;
using UnityEngine;

public class GymTrainerDialogueController : MonoBehaviour
{
    [SerializeField] private string trainerDisplayName = "Coach Bima";

    public DialogueGraphData BuildPreTrainingDialogue(GymSessionData session)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_trainer";
        graph.npcDisplayName = trainerDisplayName;
        graph.startNodeId = "greeting";

        var nodes = new List<DialogueNodeData>();

        string greeting = BuildPreGreeting(session);
        string explanation = BuildPreExplanation(session);
        string motivation = BuildPreMotivation(session);

        nodes.Add(new DialogueNodeData
        {
            nodeId = "greeting",
            fallbackLine = greeting,
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Apa rencana hari ini, Coach?", nextNodeId = "explanation" }
            }
        });

        nodes.Add(new DialogueNodeData
        {
            nodeId = "explanation",
            fallbackLine = explanation,
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Siap, kita mulai.", nextNodeId = "motivation" }
            }
        });

        nodes.Add(new DialogueNodeData
        {
            nodeId = "motivation",
            fallbackLine = motivation,
            choices = new List<DialogueChoiceData>(),
            isConversationEnd = true,
            isTerminal = true
        });

        graph.nodes = nodes;
        return graph;
    }

    public DialogueGraphData BuildPostTrainingDialogue(GymSessionData session)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_trainer_post";
        graph.npcDisplayName = trainerDisplayName;
        graph.startNodeId = "result";

        var nodes = new List<DialogueNodeData>();

        string resultLine = BuildPostResult(session);
        string closingLine = BuildPostClosing(session);

        nodes.Add(new DialogueNodeData
        {
            nodeId = "result",
            fallbackLine = resultLine,
            choices = new List<DialogueChoiceData>
            {
                new DialogueChoiceData { choiceText = "Ada catatan lain, Coach?", nextNodeId = "closing" }
            }
        });

        string summary = string.Format(
            "Tier: {0} | Adaptasi +{1:0.0} | Fatigue +{2:0.0}",
            GetTierLabel(session.tierAtStart),
            Mathf.Max(0f, session.adaptationGain),
            Mathf.Max(0f, session.fatigueGain));

        nodes.Add(new DialogueNodeData
        {
            nodeId = "closing",
            fallbackLine = closingLine,
            npcFollowUpText = summary,
            choices = new List<DialogueChoiceData>(),
            isConversationEnd = true,
            isTerminal = true
        });

        graph.nodes = nodes;
        return graph;
    }

    private string BuildPreGreeting(GymSessionData session)
    {
        if (session.energyAtStart < 0.35f)
            return "Hei, kamu datang. Aku lihat energi kamu lagi tipis hari ini. " +
                   "Kita tetap latihan, tapi aku akan sesuaikan volumenya supaya tubuhmu tidak dipaksa berlebihan.";

        switch (session.tierAtStart)
        {
            case GymTier.Advanced:
                return "Wah, datang lagi. Fondasi kamu udah kuat sekarang, gerakannya lebih terkontrol. " +
                       "Hari ini kita fokus ke teknik dan efisiensi, bukan sekedar angkat beban.";
            case GymTier.Regular:
                return "Hei, ritme latihanmu udah mulai kebentuk. Aku bisa lihat progresnya dari sesi-sesi terakhir. " +
                       "Hari ini kita dorong sedikit intensitasnya.";
            default:
                return "Bagus, kamu datang lagi. Konsistensi itu yang paling penting di awal. " +
                       "Hari ini kita tetap fokus ke gerakan dasar, bangun fondasi yang kuat dulu.";
        }
    }

    private string BuildPreExplanation(GymSessionData session)
    {
        if (session.energyAtStart < 0.35f)
            return "Kita akan kurangi set dan repetisi. Yang penting tetap gerak, tapi jangan sampai collapse. " +
                   "Tubuh butuh sinyal bahwa kita masih aktif, bukan sinyal bahwa kita sedang menyiksa diri.";

        switch (session.tierAtStart)
        {
            case GymTier.Advanced:
                return "Di level ini, yang bikin beda bukan berapa berat yang kamu angkat, tapi kontrol napas dan tempo. " +
                       "Kualitas gerakan selalu lebih penting daripada ego.";
            case GymTier.Regular:
                return "Kita naikkan sedikit bebannya, tapi form tetap prioritas. " +
                       "Kalau mulai goyang, turunkan tempo sebentar, lalu lanjut rapi. " +
                       "Progres yang stabil lebih baik dari progres yang ngebut lalu cedera.";
            default:
                return "Fokus hari ini: gerakan compound dasar. Squat, push, pull. " +
                       "Jangan terburu-buru naikkan beban. Tubuhmu masih beradaptasi dan itu normal.";
        }
    }

    private string BuildPreMotivation(GymSessionData session)
    {
        if (session.energyAtStart < 0.35f)
            return "Ingat: datang saat energi rendah itu sudah achievement. Kita mulai pelan. Siap?";

        switch (session.tierAtStart)
        {
            case GymTier.Advanced:
                return "Oke, cukup ngobrolnya. Waktunya buktiin di lapangan. Let's go.";
            case GymTier.Regular:
                return "Sip, kamu udah ngerti ritmenya. Kita jalan sekarang. Fokus dan nikmati prosesnya.";
            default:
                return "Ingat, progres cepat itu hasil dari repetisi yang rapi, bukan asal ngebut. Yuk mulai.";
        }
    }

    private string BuildPostResult(GymSessionData session)
    {
        switch (session.result)
        {
            case GymSessionResult.Excellent:
                return "Performa kamu bagus banget hari ini. Gerakannya terkontrol, tempo rapi, dan kamu push sampai batas yang tepat. " +
                       "Adaptasi naik signifikan dengan fatigue yang masih terkontrol.";
            case GymSessionResult.Strained:
                return "Sesi ini agak berat buat kondisi kamu sekarang. Aku bisa lihat di pertengahan kamu mulai struggle. " +
                       "Tetap dapat progres, tapi fatigue-nya naik cukup tinggi. Besok pastikan istirahat cukup.";
            case GymSessionResult.Solid:
                return "Latihan rapi dan konsisten. Tidak ada yang wow tapi juga tidak ada yang gagal. " +
                       "Ini yang kita cari untuk jangka panjang: progres stabil tanpa burnout.";
            default:
                return "Sesi tadi belum optimal, tapi kita tetap catat sebagai latihan hari ini. " +
                       "Yang penting kamu datang dan bergerak.";
        }
    }

    private string BuildPostClosing(GymSessionData session)
    {
        switch (session.result)
        {
            case GymSessionResult.Excellent:
                return "Kalau bisa pertahankan ritme ini, kamu akan naik tier dalam waktu dekat. " +
                       "Sekarang pulang, makan yang cukup protein, dan tidur berkualitas. Itu bagian dari latihan juga.";
            case GymSessionResult.Strained:
                return "Besok, kalau masih capek, ga apa skip sehari. Recovery itu bagian dari program. " +
                       "Tapi jangan sampai satu hari jadi satu minggu, ya.";
            case GymSessionResult.Solid:
                return "Konsistensi beats intensitas. Ingat itu selalu. " +
                       "Sampai ketemu di sesi berikutnya.";
            default:
                return "Ga setiap hari bisa perfect. Yang penting jangan berhenti. Sampai besok.";
        }
    }

    private static string GetTierLabel(GymTier tier)
    {
        switch (tier)
        {
            case GymTier.Advanced: return "Advanced";
            case GymTier.Regular: return "Regular";
            default: return "Beginner";
        }
    }
}
