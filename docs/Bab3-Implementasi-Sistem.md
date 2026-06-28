# Bab 3 — Implementasi Sistem HealthSim

Dokumen ini berisi cuplikan kode implementasi beserta penjelasan naratif untuk disalin ke skripsi.

**Format standar setiap entri (untuk Word/skripsi):**

1. **File** — path script di project (`Assets/_Project/Scripts/...`)
2. **Potongan kode** — method atau logika inti (bukan seluruh file)
3. **Penjelasan** — naratif panjang yang wajib memuat tiga unsur berikut:
   - **Alur kerja** — apa yang dilakukan kode, langkah demi langkah
   - **Alasan desain** — mengapa pendekatan, rumus, atau nilai parameter tersebut dipilih (bukan sekadar "apa")
   - **Integrasi** — script/komponen lain yang terhubung dan dampaknya

Contoh kalimat alasan yang baik: *"Drain dibatasi dengan `movementDrainScale` 0,30 agar pemain sempat menyelesaikan aktivitas harian dalam durasi satu hari simulasi (~4 menit real-time), tanpa pingsan terlalu cepat yang mengganggu eksplorasi edukatif."*

Di bawah setiap judul method, baris **File:** menunjukkan sumber kode. Bagian awal memuat subsistem inti dengan penjelasan lengkap; bagian **Kelengkapan** menambah method dan script pendukung.

---

# BATCH 1 — Core & Health Systems

---

## PlayerStats

**File:** `Assets/_Project/Scripts/Core/PlayerStats.cs`

`PlayerStats` adalah komponen inti berupa singleton `MonoBehaviour` dengan `DontDestroyOnLoad`. Script ini menyimpan seluruh variabel status pemain—energi, mood, nutrisi harian, uang, skor kesehatan per fase, counter perilaku, serta progresi usia—dan menjadi sumber data tunggal yang dibaca evaluator kesehatan, HUD, dan sistem ending.

**Alasan memakai singleton persisten:** Data pemain harus tetap ada saat berpindah scene (kota → kantor → gym → kota). Tanpa pola ini, stat akan ter-reset setiap `LoadScene` dan evaluasi kesehatan multi-hari tidak mungkin dijalankan. Memusatkan state di satu komponen juga menghindari duplikasi logika di puluhan script interaksi.

### DrainEnergy()

```csharp
public void DrainEnergy(bool isWalking, bool isRunning, float deltaTime)
{
    if (currentEnergyState == EnergyState.Fainted) return;

    float drainRate = energyDrainIdle;
    if (isRunning)
    {
        runningDuration += Mathf.Max(0f, deltaTime);
        float runRamp = Mathf.Clamp01(runningDuration / Mathf.Max(0.1f, runDrainRampSeconds));
        drainRate = Mathf.Lerp(energyDrainWalk, energyDrainRun, runRamp);
    }
    else if (isWalking)
    {
        runningDuration = 0f;
        drainRate = energyDrainWalk;
    }
    else
    {
        runningDuration = 0f;
    }

    float drainModifier = Mathf.Clamp(movementDrainModifier, 0.75f, 1.25f);
    float graceMultiplier = postActivityTravelGraceActive
        ? Mathf.Clamp(postActivityTravelGraceMultiplier, 0.1f, 1f)
        : 1f;
    float scaledDrain = drainRate * Mathf.Clamp(movementDrainScale, 0.05f, 1f)
                      * drainModifier * graceMultiplier;
    ModifyEnergy(-(scaledDrain * deltaTime));
}
```

----

Method ini dipanggil setiap frame oleh `PlayerController` saat pemain bergerak. Alurnya dimulai dengan pemilihan laju drain dasar: diam (`energyDrainIdle` = 0,2 poin/detik), berjalan (0,45), atau berlari. Untuk lari, sistem tidak langsung memakai nilai maksimum, melainkan menaikkan drain secara bertahap melalui `runRamp`—interpolasi linear dari walk ke run selama `runDrainRampSeconds` (1,2 detik). Hal ini mensimulasikan bahwa lari singkat masih terkendali, tetapi lari terus-menerus cepat menguras energi.

Drain akhir dihitung: ΔE = −(drainRate × movementDrainScale × drainModifier × graceMultiplier) × Δt.

**Alasan desain:**
- **`movementDrainScale` (0,30):** Nilai drain "mentah" sengaja diskalakan ke 30% karena satu hari simulasi hanya berlangsung ~4 menit real-time. Tanpa skala ini, pemain akan pingsan sebelum sempat makan, kerja, atau gym—mengganggu tujuan edukatif simulasi.
- **`runRamp`:** Memberi feedback gradual; pemain sempat mengubah strategi (berhenti berlari) sebelum energi kolaps.
- **`drainModifier` per fase usia:** Lansia mendapat pengali drain lebih tinggi (via `ApplyPhaseModifiers`) karena kebutuhan energi metabolisme dan mobilitas berbeda—mencerminkan literatur bahwa aktivitas fisik pada usia lanjut terasa lebih melelahkan.
- **`graceMultiplier` (travel grace):** Setelah gym/kerja, drain perjalanan diturunkan hingga pemain makan. Alasannya: dalam kehidupan nyata, tubuh dalam recovery pasca-aktivitas berat; tanpa mekanisme ini, pemain "dihukum ganda" (habis energi di gym lalu pingsan di jalan pulang) yang terasa tidak adil secara pedagogis.

Hasil dikirim ke `ModifyEnergy`, yang memperbarui `currentEnergyState` (Normal/Warning/Critical) dan memicu event ke `HUDManager` agar bar energi bereaksi tanpa polling manual.

### RegisterHealthScore() dan GetAveragePhaseScore()

```csharp
public void RegisterHealthScore(float delta)
{
    healthScoreThisPhase = Mathf.Clamp(healthScoreThisPhase + delta, 0f, 100f);
    TryRearmHealthGuidance(40f);
}

public float GetAveragePhaseScore()
{
    int currentIndex = (int)currentAgeStage;
    if (currentIndex >= 0 && currentIndex < committedPhaseScores.Length)
        committedPhaseScores[currentIndex] = healthScoreThisPhase;

    float total = 0f;
    for (int i = 0; i < 3; i++)
        total += committedPhaseScores[i];

    return total / 3f;
}
```

----

**Alur kerja:** Setiap malam, `SleepBedInteractable` memanggil `DailyHealthEvaluator.Evaluate()` yang menghasilkan `totalDelta`. Nilai ini masuk ke `RegisterHealthScore` dan ditambahkan ke `healthScoreThisPhase`, lalu di-clamp 0–100 agar skor tidak keluar dari skala persentase yang mudah dipahami pengguna. `TryRearmHealthGuidance` memeriksa apakah panel peringatan kesehatan perlu diaktifkan kembali setelah skor membaik.

`GetAveragePhaseScore` dipanggil saat ending (hari ke-12). Sebelum menghitung rata-rata, skor fase yang sedang berjalan di-*commit* ke `committedPhaseScores[indeksFase]`. Rata-rata dihitung dari tiga slot: Muda, Dewasa, Lansia.

**Alasan desain:**
- **Akumulasi harian, bukan overwrite:** Skor fase adalah penjumlahan delta harian sehingga konsistensi perilaku sepanjang fase lebih berpengaruh daripada satu hari ekstrem. Ini selaras dengan konsep gaya hidup kumulatif dalam promosi kesehatan.
- **Clamp 0–100:** Mencegah angka liar dari bug atau eksploit; penguji dan pemain dapat menginterpretasi skor seperti nilai rapor kesehatan.
- **Rata-rata tiga fase dengan default 50:** Simulasi berlangsung 12 hari dalam tiga fase (~4 hari/fase). Fase yang belum dimainkan tetap bernilai 50 (netral) agar ending tidak ditentukan hanya oleh fase terakhir—menghindari situasi pemain "benar-benar hancur" di Lansia meski Muda/Dewasa baik, tanpa bobot seimbang.
- **Threshold 40 untuk health guidance:** Angka ini dipakai konsisten di `HealthAlertPanelController` dan `LifestyleMortalityEvaluator` sebagai batas "perlu perhatian", sehingga UI, rumah sakit, dan risiko mortalitas berbicara dalam bahasa skor yang sama.

**Integrasi:** `EndingManager.TriggerEnding()` memakai rata-rata ini untuk Good/Neutral/Bad. `PhaseReviewBuilder` memakai snapshot per fase untuk narasi transisi usia.

### SyncDayAndTryAdvanceAgeStage()

```csharp
public bool SyncDayAndTryAdvanceAgeStage(int dayNumber, out AgeStage previousStage, out AgeStage newStage)
{
    progressionDayCount = Mathf.Max(1, dayNumber);
    previousStage = currentAgeStage;
    newStage = ResolveAgeStageForDay(progressionDayCount);

    if (newStage == previousStage)
        return false;

    currentAgeStage = newStage;

    if (healthScoreThisPhase > 70f)
        phaseCarryOverModifier = 1.1f;
    else if (healthScoreThisPhase < 40f)
        phaseCarryOverModifier = 0.9f;
    else
        phaseCarryOverModifier = 1.0f;

    maxEnergy = Mathf.Clamp(maxEnergy * phaseCarryOverModifier, 60f, 150f);

    int prevIndex = (int)previousStage;
    if (prevIndex >= 0 && prevIndex < committedPhaseScores.Length)
        committedPhaseScores[prevIndex] = healthScoreThisPhase;

    healthScoreThisPhase = 50f;
    SnapshotPhaseStartCounters();
    gymSkipStreak = 0;
    workSkipStreak = 0;
    ApplyPhaseModifiers();
    ApplyPhaseWeightShift(previousStage, newStage, healthScoreThisPhase);
    OnAgeStageChanged?.Invoke(previousStage, newStage, progressionDayCount);
    return true;
}

private static AgeStage ResolveAgeStageForDay(int dayNumber)
{
    int safeDay = Mathf.Max(1, dayNumber);
    if (safeDay >= 10) return AgeStage.Senior;
    if (safeDay >= 5)  return AgeStage.Adult;
    return AgeStage.Youth;
}
```

----

**Alur kerja:** Dipanggil dari `SleepBedInteractable` setelah `TimeManager.AdvanceToNextDayFromSleep`. Hari simulasi dipetakan ke fase usia: hari 1–4 Muda, 5–9 Dewasa, ≥10 Lansia. Bila fase tidak berubah, method mengembalikan `false` dan tidak ada efek samping. Bila berubah: skor fase lama disimpan, carry-over modifier energi ditetapkan, skor fase direset ke 50, snapshot counter perilaku diambil, streak gym/kerja direset, modifier AKG/drain diterapkan, berat badan disesuaikan, dan event usia dipancarkan.

**Alasan desain:**
- **Pembagian 4+5+3 hari (total 12):** Selaras dengan `endingTriggerDay` = 12 dan memberi waktu belajar di tiap fase usia sebelum ending—cukup panjang untuk pola kebiasaan terbentuk, cukup pendek untuk satu sesi playthrough skripsi.
- **Carry-over energi (×1,1 / ×0,9):** Menghubungkan perilaku di satu fase dengan "warisan" fisik di fase berikutnya. Pemain yang menjaga kesehatan merasakan tubuh lebih kuat; sebaliknya, kelalaian meninggalkan beban—metafora aging tanpa simulasi medis penuh.
- **Reset skor ke 50, bukan 0:** Fase baru adalah lembar evaluasi baru, bukan hukuman total. Pemain tetap punya kesempatan memperbaiki di fase Dewasa/Lansia.
- **Reset streak saat transisi:** Streak gym/kerja mengukur kebiasaan dalam satu fase; memindahkan streak antar fase akan menghukum pemain yang baru masuk fase Lansia dengan data lama yang tidak relevan.
- **Perubahan berat via `ApplyPhaseWeightShift`:** Memberi konsekuensi visual (model tubuh via BMI/`CharacterModelSwapper`) sehingga skor abstrak terasa di dunia game.

**Integrasi:** `OnAgeStageChanged` → `HUDManager`, panel aging di `SleepBedInteractable`, `CharacterModelSwapper.EvaluateAndSwap()`.

### FaintAndRespawn()

```csharp
void CheckFaintCondition()
{
    if (currentEnergy <= 0)
    {
        faintTimer += Time.deltaTime;
        if (faintTimer >= faintDuration && !faintRespawnHandled)
            FaintAndRespawn();
    }
    else
    {
        faintTimer = 0f;
    }
}

public void FaintAndRespawn()
{
    if (faintRespawnHandled || vehicleKnockdownInProgress)
        return;

    faintRespawnHandled = true;
    faintPresentationRoutine = StartCoroutine(FaintAndRespawnRoutine());
}

private IEnumerator FaintAndRespawnRoutine()
{
    yield return FadeToBlackWithTimeout(faintFadeOutDuration);

    currentEnergy = maxEnergy;
    currentEnergyState = EnergyState.Fainted;
    OnEnergyStateChanged?.Invoke(currentEnergyState);

    TeleportPlayerToRespawn();
    OnPlayerFainted?.Invoke();

    // Tampilkan FaintNotificationController, tunggu konfirmasi pemain
    yield return FadeFromBlackWithTimeout(faintFadeInDuration);
}
```

----

**Alur kerja:** `CheckFaintCondition` menumpuk timer selama energi = 0. Setelah 3 detik (`faintDuration`), `FaintAndRespawn` menjalankan coroutine presentasi: fade hitam → energi dipulihkan penuh tetapi state = Fainted (input terkunci) → teleport ke spawn → event `OnPlayerFainted` → panel edukatif → fade masuk → pemain konfirmasi → `ResetFaintState` → `EnergySystem` melepas kunci.

**Alasan desain:**
- **Timer 3 detik, bukan instant:** Memberi jeda agar pemain menyadari konsekuensi sebelum transisi visual; menghindari pingsan "tanpa sadar" saat drain frame terakhir.
- **Energi dipulihkan penuh tetapi tetap Fainted:** Pingsan dalam simulasi ini bukan game over, melainkan *consequence with recovery*—mirip istirahat paksa. Memulihkan energi mencegah loop pingsan tanpa henti di spawn; mengunci input memastikan pemain membaca notifikasi edukatif terlebih dahulu.
- **Teleport ke respawn:** Mencegah pemain terjebak di geometri atau jalan raya setelah kolaps; spawn terpusat memudahkan level design.
- **Memisahkan presentasi (`PlayerStats`) dari unlock input (`EnergySystem`):** `PlayerStats` tidak tahu detail kontrol pemain; `EnergySystem` yang memegang kunci input sesuai Single Responsibility—memudahkan debug saat modal atau dialog ikut mengunci gerak.

**Integrasi:** `PlayerActionTracker` mencatat pingsan (memengaruhi `LifestyleMortalityEvaluator` dan branch outcome). `HUDManager.HandlePlayerFainted` dapat menampilkan feedback visual. `FaintNotificationController` wajib ada; bila tidak, `ResetFaintState` dipanggil otomatis agar soft-lock tidak terjadi.

---

## TimeManager

**File:** `Assets/_Project/Scripts/Core/TimeManager.cs`

`TimeManager` mengatur jam simulasi satu hari dalam durasi real-time tetap (default 270 detik ≈ 4 menit 50 detik), memetakannya ke rentang 06.00–24.00, membagi hari menjadi empat periode, dan mengelola pergantian hari setelah tidur.

**Alasan desain waktu terkompresi:** Simulasi gaya hidup 12 hari tidak realistis jika satu hari = 24 jam real-time. Kompresi 18 jam simulasi ke ~4 menit memungkinkan playthrough lengkap dalam satu sesi uji skripsi, sambil tetap mempertahankan ritme pagi–siang–sore–malam yang dibutuhkan untuk keputusan makan, kerja, gym, dan tidur.

### CurrentHour

```csharp
public float TimePercent => currentGameTime / totalGameDuration;
public float CurrentHour => Mathf.Clamp(6f + (TimePercent * 18f), 0f, 24f);
```

----

**Alur kerja:** `TimePercent` menghitung posisi pemain dalam satu siklus hari (0 = baru mulai, 1 = hari habis). `CurrentHour` memetakan linear ke jam 06.00–24.00.

**Alasan desain:**
- **Rentang 06.00–24.00 (18 jam), bukan 00.00–24.00:** Jam tidur dan bangun adalah fokus edukatif; fase "subuh" jarang dipakai gameplay. Memulai di 06.00 selaras dengan bangun normal setelah tidur.
- **Mapping linear:** Sederhana, dapat diprediksi penguji, dan mudah di-inverse lewat `SetTimeByHour`—penting saat tidur atau kerja memajukan jam ke titik tertentu.
- **Clamp 0–24:** Mencegah nilai jam invalid dari bug floating-point atau time-skip.

**Integrasi:** `FacilityHours`, `FoodData.IsAvailableAt`, pengingat tidur (≥22.00), HUD `GetFormattedTimeRemaining`, serta `WorkSessionManager.BuildSession` (jam mulai kerja).

### Update()

```csharp
void Update()
{
    if (!isRunning) return;

    currentGameTime += Time.deltaTime;
    CheckPeriodChange();
    HandleSleepReminder();

    if (currentGameTime >= totalGameDuration)
    {
        currentGameTime = totalGameDuration;
        isRunning = false;
        OnGameTimeUp?.Invoke();
    }
}
```

----

**Alur kerja:** Bila `isRunning`, waktu bertambah tiap frame. Periode dicek; pengingat tidur dievaluasi; bila cap tercapai, jam berhenti dan event `OnGameTimeUp` dipancarkan.

**Alasan desain:**
- **`isRunning` sebagai gate:** Dialog (`NpcDialogueMenuController`) dan beberapa modal memanggil `PauseTime()` agar pemain punya waktu membaca tanpa kehilangan hari—penting untuk konten edukatif NPC dan dokter.
- **Empat periode sama panjang (25%):** Keseimbangan sederhana; pemain mendapat slot waktu setara untuk makan siang, kerja pagi, gym sore, dan malam. Alternatif bobot tidak sama (misalnya malam lebih pendek) dihindari agar tidak membingungkan dalam waktu terkompresi.
- **`OnGameTimeUp` tanpa memaksa tidur:** Jika pemain membiarkan hari habis, sistem mencatat branch outcome via `SessionFlowController` tetapi tidak langsung game over—menghindari hard fail yang memutus eksplorasi; tidur tetap mekanisme utama penutup hari.

**Integrasi:** `HUDManager` (subscribe period/day), `PlayerActionTracker` (periode untuk statistik), `SessionFlowController` (waktu habis).

### AdvanceToNextDayFromSleep()

```csharp
public void AdvanceToNextDayFromSleep()
{
    currentDayNumber = Mathf.Max(1, currentDayNumber + 1);
    currentDayOfWeekIndex = (currentDayOfWeekIndex + 1) % DayNamesIndonesia.Length;

    currentGameTime = 0f;
    isRunning = true;

    TimePeriod previousPeriod = currentPeriod;
    currentPeriod = TimePeriod.Morning;
    if (previousPeriod != currentPeriod)
        OnPeriodChanged?.Invoke(currentPeriod);

    ResetSleepReminderState();
    OnDayChanged?.Invoke(currentDayNumber, GetDayNameIndonesia());
}
```

----

**Alur kerja:** Hanya dipanggil dari `SleepBedInteractable`—bukan dari UI atau cheat—agar pergantian hari selalu disertai evaluasi kesehatan malam. Hari dan nama hari maju; waktu di-reset ke awal siklus; periode kembali Pagi; event `OnDayChanged` dipancarkan.

**Alasan desain:**
- **Pemisahan "advance day" dari "set hour":** Tidur selalu reset siklus, lalu `SetTimeByHour(wakeHour)` menetapkan bangun 07/08/09. Pola dua langkah memungkinkan tidur terganggu/terlambat tanpa mengubah logika increment hari.
- **Nama hari berputar (Senin–Minggu):** Memberi konteks naratif di UI bangun tidur tanpa mempengaruhi mekanik—kosmetik edukatif.
- **Hanya tidur yang boleh ganti hari:** Mencegah exploit memajukan hari tanpa konsekuensi evaluasi (makan, gym, kerja, mortalitas).

**Integrasi:** `PlayerStats.SyncDayAndTryAdvanceAgeStage`, reset `WorkSessionManager`/`GymProgressionSystem`, `HealthAlertPanelController.HandleDayChanged`, potensi spawn event harian.

---

## DailyHealthEvaluator

**File:** `Assets/_Project/Scripts/Health/Dailyhealthevaluator.cs`

`DailyHealthEvaluator` adalah kelas statis tanpa GameObject yang mengevaluasi skor kesehatan harian saat pemain tidur. Ia menerima snapshot boolean perilaku hari itu agar data tetap valid meskipun counter harian sudah di-reset.

**Alasan kelas statis (bukan MonoBehaviour):** Evaluasi adalah fungsi murni tanpa state scene—input masuk, angka keluar. Memisahkannya dari `PlayerStats` menjaga `PlayerStats` sebagai penyimpan data dan `DailyHealthEvaluator` sebagai aturan bisnis yang bisa diuji/dijelaskan di skripsi tanpa ketergantungan Unity lifecycle.

### Evaluate()

```csharp
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

    result.totalDelta = result.dietScore + result.sleepScore
                      + result.gymScore + result.workScore;

    result.totalDelta = Mathf.Clamp(result.totalDelta, -8f, 12f);
    BuildSummary(result);
    return result;
}
```

----

**Alur kerja:** Empat evaluator domain dipanggil berurutan; skor dijumlahkan; hasil di-clamp; catatan naratif (`overallNote`) dibuat untuk UI bangun tidur.

**Alasan desain:**
- **Empat domain paralel (diet, tidur, gym, kerja):** Mencerminkan pilar gaya hidup dalam promosi kesehatan Kemenkes—pemain melihat kontribusi masing-masing aspek, bukan satu angka misterius.
- **Snapshot parameter (`workedToday`, `lastWorkSession`, dll.):** `NotifyDayResetFromSleep` bisa mengubah `HasWorkedToday` sebelum evaluasi selesai. Snapshot diambil di awal `SleepRoutine` agar penilaian merepresentasikan hari yang benar-benar baru saja dimainkan—menghindari bug penilaian dan memberi alasan teknis yang kuat di skripsi.
- **Clamp [−8, +12]:** Hari terburuk tidak boleh menggerus lebih dari ~8 poin fase; hari terbaik tidak boleh menaikkan lebih dari 12. Tanpa clamp, kombinasi ekstrem (misalnya junk food + skip gym streak + skip work streak) bisa terasa tidak adil dalam simulasi 4 menit/hari.
- **`BuildSummary` dengan ambang teks:** Memberi umpan balik manusiawi di layar bangun—menghubungkan angka dengan pesan edukatif.

**Integrasi:** `SleepBedInteractable.ApplyRecovery` → `RegisterHealthScore` + `RegisterDailyHealthSnapshot` + pesan wake UI.

### EvaluateDiet()

```csharp
float healthyRatio = (float)healthy / total;

if (healthyRatio >= 0.80f)
    score = DietMaxBonus;          // +4.0
else if (healthyRatio >= 0.50f)
    score = Mathf.Lerp(2.0f, DietMaxBonus, (healthyRatio - 0.5f) / 0.3f);
else if (healthyRatio > 0f)
    score = Mathf.Lerp(0f, 2.0f, healthyRatio / 0.5f);
else
    score = DietJunkPenalty;       // -3.0

float calorieRatio = stats.TotalCalories / stats.DailyCalorieTarget;
if (calorieRatio < 0.50f) score += DietCalorieLow;   // -1.0
if (calorieRatio > 1.30f) score += DietCalorieHigh;  // -1.0
if (stats.DailyProtein < 40f) score -= 1.0f;
if (stats.DailyFat > 65f)       score -= 1.0f;
```

----

**Alur kerja:** Hitung rasio makanan sehat dari counter `PlayerActionTracker`. Beri skor tiered atau penalti junk. Bandingkan kalori harian dengan `DailyCalorieTarget` (AKG per fase). Periksa protein dan lemak harian.

**Alasan desain:**
- **Rasio 80%/50% sebagai ambang:** Mengadaptasi ide "pola makan seimbang" tanpa mengharuskan simulasi menghitung setiap mikronutrien—cukup untuk edukasi SMA/mahasiswa awam.
- **Threshold kalori 50% dan 130% AKG:** Mengacu praktik gizi umum (asupan terlalu rendah = defisit; >130% = kelebihan berulang). Target AKG tidak fixed 2000 kkal melainkan dari `PlayerStats` per gender/fase—menghindari bias nutrisi satu ukuran untuk semua.
- **Protein <40 g dan lemak >65 g:** Ambang disederhanakan dari rekomendasi harian umum; memberi sinyal "kurang protein" / "lemak berlebih" tanpa kalkulator gizi penuh.
- **Penalti tidak makan (−1,5):** Melewatkan makan sama sekali adalah perilaku berisiko nyata; penalti eksplisit mendorong pemain memakai sistem makanan simulasi.

**Integrasi:** Counter dari `FoodPickupInteractable`, stash, dan NPC restoran; target kalori dari `ApplyPhaseModifiers`.

### EvaluateGym() dan EvaluateWork()

```csharp
// Gym — bila berlatih:
switch (session.result)
{
    case GymSessionResult.Excellent: score = GymExcellent; break;  // +3.0
    case GymSessionResult.Solid:     score = GymSolid;     break;  // +2.0
    case GymSessionResult.Strained:  score = GymStrained;  break;  // +1.0
}
// Bila tidak gym dan streak >= 3:
score = GymStreakPenalty * (streak - GymStreakThreshold + 1);  // -1.5 per hari

// Work — bila bekerja:
case WorkResult.Full:    score = hadBonus ? WorkFullBonusPay : WorkFullBonus;  // +2.0
case WorkResult.Partial: score = WorkPartial;   // +0.5
case WorkResult.Failed:  score = WorkFailed;    // -1.5
// Bila tidak kerja dan streak >= 2:
score = WorkStreakPenalty * (streak - WorkStreakThreshold + 1);  // -2.0 per hari
```

----

**Alur kerja:** Jika berlatih/kerja, skor berdasarkan kualitas sesi (`GymSessionResult` / `WorkResult`). Jika tidak, streak counter naik; setelah ambang (gym ≥3 hari, kerja ≥2 hari), penalti kumulatif per hari.

**Alasan desain:**
- **Bobot gym sedikit lebih tinggi pada hari latihan (+3 Excellent):** Aktivitas fisik punya dampak langsung pada skor kesehatan fase—selaras literatur tentang manfaat olahraga teratur.
- **Streak gym threshold 3 vs kerja 2:** Melewatkan kerja lebih cepat memicu penalti karena konsekuensi ekonomi dan struktur hari (jam kerja terbatas 07–15); melewatkan gym tiga hari mensimulasikan kebiasaan sedentari yang berkembang pelan.
- **Penalti kerja (−2/hari) > gym (−1,5/hari) setelah threshold:** Memperkuat pesan bahwa absen kerja berulang punya tekanan sosial-ekonomi lebih terasa dalam narasi simulasi.
- **Reset streak saat aktivitas dilakukan:** Memberi jalan keluar—pemain yang memperbaiki kebiasaan tidak terhukum selamanya.

**Integrasi:** `GymProgressionSystem.LastSession`, `WorkSessionManager.LastSession`, streak di `PlayerStats`.

---

## LifestyleMortalityEvaluator

**File:** `Assets/_Project/Scripts/Health/LifestyleMortalityEvaluator.cs`

Evaluator statis ini memperkirakan risiko kematian dini probabilistik di akhir hari tidur. Mekanisme bersifat edukatif: pemain dilindungi di hari awal dan pada hari ending resmi.

**Alasan mekanisme probabilistik (bukan deterministik):** Kematian dini dalam kehidupan nyata dipengaruhi banyak faktor dan tidak bisa diprediksi 100% dari satu skor. Roll acak dengan peluang rendah memberi ketegangan naratif tanpa menghukum pemain yang skornya "cukup baik"—selaras tujuan edukasi, bukan permainan keras.

### Assess()

```csharp
public static MortalityAssessment Assess(
    int currentDayNumber, int endingDay,
    PlayerStats stats, PlayerActionTracker tracker)
{
    var result = new MortalityAssessment();

    if (stats == null || currentDayNumber <= YouthProtectionUntilDay
        || currentDayNumber >= endingDay)
        return result;

    float phaseScore = stats.HealthScoreThisPhase;
    result.IsWarningZone = phaseScore < 45f;

    if (phaseScore >= 35f && !HasChronicPoorPattern(stats))
        return result;

    float chance = ComputeBaseChance(phaseScore, stats, tracker);
    if (stats.CurrentAgeStage == PlayerStats.AgeStage.Senior)
        chance *= 1.65f;

    chance = Mathf.Clamp(chance, 0f, MaximumRiskChance);

    if (stats.VisitedHospitalToday && chance > 0f)
        chance = Mathf.Clamp(Mathf.Max(MinimumRiskChance, chance * HospitalChanceMultiplier),
                             MinimumRiskChance, MaximumRiskChance);

    result.RollChance = chance;
    result.IsRiskZone = chance > 0f;
    result.PrimaryCauseLabel = ResolvePrimaryCause(stats);
    return result;
}
```

----

**Alur kerja:** Cek perlindungan hari muda dan hari ending → baca skor fase → tentukan zona peringatan → jika skor cukup baik dan tidak ada pola kronis, keluar tanpa risiko → hitung peluang dasar → modifikasi fase Lansia dan kunjungan RS → kembalikan `MortalityAssessment`.

**Alasan desain:**
- **`YouthProtectionUntilDay` (hari 1–4):** Pemain masih belajar kontrol dan sistem; risiko kematian di awal akan terasa arbitrer dan membuat tutorial tidak selesai.
- **Skip evaluasi pada `currentDayNumber >= endingDay`:** Hari 12 mengarah ke ending resmi via `EndingManager`, bukan kematian mendadak—menjaga closure naratif yang direncanakan.
- **Zona peringatan (skor <45) tanpa roll:** Memberi sinyal "bahaya" di UI bangun tidur sebelum roll aktif (skor <35 atau pola kronis)—pemain punya kesempatan koreksi perilaku.
- **Ambang 35 untuk roll:** Skor di atas 35 dianggap "masih selamat" kecuali pola buruk kronis; memisahkan hari buruk sekali dari kebiasaan buruk berulang.
- **Pengali Lansia 1,65:** Usia lanjut secara epidemiologis punya risiko komplikasi lebih tinggi pada gaya hidup buruk yang sama—disesuaikan tanpa membuat fase Lansia mustahil dimainkan.
- **`HospitalChanceMultiplier` (×0,45, min 1%):** Kunjungan RS mensimulasikan intervensi kesehatan dini; mengurangi peluang roll memberi insentif edukatif untuk memakai fasilitas rumah sakit.

**Integrasi:** Dipanggil dari `SleepBedInteractable` setelah evaluasi harian; hasil ke UI peringatan dan `EndingManager.TriggerPrematureDeath` bila roll gagal.

### HasChronicPoorPattern()

```csharp
private static bool HasChronicPoorPattern(PlayerStats stats)
{
    int days = Mathf.Max(1, stats.TotalDaysEvaluated);
    float poorDietRatio = stats.PoorDietDays / (float)days;
    float skipGymRatio = stats.SkippedGymDays / (float)days;
    float disturbedSleepRatio = stats.DisturbedSleepDays / (float)days;
    return poorDietRatio >= 0.4f || skipGymRatio >= 0.45f || disturbedSleepRatio >= 0.35f;
}
```

----

**Alur kerja:** Bagi counter lifetime (`PoorDietDays`, `SkippedGymDays`, `DisturbedSleepDays`) dengan `TotalDaysEvaluated`; bandingkan dengan ambang rasio; return true jika salah satu melewati threshold.

**Alasan desain:**
- **Proporsi, bukan hitungan absolut:** Hari ke-5 dengan 2 hari diet buruk lebih bermakna daripada hari ke-50 dengan 2 hari buruk—rasio menormalisasi durasi permainan.
- **Ambang diet 40%, skip gym 45%, tidur 35%:** Tidur diberi ambang sedikit lebih rendah karena dampak metabolisme dan kognitif tidur buruk cepat terasa dalam simulasi energi; gym sedikit lebih toleran karena tidak semua hari punya akses waktu ke fasilitas.
- **Memaksa zona risiko meski skor 35–44:** Mencegah pemain "menyelamatkan" skor dengan satu atau dua hari baik sementara kebiasaan buruk dominan—lebih mencerminkan penyakit kronis dari pola jangka panjang.

**Integrasi:** Counter diisi saat `RegisterDailyHealthSnapshot` dan event tidur/makan; mempengaruhi `ComputeBaseChance` (+2% tambahan bila pola kronis).

### RollMortality()

```csharp
public static bool RollMortality(float chance)
{
    if (chance <= 0f)
        return false;

    return Random.value < chance;
}
```

----

**Alur kerja:** Jika `chance <= 0`, langsung aman. Jika tidak, bandingkan `Random.value` (uniform [0,1)) dengan `chance`.

**Alasan desain:**
- **Fungsi terpisah dari `Assess`:** Memisahkan perhitungan risiko dari keputusan acak memudahkan pengujian logika (mock chance) dan menjelaskan di skripsi bahwa "peluang" dan "hasil roll" adalah dua langkah berbeda.
- **Uniform random:** Implementasi sederhana Unity `Random.value`; cukup untuk simulasi edukatif tanpa distribusi statistik kompleks.
- **Contoh 8% chance:** Transparan bagi pembaca skripsi—pemain dengan perilaku buruk tetap punya 92% kemungkinan bangun besok, menghindari frustrasi total.

**Integrasi:** `SleepBedInteractable` memanggil setelah `Assess`; `true` → `EndingManager.TriggerPrematureDeath`.

---

## EndingManager

**File:** `Assets/_Project/Scripts/Core/EndingManager.cs`

`EndingManager` menangani presentasi akhir permainan: dialog dokter, panel naratif, rekap statistik, informasi risiko penyakit, dan credits.

**Alasan komponen terpusat untuk ending:** Semua alur akhir (normal, prematur, baik/netral/buruk) butuh UI, audio, dan time-scale yang sama. Satu manager mencegah duplikasi coroutine dan memastikan ending tidak terpicu dua kali (`endingTriggered`).

### TriggerEnding()

```csharp
public void TriggerEnding()
{
    if (isShowing || endingTriggered) return;

    float avg = PlayerStats.Instance.GetAveragePhaseScore();
    PlayerStats.Gender gender = PlayerStats.Instance.PlayerGender;

    float goodThreshold    = gender == PlayerStats.Gender.Female ? 65f : 70f;
    float neutralThreshold = gender == PlayerStats.Gender.Female ? 40f : 45f;

    pendingEndingType = avg >= goodThreshold  ? EndingType.Good
                      : avg >= neutralThreshold ? EndingType.Neutral
                      : EndingType.Bad;

    endingTriggered = true;
    // Buka dialog dokter Sri 4-node, lalu ShowEndingRoutine
}
```

----

**Alur kerja:** Guard double-trigger → hitung rata-rata skor tiga fase → bandingkan ambang per gender → set `EndingType` → mulai dialog dokter 4-node → lanjut `ShowEndingRoutine` (panel, rekap, credits).

**Alasan desain:**
- **Ambang Good/Neutral lebih rendah untuk perempuan (65/40 vs 70/45):** Mengakomodasi perbedaan kebutuhan energi dan target AKG di `PlayerStats`—penilaian ending selaras dengan parameter fase yang sudah gender-aware, bukan diskriminasi arbitrer.
- **Rata-rata tiga fase, bukan skor hari terakhir:** Gaya hidup dinilai sebagai perjalanan hidup penuh (remaja → dewasa → lansia), sesuai narasi simulasi kehidupan.
- **Dialog dokter sebelum panel:** Memberi konteks klinis dan transisi emosional; dokter Sri sebagai figur otoritas kesehatan memperkuat pesan edukatif.
- **Guard `isShowing || endingTriggered`:** Mencegah race condition jika pemain spam interaksi tidur atau event ganda terpicu.

**Integrasi:** Dipicu `SleepBedInteractable` hari 12; membaca `PlayerStats.GetAveragePhaseScore`; menampilkan `BuildDiseaseRisks` di panel.

### BuildDiseaseRisks()

```csharp
// Contoh: Diabetes tipe 2
Score = (poorDietRatio * 0.55f) + (highCalorieRatio * 0.35f) + (skippedGymRatio * 0.10f)

// Hipertensi
Score = (overworkedRatio * 0.45f) + (disturbedSleepRatio * 0.35f) + (lowEnergySleepRatio * 0.20f)

// Kolesterol tinggi
Score = (poorDietRatio * 0.60f) + (highCalorieRatio * 0.40f)

// Obesitas
Score = (highCalorieRatio * 0.50f) + (skippedGymRatio * 0.30f) + (poorDietRatio * 0.20f)

// Kelelahan kronis
Score = (disturbedSleepRatio * 0.45f) + (lowEnergySleepRatio * 0.35f) + (overworkedRatio * 0.20f)

// Gangguan tidur
Score = (disturbedSleepRatio * 0.70f) + (lowEnergySleepRatio * 0.30f)
```

----

**Alur kerja:** Untuk setiap penyakit, hitung skor tertimbang dari rasio perilaku lifetime → urutkan menurun → tampilkan yang skor ≥0,20 → label "tinggi" jika ≥0,35 → lampirkan alasan teks jika komponen >35%.

**Alasan desain:**
- **Enam penyakit umum NCD:** Diabetes, hipertensi, kolesterol, obesitas, kelelahan kronis, gangguan tidur—selaras fokus promosi PHBS dan gaya hidup pada populasi urban Indonesia.
- **Bobot berbeda per penyakit:** Hipertensi lebih sensitif ke overwork dan tidur (0,45 + 0,35); diabetes lebih ke diet (0,55)—mencerminkan literatur epidemiologi sederhana tanpa klaim medis presisi.
- **Ambang tampil 0,20 dan "tinggi" 0,35:** Menghindari daftar panjang risiko noise; hanya pola yang konsisten cukup kuat yang ditampilkan ke pemain.
- **Rasio lifetime, bukan hari terakhir:** Konsisten dengan pesan "kebiasaan jangka panjang" di seluruh sistem evaluasi.

**Integrasi:** Data dari counter `PlayerStats` (`PoorDietDays`, `OverworkedDays`, dll.) yang terisi via `RegisterDailyHealthSnapshot`.

### TriggerPrematureDeath()

```csharp
public void TriggerPrematureDeath(
    LifestyleMortalityEvaluator.MortalityAssessment assessment, int dayNumber)
{
    pendingEndingType = EndingType.PrematureDeath;
    pendingMortalityCause = assessment.PrimaryCauseLabel;
    pendingDeathDay = dayNumber;
    endingTriggered = true;
    StartCoroutine(ShowEndingRoutine(EndingType.PrematureDeath, pendingGender));
}
```

----

**Alur kerja:** Set `EndingType.PrematureDeath` dan label penyebab dari assessment → tandai `endingTriggered` → jalankan `ShowEndingRoutine` tanpa dialog dokter pra-ending.

**Alasan desain:**
- **Tanpa dialog dokter 4-node:** Kematian dini bersifat mendadak secara naratif; menambah dialog panjang akan mengurangi dampak emosional dan membingungkan pemain yang sudah melihat peringatan risiko.
- **Menyimpan `PrimaryCauseLabel`:** Memberi penutup kausal ("pola makan tidak seimbang") agar pemain memahami hubungan perilaku–konsekuensi, bukan sekadar game over acak.
- **Tetap memutar rekap dan credits:** Menjaga konsistensi UX ending dan menegaskan bahwa ini simulasi edukatif, bukan kegagalan teknis.

**Integrasi:** Dipanggil dari `SleepBedInteractable.HandlePrematureDeathDuringSleep` setelah `RollMortality` true.

---

## EnergySystem

**File:** `Assets/_Project/Scripts/Player/EnergySystem.cs`

`EnergySystem` melengkapi `PlayerStats` dengan penanganan respawn setelah pingsan di sisi `PlayerController`.

**Alasan pemisahan dari `PlayerStats`:** `PlayerStats` menyimpan angka energi; `EnergySystem` menangani perilaku scene (kunci input, coroutine respawn, teleport). Pemisahan ini mengikuti prinsip single responsibility—stat tidak perlu tahu tentang `PlayerController` atau UI notifikasi pingsan.

```csharp
public void CompleteFaintRecovery()
{
    hasFainted = false;
    ReleaseMovementLock();
}

void HandleFaint()
{
    hasFainted = true;
    respawnRoutine = StartCoroutine(RespawnAfterDelay());
}
```

----

**Alur kerja:** `HandleFaint` set flag dan mulai coroutine respawn dengan delay → pemain melihat notifikasi → setelah `ResetFaintState`, `CompleteFaintRecovery` melepas kunci gerak.

**Alasan desain:**
- **Kunci input saat pingsan:** Mencegah pemain bergerak saat energi 0—menghindari exploit (lari sambil "tidak sadar") dan memberi jeda untuk membaca pesan edukatif tentang kelelahan.
- **Respawn dengan delay, bukan instant:** Memberi waktu UI `FaintNotification` tampil; transisi terasa lebih natural daripada teleport langsung.
- **`ReleaseMovementLock` terpisah dari reset energi:** Energi dipulihkan di `PlayerStats.FaintAndRespawn`; unlock gerak hanya setelah pemain mengakui notifikasi—memastikan pemain tidak langsung mengulangi pola yang sama tanpa membaca feedback.

**Integrasi:** `PlayerStats` event state Fainted → `EnergySystem`; unlock via `PlayerController.ForceUnlockInput`.

---

## FacilityHours

**File:** `Assets/_Project/Scripts/Core/FacilityHours.cs`

```csharp
public static bool IsWorkOpen(float hour)
{
    return hour >= WorkOpenHour && hour < WorkCloseHour;  // 07.00 – 15.00
}

public static bool IsGymOpen(float hour)
{
    return hour >= GymOpenHour && hour < GymCloseHour;   // 06.00 – 22.00
}
```

----

**Alur kerja:** Bandingkan `hour` dari `TimeManager` dengan konstanta buka/tutup kerja (07–15) atau gym (06–22); return boolean.

**Alasan desain:**
- **Kelas statis terpusat:** Jam operasional hanya didefinisikan sekali. Tanpa ini, setiap pintu dan manager bisa punya angka berbeda (bug "kantor tutup di script A tapi buka di script B").
- **Kerja 07–15:** Mensimulasikan jam kantor reguler; membatasi pemain merencanakan hari (makan pagi → kerja → gym/sore) dalam kerangka waktu realistis.
- **Gym 06–22 lebih panjang:** Fasilitas olahraga umumnya lebih fleksibel; pemain yang pulang kerja masih bisa latihan—mendorong aktivitas fisik tanpa bentrok shift kerja.
- **Half-open interval (`>= open && < close`):** Konvensi umum agar jam tutup tepat (15.00) tidak dihitung "masih buka".

**Integrasi:** `WorkSessionManager.BuildSession`, `WorkDoorInteractable`, `GymDoorInteractable`, validasi interaksi NPC.

---

# BATCH 2 — Sleep & Session

---

## SleepBedInteractable

**File:** `Assets/_Project/Scripts/Interaction/SleepBedInteractable.cs`

`SleepBedInteractable` adalah orkestrator siklus tidur—satu-satunya titik yang menutup hari simulasi, mengevaluasi kesehatan, menggulung risiko mortalitas, dan memicu ending.

**Alasan satu titik orkestrasi tidur:** Semua reset harian, evaluasi, dan transisi fase usia harus terjadi dalam urutan tetap. Memusatkan di satu coroutine mencegah race condition (misalnya hari naik sebelum skor diregistrasi) dan memudahkan penjelasan alur di skripsi sebagai "event sink" siklus 24 jam simulasi.

### SleepRoutine() — alur utama

```csharp
private IEnumerator SleepRoutine(GameObject interactor, bool forcedSleep)
{
    // Snapshot sebelum fade
    bool workedYesterday  = workSessionManager.HasWorkedToday;
    bool trainedYesterday = GymProgressionSystem.DidTrainToday(gymProgressionSystem, playerStats);
    float energyBeforeSleep = playerStats.EnergyPercent;
    bool disturbedSleep = RollSleepDisturbance(...);
    bool lateWakePenaltyTriggered = Random.value < CalculateLateWakeChance(...);

    // Fade hitam + animasi jam 21:00 → wakeHour
    ApplyRecovery(...);  // evaluasi harian + RegisterHealthScore

    mortalityAssessment = LifestyleMortalityEvaluator.Assess(...);
    if (LifestyleMortalityEvaluator.RollMortality(mortalityAssessment.RollChance))
    {
        yield return HandlePrematureDeathDuringSleep(...);
        yield break;
    }

    timeManager.AdvanceToNextDayFromSleep();
    playerStats.SyncDayAndTryAdvanceAgeStage(timeManager.CurrentDayNumber, ...);
    workSessionManager.NotifyDayResetFromSleep();
    gymProgressionSystem.NotifyDayResetFromSleep();

    if (isFinalDaySleep)
        EndingManager.Instance.TriggerEnding();
}
```

----

**Alur kerja:** Snapshot perilaku → fade + animasi jam malam → `ApplyRecovery` (evaluasi) → `Assess` + `RollMortality` → jika aman: advance day, reset manager, cek ending hari 12.

**Alasan desain:**
- **Snapshot sebelum fade:** `NotifyDayResetFromSleep` mengubah flag harian; snapshot menjamin evaluator melihat data hari yang benar—alasan teknis penting untuk defend di sidang.
- **Evaluasi sebelum roll mortalitas:** Skor malam itu sudah terdaftar; roll memakai skor fase terkini termasuk delta hari ini.
- **Roll sebelum `AdvanceToNextDay`:** Kematian dini terjadi "malam itu" pada hari N, bukan hari N+1—naratif dan counter hari konsisten.
- **Ending hari 12 tanpa wake biasa:** Menghindari pemain bermain "hari ke-13" setelah ending; transisi langsung ke `EndingManager` memberi penutup simulasi kehidupan 12 hari × 3 fase.

**Integrasi:** `TimeManager`, `DailyHealthEvaluator`, `LifestyleMortalityEvaluator`, `EndingManager`, `WorkSessionManager`, `GymProgressionSystem`, `FadeManager`, `ClockAnimationUI`.

### ApplyRecovery()

```csharp
private void ApplyRecovery(..., out DailyHealthResult evalResult)
{
    float targetNormalized = disturbedSleep
        ? disturbedRecoveryNormalized : fullRecoveryNormalized;

    float targetEnergy = stats.MaxEnergy * targetNormalized;
    stats.AddFood(targetEnergy - stats.CurrentEnergy, 0, 0, 0, 0);

    evalResult = DailyHealthEvaluator.Evaluate(
        disturbedSleep, energyBeforeSleep, workedYesterday,
        lastWorkSession, lastWorkHadBonus, trainedYesterday,
        lastGymSession, PlayerActionTracker.Instance, stats);

    stats.RegisterHealthScore(evalResult.totalDelta);
    stats.RegisterDailyHealthSnapshot(...);

    PlayerActionTracker.Instance.ResetDailyFoodCounts();
    stats.ResetDailyCalories();
}
```

----

**Alur kerja:** Hitung target energi (100% atau 65% jika terganggu) → `AddFood` sebagai delta energi → panggil `DailyHealthEvaluator` → registrasi skor dan snapshot → reset counter makan harian.

**Alasan desain:**
- **Pemulihan via `AddFood` (bukan method energi terpisah):** Reuse pipeline nutrisi yang sudah memicu event HUD; menghindari duplikasi logika clamp dan notifikasi.
- **65% vs 100% recovery:** Tidur terganggu punya konsekuensi gameplay nyata (energi tidak penuh besok)—mendorong pemain memperhatikan faktor gangguan tidur, bukan hanya "tekan tidur".
- **Reset makan setelah evaluasi:** Counter diet harian harus tetap utuh saat `EvaluateDiet` berjalan; reset setelahnya menyiapkan hari baru tanpa menghapus data yang baru dinilai.
- **Lifetime counter tidak direset:** `PoorDietDays`, `FaintEvents`, dll. dipakai ending dan mortalitas—harus akumulatif sepanjang simulasi.

**Integrasi:** `DailyHealthEvaluator`, `PlayerStats.RegisterHealthScore`, `PlayerActionTracker.ResetDailyFoodCounts`.

### CalculateSleepDisturbanceChance()

```csharp
private float CalculateSleepDisturbanceChance(
    bool workedYesterday, bool overworkedYesterday, bool poorDietYesterday)
{
    float chance = Mathf.Clamp01(baseDisturbChance);
    if (!workedYesterday)    chance += chanceIfNoWork;
    if (overworkedYesterday) chance += chanceIfOverwork;
    if (poorDietYesterday)   chance += chanceIfPoorDiet;
    return Mathf.Clamp01(chance * disturbanceMultiplier);
}
```

----

**Alur kerja:** Mulai dari chance dasar 5% → tambah modifier kondisional (tidak kerja, overwork, diet buruk) → kalikan `disturbanceMultiplier` 1,6 → clamp [0,1] → roll menentukan recovery dan jam bangun.

**Alasan desain:**
- **Faktor tidak kerja (+20%):** Hari tanpa aktivitas terstruktur sering berkorelasi dengan rutinitas tidur tidak teratur dalam literatur gaya hidup—disederhanakan menjadi satu modifier.
- **Overwork (+25%):** Menghubungkan sesi kerja berat (`WorkResult` dengan drain tinggi) dengan kualitas tidur—pesan edukatif tentang work-life balance.
- **Diet buruk (+15%):** Asupan tinggi gula/lemak mempengaruhi kualitas tidur; ambang "selisih junk vs sehat ≥2" memakai data tracker yang sudah ada.
- **Multiplier 1,6:** Meningkatkan relevansi gameplay tanpa membuat tidur terganggu hampir pasti—tetap ada variasi acak.

**Integrasi:** Input dari snapshot kerja dan `PlayerActionTracker`; output ke `ApplyRecovery` dan `EvaluateSleep` di evaluator.

---

## SessionFoodStash

**File:** `Assets/_Project/Scripts/Session/SessionFoodStash.cs`

```csharp
public bool ConsumeAt(int index)
{
    FoodData food = savedFoods[index];
    savedFoods.RemoveAt(index);
    OnStashChanged?.Invoke();

    PlayerStats.Instance.AddFood(
        food.energyRestored, food.calories, food.moodEffect,
        food.protein, food.fat, countsAsMeal: true);

    PlayerActionTracker.Instance.Track(
        food.isHealthy ? ActionType.HealthyFoodTaken : ActionType.UnhealthyFoodTaken,
        $"StashConsume:{food.foodName}");
    return true;
}
```

----

**Alur kerja:** Ambil `FoodData` dari slot → hapus dari list → invoke event UI → `AddFood` dengan nutrisi penuh → `Track` ke action tracker.

**Alasan desain:**
- **Persist antar hari (`DontDestroyOnLoad`):** Makanan dibeli/disimpan tidak hilang saat ganti scene—mensimulasikan kulkas/rumah tanpa inventory scene-per-scene.
- **`countsAsMeal: true`:** Konsumsi stash dianggap makan nyata; memicu aturan nutrisi dan menonaktifkan travel grace yang hanya untuk "snack ringan" pasca aktivitas.
- **Track sehat/tidak sehat:** Stash bukan loophole untuk menghindari penilaian diet; setiap konsumsi tetap masuk `DailyHealthEvaluator`.

**Integrasi:** UI stash, `PlayerStats.AddFood`, `PlayerActionTracker`, evaluasi diet malam.

---

## SessionFlowController

**File:** `Assets/_Project/Scripts/Session/SessionFlowController.cs`

```csharp
private void HandleGameTimeUp()
{
    if (sessionEnded) return;
    sessionEnded = true;

    BranchOutcome outcome = PlayerActionTracker.Instance.EvaluateBranchOutcome();
    Debug.Log($"[Session] Akhir sesi: {outcome}");
}
```

----

**Alur kerja:** Subscribe `OnGameTimeUp` → saat waktu habis tanpa tidur, set `sessionEnded` → evaluasi `BranchOutcome` dari tracker → log hasil.

**Alasan desain:**
- **Bukan trigger ending:** Membiarkan waktu habis tanpa tidur adalah pilihan pemain (misalnya eksplorasi); menghukum dengan ending langsung terlalu keras untuk simulasi edukatif awal.
- **`BranchOutcome` (Healthy/Mixed/Risky):** Klasifikasi kumulatif untuk analitik/debug dan potensi fitur future—menunjukkan desain extensible tanpa mengubah core loop saat ini.
- **Guard `sessionEnded`:** Mencegah event ganda jika jam melewati batas beberapa frame berturut-turut.

**Integrasi:** `TimeManager.OnGameTimeUp`, `PlayerActionTracker.EvaluateBranchOutcome`.

---

## SampleSceneBootstrap

**File:** `Assets/_Project/Scripts/Core/SampleSceneBootstrap.cs`

```csharp
private void EnsureCoreManagers()
{
    TimeManager.EnsureExists();
    EnsureComponent<PlayerActionTracker>(manager);
    EnsureComponent<WorkSessionManager>(manager);
    EnsureComponent<GymProgressionSystem>(manager);
    EnsureComponent<EndingManager>(manager);
    EnsureComponent<FadeManager>(manager);
    EnsureComponent<NpcDialogueMenuController>(manager);
    EnsureComponent<SessionFoodStash>(manager);
    // ... komponen lainnya
}
```

----

**Alur kerja:** Saat scene load, pastikan objek `GameManager` ada → attach semua singleton inti yang belum ada → setup EventSystem dan mobile UI.

**Alasan desain:**
- **`[RuntimeInitializeOnLoadMethod]`:** Proyek skripsi sering dibuka di scene berbeda; bootstrap otomatis mencegah "manager hilang" saat testing tanpa wiring manual Inspector setiap kali.
- **`EnsureExists` / `EnsureComponent`:** Idempotent—aman dipanggil berulang; tidak duplikasi objek jika sudah ada.
- **Satu parent `GameManager`:** Hierarki rapi dan mudah dijelaskan di dokumentasi sebagai root dependensi runtime.

**Integrasi:** Semua singleton Batch 1–5; dipanggil saat `SampleScene` (dan scene yang mengandung bootstrap) dimuat.

---

## SpawnPlayerManager

**File:** `Assets/_Project/Scripts/Manager/SpawnPlayerManager.cs`

`SpawnPlayerManager` mengatur posisi pemain setiap kali `SampleScene` dimuat—termasuk spawn awal, kembali dari kantor/gym, dan fallback jika titik spawn tidak ditemukan.

**Alasan singleton persisten:** `TargetSpawnID` harus survive saat `FadeManager` memuat ulang scene kota; sub-scene office/gym menulis ID spawn (`"officedoor"`, `"gymdoor"`) sebelum load.

### TargetSpawnID dan IsReturningFromSubSceneLoad()

```csharp
public static string TargetSpawnID { get; set; } = "";

public static bool IsReturningFromSubSceneLoad()
{
    if (string.IsNullOrEmpty(TargetSpawnID))
        return false;

    return !string.Equals(TargetSpawnID, DefaultSpawnId, StringComparison.OrdinalIgnoreCase);
}
```

----

**Alur kerja:** Script lain set `TargetSpawnID` sebelum load scene → saat `SampleScene` loaded, manager cek apakah ini return dari sub-scene (bukan spawn default).

**Alasan desain:**
- **Static string bridge antar scene:** Sub-scene tidak perlu referensi ke objek spawn di kota—cukup pass ID string via `SubSceneReturnHelper`.
- **Default `"spawnpoint"` vs ID khusus:** Membedakan first load / intro dari pulang kerja—`SampleSceneBootstrap` bisa skip intro jika `IsReturningFromSubSceneLoad()` true.
- **Clear setelah spawn:** `TargetSpawnID` dikosongkan post-teleport agar load berikutnya tidak salah posisi.

**Integrasi:** `SubSceneReturnHelper.ReturnToSampleScene`, `SampleSceneBootstrap`, event `OnSpawnComplete`.

### SpawnWhenPlayerReady() dan SpawnPlayer()

```csharp
public IEnumerator SpawnWhenPlayerReady()
{
    // Tunggu GameObject Player (tag atau PlayerController) max 8 detik
    SpawnPlayer();
}

private void SpawnPlayer()
{
    SpawnPointID targetSpawn = ResolveSpawnPointId(spawnPoints, TargetSpawnID);
    // Fallback: tag Respawn, "SpawnPoint (Main)", dll.
    player.transform.position = spawnTransform.position;
    player.transform.rotation = spawnTransform.rotation;
    if (spawnTransform.position.y < 1f)
        SnapToGround(player.transform, spawnTransform.position);

    TargetSpawnID = "";
    OnSpawnComplete?.Invoke();
}
```

----

**Alur kerja:** Coroutine tunggu player aktif → cari `SpawnPointID` by `TargetSpawnID` → teleport + optional snap ground → clear ID → invoke event.

**Alasan desain:**
- **Timeout 8 detik:** Player bisa inactive sementara saat bootstrap; polling aman daripada assume instant ready.
- **`FindObjectsInactive.Include`:** Spawn point atau player bisa nonaktif di hierarchy saat scene baru load—Unity default search akan miss.
- **`ResolveSpawnPointId` prioritas Respawn tag:** Jika ada duplikat ID, titik bertag `Respawn` diutamakan—lebih predictable untuk level design.
- **`SnapToGround` raycast dengan safety ±10 m:** Mencegah player jatuh ke void jika spawn Y salah, tanpa snap ke collider jauh di bawah tanah.

**Integrasi:** `SpawnPointID` di scene, `PlayerController`, `FadeManager` (setelah fade in kota).

---

## PhaseReviewBuilder

**File:** `Assets/_Project/Scripts/Health/PhaseReviewBuilder.cs`

```csharp
public static string Build(PhaseReviewInput input)
{
    // Gabungkan: ringkasan skor fase, highlight positif,
    // isu yang perlu diperbaiki, dan konsekuensi carry-over
}
```

----

**Alur kerja:** Terima `PhaseReviewInput` (skor fase, highlight, isu) → susun paragraf narasi → return string untuk typewriter UI.

**Alasan desain:**
- **Kelas statis pembuat teks:** Memisahkan logika copywriting dari UI `SleepBedInteractable`—memudahkan revisi narasi tanpa menyentuh coroutine tidur.
- **Review saat transisi usia:** Momen pedagogis alami untuk refleksi sebelum parameter fase baru (AKG, drain) berlaku.
- **Carry-over consequences:** Menjelaskan dampak fase sebelumnya ke fase berikutnya—menghubungkan keputusan pemain lintas periode.

**Integrasi:** `SleepBedInteractable.ShowAgingNotificationRoutine`, data dari `PlayerStats` saat `SyncDayAndTryAdvanceAgeStage`.

---

# BATCH 3 — Gym & Work

---

## WorkSessionManager

**File:** `Assets/_Project/Scripts/Session/WorkSessionManager.cs`

`WorkSessionManager` mengelola logika sesi kerja: validasi jam dan energi, pembayaran, drain energi, serta sinkronisasi jam setelah shift.

**Alasan manager terpisah dari `WorkSessionController`:** Manager hidup di scene kota (persisten); controller hidup di `OfficeScene` (alur presentasi). Pemisahan memungkinkan validasi "bisa kerja hari ini?" di pintu kantor tanpa memuat scene office.

### BuildSession()

```csharp
public WorkSessionData BuildSession(TimeManager.TimePeriod currentPeriod, float currentEnergyNormalized)
{
    if (!FacilityHours.IsWorkOpen(timeManager))
        return null;

    WorkSessionData data = new WorkSessionData
    {
        period = MapWorkPeriod(currentPeriod),
        energyAtStart = Mathf.Clamp01(currentEnergyNormalized),
        startHour = Mathf.CeilToInt(Mathf.Max(FacilityHours.WorkOpenHour, timeManager.CurrentHour)),
        endHour = Mathf.RoundToInt(FacilityHours.WorkCloseHour)
    };
    PendingSession = data;
    return data;
}
```

----

**Alur kerja:** Cek `FacilityHours.IsWorkOpen` → buat `WorkSessionData` dengan periode, energi awal, jam mulai (ceil jam sekarang), jam tutup 15.00 → simpan ke `PendingSession`.

**Alasan desain:**
- **Return null jika tutup:** Fail-fast di pintu—pemain mendapat feedback jelas tanpa load scene sia-sia.
- **Jam mulai `Ceil` jam saat ini:** Masuk kantor jam 09.30 dihitung mulai 10.00—mensimulasikan tidak bisa "mengulang" jam kerja penuh jika terlambat.
- **Periode Pagi/Siang/Sore → gaji berbeda:** Mendorong pemain datang lebih pagi (gaji 200 vs 150)—mencerminkan insentif produktivitas tanpa simulasi payroll kompleks.

**Integrasi:** `FacilityHours`, `TimeManager`, `WorkDoorInteractable`, `WorkSessionController`.

### ApplyResult()

```csharp
public void ApplyResult(WorkSessionData data)
{
    if (energy < MinEnergyToWork)  // < 10%
    {
        data.result = WorkResult.Failed;
        data.moneyEarned = 0;
    }
    else if (energy < _energyThresholdFail || data.performanceDropped)  // < 30%
    {
        data.result = WorkResult.Partial;
        data.moneyEarned = Mathf.RoundToInt(basePay * payFactor);
        data.energyConsumed = Mathf.Lerp(_energyDrainPartial, _energyDrainFull, completionRatio);
    }
    else
    {
        data.result = WorkResult.Full;
        data.moneyEarned = energy > _energyThresholdBonus
            ? Mathf.RoundToInt(basePay * _bonusMultiplier) : basePay;
        data.energyConsumed = _energyDrainFull;
    }

    stats.AddMoney(data.moneyEarned);
    stats.DrainEnergy(data.energyConsumed);
    HasWorkedToday = true;
}
```

----

**Alur kerja:** Bandingkan energi awal dengan ambang 10%/30%/60% → set `WorkResult` dan gaji → drain energi sesuai tier → `AddMoney` → set `HasWorkedToday`.

**Alasan desain:**
- **<10% = Failed:** Tidak mampu bekerja sama sekali—realistis dan mendorong manajemen energi sebelum berangkat.
- **10–30% = Partial:** Masih bisa hadir tetapi performa turun; gaji `completionRatio` menghargai usaha tanpa reward penuh.
- **>60% bonus 25%:** Insentif menjaga energi sebelum kerja—menghubungkan nutrisi/tidur dengan outcome ekonomi.
- **`HasWorkedToday` untuk evaluator:** Satu flag eksplisit untuk malam hari, lebih andal daripada infer dari uang.

**Integrasi:** `PlayerStats`, `DailyHealthEvaluator.EvaluateWork`, `ClockAnimationUI` (performa drop).

---

## WorkSessionController

**File:** `Assets/_Project/Scripts/Session/WorkSessionController.cs`

```csharp
private IEnumerator RunFlow()
{
    _activeSession = WorkSessionManager.Instance.PendingSession;
    yield return PlayDialogueAndWait(BuildPreWorkDialogueForEnergy(_activeSession.energyAtStart));

    _clockUI.PlayWorkAnimation(_activeSession, _ => animationCompleted = true);
    while (!animationCompleted) yield return null;

    WorkSessionManager.Instance.ApplyResult(_activeSession);
    WorkSessionManager.SyncGameClockAfterWork(_activeSession);

    yield return PlayDialogueAndWait(BuildPostWorkDialogueForResult());
    SubSceneReturnHelper.ReturnToSampleScene("officedoor", _mainSceneName, activateTravelGrace: true);
}
```

----

**Alur kerja:** Ambil `PendingSession` → dialog pra-kerja (bos) → animasi jam kerja → `ApplyResult` + sync jam → dialog pasca → return kota dengan travel grace.

**Alasan desain:**
- **Coroutine berurutan:** Urutan tetap (dialog → waktu → ekonomi → dialog) mencegah pemain bergerak saat shift dan memberi pacing naratif seperti visual novel ringan.
- **Dialog energi-awal:** Feedback kontekstual ("kamu kelihatan lelah") menghubungkan stat dengan cerita tanpa angka mentah di layar.
- **`activateTravelGrace: true`:** Setelah drain kerja, pemain butuh waktu sampai warung—grace mencegah frustrasi pingsan di jalan pulang (selaras desain `PlayerStats`).
- **Sub-scene terpisah:** Kantor sebagai scene sendiri memungkinkan layout 3D berbeda tanpa membebani scene kota utama.

**Integrasi:** `WorkSessionManager`, `ClockAnimationUI`, `OfficeBossDialogueController`, `SubSceneReturnHelper`, `FadeManager`.

---

## GymProgressionSystem

**File:** `Assets/_Project/Scripts/Gym/GymProgressionSystem.cs`

### ApplyResult()

```csharp
public void ApplyResult(GymSessionData data)
{
    float adaptationNorm = Mathf.Clamp01(stats.TrainingAdaptation / 100f);
    float fatigueNorm    = Mathf.Clamp01(stats.FatigueDebt / 100f);

    float quality = 0.55f * data.energyAtStart
                  + 0.35f * adaptationNorm
                  + 0.10f * (1f - fatigueNorm);

    if (quality >= excellentThreshold)      // >= 0.72
        data.result = GymSessionResult.Excellent;
    else if (quality < strainedThreshold)   // < 0.45
        data.result = GymSessionResult.Strained;
    else
        data.result = GymSessionResult.Solid;

    stats.ApplyGymProgression(data.adaptationGain, data.fatigueGain);
    stats.DrainEnergy(data.energyConsumed);
    HasTrainedToday = true;
}
```

----

**Alur kerja:** Hitung `quality` dari energi awal, adaptasi, dan fatigue → klasifikasi Excellent/Solid/Strained → apply gain adaptasi/fatigue → drain energi → set `HasTrainedToday`.

**Alasan desain:**
- **Bobot energi 55%:** Latihan saat lelah menghasilkan sesi buruk—mendorong makan/tidur sebelum gym, selaras edukasi recovery.
- **Adaptasi 35%:** Pemain reguler (adaptasi tinggi) mendapat sesi lebih baik—mensimulasikan "tubuh terbiasa" tanpa RPG level kompleks.
- **Fatigue 10% inverse:** Overtraining menurunkan kualitas; cukup kecil agar tidak mendominasi di awal permainan.
- **Tiga tier hasil untuk evaluator:** Mapping langsung ke skor `DailyHealthEvaluator` (+3/+2/+1).

**Integrasi:** `GymSessionController`, `DailyHealthEvaluator.EvaluateGym`, `PlayerStats.ApplyGymProgression`.

### NotifyDayResetFromSleep()

```csharp
public void NotifyDayResetFromSleep()
{
    HasTrainedToday = false;
    stats.ResetGymCompletedForNewDay();
    float adaptationDecay = stats.FatigueDebt > overFatigueDecayThreshold
        ? sleepAdaptationDecayWhenOverFatigued : 0f;
    stats.ApplyGymProgression(-adaptationDecay, -sleepFatigueRecovery);
}
```

----

**Alur kerja:** Reset flag harian → recovery fatigue 7,5 poin → jika fatigue >35, decay adaptasi kecil.

**Alasan desain:**
- **Recovery fatigue per malam:** Istirahat mengurangi beban kumulatif—mencegah spiral mustahil jika pemain gym beberapa hari berturut-turut.
- **Decay adaptasi saat over-fatigue:** Mensimulasikan overtraining: latihan tanpa recovery membuat progres mundur, bukan hanya stagnasi.
- **Dipanggil dari `SleepBedInteractable`:** Sinkron dengan siklus hari, sama seperti reset kerja.

**Integrasi:** `SleepBedInteractable`, `PlayerStats` fatigue/adaptation fields.

---

## GymSessionController

**File:** `Assets/_Project/Scripts/Gym/GymSessionController.cs`

```csharp
private IEnumerator RunFlow()
{
    activeSession = GymProgressionSystem.Instance.PendingSession;
    SetGymSessionLock(true);

    yield return PlayDialogueAndWait(trainerDialogueController.BuildPreTrainingDialogue(activeSession));

    clockUI.PlayTimeSkipAnimation("Sesi Latihan", activeSession.startHour, activeSession.endHour,
        trainingAnimationDuration, () => animationDone = true);

    progression.ApplyResult(activeSession);
    GymProgressionSystem.SyncGameClockAfterGym(activeSession);

    yield return PlayDialogueAndWait(trainerDialogueController.BuildPostTrainingDialogue(activeSession));
    ExitToMainScene();
}
```

----

**Alur kerja:** Lock input → dialog pelatih → time-skip animasi latihan → `ApplyResult` + sync jam → dialog pasca → exit ke kota.

**Alasan desain:**
- **`SetGymSessionLock`:** Mencegah interaksi lain saat latihan—konsisten dengan work session.
- **Durasi 2 jam (1 jam sore):** Mempercepat simulasi tanpa memakan seluruh hari; sore lebih singkat karena pemain sering datang setelah kerja dengan waktu terbatas.
- **`RunFaintFlow` terpisah:** Edge case energi sangat rendah tetap punya alur naratif, bukan crash atau soft-lock.

**Integrasi:** `GymProgressionSystem`, `GymTrainerDialogueController`, `ClockAnimationUI`, `SubSceneReturnHelper`.

---

## WorkDoorInteractable dan GymDoorInteractable

**File:** `Assets/_Project/Scripts/Interaction/WorkDoorInteractable.cs`, `GymDoorInteractable.cs`

```csharp
// WorkDoorInteractable.OnInteract — validasi energi & jam, lalu:
fadeManager.FadeToBlackAndLoad("OfficeScene", fadeDuration);

// GymDoorInteractable.OnInteract — validasi serupa, lalu:
fadeManager.FadeToBlackAndLoad(gymSceneName, fadeDuration);
```

----

**Alur kerja:** `OnInteract` → validasi jam (`FacilityHours`), energi, belum aktivitas hari ini → `BuildSession` → fade load sub-scene.

**Alasan desain:**
- **Validasi di pintu, bukan di scene tujuan:** Menghemat load scene dan memberi pesan error segera ("kantor tutup", "sudah kerja hari ini").
- **Pola `IInteractable` + fade:** Konsisten dengan seluruh interaksi dunia; transisi halus menyembunyikan loading.
- **`PendingSession` sebagai bridge:** Data sesi dibuat di kota, dibaca controller di sub-scene—menghindari static global tersebar.

**Integrasi:** `FacilityHours`, `WorkSessionManager`/`GymProgressionSystem`, `FadeManager`.

---

# BATCH 4 — Player & Interaction

---

## PlayerController

**File:** `Assets/_Project/Scripts/PlayerController.cs`

`PlayerController` adalah komponen locomotion pemain berbasis `Rigidbody`: membaca input keyboard/mobile, menggerakkan karakter di `FixedUpdate`, memanggil drain energi ke `PlayerStats`, dan mengelola kunci input saat dialog/tidur/pingsan.

**Alasan pisah Update vs FixedUpdate:** Input frame-sensitive (jump, keyboard) di `Update`; physics (velocity, step assist, rotasi) di `FixedUpdate`—pola Unity standar agar gerakan konsisten di berbagai framerate.

### FixedUpdate() — Move() dan ProcessMoveDir()

```csharp
void FixedUpdate()
{
    GroundCheck();
    if (IsInputLocked) { /* zero velocity */ return; }

    ProcessMoveDir();
    Move();
    StepAssist();
    Rotate();
    ApplyJump();
    UpdateAnimator();
}

void Move()
{
    float speedMult = playerStats != null ? playerStats.GetSpeedMultiplier() : 1f;
    float speed = moveDir.magnitude > 0.1f
        ? (isRunning ? runSpeed : walkSpeed) * speedMult
        : 0f;

    Vector3 targetHorizontal = moveDir.normalized * speed;
    // MoveTowards dengan acceleration / deceleration
    rb.linearVelocity = new Vector3(nextHorizontal.x, rb.linearVelocity.y, nextHorizontal.z);
}
```

----

**Alur kerja:** Cek ground → jika input tidak terkunci, haluskan arah gerak (`ProcessMoveDir`) → hitung kecepatan jalan/lari × multiplier fase usia → terapkan ke `Rigidbody` horizontal.

**Alasan desain:**
- **`walkSpeed` 4,6 / `runSpeed` 8:** Kota dieksplorasi dalam ~4 menit/hari; kecepatan cukup untuk sampai kerja/gym tanpa terasa lambat.
- **`GetSpeedMultiplier()` dari `PlayerStats`:** Fase Lansia atau kondisi lelah bisa memperlambat gerak—feedback gameplay selaras skor kesehatan.
- **Acceleration vs deceleration terpisah:** Berhenti terasa lebih cepat daripada akselerasi—karakter terasa responsif, tidak "ice skating".
- **`StepAssist`:** Trotoar kota punya undakan kecil; tanpa ini pemain tersangkut di tepi jalan—mengurangi frustrasi eksplorasi edukatif.

**Integrasi:** `PlayerStats.GetSpeedMultiplier`, `CameraSystem` (basis arah gerak), `MobileInputController.InjectMobileInput`.

### Update() — drain energi dari gerakan

```csharp
void Update()
{
    // ... baca keyboard + mobile, cache camera forward ...

    if (playerStats != null
        && !IsInputLocked
        && ShouldDrainEnergyFromMovement())
        playerStats.DrainEnergy(
            moveDir.magnitude > 0.1f && !isRunning,
            isRunning,
            Time.deltaTime);
}

private bool ShouldDrainEnergyFromMovement()
{
    if (TimeManager.Instance != null && !TimeManager.Instance.IsRunning)
        return false;
    if (ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen)
        return false;
    if (NpcDialogueMenuController.Instance != null && dialogue.IsOpen)
        return false;
    return true;
}
```

----

**Alur kerja:** Setiap frame, jika pemain bergerak dan tidak diblokir, panggil `PlayerStats.DrainEnergy` dengan flag jalan vs lari.

**Alasan desain:**
- **Drain di `Update` dengan `Time.deltaTime`:** Sinkron dengan pembacaan input gerak; `DrainEnergy` dirancang per-frame di sisi stats.
- **Tidak drain saat waktu pause/dialog/modal:** Fair—pembaca dialog atau menu makan tidak menghukum energi; fokus edukatif pada keputusan, bukan micromanagement saat UI terbuka.
- **Boolean jalan vs lari terpisah:** Memungkinkan `runRamp` di `PlayerStats.DrainEnergy`—lari bertahap lebih mahal daripada jalan.

**Integrasi:** `PlayerStats.DrainEnergy`, `TimeManager.IsRunning`, `ModalStateManager`, `NpcDialogueMenuController`.

### LockInput() / UnlockInput() dan InjectMobileInput()

```csharp
public void LockInput(string source)
{
    string key = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
    if (!inputLocks.ContainsKey(key)) inputLocks[key] = 0;
    inputLocks[key]++;
}

public bool IsInputLocked => inputLocks.Count > 0;

public void InjectMobileInput(Vector2 moveInput)
{
    if (IsInputLocked) return;
    mobileInput = moveInput;
    hasMobileInput = moveInput.sqrMagnitude > 0.001f;
    mobileRunRequest = moveInput.y >= 0.9f && moveInput.magnitude >= 0.95f;
}
```

----

**Alur kerja:** Sistem lain (`SleepBedInteractable`, dialog, faint) memanggil `LockInput("Sleep")` → counter per sumber naik → gerak nol sampai `UnlockInput` → mobile inject diabaikan saat terkunci.

**Alasan desain:**
- **Dictionary counter per sumber, bukan satu bool:** Dialog bisa overlap dengan fade; nested lock tidak saling timpa—unlock salah satu sumber tidak membuka input terlalu dini.
- **`ForceUnlockInput` untuk recovery pingsan:** `EnergySystem` bisa paksa buka kunci setelah notifikasi—escape hatch jika state macet.
- **Keyboard prioritas atas mobile:** Developer testing di PC tidak tertimpa joystick; mobile tetap jalan saat keyboard idle.
- **Run via joystick push forward full:** Pola umum game mobile tanpa tombol sprint terpisah.

**Integrasi:** `EnergySystem.CompleteFaintRecovery`, `SleepBedInteractable`, `NpcDialogueMenuController`, `MobileInputController`.

### Indikator fatigue dan kesehatan di atas kepala

```csharp
void UpdateFatigueIndicator()
{
    // Tampil jika EnergyState Warning/Critical — warna + pulse
}

void UpdateHealthIndicator()
{
    // Tampil jika HealthScoreThisPhase < 40 dan sudah di-acknowledge
}
```

----

**Alur kerja:** Setiap `Update`, baca state energi/skor fase → tampilkan/sembunyikan `TextMeshPro` floating di atas karakter dengan animasi pulse.

**Alasan desain:**
- **Feedback dunia, bukan hanya HUD bar:** Pemain yang fokus kamera ke depan tetap melihat peringatan—mendukung awareness kesehatan tanpa selalu melihat UI corner.
- **Threshold selaras `PlayerStats` (40):** Satu bahasa visual dengan `HealthAlertPanelController` dan mortalitas.
- **Pulse speed berbeda Warning vs Critical:** Urgency meningkat secara visual sebelum pingsan.

**Integrasi:** `PlayerStats.EnergyState`, `PlayerStats.ShouldShowHealthIndicators`, `HUDManager` (bar paralel).

---

## UniversalInteractionController

**File:** `Assets/_Project/Scripts/Interaction/UniversalInteractionController.cs`

```csharp
void FixedUpdate()
{
    ResolveCurrentInteractable();

    if (pendingInteract)
    {
        pendingInteract = false;
        TryInteract(currentInteractable, currentInteractableTransform, currentInteractableCollider);
    }
}

private void ResolveCurrentInteractable()
{
    for (int i = 0; i < InteractableRegistry.Entries.Count; i++)
    {
        float distance = (targetPoint - playerPos).magnitude;
        if (distance > interactDistance) continue;

        float score = -distance + tieBreakForwardWeight * Vector3.Dot(forward, dir);
        // Pilih entry dengan skor tertinggi yang lolos CanInteract + line-of-sight
    }
}
```

----

**Alur kerja:** `FixedUpdate` scan registry → skor jarak + arah hadap → buffer input di `Update`, eksekusi di `FixedUpdate` → panggil `IInteractable.Interact` jika valid.

**Alasan desain:**
- **Registry, bukan `Physics.OverlapSphere` tiap frame:** Performa lebih stabil di scene kota penuh collider; interactable mendaftar sendiri saat enable.
- **Skor jarak + dot forward:** Objek di depan pemain diprioritaskan—mengurangi interaksi salah target saat banyak NPC berdekatan.
- **Input di `Update`, aksi di `FixedUpdate`:** Menyamakan timing dengan fisika karakter dan menghindari double-trigger frame.
- **Blok saat modal:** Mencegah buka menu makan sambil dialog NPC terbuka—UX dan state machine lebih bersih.

**Integrasi:** `InteractableRegistry`, `ModalStateManager`, `ProximityInteractButton`, semua `IInteractable`.

---

## InteractableRegistry

**File:** `Assets/_Project/Scripts/Interaction/InteractableRegistry.cs`

```csharp
public static void Register(IInteractable interactable, Collider collider, Transform transform)
{
    entries.Add(new Entry { Interactable = interactable, Collider = collider, Transform = transform });
}

public static void Unregister(IInteractable interactable) { ... }
```

----

**Alur kerja:** `OnEnable` interactable → `Register` dengan collider/transform → `UniversalInteractionController` iterasi list → `OnDisable` → `Unregister`.

**Alasan desain:**
- **O(1) register, O(n) scan n kecil:** Lebih murah daripada tag search seluruh hierarchy setiap frame di Unity.
- **Coupling rendah:** Interactable tidak perlu referensi ke player; registry sebagai bus data.
- **Collider + Transform terpisah:** Line-of-sight dan hit point bisa memakai collider bounds yang berbeda dari root transform.

**Integrasi:** Semua pintu, NPC, makanan, tempat tidur yang implement `IInteractable`.

---

## CharacterModelSwapper

**File:** `Assets/_Project/Scripts/Player/CharacterModelSwapper.cs`

```csharp
public static BodyBuild ResolveBodyBuild(float bmi)
{
    if (bmi < 18.5f) return BodyBuild.Kurus;
    if (bmi < 25f)   return BodyBuild.Ideal;
    return BodyBuild.Overweight;
}

public void EvaluateAndSwap()
{
    BodyBuild build = ResolveBodyBuild(stats.PlayerBMI);
    if (build == activeBuild && gender == activeGender) return;
    SetActiveModel(ResolveModel(gender, build), gender);
    locomotionRig?.RefreshAfterModelSwap();
}
```

----

**Alur kerja:** Hitung BMI dari `PlayerStats` → map ke `BodyBuild` WHO → jika berubah, swap prefab aktif → refresh rig animasi.

**Alasan desain:**
- **Klasifikasi WHO (18,5 / 25):** Ambang standar yang bisa dipertanggungjawabkan di skripsi kesehatan, bukan angka arbitrary game.
- **Enam varian gender × build:** Feedback visual jelas tanpa sistem morph mesh kompleks—sesuai scope proyek akhir.
- **Early return jika tidak berubah:** Menghindari hitch frame dari destroy/instantiate prefab tiap frame.
- **Dipanggil setelah tidur:** BMI berubah via `AdjustWeight` harian; swap sinkron dengan momen refleksi pemain.

**Integrasi:** `PlayerStats.PlayerBMI`, `AdjustWeight`, locomotion rig refresh.

---

## FoodPickupInteractable

**File:** `Assets/_Project/Scripts/Interaction/FoodPickupInteractable.cs`

```csharp
public void Interact(GameObject interactor)
{
    List<FoodData> choices = GetAvailableFoodChoices();
    FoodChoiceMenuController.Instance.OpenMenu(interactionLabel, choices);
}

public void ConsumeNow(GameObject interactor, FoodData food)
{
    if (!TrySpendForFood(food, out int chargedPrice)) return;

    PlayerStats.Instance.AddFood(food.energyRestored, food.calories,
        food.moodEffect, food.protein, food.fat, countsAsMeal: true);

    PlayerActionTracker.Instance.Track(
        food.isHealthy ? ActionType.HealthyFoodTaken : ActionType.UnhealthyFoodTaken,
        food.foodName);
}
```

----

**Alur kerja:** `Interact` → filter `FoodData` by jam → buka menu → pemain pilih makan/beli/simpan → `ConsumeNow` atau stash → track aksi.

**Alasan desain:**
- **Menu tiga opsi (makan/beli/simpan):** Mensimulasikan keputusan ekonomi dan perencanaan nutrisi, bukan sekadar pickup otomatis.
- **Filter periode waktu:** Sarapan tidak tersedia malam—mendorong rutinitas harian realistis.
- **Harga dan `TrySpendForFood`:** Menghubungkan kerja (uang) dengan nutrisi; pemain miskin harus strategi.
- **Track sehat/tidak sehat:** Setiap jalur konsumsi tetap masuk evaluasi malam.

**Integrasi:** `FoodChoiceMenuController`, `SessionFoodStash`, `PlayerStats`, `PlayerActionTracker`.

---

## NpcDialogueMenuController

**File:** `Assets/_Project/Scripts/UI/NpcDialogueMenuController.cs`

```csharp
public bool OpenDialogue(IDialogueActor npc, DialogueGraphData graph)
{
    activeNpc = npc;
    activeGraph = graph;
    activeNode = activeGraph.GetNode(activeGraph.startNodeId);

    PauseGameplay();
    RenderNode();
    PlayOpenAnimation();
    return true;
}

private void RenderNode()
{
    SetDialogueLine(activeNode.PickLine(seed));
    List<DialogueChoiceData> choices = activeNpc.GetAvailableChoices(activeNode);
  // Tampilkan kartu pilihan; pilih → ApplyConsequence → lanjut node
}
```

----

**Alur kerja:** `OpenDialogue` → pause waktu & input → render node + pilihan → pilih → apply consequence → node berikutnya atau tutup.

**Alasan desain:**
- **Graph data (`DialogueGraphData`):** Narasi bisa direvisi tanpa recompile; cocok untuk NPC banyak dengan pola sama.
- **`PauseGameplay`:** Dialog adalah fokus tunggal; waktu tidak berjalan saat membaca—fair untuk pemain mobile.
- **`IDialogueActor` filter pilihan:** Rumah sakit vs restoran punya opsi berbeda dari graph yang sama—reuse asset dialog.
- **Singleton persisten:** Dialog bisa dipanggil dari sub-scene office/gym dan ending tanpa referensi scene-specific.

**Integrasi:** `TimeManager.PauseTime`, `ModalStateManager`, bos/pelatih/dokter NPC.

---

## PlayerActionTracker

**File:** `Assets/_Project/Scripts/Tracking/PlayerActionTracker.cs`

```csharp
public void Track(ActionType actionType, string sourceId)
{
    counts[actionType]++;
    if (IsPositive(actionType)) positiveByPeriod[period]++;
    else if (IsNegative(actionType)) negativeByPeriod[period]++;
    OnActionTracked?.Invoke(actionType, sourceId);
}

public BranchOutcome EvaluateBranchOutcome()
{
    int score = positive * 3 - negative * 3 + Mathf.Clamp(generic, 0, 5)
              - warningEvents - criticalEvents * 2 - faintEvents * 4;
    if (score >= 5)  return HealthyPath;
    if (score <= -3) return RiskyPath;
    return MixedPath;
}
```

----

**Alur kerja:** Setiap aksi → increment counter per `ActionType` dan periode → optional event → `EvaluateBranchOutcome` saat waktu habis.

**Alasan desain:**
- **Counter per periode (Pagi/Siang/Sore/Malam):** Memungkinkan analisis pola waktu tanpa menyimpan log mentah yang boros memori.
- **Pemisahan makan harian vs lifetime events:** Makan reset tiap tidur; pingsan/warning akumulatif untuk mortalitas—dua horizon waktu berbeda.
- **Formula branch outcome:** Bobot faint ×4 menekankan bahwa pingsan berulang adalah sinyal risiko kuat, selaras evaluator kesehatan.

**Integrasi:** Semua interactable makan/dialog; `SessionFlowController`; `DailyHealthEvaluator` (diet).

---

# BATCH 5 — UI & Flow

---

## HUDManager

**File:** `Assets/_Project/Scripts/UI/HUDManager.cs`

```csharp
void UpdateEnergyBar()
{
    float energyNormalized = Mathf.Clamp01(playerStats.EnergyPercent);
    bool isDraining = energyNormalized < energyVisualPercent;

    float frontSpeed = isDraining ? energyFrontDrainSpeed : energyFrontRecoverSpeed;
    energyVisualPercent = Mathf.MoveTowards(energyVisualPercent, energyNormalized,
        frontSpeed * Time.deltaTime);

    energyBarFill.fillAmount = energyVisualPercent;

    if (energyNormalized > 0.60f)      energyBarFill.color = colorNormal;
    else if (energyNormalized > 0.30f) energyBarFill.color = colorWarning;
    else                               energyBarFill.color = colorCritical;
}
```

----

**Alur kerja:** Setiap frame baca `PlayerStats` dan `TimeManager` → update bar energi (dengan delay visual), mood, kalori/AKG, sisa waktu → ubah warna menurut threshold.

**Alasan desain:**
- **Chip delay pada drain energi:** Bar "mengikuti" dengan lag saat turun—pemain sempat melihat warning sebelum angka mentah habis; saat naik, recovery lebih cepat (feedback positif).
- **Threshold 60%/30% selaras `EnergyState`:** Warna hijau/oranye/merah konsisten dengan logika pingsan di `PlayerStats`—satu bahasa visual di seluruh game.
- **Kalori vs target AKG:** Menampilkan progress gizi harian tanpa kalkulator eksternal; mendukung tujuan edukatif simulasi.
- **Hide di menu scene:** Menghindari HUD gameplay muncul di main menu—polish UX.

**Integrasi:** Event `OnCaloriesChanged`, `TimeManager.GetFormattedTimeRemaining`, `ModalStateManager` (sembunyi saat modal).

---

## FadeManager

**File:** `Assets/_Project/Scripts/UI/FadeManager.cs`

```csharp
public void FadeToBlackAndLoad(string sceneName, float fadeDuration)
{
    _canvasGroup.blocksRaycasts = true;
    FadeToBlack(fadeDuration, () =>
    {
        _fadeRoutine = StartCoroutine(LoadThenFadeInRoutine(sceneName, fadeDuration));
    });
}

private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onComplete)
{
    while (t < duration)
    {
        _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
        _canvasGroup.blocksRaycasts = _canvasGroup.alpha > 0f;
        yield return null;
    }
    onComplete?.Invoke();
}
```

----

**Alur kerja:** Fade alpha canvas ke 1 → callback load scene → fade in ke 0 → release input block.

**Alasan desain:**
- **Persisten (`DontDestroyOnLoad`):** Fade kontinyu antar kota–kantor–gym; pemain tidak melihat "flash" putih load scene.
- **`blocksRaycasts` mengikuti alpha:** Mencegah klik tembus ke dunia saat layar setengah gelap—bug umum pada overlay tanpa raycast block.
- **`ReleaseInputBlock` saat kembali kota:** Sub-scene bisa lupa melepas lock; fade manager sebagai safety net terakhir.

**Integrasi:** `WorkDoorInteractable`, `GymDoorInteractable`, `SubSceneReturnHelper`, `SleepBedInteractable`.

---

## ClockAnimationUI

**File:** `Assets/_Project/Scripts/UI/ClockAnimationUI.cs`

```csharp
public void PlayTimeSkipAnimation(string title, float startHour, float endHour,
    float customDuration, Action onComplete)
{
    float hoursForward = toHour - fromHour;
    if (hoursForward <= 0f) hoursForward += 24f;

    while (elapsed < duration)
    {
        float eased = Mathf.SmoothStep(0f, 1f, elapsed / duration);
        float currentHour = Mathf.Repeat(fromHour + (hoursForward * eased), 24f);
        UpdateClockVisual(currentHour);
        yield return null;
    }
    onComplete?.Invoke();
}
```

----

**Alur kerja:** Interpolasi `SmoothStep` dari `startHour` ke `endHour` selama `duration` → update visual jarum jam → invoke callback selesai.

**Alasan desain:**
- **Visual time-skip vs jump instan:** Pemain "merasakan" waktu berlalu saat tidur/kerja/gym—menjaga immersi tanpa menunggu real-time.
- **`Mathf.Repeat` untuk wrap 24 jam:** Tidur 21→07 melewati tengah malam tanpa bug aritmetika jam.
- **`PlayWorkAnimation` dengan `completionRatio`:** Shift parsial punya animasi lebih pendek—feedback visual selaras gaji/energi.

**Integrasi:** `SleepBedInteractable`, `WorkSessionController`, `GymSessionController`, `SessionTimeSkipPresenter`.

---

## MobileInputController

**File:** `Assets/_Project/Scripts/Player/MobileInputController.cs`

```csharp
void Update()
{
    if (!ShouldEnableTouchUi()) return;

    MoveInput = moveStickPresenter.ReadInput();
    LookDelta = touchRouter.ReadLookDelta();
}
```

----

**Alur kerja:** Jika touch UI aktif → baca joystick kiri (`MoveInput`) dan swipe kanan (`LookDelta`) → `PlayerController` konsumsi sebagai input alternatif keyboard-mouse.

**Alasan desain:**
- **Satu abstraction input:** `PlayerController` tidak perlu tahu platform; memudahkan build Android/PC dari codebase sama.
- **Sembunyikan di office/gym:** Sub-scene fokus dialog dan animasi; joystick tidak relevan dan mengganggu layar kecil.
- **Presenter pattern (stick/router):** Memisahkan UI touch dari logika gerak—UI bisa diganti tanpa ubah controller.

**Integrasi:** `PlayerController`, `ProximityInteractButton`, scene detection.

---

## ModalStateManager

**File:** `Assets/_Project/Scripts/UI/ModalStateManager.cs`

```csharp
public void OpenModal(string modalName)
{
    modalCountsBySource[modalName] = GetCount(modalName) + 1;
}

public bool IsAnyModalOpen => GetActiveModalCount() > 0;
```

----

**Alur kerja:** `OpenModal(name)` increment counter per sumber → `CloseModal` decrement → `IsAnyModalOpen` true jika total > 0.

**Alasan desain:**
- **Stack counter, bukan boolean tunggal:** Dialog bisa membuka submenu; nested modal tidak saling timpa state.
- **Nama sumber (string key):** Debug mudah ("siapa yang buka modal?") tanpa enum besar yang harus diupdate tiap fitur baru.
- **Gate interaksi global:** Satu titik cek untuk `UniversalInteractionController` dan HUD—menghindari duplikasi if-modal di puluhan script.

**Integrasi:** `NpcDialogueMenuController`, `FoodChoiceMenuController`, `EndingManager`, `HealthAlertPanelController`.

---

## ProximityInteractButton

**File:** `Assets/_Project/Scripts/UI/ProximityInteractButton.cs`

```csharp
void LateUpdate()
{
    if (interactionController.TryGetProximityActionLabel(out string label))
        Show(label);
    else
        HideImmediate();
}

void OnClick()
{
    interactionController.TriggerInteractFromMobile();
}
```

----

**Alur kerja:** `LateUpdate` query label dari `UniversalInteractionController` → tampil/sembunyi tombol → tap memanggil `TriggerInteractFromMobile`.

**Alasan desain:**
- **`LateUpdate` setelah controller resolve target:** Label selalu sinkron dengan interactable terdekat frame ini—mengurangi flicker "Tidur" → hilang → "Makan".
- **Label kontekstual dari `GetInteractionText`:** Satu tombol untuk semua interaksi—pola UX mobile standar (satu tombol aksi dinamis).
- **Paritas dengan tombol E desktop:** Logika interaksi identik; hanya input layer berbeda.

**Integrasi:** `UniversalInteractionController`, HUD mobile layout.

---

# KELENGKAPAN — Method & Script Tambahan

Bagian ini melengkapi dokumen dengan path file eksplisit, method penting yang belum dijelaskan, dan script inti yang sebelumnya hanya disebut singkat. Setiap entri mengikuti format yang sama: **Alur kerja**, **Alasan desain**, **Integrasi**.

---

## PlayerStats — method tambahan

**File:** `Assets/_Project/Scripts/Core/PlayerStats.cs`

### AddFood()

```csharp
public void AddFood(float energyAmount, float calories, float moodEffect,
    float protein, float fat, bool countsAsMeal = false)
{
    ModifyEnergy(energyAmount);
    totalCaloriesConsumed += calories;
    dailyProtein += protein;
    dailyFat += fat;
    ModifyMood(moodEffect);
    OnCaloriesChanged?.Invoke(totalCaloriesConsumed);
    if (countsAsMeal)
        ClearPostActivityTravelGrace();
}
```

----

**Alur kerja:** Terapkan delta energi/kalori/makro/mood → jika `countsAsMeal`, matikan travel grace.

**Alasan desain:**
- **Satu API konsumsi:** Warung, stash, rumah, dan recovery tidur memakai jalur sama—nutrisi tidak bisa "lolos" tanpa tercatat.
- **`countsAsMeal` eksplisit:** Snack ringan vs makan proper dibedakan untuk travel grace; mencegah abuse makan 1 byte untuk mematikan diskon yang seharusnya untuk recovery pasca gym.
- **Event `OnCaloriesChanged`:** HUD update reaktif tanpa polling setiap field nutrisi.

**Integrasi:** `FoodPickupInteractable`, `SessionFoodStash`, `SleepBedInteractable.ApplyRecovery`, `HUDManager`.

### RegisterDailyHealthSnapshot()

```csharp
public void RegisterDailyHealthSnapshot(DailyHealthResult evalResult,
    bool disturbedSleep, float energyBeforeSleep,
    bool workedYesterday, bool trainedYesterday, bool overworkedYesterday,
    float calorieRatio)
{
    totalDaysEvaluated++;
    if (totalFood == 0) noFoodDays++;
    if (evalResult.dietScore < 0f) poorDietDays++;
    if (calorieRatio > 1.30f) highCalorieDays++;
    else if (calorieRatio > 0f && calorieRatio < 0.50f) lowCalorieDays++;
    if (disturbedSleep) disturbedSleepDays++;
    if (energyBeforeSleep <= 0.20f) lowEnergySleepDays++;
    if (trainedYesterday) trainedGymDays++; else skippedGymDays++;
    if (!workedYesterday) skippedWorkDays++;
    if (overworkedYesterday) overworkedDays++;
}
```

----

**Alur kerja:** Increment `totalDaysEvaluated` → update counter boolean harian (diet buruk, skip gym/kerja, tidur terganggu, dll.) berdasarkan hasil evaluasi dan snapshot.

**Alasan desain:**
- **Lifetime counters terpisah dari skor fase:** Skor bisa pulih; pola buruk akumulatif tetap tercatat untuk NCD risk dan mortalitas—dua lensa evaluasi (jangka pendek vs panjang).
- **Dipanggil setelah `Evaluate`, sebelum reset harian:** Urutan menjamin data evaluasi dan snapshot konsisten.
- **Threshold implisit di field (mis. `dietScore < 0`):** Hari "cukup buruk" di diet dihitung tanpa duplikasi logika evaluator.

**Integrasi:** `LifestyleMortalityEvaluator.HasChronicPoorPattern`, `EndingManager.BuildDiseaseRisks`, `SleepBedInteractable.ApplyRecovery`.

### ApplyPhaseModifiers()

```csharp
public void ApplyPhaseModifiers()
{
    PhaseModifierData data = GetPhaseModifier(currentAgeStage, playerGender);
    dailyCalorieTarget    = data.dailyCalorieTarget;
    movementDrainModifier = Mathf.Clamp(data.movementDrainMultiplier, 0.5f, 1.5f);
    moodDrainRate         = data.moodDrainMultiplier * 0.2f;
}

// Contoh: Youth+Male → 2600 kkal | Senior+Female → 1700 kkal (AKG disederhanakan)
```

----

**Alur kerja:** Lookup `PhaseModifierData` by fase + gender → set `dailyCalorieTarget`, `movementDrainModifier`, `moodDrainRate`.

**Alasan desain:**
- **AKG Indonesia 2019 (disederhanakan):** Memberi landasan ilmiah di skripsi—bukan angka random; contoh Youth Male 2600 vs Senior Female 1700 kkal.
- **Drain modifier per fase:** Lansia lebih cepat kehabisan energi gerak—selaras fase usia simulasi.
- **Clamp modifier [0,5–1,5]:** Mencegah tuning asset menghasilkan gameplay mustahil.

**Integrasi:** `SyncDayAndTryAdvanceAgeStage`, `DailyHealthEvaluator` (calorieRatio), `DrainEnergy`.

---

## TimeManager — method tambahan

**File:** `Assets/_Project/Scripts/Core/TimeManager.cs`

### SetTimeByHour()

```csharp
public void SetTimeByHour(float hour)
{
    float clampedHour = Mathf.Clamp(hour, 6f, 24f);
    float normalized = (clampedHour - 6f) / 18f;
    currentGameTime = totalGameDuration * normalized;
    CheckPeriodChange();
}
```

----

**Alur kerja:** Clamp jam [6–24] → konversi ke `currentGameTime` normalisasi → `CheckPeriodChange`.

**Alasan desain:**
- **Inverse mapping dari `CurrentHour`:** Satu rumus konsisten; menghindari drift waktu setelah banyak skip.
- **Clamp 6–24:** Hari simulasi tidak dimulai sebelum bangun atau melewati malam tanpa tidur eksplisit.
- **Dipakai setelah aktivitas maju jam:** Kerja/gym/tidur/RS semua memakai API sama.

**Integrasi:** `SleepBedInteractable`, `SyncGameClockAfterWork/Gym`, `HospitalDoorInteractable`.

### CheckPeriodChange()

```csharp
void CheckPeriodChange()
{
    if (currentGameTime < morningEnd)       newPeriod = TimePeriod.Morning;
    else if (currentGameTime < afternoonEnd) newPeriod = TimePeriod.Afternoon;
    else if (currentGameTime < eveningEnd)   newPeriod = TimePeriod.Evening;
    else                                     newPeriod = TimePeriod.Night;

    if (newPeriod != currentPeriod)
    {
        currentPeriod = newPeriod;
        OnPeriodChanged?.Invoke(currentPeriod);
    }
}
```

----

**Alur kerja:** Bandingkan `currentGameTime` dengan ambang 25% per periode → jika berubah, update `currentPeriod` dan invoke `OnPeriodChanged`.

**Alasan desain:**
- **Empat periode sama panjang (25% durasi hari):** Sederhana untuk dijelaskan; ~1 menit real-time per periode pada durasi 4 menit/hari.
- **Event-driven, bukan polling di banyak script:** `FoodData`, HUD, tracker subscribe satu sumber kebenaran.
- **`SyncPeriodThresholdsFromTotalDuration`:** Jika durasi hari diubah di Inspector, ambang ikut menyesuaikan.

**Integrasi:** `PlayerActionTracker` (periode aktif), `FoodPickupInteractable`, `HUDManager`.

---

## DailyHealthEvaluator — EvaluateSleep()

**File:** `Assets/_Project/Scripts/Health/Dailyhealthevaluator.cs`

```csharp
private static float EvaluateSleep(bool disturbedSleep, float energyBeforeSleep)
{
    float score = disturbedSleep ? SleepDisturbedBonus : SleepNormalBonus;
    // SleepNormalBonus = +1.0 | SleepDisturbedBonus = 0.0

    if (energyBeforeSleep < 0.20f)
        score += SleepLowEnergyPen;  // -1.0

    return score;
}
```

----

**Alur kerja:** Beri bonus +1 jika tidur normal, 0 jika terganggu → penalti −1 jika energi sebelum tidur <20%.

**Alasan desain:**
- **Bobot kecil (max +1):** Tidur sudah mempengaruhi energi pagi di `ApplyRecovery`; domain evaluator tidak double-count pemulihan penuh.
- **Disturbed = 0 bukan negatif besar:** Gangguan tidur sudah menghukum via 65% recovery—penalti evaluator tambahan ringan agar tidak terasa triple-punish.
- **Low energy before sleep:** Mensimulasikan tidur "drop" saat tubuh sangat lelah—selaras risiko kesehatan tidur berkualitas buruk.

**Integrasi:** `DailyHealthEvaluator.Evaluate`, faktor gangguan dari `CalculateSleepDisturbanceChance`.

---

## EndingManager — ShowEndingRoutine()

**File:** `Assets/_Project/Scripts/Core/EndingManager.cs`

```csharp
private IEnumerator ShowEndingRoutine(EndingType type, PlayerStats.Gender gender)
{
    EnsurePanel();
    ModalStateManager.Instance?.OpenModal(ModalKey);

    titleText.text = GetTitle(type);
    string narrative = GetNarrative(type, gender);
    narrative += "\n\n" + BuildDiseaseInfoSection(PlayerStats.Instance, type);

    // Typewriter effect ~50 karakter/detik
    while (visible < narrative.Length) { ... }

    // Tunggu pemain tutup panel → tampilkan recap → PlayCredits()
}
```

----

**Alur kerja:** Buka modal ending → typewriter narasi + `BuildDiseaseInfoSection` → tunggu input pemain → recap → credits.

**Alasan desain:**
- **Typewriter ~50 char/detik:** Memaksa pemain membaca risiko penyakit, bukan skip instan—pedagogis untuk ending.
- **Satu coroutine untuk semua tipe ending:** Good/Neutral/Bad/PrematureDeath share pipeline UI; hanya konten teks berbeda.
- **`OpenModal` via `ModalStateManager`:** Blok interaksi dunia selama ending—fokus pada penutup simulasi.

**Integrasi:** `BuildDiseaseRisks`, `BuildRecapContent`, `ModalStateManager`, dipanggil dari `TriggerEnding` dan `TriggerPrematureDeath`.

---

## SleepBedInteractable — Interact() dan konfirmasi tidur

**File:** `Assets/_Project/Scripts/Interaction/SleepBedInteractable.cs`

```csharp
public void Interact(GameObject interactor)
{
    if (!CanSleepNow())
    {
        HandleBlockedSleepAttempt();
        return;
    }

    if (ShouldRequestSleepConfirm())
    {
        RequestSleepConfirm();  // pemain harus tekan E sekali lagi dalam 2,2 detik
        return;
    }

    BeginSleepRoutine(interactor, false);
}

private float CalculateLateWakeChance(...)
{
    // Faktor: pola gula tinggi, overwork, fatigue debt tinggi, energi rendah saat tidur
    return Mathf.Clamp01(lateWakeBaseChance + ...);
}
```

----

**Alur kerja:** `CanSleepNow` → optional double-confirm (2,2 detik) → `BeginSleepRoutine` → `CalculateLateWakeChance` mempengaruhi jam/energi bangun.

**Alasan desain:**
- **Hanya malam (kecuali forced):** Mendorong siklus hari lengkap—pemain tidak bisa skip aktivitas dengan tidur siang sembarangan.
- **Konfirmasi ganda:** Tidur tidak reversible; mencegah mis-press E di mobile yang fatal untuk evaluasi hari.
- **Preview risiko gangguan tidur saat confirm:** Pemain informed sebelum commit—transparansi untuk sidang ("UI edukatif").
- **Late wake chance:** Bangun 09.00 dengan energi berkurang mensimulasikan jetlag/rutinitas buruk akibat gula tinggi, overwork, fatigue.

**Integrasi:** `TimeManager` (periode Malam), UI confirm, `CalculateSleepDisturbanceChance`.

---

## WorkSessionManager — PrepareWorkSessionOutcome() dan CanWork()

**File:** `Assets/_Project/Scripts/Session/WorkSessionManager.cs`

```csharp
public void PrepareWorkSessionOutcome(WorkSessionData data)
{
    if (energy < 0.30f)
    {
        float minimumFinishRatio = energy < 0.20f ? 0.25f : 0.40f;
        completionRatio = Mathf.Lerp(minimumFinishRatio, 0.75f, normalizedLowEnergy);
        performanceDrop = true;
    }
    data.completionRatio = completionRatio;
    data.performanceDropped = performanceDrop;
}

public bool CanWork(float energyNormalized)
{
    if (HasWorkedToday) return false;
    if (energy < 0.10f) return false;
    return FacilityHours.IsWorkOpen(TimeManager.Instance);
}
```

----

**Alur kerja:** `PrepareWorkSessionOutcome` set `completionRatio` dan `performanceDropped` jika energi <30% → `CanWork` gate di pintu.

**Alasan desain:**
- **Prepare sebelum animasi:** `ClockAnimationUI` butuh ratio untuk durasi visual sebelum `ApplyResult` final.
- **`minimumFinishRatio` 0,25 vs 0,40:** Energi sangat kritis (<20%) = hampir tidak produktif; masih ada partial reward kecil agar tidak zero-sum total.
- **`CanWork` tiga syarat:** Sekali/hari, energi 10%, jam buka—semua bisa dijelaskan ke penguji sebagai simulasi disiplin kerja.

**Integrasi:** `WorkDoorInteractable`, `WorkSessionController`, `ClockAnimationUI.PlayWorkAnimation`.

---

## GymProgressionSystem — BuildSession() dan CanTrain()

**File:** `Assets/_Project/Scripts/Gym/GymProgressionSystem.cs`

```csharp
public bool CanTrain(float energyNormalized, TimeManager.TimePeriod period)
{
    if (HasTrainedToday) return false;
    if (period == TimePeriod.Night) return false;
    return energyNormalized >= minEnergyToTrain;  // default 15%
}

public GymSessionData BuildSession(TimeManager.TimePeriod period, float energyNormalized)
{
    data.tierAtStart = EvaluateTier(adaptation, fatigue);
    ResolveGymSessionHours(data, period);  // 2 jam, atau 1 jam di sore
    PendingSession = data;
    return data;
}

private GymTier EvaluateTier(float adaptation, float fatigue)
{
    if (adaptation >= 55 && fatigue <= 40) return Advanced;
    if (adaptation >= 25 && fatigue <= 55) return Regular;
    return Beginner;
}
```

----

**Alur kerja:** `CanTrain` validasi → `BuildSession` freeze tier + jam → `EvaluateTier` dari adaptasi/fatigue → `PendingSession` untuk gym scene.

**Alasan desain:**
- **Min energi 15% (vs kerja 10%):** Latihan fisik lebih menuntut; ambang sedikit lebih tinggi secara fisiologis.
- **Tidak gym malam:** Selaras jam fasilitas dan mendorong recovery malam untuk tidur.
- **Tier Beginner/Regular/Advanced:** Progresi terasa tanpa level RPG; dialog pelatih dan gain disesuaikan tier.
- **Jam sesi 2h (1h sore):** Sore = waktu terbatas setelah kerja; kompromi gameplay realistis.

**Integrasi:** `GymDoorInteractable`, `GymSessionController`, `GymTrainerDialogueController`.

---

## FoodData

**File:** `Assets/_Project/Scripts/Core/FoodData.cs`

```csharp
[CreateAssetMenu(fileName = "NewFood", menuName = "HealthSim/Food Data")]
public class FoodData : ScriptableObject
{
    public float calories, energyRestored, moodEffect, fat, protein, carbohydrate;
    public bool isHealthy;
    public FoodCategory category;

    public bool IsAvailableAt(TimeManager.TimePeriod period) { ... }

    public int GetEffectivePrice()
    {
        if (price > 0) return price;
        // fallback harga per kategori (sehat vs junk)
    }
}
```

----

**Alur kerja:** Designer buat asset → runtime baca nutrisi/harga/ketersediaan → `IsAvailableAt` filter per periode → `GetEffectivePrice` fallback jika harga 0.

**Alasan desain:**
- **ScriptableObject, bukan hard-code:** Menambah menu warung tidak perlu rebuild C#—workflow Unity standar untuk data-driven design.
- **`isHealthy` boolean:** Evaluator diet tidak perlu tahu nama makanan; cukup kategori untuk rasio sehat/junk.
- **`IsAvailableAt` per periode:** Mensimulasikan menu sarapan vs makan malam tanpa scene terpisah per jam.
- **Fallback harga per kategori:** Asset lama tanpa field harga tetap punya ekonomi gameplay.

**Integrasi:** `FoodPickupInteractable`, `SessionFoodStash`, `DailyHealthEvaluator`, `PlayerStats.AddFood`.

---

## WorkSessionData dan GymSessionData

**File:** `Assets/_Project/Scripts/Session/WorkSessionData.cs`  
**File:** `Assets/_Project/Scripts/Gym/GymProgressionSystem.cs` (struct `GymSessionData`)

```csharp
// WorkSessionData
public WorkPeriod period;
public int startHour, endHour;
public float energyAtStart;
public WorkResult result;       // Full | Partial | Failed
public int moneyEarned;
public float energyConsumed, completionRatio;

// GymSessionData
public GymTier tierAtStart;
public GymSessionResult result; // Excellent | Solid | Strained | Failed
public float qualityScore, adaptationGain, fatigueGain;
```

----

**Alur kerja:** `BuildSession` di kota isi struct → load sub-scene → controller baca/mutasi → simpan `LastSession` → malam evaluator baca hasil.

**Alasan desain:**
- **Struct snapshot, bukan live reference:** Scene office/gym bisa unload; data sesi harus survive di manager persisten.
- **Field eksplisit (`completionRatio`, `tierAtStart`):** Debugging dan penulisan skripsi jelas—setiap angka punya nama di kode.
- **Paralel Work/Gym pattern:** Arsitektur konsisten; penguji melihat pola yang sama di dua aktivitas utama.

**Integrasi:** `SleepBedInteractable` snapshot, `DailyHealthEvaluator.EvaluateWork/EvaluateGym`.

---

## IInteractable

**File:** `Assets/_Project/Scripts/Interaction/IInteractable.cs`

```csharp
public interface IInteractable
{
    string GetInteractionText();
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
```

----

**Alur kerja:** Implementor supply teks, validasi `CanInteract`, dan aksi `Interact` → registry → controller panggil via interface.

**Alasan desain:**
- **Interface minimal (3 method):** Cukup untuk semua kasus (pintu, NPC, pickup) tanpa fat interface seperti `OnHover`.
- **Decoupling player dari konkret class:** `UniversalInteractionController` tidak referensi 20 tipe interactable—scalable saat scene kota bertambah.
- **`GetInteractionText` untuk mobile bubble:** Satu method untuk desktop hint dan mobile label.

**Integrasi:** `InteractableRegistry`, `UniversalInteractionController`, semua door/food/bed/NPC.

---

## FoodChoiceMenuController

**File:** `Assets/_Project/Scripts/Interaction/FoodChoiceMenuController.cs`

```csharp
public void OpenMenu(string locationName, List<FoodData> foods)
{
    currentFoods.Clear();
    currentFoods.AddRange(foods);
    isOpen = true;
    // OnGUI menggambar window IMGUI: thumbnail, nutrisi, harga, tombol Makan/Simpan
}

private void EatFood(FoodData food)
{
    foodPickup.ConsumeNow(player, food);
    CloseMenu();
}

private void StashFood(FoodData food)
{
    SessionFoodStash.Instance.Add(food);
}
```

----

**Alur kerja:** `OpenMenu` → pause input → `OnGUI` render pilihan → `EatFood`/`StashFood` → `CloseMenu`.

**Alasan desain:**
- **IMGUI untuk menu makanan:** Iterasi cepat layout nutrisi/harga/thumbnail tanpa prefab UI berat; cocok scope skripsi.
- **`OpenHomeMenu` harga lebih murah:** Insentif masak di rumah—mensimulasikan ekonomi rumah tangga vs eating out.
- **Pause saat terbuka:** Mencegah pemain berjalan sambil memilih menu; konsisten dengan dialog NPC.

**Integrasi:** `FoodPickupInteractable`, `SessionFoodStash`, `ModalStateManager`, `PlayerController` input lock.

---

## HospitalDoorInteractable

**File:** `Assets/_Project/Scripts/Interaction/HospitalDoorInteractable.cs`

```csharp
public void OnInteract(GameObject interactor)
{
    DialogueGraphData graph = dialogueController.BuildConsultationDialogue(
        healthScore, dailyFat, dailyProtein);
    NpcDialogueMenuController.Instance.OpenDialogue(this, graph);
    awaitingConsultationClose = true;
}

private void HandleDialogueClosed()
{
    PlayerStats.Instance.SetVisitedHospital();
    // time-skip konsultasi 1 jam + panel rekomendasi jika alert aktif
}
```

----

**Alur kerja:** `OnInteract` → build dialog dinamis dari skor + makro harian → `OpenDialogue` → on close `SetVisitedHospital` + time-skip 1 jam + rekomendasi.

**Alasan desain:**
- **Dialog dinamis, bukan script tetap:** Rekomendasi dokter relevan dengan state pemain aktual—lebih edukatif daripada teks generik.
- **`SetVisitedHospital` mengurangi mortalitas:** Intervensi kesehatan punya manfaat gameplay nyata; mendorong kunjungan saat skor rendah.
- **Time-skip 1 jam:** Konsultasi memakan waktu dalam simulasi—trade-off hari yang tersisa vs manfaat risiko.
- **Dismiss health alert:** Menghubungkan RS dengan `HealthAlertPanelController`—satu loop feedback kesehatan.

**Integrasi:** `LifestyleMortalityEvaluator`, `HealthAlertPanelController`, `NpcDialogueMenuController`, `TimeManager`.

---

## NpcRestaurantInteractable

**File:** `Assets/_Project/Scripts/Interaction/NpcRestaurantInteractable.cs`

```csharp
public void Interact(GameObject interactor)
{
    DialogueGraphData graph = ResolveDialogueGraph();  // first visit vs return
    InjectMenuFromFoodPickup(graph);  // sisipkan pilihan makanan ke node dialog
    NpcDialogueMenuController.Instance.OpenDialogue(this, graph);
}

public void HandleFoodChoice(DialogueChoiceData choice)
{
    // Order makanan → ConsumeNow atau stashed order dictionary
}
```

----

**Alur kerja:** Pilih graph first/return visit → inject pilihan makanan dari `FoodPickupInteractable` → dialog → `HandleFoodChoice` konsumsi/stash.

**Alasan desain:**
- **Gabung dialog + commerce:** Restoran nyata = sosialisasi + pesan; lebih immersive daripada menu popup saja.
- **Graph berbeda first vs return:** Narasi tidak repetitif; kunjungan ulang lebih ringkas.
- **`IDialogueActor`:** Filter pilihan per konteks NPC tanpa duplikasi graph asset.

**Integrasi:** `FoodPickupInteractable`, `NpcDialogueMenuController`, `PlayerActionTracker`.

---

## GymTrainerDialogueController

**File:** `Assets/_Project/Scripts/Gym/GymTrainerDialogueController.cs`

```csharp
public DialogueGraphData BuildPreTrainingDialogue(GymSessionData session)
{
    // Node: greeting → explanation → motivation
    // Teks disesuaikan tier (Beginner/Regular/Advanced) dan energi awal
}

public DialogueGraphData BuildPostTrainingDialogue(GymSessionData session)
{
    // Umpan balik hasil: Excellent / Solid / Strained / Failed
}
```

----

**Alur kerja:** Build graph runtime dari `GymSessionData` (tier, energi, hasil) → pre/post training dipanggil `GymSessionController`.

**Alasan desain:**
- **Runtime graph, bukan asset statis:** Kombinasi tier × energi × hasil terlalu banyak untuk asset manual; generator menjaga konsistensi teks.
- **Pre-training motivasi by energi:** Feedback sebelum latihan mendorong pemain cancel jika terlalu lelah—penghematan energi strategis.
- **Post-training by result:** Menghubungkan angka `Excellent/Strained` dengan narasi pelatih—reinforcement learning implisit.

**Integrasi:** `GymSessionController`, `GymProgressionSystem.ApplyResult`, `NpcDialogueMenuController`.

---

## SubSceneReturnHelper

**File:** `Assets/_Project/Scripts/Session/SubSceneReturnHelper.cs`

```csharp
public static void ReturnToSampleScene(string spawnId, string sceneName = "SampleScene",
    bool activateTravelGrace = false)
{
    if (activateTravelGrace)
        PlayerStats.Instance?.ActivatePostActivityTravelGrace();

    SpawnPlayerManager.TargetSpawnID = spawnId;
    FadeManager.Instance.FadeToBlackAndLoad(target, ReturnFadeDuration);
}
```

----

**Alur kerja:** Optional `ActivatePostActivityTravelGrace` → set `SpawnPlayerManager.TargetSpawnID` → fade load `SampleScene`.

**Alasan desain:**
- **Static helper, bukan component scene:** Dipanggil dari controller office/gym tanpa referensi silang scene.
- **`spawnId` string:** Designer menempatkan spawn point di kota (`officedoor`, `gymdoor`) tanpa koordinat hard-code.
- **Travel grace parameter opsional:** Kerja/gym aktifkan; scene lain bisa return tanpa grace.

**Integrasi:** `FadeManager`, `SpawnPlayerManager`, `PlayerStats.ActivatePostActivityTravelGrace`.

---

## HealthAlertPanelController

**File:** `Assets/_Project/Scripts/UI/HealthAlertPanelController.cs`

```csharp
void Update()
{
    if (!IsHealthAlertActive()) return;  // HealthScoreThisPhase < 40
    if (!CanShowNow()) return;           // tidak saat modal/dialog terbuka
    pendingShowRoutine = StartCoroutine(ShowNoticeWhenReady());
}

// Setelah pemain mengakui:
stats.AcknowledgeHealthAlertNotice(40f);  // unlock indikator !!! di HUD
```

----

**Alur kerja:** Cek skor fase <40 → tunggu tidak ada modal → tampilkan notice sekali → `AcknowledgeHealthAlertNotice` unlock HUD !!!.

**Alasan desain:**
- **Threshold 40 selaras zona peringatan mortalitas (45):** Pemain mendapat peringatan sebelum risiko roll meningkat—early warning system.
- **Sekali per hari + reset `OnDayChanged`:** Tidak spam setiap frame; tetap relevan tiap hari baru jika skor masih rendah.
- **`CanShowNow` block saat modal:** Tidak menimpa dialog makanan/ending—UX bersih.
- **Acknowledge unlock HUD:** Pemain harus sadar masalah sebelum indikator permanen—forced attention edukatif.

**Integrasi:** `PlayerStats.HealthScoreThisPhase`, `HospitalDoorInteractable` (dismiss), `HUDManager`, `TimeManager.OnDayChanged`.

---

## SessionTimeSkipPresenter

**File:** `Assets/_Project/Scripts/Session/SessionTimeSkipPresenter.cs`

```csharp
public void Queue(float startHour, float endHour, string title, float duration = 3.5f)
{
    pending = new PendingSkip { startHour, endHour, title, duration, active = true };
}

private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
{
    if (pending.active)
        playRoutine = StartCoroutine(PlayPendingWhenReady());
}
```

----

**Alur kerja:** `Queue` simpan skip pending → on `SampleScene` loaded → coroutine tunggu `ClockAnimationUI` ready → play animasi.

**Alasan desain:**
- **Problem async load:** Fade selesai sebelum scene siap; animasi langsung dari office/gym bisa gagal karena `ClockAnimationUI` belum ada di kota.
- **Queue pattern persisten:** Skip "dibawa" antar scene tanpa static scattered di banyak controller.
- **Default duration 3,5 detik:** Cukup panjang untuk dibaca judul skip ("Pulang dari kantor") tanpa membosankan.

**Integrasi:** `SubSceneReturnHelper`, `ClockAnimationUI`, `SceneManager.sceneLoaded`.

---

## OfficeBossDialogueController

**File:** `Assets/_Project/Scripts/Session/OfficeBossDialogueController.cs`

```csharp
private void HandleDialogueClosed()
{
    WorkSessionManager.Instance.RequestWorkStartFromBossDialogue();
}

public void Configure(NpcDialogueInteractable boss,
    DialogueGraphData preWork, DialogueGraphData postWork) { ... }
```

----

**Alur kerja:** Configure graph pre/post work → pemain selesai dialog pre → `RequestWorkStartFromBossDialogue` → `WorkSessionController.RunFlow`.

**Alasan desain:**
- **Dialog sebagai consent kerja:** Pemain tidak langsung masuk animasi shift—narrative framing untuk sub-scene office.
- **Event `OnWorkStartRequested`:** Manager (data) memisah dari controller (presentasi)—sama pola gym dengan pelatih.
- **Post-work graph terpisah:** Umpan balik hasil `WorkResult` kontekstual setelah animasi, bukan sebelum.

**Integrasi:** `WorkSessionManager`, `WorkSessionController`, `NpcDialogueMenuController`.

---

## Daftar File per Batch (referensi cepat)

| Batch | File utama |
|-------|------------|
| Core & Health | `Core/PlayerStats.cs`, `Core/TimeManager.cs`, `Core/EndingManager.cs`, `Core/EnergySystem.cs`, `Core/FacilityHours.cs`, `Core/FoodData.cs`, `Health/Dailyhealthevaluator.cs`, `Health/LifestyleMortalityEvaluator.cs`, `Health/PhaseReviewBuilder.cs` |
| Sleep & Session | `Interaction/SleepBedInteractable.cs`, `Session/SessionFoodStash.cs`, `Session/SessionFlowController.cs`, `Session/SampleSceneBootstrap.cs`, `Manager/SpawnPlayerManager.cs`, `Session/SubSceneReturnHelper.cs`, `Session/SessionTimeSkipPresenter.cs` |
| Gym & Work | `Session/WorkSessionManager.cs`, `Session/WorkSessionController.cs`, `Session/WorkSessionData.cs`, `Gym/GymProgressionSystem.cs`, `Gym/GymSessionController.cs`, `Gym/GymTrainerDialogueController.cs`, `Interaction/WorkDoorInteractable.cs`, `Gym/GymDoorInteractable.cs` |
| Player & Interaction | `PlayerController.cs`, `Interaction/UniversalInteractionController.cs`, `Interaction/InteractableRegistry.cs`, `Interaction/IInteractable.cs`, `Interaction/FoodPickupInteractable.cs`, `Interaction/FoodChoiceMenuController.cs`, `Interaction/NpcDialogueMenuController.cs`, `Interaction/HospitalDoorInteractable.cs`, `Interaction/NpcRestaurantInteractable.cs`, `Player/CharacterModelSwapper.cs`, `Tracking/PlayerActionTracker.cs` |
| UI & Flow | `UI/HUDManager.cs`, `UI/FadeManager.cs`, `UI/ClockAnimationUI.cs`, `UI/MobileInputController.cs`, `UI/ModalStateManager.cs`, `UI/ProximityInteractButton.cs`, `UI/HealthAlertPanelController.cs` |

*Script Editor (`Assets/_Project/Scripts/Editor/`), traffic/NPC dekoratif, dan main menu sengaja tidak masuk karena di luar inti gameplay loop simulasi kesehatan.*

---

# Penutup — Alur Integrasi

Satu siklus hari simulasi berjalan sebagai berikut. `TimeManager` menjalankan jam dari 06.00 hingga 24.00 sementara `PlayerController` menggerakkan pemain dan memicu drain energi ke `PlayerStats`. Pemain beraktivitas—makan melalui `FoodPickupInteractable`, bekerja melalui `WorkDoorInteractable`, berlatang melalui `GymDoorInteractable`, berinteraksi NPC melalui `UniversalInteractionController`. Setelah sub-scene, `SpawnPlayerManager` menempatkan pemain kembali di pintu yang sesuai. Setiap aksi dicatat `PlayerActionTracker`. Saat pemain berinteraksi dengan tempat tidur, `SleepBedInteractable` menjalankan evaluasi `DailyHealthEvaluator`, roll `LifestyleMortalityEvaluator`, lalu mengganti hari via `TimeManager.AdvanceToNextDayFromSleep`. Pada hari ke-12, `EndingManager.TriggerEnding` mengklasifikasi perjalanan hidup pemain berdasarkan rata-rata skor tiga fase usia.

**Alasan arsitektur keseluruhan:**
- **Pemisahan evaluator statis vs state MonoBehaviour:** Rumus kesehatan dan mortalitas bisa dijelaskan di skripsi seperti modul bisnis murni; `PlayerStats` hanya menyimpan hasil—memudahkan penguji menilai "logika" vs "data".
- **Satu orkestrator tidur (`SleepBedInteractable`):** Semua reset harian dan transisi fase terjadi di urutan tetap—menghindari bug dan memberi narasi Bab 3 yang jelas (satu diagram siklus).
- **Sub-scene kerja/gym:** Memisahkan aktivitas fokus dari kota terbuka tanpa memuat seluruh map—trade-off load time vs kompleksitas scene, umum di game simulasi.
- **ScriptableObject untuk makanan:** Data gizi dapat diubah tim desain tanpa programmer—sesuai praktik industri game edukatif.

Pemisahan concern menjadi kunci arsitektur: logika numerik murni (`DailyHealthEvaluator`, `LifestyleMortalityEvaluator`) terpisah dari state (`PlayerStats`) dan presentasi (`HUDManager`, `EndingManager`, `FadeManager`), sehingga masing-masing subsistem dapat dijelaskan, diuji, dan didokumentasikan secara independen dalam implementasi HealthSim.
