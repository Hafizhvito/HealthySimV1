# Dual-Touch Device Test Matrix

Physical Android validation for Mobile Touch Router.

## Setup

1. Build APK to a physical Android device.
2. On `MobileInputController` in `MobileJoystickUI.prefab`, enable **Debug Dual Touch Overlay**.
3. Project uses Input System Package (New) only.

## Test cases

| # | Steps | Expected |
|---|--------|----------|
| 1 | Hold left joystick, drag right half | `moveId` and `lookId` set; move + camera together |
| 2 | Hold left **first**, then drag right | Swipe works (fixes order dependency) |
| 3 | Release right only, keep left, swipe right again | Look resumes immediately |
| 4 | Drag left far outside ring | Base stays; knob at edge; move still works |
| 5 | Tap FPP/TPP button | No move/look from that finger |
| 6 | Two-finger release | Next touches assign fresh roles |

## Overlay signals

| Field | Healthy dual-touch |
|-------|-------------------|
| `touchCount` | 2 while both fingers down |
| `moveId` / `lookId` | Not -1 during respective drags |
| `fingers:` | One Move, one Look |

## Pass criteria

All six cases pass without requiring swipe before joystick.
