# 10_slab

- `ConcreteSlab`: kept the original run/width/rise/yaw and `Flight` ramp, then added a torn thick fracture face, chipped corners/edge bites, 2–3 slope-following cracks, a muted soot band, underside shadow, low/high-end rubble bearings, and 5–7 bent 0.05–0.07 m rebar rods. All ordinary top detail stays within 0.13 m of the ramp; only the raised fracture/rebar rises higher.
- `Fragments`: replaced the full grid with deterministic rejection-sampled scatter, rotated and varied shard sizes, sampled every corner from the supplied surface, retained round clipping, and reduced brick-red fragments to 4%. A slab now receives about 3–6 loose pieces; a 13×13 mound receives about 17.
- Rough slab budget: about 550–700 triangles depending on seed (well below 2,500). Shared fragments cost about 10 triangles each, plus rare small rebar.
- Could not run the Unity editor here; performed a focused syntax, winding, footprint, and diff review.
