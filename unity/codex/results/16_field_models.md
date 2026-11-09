# 16_field_models

- `Rat()` — faceted ash-brown body (~0.36 m nose to rump), crouched paws and haunches, large fleshy ears, pale eyes, swept bone scutes and a long bent tail. **352 triangles**.
- `Crate()` — dented, bevelled olive case with worn lid rails, rust runoff, two latches, recessed loop handles, rear hinges, retaining strap/buckle and small base fragments. **591 triangles**. Conservative total bounds: **0.998 × 0.691 × 0.693 m (X/Y/Z)**, including attachments/debris.
- `CrateGlow()` — two short exposed seal windows and a small top status lamp; shared seam/lamp coordinates align with the body recesses. **36 triangles; crate total 627 / 900**.
- `PartDrop("leap")` — bent knee fork, transverse axle, piston with three open spring turns, mounting cuffs and torn cables. **448 / 500 triangles**, maximum axis extent ~0.435 m. Unknown/null kinds use this same shape.
- `PartDrop("skill:wave")` — open octagonal emitter bezel with a missing sector, three separated lens shards, six cooling fins and a broken connector/cable. **204 / 500 triangles**, maximum axis extent ~0.436 m. Physical gaps preserve detail under the caller's uniform gold glow.

Validation: compiled `FieldModels.cs` with the unchanged `MeshBuilder.cs` and `Sim.cs` against installed Unity 6000.3.19f1 references using C# 9, with no compiler errors. A temporary offline primitive-count/bounds harness confirmed budgets, conservative footprints, nondegenerate custom triangles and fallback branches. Manually checked custom winding, shared glow positions and fixed seeds (77–80). Public signatures preserved; no gameplay/collision files changed.

Remaining: Unity import, actual mesh/render inspection and readability at the gameplay camera were not run. Bounds above are conservative primitive envelopes, not Unity-rendered measurements.
