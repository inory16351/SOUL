# JOB_ID: 11_boss_pillar — Z0 boss energy pillar

## File you may edit
- `unity/Assets/SOUL/Runtime/World/BossPillarProps.cs` (only this file)

The Z0 boss (재의 감시자, a hollow warden machine) is fought in a walled concrete arena. During the fight
energy pillars burst out of the ground around it (3 in phase 1, 4 in phase 2), their lights come on one by one,
and the player must break them in that order (phase 2: reverse order) with three hits each, or the boss drains
their energy. The pillars are the dead scientist's power conduits that keep the machine running: industrial,
not magical.

Replace the placeholder in `BossPillarProps.Pillar(out Mesh glow)`:
- Body (lit, vertex colour): a heavy conduit pylon — concrete or cast-iron foot that looks like it tore up
  through the arena floor (broken slab chunks / cracked plates around the base), an armoured shaft with
  clamps, bolted panels, cable bundles running down into the ground, a capacitor head on top. Weathered,
  soot and rust bands like `RuinStructures.cs`. Must read clearly from the top-down camera (strong top silhouette:
  the capacitor head and the base debris are what the player sees most).
- Glow (white vertex colour; the game tints it blue in phase 1 and red in phase 2 and pulses it): vertical
  energy seams on the shaft (3–4 visible from any side), a core / coil in the head. This is how the player sees
  which pillar is lit, so the glow must be visible from above and from every side. No numbers, symbols or text.
- Breakable look is welcome (seams between armour plates) but the mesh is one piece; breaking is handled by
  the game (it removes the pillar with a particle burst).

Also replace `BossPillarProps.ScrapChunk()`: the lump of scrap the boss (a scrap-metal beast with one giant
hanging arm) tears off the ground and hurls at the player. Crushed car body / engine block / tyre / rebar
fused into one ~1.6 m lump, origin at its centre (it tumbles in flight), ≤ ~600 triangles. Same palette.

## Keep (gameplay numbers live in `Game/BossPillars.cs`)
- Signature `public static Mesh Pillar(out Mesh glow)`; return the body mesh, `glow` = glow mesh.
- Origin at the ground centre. The pillar rises out of the ground at runtime (moved down by ~4.2 m), so
  everything must be above y = 0 except base debris, which may sink to y = -0.3.
- Body footprint inside radius 0.75 up to 2 m height (collider circle r 0.75); base debris may spread to
  r 1.3 but must stay below 0.35 m (walkable look). Hit capsule r 0.6 from y 0.2 to 3.6 — the shaft
  should fill it. Total height 3.8–4.4 m.
- ≤ ~2,500 triangles body + glow.

## Check
- Compile, then in play mode run the fight (HANDOFF §0 "Z0 보스 에너지 기둥" has the test steps) and capture
  `unity/Captures/codex_boss_pillar.png` with lit (blue) pillars, and one in phase 2 (red).
