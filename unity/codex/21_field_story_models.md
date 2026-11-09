# JOB_ID: 21_field_story_models — survivor holograms, recorder, hidden merchant, locked depot, burrow

## Files you may edit
- `unity/Assets/SOUL/Runtime/Game/SoulGame.Lore.cs` — **only** the `RecordModels` class at the bottom.
- `unity/Assets/SOUL/Runtime/Game/SoulGame.Field.cs` — **only** the `FieldPropModels` class at the bottom.
Keep every method name / signature; replace the bodies (Claude placeholders). Read the rest of both files (how the meshes
are used: materials, scales, glow) and `기획서/Z0_퀘스트_동선.md` (the decisions). Do not touch other code.

## Context
Z0 now has survivor records (dead human survivors' holograms that speak when you walk up), a hidden merchant, sealed
depots beside sentry turrets, and burrows where rescued prey run. Top-down camera (~55°, 10–25 m). Low-poly flat-shaded,
vertex-coloured ruined palette (see `World/RegionProps.cs`, `World/FieldModels.cs`).

## Meshes
- `RecordModels.Recorder()`: a dented hand-held recorder / hologram emitter puck lying in the rubble (~0.35 m), with a
  short bent antenna and a small lens on top (the lens may be a light vertex colour; the hologram rises from it).
- `RecordModels.Hologram(int build, float height)`: a standing human silhouette, rendered with ONE additive cyan unlit
  material (no vertex colour used), alpha flicker driven by the code. build 0 = slim adult, 1 = stocky adult (soldier /
  mechanic), 2 = child (height ~1.25 m). Make it read as a person at a glance from above: head, shoulders, arms slightly
  apart, a recognisable stance (a hand raised to the ear or clutching something is fine), clothes edges (coat hem,
  backpack or bag) — and horizontal **scanline gaps** (thin missing slices every ~0.12 m) so it reads as a hologram.
  ≤ 700 tris each. Origin on the ground at the feet, faces +Z.
- `FieldPropModels.Burrow()`: a dark burrow mouth dug under a slab / pipe end, scattered earth and a few bones (~1.2 m).
- `FieldPropModels.Depot()`: a sealed military supply container (~1.6 × 1 × 1 m before the code's ×1.5 scale): ribbed
  steel, stencil-less, a heavy hasp at the front centre around (0, .55, .53) where the lock glow sits, bolted feet, a
  tarp corner, rust. `FieldPropModels.DepotLock()`: the lock plate only (unlit glow material, red/green set by code),
  matching that hasp.
- `FieldPropModels.Merchant()`: a gentle, hunched old scavenger machine sitting among its wares (~1.4 m tall seated):
  round patched body, a soft face visor, long careful arms, a backpack frame stacked with parts, a mat with laid-out
  parts and two crates; friendly, not threatening. `FieldPropModels.MerchantLantern()`: a small lantern bulb only
  (unlit warm glow material) hanging from a pole on the backpack, positioned to match the merchant mesh.
- Budgets: ≤ 1,200 tris for the merchant, ≤ 600 each for the rest. Deterministic MeshBuilder seeds. No text.

## When done
Write `unity/codex/results/21_field_story_models.md` (what each mesh looks like, triangle counts).
