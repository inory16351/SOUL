# JOB_ID: 14_overload_aura — natural red aura for the "코어 과부하" skill

## File you may edit / create
- `unity/Assets/SOUL/Runtime/Fx/OverloadAura.cs` (new file; only this file)

## Context
The skill 코어 과부하 (core overload) drives the robot's core at high output for ~8 s: it heals over time,
moves faster and takes more damage. While it runs a **red aura** surrounds the character. The current placeholder
(`Game/SoulGame.Skills.cs`, `AuraMesh()`) is a plain open cylinder + ground ring with a uniform pulse; the user
says it must look natural. Read `unity/HANDOFF_UNITY.md` §3 (sim space) and `Runtime/Core/Mats.cs`
(`Mats.Unlit(color, Blend.Add, vertexColor, cull, zwrite, fog)`, `Mats.SetOpacity/SetColor`), `Runtime/Fx/Vfx.cs`
for the existing VFX style (HDR colours feed bloom).

## Build
A self-contained component-free class:
```csharp
public class OverloadAura
{
    public OverloadAura(Transform parent);            // parent sits at the world origin (like Vfx's)
    public void Update(float dt, Vector3 simPos, float strength); // strength 0..1 (fade in/out); call every frame
    public void Dispose();
}
```
- Look: heat / energy leaking from an overdriven core, not a solid tube.
  - Several **flickering flame-like ribbons or tongues** rising from the feet and curling upward, each with its own
    phase, height and slight sway; additive red-orange core fading to dark red at the tips (vertex alpha 0 at the top).
  - A soft **ground glow** that breathes irregularly (noise, not a sine), slightly larger than the character.
  - **Rising embers/sparks**: small quads drifting up and sideways with random lifetimes, a few brighter.
  - Occasional **heat crackle**: a short bright flicker every 0.6–1.5 s.
  - Colour HDR red (≈ (2.2, .25, .1)), never pink; keep it readable from the top-down camera (~55°) without hiding
    the character (low alpha in the middle, stronger at the rim).
- Implementation: build meshes once, animate by rewriting a small vertex buffer per frame (≤ ~400 vertices total)
  or by transforming a few child objects. Positions are sim space; convert with `Sim.ToUnity`. No allocations per
  frame (reuse arrays). Deterministic random from a `System.Random` seeded in the constructor.
- Fade with `strength`: everything scales/fades in over ~0.3 s and out over ~0.5 s.
- C# that compiles in Unity 6 (C# 9), `using UnityEngine; using System.Collections.Generic;` only. No other files.

## When done
Write `unity/codex/results/14_overload_aura.md` (what it looks like, vertex budget, the exact API).
