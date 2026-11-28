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

## The facility hub and Level 0

Menu **Monk > Build Backrooms Map** (empty scene, URP assigned) generates everything: the facility hub, the Backrooms maze below it, the player rig and all gameplay. Save the scene afterwards. **Monk > Build Backrooms Map (new random layout)** builds a different maze. The hub is always the same.

### Spawn hub (Async-style research facility)
An elevated concrete complex sitting over a huge open atrium, with grimy PBR concrete (normal maps, scuffs, streaks), metal floor grates and diamond plate with scuffed metallic reflections, hazard stripes, exposed conduits and hanging cable bundles.
- **Observation window:** a wall-sized wire-reinforced glass window (unbreakable) looking straight down into the atrium and the maze beyond. Spot hazards (and, later, monsters) before you drop in.
- **Entry airlock:** a heavy blast door with glowing push-buttons (inside and out) that slides open with hydraulic sound, flashing warning lamps and a controller rumble. It leads onto a grated catwalk above the atrium with a hazard-striped drop edge. Jump off to enter Level 0. A spiral scaffold of platforms (with crates at the bottom) is the way back up.
- **Cosmetics wing:** a real-time reflective mirror (a hidden camera renders your reflection with an off-axis frustum, only while you are near), mannequins wearing sample outfits, rotating hologram displays, and a wardrobe board with buttons for hat, visor, badge and seven hazmat suit colours. Your choices are saved. Head, hat and visor are on a layer your own camera cannot see, but the mirror can.
- **Computer terminal:** a retro CRT with a physical keyboard of tappable keys. Type a display name (shown on a name tag over your head in the mirror, saved) and flip to a server-stats page.
- **Gamemode console:** four big mode buttons (Standard Escape, Infection / Tag, Lights Out, Speedrun), two levers (alarm mute, prep time) and glowing status screens. Changing the mode changes the hub: cool white / pulsing red with a siren and rotating beacons / near-dark amber with a klaxon / racing green chase lights with a ticking beep. Gamemodes themselves come later; for now the room reacts and keeps the choice.
- **CCTV wall:** six live monitors showing real cameras placed in the atrium, the Pitfalls, a relay station and three corridors. Cameras render one at a time and only while you are near the wall, and the maze around them is kept loaded.

### Level 0
- A seeded 44 x 44 cell maze (176 m) from `MapLayout.cs`: thin partition walls, dead ends, loops, pillar halls, tall halls with catwalks, flooded areas, three Pitfalls rooms, two server stations. Tests check everything is reachable from the atrium.
- **Looks:** three wallpapers, three carpets and two ceiling tiles that change across the map, with normal maps (fabric weave, embossed paper), water stains, peeling corners, mold, cracks, tally marks and spray arrows. Textures are generated in code into `Assets/_Monk/Art/Generated` (delete with Monk > Delete Generated Map Textures). All sounds are synthesised at runtime.
- **Verticality:** fallen drywall ramps, climbable server racks with crates stacked on top, crate stacks, shelving, cubicles, overhead pipes and cross bars to swing from, ventilation ducts, drywall holes through partitions, catwalks.
- **Fire doors:** heavy doors in the corridors, worked by hand with push-buttons on both sides of both walls. Closing warns with sparks and beeps, then drops hard; being under it eliminates you. Six of them start locked (red lamps).
- **Emergency fuse boxes** hidden in dead ends (some half-buried under drywall): flip the lever to unlock the doors wired to it, or to restore the ceiling lights (even dead ones) around it for a minute.
- **Audio and haptics:** a fluorescent hum that rises in pitch near a panel, vent rumbles, wet carpet squelches, echoing distant footsteps, a hallway reverb; both controllers rumble when a heavy door slams nearby, and on button, lever and fuse hits.

### Performance
The maze is built in 24 m blocks and `ChunkVisibility` keeps only the blocks within about 56 m active (plus those around the CCTV cameras while you are at the monitors), hidden in fog. Floors, ceilings and walls are merged into runs and batching-static. This is still a lot of geometry for a standalone headset: if Quest struggles, lower `radius` on `ChunkVisibility`, raise the fog density, or reduce `DefaultSize` in `MapLayout`.
