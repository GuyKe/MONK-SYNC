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

## M2: the Backrooms map
Menu **Monk > Build Backrooms Map** (empty scene, URP assigned) generates the whole level, the player rig and all gameplay. Save the scene afterwards. Run it again to rebuild.

- **Data-driven:** the layout is the ASCII grid in `Scripts/World/MapLayout.cs` (4 m cells; legend in the file). EditMode tests check every area is reachable. Edit the grid and rebuild.
- **Textures and audio are generated in code.** Textures land in `Assets/_Monk/Art/Generated` (delete them with Monk > Delete Generated Map Textures to regenerate). Every sound is synthesised at runtime, so there are no art or audio files to import.
- **Glass:** the observation window is two clear panes. Slap one hard (about 3 m/s) and it shatters into physical shards, opening the start zone. Slower touches just rest on it.
- **Door approaches:** props are never placed in front of or behind a door (`NearDoor` in `MapBuilder`).
- **Detail pass:** sprinklers, ceiling grates, outlets, switches, conduit, extinguishers, exit signs, hazard-striped door frames, floor stains, litter, fallen ceiling tiles, office cubicles, metal shelving, a control console, floor cabling and more.
- **Areas:** concrete start zone with airlock and shattered observation window; carpet maze with an atrium; The Pitfalls (deep square drops, narrow walkways, hanging pipes); flooded hallway (slick floor, reflective water); server / relay station; tall hall with a catwalk.
- **Verticality:** crate stacks, knocked-over dividers, ceiling pipes, a catwalk, wall nooks (hiding spots cut into drywall) and 0.9 m vent shafts between rooms.
- **Doors:** fire doors (manual), blast doors (automatic, rise from above) and the airlock. Slap a button or lever with a hand. Closing = warning sparks and beeps, then a heavy drop. Being under it eliminates you.
- **Monster:** the Stalker patrols the wide halls and hunts by sight and by noise (hand slaps and door slams are noise). Its NavMesh agent is wide, so vents and narrow gaps are closed to it; doorways slow it.
- **Lighting:** ~150 emissive ceiling panels (steady, flickering, dead) but only 4 real lights, which follow the nearest working panels (`LightPanelManager`). Dead panels leave dark pockets.
- **Audio:** fluorescent buzz that rises in pitch near a panel, vent rumbles and clanks, wet carpet squelches, water splashes, monster footsteps, a hallway reverb zone.
- **Hazards:** door crush, pit drop, monster. Elimination respawns you at the last checkpoint.

### Performance notes (Quest)
Static geometry is batching-static; no realtime shadows; 4 realtime lights plus 2 red ones in the server room; one realtime reflection probe rendered once. If the headset struggles, lower `lightCount` on `LightPanelManager`, and bake lighting instead for a fixed look.
