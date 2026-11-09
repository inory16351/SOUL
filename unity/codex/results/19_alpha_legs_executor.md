# 19_alpha_legs_executor

- **createHoundAlpha:** three moving segments per leg: backward fore elbow, near-vertical forearm, forward pastern; forward rear stifle, backward shin and long metatarsus. Added ankle pivots, layered muscle plates, pistons, cable tendons, dewclaws and splayed blade soles. Body/head/jaw/eye/core/throat geometry unchanged.
- **createExecutor:** bowed chipped breastplate shingles, domed spiked pauldrons, curved notched blades, broken right sleeve with splint/clamps, chain-hung hound skulls and torn trophies. Rear core sits in an open cup framed by fractured ribs/cables and bent exhausts. Split hood, shoulder mantle, trailing strips and long tabard. Helm geometry sunk forward between the shoulders without changing rig transforms.
- **executor helpers:** private geometry helpers and independent executorArm/executorLeg. Shared fieldBipedArm/fieldBipedLeg and createSentry are byte-for-byte unchanged.

## Alpha pivot table

Parent-local sim metres; body=(0,.5,0). Hip → knee → ankle hierarchy; paw geometry on ankle. All pivots have identity rotation/unit scale.

| Entry | hip | knee | ankle | Ankle-local sole Y |
| --- | --- | --- | --- | ---: |
| FL (-1,0) | (-.17,-.01,.30) | (-.01,-.17,-.06) | (0,-.21,.03) | -.11 |
| FR (+1,0) | (.17,-.01,.30) | (.01,-.17,-.06) | (0,-.21,.03) | -.11 |
| RR (+1,1) | (.17,-.01,-.35) | (.01,-.16,.09) | (0,-.17,-.13) | -.16 |
| RL (-1,1) | (-.17,-.01,-.35) | (-.01,-.16,.09) | (0,-.17,-.13) | -.16 |

All four paws touch world Y=0 (Float32 error <4e-9 m). Entries: { hip, knee, ankle, side, index }. Radius .75 / height .9 unchanged.

## Executor parts tree

Parent-local positions; s=-1 left / +1 right. All rig rotations/scales remain identity.

    root
      pelvis (0,1.05,0) — yoke, tabard/loincloth
        torso (0,.25,0) — armour, rear housing/stacks, trophies, mantle
          core (0,.53,-.36) — rear glow
          head (0,1.12,.025) — helm/hood
            eye (-.035,.025,.19) — slit glow
          armL / armR:
            shoulder (s*.41,.80,0) — pauldron/upper arm
              elbow (s*.12,-.43,.015) — gauntlet; right splint/clamps
                blade (s*.08,-.35,.045) — execution blade
        legL / legR:
          hip (s*.20,0,0) — thigh
            knee (s*.045,-.44,.14) — spiked knee/reverse shin
              ankle (s*.025,-.37,-.31) — sabatons

Head meshes, including eye geometry, have a baked (0,-.08,.14) offset; pivot positions remain unchanged. Return contract, radius .95 / height 2.7 unchanged. Rest Y=0..2.6513 m; maximum radial extent .8364 m.

## Triangle counts

| Alpha section | Triangles |
| --- | ---: |
| Unchanged body/head/jaw/glows, including hidden throat | 6,592 |
| Four upper legs | 816 |
| Four forearms/shins | 608 |
| Four ankle/paw batches | 1,168 |
| **Alpha total** | **9,184** |

| Executor section | Triangles |
| --- | ---: |
| Yoke, torso/housing/stacks, trophies | 3,860 |
| Helm | 464 |
| Both arms/blades | 3,068 |
| Both legs | 2,096 |
| Hood, mantle, tabard | 236 |
| Eye/core glows | 160 |
| **Executor total** | **9,884** |

Validated: Node module syntax; Three.js r160 construction; finite attributes/nondegenerate triangles; repeat-build determinism; executor part names/transforms/parents; alpha leg order/hierarchy/ground contact; unchanged sentry geometry. Inspected four-angle depth-buffer software previews. Front core ray samples at yaw 0/45/80°, elevation 0/55° were all occluded; rear samples showed the core. Source uses CRLF; bytes outside allowed function/helper ranges preserved.

No exporter or Unity execution. Final Unity lighting and procedural gait/combat animation remain unverified.
