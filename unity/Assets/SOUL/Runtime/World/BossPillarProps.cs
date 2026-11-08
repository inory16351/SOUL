using UnityEngine;

namespace Soul
{
    // BOSS ENERGY PILLAR MODEL (Codex job 11, unity/codex/11_boss_pillar.md). Placeholder by Claude.
    // The Z0 boss arena raises these out of the ground (Game/BossPillars.cs): the player breaks them in the order their
    // lights come on. Body mesh = lit vertex colour; glow mesh = white vertex colour, tinted blue / red at runtime.
    // Keep: origin at the ground centre, body inside radius .75 (collider circle r .75), top of the glow ≈ 4.2 m,
    // light seams + tip in the glow mesh. Hit test is a capsule r .6 from y .2 to 3.6.
    public static class BossPillarProps
    {
        public static Mesh Pillar(out Mesh glow)
        {
            // dark steel shaft on a concrete plinth, collars, energy seams + crystal tip (glow mesh)
            var b = new MeshBuilder(11);
            b.Cylinder(new Vector3(0, 0, 0), 1.05f, .95f, .32f, 6, MeshBuilder.Hex(0x6d6a66), true, .04f);
            b.Cylinder(new Vector3(0, .32f, 0), .62f, .5f, 2.95f, 6, MeshBuilder.Hex(0x34393c));
            b.Cylinder(new Vector3(0, 1.1f, 0), .7f, .7f, .18f, 6, MeshBuilder.Hex(0x55493d));
            b.Cylinder(new Vector3(0, 2.45f, 0), .64f, .64f, .16f, 6, MeshBuilder.Hex(0x55493d));
            b.Cylinder(new Vector3(0, 3.27f, 0), .56f, .3f, .3f, 6, MeshBuilder.Hex(0x2a2e30));
            for (int i = 0; i < 3; i++) { b.Push(); b.RotateY(i * Mathf.PI * 2 / 3 + .5f); b.Box(new Vector3(0, .5f, .95f), new Vector3(.32f, .4f, .3f), MeshBuilder.Hex(0x5a5752)); b.Pop(); }
            var g = new MeshBuilder(12); var w = Color.white;
            for (int i = 0; i < 3; i++) { g.Push(); g.RotateY(i * Mathf.PI * 2 / 3); g.Box(new Vector3(0, 1.8f, .53f), new Vector3(.1f, 2.2f, .06f), w, 0); g.Pop(); }
            g.Cylinder(new Vector3(0, 3.3f, 0), .06f, .3f, .32f, 4, w);
            g.Cylinder(new Vector3(0, 3.62f, 0), .3f, 0, .62f, 4, w);
            glow = g.Build("boss-pillar-glow");
            return b.Build("boss-pillar");
        }

        /// rope arm part (boss reward, floats over the wreck): forearm housing + cable coil + grapple hook; glow = coil light
        /// + hook tips. About 1.2 m long, origin at its centre (it spins).
        public static Mesh RopeArm(out Mesh glow)
        {
            var b = new MeshBuilder(31);
            b.Push(); b.RotateX(Mathf.PI / 2);
            b.Cylinder(new Vector3(0, -.6f, 0), .17f, .2f, .75f, 8, MeshBuilder.Hex(0x4a5054));             // housing
            b.Cylinder(new Vector3(0, -.62f, 0), .23f, .23f, .12f, 8, MeshBuilder.Hex(0x78432c));
            b.Cylinder(new Vector3(0, .1f, 0), .26f, .26f, .28f, 10, MeshBuilder.Hex(0x2b2f31));            // cable drum
            b.Cylinder(new Vector3(0, .38f, 0), .08f, .05f, .3f, 6, MeshBuilder.Hex(0x6d6a66));
            b.Pop();
            for (int i = 0; i < 3; i++) { b.Push(); b.RotateZ(i * Mathf.PI * 2 / 3); b.Beam(new Vector3(0, 0, .66f), new Vector3(0, .26f, .86f), .04f, .02f, 4, MeshBuilder.Hex(0x8f8b86)); b.Pop(); }   // hook prongs
            var g = new MeshBuilder(32); var w = Color.white;
            g.Push(); g.RotateX(Mathf.PI / 2); g.Cylinder(new Vector3(0, .13f, 0), .275f, .275f, .05f, 10, w); g.Cylinder(new Vector3(0, .27f, 0), .275f, .275f, .05f, 10, w); g.Pop();
            for (int i = 0; i < 3; i++) { g.Push(); g.RotateZ(i * Mathf.PI * 2 / 3); g.Box(new Vector3(0, .27f, .87f), new Vector3(.05f, .05f, .05f), w, 0); g.Pop(); }
            glow = g.Build("rope-arm-glow");
            return b.Build("rope-arm");
        }

        static Mesh scrap;
        /// lump of scrap the boss's hanging arm hurls (ranged attack): about 1.6 m across, origin at its centre
        public static Mesh ScrapChunk()
        {
            if (scrap) return scrap;
            var b = new MeshBuilder(21);
            b.Box(new Vector3(0, 0, 0), new Vector3(1.3f, .8f, 1.0f), MeshBuilder.Hex(0x5c4a3a));                       // crushed car body
            b.Push(); b.RotateZ(.5f); b.RotateY(.4f); b.Box(new Vector3(.25f, .35f, .1f), new Vector3(.9f, .5f, .7f), MeshBuilder.Hex(0x78432c)); b.Pop();
            b.Push(); b.RotateX(.7f); b.Box(new Vector3(-.4f, -.2f, .35f), new Vector3(.7f, .45f, .6f), MeshBuilder.Hex(0x44494b)); b.Pop();
            b.Cylinder(new Vector3(.55f, -.2f, -.45f), .32f, .32f, .22f, 7, MeshBuilder.Hex(0x232425));                   // tyre
            b.Beam(new Vector3(-.7f, .1f, -.5f), new Vector3(.8f, .5f, .7f), .04f, .04f, 4, MeshBuilder.Hex(0x6b3a22));       // rebar
            b.Beam(new Vector3(-.5f, .4f, .6f), new Vector3(.4f, -.4f, -.7f), .035f, .035f, 4, MeshBuilder.Hex(0x6b3a22));
            return scrap = b.Build("boss-scrap");
        }
    }
}
