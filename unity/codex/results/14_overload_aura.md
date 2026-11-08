# 14_overload_aura

- Added `Soul.OverloadAura` with the exact API: `OverloadAura(Transform parent)`, `Update(float dt, Vector3 simPos, float strength)`, and `Dispose()`.
- The single additive dynamic mesh combines eight crossed, independently flickering red-orange flame tongues, an irregular breathing annular ground glow, sixteen rising embers, and deterministic 0.6–1.5 s heat crackles.
- Uses 360 persistent vertices and about 320 triangles in one draw call. Vertex and colour arrays are reused every frame; all randomness comes from a constructor-seeded `System.Random`.
- Strength is smoothed over 0.3 s in / 0.5 s out. Positions enter in sim space and are converted with `Sim.ToUnity`.
- No known omissions; Unity runtime visual tuning was not available in this environment.
