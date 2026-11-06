# 06_gates_fix — gate review fixes

Changed only `GateModels.cs` and this report. Public signatures and all collision/surface definitions remain unchanged.

| Method | Change | Triangles including glow |
|---|---|---:|
| `CannonWall` | Replaced tiled caps and medallion-like slabs with an uneven fractured ridge, deep crown notches, differently sized leaning concrete shards, upright torn scrap, exposed bent rebar, surface-following cracks and rubble on both faces. | ~2,700 (2,716 maximum across checked seeds) |
| `MagnetWall` | Shifted panel seams for widths within ±20%; bowed three panels and adjusted their cross ribs. Preserved the continuous hull, rivets and rust. | 3,596 |
| `Sludge` | Removed every disc/ring detail; added four irregular scum patches with small clusters of varied bubbles, two tiny polygonal glimmers, irregular drum stains and angular bung fittings. Kept the pool, crust and half-sunk drums. | 2,472 |

Private helpers: `Shard` builds closed, leaning notched fragments; `Scum` builds filled asymmetric polygons; `Facing` accepts an optional bow, zero for existing callers. Other public model methods are unchanged; the upright rope anchor ring is preserved.

Validation: C# 9 compilation against installed Unity 6 reference assemblies and the cached project assembly passed. The existing temporary managed-math harness ran the actual MeshBuilder/GateModels sources for 64 seeds per model: deterministic vertices/colours, footprint bounds, nondegenerate triangles, walking planes and combined budgets passed. Cannon bounds: x ±14, z ±1.79, maximum y 4.460; magnet: z ±0.9, y 7. Public signatures match; no Disc calls remain. Inspected offline depth-buffered views at approximately 55° pitch, including looking along the wall. Temporary checks/previews are outside the repository.

Limitation: Unity was not launched; final in-engine lighting and camera review remain outstanding. The prior jet-gap terrain limitation is unchanged.
