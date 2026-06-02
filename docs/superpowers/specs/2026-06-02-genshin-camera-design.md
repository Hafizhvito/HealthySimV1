# Genshin-Style Third-Person Camera Design

## Summary

Polish existing Cinemachine 3 orbital TPP stack for Genshin-like mobile feel: lower-third framing, unified swipe look, wall deocclusion, and soft yaw recenter while moving.

**Constraint:** Do not edit `SampleScene.unity`. All Cinemachine tuning is applied at runtime by `CameraSystem.ApplyGenshinTppDefaults()`.

## Files changed (camera-related only)

| File | Change |
|------|--------|
| `CameraSystem.cs` | Runtime CM tuning, yaw recenter, pitch sync, `NotifyManualLook()` |
| `MobileInputController.cs` | Single TPP swipe look path |
| `MobileTouchRouter.cs` | Move-only router (right half ignored) |
| `MobileJoystickUI.prefab` | Already has LookSwipeZone 45%–100% (unchanged) |

## Runtime tuning (CameraSystem)

Applied in `Awake` when `applyGenshinTppDefaultsAtRuntime` is true:

- `RotationComposer.ScreenPosition`: (0, -0.15)
- `TargetOffset.y`: 1.3
- `OrbitalFollow.Radius`: 4.8, position damping 0.35
- `VerticalAxis`: 12°, range -20..35
- `Deoccluder`: Default + Ground layers, ignore Player tag, min distance 1.0

## Mobile look (single path)

- Left touch → `MobileTouchRouter` → move
- Right drag → `MobileSwipeLookZone` → `AddMobileTppLookInput`
- Cinemachine input controller disabled on mobile TPP

## Verification checklist

1. Swipe only → camera rotates
2. Joystick + swipe → move + look
3. Re-swipe while joystick held
4. Character lower-third framing
5. Walk into wall → camera pulls in
6. Run forward without touch → camera drifts behind player
7. Dialogue zoom / intro cinematic / FPP toggle still work
