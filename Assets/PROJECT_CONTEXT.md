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
- Modal safety is source-keyed and centralized:
  - ModalStateManager controls global modal lock intent
  - PlayerController stores lock sources and suppresses movement/actions while locked
  - startup/scene-load paths include stale lock cleanup

Ringkasan (ID): Aturan inti gameplay sudah tegas, terutama untuk movement physics dan sistem lock input berbasis sumber.

## ACTIVE SYSTEMS (VERIFIED)

- Player and core runtime:
  - PlayerController handles locomotion, strict jump, step assist, and lock integration
  - PlayerStats manages hunger/fullness/hydration/mood/stress/energy values
  - TimeManager runs day periods (morning to night)
  - EnergySystem handles faint/recover flow
- Interaction and prompts:
  - UniversalInteractionController resolves nearest valid target with line-of-sight filtering
  - InteractableRegistry and IInteractable standardize registration and interaction calls
- Food and stash:
  - FoodCatalogProvider loads food data assets
  - FoodPickupInteractable and FoodChoiceMenuController drive consume-or-save flow
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
  - WorkReminderUI suppresses reminders while sequential tutorial is active

Ringkasan (ID): Semua subsistem inti sudah tersambung, termasuk tutorial berlapis (sequential + contextual) dan pengingat kerja yang sadar state tutorial.

## PROGRESS STATUS

- Done:
  - Movement strict jump and step traversal stabilization
  - Modal/input lock discipline through centralized manager
  - NPC dialogue routing and cinematic UI flow
  - Work session loop with result and payout branches
  - Safe migration micro-step: optional manager references added in work-door path without removing fallback behavior
  - Intro completion events and tutorial UI implementation
  - Reminder/tutorial overlap handling
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

Ringkasan (ID): Debt teknis terbesar ada di konsistensi UI dan belum adanya test otomatis yang menjaga regresi.

## DATA SNAPSHOT

- Food assets detected: 13
- Dialogue assets detected: 30
- Primary scenes:
  - Assets/Scenes/SampleScene.unity
  - Assets/Scenes/OfficeScene.unity
  - Assets/Scenes/CinematicIntro.unity

Ringkasan (ID): Data konten dasar sudah tersedia dan cukup untuk menjalankan loop gameplay inti saat ini.

## NEXT FOCUS (PRIORITY ORDER)

1. Unify food/stash menu UX into the same runtime canvas design language used by dialogue/tutorial
2. Add PlayMode smoke tests for movement, interaction, dialogue open-close, and work completion paths
3. Remove or retire legacy duplicate scene objects
4. Standardize runtime canvas scaler/reference resolution across all auto-generated UI

Ringkasan (ID): Prioritas berikutnya adalah merapikan arsitektur UI lalu menambah test otomatis agar perubahan berikutnya lebih aman.

---

Last synchronized against current repository scripts and scene usage.
