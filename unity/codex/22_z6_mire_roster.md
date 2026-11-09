# JOB_ID: 22_z6_mire_roster — unique models for the Z6 toxic mire roster (field, two elites, boss + its tentacle)

## Files you may edit
- `web-proto/src/enemy_models.js` — **add** eight new exported functions (and private helpers they need):
  `createSwarmBug()`, `createSludgeCrawler()`, `createSporeVine()`, `createDredger()`, `createGiantDredger()`,
  `createShellHost()`, `createMireMother()`, `createMireTentacle()`. Do not change the existing ones.
  The file uses CRLF line endings: keep them.
- `web-proto/tools/export-unity.js` — only the enemy list line: append
  `['swarmBug', em.createSwarmBug]`, `['sludgeCrawler', em.createSludgeCrawler]`, `['sporeVine', em.createSporeVine]`,
  `['dredger', em.createDredger]`, `['giantDredger', em.createGiantDredger]`, `['shellHost', em.createShellHost]`,
  `['mireMother', em.createMireMother]`, `['mireTentacle', em.createMireTentacle]`.

Do not run the exporter; Claude exports and builds the Unity prefabs. Read `기획서/Z6_독성늪_보스_생태계.md` (the zone,
roster, elites, boss) and `unity/Assets/SOUL/Runtime/Game/SoulGame.Mire.cs` (how they play), and the existing
`createAshMite`, `createHoundAlpha`, `createSentry`, `createExecutor` in `enemy_models.js` (the same contracts, done before).

## Theme
Z6 is the zone where **organisms eat machines**. Organic types are wet, swollen, sickly green / violet with glowing
veins and soft flesh; machine types are rusted, half-sunk, gnawed through, with organic growth (algae, sacs, roots)
eating into them. Contrast soft flesh vs riveted plates and bolts.

## Hard contract: rigs are animated procedurally in Unity by part names, and Unity scales each type
Build each model at the size of the rig it reuses (Unity applies `EnemyType.size`); keep the exact parts structure.

| function | rig (copy its contract exactly) | Unity size |
|---|---|---|
| `createSwarmBug` | `createCrawler` → `{ body, head, jaw, eye, core, legs: [{hip, knee, side, index}] ×6 }` | ×0.42 |
| `createSludgeCrawler` | `createCrawler` (same keys) | ×0.95 |
| `createShellHost` | **`createHoundAlpha`'s quad contract** (4 legs with `hip`, `knee`, `ankle`; plus whatever keys it has) | ×4.6 (rig `crawler`, quad) |
| `createSporeVine` | `createWarden` → `{ pelvis, torso, head, eye, core, armL: {shoulder, elbow, blade}, armR: {...}, legL: {hip, knee, ankle}, legR: {...} }` | ×0.8 |
| `createDredger` | `createWarden` (same keys) | ×1.3 |
| `createGiantDredger` | `createWarden` (same keys) | ×3.0 |
| `createMireMother` | `createWarden` (same keys) | ×3.4 |
| `createMireTentacle` | `createWarden` (same keys) | ×1.25 |

- Pivot positions stay close to the reused rig's (the procedural walk / attack poses assume them). Leg count, sides
  and indices identical. `eye` / `core` are `glow(...)` meshes (Unity recolours them per type).
- +Y up, faces +Z, origin on the ground, identity pivot rotations, flat-shaded low-poly, `scrap(...)` batching per
  moving section. Budgets: swarm bug ≤ 1,800 tris, crawler ≤ 3,500, vine ≤ 3,500, dredger ≤ 5,000, giant dredger
  ≤ 9,000, shell host ≤ 9,000, mother ≤ 11,000, tentacle ≤ 2,500.

## Looks (must read from the top-down camera ~55°, 10–25 m; no text)
- **떼벌레 swarmBug**: a tiny flying-looking gnawing insect: bulbous abdomen, translucent-looking wing plates held
  up (static meshes on the body), mandibles; the legs are thin. Green eye.
- **슬러지 크롤러 sludgeCrawler**: a polluted machine crawler that lies under the sludge: low, flat, dripping sludge
  sheets hanging from its plates, algae tufts, a spitting nozzle under the jaw. Yellow-green eye.
- **포자 덩굴 sporeVine**: a rooted organic turret: the legs are thick roots splayed into the ground, the torso a
  twisted trunk, the head a big swollen spore pod with a puckered mouth (it lobs spores), arms = whipping vines
  (`armL.blade`, `armR.blade` = vine whips). Clearly a plant-creature, not a machine.
- **녹슨 준설기 dredger**: a half-sunk dredging machine on stubby legs: a big round **intake mouth** on the front (a
  ring of teeth / a grille, the dark hole visible from the camera), a bucket arm (`armR.blade` = the bucket), holes
  gnawed through its hull with swarm nests (lumpy organic sacs) stuck to it.
- **거대 준설기 giantDredger** (elite): the same family but huge and more detailed: a giant intake funnel as the head,
  chains and hoses, two bucket / grab arms, a crane boom stump on its back, the hull eaten through in places with
  organic growth bulging out. It should feel like a dying industrial giant being eaten alive.
- **껍질 숙주 shellHost** (elite, the "turtle"): an evolved swamp organism like a giant snapping turtle crossed with a
  swarm host: a **huge domed, ridged shell** dominating the silhouette (most of it is above the sludge when the body
  sinks), barnacle-like growths and half-swallowed machine plates embedded in the shell, a heavy beaked head on a thick
  neck, short columnar legs with claws. **Leave the crown of the shell free** around local radius ~0.42 × body radius
  at the top: Unity adds three glowing poison sacs there. Violet eye.
- **늪의 어미 mireMother** (boss): the mother of the mire, an enormous evolved organism rooted in the swamp: a vast
  swollen body / head mass with a split vertical maw full of teeth (the weak spot is inside the maw: put the `core`
  glow deep inside the mouth), swallowed machines (tanks, pipes, a car door) half-embedded in its flesh, egg sacs
  along its flanks, glowing veins. Its "arms" read as two short thick tentacles, its "legs" as a root mass anchoring
  it (it never walks). It should dwarf the player and read as organic and ancient.
- **어미의 촉수 mireTentacle**: a single tall tentacle rising from the sludge: segmented, tapering, suckers, with a
  **glowing bulb at its root** (the `core` glow at the base: what the player cuts). The torso / arms / legs keys may be
  small or folded into the tentacle; keep the keys present.

## When done
Write `unity/codex/results/22_z6_mire_roster.md`: per model the parts tree (pivot positions), triangle count, and
anything you could not do.
