using UnityEngine;

namespace Soul
{
    // BOSS ENERGY PILLAR MODEL (Codex job 11). All construction is in sim space.
    // The Z0 boss arena raises these out of the ground (Game/BossPillars.cs): the player breaks them in the order their
    // lights come on. Body mesh = lit vertex colour; glow mesh = white vertex colour, tinted blue / red at runtime.
    // Foot and shaft fit r .75; only low floor fragments extend beyond it (r <= 1.3, y <= .35).
    // Capacitor crown reaches 4.18 m. Hit test is a capsule r .6 from y .2 to 3.6.
    public static class BossPillarProps
    {
        public static Mesh Pillar(out Mesh glow)
        {
            var b = new MeshBuilder(11);
            var g = new MeshBuilder(12);
            Color steel = MeshBuilder.Hex(0x44494b), soot = MeshBuilder.Hex(0x252728);
            Color rust = MeshBuilder.Hex(0x78432c), concrete = MeshBuilder.Hex(0x8f8b86);
            // Split arena slabs: radial gaps are actual cracks, with sheared undersides.
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7 + b.Range(-.045f, .045f);
                FloorShard(b, a, b.Range(.94f, 1.24f), b.Range(.13f, .29f), b.Jitter(concrete));
                b.Push(); b.RotateY(a);
                b.Beam(new Vector3(-.08f, .12f, .68f), new Vector3(.06f, .18f, .94f), .018f, .014f, 4, rust);
                if (i % 2 == 0) b.Rock(new Vector3(.16f, .055f, .92f), .13f, b.Jitter(0x6a6762), .16f, .45f, 0);
                b.Pop();
            }
            b.Cylinder(Vector3.zero, .7f, .65f, .38f, 8, concrete, true, .025f);
            b.Cylinder(new Vector3(0, .28f, 0), .72f, .72f, .13f, 8, soot);
            b.Cylinder(new Vector3(0, .41f, 0), .57f, .52f, 2.81f, 8, soot, false);
            // Eight split armour faces leave eight exposed conduits: 3-4 read from any side.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                b.Push(); b.RotateY(a);
                ArmourPlate(b, .34f, .48f, 1.58f, .555f, .07f, b.Jitter(steel));
                ArmourPlate(b, .32f, 1.64f, 3.16f, .545f, .065f, b.Jitter(i % 3 == 0 ? 0x5e625e : 0x44494b));
                b.Quad(new Vector3(-.13f, 2.43f, .549f), new Vector3(-.09f, 2.49f, .549f),
                    new Vector3(-.075f, 3.02f, .549f), new Vector3(-.13f, 3.02f, .549f), MeshBuilder.Hex(0x594333));
                b.Quad(new Vector3(.01f, .64f, .56f), new Vector3(.14f, .64f, .56f),
                    new Vector3(.14f, .83f, .56f), new Vector3(.01f, .83f, .56f), soot);
                for (int k = 0; k < 2; k++)
                {
                    float y = k == 0 ? .6f : 3.02f;
                    b.Box(new Vector3(.105f, y, .568f), new Vector3(.065f, .065f, .044f), rust);
                }
                b.Pop();
                g.Push(); g.RotateY(a + Mathf.PI / 8);
                // Raw faces keep energy vertices exactly white, without primitive shading.
                ArmourPlate(g, .075f, .56f, 3.2f, .572f, .03f, Color.white);
                g.Pop();
            }
            for (int k = 0; k < 2; k++)
            {
                float y = k == 0 ? 1.08f : 2.48f;
                b.Cylinder(new Vector3(0, y, 0), .64f, .64f, .13f, 8, rust);
                b.Cylinder(new Vector3(0, y + .035f, 0), .648f, .648f, .045f, 8, steel);
            }
            // Four clamped cable bundles return to the torn floor, entirely inside r .75.
            for (int i = 0; i < 4; i++)
            {
                b.Push(); b.RotateY(i * Mathf.PI / 2 + .16f);
                for (int j = 0; j < 2; j++)
                {
                    float x = (j - .5f) * .075f;
                    b.Beam(new Vector3(x, 1.02f, .58f), new Vector3(x, .44f, .66f), .027f, .03f, 3, soot);
                    b.Beam(new Vector3(x, .44f, .66f), new Vector3(x + .035f, .03f, .67f), .03f, .03f, 3, soot);
                }
                b.Box(new Vector3(0, .64f, .65f), new Vector3(.18f, .075f, .085f), steel);
                b.Cylinder(new Vector3(0, .41f, .61f), .05f, .05f, .06f, 6, rust);
                b.Pop();
            }
            // Broad capacitor head: exposed stack, recessed top coil and four protective jaws.
            b.Cylinder(new Vector3(0, 3.16f, 0), .57f, .71f, .16f, 12, steel);
            b.Cylinder(new Vector3(0, 3.32f, 0), .35f, .35f, .73f, 12, soot);
            for (int k = 0; k < 3; k++)
            {
                float y = 3.37f + k * .22f;
                b.Cylinder(new Vector3(0, y - .04f, 0), .54f, .54f, .045f, 8, b.Jitter(0x65584b));
                Sleeve(g, y + .018f, .35f, .47f, .085f, 8, Color.white);
            }
            Sleeve(b, 4.02f, .39f, .68f, .1f, 8, steel);
            Sleeve(g, 4.075f, .18f, .365f, .035f, 8, Color.white);
            b.Cylinder(new Vector3(0, 4.04f, 0), .13f, .13f, .14f, 8, rust);
            for (int i = 0; i < 4; i++)
            {
                b.Push(); b.RotateY(i * Mathf.PI / 2 + Mathf.PI / 4);
                ArmourPlate(b, .23f, 3.29f, 4.14f, .64f, .13f, b.Jitter(steel));
                b.Box(new Vector3(0, 3.46f, .648f), new Vector3(.16f, .12f, .025f), rust);
                b.Box(new Vector3(0, 4.145f, .53f), new Vector3(.07f, .05f, .07f), soot);
                b.Pop();
            }
            glow = g.Build("boss-pillar-glow");
            return b.Build("boss-pillar");
        }

        // Convex clipped plate in local XY, closed with CCW outside faces.
        static void ArmourPlate(MeshBuilder b, float width, float bottom, float top, float front, float depth, Color color)
        {
            float w = width * .5f, clip = Mathf.Min(.065f, width * .22f);
            var p = new[] { new Vector3(-w, bottom + clip, front), new Vector3(-w + clip, bottom, front),
                new Vector3(w, bottom, front), new Vector3(w, top - clip, front),
                new Vector3(w - clip, top, front), new Vector3(-w, top, front) };
            Vector3 center = new Vector3(0, (bottom + top) * .5f, front), back = Vector3.forward * depth;
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 a = p[i], c = p[(i + 1) % p.Length];
                b.Tri(center, a, c, color);
                b.Tri(center - back, c - back, a - back, color);
                b.Quad(a, a - back, c - back, c, color);
            }
        }

        static void FloorShard(MeshBuilder b, float angle, float reach, float height, Color color)
        {
            b.Push(); b.RotateY(angle);
            // Outer notch and sloping top break up the silhouette without a raised walking obstacle.
            var p = new[] { new Vector3(-.16f, height, .48f), new Vector3(-.24f, .045f, reach - .08f),
                new Vector3(-.02f, .025f, reach - .14f), new Vector3(.1f, .04f, reach),
                new Vector3(.23f, .06f, reach - .12f), new Vector3(.16f, height * .8f, .5f) };
            Vector3 center = new Vector3(0, height * .65f, .75f);
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 a = p[i], c = p[(i + 1) % p.Length];
                Vector3 loA = new Vector3(a.x * .86f, -.1f, a.z * .98f);
                Vector3 loC = new Vector3(c.x * .86f, -.1f, c.z * .98f);
                b.Tri(center, a, c, b.Jitter(color, .035f));
                b.Quad(a, loA, loC, c, MeshBuilder.Hex(0x6a6762));
                b.Tri(new Vector3(0, -.1f, .75f), loC, loA, MeshBuilder.Hex(0x4c4945));
            }
            b.Pop();
        }

        // Closed annulus, also used for the hollow tyre. No implicit colour multiplication.
        static void Sleeve(MeshBuilder b, float y, float inner, float outer, float height, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, c = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 u = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), v = new Vector3(Mathf.Sin(c), 0, Mathf.Cos(c));
                Vector3 o0 = u * outer + Vector3.up * y, o1 = v * outer + Vector3.up * y;
                Vector3 i0 = u * inner + Vector3.up * y, i1 = v * inner + Vector3.up * y, lift = Vector3.up * height;
                b.Quad(o0, o1, o1 + lift, o0 + lift, color);
                b.Quad(i1, i0, i0 + lift, i1 + lift, color);
                b.Quad(i0 + lift, o0 + lift, o1 + lift, i1 + lift, color);
                b.Quad(i1, o1, o0, i0, color);
            }
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
            Color rust = MeshBuilder.Hex(0x78432c), steel = MeshBuilder.Hex(0x44494b), oil = MeshBuilder.Hex(0x252728);
            // Central engine casting and sump give the tumbling lump weight on every face.
            b.Push(); b.RotateZ(-.18f); b.RotateY(.24f);
            b.Box(new Vector3(-.06f, -.03f, -.04f), new Vector3(.88f, .68f, .72f), steel);
            b.Box(new Vector3(-.06f, -.43f, -.04f), new Vector3(.65f, .22f, .53f), oil);
            for (int i = 0; i < 3; i++)
            {
                float x = -.32f + i * .25f;
                b.Cylinder(new Vector3(x, .31f, -.08f), .098f, .098f, .13f, 6, oil);
                b.Box(new Vector3(x, -.03f, -.418f), new Vector3(.065f, .52f, .045f), MeshBuilder.Hex(0x6a6762));
            }
            b.Pop();
            // Crumpled door/bonnet folds with chipped muted paint remaining on one panel.
            for (int i = 0; i < 3; i++)
            {
                b.Push(); b.RotateY(i * 2.09f + .3f); b.RotateZ(i == 1 ? -.38f : .25f);
                ArmourPlate(b, .9f, -.38f, .26f, .48f, .055f, b.Jitter(i == 1 ? 0x5e7470 : 0x78432c));
                b.Push(); b.Translate(0, .25f, .48f); b.RotateX(-.72f);
                ArmourPlate(b, .76f, 0, .34f, 0, .045f, b.Jitter(0x5c4a3a)); b.Pop();
                b.Box(new Vector3(.15f, -.12f, .49f), new Vector3(.3f, .04f, .025f), oil);
                b.Pop();
            }
            // A real tyre opening and recessed corroded rim, pinched into the engine side.
            b.Push(); b.Translate(.4f, -.13f, .14f); b.RotateZ(-1.1f); b.RotateX(.22f);
            Sleeve(b, -.13f, .19f, .37f, .24f, 10, oil);
            Sleeve(b, -.09f, .105f, .2f, .16f, 8, rust);
            for (int i = 0; i < 5; i++)
            {
                b.Push(); b.RotateY(i * Mathf.PI * 2 / 5);
                b.Box(new Vector3(0, 0, .372f), new Vector3(.06f, .2f, .025f), MeshBuilder.Hex(0x393936)); b.Pop();
            }
            b.Pop();
            b.Beam(new Vector3(-.65f, .25f, -.42f), new Vector3(-.46f, .63f, .12f), .028f, .025f, 4, rust);
            b.Beam(new Vector3(-.46f, .63f, .12f), new Vector3(.24f, .54f, .55f), .025f, .02f, 4, rust);
            b.Beam(new Vector3(-.48f, -.54f, .4f), new Vector3(.28f, -.65f, -.23f), .03f, .025f, 4, rust);
            return scrap = b.Build("boss-scrap");
        }
    }
}
