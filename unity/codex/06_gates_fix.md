# JOB_ID: 06_gates_fix — gate review fixes (Claude's in-engine review of 01_gates)

## File you may edit
- `unity/Assets/SOUL/Runtime/World/GateModels.cs` (only this file)

In-engine captures (`unity/Captures/codex_gate_*.png`, camera ~20 m up, ~55° like the game) showed:

1. **CannonWall reads wrong from above.** Seen from the top-down camera it looks like a paved walkway of
   six square tiles with a round medallion on each, not a barricade. The camera mostly sees the TOP.
   Make the top read as a broken, uneven barricade: jagged crowns at different heights, slabs leaning
   against each other at angles, gaps and cracks visible from above, scrap / rebar poking up, rubble
   spilling out on both faces (within |z| < 1.8). No repeated square modules, no round discs on top.
2. **No round "medallions" / discs / rings lying on surfaces anywhere** (the user dislikes round shapes
   on the ground — they read as artificial). Remove the round plates on the cannon wall; in `Sludge`
   replace the ring-shaped bubbles with irregular blobs / clusters of small bubbles of varied size and
   keep at most a few; elsewhere use irregular polygons.
3. Repetition: avoid identical modules repeated at a fixed spacing (cannon tiles, magnet panels ok but
   vary widths ±20 % and dent / bend a few).

Keep everything else from 01_gates (it reads well: rope ledge + anchor, magnet hull, lab door, sludge pool,
jet gap). Same constraints and footprints as in `01_gates.md`. Update `unity/codex/results/06_gates_fix.md`.
