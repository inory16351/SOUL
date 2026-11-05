# Ashen Vanguard — procedural motion study

Run `node web-proto/server.mjs` from the repository root, then open http://localhost:5173.
Node 18+ and a WebGL browser are required. Internet access loads the pinned Three.js 0.160.0 modules from jsDelivr; there are no model, animation, texture, font, or build dependencies.

- WASD moves relative to the camera; Shift runs. Space backsteps when stationary and rolls while moving.
- J / left click attacks. Press again in the latter half of a light attack to buffer the next of three strikes.
- Hold K / right click to charge; release to cleave. F jumps; J while airborne plunges.
- L parries. H reacts to a hit; H again within one second staggers. C changes camera.
- The move library stages every move directly. The charge preview holds its pose; K then release or the cleave button releases it. The slider controls 0.05–1.5× playback. The skeleton checkbox reveals all joints.

## Implementation

`character.js` builds vertex-painted armor, body and sword meshes. `rig.js` owns the joint hierarchy and analytic two-bone IK. `moves.js` contains degree-based poses, weapon guides, timing, attack windows and root curves. `anim.js` compiles normalized quaternion Hermite splines, blends states, handles combo buffering, drives locomotion and plants feet in world space. Both arms solve to the authored weapon arc. `secondary.js` simulates constrained cape/tabard cloth and a damped plume. `scene.js`, `camera.js` and `vfx.js` provide staging, views, a spring dummy, fixed-rate blade ribbon and impacts. `main.js` connects input, collision, post-processing and UI.

Attack contacts use a blade segment and swept tip against the dummy's torso, with one hit per swing. Hit-stop lasts 60 ms (85 ms on slams) in real time. Physics and animation run at substeps up to 1/120 s with a clamped frame delta. Preview buttons reset the actor's starting position; keyboard play preserves accumulated root motion, with recentering at the edge of the stage.

## Automation

```js
window.__proto.moves                 // Every supported preview name
window.__proto.play('light1')        // Returns true; stages and plays a move
window.__proto.state()               // e.g. 'light1', 'idle', 'walk', 'run'
window.__proto.setTimeScale(0.25)
window.__proto.inspect()             // Numeric diagnostics: grip, feet, finite joints, hits
```

This is an animation study, not a combat game. Ground is flat for foot IK; armor uses rigid overlapping pieces; cape collision uses a simple body envelope.
