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
