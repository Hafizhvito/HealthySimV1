# HEALTHSIM PROJECT CONTEXT

This file is the technical truth snapshot for current architecture, behavior, and risks.

**Last synchronized:** repository state as of June 2026 — 157 scripts in `Assets/_Project/Scripts/`, 7 build scenes, Unity 6000.3.10f1.

---

## TEAM ONBOARDING OPERATIONS

- Required editor version: Unity `6000.3.10f1` (`ProjectSettings/ProjectVersion.txt`).
- Required startup flow:
  - clone repo → open via Unity Hub → wait compile idle
  - **full game:** open `Assets/Scenes/MainMenu/MainMenu.unity` → Play
  - **dev shortcut:** open `Assets/Scenes/SampleScene.unity` → Play
  - run smoke checklist before making changes
- Build settings scene order:
  1. `MainMenu` → 2. `InputMenu` → 3. `LoadingScreen` → 4. `SampleScene` → 5. `OfficeScene` → 6. `GymScene` → 7. `HouseInteriorScene`
- Daily teammate routine: pull → smoke → focused change → smoke → commit.
- Branching: `main`, `feature/<topic>`, `fix/<topic>`.
- Commit style: `feat|fix|docs|chore(scope): …`
- Common first-open risks: wrong Unity version, incomplete package restore, wrong scene, missing `.meta` after partial pull.

Ringkasan (ID): Onboarding = clone → compile → MainMenu Play → smoke test.

---

## CORE ARCHITECTURE RULES (CURRENT)

### Physics & movement

- Player movement is Rigidbody-driven, resolved in `FixedUpdate`.
- Input sampled in `Update`, consumed in physics step.
- Strict single-jump: no coyote time, no jump buffer.
- Step assist: probe-based lower/upper forward + top validation.
- Movement energy drain: walk/run/idle values scaled by `movementDrainScale` (default 0.30); sprint uses ramp-up over `runDrainRampSeconds`.
- Hidden sustainability modifier: `PlayerStats.movementDrainModifier` (clamped ~0.75–1.25) from gym adaptation/fatigue balance.

### Modal & input safety

- `ModalStateManager` (singleton on GameManager bootstrap) owns global modal lock intent.
- `PlayerController` stores lock sources; suppresses movement/actions while locked.
- Startup/scene-load paths force-reset stale locks (`SessionResetService`, `SleepBedInteractable`, `MobileInputController`).
- `PauseMenuManager` (DontDestroyOnLoad) registers modal key `Pause`.

### Time & sleep

- `TimeManager.totalGameDuration` default **270 s** (~4m30s real-time per in-game day).
- In-game clock **06:00–24:00** via `TimePercent`; 4 equal periods (morning/afternoon/evening/night).
- Non-gameplay scenes skip time advance: `MainMenu`, `InputMenu`, `LoadingScreen`.
- Sleep: night-gated, confirm UX, 22:00 soft reminder, 24:00 forced sleep + begadang penalty (energy 60%, drain 1.3× that day).
- Disturbed sleep probability from recent behavior; late-wake = energy penalty + narrative only (no schedule change).
- Day advance triggers: `DailyHealthEvaluator`, bazaar spawn, aging check, gym/work daily reset, nutrition counters reset.

### Health scoring & endings

- `PlayerStats.healthScoreThisPhase` (0–100) updated by `DailyHealthEvaluator` at sleep and other events via `RegisterHealthScore`.
- Phase commit on age transition; `GetAveragePhaseScore()` averages 3 phases for ending.
- Aging: Youth days 1–4, Adult 5–9, Senior 10–12; `ApplyPhaseModifiers()` by gender + stage.
- `EndingManager`: triggers on sleep day **12** → dr. Sri bridge dialogue → ending panel → recap → `CreditsController` → MainMenu.
- `LifestyleMortalityEvaluator`: optional premature death roll on sleep days 5–11 if health pattern is chronic poor; youth days 1–4 protected; hospital visit reduces chance.

### Onboarding overlays

- Cutscene-aware: tutorial/reminder suppressed while intro cutscene active.
- Work reminder only after cutscene + sequential tutorial both released.
- Sequential tutorial: single pending-request gate + startup visual lock against flicker.

### Bazaar

- `BazaarManager` (`Assets/Scripts/BazaarManager.cs`, DontDestroyOnLoad).
- After sleep day-advance: eligible on day interval (default every 5 days) + spawn chance roll (default 0.8).
- Discounted food menu; pool refreshed by `HealthySim/Food/Sync Catalog From Food Icons`.

Ringkasan (ID): Aturan inti tegas — physics jump, modal lock, 270s/hari, evaluasi kesehatan saat tidur, ending hari 12.

---

## ACTIVE SYSTEMS (VERIFIED)

### Session bootstrap (`SampleSceneBootstrap`)

Auto-created on SampleScene load if missing. Ensures manager graph including:

`ModalStateManager`, `TimeManager`, `PlayerStats`, `EnergySystem`, `WorkSessionManager`, `GymProgressionSystem`, `StoryIntroManager`, `SessionFlowController`, `EndingManager`, `HealthAlertPanelController`, `BazaarManager`, `FadeManager`, `SpawnPlayerManager`, food/stash/dialogue providers, `ProximityInteractButton`, etc.

Also loads `PlayerData` from PlayerPrefs and applies name/gender/height/weight to `PlayerStats` before intro.

### Player & character visuals

| Component | Role |
| --------- | ---- |
| `PlayerController` | Locomotion, locks, step assist, fatigue/health world indicators |
| `PlayerStats` | All stat bars, money, daily nutrition, phase scores, gym hidden state |
| `CharacterModelSwapper` | Swaps 6 body meshes by BMI + gender; Mixamo animator overrides |
| `PlayerLocomotionRig` | Animation Rigging foot/locomotion (optional editor setup) |
| `CameraSystem` | Cinemachine 3 TPP/FPP, dialogue zoom, startup cinematic |
| `EnergySystem` | Faint/recover at zero energy |
| `MobileInputController` | Joystick prefab, look zone, interact, modal-aware visibility |

Health warning flow:

1. `HealthAlertPanelController` — one-time modal notice when score &lt; 40 (once/day until acknowledged).
2. `PlayerController` — floating purple `!!!` above player after notice acknowledged.
3. `HospitalDoorInteractable` — consultation with dr. Sri; sets `VisitedHospitalToday`.

### Interaction

| Component | Role |
| --------- | ---- |
| `UniversalInteractionController` | Nearest target + LOS; E key + mobile triggers |
| `ProximityInteractButton` | HUD canvas button showing nearest action label |
| `InteractableRegistry` | Collider registration for targeting |
| `SceneLoaderInteractable` | Fade + spawn-ID scene loads (house interior, etc.) |
| `InvisibleInteractableVisual` | Hidden collider markers for designers |

Key interactables: food pickup, home station, work door, gym door, hospital door, sleep bed, bazaar, NPC dialogue, restaurant NPC, scene loaders.

### Food & economy

- `FoodCatalogProvider` loads 27 `FoodData` assets from `Assets/_Project/Data/Foods/`.
- Purchase deducts `PlayerStats` money; insufficient funds shown in food panel.
- Home station multiplier ~0.65; restaurant ~1.25; bazaar uses manager discount.
- `SessionFoodStash` + `FoodStashMenuController` (IMGUI) for saved meals.
- Runtime bootstrap enforces one restaurant pickup + one home station; removes duplicate pickups / legacy cube when configured.

### Dialogue

- 39 `DialogueGraphData` assets under `Assets/_Project/Data/Dialogues/` (incl. 10 `CityNpc/*`).
- `NpcDialogueMenuController` — cinematic UI, choice cards, modal lock.
- `DialogueCatalogProvider` — ID-based lookup.
- `BackstoryDialogueController` — post-intro panel; resolves `CharacterData` by BMI/gender/name.

### Work & gym

| System | Entry | Scene | Daily lock |
| ------ | ----- | ----- | ---------- |
| Work | `WorkDoorInteractable` 07:00–15:00 | `OfficeScene` | `HasWorkedToday` |
| Gym | `GymDoorInteractable` 06:00–22:00 | `GymScene` | `HasTrainedToday` |

Both use `FadeManager` → sub-scene → pre/post NPC dialogue → return via `SubSceneReturnHelper`.
`FadeManager` waits for `SpawnPlayerManager.OnSpawnComplete` before fade-in.
Gym: faint confirm dialog when energy &lt; 20%.

### UI & onboarding

| Component | Role |
| --------- | ---- |
| `HUDAutoSetup` / `HUDManager` | Stat bars, day/time, money, energy visuals |
| `TutorialSequentialUI` | Step panel after intro |
| `TutorialContextualUI` | Queued contextual hints |
| `WorkReminderUI` | Post-tutorial work nudge |
| `SleepBedInteractable` | Confirm, fade, clock skip, wake/aging modals |
| `AgingNotificationPanelBinder` | Phase transition narrative |
| `FaintNotificationController` | Faint / vehicle hit notices |
| `PauseMenuManager` | In-game pause + return to menu |
| `CreditsController` | Post-ending credits scroll |

### Main menu stack

| Scene | Key scripts |
| ----- | ----------- |
| MainMenu | `MainMenuManager`, `AudioManager`, credits panel |
| InputMenu | `InputFormManager`, `UIGenderSelector`, `InputStepper`, `StepProgressBar` |
| LoadingScreen | `LoadingManager`, `SceneLoader` |

Flow: MainMenu Play → `SceneLoader.LoadScene("InputMenu")` → form saves `PlayerData` → `SampleScene` via LoadingScreen.

### City ambience

- `CityTrafficManager` + `TrafficRoute` + `TrafficVehicleController` (7 vehicles, editor setup menus).
- `NpcWanderController` + `NpcVisualModelSlot` for city NPCs on NavMesh.

### Health evaluation (`DailyHealthEvaluator`)

Nightly at sleep — weighted deltas (clamped −8…+12 per day):

- **Diet:** junk penalty, calorie band, protein/fat thresholds, no-food penalty.
- **Sleep:** normal/disturbed, low-energy-at-sleep penalty.
- **Gym:** excellent/solid/strained + streak penalty after 3 days skipped.
- **Work:** full/partial/fail + streak penalty after 2 days skipped.

### External scripts (outside `_Project/Scripts`)

- `Assets/Scripts/BazaarManager.cs`
- `Assets/Scripts/BazaarInteractable.cs`

Ringkasan (ID): Semua subsistem inti tersambung — bootstrap, profil pemain, kesehatan, ending, kota, sesi kerja/gym.

---

## GYM + WAKE MICRO-STEP NOTES

### Day timeline

| Parameter | Value |
| --------- | ----- |
| Total day duration | 270 s |
| Period splits | 67.5 s × 4 |
| Ending day | 12 |
| Youth protection (mortality) | days 1–4 |

### Nutrient schema

`FoodData`: protein, fat, carbohydrate, sugar; `IsJunkFood` computed.

### Movement drain sustainability

Applied on sleep: better gym balance → ~0.85; overtraining → ~1.15; no gym → 1.0.

### Late wake chance (energy-only)

Signals: high sugar (+0.15), overwork (+0.20), fatigue debt (+0.15), low energy at sleep (+0.25); clamp 0–0.75; default energy penalty 15.

### Aging thresholds

| Stage | Days |
| ----- | ---- |
| Youth | 1–4 |
| Adult | 5–9 |
| Senior | 10–12 |

Evaluated on wake after `AdvanceToNextDayFromSleep()`.

Ringkasan (ID): Timeline 270s, evaluasi nutrisi & wake behavior-aware, HUD tetap minimal.

---

## SWAP CONTRACT CHECKLIST (PHASE-1)

- Non-destructive hardening only; runtime fallbacks retained with warning logs.
- **Player:** tag `Player`; `PlayerController`, `Rigidbody`, `CapsuleCollider`, `Animator`, `UniversalInteractionController`, `CharacterModelSwapper`; animator params `Speed`, `IsGrounded`, `IsJumping`.
- **Office:** `PlayerSpawnPoint`; boss via `NPCBoss` tag or serialized ref.
- **Interactable:** `IInteractable` + collider + `InteractableRegistry`.
- **UI:** `HUD_Canvas` present; binding-safe renames only.

Validators:

- `HealthSim/Validate/Swap Contract (Warning Only)`
- `HealthSim/Validate/Core Loop Scene Contract`

Ringkasan (ID): Kontrak swap = checklist operasional sebelum pergantian aset besar.

---

## PROGRESS STATUS

### Done

- Full main-menu → InputMenu → gameplay → ending → credits loop
- Player profile persistence (`PlayerData` / PlayerPrefs)
- Character body model swap (6 variants, Mixamo pipeline, editor menus)
- Movement, camera (Cinemachine 3), mobile controls, proximity interact button
- Modal/input lock discipline, pause menu
- Food economy (27 items), stash, home/restaurant/bazaar pricing
- NPC dialogue (39 graphs), city wandering NPCs, restaurant routes
- Work + gym sub-scenes with dialogue stages and daily locks
- Sleep/day transition, disturbed sleep, begadang, aging notifications
- Daily health evaluator, phase/gender modifiers, health alert + hospital visit
- Premature mortality roll + day-12 ending manager + credits
- City traffic system (7 vehicles)
- House interior scene loading
- Intro cutscene + backstory + tutorial gating
- Phase-1 swap validators + bootstrap hardening
- Scene transition fix: fade waits for spawn complete
- Faint notification panel, gym faint confirm, vehicle hit handling

### In progress

- UI consistency: IMGUI food/stash vs runtime canvas dialogue/tutorial/ending
- Economy/health evaluator tuning from playtest feedback
- Canvas/scaler normalization across auto-generated UI roots

### Pending

- Automated PlayMode smoke/regression tests
- Cleanup of legacy duplicate scene UI objects
- Optional: migrate `BazaarManager` into `_Project/Scripts`

Ringkasan (ID): Gameplay loop dominan selesai; debt utama di UI parity dan automated test.

---

## TECHNICAL RISKS AND DEBT

- Mixed UI paradigms (IMGUI food/stash vs canvas dialogue/HUD/ending).
- Legacy Hierarchy objects may duplicate active systems.
- No regression tests — behavior drift risk on refactors.
- Inconsistent runtime canvas reference resolution (some roots still portrait-style).
- `BazaarManager` location split (`Assets/Scripts/` vs `_Project`).
- `MainMenuManager.gameSceneName` C# default is `GameScene` but scene serializes `InputMenu` — do not rely on default alone.
- `TimeManager` inline comment says "3m50s" but serialized value is 270s (4m30s) — trust the float, not the comment.

Ringkasan (ID): Risiko terbesar = UI inconsistency + no automated tests.

---

## DATA SNAPSHOT

| Asset type | Count | Location |
| ---------- | ----- | -------- |
| FoodData | 27 | `Assets/_Project/Data/Foods/` |
| DialogueGraphData | 39 | `Assets/_Project/Data/Dialogues/` |
| CharacterData | 6 | `Assets/_Project/Data/Characters/Character Temp/` |
| Runtime C# scripts | 157 | `Assets/_Project/Scripts/` |

### Primary scenes

- `Assets/Scenes/MainMenu/MainMenu.unity`
- `Assets/Scenes/MainMenu/InputMenu.unity`
- `Assets/Scenes/MainMenu/LoadingScreen.unity`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/OfficeScene.unity`
- `Assets/Scenes/GymScene.unity`
- `Assets/Scenes/HouseScene/HouseInteriorScene.unity`

### Character variants

| Asset file | Name | Gender | BMI Type |
| ---------- | ---- | ------ | -------- |
| Char_Male_Kurus | Rafi | Laki-laki | Underweight |
| Char_Male_Normal | Dimas | Laki-laki | Normal |
| Char_Male_Gemuk | Bagas | Laki-laki | Overweight |
| Char_Female_Kurus | Nisa | Perempuan | Underweight |
| Char_Female_Normal | Ayu | Perempuan | Normal |
| Char_Female_Gemuk | Dina | Perempuan | Overweight |

Backstory via `CharacterData.backstoryText`. Runtime variant from InputMenu BMI/gender → `CharacterModelSwapper.ResolveBodyBuild()`. Editor backstory test: `BackstoryDialogueController.preferredCharacterDataAssetName`.

---

## SCRIPT FOLDER MAP (`Assets/_Project/Scripts/`)

| Folder | Purpose |
| ------ | ------- |
| `Core/` | PlayerStats, TimeManager, EnergySystem, FoodData, EndingManager, FacilityHours |
| `Session/` | Bootstrap, work/gym session controllers, spawn, fade helpers |
| `Interaction/` | Interactables, food/dialogue menus, registry, sleep bed |
| `UI/` | HUD, tutorial, modal, mobile input, health/aging/ending panels |
| `Health/` | DailyHealthEvaluator, LifestyleMortalityEvaluator, PhaseReviewBuilder |
| `Gym/` | Gym door, session, trainer dialogue, progression |
| `Story/` | Intro, backstory |
| `Player/` | CharacterModelSwapper, locomotion rig |
| `NPC/` | NpcWanderController |
| `Traffic/` | CityTrafficManager, routes, vehicles |
| `Tracking/` | PlayerActionTracker |
| `MainMenu Script/` | Menu managers, loading, UI transitions, PlayerData |
| `Manager/` | SpawnPlayerManager, PauseMenuManager |
| `Editor/` | Scene setup, validators, asset generators (~30 menu items) |
| `Cutscene/` | IntroCutsceneController |
| `CameraSystem.cs` | Root-level camera controller |

---

## NEXT FOCUS (PRIORITY ORDER)

1. Unify food/stash menu UX into runtime canvas design language
2. Tune economy + `DailyHealthEvaluator` weights from structured playtests
3. Add PlayMode smoke tests (profile load, interact, sleep, work, ending path)
4. Normalize canvas scaler across all auto-generated UI
5. Retire legacy duplicate scene objects; consider consolidating `BazaarManager` path

Ringkasan (ID): Prioritas = UI parity, test otomatis, tuning balance.

---

## RELATED DOCS

- `README.md` — onboarding, smoke tests, feature summary
- `Assets/_Project/Dokumentasi_HealthSim_Skripsi.md` — exhaustive TA reference (constants, flows, dependency map)
- `docs/Bab3-Implementasi-Sistem.md` — thesis implementation excerpts

---

Last synchronized against current repository scripts, scenes, and build settings.
