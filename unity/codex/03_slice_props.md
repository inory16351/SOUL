# JOB_ID: 03_slice_props — Z0 vertical-slice props

## File you may edit
- `unity/Assets/SOUL/Runtime/World/ZoneProps.cs` (only this file)

Replace the placeholder meshes with detailed ones. Keep every method signature and every
`PropCollider` exactly (positions, sizes); visible geometry must match those footprints.

| method | notes |
|---|---|
| `Grass`, `Flowers` | purified-zone ground life (appears after the zone boss is beaten): tufts, moss patches, small flowers; flat-ish (≤ 0.6 m), decor only, cheap (≤ ~400 tris) |
| `Pod` (+ `glow` out) | the scientist's charging pod / checkpoint: a capsule on a base with ribs, a back console, cables into the ground, soul-cyan light (`glow` mesh, unlit HDR). Must look hand-built by the dead scientist and be the warmest, most "safe" object in a dead zone. Colliders: circle r 1.5 at origin + rect (0, -1.55, .6, .25) — keep |
| `ArenaWall(a, out entranceHalfAngle)` | ring wall (radius a.radius + 1.5, thickness `ZoneAreas.WALL_T`) of ruined concrete around the boss arena; open at the entrance (`a.entranceYaw`, half angle asin(6.5 / r)) and at the rope ledge gap (toward `a.ropeSite`). Keep the segment / collider loop and collider sizes; enrich each segment (broken tops, window holes, rebar, scorch, rubble at the base inside and out), the two entrance pillars, and the cracked arena floor disc. Keep floor flat (y ≤ 0.05) |
| `FogGate(halfWidth, height)` | soul-fog sheet across the entrance while fighting (rendered additive unlit, colour from vertex colour): layered wavy translucent planes; stay within |x| < halfWidth, |z| < 0.6 |
| `Chasm(c, out glow)` | the rope-swing demo: a collapsed street gap (open |z| < c.halfWidth along |x| < c.halfLength, dark depth, broken road lips, hanging rebar) and a crane on one end (`mast` at x = -L - 2.5) whose boom carries the hook ring at (0, c.hookHeight, 0). Colliders unchanged |
| `RopeHook()` | rope arm hook head, ~0.35 m, points +Z, attaches at origin; claws + body |
| `Scrap()` | 기본 부품 pickup, ~0.45 m: a salvaged gear / servo chunk that is readable when small |
