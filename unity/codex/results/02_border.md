# 02_border

- `RidgeColor`: blends both sampled zone materials across the border; adds height-dependent soot/rust/moss runoff, dark strata and steep faces, and lighter weathered tops. Handles world-edge samples.
- `Dress`: deterministic coordinate seed, shared ~3 m height grid, passage-first placement, ridge-tangent/flared-spur alignment. Checks surrounding solid ridge samples, full-radius trail clearance and expanded corridor exclusions. Samples the surface for rubble, piers, roots and supports. Maximum 12 clusters, with a 700-triangle reservation against the 6,000-triangle budget.
- `Facade`: chipped, tilted floor stacks, open window bays, leaning piers, rebar, soot and fallen lamps (Z0/Z1).
- `Tower`: eight-metre fallen tower sections with exposed storeys and projecting girders (Z2).
- `Gorge` / `Foundry`: layered strata, wedged plate and open pipes; slag, hollow chimney stumps and broken conveyor frames (Z3/Z4).
- `Woods` / `Mire`: paired dead trunks, branches, roots and fallen logs; drowned trees, open rust tanks and sunken ribbed hulls (Z5/Z6).
- `Security`: broken concrete panels, crossed fence wires and an unlit camera (Z7).
- `Footing` / `Cliff` / `Slab` / `Pipe`: surface-seated rubble, taller edge teeth, chipped closed slabs, and hollow jagged rims. `Fits` / `Ground` handle placement and surface sampling; `V` / `C` are coordinate/colour helpers.

Triangle counts per cluster, including 80 rubble triangles: facade ~388; tower ~560; gorge ~312; foundry ~340; woods ~340; mire ~168–340; security ~536. Edge teeth add 40. Largest cluster ~600; chunk limit remains below 6,000. Ridge colours add no triangles.

Validation: standalone C# 9 compilation passed against the existing project assembly and Unity 6 CoreModule/netstandard references. Reviewed winding, transform pairs, projected bounds, deterministic RNG and triangle arithmetic. No colliders, surfaces, signs or glow added. Only the assigned source file and this requested report were changed.

Not verified: Unity rendering, current full-project compilation, gameplay traversal or the ≤2 ms chunk timing target (no Unity run). Placement uses conservative height samples rather than an exhaustive continuous-surface test.
