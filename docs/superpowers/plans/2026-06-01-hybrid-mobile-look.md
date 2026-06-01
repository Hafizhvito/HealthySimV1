# Hybrid Mobile Look Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore working TPP swipe camera via Cinemachine + UI swipe zone; keep router for move-only; fix misleading debug.

**Architecture:** Left-half touch → `MobileTouchRouter` → move. Right-half → `MobileSwipeLookZone` (EventSystem) → Cinemachine axis controller. No manual `AddLookInput` on TPP mobile.

**Tech Stack:** Unity 6, Cinemachine 3, Input System, uGUI EventSystem.

---

### Task 1: Re-enable Cinemachine TPP input on mobile

**Files:**
- Modify: `Assets/_Project/Scripts/CameraSystem.cs`

- [ ] Remove or bypass `ShouldUseManualMobileLook()` so `SetTPP()` always enables `tppInputController`.
- [ ] Confirm `AddLookInput` TPP path unchanged (used for FPP/editor only).

### Task 2: Router move-only (no Look finger)

**Files:**
- Modify: `Assets/_Project/Scripts/UI/MobileInput/MobileTouchRouter.cs`

- [ ] In `AssignRoleForNewFinger`, right half returns `FingerRole.None` (not Look).
- [ ] Remove/stop calling `ApplyLookOutput` side effects for look (or leave inert).
- [ ] Debug report: show `lookId` as N/A or swipe-zone state instead of router look id.

### Task 3: Restore MobileSwipeLookZone

**Files:**
- Modify: `Assets/_Project/Scripts/UI/MobileInputController.cs`
- Modify: `Assets/_Project/Prefabs/UI/MobileJoystickUI.prefab` (if zone missing component)

- [ ] Re-add `MobileSwipeLookZone` nested class + `InvisibleTouchGraphic`.
- [ ] Field `lookSwipeZone`; bind in `TryBindExistingUi` / `BuildUi`.
- [ ] `CreateTouchZone`: raycast **on** for look zone graphic.
- [ ] `Update`: do not call `ApplyLookInput` when `!cameraSystem.IsFirstPerson`; FPP uses zone + `ApplyLookInput`.
- [ ] Remove dual-touch `lookSens *= 1.25f` on router look (obsolete).

### Task 4: Honest debug overlay

**Files:**
- Modify: `Assets/_Project/Scripts/UI/MobileInputController.cs`
- Modify: `Assets/_Project/Scripts/CameraSystem.cs` (optional: expose last yaw for debug)

- [ ] LIVE look: compare TPP yaw axis delta frame-to-frame (threshold ~0.01°).
- [ ] Latch "Camera moves while swiping": same criterion.
- [ ] Report line: `swipeZone=drag|idle`, `cmInput=on`.

### Task 5: Device verification

- [ ] APK: swipe only → camera moves.
- [ ] APK: joy + swipe + re-swipe.
- [ ] Editor: LMB move, RMB still via router editor mouse (optional).
