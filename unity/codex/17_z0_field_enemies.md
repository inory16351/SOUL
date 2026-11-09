# JOB_ID: 17_z0_field_enemies — unique models for the Z0 field roster + the alpha's lair pillar

## Files you may edit
- `web-proto/src/enemy_models.js` — **add** four new exported functions (and private helpers they need):
  `createAshMite()`, `createSentry()`, `createHoundAlpha()`, `createExecutor()`. Do not change the existing ones.
- `web-proto/tools/export-unity.js` — only the enemy list line: append `['ashMite', em.createAshMite]`,
  `['sentry', em.createSentry]`, `['houndAlpha', em.createHoundAlpha]`, `['executor', em.createExecutor]`.
- `unity/Assets/SOUL/Runtime/World/FieldModels.cs` — **add** `public static Mesh LairPillar()` to `CritterMeshes`
  (keep everything else in that file unchanged).

Do not run the exporter; Claude exports and builds the Unity prefabs. Read `기획서/Z0_생태계_엘리트_파츠.md` (roster,
elites, gimmicks) and `unity/Assets/SOUL/Runtime/Game/SoulGame.Elites.cs` (how the gimmicks read in play).

## Hard contract: rigs are animated procedurally in Unity by part names, and Unity scales each type up/down
Build each model **at the size of the rig it reuses** (Unity applies `EnemyType.size`); keep the exact parts structure.

| function | rig (copy its contract exactly) | build at | Unity size → final |
|---|---|---|---|
| `createAshMite` | `createCrawler` → `{ body, head, jaw, eye, core, legs: [{hip, knee, side, index}] ×6 }` | radius .75, height .9 | ×0.55 |
| `createHoundAlpha` | `createCrawler` (same keys) + extra `throat` | radius .75, height .9 | ×1.75 |
| `createSentry` | `createWarden` → `{ pelvis, torso, head, eye, core, armL: {shoulder, elbow, blade}, armR: {...}, legL: {hip, knee, ankle}, legR: {...} }` | radius .95, height 2.7 | ×0.85 |
| `createExecutor` | `createWarden` (same keys) | radius .95, height 2.7 | ×1.4 |

- Pivot positions should stay close to the reused rig's (the procedural walk/attack poses assume them): copy the
  crawler / warden pivot layout and change the geometry hung on it. Leg count, sides and indices identical.
- `eye` / `core` are glow meshes made with the existing `glow(...)` helper (Unity recolours them per type).
- Same conventions as the file: +Y up, faces +Z, origin on the ground, identity pivot rotations, flat-shaded low-poly,
  muted ruined palette, `scrap(...)` batching per moving section. Budget: mite ≤ 2,500 tris, alpha ≤ 6,000,
  sentry ≤ 5,000, executor ≤ 8,000.

## Looks (must read from the top-down camera ~55°, 10–25 m; no text or UI-like markers)
- **재 진드기 ashMite**: a small swarming tick/louse machine made of a dented can or bulb body, needle legs, a
  pincer/proboscis jaw, a jaundiced yellow eye. Reads as vermin, not a mini hound.
- **무리의 우두머리 houndAlpha**: the pack leader — heavier and taller than a scrap hound, a raised spine crest of
  bent rebar and plate "hackles", scarred mask with broken fangs, chains hanging from the neck. Its **throat** is an
  exposed resonator (a ring of cracked pipes around a glow mesh) — `throat` = a `glow(...)` mesh under the jaw (Unity
  shows it only while it howls). Orange-red eye.
- **녹슨 파수꾼 sentry**: a rooted turret-sentinel: heavy base (the legs read as anchoring struts / tripod
  feet), a hunched torso carrying a long rusted barrel on one arm (`armR.blade` = the barrel, pointing forward) and a
  short hammer/plate on the other; a hooded sensor head with a yellow lens. Should read "it shoots" at a glance.
- **부서진 집행자 executor**: a towering broken executioner. **Front = armour**: a thick riveted breastplate /
  shield-like chest slab and shoulder plates (looks impenetrable from the front). **Back = exposed**: the back of the
  torso is torn open, cables and a cracked housing; put the `core` glow there, on the **back** (−Z side) of the
  torso, clearly visible from behind and hidden from the front. Long heavy arm blades (`armL.blade`, `armR.blade`),
  one arm visibly broken/reinforced with clamps (the arm that sticks in the ground after the slam combo).
- Palette notes: elites slightly more detailed and darker; mite/sentry simpler.

## `LairPillar()` (Unity, `FieldModels.cs`)
A cracked concrete column standing in the alpha's lair (the alpha crashes into it): ~0.9 m radius at the base,
~3.4 m tall, a broken top with bent rebar, chipped edges, a few cracks showing it is already weak, rubble at its
foot. Sim space, origin on the ground at its centre, `MeshBuilder`, ≤ 900 tris, collision radius stays 0.95 m (keep
the visible base inside ~1.0 m).

## When done
Write `unity/codex/results/17_z0_field_enemies.md`: for each model the parts tree (pivot positions), triangle count,
and anything you could not do.
