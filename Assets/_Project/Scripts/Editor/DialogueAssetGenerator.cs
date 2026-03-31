#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueAssetGenerator
{
    private const string DialogueFolder = "Assets/_Project/Data/Dialogues";

    [MenuItem("HealthSim/Generate Dialogue Assets")]
    public static void GenerateDialogueAssets()
    {
        EnsureDialogueFolderExists();

        CreateNpcUtamaDialogue();
        CreateNpcRestoranFirstVisitDialogue();
        CreateNpcRestoranReturnVisitDialogue();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DialogueAssetGenerator: All assets created successfully.");
    }

    [MenuItem("HealthSim/Generate Boss Dialogue")]
    public static void GenerateBossDialogue()
    {
        EnsureDialogueFolderExists();
        CreateNpcBossPreWorkDialogue();
        CreateNpcBossPostWorkDialogue();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DialogueAssetGenerator: Boss dialogue assets created successfully.");
    }

    private static void CreateNpcUtamaDialogue()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_utama";
        graph.npcDisplayName = "Sari";
        graph.initialTrust = 10f;
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            MakeNode(
                "node_0",
                "Oh- eh, iya? Maaf, kaget. Kirain cuma angin lewat. Kamu... warga sini bukan? Kayaknya belum pernah lihat.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Baru dateng. Lagi orientasi.", "node_1a"),
                    Choice("Iya, emang kamu kenal semua orang di sini?", "node_1b"),
                    Choice("Salah, aku emang angin.", "node_1c")
                }),
            MakeNode(
                "node_1a",
                "Oh baru ya. Pantesan. Mau kemana emang? Daerah sini lumayan gampang nyasar kalau ga tau.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Lagi nyari tempat makan.", "node_2_food"),
                    Choice("Belum tau juga sih.", "node_2_wander")
                }),
            MakeNode(
                "node_1b",
                "Haha, ya enggak juga sih. Tapi muka baru itu keliatan, gitu.",
                "Aku Sari. Biasanya di sini kalau lagi males pulang cepet.",
                false,
                false,
                new[]
                {
                    Choice("Fair enough.", "node_2_wander"),
                    Choice("Namaku [Player]. Kamu?", "node_2_intro")
                }),
            MakeNode(
                "node_1c",
                "...Hah.",
                "Oke lucu sih dikit.",
                false,
                false,
                new[]
                {
                    Choice("Hehe.", "node_2_wander")
                }),
            MakeNode(
                "node_2_food",
                "Tempat makan... tergantung mau yang gimana. Yang deket sini ada warung Pak Yono. Aku sering ke sana kalau lagi males masak.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Makanannya enak?", "node_3_food"),
                    Choice("Oke, makasih.", "node_end_neutral")
                }),
            MakeNode(
                "node_3_food",
                "Enak sih. Simple. Ga berat juga, jadi ga ngantuk habis makan.",
                "Aku paling ga suka makan siang yang bikin ngantuk. Rugi banget.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_2_wander",
                "Santai aja dulu. Daerah sini ga sebesar yang keliatan kok.",
                "Ntar kalau butuh sesuatu, tanya aja ke orang. Rata-rata helpful.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_2_intro",
                "Sari. Biasanya di sini kalau lagi ga ada kerjaan atau males balik duluan.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Enak juga ya.", "node_end_friendly"),
                    Choice("Aku duluan ya.", "node_end_neutral")
                }),
            MakeNode(
                "node_end_friendly",
                "Iya sih. Ntar ketemu lagi mungkin.",
                "",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_end_neutral",
                "Oh, oke. Hati-hati.",
                "",
                true,
                true,
                new ChoiceDef[0])
        };

        WriteAsset(graph, DialogueFolder + "/NPCUtama_Dialogue.asset");
    }

    private static void CreateNpcRestoranFirstVisitDialogue()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_restoran_first";
        graph.npcDisplayName = "Pak Yono";
        graph.initialTrust = 45f;
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            MakeNode(
                "node_0",
                "Eh, silakan silakan- duduk dulu. Lagi ngebersihin ini sebentar. Pertama kali ke sini? Biasanya kalau orang baru, bingung mau pesen apa. Tenang, nanti aku bantu.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Yang paling laku apa?", "node_1_bestseller"),
                    Choice("Yang paling murah aja.", "node_1_cheap"),
                    Choice("Rekomendasiin yang enak menurut kamu.", "node_1_rec")
                }),
            MakeNode(
                "node_1_bestseller",
                "Nasi goreng. Dari dulu sampai sekarang. Ada pelanggan saya- tiap pagi mesen ini, udah bertahun-tahun. Katanya kalau ga sarapan nasi goreng sini, harinya ga bener.",
                "Entah karena emang enak, atau udah jadi ritual. Mau coba?",
                false,
                false,
                new[]
                {
                    Choice("Iya, pesen itu.", "node_end_order"),
                    Choice("Ntar dulu, lihat-lihat dulu.", "node_2_browse")
                }),
            MakeNode(
                "node_1_cheap",
                "Haha, jujur ya. Suka yang jujur. Pisang goreng sama teh manis paling terjangkau. Tapi kalau mau yang ngenyangkin, nasi sama tempe lebih worth it dikit.",
                "Banyak yang langganan combo itu. Murah, cukup buat kerja setengah hari.",
                false,
                false,
                new[]
                {
                    Choice("Pesen nasi tempe.", "node_end_order"),
                    Choice("Pisang goreng aja dulu.", "node_end_order")
                }),
            MakeNode(
                "node_1_rec",
                "Kalau aku yang milih... gado-gado. Bukan yang paling rame dipesan, tapi yang paling sering bikin orang kaget enak. Bahan-bahannya fresh, aku ga kompromi soal itu.",
                "Pelanggan yang pertama kali nyoba biasanya langsung balik lagi. Itu ukuran paling jujur.",
                false,
                false,
                new[]
                {
                    Choice("Oke, pesen gado-gado.", "node_end_order"),
                    Choice("Boleh lihat pilihan lain dulu?", "node_2_browse")
                }),
            MakeNode(
                "node_2_browse",
                "Silakan. Mau yang berat atau yang ringan? Kalau habis aktivitas banyak, mending yang ada nasinya. Kalau mau santai aja, yang sayur atau buah juga ada.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Yang berat sekalian.", "node_3_heavy"),
                    Choice("Yang ringan aja.", "node_3_light")
                }),
            MakeNode(
                "node_3_heavy",
                "Ayam goreng sama nasi. Atau kalau mau yang beda, nasi goreng spesial- ada telurnya. Yang paling sering dipesan habis olahraga pagi.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Pesen ayam goreng nasi.", "node_end_order"),
                    Choice("Nasi goreng spesial.", "node_end_order")
                }),
            MakeNode(
                "node_3_light",
                "Salad sayur, buah apel, atau jus jeruk. Yang jus jeruk itu aku peres sendiri- ga pake sirup. Beda rasanya.",
                "Pelanggan yang lagi jaga makan sering minta itu. Katanya ga bikin eneg.",
                false,
                false,
                new[]
                {
                    Choice("Jus jeruk.", "node_end_order"),
                    Choice("Salad sayur.", "node_end_order")
                }),
            MakeNode(
                "node_end_order",
                "Oke, sebentar ya.",
                "",
                true,
                true,
                new ChoiceDef[0])
        };

        WriteAsset(graph, DialogueFolder + "/NPCRestoran_FirstVisit.asset");
    }

    private static void CreateNpcRestoranReturnVisitDialogue()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_restoran_return";
        graph.npcDisplayName = "Pak Yono";
        graph.initialTrust = 50f;
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            MakeNode(
                "node_0",
                "Oh, yang kemarin. Mau yang sama lagi atau coba yang lain?",
                "",
                false,
                false,
                new[]
                {
                    Choice("Yang sama.", "node_end_order"),
                    Choice("Coba yang lain.", "node_2_browse"),
                    Choice("Tanya dulu boleh?", "node_question")
                }),
            MakeNode(
                "node_question",
                "Boleh, boleh. Mau tanya apa?",
                "",
                false,
                false,
                new[]
                {
                    Choice("Makanan sini fresh semua?", "node_q_fresh"),
                    Choice("Yang paling sehat apa?", "node_q_health")
                }),
            MakeNode(
                "node_q_fresh",
                "Aku belanja subuh. Jadi yang pagi sampai siang, masih fresh. Sore kadang tinggal beberapa. Makanya yang paling enak emang makan di sini pas pagi atau siang.",
                "",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_q_health",
                "Hm... susah jawabnya. Tergantung kondisi kamu juga sih. Tapi kalau aku lihat dari yang beli- yang paling jarang sakit itu yang makannya beragam. Ga cuma satu jenis terus.",
                "Itu bukan teori, itu pengamatan bertahun-tahun jualin makanan.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_2_browse",
                "Silakan. Mau yang berat atau yang ringan?",
                "",
                false,
                false,
                new[]
                {
                    Choice("Yang berat.", "node_end_order"),
                    Choice("Yang ringan.", "node_end_order")
                }),
            MakeNode(
                "node_end_order",
                "Oke, sebentar ya.",
                "",
                true,
                true,
                new ChoiceDef[0])
        };

        WriteAsset(graph, DialogueFolder + "/NPCRestoran_ReturnVisit.asset");
    }

    private static void CreateNpcBossPreWorkDialogue()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            MakeNode(
                "node_0",
                "Oh, kamu udah dateng. Bagus, tepat waktu.",
                "",
                false,
                false,
                new[]
                {
                    Choice("Siap kerja, Pak.", "node_1_ready"),
                    Choice("Hari ini gimana kesibukannya?", "node_1_ask")
                }),
            MakeNode(
                "node_1_ready",
                "Oke. Kalau udah siap, langsung aja mulai. Kerjaan hari ini standar, ga ada yang aneh-aneh.",
                "Yang penting fokus. Kalau badan ga fit, bilang aja - jangan dipaksain.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_1_ask",
                "Lumayan padat sebenernya. Tapi masih manageable. Yang penting kamu dalam kondisi oke.",
                "Kalau kurang fit, hasilnya ga akan maksimal. Istirahat dan makan yang bener itu investasi, bukan buang waktu.",
                true,
                true,
                new ChoiceDef[0])
        };

        WriteAsset(graph, DialogueFolder + "/NPCBoss_PreWork.asset");
    }

    private static void CreateNpcBossPostWorkDialogue()
    {
        DialogueGraphData graph = ScriptableObject.CreateInstance<DialogueGraphData>();
        graph.npcId = "npc_boss_post";
        graph.npcDisplayName = "Pak Hendra";
        graph.startNodeId = "node_0";
        graph.nodes = new List<DialogueNodeData>
        {
            MakeNode(
                "node_0",
                "Kerja bagus hari ini. Kelihatan fit, hasilnya juga beres semua.",
                "Ini gajinya, ada bonusnya sedikit.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_0_partial",
                "Udah berusaha, tapi keliatannya kurang fit tadi. Kerjaan sebagian selesai.",
                "Besok jaga kondisi ya. Makan yang bener sebelum kerja.",
                true,
                true,
                new ChoiceDef[0]),
            MakeNode(
                "node_0_failed",
                "Kamu ga keliatan fit sama sekali tadi. Istirahat dulu aja, jangan dipaksain.",
                "Badan itu modal utama. Kalau ambruk, malah rugi semua.",
                true,
                true,
                new ChoiceDef[0])
        };

        WriteAsset(graph, DialogueFolder + "/NPCBoss_PostWork.asset");
    }

    private static DialogueNodeData MakeNode(
        string id,
        string npcText,
        string npcFollowUpText,
        bool isConversationEnd,
        bool isTerminal,
        ChoiceDef[] choices)
    {
        DialogueNodeData node = new DialogueNodeData
        {
            nodeId = id,
            fallbackLine = npcText,
            variationPool = new List<string>(),
            choices = new List<DialogueChoiceData>(),
            npcFollowUpText = npcFollowUpText,
            isConversationEnd = isConversationEnd,
            isTerminal = isTerminal
        };

        for (int i = 0; i < choices.Length; i++)
        {
            node.choices.Add(new DialogueChoiceData
            {
                choiceText = choices[i].Label,
                nextNodeId = choices[i].NextNodeId,
                consequence = null,
                gate = null
            });
        }

        return node;
    }

    private static ChoiceDef Choice(string label, string nextNodeId)
    {
        return new ChoiceDef(label, nextNodeId);
    }

    private static void WriteAsset(DialogueGraphData graph, string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<DialogueGraphData>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(graph, assetPath);
        EditorUtility.SetDirty(graph);
    }

    private static void EnsureDialogueFolderExists()
    {
        if (AssetDatabase.IsValidFolder("Assets/_Project/Data/Dialogues"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            AssetDatabase.CreateFolder("Assets/_Project", "Data");

        AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogues");
    }

    private struct ChoiceDef
    {
        public readonly string Label;
        public readonly string NextNodeId;

        public ChoiceDef(string label, string nextNodeId)
        {
            Label = label;
            NextNodeId = nextNodeId;
        }
    }
}
#endif
