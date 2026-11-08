# JOB_ID: 12_scrap_beast — Z0 boss "재의 감시자" remodel: giant scrap-machine beast (web prototype)

## Files you may edit
- `web-proto/src/enemy_models.js` — **only** `createAshWarden()` and new private helpers it needs (keep the export name)

Do not run the exporter; Claude will export (`web-proto/tools/export-unity.js` already exports `ashWarden`) and
build the Unity prefab. Unity animates the parts procedurally by the names in `parts` below, so the contract
matters more than anything else.

## Design (user 2026-11-07)
A **huge beast built from random scrap machinery**, possessed by a hateful human soul.
- **Canine head**: a long wolf/jackal-like skull of welded plates, a separate hinged **jaw** with jagged
  scrap teeth, one or two glowing soul eyes (`eye` mesh, the existing glow material), ears made of bent fins.
- **Lean, sleek body** (not bulky): low-slung lizard-like torso of ribs, pistons, pipes and plates — a long
  spine that can bend side to side (lizard gait), a whip-like segmented tail of chain/vertebrae.
- **Blade-like claws** on the feet: long curved metal blades.
- **Three legs crawl** (front-left, front-right, rear-left), splayed sideways like a lizard (elbows/knees out).
- **The fourth limb (rear-right) is grotesquely twisted and huge**, grown into a giant arm that arches **up
  over the back and hangs above the body** (like a back-mounted mechanical arm, e.g. the extra arm of a
  hextech scientist, but an original design): thick twisted cables and pistons, multiple bent segments, ending
  in a large three-fingered **grabbing claw** — it picks up nearby scrap and hurls it (ranged attack).
- Random machine parts mixed in everywhere: gears, engine blocks, exhaust pipes, car panels, tyres, rebar,
  wires, a cracked furnace **core** glowing in the chest (`core`, existing glow material). Rust, soot, a few
  ash-grey rags caught on it. Muted ruined palette; glow only on eyes, core and a few seams.
- Size: body length ≈ 7 m nose to rump (tail extra ≈ 3 m), shoulder height ≈ 2.2 m when crawling; the raised
  arm reaches ≈ 6–7 m. `radius ≈ 2.4`, `height ≈ 3.2` (body, not counting the arm).
- It also **rolls into a ball / tumbling wheel to charge**: keep the silhouette able to curl (spine/tail
  pivots) — Unity drives the roll by rotating `pelvis`/`spine`; no extra geometry needed.

## Contract (return value)
```js
return { root, radius, height, parts: {
  pelvis, torso, chest, neck, head, jaw, eye, core,      // single pivots / meshes
  spine: [s0, s1, s2],                                   // pivots from pelvis forward to chest (side bend)
  tail: [t0, t1, t2, t3],                                // chained pivots, tail tip last
  legs: [ { hip, knee, ankle, claw, side: -1|1, index: 0|1|2 }, ... ],   // 0 front-left, 1 front-right, 2 rear-left
  arm:  { shoulder, upper, elbow, fore, wrist, fingers: [f0, f1, f2], grip },  // rear-right giant arm; grip = empty pivot in the palm where a thrown scrap chunk is held
} };
```
- Build with the file's helpers (`pivot`, `scrap`, `rod`, `box`, `plate`, `rag`, `glow`, `materials`), same
  coordinate conventions as `createWarden()`: +Y up, model faces **+Z**, origin on the ground under the body
  centre, all pivots identity rotation/scale (Unity sets rotations on top of the rest pose).
- Pivot names must be unique and match the keys (e.g. `pivot(..., 'hip0')` is fine; the importer stores the
  `parts` structure, so the key paths become `legs/0/hip`, `arm/fingers/1`, `tail/3`, …).
- Rest pose: crawling stance, legs splayed and planted (feet on y = 0), arm raised and hanging over the back,
  claw open above/behind the head.
- Budget ≤ ~12,000 triangles total. Flat-shaded low-poly like the other enemies.
- `side`/`index` are plain numbers on each leg object (the importer exports numbers as `Num(...)`).

## When done
Write `unity/codex/results/12_scrap_beast.md`: the parts tree (pivot positions), triangle count, anything
you could not do.
