# 18_alpha_beast

Remodeled only createHoundAlpha(), with four nested private alpha-prefixed helpers. A front-heavy stalking scrap wolf: arched plated withers, bowed pipe ribs around an engine ember, tucked waist, swept cable/rebar mane, bent soot-dark exhausts, asymmetric tyre/chain/rag patches, and a down-then-up segmented tail. Low tapered skull has overlapping welded plates, folded torn ears, one live eye and one dull lens, jagged upper/lower fangs, hinged jaw, hanging wires, and a cracked-pipe howl resonator. Thick piston/plate legs end in splayed blade-clawed paws.

## Parts tree (parent-local sim positions)

All pivots have identity rotation and unit scale; geometry transforms are baked.

- root (0, 0, 0)
  - body (0, .5, 0): one opaque batch including fixed tail and rags
    - core (0, -.015, .31): glow mesh
    - head (0, .14, .50): one opaque skull/neck/resonator batch
      - eye (-.109, .043, .145): glow mesh
      - throat (0, -.145, -.035): glow mesh, visible = false
      - jaw (0, -.085, .035): one opaque batch including wires
    - legs, ordered FL / FR / RR / RL:
      - front hip (side * .18, -.015, .30); knee (side * .012, -.23, -.015)
      - rear hip (side * .18, -.015, -.35); knee (side * .012, -.22, .095)
      - one upper batch on each hip; one lower-leg/paw batch on each knee

## Triangle count

| Section | Triangles |
| --- | ---: |
| Body, armour, mane, tail, chains, rags | 4,572 |
| Head, neck, resonator | 1,388 |
| Jaw, teeth, wires | 392 |
| Four upper legs | 992 |
| Four lower legs and paws | 1,296 |
| Three glow meshes (including hidden throat) | 240 |
| **Total** | **8,880** |

Validated with Three.js r160 construction, Node syntax check, finite attributes, identical geometry on repeated builds, exact part hierarchy/order, identity transforms, and four-angle software-rendered visual inspection. 11 opaque batches + 3 glow meshes. Rest bounds: x [-.279, .269], y [.0038, .9224], z [-1.0156, .9033] including tail/head; all four paws within .004 m of ground. Return radius .75 and height .9 unchanged; Unity applies x5 scale.

New code uses CRLF; everything outside the function is byte-for-byte preserved, including existing mixed line endings and the older surrounding scale comment. No exporter or Unity run. A browser preview was blocked by execution policy; the software render checks geometry, not final Unity lighting or procedural animation.
