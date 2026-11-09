# JOB_ID: 23_z6_mire_props — the dead shell platform, drowned roofs, egg shells, sludge ball, two part drops

## Files you may edit
- `unity/Assets/SOUL/Runtime/Game/SoulGame.Mire.cs` — **only** the mesh builds inside `ShellStream` (`shellMesh`),
  `Roofs` (`roofMesh`), `SpawnEggs` (`eggMesh`) and `MireInit` (`mireBall`): replace those MeshBuilder blocks with
  better ones. Move them into a new `static class MireModels` at the bottom of that file (`DeadShell()`, `Roof()`,
  `EggShell()`, `SludgeBall()`) and call those. Do not change any gameplay numbers, collision, or other code.
- `unity/Assets/SOUL/Runtime/World/FieldModels.cs` — in `CritterMeshes.PartDrop(kind)` add two kinds (keep the rest):
  `"core:toxic"` and `"skill:suck"`.

Read `기획서/Z6_독성늪_보스_생태계.md` and the whole `SoulGame.Mire.cs` (sizes and how they are used).

## Meshes (sim space, MeshBuilder, vertex colours, flat-shaded low-poly, origin on the ground, faces +Z)
- **DeadShell()**: the shell host's hardened shell left in its pool after death: a huge ridged turtle dome, ~5 m radius,
  sunk so its **flat-ish walkable top is exactly at y = SHELL_TOP (2.2 m)** over a ~3.6 m half-size square
  (the collision is a 3.6 m half box at 2.2 m: the visible top must match it), barnacles and embedded machine plates,
  the empty neck hole on one side, a few cracks. ≤ 1,500 tris.
- **Roof()**: a drowned house roof poking out of the boss square: ~6.4 × 4.8 m footprint, its **walkable top at y =
  ROOF_TOP (1.0 m)** (flat ridge / flattened tiles), broken tiles, a chimney stub, algae line at the waterline
  (~0.75 m). ≤ 600 tris.
- **EggShell()**: a pale floating egg shell (split open, empty), ~1.7 m radius, its **top at y = EGG_TOP (0.95 m)**
  roughly flat where you stand. ≤ 400 tris.
- **SludgeBall()**: a lumpy ball of sludge (radius .5) with a few drips and bubbles. ≤ 200 tris (one opaque glow
  material, vertex colour unused).
- **PartDrop("core:toxic")**: 독에 잠식된 코어 — a machine core (cylinder housing, coolant fins) corroded and grown over by
  violet-green toxic crystal and veins, ~0.5 m. **PartDrop("skill:suck")**: 준설기 흡입구 — a small intake module: a
  toothed ring funnel with a hose stub, ~0.5 m. Same style / budget as the existing drops.

## When done
Write `unity/codex/results/23_z6_mire_props.md` (what each mesh looks like, triangle counts).
