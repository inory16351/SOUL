# JOB_ID: 05_boss_model — Z0 boss "재의 감시자 / WARDEN OF ASH" model (web prototype)

## Files you may edit
- `web-proto/src/enemy_models.js`
- `web-proto/tools/export-unity.js` (only the list of enemy models to export)

The Unity boss currently reuses the hollow warden at 1.6× scale (`EnemyType "ashWarden"` in
`unity/Assets/SOUL/Runtime/Combat/Enemies.cs`, do not edit). Enemy models originate in the web
prototype (three.js) and are exported to Unity.

1. In `enemy_models.js` add `export function createAshWarden()` next to `createWarden()`. It must return
   **the same contract** as `createWarden()`:
   `{ root, radius, height, parts: { pelvis, torso, head, eye, core, armL, armR, legL, legR } }`
   with `armL/armR = { shoulder, elbow, blade }` and `legL/legR = { hip, knee, ankle }` built by the same
   kind of `pivot(...)` hierarchy (pivot names identical to the warden's), because the Unity importer and
   animator find parts by those names. Build it at final size, about 1.6× the warden:
   `radius ≈ 1.52, height ≈ 4.32`, pivots placed proportionally, so the Unity side can use size 1.
   Reuse the file's helpers (`pivot`, `scrap`, `rod`, `box`, `plate`, `rag`, `glow`, `materials`).
   Look: a towering warden grown out of the ash ruins — scorched concrete and scrap plating over the
   frame, a cracked furnace core in the chest (the existing core part, brighter), a crown of rebar and
   broken antennae, ash-grey cloth rags, glowing seams at the **arm joints** (those joints will be the
   tear-off points of the stage-5 gimmick: make them clearly visible as separate armoured joint
   pieces). Original design; no copying of existing games.
2. In `tools/export-unity.js` add `['ashWarden', em.createAshWarden]` to the exported enemy list.

Do not run the exporter; Claude will run it and import into Unity.
