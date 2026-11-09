# 21_field_story_models

Only `RecordModels` and `FieldPropModels` changed; public signatures, caches, seeds, runtime materials, placement and scaling are preserved.

| Method | Model | Triangles |
|---|---|---:|
| `Recorder()` | Dented octagonal emitter, inset pale lens, speaker slots, screws, bent antenna and small rubble fragments; footprint 0.324 × 0.310 m. | 304 |
| `Hologram(0, height)` | Slim adult with split coat tails, staggered boots, hand at ear and held satchel. | 672 at 1.70 m; 676 at 1.75 m |
| `Hologram(1, height)` | Broad adult with shorter jacket and backpack. | 696 at 1.70 m; 688 at 1.80 m |
| `Hologram(2, height)` | Child with proportionally larger head and small held radio. | 496 at 1.25 m |
| `Burrow()` | Recessed dark drain mouth, chipped concrete arch, soil piles, bent rebar and two small bones; footprint 1.063 × 0.996 m. | 340 |
| `Depot()` | Dented olive steel case, roof/front/back ribs, rust runoff, bolted feet, rear hinges and folded tarp corner. Body stays within 1.6 × 1 × 1 m; specified front hasp projects to z = 0.54 m. | 466 |
| `DepotLock()` | Inset lock plate aligned with the hasp at (0, .55, .53); outer face z = .55 m. | 12 |
| `Merchant()` | Rounded patched seated machine, lowered visor with quiet painted eyes, long arms and delicate fingers, loaded backpack, mat, two crates and laid-out spare parts. Body about 1.4 m; lantern support reaches 1.843 m. | 1,000 |
| `MerchantLantern()` | Small faceted bulb inside the merchant's lantern cage, centred at (.39, 1.62, -.12). | 48 |

All hologram parts have aligned 18 mm missing horizontal bands every 120 mm, including boots, hands and bags. Appearance uses silhouette geometry with the existing single additive material. At height 1.80 m the final scanline gap trims the visible crown to 1.782 m.

Validation: C# 9 compilation against installed Unity 6 CoreModule and the unchanged MeshBuilder passed. An offline geometry harness verified the counts above, repeatable vertices/colours, zero degenerate triangles and zero triangles crossing hologram gaps. Reviewed a software-rendered 55° view of the generated geometry. Temporary validation files stayed outside the repository.

Unity gameplay, actual additive flicker, lighting and collision were not run. Existing field callers register no prop collision here; no collision/gameplay code was changed.
