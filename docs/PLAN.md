# MONK-SYNC: Backrooms Arm-Locomotion VR Game

A native Unity VR game. You move only by pushing and pulling off surfaces with your arms, in a procedurally generated Backrooms. Level layouts and creatures are original designs.

## 1. Pillars
1. **Movement is the game.** Arm locomotion is the only way to move. No thumbstick walking.
2. **Liminal dread.** Fluorescent hum and sameness. Tension comes from sound, not jump scares.
3. **Built to climb.** Atriums, collapsed ceilings, shelving, vents and pits give the arms something to grab.
4. **Co-op later.** Single-player first. Don't block 2-4 player co-op.

## 2. Tech stack
| Area | Choice |
|---|---|
| Engine | Unity 6 LTS (6000.0.x), URP, Forward+, single-pass instanced |
| XR | OpenXR (XR Interaction Toolkit added later for items/UI) |
| Target | Meta Quest 3/3S standalone first, PCVR second |
| Physics | PhysX, fixed timestep 1/90 |
| AI | AI Navigation, per-chunk runtime NavMesh bake |
| Netcode (phase 2) | Photon Fusion 2 (Shared) or NGO + Relay |
| Audio | Unity audio + Meta XR Audio SDK spatializer |

## 3. Arm locomotion (core)
Implemented in `Assets/_Monk/Scripts/Locomotion`.

Per physics step, per hand:
1. Take the tracked hand target, clamped to `maxArmLength` from the head.
2. Sphere-cast from the last free hand position to the target.
3. On hit, the hand **sticks**: it stores an anchor point and surface normal.
4. While stuck, the body moves by `anchor - target` (hand stays, body moves).
5. The hand **releases** when the target leaves the surface (moves out along the normal), or drifts beyond `unstickDistance`.
6. Body movement is sphere-cast so the body can't pass through walls.
7. A rolling velocity history of the body is kept. On release of all hands: `velocity = avg(history) * jumpMultiplier`, clamped to `maxJumpSpeed`. Then gravity and drag apply.

Surface overrides (`SurfaceProperties`): `grip` (0 = slippery/no stick), `noise` (for AI hearing).

Requirement: walkable geometry uses box/sphere/capsule/convex colliders (needed for penetration resolve).

## 4. Backrooms generation
- Seeded, deterministic, chunk-based. 4 m cells, 8x8 cells per chunk, ring-streamed around the player.
- Module prefabs per cell; weighted adjacency rules (WFC only if needed).
- Low-frequency noise places points of interest: atriums (6-10 m tall), shelf mazes, exit seams.
- Flood-fill connectivity check per chunk, carve doors if needed, then async NavMesh bake.

| Level | Look | Movement twist |
|---|---|---|
| L0 Lobby | Yellow wallpaper, fluorescents | Tutorial, low ceilings then first atrium |
| L1 Storage | Concrete, crates, pipes | Vertical, shelving, pipe swinging |
| L2 Pipe Works | Narrow, hot | Wall-shimmying, vents |
| L3 Poolrooms | White tile, water | Slippery, swim drag |
| L! Run | Red emergency light | Chase, long sprints |

## 5. Art / lighting / audio
- 4 m modular kit, trim sheets, SRP-Batcher-friendly materials.
- Per-module baked lightmaps, emissive unlit light panels, shader-driven flicker, max 1-2 realtime lights, fog to hide streaming.
- Cell-based visibility culling (BFS through open edges).
- Audio: area hum, hand-slap squelch, distant footsteps, spatialized entity calls.

## 6. Entities (original)
- **Listener** (blind): hunts by sound. Hand slaps are loud.
- **Watcher** (light-sensitive): freezes in flashlight, advances during flicker.
- **Crawler** (ceiling): climbing doesn't make you safe.
State machine: Idle > Wander > Investigate(noise) > Chase > Lose. Max 3 active, sensing at 10 Hz.

## 7. Systems
Run structure (find exit seam, next level; death resets to L0 with new seed), items (almond water, flashlight, chalk, flare), sanity, journal hub, co-op later (shared seed, host simulates entities, voice).

## 8. Milestones
| # | Milestone | Done when |
|---|---|---|
| M0 | Project setup | Unity 6 URP + OpenXR, builds to Quest, LFS |
| M1 | **Locomotion prototype** | Climb a whitebox gym; feels good at 90 fps |
| M2 | Generator v1 | Seeded L0 whitebox, chunk streaming, deterministic |
| M3 | Vertical slice | L0 art, lighting, audio, Listener, exit, death/restart |
| M4 | Content | L1, L2, L!, items, sanity, hub |
| M5 | Perf and comfort | 72/90 Hz on Quest 3, comfort options |
| M6 | Co-op (optional) | 2-4 players |

Quest budget: <=150 draw calls, <=300k visible tris, <=2 realtime lights, no realtime shadows, FFR on.

## 9. Risks
1. Low ceilings fight arm locomotion: design verticality into every chunk; prototype with Backrooms proportions.
2. Tunneling: always sphere-cast, cap speed.
3. Motion sickness: no artificial rotation by default, optional vignette.
4. Procedural lighting looks flat: prove per-module lightmaps in M3, fallback to stylized unlit.
5. Chunk-stream hitches: pool modules, async bake, spread instantiation.
