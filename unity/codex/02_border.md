# JOB_ID: 02_border — zone border ridges that look like their zones

## File you may edit
- `unity/Assets/SOUL/Runtime/World/BorderDressing.cs` (only this file)

## What it is
Between zones runs an impassable ridge: a height field (`ZoneWorld.RidgeHeight`, up to ~13 m between
zones, ~24 m at the world edge) meshed per 32 m chunk by `ZoneGates.Ridge`, with its own colliders.
Near every passage two ridge spurs flare out like a funnel (`ZoneWorld.Funnel`) to gather the player.
Right now it reads as generic grey rock. Make it belong to the zones it separates.

1. `RidgeColor(p, steep, noise)` — colour of one ridge triangle. Use `ZoneWorld.Sample(p.x, p.z, out other,
   out dist)` (zone, neighbour zone, distance to the border) to blend both zones' materials, darker in
   crevices (steep), soot / rust / moss streaks by height, lighter weathered tops.
2. `Dress(x0, z0, size)` — return one decoration mesh for this chunk (or null), built with a
   `MeshBuilder` seeded from the chunk coordinates. Place pieces only where
   `ZoneWorld.RidgeHeight(x, z) > ZoneWorld.SOLID` (inside the blocking ridge) or within 2 m of it, and
   never where `ZoneWorld.Opening(x, z, out _) > 0` or `ZoneRoutes.Dist(x, z) < 6`. Sit pieces on
   the ridge surface (y = RidgeHeight). Themes by zone (`Regions.At(x, z)` per piece):
   - Z0 ash ruins / Z1 fallen city: collapsed building rows — broken facades, floor slabs stacked and
     tilted, rebar, window holes, toppled street lamps
   - Z2 skyscraper grave: huge fallen tower sections, girders sticking out
   - Z3 scrap gorge: layered canyon cliff strata, wedged scrap, pipes
   - Z4 war foundry: slag heaps, broken conveyor frames, chimney stumps
   - Z5 dead woods: wall of dead trunks, roots and fallen logs over rock
   - Z6 toxic mire: sunken hulls, rusted tanks, drowned dead trees
   - Z7 lab: concrete security wall segments, fences, cameras
   - world edge (other = -1): taller cliffs.
   Funnel spurs and corridor mouths are where the eye goes: give them the most characterful pieces
   (a leaning facade, a big trunk) so the passage is framed.
   The chunk mesh uses the same material as ridges (lit vertex colour, cutaway near the player).

Constraints: no collision is created here (the height-field colliders already block). Deterministic per
chunk (seed from x0, z0 only). Keep `Dress` fast (≤ ~2 ms per chunk; sample the ridge on a ~3 m grid).
