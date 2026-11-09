# JOB_ID: 15_guide_flock — zone creatures that drift toward the boss

## File you may edit
- `unity/Assets/SOUL/Runtime/Fx/GuideFlock.cs` (only this file; it is a Claude placeholder you replace)

## Context
Route guiding must stay subtle (user: no explicit path, only "a feel for where the boss is", different per zone).
Every 60-100 s outside combat, `Game/SoulGame.Guide.cs` makes a small group of zone creatures rise beside the
player and drift ~22 m toward the zone's boss at 2.2-4.2 m/s, then fade out. They never wait for the player and
must not look like a UI marker. Read `Game/SoulGame.Guide.cs` (how Update is driven), `Fx/Fauna.cs` (existing
butterflies / birds: dynamic mesh of flapping wing pairs — same technique), `Fx/OverloadAura.cs` (additive
embers), `Core/Mats.cs` (`Mats.Unlit(color, Blend.Add|Alpha|Opaque, vertexColor, cull, zwrite, fog)`,
`Mats.SetColor`), HANDOFF §3 sim space.

## Keep this exact API (SoulGame.Guide calls it every frame)
```csharp
public class GuideFlock
{
    public GuideFlock(Transform parent);   // parent sits at the world origin
    /// zone 0..7; lead = flock centre on the ground (sim); dir = unit travel direction (sim XZ, y = 0);
    /// alpha 0..1 (0 = hidden: disable renderers); restart = a new flight began (re-gather members at the lead)
    public void Update(float dt, int zone, Vector3 lead, Vector3 dir, float alpha, bool restart);
}
```
Ground height under the lead: `Terrain.Height(lead.x, lead.z)`. Positions are sim space; convert with `Sim.ToUnity`.

## One look per zone (members follow the lead loosely, each with its own lag / bob / phase, heading along dir)
- 0 잿빛 폐허 (ash ruins): 4-6 soul-cyan embers (HDR ≈ (.5, 1.7, 2.2)), flickering, small spark trails, 0.8-1.6 m up
- 1 무너진 도심 (fallen city): 3-5 pale grey moths fluttering erratically + 1-2 scraps of paper tumbling low
- 2 마천루 (skyscraper wreck): 2-3 crows, dark, slow wing beats, gliding 3-5 m up (bigger, ~0.5 m span)
- 3 고철 협곡 (scrap gorge): hopping orange-white metal sparks that arc and bounce off the ground
- 4 전쟁 주조소 (war foundry): glowing orange embers + 1-2 soft dark smoke puffs that grow and fade
- 5 죽은 숲 (dead woods): 4-6 dry brown leaves tumbling (rotating flat quads) + 1 small dark bird
- 6 독성 늪 (toxic mire): toxic green fireflies, slow, low (0.4-1 m), pulsing
- 7 연구소 (lab): small blue electric sparks that blink in short chains, crackle-like jitter
- Readable from the top-down camera (~55°, 10-25 m away): sizes 0.1-0.5 m, not tiny dots. Alpha fades
  everything; restart snaps members to the lead area. Members spawn slightly spread (~1-2.5 m) around the lead.
- Budget: ≤ ~600 vertices per zone look, reused arrays, no per-frame allocations, deterministic `System.Random`
  seeded in the constructor. One or two dynamic meshes total (e.g. one additive glow mesh + one alpha/opaque
  mesh for crows / leaves / moths / paper); switch content by zone.
- No text, arrows, rings, beams or anything marker-like.

## When done
Write `unity/codex/results/15_guide_flock.md` (per-zone look, vertex counts, API unchanged).
