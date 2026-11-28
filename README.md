# MONK-SYNC

Unity VR game: arm-locomotion movement in a procedural Backrooms. See [docs/PLAN.md](docs/PLAN.md).

## Open the project
1. Unity Hub > Add project from disk > this folder. Use Unity 6 (6000.0.x); it will generate `ProjectSettings` and `.meta` files. Commit the `.meta` files.
2. Project Settings > Graphics: use a URP asset (Quest: Mobile URP). Player > Android: Vulkan, ARM64, IL2CPP.
3. XR Plug-in Management > OpenXR (enable Oculus Touch Controller Profile; Meta Quest feature group on Android).
4. Physics > Fixed Timestep: `0.01111` (90 Hz).

## M1: locomotion gym
1. New empty scene, then menu **Monk > Build M1 Locomotion Gym**. It creates the whitebox gym, the rig (`ArmLocomotion`), and `Assets/_Monk/LocomotionProfile.asset`.
2. Press Play with a headset (Link / Air Link) or build to Quest.
3. Tune values in the `LocomotionProfile` asset. Select `PlayerRig` to see gizmos: cyan = body, green = stuck hand, magenta = velocity.

Walkable geometry must use box/sphere/capsule/convex colliders (body penetration resolve needs them).

Tests: Window > General > Test Runner > EditMode.

## Locomotion rules
- **Hand-contact propulsion:** pushing a hand into geometry moves the body the opposite way; real hand velocity becomes momentum when the last hand lets go.
- **No legs / no stick walking:** there is no thumbstick locomotion. The avatar is a torso and arms (`UpperBodyAvatar`).
- **Surface slip:** `SurfaceProperties.slipPercentage` (0 = rough bark, full traction; 1 = ice, hands slide). Presets: Rough, Normal, Smooth, Slick, Ice. The gym's north wall has one strip per preset.
- **Arm-span scaling:** hold both grips with arms stretched out for 1.5 s (`ArmSpanCalibrator`). Hand reach scales by `referenceArmSpan / measuredSpan`, saved in PlayerPrefs.
