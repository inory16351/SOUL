# 09_shrine

- `Shrine(out Mesh glow)`: replaced the placeholder with a concrete plinth, three spreading broken fins, a hollow containment ring, floating core, rusted clamps/bolts, rubble, six cables and three small cable lamps. Preserved mesh names, footprint metadata (3 m), and the exact origin circle collider (r 1.6); no surfaces added. Geometry is approximately 7.46 m tall, with conservative height metadata 7.6 m. Crown radius stays below 2.4 m; geometry below standing head height stays inside r 1.6.
- `Fin`: chamfered, leaning sections with chipped crowns, dark weathering bands, rust trails and bent exposed rebar (148 triangles each).
- `Ring`: closed annular geometry with an open centre (8 triangles per segment). `Radial`: shared sim-space coordinates.
- `Cable`: six-segment, sagging five-sided cable (120 triangles each).
- `Core`: asymmetric faceted luminous core and dark retaining cage (144 lit + 24 glow triangles). Glow elsewhere is limited to short ring seams and cable lamps.
- Budget: approximately **3,216 lit + 132 glow = 3,348 triangles**, calculated from primitive counts. Seeds 404/405 retained; randomness uses MeshBuilder only.
- Validation: C# 9 compilation against installed Unity 6000.3.19f1 assemblies with the actual `MeshBuilder.cs` and `Sim.cs` passed; winding, transforms, bounds and whitespace reviewed. Unity rendering/gameplay could not be run here; camera readability and bloom still need in-engine inspection.
