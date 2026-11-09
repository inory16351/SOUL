# SOUL / ALONE — Codex modeling job (shared rules)

You are modeling for the Unity 6 port of SOUL (working title ALONE): a top-down (~55° pitch) soulslike
on a ruined Earth. All geometry is generated in C# with the project's `MeshBuilder` (no external assets).
Repository root: `C:\Project\SOUL`. Unity project: `unity/`. Runtime code: `unity/Assets/SOUL/Runtime/`.

## Read first (do not edit)
- `unity/HANDOFF_UNITY.md` §3 "sim space" and the file map; §0 stages 2–4 for what these models are for.
- `unity/Assets/SOUL/Runtime/World/MeshBuilder.cs` — the API (Box, Cylinder, Beam, Rock, Disc, Quad, Tri,
  Push/Pop/Translate/RotateY/Scale, Jitter, Range/Chance, Hex). Colours are sRGB hex.
- Style references (same look you must match and extend): `World/RuinStructures.cs`, `World/RuinDressing.cs`,
  `World/RegionProps.cs`.

## Coordinate rules
- Author in **sim space** (three.js): +Y up, +Z forward, metres, right-handed. Never use Unity's
  `Quaternion.Euler/LookRotation/AngleAxis` — use `MeshBuilder` transforms (`RotateY` etc.).
- `MeshBuilder.Tri/Quad` expect counter-clockwise winding seen from outside; `Build()` converts to Unity.

## Style ("keep the current style, add detail" — user decision 2026-11-06)
- Low-poly, flat-shaded, vertex-coloured, muted ruined palette (see Regions.cs zone palettes).
- Add believable detail: broken / chipped edges instead of clean boxes, exposed rebar, soot and rust
  streaks (darker vertex colour bands), rubble and debris piled at the base, slight leaning, material
  variation between pieces, small secondary parts (pipes, bolts, cables, plates).
- **No signs, text, arrows, marker poles or UI-like glow strips.** A blocker must read from its form and
  material alone (an unclimbable ledge, cracked concrete, rusted plate, sludge, a chasm, a sealed door).
  Glow (`g`/glow builders, rendered unlit HDR) is allowed only where it is part of the object
  (anchor ring, cracks glowing like heat, sludge luminescence, door lamps) and should be sparing.
- Seen from above at ~10–25 m: make silhouettes and tops interesting, not only the side faces.

## Hard constraints
- Edit **only** the files listed in your job. Do not touch any other file.
- Keep every public method name and signature, every `PropCollider` / `PropSurface` and every dimension
  the job lists. Gameplay collision is defined elsewhere from those numbers; visible geometry must stay
  inside the stated footprints (nothing visible that the player can walk through, nothing invisible that
  blocks).
- Deterministic: randomness only from the `MeshBuilder` passed in / created with the given seed
  (`b.Range`, `b.Chance`, `b.Jitter`). No `UnityEngine.Random`, no time.
- C# that compiles in Unity 6 (C# 9): `using UnityEngine; using System.Collections.Generic;` only. Static helper
  methods must be `static` and private unless the job says otherwise. No LINQ.
- Budget: each gate / prop ≤ ~4,000 triangles; per-chunk border dressing ≤ ~6,000 triangles.
- You cannot run Unity here. Re-read your code carefully for compile errors (types, missing semicolons,
  name clashes, `Vector3` vs `Vector2`), because a compile error blocks the whole game.

## When done
Write `unity/codex/results/<JOB_ID>.md`: what you changed per method, rough triangle counts, and anything
you could not do. Keep it short.
