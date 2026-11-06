# 04_traces_landmarks

Changed only `Runtime/World/TraceProps.cs` plus this requested report.

`Trace(zone, seed)` now deterministically selects three kinds per zone, with distinct `Trace*` cache IDs. `Landmark(zone)` selects eight dedicated `Passage*` models. Both public signatures are unchanged. All builders are private static, use seeded MeshBuilder randomness, and author in sim space.

| Trace builder | Detail | Triangles |
|---|---|---:|
| ClawScratches | Paired triple gouges, displaced chips | 228 |
| DragMarks | Broken parallel furrows, scraped metal | 176 |
| OilTrail | Irregular droplets, muted oil sheen, debris | 158 |
| ArmourPlates | Chipped overlapping armour, rivets, scratches | 294 |
| HoundHusk | Broken spine, exposed ribs, splayed legs and muzzle | 489 |
| ScatteredBones | Long bones, broken ribs and skull | 325 |
| TornRoots | Severed roots and disturbed litter | 342 |
| SludgeFootprints | Alternating heel/toe prints and smears | 282 |
| SludgeDrag | Wet drag patches and embedded fragments | 173 |
| CartTracks | Twin wheel ruts, tread impressions and slag | 200 |

Traces have no colliders or surfaces; maximum measured height is 0.48 m.

| Landmark builder | Detail | Triangles | Declared radius / height (m) |
|---|---|---:|---:|
| RadioMast | Bent lattice, broken dish, supports and slack cables | 2,516 | 6 / 34.1 |
| OfficeStump | Leaning broken storeys, window voids, rebar, rooftop tank | 2,608 | 8.4 / 27.15 |
| FallenCrown | Lodged severed crown, torn facade, bent spire | 2,130 | 8.8 / 28.4 |
| CraneWreck | Folded boom, cab, counterweight, suspended hook | 3,114 | 8 / 30.15 |
| TwinStacks | Unequal hollow stacks, broken rims, soot, bands and ladders | 2,090 | 8 / 36.6 |
| HollowTree | Hollow split trunk, branching crown, segmented buttress roots | 1,716 | 7.6 / 31 |
| TankerBow | Rising hull, recessed rim, hatch, railings, hawse fittings and mast | 2,138 | 8.5 / 28.1 |
| SealedDome | Weathered faceted shell, sealed hatch, antenna | 2,462 | 9.1 / 27.1 |

`Finish` sets metadata and collider lists; landmarks set `landmark = true`, with no walkable surfaces. Shared private helpers construct chipped plates, ground patches, smears, trusses, cables and hollow shells. `Foundation` uses three visible slabs with matching colliders and missing corners; tree roots have individual matching colliders. No text, markers or emissive geometry.

Validation: C# 9 compilation against Unity 6000.3.19f1 reference DLLs passed. An external temporary math/mesh harness exercised the actual MeshBuilder and TraceProps sources for 128 seeds per kind (2,304 builds, each repeated), checking bounds, budgets, collider extents, metadata and deterministic vertices/colors. Maximum measured landmark radius: 8.671 m. Inspected approximate 55-degree software projections; these are not Unity renders.

Limitations: Unity play-mode/camera/collision QA remains outstanding. `ZoneRoutes` randomizes trace yaw, so exact directional alignment toward passages cannot be fixed in this file. The severed skyscraper crown is lodged steeply to satisfy both the tall silhouette and radius limits.
