# JOB_ID: 10_slab — fallen concrete slab + slab surface fragments

## Files you may edit
- `unity/Assets/SOUL/Runtime/World/RegionProps.cs` — **only** `ConcreteSlab(int seed)` and new private helpers it needs
- `unity/Assets/SOUL/Runtime/World/RuinStructures.cs` — **only** `Fragments(...)` (keep its signature)

## Problem (user feedback 2026-11-07: "이 구조물들 뭔지 잘 모르겠다")
`ConcreteSlab` is a floor slab of a collapsed building lying tilted against the ground (a walkable ramp,
`RuinStructures.Flight` registers the ramp surface). From the top-down camera it reads as a **studded mat / tile
board**, not as a broken concrete slab, because `Fragments` covers the whole top with a regular grid
(up to 10×12) of small raised quads, 14 % brick-red, 30 % dark. The rebar at the high end is too thin to see.

## What it must read as
A thick, heavy reinforced-concrete floor slab that broke off and slid down: one end resting on the ground, the
other end raised (rise = run × 0.12–0.32, unchanged).
- Broken silhouette: chipped corners, one or two bites out of the long edges, the raised end a **torn fracture
  face** (irregular, thicker-looking) instead of a clean edge.
- Top: mostly one continuous concrete surface with subtle colour variation; 2–3 **large** cracks following the
  slope; a few (3–6) irregular loose chunks of different sizes lying on it, not a grid. Optional dust/soot band.
- Underside visible from the side: dark gap / shadow under the raised end, a crushed rubble pile at the low end
  and under the raised end where it rests on debris.
- Rebar: a bundle of **thick, bent** rods (gauge ≈ 0.05–0.07 m) sticking out of the fracture face, rust colour,
  clearly visible from above at 15–25 m.
- Optionally a remnant of floor finish (tiles) on a small patch only — never a full grid.

## `Fragments` (shared by stairs-less `Flight` ramps and `Mound` rubble mounds)
Replace the regular grid with an irregular scatter (jittered / Poisson-like positions, varied sizes, rotation,
some gaps showing the surface beneath). Keep the parameters and their meaning (columns × rows ≈ target count and
density; `round` = keep inside the circle). The result must still follow `surface(x, z)` and stay within
`width × depth`. Avoid repeating patterns; reduce the brick-red share to ≤ 5 %.

## Keep
- `ConcreteSlab` public signature, footprint (run 3–6 m, width 2–3.6 m), the `RuinStructures.Flight(...)` call and
  its parameters (the walkable ramp), yaw logic. All visible geometry above the ramp surface must stay ≤ 0.25 m
  above it except at the raised end (fracture face / rebar may go higher) so the player does not walk through
  visible chunks.
- ≤ ~2,500 triangles per slab.
