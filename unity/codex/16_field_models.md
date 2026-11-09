# JOB_ID: 16_field_models — Z0 field ecology props (prey critter, supply crate, elite part drops)

## File you may edit
- `unity/Assets/SOUL/Runtime/World/FieldModels.cs` (only this file; Claude placeholders inside)

## Context
Z0 now has a territory ecosystem (read `기획서/Z0_생태계_엘리트_파츠.md`, `Game/SoulGame.Ecology.cs`, `Fx/Critters.cs`):
small prey organisms scurry around nests and get hunted by scrap hounds; two supply crates sit on 2.1 m mesas; two map
elites drop a part the player walks over to collect. Seen from the top-down camera (~55°, 10-25 m). Keep the low-poly,
flat-shaded, vertex-coloured ruined style (see `World/RegionProps.cs`, `World/ZoneProps.cs`).

## Keep these exact signatures (class `CritterMeshes`, static)
```csharp
public static Mesh Rat();                 // 재 쥐, prey organism
public static Mesh Crate();               // supply crate body (lit, vertex colours)
public static Mesh CrateGlow();           // the crate's glowing parts only (rendered unlit HDR soul-cyan by the caller)
public static Mesh PartDrop(string kind); // rendered unlit HDR warm gold by the caller: keep it compact
```
Sim space, +Z forward, +Y up, metres, origin on the ground at the object's centre. Build with `MeshBuilder`, return `b.Build(name)`.

## Looks
- `Rat`: an organism that survived the ruin: ~0.35 m long ash-grey/brown body, long tail, big ears, a hint of pale
  eyes; slightly mutated (a bony ridge or extra spines). Readable as an animal from above. ≤ 400 tris.
- `Crate` + `CrateGlow`: a scientist's sealed supply crate (~1.0 × 0.7 × 0.7 m) — dented olive metal case, latches,
  handles, a strap, small debris at its base; `CrateGlow` = thin seams / a status lamp strip that matches the crate
  geometry exactly (same coordinates). ≤ 900 tris total.
- `PartDrop("leap")`: 도약 관절 — a robot knee joint with a spring piston and torn cable ends (~0.5 m).
  `PartDrop("skill:wave")`: 레이저 파동 module — a cracked emitter lens housing with fins (~0.45 m). Any other kind:
  fall back to the leap shape. These render with one unlit glow material (no vertex colour), so give them strong
  shape silhouettes. ≤ 500 tris each.
- No text, arrows or marker shapes.

## When done
Write `unity/codex/results/16_field_models.md` (what each mesh looks like, triangle counts).
