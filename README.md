# HealthySimV1

HealthySimV1 is a Unity 6 URP life-simulation prototype: create a player profile, explore a city over a 12-day loop, manage food/energy/mood, work, train, visit the hospital, sleep, and reach one of several endings.

## Current Project Status

- Engine: Unity **6000.3.10f1**
- Render pipeline: **URP 17.x**
- Runtime scripts: **157** C# files under `Assets/_Project/Scripts/` (+ `BazaarManager` / `BazaarInteractable` in `Assets/Scripts/`)
- Entry flow (build order): **MainMenu → InputMenu → LoadingScreen → SampleScene**
- Gameplay sub-scenes: **OfficeScene**, **GymScene**, **HouseInteriorScene**
- Playable end-to-end from main menu through day-12 ending / credits
- QA: manual smoke testing only (no automated PlayMode tests yet)
- Target platforms: desktop + mobile (touch joystick, proximity interact button, safe-area aware menus)

Ringkasan (ID): Proyek sudah bisa dimainkan end-to-end dari main menu sampai ending/credits, dengan loop harian lengkap. QA masih manual.

## Scene Map

| Scene | Path | Role |
| ----- | ---- | ---- |
| Main Menu | `Assets/Scenes/MainMenu/MainMenu.unity` | Play, Options, Credits |
| Character Input | `Assets/Scenes/MainMenu/InputMenu.unity` | Nama, gender, tinggi, berat, ringkasan BMI |
| Loading | `Assets/Scenes/MainMenu/LoadingScreen.unity` | Async load + progress bar |
| City (main) | `Assets/Scenes/SampleScene.unity` | Loop harian, NPC, makanan, tidur |
| Office | `Assets/Scenes/OfficeScene.unity` | Sesi kerja |
| Gym | `Assets/Scenes/GymScene.unity` | Sesi latihan + dialog pelatih |
| House Interior | `Assets/Scenes/HouseScene/HouseInteriorScene.unity` | Interior rumah (via `SceneLoaderInteractable`) |

## Team Onboarding

### Teammate Quick Start

1. Install Unity Hub and Unity Editor `6000.3.10f1`.
2. Clone this repository.
3. Open the project folder in Unity Hub.
4. Wait for package import and script compilation to finish.
5. Open `Assets/Scenes/MainMenu/MainMenu.unity` and press **Play** for the full flow.
   - For a quick gameplay-only check, open `Assets/Scenes/SampleScene.unity` directly.
6. Run the smoke test checklist below before making changes.

Catatan (ID): Alur resmi dimulai dari MainMenu. SampleScene tetap bisa dipakai untuk debug cepat tanpa profil pemain.

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

5. Press Play from **MainMenu** (recommended) or **SampleScene** (dev shortcut).

Catatan (ID): Import pertama kali memang lama — tunggu compile idle sebelum Play.

### Daily Workflow (Teammates)

1. `git pull` on your branch.
2. Open Unity and wait for compile to finish.
3. Run smoke test before coding.
4. Make changes in a focused scope (`Assets/_Project/Scripts/` preferred).
5. Re-run smoke test after changes.
6. Commit with clear message.
7. Push branch and open PR.

### Minimal Branching Recommendation

- `main`: stable baseline
- `feature/<short-topic>` for new work
- `fix/<short-topic>` for bug fixes

Prefer short-lived branches and small PRs.

### Commit Convention

Use Conventional Commit style:

- `feat(scope): ...`
- `fix(scope): ...`
- `docs(scope): ...`
- `chore(scope): ...`

### Smoke Test Checklist (Run After Open and Before PR)

**Full flow (recommended):**

1. Play from `MainMenu.unity`.
2. Complete InputMenu (nama, gender, tinggi, berat) → loading → SampleScene.
3. Verify intro cutscene + backstory + sequential tutorial.
4. Walk/run/jump; toggle camera **F** / **V**.
5. Buy/eat or stash food; confirm money deduction and nutrient summary (P/L/G).
6. Finish one NPC dialogue branch.
7. Enter office (`WorkDoorInteractable`) and return.
8. Enter gym, confirm trainer dialogue + daily lock (once per day).
9. When health score is low, confirm `!!!` indicator and hospital visit (dr. Sri).
10. Sleep at night; confirm day advance, daily evaluation, and wake message.
11. Check Console for new errors.

**SampleScene shortcut:**

1. Open `SampleScene.unity` → Play.
2. Run steps 3–11 above (profile defaults apply via `PlayerData` / bootstrap).

Catatan (ID): Checklist ini cukup untuk validasi cepat bahwa loop utama tetap aman.

### Common Errors and Quick Fixes

1. **Unity version mismatch** — use `6000.3.10f1` from `ProjectSettings/ProjectVersion.txt`.
2. **Package errors on first open** — open Package Manager, wait for restore, reimport if needed.
3. **Long compile delay** — wait until compile is idle; avoid editing while importing.
4. **Wrong scene opens** — use build-index scenes listed above.
5. **Missing references after pull** — ensure `.meta` files are present, pull again, reopen Unity.
6. **Broken player camera/rig** — run `HealthySim/Fix Player Camera And Rig Gizmos`.
7. **Missing body models** — run `HealthySim/Setup Player Body Models`.

## Implemented Features (Verified)

### Main menu & player profile

- Main menu with Play, Options (SFX/Music volume), Credits, Quit.
- InputMenu multi-step form: name, gender, height, weight, BMI summary with color-coded category.
- `PlayerData` persisted via `PlayerPrefs`; wired into `PlayerStats` on SampleScene bootstrap.
- Async loading screen (`LoadingManager` + `SceneLoader`).

### Player movement, camera & character models

- Rigidbody movement in `FixedUpdate`, strict single jump, step assist.
- Movement energy drain rebalanced (`movementDrainScale`, sprint ramp-up).
- `CameraSystem` (Cinemachine 3): TPP/FPP toggle, dialogue zoom, startup cinematic, yaw recenter.
- `CharacterModelSwapper`: 6 body variants (male/female × kurus/ideal/overweight) from BMI + gender.
- Mixamo animation pipeline + editor menus under `HealthySim/Body Models/…`.

### Input, modal safety & pause

- Source-counted input locks in `PlayerController`.
- Central modal authority via `ModalStateManager`.
- `PauseMenuManager` (DontDestroyOnLoad): pause, options, return to MainMenu.
- Mobile: prefab joystick under `HUD_Canvas`, look swipe, interact button.
- `ProximityInteractButton`: canvas action button when near an interactable (mobile-friendly).

### Interaction layer

- `UniversalInteractionController` + line-of-sight targeting.
- World interaction bubbles (`?` cue) + keyboard **E** on desktop.
- `InteractableRegistry` + `IInteractable` contract.
- `SceneLoaderInteractable` for house interior and other scene transitions via `FadeManager`.

### Food, stash & bazaar

- 27 `FoodData` assets (healthy / less-healthy) with price, protein, fat, sugar.
- Buy/eat or stash flow; home station (discounted prep) vs restaurant (markup).
- Daily nutrition tracking; `IsJunkFood` computed property.
- Session stash consume/remove/clear.
- `BazaarManager` (`Assets/Scripts/`): spawn on day 5/10/15… with configurable chance/discount.

### Dialogue & NPCs

- 39 dialogue graph assets (story, react, period, boss, restaurant, 10 city NPCs).
- Cinematic dialogue UI with choice of choice cards.
- City wandering NPCs (`NpcWanderController` + NavMesh) and restaurant NPC routes.
- Editor setup: `HealthySim/City NPCs/…`.

### Work & gym sessions

- Work door: hour **07:00–15:00** + energy rules → `OfficeScene` → boss pre/post dialogue → payout.
- Gym door: hour **06:00–22:00** + energy rules → `GymScene` → trainer dialogue → progression.
- Gym faint warning when energy &lt; 20% (confirm/cancel).
- Daily lock reset on sleep; hidden `movementDrainModifier` from gym adaptation/fatigue.

### Health, hospital & ending

- `DailyHealthEvaluator`: nightly diet/sleep/gym/work score delta at sleep.
- `HealthAlertPanelController`: one-time notice before `!!!` world indicator (threshold 40).
- `HospitalDoorInteractable` + `DoctorSriDialogueController`; `VisitedHospitalToday` flag.
- `LifestyleMortalityEvaluator`: soft premature-death roll on sleep days 5–11 (youth protected).
- `EndingManager`: day-12 ending (Good/Neutral/Bad) + recap → `CreditsController` → MainMenu.
- Aging stages: Youth (1–4), Adult (5–9), Senior (10–12) with phase modifiers by gender.

### Sleep loop & day transition

- One in-game day ≈ **270 seconds** real-time (06:00–24:00, 4 periods).
- Sleep confirm, 22:00 reminder, forced sleep at 24:00, begadang penalty.
- Disturbed sleep from recent behavior; late-wake energy penalty (no time skip).
- Bazaar spawn check after day advance.

### Intro, tutorial & onboarding

- Intro aerial cutscene + one-time backstory panel (`BackstoryDialogueController`).
- Sequential tutorial + contextual toast queue; cutscene-gated reminders.
- Work reminder after cutscene + sequential tutorial complete.

### City ambience

- `CityTrafficManager` + `TrafficRoute` + 7 vehicle loop (editor: `HealthySim/Traffic/…`).

## Character Variants

Six `CharacterData` assets under `Assets/_Project/Data/Characters/Character Temp/`:

| Asset file         | Name  | Gender    | BMI Type    |
| ------------------ | ----- | --------- | ----------- |
| Char_Male_Kurus    | Rafi  | Laki-laki | Underweight |
| Char_Male_Normal   | Dimas | Laki-laki | Normal      |
| Char_Male_Gemuk    | Bagas | Laki-laki | Overweight  |
| Char_Female_Kurus  | Nisa  | Perempuan | Underweight |
| Char_Female_Normal | Ayu   | Perempuan | Normal      |
| Char_Female_Gemuk  | Dina  | Perempuan | Overweight  |

Active variant is driven by **InputMenu profile** (gender + BMI from height/weight) and resolved at runtime by `CharacterModelSwapper` + backstory text from matching `CharacterData`. For editor-only backstory testing, change `preferredCharacterDataAssetName` on `BackstoryDialogueController`.

## Quick Usage Notes

### Controls

| Action | Desktop | Mobile |
| ------ | ------- | ------ |
| Move | W A S D | Left joystick |
| Sprint | Left Shift | — |
| Jump | Space | — |
| Interact | E | Proximity button / top-right interact |
| Camera mode | F or V | Perspective toggle |
| Pause | Pause button (HUD) | Pause button |
| Cursor cancel | Escape | — |
| Debug lock reset | F8 | — |

Mobile test in Editor: `MobileInputController.forceMobileUI = true` (default).

### Recommended quick test

1. Play from MainMenu → complete InputMenu.
2. Verify movement, food purchase/stash, one NPC dialogue.
3. Complete one work session and one gym session.
4. Visit hospital when health score is low.
5. Sleep once and confirm day-2 bootstrap (time, stats, gym lock reset).

## Asset Swap Contract (Phase 1 — Safe)

Tujuan: memudahkan penggantian aset sementara ke aset final tanpa memutus alur gameplay.

- **Player contract:** tag `Player`; minimum components `PlayerController`, `Rigidbody`, `CapsuleCollider`, `Animator`, `UniversalInteractionController`, `CharacterModelSwapper`; animator params `Speed`, `IsGrounded`, `IsJumping`.
- **Office contract:** `PlayerSpawnPoint` in OfficeScene; boss resolvable via tag `NPCBoss` or serialized reference.
- **Interactable contract:** implement `IInteractable`; active collider registered in `InteractableRegistry`.
- **UI contract:** `HUD_Canvas` at runtime; do not rename bound children without updating scripts.

Validator: `HealthSim/Validate/Swap Contract (Warning Only)` or `HealthSim/Validate/Core Loop Scene Contract`.

## Editor Menus (Common)

| Menu prefix | Examples |
| ----------- | -------- |
| `HealthySim/` | Body models, camera fix, city NPCs, traffic, pause layout, narrative panels |
| `HealthSim/` | Gym/work scene setup, home food station, UI panels, dialogue generator, validators |
| `Tools/HealthySim/` | Food catalog generator, missing-script repair, StorySimV2 setup |

## Related Documentation

- `Assets/PROJECT_CONTEXT.md` — technical architecture snapshot (keep in sync with code).
- `Assets/_Project/Dokumentasi_HealthSim_Skripsi.md` — full TA/skripsi technical reference.
- `docs/Bab3-Implementasi-Sistem.md` — thesis code excerpts (Bab 3).

## Known Limitations and Risks

- No automated PlayMode/regression tests yet.
- Food/stash still uses IMGUI-era menus; dialogue/tutorial/ending use runtime canvas.
- Legacy scene objects (e.g. old dialogue panel duplicates) may confuse Hierarchy editing.
- Runtime canvas scaler not fully unified across all auto-generated UI roots.
- TMP glyph fallback warnings for some Unicode symbols in HUD/tutorial labels.
- `BazaarManager` lives outside `_Project/Scripts` — mind the split when refactoring.
- Main menu `gameSceneName` must stay `InputMenu` in scene Inspector (not the C# default `GameScene`).

## Next Steps (Short)

1. Unify food/stash UI into the same canvas design language as dialogue/tutorial.
2. Balance food price/value curves and daily evaluator weights from playtest data.
3. Add lightweight PlayMode smoke tests (movement, interact, sleep, work, ending).
4. Continue movement-energy pacing calibration.
5. Retire legacy duplicate scene objects and normalize canvas/scaler conventions.

Ringkasan (ID): Fokus berikutnya — konsolidasi UI, test otomatis ringan, dan tuning ekonomi/kesehatan dari playtest.
