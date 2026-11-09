# JOB_ID: 20_alpha_thick_legs — 무리의 우두머리: legs too thin for the body

## File you may edit
- `web-proto/src/enemy_models.js` — only the leg geometry of `createHoundAlpha()` (its `alpha…` leg helpers; you may
  add mass to the body only where the legs attach: shoulder blades / haunches). CRLF line endings — keep them.
  Do not run the exporter.

## Feedback (user, 2026-11-09, after `elites_v4.png`)
"다리가 너무 얇음 상체에 비해" — the legs look spindly under the massive plated torso (it is a ~7.5 m beast; Unity ×5).
Read `results/19_alpha_legs_executor.md` (your previous pass: pivot table) — keep that pivot table exactly.

## What to change
- Roughly **double the visual girth** of every leg segment: upper segments ~2–2.4× thicker, lower ~1.8× (still
  tapering toward the paw). Read as a heavy predator's limbs (a wolf / big cat / hyena), not stilts.
- **Muscle masses** built from layered curved plates over pistons: a big shoulder-blade + upper-arm block on each fore
  leg, a heavy rounded haunch / thigh on each hind leg (wide at the top, merging into the body side), forearm and
  gaskin plates; thick cable tendons down the back of each lower leg; armoured elbow / hock caps.
- **Paws**: wider and heavier (≈1.5×), splayed toes with long blade claws, a pad/sole plate; dew-claw spurs kept.
- Keep the joint positions, hierarchy (`hip` → `knee` → `ankle`, paw on `ankle`), order (FL, FR, RR, RL), identity
  pivots, paws touching y = 0, and nothing intersecting the opposite leg when Unity swings `hip.rotation.x` ±0.45 and
  bends knees/ankles up to ~1 rad (keep the inner sides of the legs clear by ~.03).
- Body, head, jaw, eye, core, throat unchanged. Total alpha budget ≤ 11,000 tris.

## When done
Write `unity/codex/results/20_alpha_thick_legs.md` (what changed, girths before → after, triangle count).
