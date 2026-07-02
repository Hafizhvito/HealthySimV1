# FPP/TPP Toggle Button & Camera Fix — Design Spec
**Date:** 2026-07-02  
**Status:** Approved, pending implementation

---

## Overview

Add a FPP/TPP toggle button to the HUD (top-right, left of Settings) and fix the camera transition + mobile FPP look so the feature is fully functional, smooth, and clean on both desktop and mobile (Android).

---

## Scope

| In scope | Out of scope |
|---|---|
| New `CameraToggleButton.cs` UI script | TPP mobile swipe tuning (already okay) |
| Button wiring in `HUDAutoSetup.cs` | Desktop mouse look sensitivity changes |
| Camera transition smoothness fix | Any new Cinemachine virtual cameras |
| FPP pitch reset on transition | Pause/settings button restyle |
| Mobile FPP look routing | |
| FPP sensitivity new mobile field | |

---

## Section 1 — UI Button

### File
`Assets/_Project/Scripts/UI/CameraToggleButton.cs` *(new)*

### Behaviour
- MonoBehaviour, creates its own UI elements at runtime on `Start()`
- Finds `HUD_Canvas` by name, parents itself there
- `Update()` polls `CameraSystem.IsFirstPerson` each frame and syncs visual state

### Layout
| Property | Value |
|---|---|
| Anchor + Pivot | `(1, 1)` top-right |
| AnchoredPosition | `(-158, -24)` — left of Settings button with ~12px gap |
| Visual size (sizeDelta) | `72 × 72` px |
| Min touch target | `108 × 108` px (via RaycastPadding or LayoutElement) |
| Border radius feel | `BorderRadius` via Image + rounded sprite or outline component |

### Visual States

**TPP active (default):**
- Background: `rgba(10, 18, 35, 0.92)` dark navy
- Border: `rgba(255, 255, 255, 0.18)` subtle white
- Line 1 (bold, 15px): `"TPP"` — color white `#F0F4FF`
- Line 2 (10px): `"→ FPP"` — color `#8899BB` muted

**FPP active:**
- Background: `#2ECC71` green (matches Settings/Pause button exactly)
- Border: `rgba(255, 255, 255, 0.25)`
- Line 1 (bold, 15px): `"FPP"` — color white `#FFFFFF`
- Line 2 (10px): `"→ TPP"` — color `rgba(255,255,255,0.70)`

Both states use `TextMeshProUGUI` with `outlineColor = rgba(0,0,0,0.88)`, `outlineWidth = 0.18` — matching HUD text style.

### Wiring in HUDAutoSetup
`HUDAutoSetup.BuildOrUpdateHUD()` gets one new private method call at the end:

```csharp
EnsureCameraToggleButton(canvas.transform);
```

`EnsureCameraToggleButton()` finds or creates the `CameraToggleButton` component on a child object. Idempotent — safe to call multiple times.

### Click Handler
```csharp
button.onClick.AddListener(() => {
    if (CameraSystem.Instance != null)
        CameraSystem.Instance.TogglePerspectiveFromMobile();
});
```

---

## Section 2 — Camera Transition Fix

### File
`Assets/_Project/Scripts/CameraSystem.cs` *(modified)*

### Fix A — Align player yaw on FPP entry (`SetFPP`)
Before swapping camera priority, rotate `playerRoot` to match the current orbital yaw so FPP camera faces the same direction as TPP was looking:

```csharp
// At the start of SetFPP(), before priority swap:
if (orbitalFollow != null && playerRoot != null)
{
    float currentYaw = orbitalFollow.HorizontalAxis.Value;
    Vector3 euler = playerRoot.eulerAngles;
    playerRoot.eulerAngles = new Vector3(euler.x, currentYaw, euler.z);
}
```

### Fix B — Reset FPP pitch on FPP entry (`SetFPP`)
Reset `fppPanTilt.TiltAxis.Value = 0f` before starting the transition so FPP never starts with a stale pitch from a previous session.

### Fix C — `TransitionRoutine` pitch clamp
Ensure `targetTilt` in `TransitionRoutine(ToFPP)` is always `0f` (currently lerps to last stored value which may be non-zero):
```csharp
float targetTilt = 0f; // was: Mathf.Clamp(startTilt, fppPitchMin, fppPitchMax)
```

### Fix D — Mobile cursor lock guard
Wrap `LockCursor()` and `UnlockCursor()` calls:
```csharp
private void LockCursor()
{
    if (Application.isMobilePlatform) return;
    if (MobileInputController.Instance != null && MobileInputController.Instance.IsTouchUiEnabled) return;
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
}
```
Same guard for `UnlockCursor()`.

---

## Section 3 — FPP Sensitivity & Mobile FPP Look

### File
`Assets/_Project/Scripts/CameraSystem.cs` *(modified)*

### 3a — New serialized field for mobile FPP sensitivity
```csharp
[Header("Look Input (FPP)")]
[SerializeField] [Range(0.05f, 3f)] private float fppMouseLookSensitivity = 0.4f;
[SerializeField] [Range(0f, 20f)] private float fppLookSmoothing = 8f;   // was 12f
[SerializeField] [Range(0.02f, 1f)] private float fppMobileSensitivity = 0.18f; // new
```

`fppLookSmoothing` reduced from `12f` → `8f` for less "floaty" feel.

### 3b — New public method for mobile FPP look
```csharp
public void AddMobileFppLookInput(Vector2 normalizedScreenDelta, float sensitivity)
{
    if (!isFirstPerson) return;
    if (ModalStateManager.Instance != null && ModalStateManager.Instance.IsAnyModalOpen) return;
    if (normalizedScreenDelta.sqrMagnitude <= 0.0000001f) return;

    float gain = 360f * Mathf.Max(0.01f, fppMobileSensitivity) * Mathf.Max(0.01f, sensitivity);
    float yawDeg   =  normalizedScreenDelta.x * gain;
    float pitchDeg = -normalizedScreenDelta.y * gain;  // invert Y for natural feel

    ApplyFppLookInput(yawDeg, pitchDeg);
}
```

### 3c — Routing in MobileInputController
In the swipe look dispatch (wherever `AddMobileTppLookInput` is called), add a branch:

```csharp
if (cameraSystem.IsFirstPerson)
    cameraSystem.AddMobileFppLookInput(normalizedDelta, sensitivity);
else
    cameraSystem.AddMobileTppLookInput(normalizedDelta, sensitivity, gainMultiplier);
```

The exact call site will be confirmed during implementation by auditing `MobileInputController.cs` fully.

---

## Files Changed Summary

| File | Change type |
|---|---|
| `Assets/_Project/Scripts/UI/CameraToggleButton.cs` | **New** |
| `Assets/_Project/Scripts/UI/HUDAutoSetup.cs` | Modified — add `EnsureCameraToggleButton()` |
| `Assets/_Project/Scripts/CameraSystem.cs` | Modified — transition fix + new mobile FPP method |
| `Assets/_Project/Scripts/UI/MobileInputController.cs` | Modified — branch for FPP look routing |

---

## Success Criteria

- [ ] Toggle button visible in top-right HUD, left of Settings, with correct gap
- [ ] Button shows TPP (dark) / FPP (green) state correctly
- [ ] Tapping button switches perspective — no errors in console
- [ ] TPP→FPP transition: kamera tidak "lompat", langsung menghadap arah yang sama
- [ ] FPP pitch starts at 0° (tidak dimulai dari sudut aneh)
- [ ] FPP mobile swipe look berfungsi — swipe kiri/kanan/atas/bawah menggerakkan kamera
- [ ] FPP mobile feel "mid" — tidak terlalu sensitif, tidak terlalu lambat
- [ ] Cursor lock tidak dipanggil di mobile
- [ ] Tidak ada compile error, tidak ada `NullReferenceException` di runtime
