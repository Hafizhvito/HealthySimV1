# HEALTHSIM PROJECT CONTEXT

This file is the technical truth snapshot for current architecture, behavior, and risks.

## TEAM ONBOARDING OPERATIONS

- Required editor version:
  - Unity `6000.3.10f1` (see `ProjectSettings/ProjectVersion.txt`)
- Required startup flow:
  - clone repo
  - open project via Unity Hub
  - wait for package restore and compile idle
  - open `Assets/Scenes/SampleScene.unity`
  - run smoke checklist before making changes
- Daily teammate routine:
  - pull latest branch first
  - run quick smoke before and after changes
  - keep commits focused and small
- Branching baseline:
  - `main` for stable baseline
  - `feature/<topic>` and `fix/<topic>` for active work
- Commit style baseline:
  - `feat(scope): ...`
  - `fix(scope): ...`
  - `docs(scope): ...`
  - `chore(scope): ...`
- Common first-open risks:
  - wrong Unity version
  - package restore not finished
  - opening wrong scene instead of `SampleScene`
  - missing `.meta` files after partial pull/merge

Ringkasan (ID): Onboarding tim disederhanakan ke alur clone -> open -> compile -> smoke test agar teammate baru bisa langsung jalan tanpa bingung.

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
  - Sequential tutorial first-show uses a single pending-request gate plus short startup visual lock to avoid startup flicker
- Sleep flow is period-gated and behavior-aware:
  - TimeManager day duration remains configurable via `totalGameDuration` (default 230 seconds) with proportional quarter splits across morning/afternoon/evening/night
  - CurrentHour is mapped to 6.00-24.00 using TimePercent for scalable hour display and checks
  - Sleep interaction is night-only by default
  - Sleep uses confirm-before-transition UX
  - Soft sleep reminder appears at 22.00 and re-prompts if dismissed
  - Forced sleep triggers at 24.00 with a short fade + message
  - Begadang penalty applies when forced sleep occurs (energy starts at 60% and movement drain is 1.3x for that day)
  - Recovery can be reduced by disturbed sleep chance based on recent behavior
  - Aging progression updates on wake after day increment, with stage transitions at day 5 and day 10
  - Next-day movement energy sustainability is applied as hidden `movementDrainModifier` on PlayerStats (clamped 0.75-1.25)
  - Late-wake rule applies energy penalty + narrative warning only, without changing TimeManager schedule
- Bazaar event system:
  - Bazaar eligibility checks run after sleep day-advance
  - Eligible only on day interval (5/10/15...) and must pass spawn chance roll (default 0.8)
  - Bazaar object is a scene prefab (inactive by default, toggled active on eligible days)
  - Bazaar menu uses discounted FoodData and selects 10 foods + 3 drinks without duplicates

Ringkasan (ID): Aturan inti gameplay sudah tegas, terutama untuk movement physics dan sistem lock input berbasis sumber.

## ACTIVE SYSTEMS (VERIFIED)

- Player and core runtime:
  - PlayerController handles locomotion, strict jump, step assist, and lock integration
  - FPP camera uses direct input without spin (Minecraft-style feel)
  - PlayerStats manages hunger/fullness/hydration/mood/stress/energy values
  - PlayerStats movement energy drain now includes global scale and sprint ramp timing controls
  - TimeManager runs day periods (morning to night)
  - TimeManager exposes CurrentHour for explicit hour-based gates (6.00-24.00)
  - EnergySystem handles faint/recover flow
  - MobileInputController binds joystick from prefab under HUD_Canvas (MoveJoystickOuter, MoveJoystickOuterBorder, MoveJoystickKnob)
  - MobileInputController auto-assigns runtime circle sprite to joystick images
- Interaction and prompts:
  - UniversalInteractionController resolves nearest valid target with line-of-sight filtering
  - World interaction bubbles are tappable on mobile and show '?' as the cue; keyboard E remains on desktop
  - InteractableRegistry and IInteractable standardize registration and interaction calls
- Food and stash:
  - FoodCatalogProvider loads food data assets
  - FoodData supports explicit economy pricing and fallback effective-price calculation
  - FoodPickupInteractable and FoodChoiceMenuController drive consume-or-save flow
  - FoodChoiceMenuController and FoodPickupInteractable both enforce purchase checks and spend player money
  - Food menu shows live money, per-item price, and in-panel purchase status feedback
  - SessionFoodStash and stash UI controllers manage saved food lifecycle
  - Runtime bootstrap enforces one restaurant food pickup and one home station interaction point in `SampleScene`
  - Extra `FoodPickupInteractable` duplicates are removed at runtime, and legacy `Interactable_FoodCube` is removed when full restaurant + home setup already exists
  - Placeholder interactables now have prefab assets for designer-friendly swapping
- Dialogue:
  - DialogueGraphData and DialogueCatalogProvider supply graph/content
  - NpcDialogueMenuController drives cinematic dialogue UI
  - NpcDialogueInteractable and NpcRestaurantInteractable route NPC dialogue triggers
- Work and story flow:
  - WorkSessionManager orchestrates office session flow and HasWorkedToday state
  - WorkDoorInteractable gates entry by hour 07.00-15.00 plus energy rules
  - WorkDoorInteractable supports optional serialized references for manager wiring, while retaining existing runtime fallback paths
  - GymProgressionSystem orchestrates gym session progression and HasTrainedToday state
  - GymDoorInteractable gates entry by hour 06.00-22.00 plus energy rules and pending gym session setup
  - GymSessionController runs trainer pre/post dialogue and clock-based session animation before applying progression
  - StoryIntroManager and IntroCutsceneController expose completion events
  - BackstoryDialogueController shows a one-time character backstory dialogue box immediately after intro cutscene
    - BackstoryDialogueController now prefers PlayerStats name (wired from PlayerData before intro)
  - SessionFlowController and StoryManager coordinate progression transitions
- UI and onboarding:
  - HUDAutoSetup builds runtime HUD and enforces 1920x1080 landscape scaler in its generated canvas
  - HUDManager updates stat bars and energy visuals
  - MobileInputController uses prefab joystick under HUD_Canvas plus runtime-built look swipe zone and perspective toggle
  - TutorialSequentialUI handles step-by-step onboarding panel
  - TutorialContextualUI handles queued one-time contextual hints
  - TutorialSequentialUI and TutorialContextualUI are gated by intro cutscene active state
  - TutorialSequentialUI now applies startup visual lock and early panel force-hide to avoid millisecond flash before cutscene state settles
  - WorkReminderUI suppresses while cutscene/sequential tutorial overlays are active
  - SleepBedInteractable now handles sleep transition UX (confirm, fade, clock skip, wake reminder)
  - SleepBedInteractable now triggers both work and gym day-reset hooks after sleep transition
  - SleepBedInteractable now computes lateWakeChance from behavior signals and applies wake energy penalty without time cut
  - SleepBedInteractable now triggers aging stage transition checks on wake and uses modal-keyed aging notifications (`aging_notification`)

Ringkasan (ID): Semua subsistem inti sudah tersambung, termasuk tutorial/reminder yang sadar state cutscene, serta alur tidur dan ekonomi makanan berharga.

## GYM + WAKE MICRO-STEP NOTES

- Day timeline rescale:

  - Total day duration: 230 seconds (3m50s)
  - Proportional period thresholds: 57.5s / 115s / 172.5s / 230s
  - Balancing note: shorter timeline increases decision pressure, so stamina/economy tuning should be validated against this faster cadence

- Nutrient schema:
  - FoodData now exposes protein, fat, carbohydrate, and sugar for downstream logic and UI text usage.
- Movement drain sustainability:
  - `movementDrainModifier` is stored on PlayerStats and applied inside movement energy drain calculation.
  - Clamp range: 0.75 to 1.25 (hidden gameplay modifier, not shown as HUD bar).
  - Applied during sleep-to-morning transition based on gym adaptation/fatigue balance:
    - better balance trends toward 0.85
    - overtraining trends toward 1.15
    - no gym training stays at 1.0
- Late wake chance (energy-only consequence):
  - Signal A (high sugar pattern): +0.15
  - Signal B (overwork pattern): +0.20
  - Signal C (high fatigue debt): +0.15
  - Signal D (low energy at sleep): +0.25
  - Final clamp: 0.00 to 0.75
  - Penalty: configurable wake energy deduction (default 15), plus narrative warning line.
  - Explicitly no time cut, no period skip, no morning clock offset.
- Aging progression thresholds:
  - Day 1-4: `Youth`
  - Day 5-9: `Adult`
  - Day 10+: `Senior`
  - Trigger point: evaluated on wake immediately after `AdvanceToNextDayFromSleep()`
- Step skip audit:
  - No implementation step skipped in this pass (all mandatory files were found).

Ringkasan (ID): Update mikro ini menambah sinyal nutrisi dan konsekuensi wake berbasis perilaku, tapi tetap menjaga HUD sederhana dan tidak mengubah jadwal waktu pagi.

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
  - Gym activity loop with mirrored office flow contract and trainer dialogue stages
  - PlayerStats hidden gym progression state added (training adaptation and fatigue debt)
  - Sleep day-reset integration for gym daily lock reset and overnight fatigue recovery
  - Safe migration micro-step: optional manager references added in work-door path without removing fallback behavior
  - Intro completion events and tutorial UI implementation
  - Reminder/tutorial overlap handling
  - Cutscene-gated onboarding/reminder display to prevent overlap noise
  - Sequential tutorial startup race cleanup: single-request trigger path plus startup visual lock/force-hide guard
  - Sleep/day transition flow with wake warning logic and behavior-based disturbance chance
  - Food price system with money deduction on buy/consume/save actions
  - Placeholder healthy vs less-healthy food catalog generation for rapid restaurant iteration
  - Food placeholder cleanup hardening: duplicate food pickup removal and legacy cube auto-cleanup when scene already has restaurant + home setup
  - Mobile touch control baseline added (movement joystick, look joystick, interact button, modal/cutscene-aware visibility)
  - Mobile joystick migrated to prefab under HUD_Canvas
  - Movement energy drain tuning pass (reduced immediate depletion feel during run)
  - Phase-1 swap safety hardening: warning-only contract validator + fallback telemetry in critical runtime paths
  - Gameplay modifier per fase & gender (PlayerStats: Gender enum, ApplyPhaseModifiers(), PhaseModifierData struct, 6-variant switch expression)
  - AgingNotificationPanel added to HUD_Canvas Hierarchy via Editor script
  - SpawnPoint moved to (-71.42, 0.5, -50) in front of the blue house
  - CameraSwitcher warning resolved in SampleScene
  - 6 CharacterData assets created (Char_Male/Female_Kurus/Normal/Gemuk)
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

- Food assets detected: 43
- Dialogue assets detected: 30
- Primary scenes:
  - Assets/Scenes/SampleScene.unity
  - Assets/Scenes/OfficeScene.unity
  - Assets/Scenes/CinematicIntro.unity

## Character Variants

6 CharacterData ScriptableObject assets in Assets/\_Project/Data/Characters/

| Asset file         | Name  | Gender    | BMI Type    |
| ------------------ | ----- | --------- | ----------- |
| Char_Male_Kurus    | Rafi  | Laki-laki | Underweight |
| Char_Male_Normal   | Dimas | Laki-laki | Normal      |
| Char_Male_Gemuk    | Bagas | Laki-laki | Overweight  |
| Char_Female_Kurus  | Nisa  | Perempuan | Underweight |
| Char_Female_Normal | Ayu   | Perempuan | Normal      |
| Char_Female_Gemuk  | Dina  | Perempuan | Overweight  |

Backstory diisi via backstoryText field (AppendBackstory() prioritizes this field).
Active variant determined by CharacterSelectionData at runtime (main menu — not yet built).
Temporary: BackstoryDialogueController resolves CharacterData by name
"PlayerCharacter" fallback — change preferredCharacterDataAssetName in Inspector
to test specific variant.

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
