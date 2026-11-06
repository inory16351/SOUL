# 07_ground_detail

Implemented `GroundDetail.Decals` without changing its public signature or any collision data.

| Method | Detail / triangles per cluster |
|---|---|
| `Decals` | Chunk-seeded placement, separate layout RNG, terrain draping, zone selection, sparse trails and purification. At most 36 clusters; 1,500-triangle guard. |
| `Clear` | Rejects arena-overlapping footprints; nine footprint samples exclude ridges above 0.2 m and water deeper than 0.1 m. |
| `Ruins` | Branching cracks, ash flakes, buried chipped kerbs; 24–26. |
| `City` | Asphalt repairs, seams, faded lane-paint fragments or rust; 24–30. |
| `Glass` | Grouped folded glass shards and a fallen frame strip; 23. |
| `Scrap` | Angular cable run, rust stain, square-headed bolts; 38. |
| `Foundry` | Slag flakes, surface cracks, sulfur crust and bolt; 36. |
| `Woods` | Branching roots/twigs and folded leaf litter; 28. |
| `Mire` | Angular wet sheen, algae and flattened reeds on banks; 30. |
| `Lab` | Flush slotted drains or worn floor-paint fragments; 30. |
| `Trail` | Route-aligned broken wheel ruts / worn footpath streaks; 8–16. |
| `Shoots` | Moss patches and six low green shoots; 30. |
| Helpers | `Patch` 6, `Strip` 2, `Crack` 12, `Bolt` 6, `Leaf` 4 triangles; `V`/`C` construct vectors/sRGB colours. |

Typical unobstructed 32 m chunks: roughly 730–1,200 triangles; absolute current maximum 1,368. Purification replaces 40% of eligible off-trail clusters with shoots, removes 30%, and retains 30% dead detail. Layout positions remain stable.

All shapes fit a 1.75 m placement radius. Vertex relief is 0.012–0.14 m **above terrain**: absolute world Y must follow `Terrain.Height` because the streamer attaches the mesh at world origin. No round shapes, glow, signs, or collision added.

Validation: standalone C# 9 compile against installed Unity CoreModule and project Assembly-CSharp passed; winding, dimensions, layering and triangle counts reviewed; scoped `git diff --check` passed. Unity was not run: camera readability, slope contact and the ~1 ms target still need in-engine validation.
