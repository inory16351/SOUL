# 15_guide_flock

Replaced `Fx/GuideFlock.cs`; public constructor and `Update` signature unchanged. No gameplay/collision changes.

| Zone | Look | Active vertices / triangles |
|---|---|---:|
| 0 Ash ruins | 5 cyan HDR embers, independent flicker and three broken trail flecks each; centres 0.8–1.48 m high | 135 / 45 |
| 1 Fallen city | 4 pale grey moths with hinged fore/hind wings and erratic flutter; 2 low, folded paper scraps | 144 / 48 |
| 2 Skyscraper wreck | 3 dark crows, feather tips, body ridge, tail and beak; slow wing bouts and glides at 3.15–4.8 m | 108 / 36 |
| 3 Scrap gorge | 6 orange-white sparks, staggered parabolic hops and ground-clamped trail flecks | 162 / 54 |
| 4 War foundry | 5 orange embers and 2 growing, rising dark smoke puffs with transparent edges | 207 / 69 |
| 5 Dead woods | 5 brown folded leaves with stems, independent tumble; 1 small dark bird | 111 / 37 |
| 6 Toxic mire | 6 low green fireflies with slow independent pulses and dark fluttering wings | 144 / 48 |
| 7 Lab | 5 jittering blue spark clusters, each blinking along a short three-point chain | ≤270 / ≤90 |

- Constructor: seeded `System.Random(150427)`, fixed member parameters, two dynamic meshes (300 glow + 240 alpha vertices; 180 allocated triangles including unused degenerate slots).
- `Update`: individual lag/bob/phase, terrain-relative heights, travel-aligned orientation, initial rise, full material-alpha fade. Zero alpha disables both renderers; hidden restarts and zone changes gather immediately.
- `Ember`/`GlowPatch`, `Wings`, `Tumble`, `Puff`, `Crackle`: separate zone geometry/animation helpers. `Batch`: reused vertex/color/index arrays, sim conversion with reversed winding, tight bounds, unused-slot clearing. No per-frame managed allocations in this class.
- Validation: C# 9 compiled with warnings as errors against installed Unity 6 (6000.3.19f1) reference assemblies and the existing project assembly. Reviewed buffer maxima, blend shader alpha handling, hidden restart and zone switching. Unity was not launched; visual/play-mode validation remains.
