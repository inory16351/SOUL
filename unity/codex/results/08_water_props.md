# 08_water_props

- `Reeds(zone)` / `BuildReeds`: 8-9 bent stems in split root clumps, folded two-sided leaves, snapped tips, cattail heads, dark wet bases and fallen blades. Dead straw-brown in Z5; sickly yellow-green in Z6; a thin rusted scrap rod in Z1/Z4. Approximately 372-494 triangles (494 maximum).
- `Scum(zone)` / `BuildScum`: torn algae rafts with open water gaps; Z6 adds dirty pale foam ribbons; Z4 uses five muted dark iridescent bands in broken oil streaks. Sparse rafts/foam in clean puddle zones. Z5: 27-33 triangles; Z6: 73-79; Z4: 210; other zones: 41-45.
- Helpers: `Blade`/`TwoSidedTri` give leaves folded ridges; `Raft`, `Foam`, and `OilFilm` use upward-wound flat strips, never discs. Seeded MeshBuilder randomness only.
- Preserved public signatures, cache IDs, footprints (.8 / 1.2 m), height metadata (1.5 / .05 m), and empty collision/surface lists. Geometry stays within those radii; scum is y=.012-.024 m. No glow.
- Validation: C# 9 compilation against local Unity 6 and project assemblies passed with warnings treated as errors; diff whitespace check passed. Reviewed winding, conservative bounds and worst-case triangle counts. Unity runtime/render inspection was not available; placement and water/shore colours remain Claude's work.
