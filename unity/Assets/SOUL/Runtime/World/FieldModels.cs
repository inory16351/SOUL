using UnityEngine;

namespace Soul
{
    // Field ecology meshes. All coordinates are sim space; only Build mirrors to Unity.
    public static class CritterMeshes
    {
        const int Steel = 0x484b49, Rust = 0x754c35, Soot = 0x302f2c;
        const float SeamY = .566f, SeamZ = .329f;
        static readonly Vector3 Lamp = new Vector3(.22f, .681f, .11f);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        /// Alpha lair column: sim-space, .9 m base, 3.4 m including bent rebar.
        public static Mesh LairPillar()
        {
            var b = new MeshBuilder(92);
            const int sides = 8;
            float[] heights = { 0, .18f, 1.10f, 1.43f, 1.47f, 2.60f, 3.10f };
            float[] radii = { .89f, .85f, .76f, .71f, .67f, .64f, .53f };
            var rings = new Vector3[heights.Length][];
            for (int row = 0; row < heights.Length; row++)
            {
                rings[row] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float r = radii[row] * b.Range(.94f, 1);
                    // A spalled corner and a jagged, slightly leaning crown.
                    if (row >= 5 && i == 2) r *= .76f;
                    float y = heights[row];
                    if (row == 3 || row == 4) y += (i % 3 - 1) * .09f;
                    if (row == 6) y += i % 3 == 0 ? .14f : i % 3 == 1 ? -.22f : -.04f;
                    rings[row][i] = V(Mathf.Sin(a) * r + row * .012f, y, Mathf.Cos(a) * r - row * .006f);
                }
            }
            for (int row = 0; row < heights.Length - 1; row++)
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    int paint = row == 3 ? 0x393632 : i % 3 == 0 ? 0x6f6a64 : 0x858078;
                    b.Quad(rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i], b.Jitter(paint, .07f));
                }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                b.Tri(V(.07f, 2.96f, -.03f), rings[6][i], rings[6][next], b.Jitter(0x9a9386, .08f));
                b.Tri(Vector3.zero, rings[0][next], rings[0][i], C(0x514d47));
            }
            // Irregular dark fissures follow actual faces, including the top fracture.
            for (int face = 0; face < sides; face += 2)
            {
                int next = (face + 1) % sides;
                float angle = (face + .5f) * Mathf.PI * 2 / sides;
                Vector3 outwards = V(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .004f;
                for (int row = 1; row < 6; row++)
                {
                    float t0 = row % 2 == 0 ? .38f : .59f, t1 = row % 2 == 0 ? .59f : .38f;
                    Vector3 a = Vector3.Lerp(rings[row][face], rings[row][next], t0) + outwards;
                    Vector3 c = Vector3.Lerp(rings[row + 1][face], rings[row + 1][next], t1) + outwards;
                    Vector3 across = (rings[row][next] - rings[row][face]).normalized * .014f;
                    b.Quad(a - across, a + across, c + across * .55f, c - across * .55f, C(0x393733));
                }
                // Rust runoff below an exposed rod; a broad tapered stain on concrete.
                Vector3 lo = Vector3.Lerp(rings[4][face], rings[4][next], .74f) + outwards;
                Vector3 hi = Vector3.Lerp(rings[5][face], rings[5][next], .72f) + outwards;
                Vector3 width = (rings[5][next] - rings[5][face]).normalized * .045f;
                b.Quad(lo - width * .2f, lo + width * .2f, hi + width, hi - width, C(0x625044));
            }
            // Rebar tips bend inward or sideways, never increasing the collision footprint.
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3 + .2f;
                Vector3 start = V(Mathf.Sin(a) * .34f + .06f, 2.78f, Mathf.Cos(a) * .34f - .03f);
                Vector3 bend = start + V(.025f, .45f + (i % 2) * .08f, -.015f);
                Vector3 tip = bend + V(Mathf.Cos(a) * .12f, .065f, -Mathf.Sin(a) * .12f);
                b.Beam(start, bend, .022f, .019f, 4, C(Rust));
                b.Beam(bend, tip, .019f, .012f, 4, C(0x4a3930));
            }
            // Broken stirrup across the crater at the top.
            b.Beam(V(-.26f, 3.03f, -.23f), V(.27f, 3.07f, -.21f), .014f, .014f, 4, C(Rust));
            b.Beam(V(.27f, 3.07f, -.21f), V(.35f, 3.12f, .08f), .014f, .009f, 4, C(Rust));
            // Rubble stays within .95 m even at the largest random radius.
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * .2f + b.Range(-.10f, .10f);
                float r = b.Range(.68f, .75f), size = b.Range(.105f, .17f);
                b.Rock(V(Mathf.Sin(a) * r, size * .48f, Mathf.Cos(a) * r), size,
                    b.Jitter(i % 3 == 0 ? 0x625e58 : 0x827b70), .12f, .55f, 0);
            }
            return b.Build("cracked alpha lair pillar");
        }

        /// Ash rat: roughly .35 m nose-to-rump, with a separate trailing tail.
        public static Mesh Rat()
        {
            var b = new MeshBuilder(77);
            // Rounded haunches and a tapered face, rather than a miniature machine.
            Pebble(b, V(0, .092f, -.035f), V(.083f, .073f, .135f), 0x655f56, 1);
            Pebble(b, V(0, .108f, .102f), V(.057f, .052f, .067f), 0x71695f);
            b.Beam(V(0, .095f, .134f), V(0, .075f, .187f), .033f, .009f, 4, C(0x554b43));
            for (int side = -1; side <= 1; side += 2)
            {
                Pebble(b, V(side * .067f, .063f, -.075f), V(.035f, .047f, .055f), 0x57514a);
                b.Box(V(side * .067f, .012f, -.056f), V(.036f, .024f, .061f), C(0x403a35));
                b.Box(V(side * .046f, .012f, .105f), V(.025f, .024f, .047f), C(0x484039));
                Ear(b, side);
                Pebble(b, V(side * .041f, .137f, .133f), V(.009f, .008f, .011f), 0xc4bc9e);
            }
            Vector3[] tail = { V(0, .066f, -.147f), V(-.018f, .034f, -.222f),
                V(-.065f, .018f, -.282f), V(-.052f, .012f, -.346f), V(.003f, .009f, -.381f) };
            for (int i = 0; i < tail.Length - 1; i++)
                b.Beam(tail[i], tail[i + 1], .012f - i * .0025f, .0095f - i * .0025f, 3, C(0x625047));
            // Three short, swept bony scutes break the back silhouette.
            for (int i = 0; i < 3; i++)
                b.Beam(V(0, .153f, -.075f + i * .045f), V(.004f, .184f + i * .003f, -.092f + i * .045f),
                    .014f, .002f, 3, C(0x9b927e));
            return b.Build("ash rat");
        }

        /// Sealed olive case; every attachment and fragment stays within 1.0 x .7 m.
        public static Mesh Crate()
        {
            var b = new MeshBuilder(78);
            CaseShell(b, .465f, .307f, .045f, .552f, 0x5f6656, true);
            CaseShell(b, .480f, .328f, .578f, .658f, 0x555c4f, false);
            // Rubber seal sits behind the two exposed luminous seal windows.
            b.Box(V(0, SeamY, 0), V(.918f, .028f, .636f), C(Soot));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(V(side * .33f, .026f, 0), V(.16f, .052f, .60f), C(Steel));
                // Over-centre latches: backing, tongue, hinge pin, oxidised runoff.
                b.Box(V(side * .29f, .483f, .318f), V(.097f, .158f, .028f), C(Steel));
                b.Box(V(side * .29f, .507f, .337f), V(.047f, .109f, .018f), C(0x92917d));
                b.Beam(V(side * .29f - .039f, .463f, .339f), V(side * .29f + .039f, .463f, .339f), .008f, .008f, 4, C(Rust));
                b.Quad(V(side * .29f - .008f, .275f, .308f), V(side * .29f + .014f, .235f, .308f),
                    V(side * .29f + .018f, .401f, .308f), V(side * .29f - .028f, .401f, .308f), C(0x695240));
                // Recessed side handle with visible air inside the loop.
                b.Box(V(side * .468f, .381f, 0), V(.018f, .126f, .224f), C(Soot));
                for (int end = -1; end <= 1; end += 2)
                    b.Beam(V(side * .470f, .406f, end * .088f), V(side * .490f, .363f, end * .088f), .009f, .009f, 4, C(Steel));
                b.Beam(V(side * .490f, .363f, -.088f), V(side * .490f, .363f, .088f), .009f, .009f, 4, C(0x77786b));
                // Lid wear rails and rear hinge barrels.
                b.Box(V(side * .344f, .666f, 0), V(.042f, .016f, .445f), C(0x81816e));
                b.Beam(V(side * .29f - .058f, .576f, -.327f), V(side * .29f + .058f, .576f, -.327f), .017f, .017f, 6, C(Steel));
                b.Box(V(side * .13f, SeamY, SeamZ - .002f), V(.123f, .022f, .008f), C(Soot));
            }
            // Cloth retaining strap runs across the top and down both faces.
            b.Box(V(-.062f, .668f, 0), V(.068f, .014f, .626f), C(0x383b32));
            b.Box(V(-.062f, .358f, .316f), V(.068f, .598f, .016f), C(0x414237));
            b.Box(V(-.062f, .358f, -.316f), V(.068f, .598f, .016f), C(0x414237));
            b.Box(V(-.062f, .677f, -.11f), V(.095f, .018f, .075f), C(0x777560));
            b.Box(V(-.062f, .688f, -.11f), V(.054f, .006f, .045f), C(Soot));
            b.Box(Lamp - V(0, .008f, 0), V(.152f, .018f, .061f), C(Soot));
            // Broad chipped paint on the top, readable from the gameplay camera.
            b.Quad(V(.28f, .659f, -.22f), V(.31f, .659f, -.15f), V(.41f, .659f, -.13f), V(.39f, .659f, -.20f), C(0x777769));
            b.Tri(V(-.41f, .659f, .18f), V(-.32f, .659f, .23f), V(-.27f, .659f, .19f), C(Rust));
            for (int i = 0; i < 5; i++)
            {
                float x = -.38f + i * .18f;
                Pebble(b, V(x, .018f, i % 2 == 0 ? .323f : -.323f), V(.035f, .017f, .022f), i % 2 == 0 ? 0x777166 : Rust);
            }
            return b.Build("supply crate");
        }

        /// Small physical seal windows and one recessed status lamp, sharing body coordinates.
        public static Mesh CrateGlow()
        {
            var b = new MeshBuilder(79);
            for (int side = -1; side <= 1; side += 2)
                b.Box(V(side * .13f, SeamY, SeamZ + .003f), V(.105f, .010f, .004f), Color.white);
            b.Box(Lamp, V(.112f, .006f, .023f), Color.white);
            return b.Build("supply crate glow");
        }

        /// Compact salvage silhouettes; the caller supplies a single gold unlit material.
        public static Mesh PartDrop(string kind)
        {
            var b = new MeshBuilder(80);
            if (kind == "skill:wave") WaveModule(b);
            else if (kind == "core:toxic")
            {
                // Swallowed reactor cartridge. Flat sides and separated fins still read
                // through the caller's single gold unlit material; retain toxic vertex colours.
                b.Cylinder(Vector3.zero, .17f, .17f, .045f, 10, C(Rust));
                b.Cylinder(V(0, .045f, 0), .143f, .132f, .255f, 10, C(Steel));
                b.Cylinder(V(0, .30f, 0), .16f, .147f, .04f, 10, C(0x807057));
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    b.Push(); b.RotateY(angle);
                    b.Box(V(0, .17f, .165f), V(.021f, b.Range(.18f, .25f), .12f), b.Jitter(i % 3 == 0 ? Rust : Steel));
                    b.Pop();
                }
                // Six faceted crystals, asymmetrically consuming the lid and one side.
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 2.1f, radius = i < 4 ? .10f : .16f;
                    Vector3 baseAt = V(Mathf.Sin(angle) * radius, i < 4 ? .31f : .17f, Mathf.Cos(angle) * radius);
                    b.Push(); b.Translate(baseAt); b.RotateZ(b.Range(-.35f, .35f)); b.RotateX(b.Range(-.25f, .25f));
                    float r = b.Range(.031f, .050f), height = b.Range(.10f, .16f);
                    Vector3 tip = V(r * .35f, height, -r * .25f);
                    Vector3[] corners = { V(-r, 0, -r), V(-r, 0, r), V(r, 0, r), V(r, 0, -r) };
                    for (int k = 0; k < 4; k++)
                        b.Tri(corners[k], corners[(k + 1) % 4], tip, b.Jitter(i % 2 == 0 ? 0x756080 : 0x718455, .12f));
                    b.Quad(corners[3], corners[2], corners[1], corners[0], C(0x434c34));
                    b.Pop();
                    Vector3 root = V(baseAt.x * .83f, .09f, baseAt.z * .83f);
                    Vector3 bend = V(baseAt.x * 1.12f, .22f, baseAt.z * 1.12f);
                    b.Beam(root, bend, .014f, .010f, 3, C(0x596d42));
                    b.Beam(bend, baseAt + V(0, .02f, 0), .010f, .006f, 3, C(0x6b5070));
                }
            }
            else if (kind == "skill:suck")
            {
                // Upturned +Z intake: an open funnel and hose bore, not a capped cylinder.
                b.Box(V(0, .024f, -.025f), V(.23f, .048f, .24f), C(Steel));
                b.Push(); b.Translate(0, .23f, .025f); b.RotateX(.95f);
                const int count = 12;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2 / count, c = (i + 1) * Mathf.PI * 2 / count;
                    Vector3 u = V(Mathf.Sin(a), 0, Mathf.Cos(a)), v = V(Mathf.Sin(c), 0, Mathf.Cos(c));
                    Vector3 outerA = u * .225f + V(0, .065f, 0), outerC = v * .225f + V(0, .065f, 0);
                    Vector3 innerA = u * .183f + V(0, .065f, 0), innerC = v * .183f + V(0, .065f, 0);
                    Vector3 throatA = u * .070f + V(0, -.10f, 0), throatC = v * .070f + V(0, -.10f, 0);
                    Vector3 backA = u * .070f + V(0, -.24f, 0), backC = v * .070f + V(0, -.24f, 0);
                    b.Quad(throatA + u * .025f, throatC + v * .025f, outerC, outerA, b.Jitter(Steel));
                    b.Quad(outerA, outerC, innerC, innerA, b.Jitter(Rust));
                    b.Quad(innerC, throatC, throatA, innerA, C(0x343a33));
                    b.Quad(throatC, backC, backA, throatA, C(Soot));
                    b.Quad(backA + u * .025f, backC + v * .025f, throatC + v * .025f, throatA + u * .025f, C(0x42473d));
                    b.Quad(backC + v * .025f, backA + u * .025f, backA, backC, C(Rust));
                    // Alternate teeth have lost their tips. Closed wedges leave open air between.
                    Vector3 tooth = (u + v).normalized * (i % 4 == 0 ? .160f : .132f) + V(0, .085f, 0);
                    Vector3 ta = Vector3.Lerp(innerA, innerC, .20f), tc = Vector3.Lerp(innerA, innerC, .80f);
                    Vector3 down = V(0, -.026f, 0);
                    b.Tri(ta, tc, tooth, C(0x99856b));
                    b.Tri(tooth + down, tc + down, ta + down, C(Rust));
                    b.Quad(ta, tooth, tooth + down, ta + down, C(Steel));
                    b.Quad(tooth, tc, tc + down, tooth + down, C(Rust));
                    b.Quad(tc, ta, ta + down, tc + down, C(Steel));
                }
                // Corrugated hose collars leave the throat open all the way through.
                for (int k = 0; k < 3; k++)
                {
                    b.Cylinder(V(0, -.225f + k * .045f, 0), .103f, .103f, .018f, 8, C(Rust), false);
                    b.Disc(V(0, -.207f + k * .045f, 0), .095f, .103f, 8, C(0x7a6b51));
                }
                b.Pop();
            }
            else LeapJoint(b);
            return b.Build("part drop");
        }

        static void LeapJoint(MeshBuilder b)
        {
            Color metal = C(Steel);
            // Bent fork and an exposed transverse knee axle.
            b.Beam(V(-.118f, .212f, -.012f), V(.118f, .212f, -.012f), .068f, .068f, 8, metal);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(V(side * .094f, .063f, -.167f), V(side * .094f, .212f, -.012f), .026f, .034f, 4, metal);
                b.Beam(V(side * .094f, .212f, -.012f), V(side * .094f, .356f, -.092f), .034f, .024f, 4, metal);
            }
            b.Box(V(0, .030f, -.163f), V(.247f, .060f, .092f), metal);
            b.Box(V(0, .372f, -.092f), V(.234f, .046f, .074f), metal);
            Vector3 a = V(0, .075f, .045f), end = V(0, .338f, .026f);
            Vector3 axis = (end - a).normalized, u = Vector3.right, v = Vector3.Cross(axis, u);
            b.Beam(a, Vector3.Lerp(a, end, .48f), .025f, .025f, 4, metal);
            b.Beam(Vector3.Lerp(a, end, .48f), end, .013f, .013f, 4, metal);
            b.Beam(V(0, .075f, -.163f), a, .019f, .019f, 4, metal);
            b.Beam(end, V(0, .356f, -.092f), .019f, .019f, 4, metal);
            // Three open helical turns; gaps survive the uniform glow material.
            Vector3 previous = a + u * .052f;
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f, angle = t * Mathf.PI * 6;
                Vector3 next = Vector3.Lerp(a, end, t) + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * .052f;
                b.Beam(previous, next, .008f, .008f, 3, metal); previous = next;
            }
            b.Beam(V(.09f, .34f, -.09f), V(.175f, .306f, -.035f), .011f, .010f, 3, metal);
            b.Beam(V(.175f, .306f, -.035f), V(.205f, .244f, .012f), .010f, .004f, 3, metal);
            b.Beam(V(-.08f, .355f, -.09f), V(-.164f, .418f, -.062f), .009f, .007f, 3, metal);
            b.Beam(V(-.164f, .418f, -.062f), V(-.191f, .432f, -.015f), .007f, .003f, 3, metal);
        }

        static void WaveModule(MeshBuilder b)
        {
            // An open, chipped octagonal bezel, not a solid luminous coin.
            const int segments = 8;
            Color metal = C(Steel);
            for (int i = 0; i < segments; i++)
            {
                if (i == 1) continue; // physically missing sector exposes the fracture
                float a = i * Mathf.PI / 4, c = (i + 1) * Mathf.PI / 4;
                Vector3 o0 = V(Mathf.Sin(a) * .158f, .148f, Mathf.Cos(a) * .158f);
                Vector3 o1 = V(Mathf.Sin(c) * .158f, .148f, Mathf.Cos(c) * .158f);
                Vector3 i0 = V(Mathf.Sin(a) * .109f, .148f, Mathf.Cos(a) * .109f);
                Vector3 i1 = V(Mathf.Sin(c) * .109f, .148f, Mathf.Cos(c) * .109f);
                Vector3 down = V(0, -.074f, 0);
                b.Quad(i0, o0, o1, i1, metal);
                b.Quad(o0 + down, i0 + down, i1 + down, o1 + down, metal);
                b.Quad(o0, o0 + down, o1 + down, o1, metal);
                b.Quad(i0, i1, i1 + down, i0 + down, metal);
                if (i == 0) b.Quad(i1, o1, o1 + down, i1 + down, metal);
                if (i == 2) b.Quad(o0, i0, i0 + down, o0 + down, metal);
            }
            // Separate lens fragments leave a broad dark crack through the aperture.
            LensShard(b, V(-.085f, .133f, -.058f), V(-.080f, .133f, .060f), V(-.012f, .151f, .006f));
            LensShard(b, V(.018f, .143f, -.083f), V(.014f, .143f, -.018f), V(.089f, .126f, -.012f));
            LensShard(b, V(.015f, .142f, .024f), V(.015f, .142f, .090f), V(.081f, .128f, .055f));
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                    b.Box(V(side * (.170f + (i == 1 ? .012f : 0)), .081f, -.073f + i * .059f),
                        V(i == 1 ? .072f : .055f, .124f - i * .018f, .020f), metal);
            b.Box(V(0, .035f, -.145f), V(.112f, .070f, .086f), metal);
            b.Box(V(0, .041f, -.205f), V(.067f, .048f, .034f), metal);
            b.Beam(V(-.045f, .056f, -.166f), V(-.106f, .026f, -.202f), .010f, .009f, 3, metal);
            b.Beam(V(-.106f, .026f, -.202f), V(-.144f, .022f, -.181f), .009f, .004f, 3, metal);
        }

        static void Pebble(MeshBuilder b, Vector3 center, Vector3 radii, int hex, int detail = 0)
        {
            b.Push(); b.Translate(center); b.Scale(radii.x, radii.y, radii.z);
            b.Rock(Vector3.zero, 1, C(hex), .035f, 1, detail); b.Pop();
        }

        static void Ear(MeshBuilder b, int side)
        {
            Vector3 center = V(side * .058f, .160f, .073f);
            Vector3[] rim = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                rim[i] = center + V(Mathf.Cos(angle) * .034f, Mathf.Sin(angle) * .032f, -.011f * Mathf.Sin(angle));
            }
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                b.Tri(center + V(0, 0, .009f), rim[i], rim[next], C(0x998579));
                b.Tri(center - V(0, 0, .012f), rim[next], rim[i], C(0x5b534b));
            }
        }

        // An eight-sided closed case with bevelled corners and a dented side ring.
        static void CaseShell(MeshBuilder b, float x, float z, float low, float high, int hex, bool dent)
        {
            Vector3[] outline = { V(-x + .048f, 0, -z), V(-x, 0, -z + .048f), V(-x, 0, z - .048f), V(-x + .048f, 0, z),
                V(x - .048f, 0, z), V(x, 0, z - .048f), V(x, 0, -z + .048f), V(x - .048f, 0, -z) };
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                Vector3 a = outline[i], c = outline[next];
                Vector3 al = a * .96f + V(0, low, 0), cl = c * .96f + V(0, low, 0);
                Vector3 ah = a + V(0, high, 0), ch = c + V(0, high, 0);
                if (dent)
                {
                    Vector3 am = a * (i == 6 ? .90f : 1) + V(0, high * .64f, 0);
                    Vector3 cm = c * (next == 6 ? .90f : 1) + V(0, high * .64f, 0);
                    b.Quad(al, cl, cm, am, b.Jitter(hex, .07f));
                    b.Quad(am, cm, ch, ah, b.Jitter(hex, .09f));
                }
                else b.Quad(al, cl, ch, ah, b.Jitter(hex));
                b.Tri(V(0, high, 0), ah, ch, b.Jitter(hex, .04f));
                b.Tri(V(0, low, 0), cl, al, C(Soot));
            }
        }

        // Closed triangular glass wedge. Inputs are CCW when viewed from above.
        static void LensShard(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d)
        {
            Vector3 down = V(0, -.018f, 0);
            b.Tri(a, c, d, Color.white); b.Tri(d + down, c + down, a + down, Color.white);
            b.Quad(a, a + down, c + down, c, Color.white);
            b.Quad(c, c + down, d + down, d, Color.white);
            b.Quad(d, d + down, a + down, a, Color.white);
        }
    }
}
