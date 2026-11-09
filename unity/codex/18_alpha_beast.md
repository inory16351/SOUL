# JOB_ID: 18_alpha_beast — remodel 무리의 우두머리 into a menacing scrap beast (near-boss scale)

## File you may edit
- `web-proto/src/enemy_models.js` — **only** `createHoundAlpha()` and new private helpers it needs (prefix them
  `alpha…`). Do not touch any other function. NOTE: the file uses CRLF line endings — keep them.

Do not run the exporter; Claude exports and builds the Unity prefab.

## User feedback driving this (2026-11-09)
- "짐승의 위협적인 느낌을 내봐. 지금 모델링 파츠가 너무 적고 직선적이라 이질적임" — the current version (Claude's
  quick pass) is too boxy, too few parts, too many straight axis-aligned pieces; it looks alien, not like a beast.
- "크기 거의 메인 보스보다 조금 작을 정도로, 압도적인 위압감, 엘든링처럼" — it is an Elden-Ring-style field boss: Unity
  will scale it ×5, so it ends up ~7.5 m nose-to-rump, ~3 m at the shoulder, the head ~3.5 m up.
- It must read as kin to the Z0 boss `createAshWarden()` (the scrap beast: welded plates, engine parts, cables,
  rust and soot) — read that function first and reuse its vocabulary and helpers' style. It is "a dog made of
  scrap parts": a huge hunting hound / wolf, not an insect and not a robot dog toy.

## Design (silhouette first — it is seen from a ~55° top-down camera at 10–30 m)
- **Pose = threat**: front-heavy and hunched. Massive raised shoulders / withers (a hump of layered plates and a
  mane of cables, chains and bent rebar spikes sweeping backwards), the **head carried low and forward** below the
  shoulder line, lean tucked waist, powerful haunches. Like a wolf stalking / about to lunge.
- **Curves, not boxes**: build the spine, neck, ribs and tail as **chains of short tilted segments** that follow
  arcs (no long straight rods, no axis-aligned slabs). Plates overlap like scales/armour shingles at varied angles.
  Use tapering (`rod` start/end radius), `fieldHoop`/torus arcs, rotated `plate` profiles, pebbles for joints and
  rust lumps. Asymmetry is good (a torn ear, one missing plate, an exposed piston on one side).
- **Head**: long wolf skull of welded, overlapping plates tapering to the snout; deep-set glowing eyes under a
  heavy brow ridge (the `eye` glow mesh; may be a pair inside one mesh is not possible — use one wide slit or put
  the eye glow in one socket and a dim second lens as geometry); swept-back bent-fin ears; the hinged `jaw` long
  with jagged scrap fangs, top and bottom; drool-like hanging cables/wires.
- **Neck**: thick, made of stacked ring segments / pistons and cables; the `throat` resonator (a ring of cracked
  pipes with the `throat` glow mesh inside, `throat.visible = false` — Unity lights it while it howls) sits under
  the neck, visible from the side/front.
- **Body**: ribcage of curved pipe ribs over an engine core (the `core` glow sits deep inside the chest, seen
  through gaps), radiator and exhaust pieces worked in as anatomy (exhaust stacks rising from the shoulders like
  spines, smoking-looking), car panels / tyre fragments as armour patches, hanging chains, a few ash-grey rags.
- **Legs**: thick, muscular silhouettes (pistons + plate "muscles" + cable tendons), dog-like joint angles
  (forelegs straight-ish with a wrist, hind legs with a strong hock), big splayed paws with long blade claws.
- **Tail**: long segmented whip of vertebra-like scrap, curving down then up.
- Palette: the file's PALETTE (iron, rust, oil, edge, ash, bone, cloth); dark overall, rust accents; glow only on
  eyes, throat, the chest core (and nowhere else).

## Hard contract (Unity animates it procedurally by these names)
```js
return { root, radius: .75, height: .9, parts: { body, head, jaw, eye, core, legs, throat } };
```
- Built at the hound rig's scale (Unity size ×5): keep the overall body within about x ±.3, z −.85…+.85 (head and
  tail may extend a bit), the body pivot at y ≈ .5 so the paws reach y = 0, and `radius: .75, height: .9`.
- `body` = one pivot carrying everything except legs' own geometry; `head` pivot is a child of `body` (front, low:
  around [0, .12….2, .45….55]); `jaw` a child of `head`; `eye`, `throat`, `core` are `glow(...)` meshes.
- `legs`: exactly 4 entries, **in this order**: `[side -1, index 0]` front-left, `[side 1, index 0]` front-right,
  `[side 1, index 1]` rear-right, `[side -1, index 1]` rear-left; each `{ hip, knee, side, index }`; `hip` a child
  of `body` (front hips around z +.3, rear around z −.35, x ±.15….2, y ≈ 0 relative to body), `knee` a child of
  `hip` about .2–.25 below it; the lower leg + paw geometry hangs on `knee` and must reach y ≈ 0 in the rest pose.
  Unity swings `hip.rotation.x` (leg forward/back) and bends `knee.rotation.x` (front: negative, rear: positive)
  around these pivots — so keep the pivots at the anatomical joints and identity-rotated.
- All pivots identity rotation/scale, +Y up, faces +Z, origin on the ground. Flat-shaded low-poly through the
  file's `scrap(...)` batching (one batch per moving section).
- Budget: ≤ 9,000 triangles total.

## When done
Write `unity/codex/results/18_alpha_beast.md`: the design in a few lines, the parts tree (pivot positions),
triangle count.
