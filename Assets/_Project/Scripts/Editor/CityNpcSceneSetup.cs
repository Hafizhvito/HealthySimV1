#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class CityNpcSceneSetup
{
    private const string DialogueFolder = "Assets/_Project/Data/Dialogues/CityNpc";
    private const string PrefabFolder = "Assets/_Project/Prefabs/CityNPCs";
    private const string ParentName = "CityNPCs";

    private struct NpcDef
    {
        public string name;
        public string displayName;
        public string modelPrefabPath;
        public Vector3 position;
        public float wanderRadius;
        public string[] dialogueAssetNames;
    }

    private static readonly NpcDef[] CityNpcs =
    {
        new NpcDef
        {
            name = "CityNpc_Budi",
            displayName = "Budi",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal man b.prefab",
            position = new Vector3(-305f, 0.3f, -75f),
            wanderRadius = 12f,
            dialogueAssetNames = new[] { "CityNpc_Budi_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Dewi",
            displayName = "Dewi",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal woman a.prefab",
            position = new Vector3(-290f, 0.3f, 50f),
            wanderRadius = 14f,
            dialogueAssetNames = new[] { "CityNpc_Dewi_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Agus",
            displayName = "Agus",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/stout man a.prefab",
            position = new Vector3(-350f, 0.3f, -10f),
            wanderRadius = 10f,
            dialogueAssetNames = new[] { "CityNpc_Agus_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Rina",
            displayName = "Rina",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal woman c.prefab",
            position = new Vector3(-310f, 0.3f, 20f),
            wanderRadius = 13f,
            dialogueAssetNames = new[] { "CityNpc_Rina_01" }
        },
        new NpcDef
        {
            name = "CityNpc_PakHarto",
            displayName = "Pak Harto",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/strong man c.prefab",
            position = new Vector3(-330f, 0.3f, -50f),
            wanderRadius = 18f,
            dialogueAssetNames = new[] { "CityNpc_PakHarto_01" }
        },
        new NpcDef
        {
            name = "CityNpc_BuAni",
            displayName = "Bu Ani",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/stout woman a.prefab",
            position = new Vector3(-355f, 0.3f, -25f),
            wanderRadius = 10f,
            dialogueAssetNames = new[] { "CityNpc_BuAni_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Dimas",
            displayName = "Dimas",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal man c.prefab",
            position = new Vector3(-275f, 0.3f, -80f),
            wanderRadius = 12f,
            dialogueAssetNames = new[] { "CityNpc_Dimas_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Lestari",
            displayName = "Lestari",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/strong woman a.prefab",
            position = new Vector3(-295f, 0.3f, 45f),
            wanderRadius = 14f,
            dialogueAssetNames = new[] { "CityNpc_Lestari_01" }
        },
        new NpcDef
        {
            name = "CityNpc_Joko",
            displayName = "Joko",
            modelPrefabPath = "Assets/DavidJalbert/LowPolyPeople/Prefabs/stout man b.prefab",
            position = new Vector3(-360f, 0.3f, 5f),
            wanderRadius = 15f,
            dialogueAssetNames = new[] { "CityNpc_Joko_01" }
        },
    };

    // ──────── Menu Items ────────

    [MenuItem("HealthySim/City NPCs/1 - Generate All City NPC Dialogues")]
    public static void GenerateDialogues()
    {
        EnsureFolder(DialogueFolder);

        int created = 0;
        created += CreateDialogue_Budi();
        created += CreateDialogue_Dewi();
        created += CreateDialogue_Agus();
        created += CreateDialogue_Rina();
        created += CreateDialogue_PakHarto();
        created += CreateDialogue_BuAni();
        created += CreateDialogue_Dimas();
        created += CreateDialogue_Lestari();
        created += CreateDialogue_Joko();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CityNPC] Dialogue generation done. Created/updated {created} assets in {DialogueFolder}.");
    }

    [MenuItem("HealthySim/City NPCs/2 - Setup City Wandering NPCs")]
    public static void SetupCityNpcs()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[CityNPC] Buka SampleScene dulu sebelum setup.");
            return;
        }

        Transform parent = FindOrCreateParent();
        int count = 0;

        foreach (NpcDef def in CityNpcs)
        {
            if (FindExistingByName(def.name) != null)
            {
                Debug.Log($"[CityNPC] {def.name} sudah ada, skip.");
                continue;
            }

            GameObject npc = CreateNpcGameObject(def, parent);
            if (npc != null) count++;
        }

        UpgradeExistingSari(parent);

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[CityNPC] Setup selesai. {count} NPC baru ditambahkan di bawah '{ParentName}'.\n" +
            "Langkah selanjutnya: Bake NavMesh via HealthySim > City NPCs > 3 - Bake City NavMesh.");
    }

    [MenuItem("HealthySim/City NPCs/3 - Bake City NavMesh")]
    public static void BakeCityNavMesh()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[CityNPC] Buka SampleScene dulu.");
            return;
        }

        System.Type surfaceType = System.Type.GetType(
            "Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");

        if (surfaceType != null)
        {
            BakeWithNavMeshSurface(surfaceType);
        }
        else
        {
            BakeWithLegacy();
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem("HealthySim/City NPCs/4 - Rebuild Interactable_NPC (Sari)")]
    public static void RebuildSariNpc()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[CityNPC] Buka SampleScene dulu.");
            return;
        }

        GameObject sari = FindExistingByName("Interactable_NPC");
        if (sari == null)
        {
            Debug.LogError("[CityNPC] Interactable_NPC tidak ditemukan.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(sari, "Rebuild Sari NPC");

        Transform parent = FindOrCreateParent();
        if (sari.transform.parent != parent)
            sari.transform.SetParent(parent, true);

        Vector3 pos = sari.transform.position;
        pos.y = 0.3f;
        sari.transform.position = pos;

        NpcVisualModelSlot slot = sari.GetComponent<NpcVisualModelSlot>();
        if (slot == null)
            slot = sari.AddComponent<NpcVisualModelSlot>();

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/DavidJalbert/LowPolyPeople/Prefabs/normal woman c.prefab");
        if (modelPrefab != null)
        {
            SetPrivateField(slot, "characterPrefab", modelPrefab);
            slot.RebuildFromPrefab();
        }
        else
        {
            Debug.LogWarning("[CityNPC] Prefab normal woman c tidak ditemukan.");
        }

        UpgradeExistingSari(parent);
        EditorUtility.SetDirty(sari);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[CityNPC] Interactable_NPC (Sari) visual direbuild + wander dicek.");
    }

    [MenuItem("HealthySim/City NPCs/5 - Upgrade Restoran NPC")]
    public static void UpgradeRestoranNpc()
    {
        if (!IsSampleSceneActive())
        {
            Debug.LogWarning("[CityNPC] Buka SampleScene dulu.");
            return;
        }

        GameObject restoran = FindExistingByName("Interactable_NPC_Restoran");
        if (restoran == null)
        {
            Debug.LogError("[CityNPC] Interactable_NPC_Restoran tidak ditemukan.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(restoran, "Upgrade Restoran NPC");

        Transform cityParent = FindExistingByName(ParentName)?.transform;
        if (cityParent != null && restoran.transform.parent == cityParent)
            restoran.transform.SetParent(null, true);

        Vector3 pos = restoran.transform.position;
        pos.y = 0.3f;
        restoran.transform.position = pos;

        CapsuleCollider col = restoran.GetComponent<CapsuleCollider>();
        if (col != null)
            col.center = new Vector3(0f, 1f, 0f);

        NavMeshAgent agent = restoran.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = restoran.AddComponent<NavMeshAgent>();

        agent.speed = 1f;
        agent.angularSpeed = 0f;
        agent.stoppingDistance = 0.4f;
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 0f;
        agent.updateRotation = false;

        NpcWanderController wander = restoran.GetComponent<NpcWanderController>();
        if (wander == null)
            wander = restoran.AddComponent<NpcWanderController>();
        SetPrivateField(wander, "wanderRadius", 7f);

        if (restoran.GetComponent<NpcLocomotionAnimator>() == null)
            restoran.AddComponent<NpcLocomotionAnimator>();

        NpcRestaurantInteractable restaurantNpc = restoran.GetComponent<NpcRestaurantInteractable>();
        if (restaurantNpc != null)
        {
            GameObject foodSource = FindExistingByName("Interactable_Food_Restaurant");
            if (foodSource != null)
            {
                FoodPickupInteractable pickup = foodSource.GetComponent<FoodPickupInteractable>();
                if (pickup != null)
                    SetPrivateField(restaurantNpc, "restaurantFoodSource", pickup);
            }
        }

        EditorUtility.SetDirty(restoran);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[CityNPC] Interactable_NPC_Restoran diupgrade: wander lokal + menu hybrid.");
    }

    private static void BakeWithNavMeshSurface(System.Type surfaceType)
    {
        GameObject groundParent = FindExistingByName("Ground");
        if (groundParent == null)
            groundParent = FindExistingByName("NewEnvironment");
        if (groundParent == null)
        {
            Debug.LogError("[CityNPC] Tidak ditemukan Ground atau NewEnvironment.");
            return;
        }

        Component surface = groundParent.GetComponent(surfaceType);
        if (surface == null)
        {
            Undo.RegisterFullObjectHierarchyUndo(groundParent, "Add NavMeshSurface");
            surface = groundParent.AddComponent(surfaceType);
        }

        var agentTypeID = surfaceType.GetProperty("agentTypeID");
        if (agentTypeID != null)
            agentTypeID.SetValue(surface, 0);

        var collectObjects = surfaceType.GetProperty("collectObjects");
        if (collectObjects != null)
        {
            System.Type collectEnum = collectObjects.PropertyType;
            object allValue = System.Enum.Parse(collectEnum, "All");
            collectObjects.SetValue(surface, allValue);
        }

        var overrideVoxelSize = surfaceType.GetProperty("overrideVoxelSize");
        if (overrideVoxelSize != null)
            overrideVoxelSize.SetValue(surface, false);

        var buildMethod = surfaceType.GetMethod("BuildNavMesh");
        if (buildMethod != null)
        {
            buildMethod.Invoke(surface, null);
            Debug.Log("[CityNPC] NavMesh baked via NavMeshSurface pada " +
                groundParent.name + ". Agent radius = 0.5.");
        }
        else
        {
            Debug.LogError("[CityNPC] NavMeshSurface.BuildNavMesh() tidak ditemukan.");
        }
    }

    private static void BakeWithLegacy()
    {
        int marked = 0;
        string[] groundNames = { "RoadObject", "TerrainObject", "GardenObject" };
        foreach (string gName in groundNames)
        {
            GameObject go = FindExistingByName(gName);
            if (go == null) continue;
            MarkStaticRecursive(go);
            marked++;
        }

        GameObject ground = FindExistingByName("Ground");
        if (ground != null)
        {
            MarkStaticRecursive(ground);
            marked++;
        }

#pragma warning disable CS0618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618

        Debug.Log($"[CityNPC] NavMesh baked (legacy). Marked {marked} root objects as static.");
    }

    private static void MarkStaticRecursive(GameObject go)
    {
        go.isStatic = true;
        foreach (Transform child in go.transform)
            MarkStaticRecursive(child.gameObject);
    }

    // ──────── NPC Creation ────────

    private static GameObject CreateNpcGameObject(NpcDef def, Transform parent)
    {
        GameObject npc = new GameObject(def.name);
        Undo.RegisterCreatedObjectUndo(npc, $"Create {def.name}");
        npc.transform.SetParent(parent, false);
        npc.transform.position = def.position;

        CapsuleCollider col = npc.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.radius = 0.5f;
        col.height = 2f;
        col.isTrigger = false;

        NpcDialogueInteractable dialogue = npc.AddComponent<NpcDialogueInteractable>();
        SetPrivateField(dialogue, "npcName", def.displayName);
        SetPrivateField(dialogue, "useGlobalDialogueCatalogWhenEmpty", false);
        AssignDialogues(dialogue, def.dialogueAssetNames);

        NpcVisualModelSlot slot = npc.AddComponent<NpcVisualModelSlot>();
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.modelPrefabPath);
        if (modelPrefab != null)
        {
            SetPrivateField(slot, "characterPrefab", modelPrefab);
            slot.ApplyVisual();
        }
        else
        {
            Debug.LogWarning($"[CityNPC] Model not found: {def.modelPrefabPath}");
        }

        NavMeshAgent agent = npc.AddComponent<NavMeshAgent>();
        agent.speed = 1.2f;
        agent.angularSpeed = 0f;
        agent.stoppingDistance = 0.4f;
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 0f;
        agent.updateRotation = false;

        NpcWanderController wander = npc.AddComponent<NpcWanderController>();
        SetPrivateField(wander, "wanderRadius", def.wanderRadius);

        npc.AddComponent<NpcLocomotionAnimator>();

        EditorUtility.SetDirty(npc);
        return npc;
    }

    private static void UpgradeExistingSari(Transform parent)
    {
        GameObject sari = FindExistingByName("Interactable_NPC");
        if (sari == null) return;

        Undo.RegisterFullObjectHierarchyUndo(sari, "Upgrade Sari NPC");

        if (sari.transform.parent != parent)
            sari.transform.SetParent(parent, true);

        CapsuleCollider col = sari.GetComponent<CapsuleCollider>();
        if (col != null)
            col.center = new Vector3(0f, 1f, 0f);

        NavMeshAgent agent = sari.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = sari.AddComponent<NavMeshAgent>();

        agent.speed = 1.2f;
        agent.angularSpeed = 0f;
        agent.stoppingDistance = 0.4f;
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 0f;
        agent.updateRotation = false;

        Vector3 pos = sari.transform.position;
        pos.y = 0.3f;
        sari.transform.position = pos;

        if (sari.GetComponent<NpcWanderController>() == null)
        {
            NpcWanderController wander = sari.AddComponent<NpcWanderController>();
            SetPrivateField(wander, "wanderRadius", 14f);
        }

        if (sari.GetComponent<NpcLocomotionAnimator>() == null)
            sari.AddComponent<NpcLocomotionAnimator>();

        EditorUtility.SetDirty(sari);
        Debug.Log("[CityNPC] Existing Interactable_NPC (Sari) upgraded with wander components.");
    }

    // ──────── Dialogue Generators ────────

    private static int CreateDialogue_Budi()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Budi_01");
        g.npcId = "city_budi";
        g.npcDisplayName = "Budi";
        g.initialTrust = 20f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Duh, baru selesai meeting yang panjang banget. Kepala pening, badan pegel...",
                "", false, false,
                new[] {
                    Choice("Istirahat dulu, Mas. Jangan dipaksain.", "rest"),
                    Choice("Udah makan siang belum?", "food"),
                    Choice("Semangat ya, Mas!", "end_semangat")
                }),
            MakeNode("rest",
                "Iya sih, harusnya gitu ya. Tapi deadline numpuk, Bos nagih terus...",
                "Makasih ya udah ngingetin. Kayaknya emang harus ambil napas dulu.",
                false, true, null),
            MakeNode("food",
                "Belum nih, dari tadi cuma ngopi. Emang kalau ga makan, makin mumet ya?",
                "Oke deh, habis ini aku cari makan. Makasih remindernya!",
                false, true, null),
            MakeNode("end_semangat",
                "Haha, makasih ya! Jarang-jarang ada yang nyemangatin.",
                "", false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Dewi()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Dewi_01");
        g.npcId = "city_dewi";
        g.npcDisplayName = "Dewi";
        g.initialTrust = 25f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Lagi mikirin menu makan malem nih. Pengen yang sehat tapi anak-anak doyan...",
                "", false, false,
                new[] {
                    Choice("Coba bikin sayur yang tampilannya menarik.", "sayur"),
                    Choice("Buah buat dessert aja, Bu.", "buah"),
                    Choice("Anak-anak suka apa biasanya?", "tanya")
                }),
            MakeNode("sayur",
                "Oh iya ya! Kayak bento gitu ya, ditata lucu-lucu? Bisa dicoba itu!",
                "Makasih idenya! Nanti aku coba bikin wortel bentuk bintang haha.",
                false, true, null),
            MakeNode("buah",
                "Bener juga! Mangga sama pisang lagi murah minggu ini.",
                "Oke, nanti bikin salad buah aja. Simple tapi tetep sehat!",
                false, true, null),
            MakeNode("tanya",
                "Ayam goreng mulu... Tapi kan ga sehat kalau tiap hari. Dilema emak-emak, deh.",
                "Ya sudah, nanti aku coba ayam panggang aja. Tetep ayam tapi lebih sehat.",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Agus()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Agus_01");
        g.npcId = "city_agus";
        g.npcDisplayName = "Agus";
        g.initialTrust = 30f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Eh, udah denger belum? Warung baru buka di ujung jalan. Katanya makanannya sehat-sehat gitu.",
                "", false, false,
                new[] {
                    Choice("Oh ya? Kayak gimana emang?", "detail"),
                    Choice("Males ah, sehat tapi ga enak.", "skeptis"),
                    Choice("Aku mau coba deh!", "antusias")
                }),
            MakeNode("detail",
                "Katanya sih salad, smoothie, sama nasi brown rice gitu. Rame banget lho kemarin!",
                "Cobain deh, siapa tau cocok sama selera kamu.",
                false, true, null),
            MakeNode("skeptis",
                "Hahaha, itu mah stereotip. Sekarang makanan sehat udah enak-enak, Bos.",
                "Coba dulu baru judge. Jangan kayak aku dulu, nyesel telat nyadar.",
                false, true, null),
            MakeNode("antusias",
                "Nah, bagus! Lokasinya deket Rumah Sakit situ, ga jauh kok.",
                "Semoga cocok ya sama seleramu!",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Rina()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Rina_01");
        g.npcId = "city_rina";
        g.npcDisplayName = "Rina";
        g.initialTrust = 15f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Hai! Kamu tau nggak, jalan kaki 30 menit sehari itu udah cukup buat jaga kesehatan jantung lho!",
                "", false, false,
                new[] {
                    Choice("Beneran? Aku jarang olahraga...", "jarang"),
                    Choice("Iya, aku suka jalan pagi!", "suka"),
                    Choice("Kamu mahasiswa kesehatan ya?", "tanya")
                }),
            MakeNode("jarang",
                "Ga harus olahraga berat kok! Mulai dari jalan kaki santai aja udah bagus.",
                "Yang penting konsisten. Seminggu 3-4 kali aja udah berasa bedanya!",
                false, true, null),
            MakeNode("suka",
                "Wah, bagus banget! Jalan pagi itu double benefit — olahraga plus kena sinar matahari buat vitamin D.",
                "Keep it up ya!",
                false, true, null),
            MakeNode("tanya",
                "Iya, semester 6 di FK. Lagi skripsi tentang gaya hidup urban dan kesehatan.",
                "Makanya aku suka sharing info kesehatan, hehe. Lumayan buat data juga!",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_PakHarto()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_PakHarto_01");
        g.npcId = "city_pakharto";
        g.npcDisplayName = "Pak Harto";
        g.initialTrust = 35f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Wah, keliatannya kamu capek banget, Nak. Jangan lupa istirahat yang cukup ya.",
                "", false, false,
                new[] {
                    Choice("Iya Pak, lagi banyak kerjaan.", "kerja"),
                    Choice("Bapak sendiri sehat-sehat aja?", "sehat"),
                    Choice("Makasih Pak, Bapak perhatian banget.", "makasih")
                }),
            MakeNode("kerja",
                "Kerja boleh, tapi badan juga harus dijaga. Bapak dulu kerja keras nonstop, ujung-ujungnya masuk RS.",
                "Sekarang Bapak jalan kaki keliling sini tiap hari. Badan sehat, pikiran juga fresh.",
                false, true, null),
            MakeNode("sehat",
                "Alhamdulillah, Nak. Rahasianya? Tidur cukup, makan teratur, sama jalan kaki tiap hari.",
                "Dulu Bapak 90 kilo, sekarang udah turun 15 kilo. Pelan-pelan aja yang penting konsisten.",
                false, true, null),
            MakeNode("makasih",
                "Haha, Bapak udah tua. Yang bisa dilakuin ya mengingatkan orang muda kayak kamu.",
                "Jaga kesehatanmu ya, Nak. Kesehatan itu investasi paling mahal.",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_BuAni()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_BuAni_01");
        g.npcId = "city_buani";
        g.npcDisplayName = "Bu Ani";
        g.initialTrust = 30f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Mau coba jamu, Dek? Hari ini ada jahe merah, bagus buat daya tahan tubuh!",
                "", false, false,
                new[] {
                    Choice("Boleh deh, jahe ya Bu!", "beli"),
                    Choice("Jamu itu beneran manjur ya, Bu?", "tanya"),
                    Choice("Ga deh Bu, makasih.", "tolak")
                }),
            MakeNode("beli",
                "Nih, jahe merah campur madu sama sereh. Anget-anget enak!",
                "Diminum rutin ya, Dek. Biar badan tetep fit!",
                false, true, null),
            MakeNode("tanya",
                "Ya kalo kata Ibu sih manjur, Dek. Jahe itu anti-inflamasi, kunyit bagus buat pencernaan.",
                "Nenek moyang kita udah pake dari dulu. Sekarang malah banyak penelitian yang buktiin!",
                false, true, null),
            MakeNode("tolak",
                "Ya gapapa, Dek. Kapan-kapan aja kalau mau. Ibu di sini tiap hari kok!",
                "", false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Dimas()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Dimas_01");
        g.npcId = "city_dimas";
        g.npcDisplayName = "Dimas";
        g.initialTrust = 20f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Bro! Baru dari gym nih. Hari ini leg day, gila pumping-nya mantep banget!",
                "", false, false,
                new[] {
                    Choice("Kamu nge-gym tiap hari?", "rutin"),
                    Choice("Aku belum pernah ke gym sih...", "pemula"),
                    Choice("Nice! Aku juga suka workout.", "suka")
                }),
            MakeNode("rutin",
                "Enggak sih, 4-5x seminggu. Yang penting ada rest day biar otot recover.",
                "Dulu aku maksa tiap hari, malah cedera. Sekarang lebih smart, dengerin badan.",
                false, true, null),
            MakeNode("pemula",
                "Ga masalah, Bro! Semua orang mulai dari nol. Yang penting mulai dulu.",
                "Coba dateng aja ke gym sini, minta trainer bantu bikin program pemula. Dijamin seru!",
                false, true, null),
            MakeNode("suka",
                "Mantep! Kamu fokus apa? Strength, cardio, atau mix?",
                "Kalo butuh temen gym, kabarin aja ya. Workout bareng lebih semangat!",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Lestari()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Lestari_01");
        g.npcId = "city_lestari";
        g.npcDisplayName = "Lestari";
        g.initialTrust = 25f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Selamat siang! Jangan lupa cek kesehatan rutin ya, minimal setahun sekali.",
                "", false, false,
                new[] {
                    Choice("Cek apa aja biasanya?", "detail"),
                    Choice("Terakhir cek udah lama banget...", "lama"),
                    Choice("Mbak kerja di RS sini ya?", "tanya")
                }),
            MakeNode("detail",
                "Minimal cek darah lengkap, gula darah, kolesterol, sama tensi. " +
                "Kalau umur di atas 40, tambah cek jantung.",
                "Lebih baik ketahuan awal daripada telat. Pencegahan itu lebih murah dari pengobatan!",
                false, true, null),
            MakeNode("lama",
                "Wah, jangan ditunda-tunda ya. Banyak penyakit yang ga ada gejalanya di awal.",
                "Mumpung RS-nya deket, coba jadwalin minggu depan. Ga ribet kok prosesnya!",
                false, true, null),
            MakeNode("tanya",
                "Iya, aku perawat di UGD. Shift pagi, siang jalan-jalan sebentar buat istirahat.",
                "Makanya aku suka ngingetin orang buat jaga kesehatan. Soalnya di UGD sering liat yang telat dateng...",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    private static int CreateDialogue_Joko()
    {
        DialogueGraphData g = GetOrCreate("CityNpc_Joko_01");
        g.npcId = "city_joko";
        g.npcDisplayName = "Joko";
        g.initialTrust = 25f;
        g.startNodeId = "start";
        g.nodes = new List<DialogueNodeData>
        {
            MakeNode("start",
                "Dagangan hari ini lumayan laris! Tapi badan capek juga ya jalan terus dari pagi.",
                "", false, false,
                new[] {
                    Choice("Istirahat dulu, Mas. Duduk bentar.", "istirahat"),
                    Choice("Pasti capek ya jualan keliling?", "capek"),
                    Choice("Jualan apa, Mas?", "jualan")
                }),
            MakeNode("istirahat",
                "Iya nih, kaki udah pegel. Tapi ya namanya cari rejeki, harus semangat!",
                "Makasih ya udah peduli. Jarang-jarang ada yang nyuruh istirahat, haha.",
                false, true, null),
            MakeNode("capek",
                "Capek sih, tapi udah kebiasa. Justru kalau sehari ga jalan, badan malah kaku.",
                "Yang penting makan teratur sama minum air yang banyak. Itu resep Mas Joko!",
                false, true, null),
            MakeNode("jualan",
                "Macem-macem, ada buah potong, jus seger, sama kacang rebus.",
                "Sehat-sehat semua, ga ada yang pake pengawet. Mau coba?",
                false, true, null)
        };
        EditorUtility.SetDirty(g);
        return 1;
    }

    // ──────── Helpers ────────

    private static DialogueGraphData GetOrCreate(string assetName)
    {
        string path = $"{DialogueFolder}/{assetName}.asset";
        DialogueGraphData existing = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
        if (existing != null) return existing;

        DialogueGraphData g = ScriptableObject.CreateInstance<DialogueGraphData>();
        AssetDatabase.CreateAsset(g, path);
        return g;
    }

    private static DialogueNodeData MakeNode(string id, string line, string followUp,
        bool isEnd, bool isTerminal, DialogueChoiceData[] choices)
    {
        DialogueNodeData node = new DialogueNodeData
        {
            nodeId = id,
            fallbackLine = line,
            npcFollowUpText = followUp ?? string.Empty,
            isConversationEnd = isEnd,
            isTerminal = isTerminal,
            choices = choices != null ? new List<DialogueChoiceData>(choices) : new List<DialogueChoiceData>()
        };
        return node;
    }

    private static DialogueChoiceData Choice(string text, string nextNode)
    {
        return new DialogueChoiceData
        {
            choiceText = text,
            nextNodeId = nextNode
        };
    }

    private static void AssignDialogues(NpcDialogueInteractable dialogue, string[] assetNames)
    {
        List<DialogueGraphData> list = new List<DialogueGraphData>();
        foreach (string name in assetNames)
        {
            string path = $"{DialogueFolder}/{name}.asset";
            DialogueGraphData d = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
            if (d != null) list.Add(d);
        }

        SetPrivateField(dialogue, "dialogueOptions", list);
    }

    private static Transform FindOrCreateParent()
    {
        GameObject existing = FindExistingByName(ParentName);
        if (existing != null) return existing.transform;

        GameObject parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create CityNPCs parent");
        parent.transform.position = Vector3.zero;
        return parent.transform;
    }

    private static GameObject FindExistingByName(string objectName)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i].gameObject;
        }
        return null;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        System.Type type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        Debug.LogWarning($"[CityNPC] Field '{fieldName}' not found on {target.GetType().Name}");
    }

    private static bool IsSampleSceneActive()
    {
        return string.Equals(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            "SampleScene",
            System.StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

}
#endif
