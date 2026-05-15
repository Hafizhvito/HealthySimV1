# HealthySimV1

HealthySimV1 is a Unity 6 URP prototype focused on a short daily loop: move, interact, consume/save food, talk with NPCs, and complete one work session.

## Current Project Status

- Engine: Unity 6000.3.10f1
- Render pipeline: URP
- Main gameplay scene: Assets/Scenes/SampleScene.unity
- Secondary work scene: Assets/Scenes/OfficeScene.unity
- Runtime: playable in Editor with core loop active
- QA: manual smoke testing only
- Mobile interaction: tap world bubbles marked '?' (desktop still uses E)

Ringkasan (ID): Proyek sudah bisa dimainkan end-to-end untuk loop utama, tetapi QA masih manual dan belum ada test otomatis.

## Team Onboarding

### Teammate Quick Start

1. Install Unity Hub and Unity Editor `6000.3.10f1`.
2. Clone this repository.
3. Open the project folder in Unity Hub.
4. Wait for package import and script compilation to finish.
5. Open `Assets/Scenes/SampleScene.unity`.
6. Press Play and run the smoke test checklist below.

Catatan (ID): Untuk mulai cepat, pakai Unity versi yang sama, buka `SampleScene`, lalu Play setelah compile selesai.

### Full Setup (GitHub Clone to Play Mode)

1. Clone repository:

- `git clone <repo-url>`
- `cd HealthySimV1`

2. Confirm required folders exist after clone:

- `Assets/`
- `Packages/`
- `ProjectSettings/`

3. Open Unity Hub:

- `Add` project folder `HealthySimV1`
- Select Unity version `6000.3.10f1`

4. First open checks:

- Let Unity resolve `Packages/manifest.json` and `packages-lock.json`
- Wait until Console compile activity is idle

5. Open scenes:

- Main: `Assets/Scenes/SampleScene.unity`
- Office: `Assets/Scenes/OfficeScene.unity`

6. Run Play Mode in `SampleScene`.

Catatan (ID): Jika pertama kali buka project terasa lama, itu normal karena import package dan domain reload.

### Daily Workflow (Teammates)

1. `git pull` on your branch.
2. Open Unity and wait for compile to finish.
3. Run smoke test in `SampleScene` before coding.
4. Make changes in a focused scope.
5. Re-run smoke test after changes.
6. Commit with clear message.
7. Push branch and open PR.

Catatan (ID): Biasakan test cepat sebelum dan sesudah ngoding supaya regresi cepat ketahuan.

### Minimal Branching Recommendation

1. Keep it simple:

- `main`: stable baseline
- `feature/<short-topic>` for new work
- `fix/<short-topic>` for bug fixes

2. Prefer short-lived branches and small PRs.

Catatan (ID): Branch pendek dan PR kecil bikin review lebih cepat dan minim konflik.

### First Commit and Commit Convention

Use Conventional Commit style:

- `feat(scope): ...`
- `fix(scope): ...`
- `docs(scope): ...`
- `chore(scope): ...`

Examples:

- `feat(core): add swap-contract warning validator`
- `fix(ui): stabilize tutorial startup cutscene gating`
- `docs(onboarding): add teammate setup and smoke tests`

Catatan (ID): Format commit konsisten memudahkan tracking perubahan di tim.

### Smoke Test Checklist (Run After Open and Before PR)

1. Open `Assets/Scenes/SampleScene.unity`.
2. Press Play.
3. Verify movement:

- Walk/run/jump works.
- Camera toggle `F`/`V` works.

4. Verify interaction:

- Interact with food (`E`) and complete buy/eat or stash flow.
- Interact with NPC dialogue and finish one branch.

5. Verify onboarding:

- Intro cutscene plays.
- Sequential tutorial appears after cutscene.

6. Verify work flow:

- Use `KantorDoor` logic path and validate office transition behavior.

7. Verify gym flow:

- Use `GymDoor` logic path and validate gym transition plus trainer dialogue flow.
- Confirm gym can only be done once per day and resets after sleep.

8. Check Console for new errors.

Catatan (ID): Checklist ini cukup untuk validasi cepat bahwa loop utama tetap aman.

### Common Errors and Quick Fixes

1. Unity version mismatch:

- Symptom: package/asset import issues.
- Fix: use `6000.3.10f1` from `ProjectSettings/ProjectVersion.txt`.

2. Package errors on first open:

- Symptom: unresolved package or compile red errors.
- Fix: open Package Manager, wait for restore, reimport if needed.

3. Long compile delay:

- Symptom: scripts not ready for Play.
- Fix: wait until compile is idle; avoid editing while importing.

4. Scene not loading:

- Symptom: wrong/empty scene opens.
- Fix: manually open `Assets/Scenes/SampleScene.unity`.

5. Missing references after pull:

- Symptom: null/missing component warnings.
- Fix: close Unity, ensure all files including `.meta` are present, pull again, reopen.

Catatan (ID): Mayoritas masalah teammate baru biasanya selesai dengan versi Unity yang tepat dan menunggu import selesai.

## Implemented Features (Verified)

- Player movement and camera:
  - Rigidbody-based movement in FixedUpdate
  - Strict single jump (grounded-only consume)
  - Step assist for curbs/stairs
  - Movement energy drain rebalanced for 3m50s loop pacing
  - Sprint drain now ramps up over time (no instant full-drain spike)
  - Camera toggle and dialogue zoom behavior
  - FPP camera uses direct input without spin (Minecraft-style feel)
  - Mobile touch controls use prefab joystick under HUD_Canvas (left move joystick, right look swipe, top-right interact button) for Android and Editor force-test mode
- Input and modal safety:
  - Source-counted input locks in PlayerController
  - Central modal authority via ModalStateManager
  - Startup/scene-load modal reset
- Interaction layer:
  - Line-of-sight-based interactable targeting
  - Prompt and world-bubble interaction cues
  - Debounced E-trigger interaction flow
- Food and stash:
  - Food pickup choices, consume now, or save for session stash
  - Food economy: each food has price, UI shows price, and purchase deducts player money
  - Insufficient-money and action feedback is shown directly in Food menu panel
  - FoodData nutrient schema now includes protein, fat, and sugar fields for gameplay balancing
  - Food interaction detail line now shows compact nutrient summary: P/L/G values
  - Placeholder food catalog generator now seeds healthy and less-healthy variants for restaurant-ready iteration
  - Home food station mode now supports quick eat, quick drink, and simple meal prep to stash
  - Home food station uses discounted home pricing (default multiplier `0.65`) while stash consumption remains free after prep
  - Restaurant placeholder station now uses pricier outlet multiplier (default `1.25`) to create clear economy gap vs home station
  - Runtime placeholder bootstrap now auto-creates `HomeFoodStation_Placeholder` in `SampleScene` when no home station exists yet
  - Runtime bootstrap now enforces a clean food interaction set by keeping one restaurant pickup (prefers `Interactable_Food_Restaurant`) and removing extra `FoodPickupInteractable` duplicates
  - Legacy `Interactable_FoodCube` is now fallback-only and is auto-removed when both restaurant pickup and home station already exist
  - Editor setup menu `HealthSim/Setup/Ensure Home Food Station Placeholder` now creates a persistent placeholder in `SampleScene` so it is visible before Play and can be moved freely
  - Food menu now includes a top-right `X` close button for faster dismiss
  - Stash systems now auto-bootstrap if missing (SessionFoodStash + FoodStashMenuController) so meal prep and stash open flow stay available
  - Session stash consume/remove/clear flows
- Dialogue:
  - Graph-based dialogue with choices and consequences
  - Street and restaurant NPC routes wired via IDs/catalog
  - Cinematic dialogue UI with choice cards
- Work flow:
  - Work door eligibility checks (hour 07.00-15.00 + energy)
  - Office session flow with pre/post dialogue and payout logic
  - Partial/fail outcomes for low-energy runs
  - Safe migration note: WorkDoorInteractable now supports optional serialized manager references while preserving existing GameManager fallback behavior
  - Gym door/session flow mirrors office contract (door gate -> session scene -> trainer dialogue -> progression apply -> return)
  - Gym progression uses hidden PlayerStats fields (training adaptation and fatigue debt) without adding new HUD bars
  - Sleep day-reset now clears gym daily lock and applies overnight fatigue recovery
- Intro and tutorial onboarding:
  - Intro cutscene sequence and completion events
  - One-time character backstory dialogue box appears right after intro cutscene completion
  - Backstory content now driven by CharacterData ScriptableObject variants
  - Backstory title/body now prefers PlayerStats name (wired from PlayerData before intro)
  - Sequential tutorial panel after intro
  - Contextual tutorial toast queue (NPC/energy/food/work conditions)
  - Sequential/contextual tutorial and work reminder are suppressed while cutscene is active
  - Work reminder now appears only after cutscene and sequential tutorial have both finished
  - Sequential tutorial first-show flow now uses a single pending request path with short startup visual lock to prevent initial flicker
- Sleep loop and day transition:
  - In-game day duration is configurable via `totalGameDuration` (default 230 seconds) and split proportionally across morning/afternoon/evening/night
  - CurrentHour maps to 6.00-24.00 and time display uses HH.mm
  - Sleep is night-gated and uses a confirmation step before transition
  - Sleep reminder appears at 22.00 and re-prompts if dismissed
  - Forced sleep triggers at 24.00 with a short fade + message
  - Begadang penalty applies after forced sleep (energy 60% + movement drain 1.3x for that day)
  - Sleep transition uses fade plus clock time-skip animation
  - Sleep quality can be disturbed probabilistically based on recent work and food behavior
  - Wake message summarizes day change and relevant warnings
  - Aging progression triggers on wake at day 5 (Adult) and day 10 (Senior), with modal narrative notification
  - Aging narrative now includes senior-female menopause variants and phase score context
  - Phase modifiers now apply by age and gender (daily calories, movement drain, mood drain)
  - Next-day movement drain now supports hidden gym-based sustainability modifier (`movementDrainModifier`, no HUD exposure)
  - Late-wake consequence applies energy penalty + narrative warning only (no time cut, no period skip)
- Bazaar event (new):
  - Bazaar spawn check runs after sleep day-advance (eligible day 5/10/15...)
  - Spawn chance defaults to 80% (configurable), hides when not active
  - Bazaar interactable opens food menu with discounted prices
  - Bazaar menu selects 10 foods + 3 drinks (no duplicates) from configured pool
  - Bazaar object is a scene prefab for designer-friendly swapping
  - SpawnPoint moved to (-71.42, 0.5, -50) in front of the blue house
  - CameraSwitcher warning fixed in SampleScene

## Character Variants

Six CharacterData assets are available under `Assets/_Project/Data/Characters/`:

| Asset file         | Name  | Gender    | BMI Type    |
| ------------------ | ----- | --------- | ----------- |
| Char_Male_Kurus    | Rafi  | Laki-laki | Underweight |
| Char_Male_Normal   | Dimas | Laki-laki | Normal      |
| Char_Male_Gemuk    | Bagas | Laki-laki | Overweight  |
| Char_Female_Kurus  | Nisa  | Perempuan | Underweight |
| Char_Female_Normal | Ayu   | Perempuan | Normal      |
| Char_Female_Gemuk  | Dina  | Perempuan | Overweight  |

Backstory is read from `backstoryText` (AppendBackstory prioritizes it). Active variant is expected to be chosen by a main menu selector (not implemented yet). For quick testing, change `preferredCharacterDataAssetName` in BackstoryDialogueController.

Ringkasan (ID): Fitur inti gameplay, dialog, kerja, onboarding, tidur, ekonomi makanan berharga, dan tuning energi movement sudah aktif. Alur harian sekarang lebih utuh dan saling terhubung.

## Quick Usage Notes

- Controls:
  - Move: W A S D
  - Sprint: Left Shift
  - Jump: Space
  - Interact: E
  - Camera mode: F or V
  - Cursor cancel: Escape
  - Debug lock reset: F8
  - Mobile test mode: `MobileInputController.forceMobileUI = true` (default) to show touch controls in Editor
- Recommended quick test:

  1. Enter Play Mode in SampleScene
  2. Verify move/run/jump/step behavior
  3. Interact with food and test stash actions
  4. Open NPC dialogue and complete one dialogue branch
  5. Enter office flow and return to main scene
  6. Enter gym flow and confirm trainer pre/post dialogue plus daily lock behavior
  7. Interact with home food station and verify:

  - quick drink action works
  - meal prep stores food to stash and can be consumed later without extra cost
  - home menu pricing is cheaper than normal food station pricing
  - top-right `X` button closes food menu immediately
  - only 2 food interaction points should remain in `SampleScene`: one restaurant pickup and one home station
  - if both points already exist, `Interactable_FoodCube` should not persist
  - if no home station object exists in `SampleScene`, a placeholder cube station appears automatically near player
  - for permanent edit-mode placement, run `HealthSim/Setup/Ensure Home Food Station Placeholder`, then move the object as needed in Scene view

Ringkasan (ID): Untuk cek cepat, jalankan SampleScene lalu uji movement, interaksi makanan, dialog NPC, dan sesi kerja sekali.

## Asset Swap Contract (Phase 1 - Safe)

Tujuan: memudahkan penggantian aset sementara ke aset final tanpa memutus alur gameplay.

- Player contract:
  - Tag tetap `Player`
  - Komponen minimum: `PlayerController`, `Rigidbody`, `CapsuleCollider`, `Animator`, `UniversalInteractionController`
  - Animator parameter minimum: `Speed`, `IsGrounded`, `IsJumping`
- Office flow contract:
  - Office scene memiliki `PlayerSpawnPoint`
  - Boss office dapat ditemukan stabil (tag `NPCBoss` atau reference serialized)
- Interactable contract:
  - Interactable gameplay implement `IInteractable`
  - Collider aktif dan terdaftar melalui `InteractableRegistry`
- UI contract:
  - `HUD_Canvas` tersedia saat runtime
  - Dialog/food/stash/tutorial binding tidak boleh bergantung pada rename child tanpa update wiring
  - Placeholder interactables now have prefab assets for designer-friendly swapping

Validator dan telemetry:

- Gunakan menu editor: `HealthSim/Validate/Swap Contract (Warning Only)`
- Fallback runtime tetap dipertahankan untuk kompatibilitas, tetapi sekarang mengeluarkan log peringatan agar gap kontrak cepat terlihat

Ringkasan (ID): Fase 1 tidak mengubah behavior gameplay. Fokusnya menambah pagar aman (kontrak + validator + telemetry) sebelum swap aset besar.

## Known Limitations and Risks

- No automated PlayMode/regression tests yet
- Food/stash still uses IMGUI-era menus, while dialogue/tutorial uses runtime canvas layout
- Legacy NPCDialogue_Panel object still exists in SampleScene and can confuse scene editing
- Runtime UI scaler is not fully unified across all runtime-created canvases
  - Example: InteractionHintCanvas and IntroTextCanvas still use portrait-style reference sizing
- TMP icon glyph fallback warnings still appear for some Unicode symbols in HUD/tutorial labels
- Bazaar pool requires enough FoodData items (>=10 foods and >=3 drinks) to avoid short lists

Ringkasan (ID): Risiko utama sekarang ada di konsistensi UI dan minimnya automated test. Struktur UI lama dan baru masih campur.

## Next Steps (Short)

1. Unify UI architecture (migrate IMGUI food/stash into consistent canvas flow)
2. Balance food price and value curves (healthy vs less-healthy) for stable economy pacing
3. Add lightweight PlayMode smoke tests for movement, interaction, dialogue, sleep, and work loop
4. Continue tuning movement-energy pacing from playtest feedback (walk/run drain feel)
5. Remove or retire legacy scene objects that duplicate active systems
6. Normalize canvas/scaler conventions for all runtime-created UI

Ringkasan (ID): Fokus berikutnya adalah konsolidasi UI, menambah test otomatis ringan, dan merapikan debt teknis scene/UI.
