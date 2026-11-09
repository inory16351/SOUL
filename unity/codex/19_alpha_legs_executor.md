# JOB_ID: 19_alpha_legs_executor — realistic dog legs for 무리의 우두머리 + menace pass on 부서진 집행자

## File you may edit
- `web-proto/src/enemy_models.js` — only `createHoundAlpha()` (and its `alpha…` helpers) and `createExecutor()`
  (add new `executor…` helpers for it; **do not change `fieldBipedLeg` / `fieldBipedArm`** — `createSentry` uses them:
  write executor-specific copies instead). The file uses CRLF line endings — keep them. Do not run the exporter.

Read first: `unity/codex/18_alpha_beast.md` + `results/18_alpha_beast.md` (your previous pass on the alpha),
`createAshWarden()` (the Z0 boss: style reference), `기획서/Z0_생태계_엘리트_파츠.md` (elite gimmicks).

## Part A — 무리의 우두머리: legs like a real wolf / big dog
User: "좀 더 다리가 사실적으로 기울어져야 할 거 같은데" — the legs hang too straight / vertical. Rebuild the four legs
with true canine anatomy and **three segments**, adding an `ankle` pivot (Unity will animate it):
- **Fore leg**: shoulder (`hip`) high on the body → upper arm angled **down and back** to the elbow (`knee`) which sits
  **behind** the shoulder → forearm nearly vertical down to the wrist (`ankle`) → short pastern angled **forward** to a
  big paw. Elbow tucked close to the ribs.
- **Hind leg**: hip (`hip`) → thigh angled **down and forward** to the stifle (`knee`) → long shin angled **down and
  back** to a high hock (`ankle`) → long metatarsus angled **down and slightly forward** to the paw. Clear Z-shaped
  zig-zag silhouette from the side; heavy thigh mass.
- Suggested pivot offsets in the base scale (Unity ×5), parent-local, body pivot at y ≈ .5:
  fore: hip (±.17, 0, +.30) · knee (±.01, −.17, −.06) · ankle (0, −.21, +.03) · paw contact ≈ (0, −.11, +.05)
  hind: hip (±.17, 0, −.35) · knee (±.01, −.16, +.09) · ankle (0, −.17, −.13) · paw contact ≈ (0, −.16, +.04)
  All four paws must touch y = 0 in the rest pose. Muscle mass: pistons + layered plates + cable tendons along each
  segment (thicker high, slimmer low), splayed blade-clawed paws, dew-claw spurs at the wrist/hock.
- Contract change: `legs` entries become `{ hip, knee, ankle, side, index }` — same order (FL, FR, RR, RL), `hip`
  child of `body`, `knee` child of `hip`, `ankle` child of `knee`, paw geometry on `ankle`. Identity pivots. Keep
  everything else of the alpha (body, head, jaw, eye, core, throat; return radius .75, height .9), only adjust the body
  where the new shoulders / thighs attach. Total budget ≤ 10,000 tris.

## Part B — 부서진 집행자: same treatment as the alpha (user: "집행자도 ㄱㄱ")
It is a 6.5 m tall field boss (Unity ×2.4). Make it menacing, layered and **less straight / boxy**:
- Silhouette: hunched heavy executioner — massive rounded shoulders (stacked curved pauldron plates with spikes), head
  sunk forward between them under a horned / crested helm with a slit visor, broad chest; long arms dragging two huge
  curved execution blades (chipped, notched, one arm visibly broken and clamped/reinforced).
- **Front = armour** (layered curved breastplate shingles, rivets, chains of executed machines' parts — skulls of
  scrap hounds, torn plates — hanging as trophies). **Back = exposed core**: the back housing torn open with bent
  ribs/cables around the `core` glow (on the back, −Z, visible from behind only) and exhaust stacks rising beside it.
- Tattered cloth: a hood/mantle over the shoulders and a long ragged tabard/loincloth, a few strips trailing.
- Legs: heavy, digitigrade-ish armoured legs with spiked knee guards and splayed sabatons (executor-specific copies of
  the biped helpers; keep the same pivot positions as `fieldBipedLeg` so the warden walk still lines up).
- Use curves: chains of short tilted segments, rotated plates, tapered rods, torus arcs; no long axis-aligned slabs.
- Contract (unchanged): `{ pelvis, torso, head, eye, core, armL: {shoulder, elbow, blade}, armR: {…}, legL: {hip,
  knee, ankle}, legR: {…} }`, radius .95, height 2.7, same pivot positions as now (the warden rig animates them).
  Budget ≤ 10,000 tris.

## When done
Write `unity/codex/results/19_alpha_legs_executor.md`: the new leg pivot table, the executor parts tree, triangle
counts, anything you could not do.
