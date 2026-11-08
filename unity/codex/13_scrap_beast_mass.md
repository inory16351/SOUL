# JOB_ID: 13_scrap_beast_mass — give the Z0 boss real mass (follow-up to 12)

## Files you may edit
- `web-proto/src/enemy_models.js` — **only** `createAshWarden()` and its private helpers

## Problem (user review 2026-11-07, in-game captures)
Job 12's beast reads as a **thin skeleton / stick model** from the top-down camera: gaps between the spine
segments, pencil-thin legs and arm, a small boxy head. It must read as a **huge, heavy scrap beast**.
Reference: the attached **key art** (`기획서/art/boss_scrapbeast.png`, the project's own concept image). The user
wants the in-game model to **feel exactly like the key art**: same silhouette, proportions, part layout and
palette — wolf skull with swept bone horns and open toothed jaw, ribcage barrel around the glowing core, engine
block with three exhaust stacks on the back, armoured legs with big bone-white blade claws, the giant segmented
arm arching up over the back ending in a big three-claw grabber, heavy chain tail, rusted iron + bleached
plates. Translate it faithfully into low-poly flat-shaded vertex colour (like the other enemies); judge it from
a top-down ~55° view like the key art's camera.

## Do
- **Keep the parts contract and pivot hierarchy of job 12 exactly** (same keys, same parents, pivots at the same
  positions ± 10 %, identity rotations). Unity already animates these; only the meshes change.
- Body: continuous, overlapping armour/scrap around every spine segment (no visible gaps when the spine bends
  ±25°): a ribcage barrel of curved rusted plates and bars around the glowing furnace **core** (visible through
  the ribs), engine block with 3 exhaust stacks on the back, gears, cables. Width ≈ 1.8–2.2 m at the chest.
- Head: a big long **canine/wolf skull** (≈ 1.6 m long) of welded plates, swept-back blade ears/horns, a heavy
  hinged **jaw** with rows of jagged teeth (open-able), glowing soul eyes (`eye`).
- Legs: thick armoured upper/lower segments with piston rams and joint drums; **large blade claws** (3–4 per
  foot, 0.6–0.9 m) as on the reference.
- **Giant twisted leg (the `arm` parts) — this overrides the key art**, where it looks like a scorpion tail. The
  user (2026-11-07): it must NOT read as a tail; it is **the fourth leg, grotesquely twisted and hanging up above
  the body**. So:
  - It must still read as a **leg of the same kind as the other three** — same thigh/shin armour language, a
    knee drum, an ankle, and a **foot with the same bone-white blade claws** (the `fingers` are those toes, just
    overgrown so they can grab) — not a smooth segmented arc.
  - **Grotesque**: overgrown 2–3× thicker than the other legs, swollen with fused scrap, the thigh **twisted
    around its own axis** (visible spiral of plates and cables), the knee bent the wrong way (hyperextended), a
    mangled extra joint, torn cables and pistons hanging loose, mismatched parts welded on.
  - **Hanging up**: from the rear-right hip it wrenches up and forward over the back, and the shin and foot
    **dangle / hang down** from the high knee like a limb hung on a hook (the foot below the knee, toes curled),
    over the body or beside the head — not an even arch ending in a pincer. Keep the pivot positions of job 12
    (± 10 %), only the geometry around them changes.
  - Thickness 0.6–0.9 m, toes/claws 1.0–1.3 m.
- Tail: heavy chain links (as in the reference) from the existing tail pivots.
- Colour: rusted iron, bleached bone-like plates for claws/teeth/horns, dark gaps; glow only eyes, core, a few
  arm/chest seams (cyan-blue soul colour as now; Unity recolours red in phase 2).
- **Dormant pose friendliness**: Unity will fold the beast into a curled sleeping pose (legs tucked under the
  body, head resting on the forelegs, arm folded down over the back, tail wrapped around). Keep armour pieces
  attached to their own pivots so nothing floats or tears when joints fold; avoid geometry that would poke far
  through the body when legs fold under it.
- Budget ≤ ~14,000 triangles.

## When done
Write `unity/codex/results/13_scrap_beast_mass.md`: what changed per part, triangle count, confirm the contract
is unchanged.
