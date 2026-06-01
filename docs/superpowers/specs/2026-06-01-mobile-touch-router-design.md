# Mobile Touch Router Design

## Overview

Gameplay touch on Android uses `MobileTouchRouter` as the single authority per frame. `MobileMoveStickPresenter` handles joystick visuals only. `MobileInputController` builds UI, bridges to `PlayerController` / `CameraSystem`, and optional debug overlay.

## Constraints

- Do not edit `Assets/Scenes/SampleScene.unity`.
- Do not change `InjectMobileInput` or `AddLookInput` contracts.
- Changes limited to scripts and `MobileJoystickUI.prefab`.
- Input: New Input System only (`activeInputHandler: 1`).

## Components

| Unit | Role |
|------|------|
| `MobileTouchRouter` | Poll `Touchscreen.current` (`phase != None`); assign Move / Look / UiBlocked per finger |
| `MobileMoveStickPresenter` | Base at move origin; knob clamped to radius; smoothing |
| `MobileInputController` | UI, EventSystem, bridge, debug overlay, perspective button |

## Routing rules

- Move: `screenX < width * 0.45`, max one finger.
- Look: `screenX >= width * 0.45`, max one finger.
- No joystick rect hit-test for routing.
- UI: `IsPointerOverGameObject(fingerId)` before role assignment.
- Look zone raycast off; perspective button raycast on.

## Joystick visual (Genshin-style)

- Outer ring anchored at touch begin; does not follow finger afterward.
- Knob offset = `ClampMagnitude(finger - origin, radius)`.
- Move vector from clamped offset (dead zone applied in controller).

## Debug overlay fields

`touchCount`, `moveId`, `lookId`, `moveVec`, `look`, `moveOut`, `lookOut`, `fingers:` lines with role.

## Editor testing

LMB = move, RMB = look when not on mobile platform.
