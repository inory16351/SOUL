# 22_z6_mire_roster

Added eight constructors and private Z6 geometry helpers to `enemy_models.js`; appended all eight entries to the exporter enemy list. Existing constructors are unchanged; model source retains CRLF. No gameplay files changed.

## Models

Counts include all meshes, glow meshes and the shell host's hidden throat.

| Constructor | Triangles / budget | Rig tree | Geometry |
|---|---:|---|---|
| createSwarmBug | 1,568 / 1,800 | C | Swollen violet abdomen, raised pale wing membranes with veins, green compound eye, hooked mandibles, six thin legs. |
| createSludgeCrawler | 2,284 / 3,500 | C | Low notched armour, bolts, dark runoff seams, hanging sludge sheets, algae, recessed spit nozzle. |
| createSporeVine | 3,148 / 3,500 | W | Braided stalk, swollen lobed pod with puckered mouth, curled vine whips, radial root buttresses. |
| createDredger | 3,516 / 5,000 | W | Open lined intake with teeth and recessed grille, right bucket/left hook, gnawed hull, swarm nests, mud shoes. |
| createGiantDredger | 5,436 / 9,000 | W | Larger intake, two buckets, broken crane truss, alternating chain links, hoses, additional nests and torn plates. |
| createShellHost | 3,172 / 9,000 | Q | Broad domed shell, peripheral ridges/barnacles/embedded plates, thick neck, snapping beak, columnar clawed legs. Crown reserved for runtime sacs. |
| createMireMother | 5,196 / 11,000 | W | Swollen flesh lobes, split toothed vertical maw with recessed core, flank eggs, swallowed tank/door/pipes, restrained glowing veins, thick tentacles and root skirt. |
| createMireTentacle | 2,304 / 2,500 | W | Single segmented tapering stalk, suckers, hooked tip, small folded arm seams, rootlets and exposed basal core. |

## Parts trees / pivots

Coordinates are **parent-local sim metres**, before Unity's type scale. All node rotations are identity and scales are one. Eye/core positions are listed separately.

**C — swarmBug and sludgeCrawler** (`radius .75`, `height .9`):

- root -> body `(0,.46,0)` -> head `(0,.21,.50)` -> jaw `(0,-.105,.035)`.
- body -> core; head -> eye.
- body -> six hips `(s*.22,.02,-.40+i*.34)` -> knees `(s*.25,-.15,(i-1)*.12)`.
- Ordering: `s=-1,+1`, each `i=0,1,2`; entries `{hip,knee,side,index}`.

**Q — shellHost** (`radius .75`, `height .9`; exact Hound Alpha quad contract):

- root -> body `(0,.50,0)` -> head `(0,.14,.50)` -> jaw `(0,-.085,.035)`.
- body -> core; head -> eye and hidden `throat` `(0,-.145,-.035)`.
- Leg ordering `(side,index)`: `(-1,0),(1,0),(1,1),(-1,1)`; entries `{hip,knee,ankle,side,index}`.
- Front: body -> hip `(s*.17,-.01,.30)` -> knee `(s*.01,-.17,-.06)` -> ankle `(0,-.21,.03)`.
- Rear: body -> hip `(s*.17,-.01,-.35)` -> knee `(s*.01,-.16,.09)` -> ankle `(0,-.17,-.13)`.
- Shell top `.88`; at the three runtime sac centres, shell top is approximately `.819/.852/.842`, below the `.9` sac mount height. No sacs baked into this model.

**W — sporeVine, dredger, giantDredger, mireMother and mireTentacle** (`radius .95`, `height 2.7`; exact Warden contract):

- root -> pelvis `(0,1.05,0)` -> torso `(0,.25,0)` -> head `(0,1.12,.025)`.
- torso -> core; head -> eye.
- Each arm (`s=-1` left, `+1` right): torso -> shoulder `(s*.41,.80,0)` -> elbow `(s*.12,-.43,.015)` -> blade `(s*.08,-.35,.045)`.
- Each leg: pelvis -> hip `(s*.20,0,0)` -> knee `(s*.045,-.44,.14)` -> ankle `(s*.025,-.37,-.31)`.
- Keys: `{pelvis,torso,head,eye,core,armL:{shoulder,elbow,blade},armR:{shoulder,elbow,blade},legL:{hip,knee,ankle},legR:{hip,knee,ankle}}`.

| Model | eye (head-local) | core (body-local for C/Q, torso-local for W) |
|---|---|---|
| swarmBug | `(0,.035,.13)` | `(0,.225,-.35)` |
| sludgeCrawler | `(0,-.035,.125)` | `(0,.085,-.20)` |
| sporeVine | `(.19,.13,.17)` | `(0,.45,.16)` |
| dredger | `(-.221,-.04,.25)` | `(-.18,.31,.20)` |
| giantDredger | `(-.2795,-.04,.25)` | `(-.18,.31,.20)` |
| shellHost | `(-.12,.06,.105)` | `(-.48,.055,.19)` |
| mireMother | `(-.29,.04,.36)` | `(0,.72,.30)` (world y 2.02, inside maw) |
| mireTentacle | `(.10,.09,.24)` | `(0,-1.06,.19)` (world y .24, at root) |

## Validation / limits

Instantiated every model using local Three.js r160. Passed exact reference-key/parent/joint-name/pivot/side/index comparisons against reused rigs; identity transforms; eye/core glow material checks; finite geometry; deterministic repeated geometry; triangle limits; and rest-pose radial/vertical bounds. Checked 55-degree CPU triangle previews, direct core sight lines, sac mounting heights, unchanged existing model source, exporter-only-line diff and whitespace.

No exporter or Unity build was run, as requested. Unity import, procedural animation in motion, lighting and gameplay-camera approval remain for Claude. Wings use pale opaque faceted plates to suggest translucency without transparency sorting.
