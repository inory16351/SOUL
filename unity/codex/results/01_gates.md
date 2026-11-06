# 01_gates — gate models

Changed only `Runtime/World/GateModels.cs`, plus this requested report. Public signatures and collision/surface definitions are unchanged. Geometry uses the supplied deterministic builders, sim-space transforms and muted vertex colours. No signs or navigation glow strips.

| Method | Changes | Triangles, including glow |
|---|---|---:|
| `FreeCorridor` | Broken kerbs, low edge stones and worn shoulders; no geometry inside x ±5.5 m. | 964 |
| `Ledge` | Three chipped concrete courses, exposed bars, runoff, fractured paving and dirt inlays. Full cap at H; surface details rise at most 0.012 m. | 2,674 |
| `Ramp` | Closed slab wedge with aggregate side facets, irregular joints and cracks following the straight walking plane. | 142 |
| `Anchor` | Bolted shoe, gussets, tapered post and hollow forged eye centred on the supplied rope target; narrow luminous ring insets. | 512 each |
| `JetGap` | Uninterrupted dark opening, broken road lips, exposed bent reinforcement and dark edge shading; no glow. | 738 |
| `CannonWall` | Cracked concrete barricade, irregular crowns, rubble fragments, rebar, scrap repairs and small heat fissures. | 3,560 |
| `MagnetWall` | Continuous seven-metre steel hull, overlapping plates, ribs, rivets, worn paint and rust runoff; no glow. | 3,596 |
| `Sludge` | Flat liquid at y=0.03, mineral crust, bubbles, sparse luminous scum and six tilted half-sunk drums. | 2,962 |
| `LabDoor` | Permanent chipped frame, lintel conduit and two approach-side key lamps; removable pressure-door panels, ribs and locking dogs. | 3,268 |

Combined gates: ledge + ramp **2,816** triangles; ledge + two anchors **3,698**. Every complete gate remains below 4,000.

Validation: C# 9 compilation against installed Unity 6 reference assemblies and the cached project assembly passed. An external temporary harness using the actual MeshBuilder/GateModels sources with a managed Unity math shim checked 64 seeds per model, repeat determinism, footprint bounds, nondegenerate triangles, reversed ramps, 28,800 walking-plane samples and combined budgets. Inspected an offline depth-buffered preview. Harness/preview files remain outside the repository.

Limitations: Unity was not launched; in-engine lighting, gameplay and rope traversal still need validation. A genuinely excavated jet chasm cannot be completed within this file: `WorldStreamer.BuildGround` draws unbroken terrain beneath it. The gate therefore masks that ground with a dark opening and models the broken lips; a visible below-ground pit requires a separately authorized terrain cutout.
