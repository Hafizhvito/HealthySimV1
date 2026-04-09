# HealthySimV1

HealthySimV1 is a Unity 6 URP prototype focused on a short daily loop: move, interact, consume/save food, talk with NPCs, and complete one work session.

## Current Project Status

- Engine: Unity 6000.3.10f1
- Render pipeline: URP
- Main gameplay scene: Assets/Scenes/SampleScene.unity
- Secondary work scene: Assets/Scenes/OfficeScene.unity
- Runtime: playable in Editor with core loop active
- QA: manual smoke testing only

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
  - Movement energy drain rebalanced for 5-minute loop pacing
  - Sprint drain now ramps up over time (no instant full-drain spike)
  - Camera toggle and dialogue zoom behavior
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
  - Session stash consume/remove/clear flows
- Dialogue:
  - Graph-based dialogue with choices and consequences
  - Street and restaurant NPC routes wired via IDs/catalog
  - Cinematic dialogue UI with choice cards
- Work flow:
  - Work door eligibility checks (time + energy)
  - Office session flow with pre/post dialogue and payout logic
  - Partial/fail outcomes for low-energy runs
  - Safe migration note: WorkDoorInteractable now supports optional serialized manager references while preserving existing GameManager fallback behavior
  - Gym door/session flow mirrors office contract (door gate -> session scene -> trainer dialogue -> progression apply -> return)
  - Gym progression uses hidden PlayerStats fields (training adaptation and fatigue debt) without adding new HUD bars
  - Sleep day-reset now clears gym daily lock and applies overnight fatigue recovery
- Intro and tutorial onboarding:
  - Intro cutscene sequence and completion events
  - Sequential tutorial panel after intro
  - Contextual tutorial toast queue (NPC/energy/food/work conditions)
  - Sequential/contextual tutorial and work reminder are suppressed while cutscene is active
  - Work reminder now appears only after cutscene and sequential tutorial have both finished
  - Sequential tutorial first-show flow now uses a single pending request path with short startup visual lock to prevent initial flicker
- Sleep loop and day transition:
  - Sleep is night-gated and uses a confirmation step before transition
  - Sleep transition uses fade plus clock time-skip animation
  - Sleep quality can be disturbed probabilistically based on recent work and food behavior
  - Wake message summarizes day change and relevant warnings
  - Next-day movement drain now supports hidden gym-based sustainability modifier (`movementDrainModifier`, no HUD exposure)
  - Late-wake consequence applies energy penalty + narrative warning only (no time cut, no period skip)

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
- Recommended quick test:
  1. Enter Play Mode in SampleScene
  2. Verify move/run/jump/step behavior
  3. Interact with food and test stash actions
  4. Open NPC dialogue and complete one dialogue branch
  5. Enter office flow and return to main scene
  6. Enter gym flow and confirm trainer pre/post dialogue plus daily lock behavior

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

Ringkasan (ID): Risiko utama sekarang ada di konsistensi UI dan minimnya automated test. Struktur UI lama dan baru masih campur.

## Next Steps (Short)

1. Unify UI architecture (migrate IMGUI food/stash into consistent canvas flow)
2. Balance food price and value curves (healthy vs less-healthy) for stable economy pacing
3. Add lightweight PlayMode smoke tests for movement, interaction, dialogue, sleep, and work loop
4. Continue tuning movement-energy pacing from playtest feedback (walk/run drain feel)
5. Remove or retire legacy scene objects that duplicate active systems
6. Normalize canvas/scaler conventions for all runtime-created UI

Ringkasan (ID): Fokus berikutnya adalah konsolidasi UI, menambah test otomatis ringan, dan merapikan debt teknis scene/UI.
