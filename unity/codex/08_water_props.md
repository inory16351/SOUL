# JOB_ID: 08_water_props — reeds and surface scum so pools read as water

## File you may edit
- `unity/Assets/SOUL/Runtime/World/WaterProps.cs` (only this file)

User feedback: in the toxic mire the water and the ground have similar colours, so water does not read.
Claude handles the water surface colour / shoreline darkening. You make the props that sit on shores and on the
water (placement is Claude's, in `WorldStreamer`; read `World/Terrain.cs` for water kinds:
`WATER_CLEAN` rain puddles Z0/Z1, `WATER_MIRE` Z6/Z5, `WATER_TOXIC` Z6 deep sludge, `WATER_OIL` Z4).

Implement `Reeds(zone)` and `Scum(zone)` per the header comment of `WaterProps.cs`:
- reeds / cattails clumps that clearly outline shores from above (varied heights, leaning, broken stems,
  seed heads; dead brown in Z5, sickly yellow-green in Z6, a few rusted rebar / pipes poking out in Z1/Z4
  puddles is fine)
- floating scum: irregular algae mats (mire), rainbow-dark oil film streaks (Z4, use a few colour bands),
  foam lines along toxic sludge edges (Z6); lying flat, no round discs
Keep the `(id, build)` return pattern (id unique per zone / kind), decor only, ≤ ~500 triangles.
