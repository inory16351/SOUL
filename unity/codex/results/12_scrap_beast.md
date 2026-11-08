# 12_scrap_beast

- Rebuilt `createAshWarden()` as a low, lean scrap-machine jackal: articulated three-section spine, welded canine head and hinged toothed jaw, exposed furnace core, engine ribs, gears/tyre rings, pipes, plates, cables and ash rags.
- Parts tree / local pivot offsets: `pelvis (0,1.08,-1.65)` -> `s0 (0,.08,.62)` -> `s1 (0,.06,.72)` -> `s2 (0,.06,.72)` -> `torso (0,.06,.45)` -> `chest (0,.12,.55)` -> `neck (0,.10,.54)` -> `head (0,.05,.45)` -> `jaw (0,-.20,.45)`. Tail from pelvis: `t0 (0,.05,-.75)` -> `t1 (.08,-.02,-.76)` -> `t2 (-.10,-.04,-.72)` -> `t3 (.08,-.06,-.67)`.
- Crawl legs: front-left/front-right hips on chest at `(-.62,-.05,.05)` / `(.62,-.05,.05)`; knees at `(-.85,-.62,.12)` / `(.85,-.62,.12)`; ankles at `(.12,-.62,.42)` / `(-.12,-.62,.42)`. Rear-left hip on pelvis at `(-.62,-.02,.10)`, knee `(-.82,-.48,.18)`, ankle `(.10,-.40,.35)`. Each claw pivot is `(0,-.09,.08)` and rests at ground level.
- Giant rear-right arm from pelvis: `shoulder (.58,.30,-.20)` -> `upper (0,0,0)` -> `elbow (.82,2.70,.58)` -> `fore (0,0,0)` -> `wrist (-.60,2.05,1.62)`; fingers at x `-.31/0/.31`, and empty `grip (0,-.72,.20)`. The arch reaches about 6.4 m.
- Returned contract is exactly `{ pelvis, torso, chest, neck, head, jaw, eye, core, spine[3], tail[4], legs[3], arm }`, with `radius: 2.4`, `height: 3.2`. Estimated geometry is about 5,400 triangles, below the 12,000 limit.
- Validation: JavaScript syntax and whitespace checks pass. Exact Three.js bounds/count could not be run because prototype dependencies are not installed; exporter was not run as requested.
