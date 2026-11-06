# JOB_ID: 01_gates — zone link gate models

## File you may edit
- `unity/Assets/SOUL/Runtime/World/GateModels.cs` (only this file)

## What it is
Every connection between two zones is a 14 m wide corridor cut through a rock border ridge
(`ZoneWorld`, `ZoneGates`). `GateModels` draws what sits in that corridor. Collision is in
`ZoneGates.Build` (read it, do not edit it). Frame is site-local: +Z from zone `link.a` toward `link.b`,
+X along the border, y = 0 ground. Open corridor |x| < 7; gates span |x| < hw (= 14). Beyond |x| ≈ 12
the ridge rock rises, so the outer 2 m of each gate can tuck into rock.

Replace each method's placeholder body with a detailed model in the shared style. Keep signatures.
Use `ZoneA(s)` / `ZoneB(s)` (zone ids, see `ZoneGraph.Zones`: 0 ash ruins, 1 fallen city, 2 skyscraper
grave, 3 scrap gorge, 4 war foundry, 5 dead woods, 6 toxic mire, 7 lab) to theme materials when useful.

| method | what it must read as | solid footprint (must fill, must not exceed) |
|---|---|---|
| `FreeCorridor` | the trail simply continues between the ridges: worn ground, a few edge rocks / broken kerbs | nothing inside |x| < 5.5 |
| `Ledge` | a 2.6 m high wall-topped block you clearly cannot jump: collapsed concrete slab stack / rock shelf, flat walkable top | |x| < hw, |z| < 6, top exactly at H (walkable, keep it flat ±0.05) |
| `Ramp` | a rubble slope / tilted slab you walk down from the high side (drop exits) | the wedge |x| < hx between z0 (y 0) and z1 (y H); surface stays a straight slope |
| `Anchor` | a sturdy anchor post + ring the rope arm hooks into (left by the scientist? salvaged crane hook?) | post at (ring.x, ring.z) from y = H up to the ring; ring glow in `g` |
| `JetGap` | a real chasm too wide to jump: broken road edge, dark depth, debris hanging over | |z| < 5 open; lips at |z| ≈ 5–6 |
| `CannonWall` | a barricade of cracked concrete and scrap that looks breakable by force (stays until the cannon) | |z| < 1.8, height 3–4.6 m |
| `MagnetWall` | a tall rusted riveted steel wall (a ship hull plate / factory wall) the magnet legs will climb | |z| < 0.9, height ~7 m |
| `Sludge` | a toxic sludge pool with crusted rim, bubbles, half-sunk drums | |z| < 6, flat at y ≈ 0.03 |
| `LabDoor` | the sealed lab door: heavy frame (`b`, pillars at x = ±10, lintel at 9.4 m) + door leaves (`sb`), two key lamps (`g`) | leaves |x| < 8.6, |z| < 1.6 |

Notes
- `sb`/`sg` (solid) vanish when the player owns the part; draw the barrier itself there.
  `b`/`g` stay (frame, rubble around it).
- No marker poles, no arrows, no text. The player must understand each gate from its look.
- Keep the glow sparing: anchor ring, faint heat in cannon cracks, sludge glimmer, door lamps.
