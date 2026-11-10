# 11_boss_pillar

- `Pillar(out Mesh glow)`: replaced the placeholder with a torn concrete footing, cracked slab fragments, exposed rebar, split bolted armour, soot/rust bands, clamps, four paired cable bundles and an open capacitor crown. Eight shaft seams and exposed capacitor rings use strictly white glow vertices for runtime tinting. Body: **1,992 triangles**; glow: **448**; combined: **2,440**.
- `ScrapChunk()`: engine casting and sump, folded car panels with worn paint, hollow tyre/rim, tread blocks and bent rebar. **564 triangles**, maximum radial reach 0.794 m, centred approximately at the origin. Existing cache retained.
- Added private mesh helpers `ArmourPlate`, `FloorShard`, `Sleeve`. `RopeArm` and all public signatures remain unchanged. No gameplay files changed.
- Validation: C# 9 compilation against installed Unity 6.0 (6000.3.19f1) assemblies passed. An offline geometry harness using the project's mesh construction and Unity managed math passed repeat determinism, white glow, triangle budgets, footprint and nondegenerate-triangle checks. The harness substitutes mesh storage and skips colour-space conversion; this is not a Unity runtime test.
- Measured pillar: y **-0.10 to 4.18 m**; radius above y 0.35: **0.720 m**; debris radius **1.214 m**; geometry outside r 0.75 stays at or below **0.193 m**.
- Not completed: Unity editor compilation, fight/play-mode checks and blue/red captures. Unity MCP access was rejected because it requires approval while this session's approval policy is `never`. No substitute screenshots were saved as gameplay captures.
