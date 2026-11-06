# JOB_ID: 07_ground_detail — ground character per zone

## File you may edit
- `unity/Assets/SOUL/Runtime/World/GroundDetail.cs` (only this file)

User feedback (2026-11-06): the terrain chunks look like bare dirt; they need colour and character.
Claude already added per-zone colour patterns into the ground vertex colours (`World/GroundLook.cs`,
read it; do not edit). Your job is the detail mesh layer on top: implement `GroundDetail.Decals(x0, z0, size)`
following the header comment of `GroundDetail.cs` (themes, height limits, no collision, no round shapes,
determinism, budget). Look at `World/ZoneWorld.cs` (`At`, `RidgeHeight`), `World/ZoneRoutes.cs` (`Dist`),
`World/Regions.cs` (zone palettes) for helpers. Purified zones (`ZoneState.Purified(zone)`) should get
fewer dead details and some green shoots (the chunk is rebuilt when a zone is purified).
Seen from ~10–25 m above at ~55°, details 0.3–3 m long read best; avoid noise-like confetti.
