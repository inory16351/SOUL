using UnityEngine;

namespace Soul
{
    // Shore decor in sim space; placement belongs to WorldStreamer. No collision/emission.
    // Reeds: 0.6-1.5 m tall, radius .8. Scum: radius 1.2, y .0-.03 above water.
    // Both kinds stay below 500 triangles, including seed-dependent details.
    public static class WaterProps
    {
        public static (string id, System.Func<int, PropModel> build) Reeds(int zone) => ("Reeds" + zone, s => BuildReeds(s, zone));
        public static (string id, System.Func<int, PropModel> build) Scum(int zone) => ("Scum" + zone, s => BuildScum(s, zone));

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static PropModel BuildReeds(int seed, int zone)
        {
            var b = new MeshBuilder(seed);
            int stem = zone == 6 ? 0x969649 : zone == 5 ? 0x867052 : 0x7a8054;
            int leaf = zone == 6 ? 0xa5a15b : zone == 5 ? 0x9b825e : 0x93936a;
            int wet = zone == 6 ? 0x485237 : 0x4e4634;
            int n = b.RangeInt(8, 10);
            b.RotateY(b.Range(0, Mathf.PI * 2));
            for (int i = 0; i < n; i++)
            {
                // Two offset root groups leave a split instead of a circular tuft.
                Vector3 root = V((i % 2 == 0 ? -.16f : .16f) + b.Range(-.08f, .08f),
                    .025f, b.Range(-.20f, .20f));
                float h = i == 1 ? 1.46f : b.Range(.65f, 1.39f);
                bool broken = i % 4 == 0;
                Vector3 lean = V(b.Range(.05f, .17f), 0, b.Range(-.10f, .10f));
                Vector3 knee = root + lean * .45f + Vector3.up * (h * .56f);
                Vector3 tip = root + lean + Vector3.up * (h - root.y);
                if (broken) tip = knee + V(b.Range(.10f, .19f), -.13f, b.Range(-.08f, .08f));
                b.Beam(root, knee, .022f, .015f, 3, b.Jitter(stem, .12f));
                b.Beam(knee, tip, .015f, broken ? .012f : .006f, 3, b.Jitter(leaf, .1f));

                // Folded blades and ridges show from the overhead camera.
                float a = b.Range(0, Mathf.PI * 2);
                Vector3 outward = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 baseLeaf = Vector3.Lerp(root, knee, .5f);
                Blade(b, baseLeaf, baseLeaf + outward * .18f + Vector3.up * .22f,
                    baseLeaf + outward * .36f + Vector3.up * (broken ? .02f : .14f),
                    .045f, b.Jitter(leaf, .12f));
                if (!broken && (i % 3 == 1 || b.Chance(.3f)))
                {
                    Vector3 axis = (tip - knee).normalized;
                    b.Beam(tip - axis * .25f, tip - axis * .045f, .047f, .035f, 4,
                        b.Jitter(zone == 6 ? 0x655535 : 0x594330, .13f));
                }
                if (broken)
                {
                    TwoSidedTri(b, knee, knee + V(-.018f, .065f, .008f),
                        knee + V(.018f, .012f, -.008f), b.Jitter(0xb2a17a, .08f));
                }
                if (i < 4)
                {
                    b.Beam(root, Vector3.Lerp(root, knee, .23f), .024f, .019f, 3, b.Jitter(wet));
                    Blade(b, root, root + outward * .22f + Vector3.up * .09f,
                        root + outward * .43f + Vector3.up * .01f, .035f, b.Jitter(0x716044));
                }
            }
            if (zone == 1 || zone == 4)
            {
                // Thin bent scrap among the reeds, never a marker or solid obstacle.
                Vector3 foot = V(-.28f, .02f, .16f), bend = V(-.32f, .48f, .13f);
                b.Beam(foot, bend, .024f, .022f, 3, b.Jitter(0x78432c));
                b.Beam(bend, V(-.19f, .58f, .19f), .022f, .014f, 3, b.Jitter(0x514438));
            }
            return new PropModel { mesh = b.Build("reeds"), footprint = .8f, height = 1.5f };
        }

        static void TwoSidedTri(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Color color)
        {
            b.Tri(a, c, d, color);
            b.Tri(d, c, a, color);
        }

        static void Blade(MeshBuilder b, Vector3 root, Vector3 fold, Vector3 tip, float width, Color color)
        {
            Vector3 side = Vector3.Cross(tip - root, Vector3.up).normalized * width;
            Vector3 ridge = fold + Vector3.up * .018f;
            TwoSidedTri(b, root, fold - side, ridge, color);
            TwoSidedTri(b, root, ridge, fold + side, color * .78f);
            TwoSidedTri(b, fold - side, tip, ridge, color);
            TwoSidedTri(b, ridge, tip, fold + side, color * .78f);
        }

        static PropModel BuildScum(int seed, int zone)
        {
            var b = new MeshBuilder(seed);
            b.RotateY(b.Range(0, Mathf.PI * 2));
            if (zone == 4) OilFilm(b);
            else
            {
                bool mire = zone == 5 || zone == 6;
                int algae = zone == 6 ? 0x737d3f : zone == 5 ? 0x646b48 : 0x666d59;
                // Ragged rafts have actual water gaps, never a radial fan/disc.
                int count = mire ? 3 : 2;
                for (int row = 0; row < count; row++)
                {
                    float z = (row - (count - 1) * .5f) * .34f;
                    Raft(b, z, mire ? .15f : .07f, algae);
                }
                if (zone == 6)
                {
                    Foam(b, -.53f, 0xb0b08a);
                    Foam(b, .52f, 0x969d73);
                }
                else if (!mire) Foam(b, .34f, 0x8c9283);
            }
            return new PropModel { mesh = b.Build("scum"), footprint = 1.2f, height = .05f };
        }

        static void Raft(MeshBuilder b, float z, float width, int hex)
        {
            const int steps = 8;
            var left = new Vector3[steps + 1];
            var right = new Vector3[steps + 1];
            float halfLength = b.Range(.65f, .94f);
            for (int i = 0; i <= steps; i++)
            {
                float x = Mathf.Lerp(-halfLength, halfLength, i / (float)steps);
                float drift = b.Range(-.065f, .065f);
                float taper = i == 0 || i == steps ? .2f : 1;
                left[i] = V(x, .012f, z + drift - width * b.Range(.5f, 1) * taper);
                right[i] = V(x, .012f, z + drift + width * b.Range(.5f, 1) * taper);
            }
            for (int i = 0; i < steps; i++)
            {
                if (i == 3 || (i == 6 && b.Chance(.6f))) continue;
                Color tint = b.Jitter(hex, .18f);
                b.Tri(left[i], right[i], right[i + 1], tint);
                // Missing half-cells leave angular bites in the trailing edge.
                if (i % 3 != 1) b.Tri(left[i], right[i + 1], left[i + 1], b.Jitter(tint, .06f));
            }
        }

        static void Foam(MeshBuilder b, float z, int hex)
        {
            // Broken tapered ribbons with open water between foam flecks.
            for (int i = 0; i < 9; i++)
            {
                float x = -.88f + i * .20f;
                float drift = b.Range(-.045f, .045f), w = b.Range(.022f, .050f);
                float length = b.Range(.09f, .17f);
                Vector3 a = V(x, .024f, z + drift);
                Vector3 end = V(x + length, .024f, z + drift + b.Range(-.045f, .045f));
                b.Quad(a, a + V(.025f, 0, w), end + V(-.02f, 0, w * .55f), end, b.Jitter(hex, .1f));
                if (i % 2 == 0)
                    b.Tri(a + V(.04f, 0, -.035f), a + V(.06f, 0, -.012f),
                        a + V(.10f, 0, -.027f), b.Jitter(hex, .16f));
            }
        }

        static void OilFilm(MeshBuilder b)
        {
            // Adjacent bands share edges: restrained iridescence without layered z-fighting.
            int[] colors = { 0x202627, 0x3b3544, 0x344e4c, 0x625b42, 0x272a30 };
            const int steps = 8;
            for (int row = 0; row < 3; row++)
            {
                var centers = new Vector3[steps + 1];
                var widths = new float[steps + 1];
                float length = b.Range(.72f, .95f);
                for (int i = 0; i <= steps; i++)
                {
                    centers[i] = V(Mathf.Lerp(-length, length, i / (float)steps), .016f,
                        (row - 1) * .34f + b.Range(-.065f, .065f));
                    widths[i] = b.Range(.14f, .25f) * (i == 0 || i == steps ? .18f : 1);
                }
                for (int i = 0; i < steps; i++)
                {
                    if (i == 2 + row) continue;
                    for (int band = 0; band < colors.Length; band++)
                    {
                        float lo = band / (float)colors.Length - .5f;
                        float hi = (band + 1f) / colors.Length - .5f;
                        b.Quad(centers[i] + V(0, 0, lo * widths[i]),
                            centers[i] + V(0, 0, hi * widths[i]),
                            centers[i + 1] + V(0, 0, hi * widths[i + 1]),
                            centers[i + 1] + V(0, 0, lo * widths[i + 1]), b.Jitter(colors[band], .08f));
                    }
                }
            }
        }
    }
}
