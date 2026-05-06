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
        graph.startNodeId = "node_0";

        string opener;
        string followUp;

        switch (session.tierAtStart)
        {
            case GymTier.Advanced:
                opener = "Mantap, fondasi kamu udah kuat. Hari ini kita fokus teknik supaya progres tetap naik tanpa buang energi.";
                followUp = "Jaga kontrol napas dan tempo. Kualitas gerakan lebih penting daripada ego.";
                break;
            case GymTier.Regular:
                opener = "Ritme latihanmu sudah kebentuk. Kita dorong sedikit intensitas, tapi jangan sampai form berantakan.";
                followUp = "Kalau mulai goyang, turunkan tempo sebentar lalu lanjut rapi.";
                break;
            default:
                opener = "Bagus udah konsisten datang. Hari ini fokus gerakan dasar dulu biar tubuh adaptasinya aman.";
                followUp = "Ingat, progres cepat itu hasil dari repetisi rapi, bukan asal ngebut.";
                break;
        }

        if (session.energyAtStart < 0.35f)
            opener = "Energi kamu lagi tipis. Kita tetap jalan, tapi volumenya harus dijaga biar tidak overfatigue.";

        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = opener,
                choices = new List<DialogueChoiceData>
                {
                    new DialogueChoiceData { choiceText = "(Siap) Gas, Coach. Saya ikutin ritmenya.", nextNodeId = "node_1" },
                    new DialogueChoiceData { choiceText = "(Tenang) Oke, saya fokus kualitas dulu.", nextNodeId = "node_1" }
                },
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "node_1",
                fallbackLine = "Sip. Kita mulai sesi sekarang.",
                npcFollowUpText = followUp,
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    public DialogueGraphData BuildPostTrainingDialogue(GymSessionData session)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_trainer_post";
        graph.npcDisplayName = trainerDisplayName;
        graph.startNodeId = "node_0";

        string resultLine;
        switch (session.result)
        {
            case GymSessionResult.Excellent:
                resultLine = "Performa kamu bagus banget hari ini. Adaptasi naik dengan fatigue yang masih terkontrol.";
                break;
            case GymSessionResult.Strained:
                resultLine = "Sesi ini agak berat buat kondisi kamu sekarang. Tetap dapat progres, tapi fatigue naik cukup tinggi.";
                break;
            case GymSessionResult.Solid:
                resultLine = "Latihan rapi dan konsisten. Progresnya stabil dan ini yang kita cari untuk jangka panjang.";
                break;
            default:
                resultLine = "Sesi belum kebaca sempurna, tapi tetap kita catat sebagai latihan hari ini.";
                break;
        }

        string summary = string.Format(
            "Tier: {0} | Adaptasi +{1:0.0} | Fatigue +{2:0.0}",
            GetTierLabel(session.tierAtStart),
            Mathf.Max(0f, session.adaptationGain),
            Mathf.Max(0f, session.fatigueGain));

        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = resultLine,
                npcFollowUpText = summary,
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }

    private static string GetTierLabel(GymTier tier)
    {
        switch (tier)
        {
            case GymTier.Advanced:
                return "Advanced";
            case GymTier.Regular:
                return "Regular";
            default:
                return "Beginner";
        }
    }
}
