# JOB_ID: 24_shell_host_zerg_turtle — redo the shell host (껍질 숙주) as a Zerg-like bug × turtle, and its corpse

The user's verdict on job 22/23: the living shell host's legs stand straight down like table legs ("거북이가 다리가 왜
저러냐 — 다리가 옆으로 달려야"), and the dead shell does not read as a creature ("별로 생물처럼 안 보임"). Direction:
**mix a StarCraft-Zerg-like bug with a turtle** (약간 저그의 벌레 느낌 + 거북이). Original creature: do not copy any
existing game's unit design, only the general "chitinous organic hive bug" feel.

## Files you may edit
- `web-proto/src/enemy_models.js` — **only** `createShellHost()` (and new private helpers it needs). Keep CRLF.
- `unity/Assets/SOUL/Runtime/Game/SoulGame.Mire.cs` — **only** `MireModels.DeadShell()` and the private helpers
  above it inside `MireModels` (`Pol`, `SkirtR`, `SkirtFrame`, `Barnacle`, `Rivets`, `Gear`, `TwoSided`, `Scute`, … you
  may delete the ones you stop using, but `SquarePoint` is used by `Roof()`: keep it). Nothing else in that file.

Do not run the exporter; Claude exports and rebuilds the prefabs. Read `기획서/Z6_독성늪_보스_생태계.md` (the turtle
section), the current `createShellHost`, `createHoundAlpha` (the quad contract it copies) and `SoulGame.Mire.cs`
(`HostUpdate` / `HostHit` / `ShellPlatform`: how the host plays, where the 3 sacs sit, the shell platform numbers).

## Look (both models are the same creature: the corpse must be recognisable as the thing the player just killed)
- **Carapace**: still a broad turtle dome as the dominant silhouette, but built from **overlapping chitin plates**
  (segmented, layered like an insect's back), with a raised spined keel ridge front-to-back and rows of short curved
  spikes / horn knobs along the plate edges; the rim flares into a serrated chitin skirt. Glossy dark purple-brown
  chitin with sickly olive edges; fleshy violet-pink membrane visible in the plate seams.
- **Organic rot over machines**: a couple of half-swallowed riveted machine plates still embedded (the zone theme:
  organisms eat machines), but secondary.
- **Head**: a heavy snapping-turtle beak merged with bug mandibles: hooked horn beak + a pair of side mandibles,
  small armored head on a thick wrinkled neck, violet `eye` glow.
- **Legs: sprawled to the sides, like a real turtle / a crab-insect hybrid.** The hip sits at the body's side under
  the shell rim; the upper leg goes **outward (sideways) and slightly down**, the knee bends down, the foot is planted
  well outside the shell's footprint. Upper leg = armored chitin segment, joints show soft membrane, the foot is a
  broad turtle paw ending in 3 long hooked bug claws. Front legs angle forward, rear legs angle back.
- **Back of the shell**: three fleshy bulges where Unity adds the 3 glowing poison sacs (see the contract), with
  veined tissue around them.
- Optional: a short segmented tail with a spiked tip.

## Living model — `createShellHost()` hard contract
- Same `parts` keys as now: `{ body, head, jaw, eye, core, legs: [{hip, knee, ankle, side, index}] ×4, throat }`,
  same leg order `[[-1,0],[1,0],[1,1],[-1,1]]`, `eye`/`core`/`throat` are `glow(...)` meshes, `root/radius/height`
  returned as now (`radius` may grow to ~.8 because of the sprawl).
- Unity scale is ×4.6 (rig `crawler`, quad): keep the dome radius ~.65 and height similar so gameplay sizes hold.
- **Leave the crown free** inside local radius ~.315 from the dome centre at the top: Unity puts the 3 sacs there.
- The hip pivots move outward to the sides (|x| ≈ .42–.5) and the knee offsets point outward (+side) and down so the
  sprawl reads; ankles below the knees with the feet at the ground (y of the foot sole ≈ the old one, ground = 0
  when body is at its pivot height). Pivot rotations identity. The procedural walk swings hips/knees around X;
  that still works with sideways legs.
- Budget ≤ 9,000 tris. Flat-shaded, `scrap(...)` batching per moving section, faces +Z.

## Corpse — `MireModels.DeadShell()` hard contract (sim space, MeshBuilder, vertex colours, origin = pool floor)
- The same creature dead, sunk in its pool. The walkable collision is **fixed**: four boxes half (2.8, 1.2) at yaw
  0/45/90/135° at **y = 2.2** (a 16-gon: 2.8 m on the axes, 3.03 m between) plus a solid circle r 3.6 below it.
  So: the carapace top must be **flat-ish at y = 2.2 out to r ≈ 3.05** (keep everything that sticks up within
  ~0.15 m of 2.2 inside r 3.0: plate seams, low knobs only; spikes / keel spines only at r > 3.0 or flattened), the
  dome skirt drops from there to about r 3.6–3.9 at the sludge (the sludge surface is ≈ 0.8 m; the bottom may go
  below 0).
- Dead read: the head and neck lie slumped out of the front (+Z) onto the sludge, jaw and mandibles hanging open, the
  eye dull (no glow: a dark grey-violet). Legs splayed limp sideways, half sunk, claws curled. The three sacs are burst:
  torn flat husks on the crown (flat, ≤ 0.12 m). A rotting flank on one side with exposed pale ribs / bone. Some
  sludge drips and algae at the waterline. Chitin colours as the living one.
- ≤ 5,000 tris.

## When done
Write `unity/codex/results/24_shell_host_zerg_turtle.md`: what each model looks like, the new leg pivot positions,
triangle counts, anything you could not do.
