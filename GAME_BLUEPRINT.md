# HealthySimV1 Game Blueprint (Living Document)

Status: Draft v1
Date: 2026-03-31
Owner: Core Team

## 1) Game Vision

HealthySimV1 is a short-session life simulation where the player balances daily health choices, social interaction, and work pressure in a city routine.

Ringkasan (ID): Pemain menjalani rutinitas harian kota sambil menyeimbangkan kesehatan, interaksi sosial, dan tuntutan kerja.

## 2) Player Fantasy

"I can survive and improve my day through smart small decisions, not perfect decisions."

Ringkasan (ID): Fantasi utama adalah tetap bertahan dan membaik lewat keputusan kecil yang tepat.

## 3) Design Pillars

1. Clear daily loop, low confusion.
2. Trade-off driven decisions (energy, mood, calories, time).
3. Short but meaningful interactions.
4. Recoverable mistakes (failure should teach, not hard-stop too early).

Ringkasan (ID): Pilar utama adalah loop jelas, keputusan berbasis trade-off, interaksi bermakna, dan kegagalan yang tetap bisa dipulihkan.

## 4) Target Experience and Session Length

- Platform focus: Android-ready architecture, PC editor for development.
- Session length target: 8-15 minutes per day cycle.
- Immediate player goal per session: finish one healthy and productive day.

Ringkasan (ID): Satu sesi ditargetkan singkat, sekitar 8-15 menit, dengan tujuan menutup hari secara sehat dan produktif.

## 5) Core Loop (Finalized for MVP)

1. Spawn into main city scene.
2. Explore and manage movement safely (move, sprint, jump, step traversal).
3. Interact with food options (consume now or stash for later).
4. Talk to NPCs and pick dialogue choices.
5. Decide whether to enter work session (time and energy gate).
6. Resolve work outcome, return to city, continue next period.

Ringkasan (ID): Loop inti: masuk kota, kelola sumber daya, interaksi makanan dan NPC, ambil keputusan kerja, lalu kembali lanjut periode hari.

## 6) Core Systems and Responsibilities

- Movement and camera: stable player control and readability.
- Stats model: energy, mood, calorie and related health signals.
- Time progression: morning, afternoon, evening, night.
- Interaction layer: nearest-target interaction with prompts and world bubbles.
- Food and stash: consume vs save decisions.
- Dialogue: branching social choices with consequences.
- Work session: gated entry and outcome branch.
- Onboarding: intro cutscene + sequential and contextual tutorial hints.

Ringkasan (ID): Sistem inti sudah mencakup kontrol pemain, stat, waktu, interaksi, makanan, dialog, kerja, dan onboarding.

## 7) Progression Model

### A. Moment-to-Moment

- Keep control responsive.
- Surface one clear next action through prompts/hints.

### B. One-Day Arc

- Build pressure through time and energy.
- Reward planning (stash, timing, dialogue choices).

### C. Multi-Day Arc (Planned)

- Introduce weekly trend outcomes (health, finance, trust).
- Unlock new interactions based on consistent behavior.

Ringkasan (ID): Progres dibagi tiga horizon: aksi singkat, siklus satu hari, dan rencana progres multi-hari.

## 8) Economy and Resource Rules (MVP)

- Energy is the primary pressure resource.
- Food decisions affect short-term survivability and longer-term balance.
- Work gives payout but consumes capability if done at low energy.
- Dialogue affects trust and can alter strategic options.

Ringkasan (ID): Energi jadi sumber tekanan utama; makanan, kerja, dan dialog saling memengaruhi hasil harian.

## 9) MVP Scope (Must Have)

1. Stable full loop from city start to work and return.
2. Food consume and stash use both functional.
3. NPC dialogue branch and close-restore control functional.
4. Work gate and transition reliable.
5. No permanent input lock and no blocker errors in normal play.

Ringkasan (ID): MVP wajib memastikan loop end-to-end stabil tanpa lock input permanen dan tanpa error pemblokir.

## 10) Out of Scope for MVP (Not Now)

1. Final art replacement and full visual polish.
2. Large narrative campaign.
3. Complex quest chains and inventory expansion.
4. Heavy optimization pass beyond blocker fixes.

Ringkasan (ID): Fokus MVP bukan final art atau fitur naratif besar, tetapi kestabilan loop utama.

## 11) Quality Gates

- Build/compile clean.
- Core loop smoke checklist passes repeatedly.
- No new critical runtime errors after each micro-step.
- Documentation stays synchronized with actual implementation.

Ringkasan (ID): Gate kualitas utama adalah compile bersih, smoke test lolos, dan dokumentasi selalu sinkron.

## 12) Current Technical Strategy

- Stability over cleanliness.
- Safe migration in micro-steps (1-2 files each step).
- Keep fallback paths active until equivalent replacement is proven safe.
- Add diagnostics and validators before structural rewrites.

Ringkasan (ID): Strategi teknis saat ini memprioritaskan stabilitas dan migrasi bertahap yang aman.

## 13) Top Product Risks

1. Undefined long-term progression can reduce retention.
2. Mixed UI paradigms can slow iteration speed.
3. Hardcoded scene contracts can break with content scaling.
4. Missing automated smoke tests can allow silent regressions.

Ringkasan (ID): Risiko produk terbesar ada pada progres jangka panjang, konsistensi UI, kontrak scene, dan minim otomasi tes.

## 14) Milestones (Suggested)

### M1 - Core Loop Lock

- Freeze gameplay flow and remove blocker instability.
- Add smoke tests and scene contract validator usage in team workflow.

### M2 - Content Layering

- Add more meaningful food/NPC/work variations without changing core architecture.

### M3 - Asset and Presentation Pass

- Replace temporary assets and polish UX after loop is stable.

Ringkasan (ID): Urutan milestone: kunci loop, tambah variasi konten, lalu finalisasi aset dan presentasi.

## 15) Decision Log Template

Use this template for every major decision:

- Date:
- Problem:
- Chosen option:
- Why:
- Risk:
- Rollback plan:

Ringkasan (ID): Setiap keputusan besar harus punya alasan, risiko, dan rencana rollback yang jelas.

## 16) Open Questions (To Resolve Next)

1. What is the target success state after 7 in-game days?
2. What is the minimum content count for NPC and food to feel replayable?
3. Which metric is primary for MVP success: completion rate, retention, or average session length?

Ringkasan (ID): Pertanyaan terbuka berikutnya fokus pada target 7 hari, skala konten minimum, dan metrik sukses MVP.
