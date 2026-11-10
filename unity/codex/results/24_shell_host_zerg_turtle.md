# 24 — Shell host: chitinous bug × turtle

- **createShellHost():** Purple-brown dome with five paired overlapping plates, olive bevels, serrated skirt, curved edge horns and a fore/aft spined keel. Raised armour stays outside the clear r≈0.315 crown. Three low violet-pink sac bases match runtime angles (0.4 + k×2π/3), r=0.336. Wrinkled neck, hooked turtle beak, side mandibles, violet eye, segmented tail, two swallowed riveted plates. Sideways upper limbs, bent shins, broad three-clawed paws; chitin roughness .32.
- **DeadShell():** Matching plates, pigments and anatomy; slumped head with open jaw/mandibles, dull eyes, four limp half-submerged paws, flattened keel, three torn non-glowing sac husks, exposed pale flank ribs, algae/drips. Neck/leg gaps keep anatomy visible. Crown skin y=2.19–2.2 to r≈3.05; crown relief ≤2.307. Skirt teeth r=3.86; all appendages within r=3.90. Collision code untouched.
- **Private helpers:** Replaced old oversized skirt/machine/barnacle helpers with Scute, ShellLobe and ShellHook; retained polar, two-sided and rivet helpers. SquarePoint and Roof onward remain byte-for-byte unchanged.

## Rig positions (local, s = side ±1)

| Joint | Front pair | Rear pair |
|---|---|---|
| Body in root | (0, .5, 0) | same |
| Hip in body | (s×.46, −.07, .25) | (s×.46, −.07, −.29) |
| Knee in hip | (s×.20, −.10, .07) | (s×.20, −.10, −.07) |
| Ankle in knee | (s×.025, −.25, .035) | (s×.025, −.25, −.035) |

Ankle world y=.08; paw soles y≈0. Identity rotations, leg order, parts keys and eye/core/throat glow meshes preserved. Returned radius=.8, height=.9.

## Counts and checks

- Living: **6,008 triangles**, including hidden throat. Corpse: **4,102 triangles**.
- JavaScript syntax/finite geometry, deterministic generation, pivot hierarchy, crown clearance and X-hinge motion checked.
- Final isolated C# 9 compile of the modified model with actual MeshBuilder/Sim against Unity assemblies: **passed**.
- Full runtime compilation passed earlier. Final full-project retry is blocked by an unrelated concurrent workspace error: **World/Skills.cs:75, CS0117 — Growth.SlotBonus is missing**. Those files were not edited.
- Standalone actual-MeshBuilder math harness: no degenerate corpse triangles; 5,184 crown ray samples without holes, y=2.19–2.30.
- Inspected procedural previews at ~55°, low/front and flank angles. Bytes outside authorized edit regions preserved; JavaScript remains CRLF.

Unity was not run. Exporter, generated assets and prefabs were not touched. Claude still needs to export/rebuild and inspect in-game animation, sacs, sludge and collision.
