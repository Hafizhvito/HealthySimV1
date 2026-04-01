# HEALTHSIM PROJECT CONTEXT

This file is the technical truth snapshot for current architecture, behavior, and risks.

## CORE ARCHITECTURE RULES (CURRENT)

- Player movement is Rigidbody-driven and resolved in FixedUpdate.
- Input is sampled in Update, then consumed by physics step.
- Jump is strict single-jump:
  - jump request is consumed once
  - jump force applies only when grounded
  - no coyote time and no jump buffer extensions
- Step assist is probe-based (lower/upper forward checks plus top validation).
- Movement energy drain is behavior-scaled:
  - walk/run/idle drain values are rebalanced for current short-session pacing
  - sprint uses ramp-up timing so drain does not spike instantly at run start
- Modal safety is source-keyed and centralized:
  - ModalStateManager controls global modal lock intent
  - PlayerController stores lock sources and suppresses movement/actions while locked
  - startup/scene-load paths include stale lock cleanup
- Onboarding overlays are cutscene-aware:
  - Tutorial and reminder surfaces must stay suppressed while intro cutscene is active
  - Work reminder can only appear after cutscene and sequential tutorial release
- Sleep flow is period-gated and behavior-aware:
  - Sleep interaction is night-only by default
  - Sleep uses confirm-before-transition UX
  - Recovery can be reduced by disturbed sleep chance based on recent behavior

Ringkasan (ID): Aturan inti gameplay sudah tegas, terutama untuk movement physics dan sistem lock input berbasis sumber.

## ACTIVE SYSTEMS (VERIFIED)

- Player and core runtime:
  - PlayerController handles locomotion, strict jump, step assist, and lock integration
  - PlayerStats manages hunger/fullness/hydration/mood/stress/energy values
  - PlayerStats movement energy drain now includes global scale and sprint ramp timing controls
  - TimeManager runs day periods (morning to night)
  - EnergySystem handles faint/recover flow
- Interaction and prompts:
  - UniversalInteractionController resolves nearest valid target with line-of-sight filtering
  - InteractableRegistry and IInteractable standardize registration and interaction calls
- Food and stash:
  - FoodCatalogProvider loads food data assets
  - FoodData supports explicit economy pricing and fallback effective-price calculation
  - FoodPickupInteractable and FoodChoiceMenuController drive consume-or-save flow
  - FoodChoiceMenuController and FoodPickupInteractable both enforce purchase checks and spend player money
  - Food menu shows live money, per-item price, and in-panel purchase status feedback
  - SessionFoodStash and stash UI controllers manage saved food lifecycle
- Dialogue:
  - DialogueGraphData and DialogueCatalogProvider supply graph/content
  - NpcDialogueMenuController drives cinematic dialogue UI
  - NpcDialogueInteractable and NpcRestaurantInteractable route NPC dialogue triggers
- Work and story flow:
  - WorkSessionManager orchestrates office session flow and HasWorkedToday state
  - WorkDoorInteractable gates entry by period/energy rules
  - WorkDoorInteractable supports optional serialized references for manager wiring, while retaining existing runtime fallback paths
  - StoryIntroManager and IntroCutsceneController expose completion events
  - SessionFlowController and StoryManager coordinate progression transitions
- UI and onboarding:
  - HUDAutoSetup builds runtime HUD and enforces 1920x1080 landscape scaler in its generated canvas
  - HUDManager updates stat bars and energy visuals
  - TutorialSequentialUI handles step-by-step onboarding panel
  - TutorialContextualUI handles queued one-time contextual hints
  - TutorialSequentialUI and TutorialContextualUI are gated by intro cutscene active state
  - WorkReminderUI suppresses while cutscene/sequential tutorial overlays are active
  - SleepBedInteractable now handles sleep transition UX (confirm, fade, clock skip, wake reminder)

Ringkasan (ID): Semua subsistem inti sudah tersambung, termasuk tutorial/reminder yang sadar state cutscene, serta alur tidur dan ekonomi makanan berharga.

## SWAP CONTRACT CHECKLIST (PHASE-1)

- Scope fase ini:
  - non-destructive hardening only
  - tidak menghapus fallback runtime
  - tidak mengubah behavior gameplay utama
- Player contract minimum:
  - tag `Player`
  - `PlayerController`, `Rigidbody`, `CapsuleCollider`, `Animator`, `UniversalInteractionController`
  - animator parameter: `Speed`, `IsGrounded`, `IsJumping`
- Office contract minimum:
  - object `PlayerSpawnPoint` ada di OfficeScene
  - boss office dapat di-resolve stabil (reference serialized atau tag `NPCBoss`)
- Interactable contract minimum:
  - implement `IInteractable`
  - collider valid + register/unregister ke `InteractableRegistry`
- UI contract minimum:
  - `HUD_Canvas` tersedia
  - rename child/hierarchy wajib diikuti update binding script

Validator workflow:

- Jalankan menu editor `HealthSim/Validate/Swap Contract (Warning Only)`
- Semua hasil fase ini warning-only agar aman untuk iterasi aset
- Fallback runtime yang aktif harus dianggap sinyal debt, bukan jalur final production

Ringkasan (ID): Kontrak swap sekarang didokumentasikan sebagai checklist operasional sehingga pergantian aset bisa bertahap dan aman.

## PROGRESS STATUS

- Done:
  - Movement strict jump and step traversal stabilization
  - Modal/input lock discipline through centralized manager
  - NPC dialogue routing and cinematic UI flow
  - Work session loop with result and payout branches
  - Safe migration micro-step: optional manager references added in work-door path without removing fallback behavior
  - Intro completion events and tutorial UI implementation
  - Reminder/tutorial overlap handling
  - Cutscene-gated onboarding/reminder display to prevent overlap noise
  - Sleep/day transition flow with wake warning logic and behavior-based disturbance chance
  - Food price system with money deduction on buy/consume/save actions
  - Placeholder healthy vs less-healthy food catalog generation for rapid restaurant iteration
  - Movement energy drain tuning pass (reduced immediate depletion feel during run)
  - Phase-1 swap safety hardening: warning-only contract validator + fallback telemetry in critical runtime paths
- In progress:
  - UI consistency unification between IMGUI-era menus and modern runtime canvas screens
  - Readability/layout polish standardization across generated UI
- Pending:
  - Automated PlayMode smoke/regression tests
  - Cleanup of legacy duplicate scene objects

Ringkasan (ID): Status proyek dominan selesai di fitur gameplay utama, tetapi standardisasi UI dan automated test masih berjalan.

## TECHNICAL RISKS AND DEBT

- Mixed UI paradigms increase maintenance risk (legacy IMGUI vs runtime canvas stack).
- Some legacy scene UI objects remain and may cause confusion during scene editing.
- Automated regression coverage is absent, so behavior drift risk is higher.
- Runtime canvas/scaler conventions are not yet fully uniform across all generated UI roots.
- Current economy balancing is functional but still early-stage (price-value curve tuning pending).
- Movement-energy pacing may still need iterative tuning after broader playtest sessions.

Ringkasan (ID): Debt teknis terbesar ada di konsistensi UI dan belum adanya test otomatis yang menjaga regresi.

## DATA SNAPSHOT

- Food assets detected: 25
- Dialogue assets detected: 30
- Primary scenes:
  - Assets/Scenes/SampleScene.unity
  - Assets/Scenes/OfficeScene.unity
  - Assets/Scenes/CinematicIntro.unity

Ringkasan (ID): Data konten dasar sudah tersedia dan cukup untuk menjalankan loop gameplay inti saat ini.

## NEXT FOCUS (PRIORITY ORDER)

1. Unify food/stash menu UX into the same runtime canvas design language used by dialogue/tutorial
2. Tune healthy vs less-healthy economy and behavior impact (price, value, sleep-risk interactions)
3. Continue movement-energy pacing calibration using playtest feedback (especially sprint feel)
4. Add PlayMode smoke tests for movement, interaction, dialogue, sleep transitions, and work completion paths
5. Remove or retire legacy duplicate scene objects
6. Standardize runtime canvas scaler/reference resolution across all auto-generated UI

Ringkasan (ID): Prioritas berikutnya adalah merapikan arsitektur UI lalu menambah test otomatis agar perubahan berikutnya lebih aman.

---

Last synchronized against current repository scripts and scene usage.
