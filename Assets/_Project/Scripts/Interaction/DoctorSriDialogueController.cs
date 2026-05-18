using System.Collections.Generic;
using UnityEngine;

public class DoctorSriDialogueController : MonoBehaviour
{
    [SerializeField] private string doctorDisplayName = "dr. Sri Wuryanti, MS, Sp.GK";

    public DialogueGraphData BuildConsultationDialogue(float healthScore, float dailyFat, float dailyProtein)
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_doctor_sri";
        graph.npcDisplayName = doctorDisplayName;
        graph.startNodeId = "node_0";

        string opener;
        if (healthScore >= 70f)
            opener = "Selamat datang. Saya sudah melihat pola hidupmu belakangan ini — kondisi gizimu sangat baik! Pertahankan konsistensi ini.";
        else if (healthScore >= 50f)
            opener = "Kondisi gizimu cukup baik, namun ada beberapa hal yang perlu diperhatikan. Asupan nutrisi harianmu perlu sedikit penyesuaian.";
        else if (healthScore >= 30f)
            opener = "Kondisi gizimu mulai mengkhawatirkan. Tubuhmu memberikan sinyal yang tidak boleh diabaikan. Kita perlu bicara serius.";
        else
            opener = "Kondisi gizimu buruk. Jika pola ini terus berlanjut, risiko penyakit serius akan sangat meningkat. Ini harus segera diperbaiki.";

        if (dailyFat > 65f)
            opener += " Lemak harianmu terlalu tinggi — batasi gorengan dan makanan berlemak.";

        if (dailyProtein < 40f)
            opener += " Protein harianmu kurang — tambahkan telur, tahu, tempe, atau ikan.";

        string advice;
        if (healthScore >= 70f)
            advice = "Terus jaga pola makan seimbang dan rutin berolahraga. Tubuh yang sehat adalah investasi terbaik untuk masa depanmu.";
        else if (healthScore >= 50f)
            advice = "Perbanyak sayuran dan protein. Kurangi makanan tinggi lemak dan gula. Olahraga minimal 3x seminggu sudah cukup.";
        else if (healthScore >= 30f)
            advice = "Mulai dari hal kecil — pilih makanan bergizi, tidur cukup, dan jangan lewatkan olahraga. Konsistensi itu kuncinya.";
        else
            advice = "Prioritaskan perubahan sekarang. Mulai dengan makan teratur, pilih makanan bergizi, dan hindari junk food sepenuhnya.";

        graph.nodes = new List<DialogueNodeData>
        {
            new DialogueNodeData
            {
                nodeId = "node_0",
                fallbackLine = opener,
                choices = new List<DialogueChoiceData>
                {
                    new DialogueChoiceData { choiceText = "(Serius) Apa yang harus saya lakukan, Dok?", nextNodeId = "node_1" },
                    new DialogueChoiceData { choiceText = "(Santai) Terima kasih atas informasinya, Dok.", nextNodeId = "node_1" }
                },
                isConversationEnd = false,
                isTerminal = false
            },
            new DialogueNodeData
            {
                nodeId = "node_1",
                fallbackLine = advice,
                npcFollowUpText = "Jaga kesehatanmu baik-baik. Pintu klinik selalu terbuka untukmu.",
                choices = new List<DialogueChoiceData>(),
                isConversationEnd = true,
                isTerminal = true
            }
        };

        return graph;
    }
}
