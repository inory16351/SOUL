# JOB_ID: 09_shrine — zone skill shrine pillar

## File you may edit
- `unity/Assets/SOUL/Runtime/World/ShrineProps.cs` (only this file)

Each zone has one skill shrine (스킬 기둥): the player resonates with it to take the zone core, which reveals the
zone map and unlocks pod warps; later the skill tree is opened there. Role like Genshin's statues, design 100 %
original. Replace the placeholder in `ShrineProps.Shrine(out glow)`: an ancient-looking but scientist-built
pillar — weathered stone / concrete fins, cables and a containment ring around a floating core (glow mesh:
core + rune seams + a few cable lights). Tall (6–8 m), readable from the top-down camera (strong top silhouette),
footprint radius ≤ 3 m, keep the collider (circle r 1.6 at the origin). ≤ ~3,500 triangles.
