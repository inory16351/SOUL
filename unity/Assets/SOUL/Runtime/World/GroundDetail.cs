using UnityEngine;

namespace Soul
{
    // GROUND DETAIL (Codex-owned file). Flat detail meshes that sit on the ground of each 32 m chunk so open
    // ground has character: cracks and broken kerbs (ruins), faded lane paint and manhole-free asphalt seams
    // (city), glass shards (skyscraper grave), rusted bolts and cable runs (gorge / foundry), leaf / twig
    // litter and roots (woods), puddle sheen and reeds (mire), floor markings and drains (lab).
    // Rules: y between 0.005 and 0.15 (no collision is made; nothing may look like an obstacle),
    // no round / ring shapes, deterministic per chunk (seed from x0, z0), skip where
    // ZoneWorld.RidgeHeight(x, z) > 0.2 and inside the original arena (|x|, |z| < Regions.ARENA_HALF),
    // theme by ZoneWorld.At(x, z), lighter on trails (ZoneRoutes.Dist < ZoneRoutes.EDGE: wheel ruts / foot paths).
    // Budget ~1,500 triangles per chunk, ~1 ms. Colours sRGB via MeshBuilder; rendered lit with vertex colour.
    // GroundLook.cs (Claude) already paints per-zone colour patterns into the ground itself.
    public static class GroundDetail
    {
        const int Budget = 1500, Reserve = 60;
        const float Reach = 1.75f;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        /// Heights below are offsets above the terrain, not absolute world Y. The streamer
        /// attaches this mesh at the world origin, so drape its vertices before returning it.
        public static Mesh Decals(float x0, float z0, float size)
        {
            if (size < Reach * 2 || float.IsNaN(size) || float.IsInfinity(size)) return null;
            int seed;
            unchecked { seed = Mathf.RoundToInt(x0 * 16) * 73856093 ^ Mathf.RoundToInt(z0 * 16) * 19349663 ^ 70439; }
            var b = new MeshBuilder(seed);
            // Layout has its own stream: purification and richer motifs cannot move later patches.
            var layout = new MeshBuilder(seed ^ 0x175ab3);
            int n = Mathf.Clamp(Mathf.FloorToInt(size / 5), 1, 6);
            float step = size / n, slack = (step - Reach * 2) * .5f;
            for (int i = 0; i < n * n && b.TriangleCount <= Budget - Reserve; i++)
            {
                float x = x0 + (i % n + .5f) * step + layout.Range(-slack, slack);
                float z = z0 + (i / n + .5f) * step + layout.Range(-slack, slack);
                float yaw = layout.Range(-Mathf.PI, Mathf.PI);
                float life = layout.Range(0, 1), density = layout.Range(0, 1);
                bool alternate = layout.Chance(.5f);
                if (!Clear(x, z)) continue;
                int zone = ZoneWorld.At(x, z);
                if (zone < 0 || zone > 7) continue;
                bool healed = ZoneState.Purified(zone);
                float distance = ZoneRoutes.Dist(x, z, out var route);
                bool trail = distance < ZoneRoutes.EDGE;
                // A quiet transition at the trail edge, rather than litter spilling into its core.
                if (density > (trail ? .35f : .88f)) continue;
                if (healed && !trail && life > .7f) continue;
                if (trail && route != null)
                {
                    Vector2 dir = route.b - route.a;
                    yaw = Mathf.Atan2(dir.x, dir.y);
                }
                else if (zone == 1 || zone == 7)
                    yaw = Mathf.Round(yaw / (Mathf.PI * .5f)) * Mathf.PI * .5f + b.Range(-.07f, .07f);

                b.Push(); b.Translate(x, 0, z); b.RotateY(yaw);
                if (trail) Trail(b, zone, alternate);
                else if (healed && life < .4f) Shoots(b);
                else
                {
                    switch (zone)
                    {
                        case 0: Ruins(b, alternate); break;
                        case 1: City(b, alternate); break;
                        case 2: Glass(b); break;
                        case 3: Scrap(b); break;
                        case 4: Foundry(b); break;
                        case 5: Woods(b); break;
                        case 6: Mire(b); break;
                        case 7: Lab(b, alternate); break;
                    }
                }
                b.Pop();
            }
            if (b.TriangleCount == 0) return null;
            Mesh mesh = b.Build("ground detail");
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                // Build has already mirrored X; only sample terrain back in sim space.
                Vector3 p = vertices[i];
                p.y += Terrain.Height(-p.x, p.z);
                vertices[i] = p;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static bool Clear(float x, float z)
        {
            // Reject the whole rotated footprint at the arena, and sample its interior as
            // well as its corners at ridges. All motifs fit inside this square at any yaw.
            if (Mathf.Abs(x) < Regions.ARENA_HALF + Reach && Mathf.Abs(z) < Regions.ARENA_HALF + Reach) return false;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
            {
                float px = x + i * Reach, pz = z + j * Reach;
                if (ZoneWorld.RidgeHeight(px, pz) > .2f) return false;
                // Ground litter belongs on banks; don't spend the budget underneath deep water.
                if (Terrain.Water(px, pz, out _) > .1f) return false;
            }
            return true;
        }

        // Six straight, chipped edges: no radial outlines, discs, or round puddle stamps.
        // Perimeter is CCW from above (+Y); the centre fan keeps long patches terrain-friendly.
        static void Patch(MeshBuilder b, float x, float z, float width, float length, float y, int hex)
        {
            float w = width * .5f, l = length * .5f;
            Vector3 a = V(x - w, y, z - l * .78f), d = V(x - w, y, z + l * .5f);
            Vector3 e = V(x - w * .45f, y, z + l), f = V(x + w, y, z + l * .76f);
            Vector3 g = V(x + w * .87f, y, z - l * .7f), h = V(x + w * .38f, y, z - l);
            Vector3 o = V(x, y, z);
            Color c = b.Jitter(hex, .065f);
            b.Tri(o, a, d, c); b.Tri(o, d, e, c); b.Tri(o, e, f, c);
            b.Tri(o, f, g, c); b.Tri(o, g, h, c); b.Tri(o, h, a, c);
        }

        static void Strip(MeshBuilder b, Vector3 a, Vector3 end, float width, Color c)
        {
            Vector3 delta = end - a;
            Vector3 side = V(delta.z, 0, -delta.x).normalized * (width * .5f);
            b.Quad(a - side, end - side, end + side, a + side, c);
        }

        // Branching hairline rather than a dark, apparently impassable chasm.
        static void Crack(MeshBuilder b, float x, int hex, float y = .024f)
        {
            Vector3 last = V(x, y, -1.3f);
            for (int i = 1; i <= 5; i++)
            {
                Vector3 next = V(x + b.Range(-.16f, .16f), y, -1.3f + i * .5f);
                Strip(b, last, next, b.Range(.045f, .08f), C(hex));
                if (i == 3) Strip(b, next, next + V(.38f, 0, .27f), .035f, C(hex));
                last = next;
            }
        }

        static void Ruins(MeshBuilder b, bool kerb)
        {
            Crack(b, kerb ? -.35f : 0, 0x36332f);
            if (kerb)
            {
                // Almost buried kerb remnants: tops only, never a climbable-looking step.
                Patch(b, .3f, -.64f, .4f, 1.05f, .065f, 0x8b8780);
                Patch(b, .34f, .5f, .34f, .94f, .052f, 0x78766e);
                Strip(b, V(.18f, .07f, -.95f), V(.2f, .07f, -.35f), .06f, C(0xa29b90));
            }
            else
            {
                Patch(b, -.46f, -.55f, .65f, .9f, .014f, 0x898177);
                Patch(b, .5f, .5f, .74f, 1.1f, .014f, 0x655d54);
            }
        }

        static void City(MeshBuilder b, bool paint)
        {
            Patch(b, 0, 0, 1.55f, 2.65f, .012f, 0x49484b);
            Crack(b, .48f, 0x2e2e30);
            if (paint)
            {
                Patch(b, -.26f, -.7f, .18f, .91f, .032f, 0xaaa38b);
                Patch(b, -.23f, .38f, .14f, .75f, .032f, 0x898573);
            }
            else Patch(b, -.24f, .15f, .47f, 1.8f, .03f, 0x655447);
        }

        static void Glass(MeshBuilder b)
        {
            Patch(b, 0, 0, 1.4f, 2.3f, .012f, 0x484650);
            for (int i = 0; i < 5; i++)
            {
                float x = b.Range(-.65f, .65f), z = -.95f + i * .43f;
                float length = b.Range(.48f, .78f), width = b.Range(.18f, .36f);
                Vector3 a = V(x - width, .035f, z), tip = V(x + .12f, .05f, z + length);
                Vector3 end = V(x + width, .035f, z + .15f), fold = V(x, .058f, z + .24f);
                b.Tri(a, tip, fold, b.Jitter(0x677b7d));
                b.Tri(fold, tip, end, b.Jitter(0x858494));
                b.Tri(a, fold, end, C(0x4d6267));
            }
            Strip(b, V(-.8f, .06f, -.95f), V(-.65f, .06f, .9f), .06f, C(0x43494f));
        }

        static void Bolt(MeshBuilder b, float x, float z)
        {
            Strip(b, V(x, .037f, z - .28f), V(x, .037f, z + .24f), .065f, C(0x744a34));
            b.Quad(V(x - .1f, .075f, z - .1f), V(x - .1f, .075f, z + .1f),
                V(x + .1f, .075f, z + .1f), V(x + .1f, .075f, z - .1f), C(0x8c6649));
            Strip(b, V(x - .07f, .078f, z), V(x + .07f, .078f, z), .025f, C(0x46352c));
        }

        static void Scrap(MeshBuilder b)
        {
            Patch(b, .05f, 0, 1.05f, 2.5f, .012f, 0x4d3427);
            Vector3 last = V(-.35f, .045f, -1.3f);
            for (int i = 1; i <= 5; i++)
            {
                Vector3 next = V(b.Range(-.25f, .25f), .045f, -1.3f + .5f * i);
                Strip(b, last, next, .095f, C(0x292723));
                Strip(b, last + V(0, .008f, 0), next + V(0, .008f, 0), .025f, C(0x79604b));
                last = next;
            }
            Bolt(b, .62f, -.65f); Bolt(b, -.65f, .6f);
        }

        static void Foundry(MeshBuilder b)
        {
            Patch(b, -.28f, -.5f, 1.15f, 1.5f, .045f, 0x3b3029);
            Patch(b, .35f, .6f, .95f, 1.3f, .055f, 0x5c4030);
            Crack(b, -.03f, 0x241f1b, .068f);
            Patch(b, -.48f, -.6f, .16f, .95f, .065f, 0x847443);
            Bolt(b, .5f, .55f);
        }

        // Folded pointed leaf / flattened reed; the shallow ridge is visible from above.
        static void Leaf(MeshBuilder b, float x, float z, float length, float width, int hex, float height)
        {
            Vector3 a = V(x, .022f, z - length * .5f), tip = V(x + .12f, .027f, z + length * .5f);
            Vector3 left = V(x - width, .03f, z), right = V(x + width, .03f, z);
            Vector3 fold = V(x, height, z + .06f);
            Color c = b.Jitter(hex);
            b.Tri(a, left, fold, c); b.Tri(left, tip, fold, c);
            b.Tri(a, fold, right, c * .8f); b.Tri(fold, tip, right, c * .8f);
        }

        static void Woods(MeshBuilder b)
        {
            Vector3 a = V(-.5f, .055f, -1.25f), joint = V(.08f, .07f, -.25f), end = V(.25f, .04f, 1.2f);
            Strip(b, a, joint, .13f, C(0x56432e)); Strip(b, joint, end, .09f, C(0x665138));
            Strip(b, joint, V(.8f, .035f, .3f), .06f, C(0x4b3b2a));
            Strip(b, V(.15f, .054f, .45f), V(-.6f, .03f, .95f), .045f, C(0x57442e));
            for (int i = 0; i < 5; i++)
                Leaf(b, b.Range(-.65f, .65f), -.85f + .4f * i, b.Range(.48f, .8f), .17f,
                    i % 2 == 0 ? 0x75603b : 0x534b32, .075f);
        }

        static void Mire(MeshBuilder b)
        {
            Patch(b, -.12f, 0, 1.65f, 2.6f, .014f, 0x323e36);
            Patch(b, .32f, -.35f, .53f, 1.25f, .021f, 0x566443);
            // Broken, matte sky reflection, not emissive slime or a circular water ripple.
            Strip(b, V(-.62f, .028f, -.65f), V(-.53f, .028f, .15f), .075f, C(0x6b7b70));
            for (int i = 0; i < 4; i++)
                Leaf(b, .5f + b.Range(-.2f, .2f), -.65f + i * .4f, .85f, .065f, 0x75724b, .12f);
        }

        static void Lab(MeshBuilder b, bool drain)
        {
            if (drain)
            {
                Patch(b, 0, 0, .75f, 2.5f, .025f, 0x505b5c);
                // Solid flush grille with narrow slots, not a black open pit.
                for (int i = 0; i < 8; i++)
                {
                    float z = -.98f + i * .28f;
                    Strip(b, V(-.23f, .032f, z), V(.23f, .032f, z), .08f, C(0x303a3c));
                }
                Strip(b, V(-.31f, .036f, -1.02f), V(-.31f, .036f, 1.02f), .045f, C(0x8b9795));
                Patch(b, .57f, .35f, .21f, 1.1f, .014f, 0x695a47);
            }
            else
            {
                Patch(b, 0, 0, 1.5f, 2.55f, .012f, 0x777e7d);
                Crack(b, .42f, 0x4c5353);
                Patch(b, -.35f, -.6f, .13f, 1.1f, .032f, 0x9b9f90);
                Patch(b, -.34f, .6f, .11f, .65f, .032f, 0x899385);
            }
        }

        static void Trail(MeshBuilder b, int zone, bool paired)
        {
            Color worn = Color.Lerp(ZoneRoutes.TrailColor(zone), C(0x8e8473), .23f);
            int count = paired && zone < 5 ? 2 : 1;
            for (int j = 0; j < count; j++)
            {
                float x = count == 2 ? (j == 0 ? -.46f : .46f) : 0;
                for (int i = 0; i < 4; i++)
                {
                    float z = -1.25f + i * .62f;
                    Strip(b, V(x, .018f, z), V(x + b.Range(-.035f, .035f), .018f, z + .54f),
                        b.Range(.12f, .22f), b.Jitter(worn, .04f));
                }
            }
        }

        static void Shoots(MeshBuilder b)
        {
            Patch(b, -.12f, 0, 1.05f, 2.1f, .013f, 0x536044);
            for (int i = 0; i < 6; i++)
                Leaf(b, b.Range(-.45f, .45f), -.8f + i * .3f, b.Range(.4f, .65f), .09f,
                    i % 2 == 0 ? 0x7e9254 : 0x5f7b46, b.Range(.085f, .14f));
        }
    }
}
