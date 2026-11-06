using UnityEngine;
using System.Collections.Generic;

namespace Soul
{
    // TRACES + LANDMARKS (Codex-owned file).
    //
    // Traces: small ground props that tell the player "something went this way" along the last
    // ~110 m before a passage (ZoneRoutes.Placements kind 1, placed ON the trail, no collision):
    // claw scratches, drag marks, oil trails, shed scrap plates, a dead hound husk, bones and roots
    // in the woods, sludge footprints in the mire. Flat or knee-high; must not block walking.
    // Variants by `seed`, theme by `zone` (ZoneGraph zone id).
    //
    // Landmarks: one tall, unmistakable silhouette per destination zone, placed just past each
    // passage beside the trail (ZoneRoutes.PassageLandmarks) and drawn on the world map. Seen from
    // the top-down camera only within ~25 m, so the shape must read from above (tops, shadows):
    //   Z0 ash ruins: broken radio mast · Z1 fallen city: leaning office tower stump
    //   Z2 skyscraper grave: a toppled skyscraper's crown · Z3 scrap gorge: crane wreck
    //   Z4 war foundry: twin smoke stacks · Z5 dead woods: giant hollow tree
    //   Z6 toxic mire: sunk tanker bow · Z7 lab: sealed white dome / antenna
    // Landmarks may have colliders (footprint radius <= 10 m) but no walkable surfaces.
    public static class TraceProps
    {
        const float Pi = Mathf.PI;
        const int Concrete = 0x8f8b86, Dark = 0x55524e, Soot = 0x292b2a;
        const int Steel = 0x44494b, Rust = 0x78432c, Bone = 0xb6ae98;
        const int Bark = 0x3a3430, Sludge = 0x303b28;

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        // IDs identify geometry, not placement seeds, and cannot alias scatter cache entries.
        public static (string id, System.Func<int, PropModel> build) Trace(int zone, int seed)
        {
            int kind = new MeshBuilder(seed).RangeInt(0, 3);
            switch (zone)
            {
                case 1: return kind == 0 ? ("TraceDrag", DragMarks) : kind == 1 ? ("TraceArmour", ArmourPlates) : ("TraceOil", OilTrail);
                case 2: return kind == 0 ? ("TraceClaws", ClawScratches) : kind == 1 ? ("TraceArmour", ArmourPlates) : ("TraceBones", ScatteredBones);
                case 3: return kind == 0 ? ("TraceDrag", DragMarks) : kind == 1 ? ("TraceArmour", ArmourPlates) : ("TraceHound", HoundHusk);
                case 4: return kind == 0 ? ("TraceCart", CartTracks) : kind == 1 ? ("TraceOil", OilTrail) : ("TraceArmour", ArmourPlates);
                case 5: return kind == 0 ? ("TraceBones", ScatteredBones) : kind == 1 ? ("TraceClaws", ClawScratches) : ("TraceRoots", TornRoots);
                case 6: return kind == 0 ? ("TraceSludgeFeet", SludgeFootprints) : kind == 1 ? ("TraceBones", ScatteredBones) : ("TraceSludgeDrag", SludgeDrag);
                case 7: return kind == 0 ? ("TraceArmour", ArmourPlates) : kind == 1 ? ("TraceOil", OilTrail) : ("TraceHound", HoundHusk);
                default: return kind == 0 ? ("TraceClaws", ClawScratches) : kind == 1 ? ("TraceOil", OilTrail) : ("TraceHound", HoundHusk);
            }
        }

        public static (string id, System.Func<int, PropModel> build) Landmark(int zone)
        {
            switch (zone)
            {
                case 1: return ("PassageOffice", OfficeStump);
                case 2: return ("PassageCrown", FallenCrown);
                case 3: return ("PassageCrane", CraneWreck);
                case 4: return ("PassageStacks", TwinStacks);
                case 5: return ("PassageHollowTree", HollowTree);
                case 6: return ("PassageTanker", TankerBow);
                case 7: return ("PassageDome", SealedDome);
                default: return ("PassageRadio", RadioMast);
            }
        }

        static PropModel Finish(MeshBuilder b, string name, float radius, float height,
            List<PropCollider> colliders = null, bool landmark = false)
        {
            return new PropModel { mesh = b.Build(name), footprint = radius, height = height,
                colliders = colliders ?? new List<PropCollider>(), landmark = landmark };
        }

        // Flat chipped polygon, CCW from above; used for stains and shallow ground fragments.
        static void Patch(MeshBuilder b, Vector3 p, float rx, float rz, int hex)
        {
            var rim = new Vector3[9];
            for (int i = 0; i < rim.Length; i++)
            {
                float a = i * Pi * 2 / rim.Length, r = b.Range(.72f, 1);
                rim[i] = p + V(Mathf.Sin(a) * rx * r, 0, Mathf.Cos(a) * rz * r);
            }
            for (int i = 0; i < rim.Length; i++) b.Tri(p, rim[i], rim[(i + 1) % rim.Length], b.Jitter(hex, .045f));
        }

        // Closed eight-sided broken plate; all chips stay inside the supplied half extents.
        static void Plate(MeshBuilder b, Vector3 p, float rx, float rz, float thick, int hex)
        {
            var rim = new Vector3[8];
            for (int i = 0; i < rim.Length; i++)
            {
                float a = i * Pi * .25f, r = b.Range(.78f, 1);
                rim[i] = p + V(Mathf.Sin(a) * rx * r, thick, Mathf.Cos(a) * rz * r);
            }
            for (int i = 0; i < rim.Length; i++)
            {
                Vector3 a = rim[i], c = rim[(i + 1) % rim.Length];
                b.Tri(p + V(0, thick, 0), a, c, b.Jitter(hex));
                b.Quad(a - V(0, thick, 0), c - V(0, thick, 0), c, a, C(Dark));
                b.Tri(p, c - V(0, thick, 0), a - V(0, thick, 0), C(Soot));
            }
        }

        static void Smear(MeshBuilder b, Vector3 a, Vector3 end, float width, int hex)
        {
            Vector3 side = Vector3.Cross(Vector3.up, end - a).normalized * width * .5f;
            b.Quad(a - side, end - side * .3f, end + side * .3f, a + side, b.Jitter(hex));
        }

        // Traces are authored along +Z. ZoneRoutes currently supplies random trace yaw.
        static PropModel ClawScratches(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int set = 0; set < 2; set++) for (int claw = 0; claw < 3; claw++)
            {
                float x = (claw - 1) * .23f + (set == 0 ? -.35f : .35f), z = set * 1.35f - 1.45f;
                Vector3 a = V(x, .025f, z), bend = V(x + .13f, .025f, z + .6f);
                Vector3 end = V(x + .1f, .025f, z + b.Range(.85f, 1.25f));
                Smear(b, a, bend, .115f, Soot); Smear(b, bend, end, .08f, Soot);
                Smear(b, a + V(.09f, .007f, 0), bend + V(.09f, .007f, 0), .055f, 0x777066);
                Plate(b, V(x + .13f, .02f, z + 1.2f), .12f, .17f, .045f, Dark);
            }
            return Finish(b, "claw scratches", 2, .065f);
        }

        static PropModel DragMarks(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int lane = 0; lane < 2; lane++) for (int i = 0; i < 6; i++)
            {
                float x = (lane == 0 ? -.38f : .38f) + b.Range(-.08f, .08f), z = -1.8f + i * .6f;
                Smear(b, V(x, .027f, z), V(x + .08f, .027f, z + .53f), b.Range(.12f, .23f), Soot);
                Smear(b, V(x + .18f, .03f, z), V(x + .23f, .03f, z + .4f), .06f, 0x807267);
            }
            for (int i = 0; i < 4; i++) Plate(b, V(b.Range(-.7f, .7f), .025f, b.Range(-1.6f, 1.6f)), .2f, .3f, .06f, Rust);
            return Finish(b, "dragged metal furrows", 2.2f, .085f);
        }

        static PropModel OilTrail(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 10; i++)
            {
                float z = -1.6f + i * .34f, x = Mathf.Sin(i * .8f) * .22f;
                Patch(b, V(x, .02f + i * .001f, z), b.Range(.12f, .35f), b.Range(.15f, .4f), 0x242928);
                if (i % 3 == 0) Patch(b, V(x + .07f, .036f, z), .09f, .22f, 0x454c47);
            }
            Plate(b, V(-.55f, .02f, -.8f), .23f, .35f, .07f, Steel);
            return Finish(b, "leaking oil trail", 2.1f, .09f);
        }

        static PropModel ArmourPlates(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 3; i++)
            {
                b.Push(); b.Translate(b.Range(-.45f, .45f), .03f, i * .85f - .85f); b.RotateY(b.Range(-.5f, .5f));
                Plate(b, Vector3.zero, .48f, .6f, .12f, i == 1 ? 0x737d7b : Steel);
                Plate(b, V(.12f, .12f, -.06f), .25f, .36f, .05f, Rust);
                for (int j = 0; j < 2; j++) b.Cylinder(V(j * .4f - .2f, .13f, -.2f), .045f, .045f, .045f, 4, C(Dark));
                Smear(b, V(-.24f, .125f, .1f), V(.05f, .125f, .28f), .05f, Soot);
                b.Pop();
            }
            return Finish(b, "shed armour plates", 1.9f, .205f);
        }

        static PropModel HoundHusk(int seed)
        {
            var b = new MeshBuilder(seed);
            Patch(b, V(0, .018f, 0), .85f, 1.5f, 0x282b29);
            b.Beam(V(0, .22f, -.85f), V(0, .29f, .65f), .13f, .1f, 5, C(Steel));
            for (int i = 0; i < 4; i++)
            {
                float z = -.55f + i * .29f;
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(V(0, .28f, z), V(side * .33f, .37f, z + .06f), .047f, .04f, 4, C(Rust));
                    b.Beam(V(side * .33f, .37f, z + .06f), V(side * .42f, .1f, z + .12f), .04f, .025f, 4, C(Steel));
                }
            }
            Plate(b, V(-.12f, .38f, -.35f), .32f, .46f, .1f, 0x626b65);
            Plate(b, V(.06f, .16f, .92f), .27f, .39f, .2f, Steel);
            b.Box(V(.06f, .2f, 1.24f), V(.33f, .17f, .27f), C(Soot));
            for (int side = -1; side <= 1; side += 2) for (int leg = 0; leg < 2; leg++)
            {
                float z = leg == 0 ? -.62f : .48f;
                Vector3 knee = V(side * .62f, .14f, z - .16f);
                b.Beam(V(0, .24f, z), knee, .08f, .06f, 4, C(Steel));
                b.Beam(knee, V(side * .76f, .09f, z + .22f), .05f, .035f, 4, C(Rust));
            }
            return Finish(b, "broken hound husk", 1.6f, .48f);
        }

        static PropModel ScatteredBones(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = V(b.Range(-.65f, .3f), .1f, b.Range(-1.15f, 1.1f));
                Vector3 end = a + V(b.Range(.25f, .5f), .02f, b.Range(.18f, .45f));
                b.Beam(a, end, .055f, .075f, 5, b.Jitter(Bone));
                b.Rock(a, .09f, C(Bone), .1f, .7f, 0);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = i * .25f - .35f;
                b.Beam(V(-.4f, .08f, z), V(-.2f, .29f, z), .035f, .04f, 4, C(Bone));
                b.Beam(V(-.2f, .29f, z), V(.13f, .17f, z + .04f), .04f, .025f, 4, C(Bone));
            }
            b.Rock(V(.45f, .18f, .8f), .23f, C(Bone), .12f, .65f, 0);
            Patch(b, V(.48f, .336f, .88f), .075f, .08f, Soot);
            return Finish(b, "scattered bones", 1.8f, .35f);
        }

        static PropModel TornRoots(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 5; i++)
            {
                float x = b.Range(-.7f, .7f), z = b.Range(-1.3f, .8f);
                b.Beam(V(x, .08f, z), V(x + .2f, .14f, z + .6f), .1f, .025f, 5, b.Jitter(Bark));
                b.Beam(V(x + .1f, .1f, z + .3f), V(x - .28f, .06f, z + .55f), .045f, .01f, 4, C(Bark));
                Plate(b, V(x, .02f, z), .18f, .24f, .04f, 0x625844);
            }
            Smear(b, V(0, .024f, -1.5f), V(.2f, .024f, 1.6f), .3f, 0x252923);
            return Finish(b, "torn roots and disturbed litter", 2, .24f);
        }

        static PropModel SludgeFootprints(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 6; i++)
            {
                float x = i % 2 == 0 ? -.3f : .3f, z = i * .59f - 1.55f;
                Patch(b, V(x, .022f, z), .24f, .33f, 0x566147);
                Patch(b, V(x, .031f, z), .17f, .26f, 0x202b22);
                for (int toe = 0; toe < 3; toe++) Patch(b, V(x + (toe - 1) * .12f, .026f, z + .31f), .06f, .1f, Sludge);
                Smear(b, V(x, .034f, z - .1f), V(x - .07f, .034f, z - .4f), .13f, Sludge);
            }
            return Finish(b, "sludge footprints", 2.1f, .034f);
        }

        static PropModel SludgeDrag(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int i = 0; i < 7; i++)
            {
                float z = i * .48f - 1.45f, x = Mathf.Sin(i) * .18f;
                Patch(b, V(x, .021f + i * .002f, z), .43f, .4f, Sludge);
                Smear(b, V(x, .04f, z - .25f), V(x + .08f, .04f, z + .3f), .12f, 0x687347);
            }
            for (int i = 0; i < 3; i++) Plate(b, V(b.Range(-.6f, .6f), .03f, b.Range(-1.2f, 1.2f)), .18f, .3f, .07f, Bark);
            return Finish(b, "sludge drag trail", 2.1f, .1f);
        }

        static PropModel CartTracks(int seed)
        {
            var b = new MeshBuilder(seed);
            for (int lane = -1; lane <= 1; lane += 2) for (int i = 0; i < 9; i++)
            {
                float x = lane * .62f, z = -1.9f + i * .43f;
                Smear(b, V(x, .025f, z), V(x, .025f, z + .39f), .18f, 0x211f1c);
                Smear(b, V(x - .13f, .033f, z + .1f), V(x + .13f, .033f, z + .16f), .09f, Rust);
            }
            for (int i = 0; i < 4; i++) Plate(b, V(b.Range(-.4f, .4f), .02f, b.Range(-1.5f, 1.5f)), .13f, .2f, .06f, Rust);
            return Finish(b, "foundry cart tracks", 2.3f, .08f);
        }

        // A solid footing matches its base collider exactly. Broken debris is confined to its
        // top, so the landmark cannot leave uncollided rubble in the surrounding walking lane.
        static List<PropCollider> Foundation(MeshBuilder b, float hx, float hz, int hex)
        {
            // Missing corners break the slab outline; the three rectangles also define collision.
            b.Box(V(0, .8f, 0), V(hx * 2, 1.6f, (hz - .55f) * 2), C(hex));
            b.Box(V(0, .24f, 0), V(hx * 2, .3f, (hz - .55f) * 2), C(Soot));
            var cols = new List<PropCollider> { PropCollider.Rect(0, 0, hx, hz - .55f).Between(0, 1.6f) };
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (hz - .275f);
                b.Box(V(0, .8f, z), V((hx - .65f) * 2, 1.6f, .55f), b.Jitter(hex));
                b.Box(V(0, .24f, z), V((hx - .65f) * 2, .3f, .55f), C(Soot));
                cols.Add(PropCollider.Rect(0, z, hx - .65f, .275f).Between(0, 1.6f));
            }
            for (int i = 0; i < 15; i++)
            {
                float x = b.Range(-hx + 1.3f, hx - 1.3f), z = b.Range(-hz + .65f, hz - .65f);
                Plate(b, V(x, 1.6f, z), .6f, .55f, b.Range(.12f, .55f), b.Chance(.25f) ? Rust : hex);
            }
            // Dark jagged cracks lie on the solid top, not on free-standing sign-like panels.
            for (int i = 0; i < 5; i++)
            {
                float x = b.Range(-hx + 1, hx - 1);
                Smear(b, V(x, 1.607f, -hz + .1f), V(x + .4f, 1.607f, -hz + 1), .06f, Soot);
            }
            return cols;
        }

        static void Truss(MeshBuilder b, Vector3 a, Vector3 end, float r0, float r1, int bays, float gauge)
        {
            Vector3 axis = (end - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            for (int k = 0; k < bays; k++) for (int i = 0; i < 4; i++)
            {
                float t = k / (float)bays, next = (k + 1f) / bays;
                float angle = Pi * (.25f + i * .5f), angle2 = angle + Pi * .5f;
                Vector3 d = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                Vector3 e = u * Mathf.Cos(angle2) + v * Mathf.Sin(angle2);
                Vector3 p = Vector3.Lerp(a, end, t) + d * Mathf.Lerp(r0, r1, t);
                Vector3 q = Vector3.Lerp(a, end, next) + d * Mathf.Lerp(r0, r1, next);
                Vector3 r = Vector3.Lerp(a, end, next) + e * Mathf.Lerp(r0, r1, next);
                b.Beam(p, q, gauge, gauge * .9f, 4, b.Jitter(Steel));
                b.Beam(q, r, gauge * .7f, gauge * .7f, 4, C(Rust));
                b.Beam(p, r, gauge * .55f, gauge * .55f, 3, C(Rust));
            }
        }

        static void Cable(MeshBuilder b, Vector3 a, Vector3 end, float sag)
        {
            Vector3 p = a;
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 6f;
                Vector3 q = Vector3.Lerp(a, end, t) - V(0, 4 * sag * t * (1 - t), 0);
                b.Beam(p, q, .045f, .045f, 3, C(Soot)); p = q;
            }
        }

        // Open, thick-walled, jagged tube. Ring tops are shared by outer/inner faces and the lip.
        // Intermediate rings carry alternating soot/rust bands and a small lean.
        static void Shell(MeshBuilder b, Vector3 p, float r0, float r1, float height,
            float wall, int hex, float lean, int segments = 12)
        {
            const int levels = 5;
            var outer = new Vector3[levels + 1, segments];
            var inner = new Vector3[levels + 1, segments];
            for (int j = 0; j <= levels; j++) for (int i = 0; i < segments; i++)
            {
                float t = j / (float)levels, a = i * Pi * 2 / segments;
                float radius = Mathf.Lerp(r0, r1, t), y = height * t;
                if (j == levels) y -= i % 4 == 0 ? b.Range(.5f, 1.3f) : b.Range(0, .22f);
                Vector3 center = p + V(lean * t, y, 0), radial = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                outer[j, i] = center + radial * radius;
                inner[j, i] = center + radial * (radius - wall);
            }
            for (int j = 0; j < levels; j++) for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                int tint = j == levels - 1 ? Soot : i % 5 == 0 ? Dark : hex;
                b.Quad(outer[j, i], outer[j, n], outer[j + 1, n], outer[j + 1, i], b.Jitter(tint));
                b.Quad(inner[j, n], inner[j, i], inner[j + 1, i], inner[j + 1, n], C(Soot));
            }
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                b.Quad(inner[levels, i], outer[levels, i], outer[levels, n], inner[levels, n], b.Jitter(hex));
            }
            b.Disc(p + V(0, .02f, 0), 0, r0 - wall, segments, C(Soot));
        }

        static PropModel RadioMast(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 4.2f, 4.2f, Concrete);
            Truss(b, V(0, 1.7f, 0), V(.8f, 27, .3f), 3.6f, .65f, 7, .16f);
            Truss(b, V(.8f, 27, .3f), V(4, 31, .8f), .65f, .24f, 2, .1f);
            b.Beam(V(4, 31, .8f), V(3.7f, 34, 1), .1f, .025f, 5, C(Steel));
            Cable(b, V(.4f, 23, 0), V(-3.3f, 2.2f, -3.3f), 1.3f);
            Cable(b, V(.4f, 23, 0), V(3.3f, 2.2f, 3.3f), .7f);
            // Concave, visibly broken radio dish; doubled faces give a readable upper silhouette.
            Vector3 hub = V(-1.2f, 22.5f, 0);
            b.Beam(hub, V(.6f, 22.5f, 0), .13f, .13f, 4, C(Steel));
            b.Beam(V(-1.7f, 21.5f, 0), V(.6f, 20.5f, 0), .09f, .09f, 4, C(Rust));
            for (int i = 0; i < 9; i++)
            {
                float a = i * Pi * 2 / 12, c = (i + 1) * Pi * 2 / 12;
                Vector3 p = V(-2.4f, 22.5f + Mathf.Sin(a) * 2.4f, Mathf.Cos(a) * 2.4f);
                Vector3 q = V(-2.4f, 22.5f + Mathf.Sin(c) * 2.4f, Mathf.Cos(c) * 2.4f);
                b.Tri(hub, p, q, b.Jitter(0x777b76)); b.Tri(hub, q, p, C(Dark));
                b.Beam(p, q, .06f, .06f, 4, C(Rust));
            }
            b.Box(V(0, 2.3f, 0), V(2.6f, 1.4f, 2.2f), C(Steel));
            cols.Add(PropCollider.Rect(0, 0, 1.3f, 1.1f).Between(1.6f, 3));
            return Finish(b, "broken radio mast", 6, 34.1f, cols, true);
        }

        static PropModel OfficeStump(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 5.3f, 4.6f, Dark);
            // Sheared storeys, capped core and window voids distinguish this from a solid box.
            for (int floor = 0; floor < 8; floor++)
            {
                float y = 1.6f + floor * 3.05f, x = floor * .28f;
                b.Push(); b.Translate(x, y, 0);
                Plate(b, Vector3.zero, 4.8f, 4.1f, .38f, Concrete);
                if (floor < 7)
                {
                    b.Box(V(0, 1.65f, 0), V(3.8f, 2.55f, 3), C(Dark));
                    for (int side = -1; side <= 1; side += 2) for (int k = 0; k < 3; k++)
                    {
                        float z = (k - 1) * 2.7f;
                        b.Beam(V(side * 3.25f, .35f, z), V(side * 3.25f + .28f, 3.05f, z), .19f, .17f, 4, b.Jitter(Concrete));
                        if ((floor + k) % 3 != 1) b.Box(V(side * 3.25f + .12f, 1, z), V(.22f, 1.25f, 1.7f), C(0x56605f));
                    }
                }
                for (int k = 0; k < 3; k++) b.Beam(V(-2 + k * 1.7f, .36f, 2.6f), V(-1.8f + k * 1.7f, b.Range(.9f, 1.7f), 2.8f), .035f, .023f, 4, C(Rust));
                b.Pop();
            }
            Shell(b, V(2, 23.35f, -.6f), 1.5f, 1.4f, 3.8f, .15f, Steel, .25f, 10);
            b.Box(V(3.5f, 23.7f, 1.4f), V(1.2f, .7f, 1.6f), C(Steel));
            Cable(b, V(4.9f, 22, 2.7f), V(4.2f, 9, 3), 1);
            cols.Add(PropCollider.Rect(0, 0, 1.9f, 1.5f).Between(1.6f, 4.6f));
            return Finish(b, "leaning office stump", 8.4f, 27.15f, cols, true);
        }

        static PropModel FallenCrown(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 6, 5, Concrete);
            // A severed crown lodged steeply in debris: torn underside and folded spire,
            // rather than a full horizontal tower that would violate the 10 m footprint.
            b.Push(); b.Translate(2, 2.5f, 0); b.RotateZ(.23f);
            for (int tier = 0; tier < 5; tier++)
            {
                float y = tier * 3.5f, r = 4.2f - tier * .55f;
                Plate(b, V(0, y, 0), r, r, .42f, tier % 2 == 0 ? 0x78757e : Steel);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Pi * .25f;
                    Vector3 p = V(Mathf.Sin(a) * r * .83f, y + .3f, Mathf.Cos(a) * r * .83f);
                    Vector3 q = V(Mathf.Sin(a) * (r - .55f) * .83f, y + 3.5f, Mathf.Cos(a) * (r - .55f) * .83f);
                    b.Beam(p, q, .15f, .1f, 4, b.Jitter(Steel));
                    if ((i + tier) % 3 != 0)
                    {
                        Vector3 tangent = V(Mathf.Cos(a), 0, -Mathf.Sin(a)) * .55f;
                        b.Quad(p - tangent, p + tangent, q + tangent * .6f, q - tangent * .6f, C(0x4b5559));
                        b.Quad(p + tangent, p - tangent, q - tangent * .6f, q + tangent * .6f, C(Dark));
                    }
                }
            }
            Truss(b, V(0, 17.5f, 0), V(0, 24, 0), 1.4f, .35f, 3, .11f);
            b.Beam(V(0, 24, 0), V(2, 26, .7f), .12f, .03f, 5, C(Steel));
            for (int i = 0; i < 7; i++) b.Beam(V(-2 + i * .65f, .5f, 2), V(-2.3f + i * .65f, b.Range(1.5f, 3), 2.5f), .045f, .025f, 4, C(Rust));
            b.Pop();
            return Finish(b, "lodged skyscraper crown", 8.8f, 28.4f, cols, true);
        }

        static PropModel CraneWreck(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 5.2f, 4.3f, Rust);
            Truss(b, V(-1.7f, 1.7f, 0), V(-2, 25, 0), 2.1f, 1.25f, 6, .2f);
            b.Box(V(-2, 24.8f, 0), V(3.8f, .7f, 3.3f), C(Steel));
            b.Box(V(-3.2f, 26, 0), V(2.2f, 1.7f, 2), C(0x7b6548));
            b.Box(V(-3.2f, 26.3f, 1.012f), V(1.5f, .7f, .025f), C(Soot));
            Truss(b, V(-1, 25.5f, 0), V(6.6f, 28, 0), 1, .7f, 4, .13f);
            Truss(b, V(6.6f, 28, 0), V(3.3f, 20, 2.3f), .7f, .4f, 3, .11f);
            b.Box(V(-5, 25, -.1f), V(2.6f, 2, 2.5f), C(Dark));
            b.Beam(V(-2, 25, 0), V(-2, 30, 0), .25f, .14f, 5, C(Rust));
            Cable(b, V(-2, 30, 0), V(6.6f, 28, 0), .5f);
            Cable(b, V(3.3f, 20, 2.3f), V(3.6f, 10, 2.3f), .5f);
            b.Beam(V(3.6f, 10, 2.3f), V(4.3f, 9.4f, 2.3f), .17f, .17f, 5, C(Steel));
            b.Beam(V(4.3f, 9.4f, 2.3f), V(4.7f, 10, 2.3f), .17f, .08f, 5, C(Steel));
            b.Box(V(-1.7f, 2.3f, 0), V(2.5f, 1.4f, 2.5f), C(Steel));
            cols.Add(PropCollider.Rect(-1.7f, 0, 1.25f, 1.25f).Between(1.6f, 3));
            return Finish(b, "folded crane wreck", 8, 30.15f, cols, true);
        }

        static PropModel TwinStacks(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 6, 4.6f, Dark);
            for (int stack = 0; stack < 2; stack++)
            {
                float x = stack == 0 ? -3 : 3, height = stack == 0 ? 35 : 28;
                Shell(b, V(x, 1.6f, 0), 2.15f, 1.7f, height, .38f, stack == 0 ? 0x6f5041 : 0x655953, stack == 0 ? -.5f : .4f);
                cols.Add(PropCollider.Circle(x, 0, 2.1f).Between(1.6f, 6));
                for (int band = 0; band < 5; band++)
                {
                    float y = 4 + band * 5.5f, t = (y - 1.6f) / height;
                    float radius = Mathf.Lerp(2.15f, 1.7f, t) + .06f;
                    b.Cylinder(V(x + (stack == 0 ? -.5f : .4f) * t, y, 0), radius, radius, .18f, 12, C(Steel), false);
                }
                for (int rung = 0; rung < 20; rung++)
                {
                    float y = 3 + rung * 1.1f;
                    b.Beam(V(x - .32f, y, 2.18f), V(x + .32f, y, 2.18f), .045f, .045f, 4, C(Rust));
                }
                b.Beam(V(x - .32f, 3, 2.18f), V(x - .32f, 24, 2.18f), .06f, .06f, 4, C(Steel));
                b.Beam(V(x + .32f, 3, 2.18f), V(x + .32f, 24, 2.18f), .06f, .06f, 4, C(Steel));
            }
            b.Beam(V(-3, 7, -1.9f), V(3, 7, -1.9f), .55f, .55f, 8, C(Rust));
            return Finish(b, "twin broken smoke stacks", 8, 36.6f, cols, true);
        }

        static PropModel HollowTree(int seed)
        {
            var b = new MeshBuilder(seed); var cols = new List<PropCollider>();
            Shell(b, V(0, 1.8f, 0), 3.7f, 2.1f, 21.2f, .8f, Bark, .7f);
            // The lowest trunk wall and each root have exact rectangular collision matches.
            // No central circle fills the hollow, and low root tips fall below the step height.
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) * Pi / 6;
                b.Push(); b.RotateY(a);
                b.Box(V(0, .9f, 3.16f), V(1.92f, 1.8f, .82f), b.Jitter(Bark));
                cols.Add(PropCollider.Rect(Mathf.Sin(a) * 3.16f, Mathf.Cos(a) * 3.16f,
                    .96f, .41f, a).Between(0, 1.8f));
                if (i % 2 == 0) for (int part = 0; part < 3; part++)
                {
                    float r = 3.5f + part * 1.05f, width = .6f - part * .18f;
                    float h = part == 0 ? 1.25f : part == 1 ? .82f : .36f;
                    b.Box(V(0, h * .5f, r), V(width * 2, h, 1.1f), b.Jitter(Bark));
                    Plate(b, V(0, h, r), width * .85f, .48f, .05f, 0x51483d);
                    Smear(b, V(0, h + .053f, r - .4f), V(.06f, h + .053f, r + .4f), .065f, Soot);
                    cols.Add(PropCollider.Rect(Mathf.Sin(a) * r, Mathf.Cos(a) * r,
                        width, .55f, a).Between(0, h));
                }
                b.Pop();
            }
            for (int branch = 0; branch < 5; branch++)
            {
                float a = branch * Pi * 2 / 5 + .4f;
                Vector3 start = V(.5f, 12 + branch * 1.3f, 0);
                Vector3 fork = V(Mathf.Sin(a) * 4.8f, 21 + branch * 1.6f, Mathf.Cos(a) * 4.8f);
                Vector3 tip = V(Mathf.Sin(a) * 7, 25 + branch * 1.25f, Mathf.Cos(a) * 7);
                b.Beam(start, fork, .75f, .36f, 7, b.Jitter(Bark));
                b.Beam(fork, tip, .36f, .035f, 5, C(0x524a40));
                b.Beam(fork, fork + V(Mathf.Cos(a) * 2, 3.2f, -Mathf.Sin(a) * 2), .24f, .025f, 5, C(Bark));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Pi * .25f;
                b.Beam(V(Mathf.Sin(a) * 3.68f, .6f, Mathf.Cos(a) * 3.68f),
                    V(.65f + Mathf.Sin(a) * 2.15f, 21, Mathf.Cos(a) * 2.15f), .09f, .045f, 4, C(i % 2 == 0 ? Soot : 0x51483d));
            }
            return Finish(b, "giant hollow dead tree", 7.6f, 31, cols, true);
        }

        static PropModel TankerBow(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 5.8f, 5.8f, Sludge);
            // Bow rises out of the mire: broad torn stern at the bottom, narrowing prow above.
            var rings = new Vector3[5, 6];
            float[] xs = { -1, -1, 0, 1, 1, 0 }, zs = { -.8f, .45f, 1, .45f, -.8f, -1 };
            for (int level = 0; level < 5; level++) for (int i = 0; i < 6; i++)
            {
                float t = level / 4f, width = Mathf.Lerp(4.9f, 1.05f, t), depth = Mathf.Lerp(4.9f, 1.3f, t);
                rings[level, i] = V(xs[i] * width, 1.6f + t * 22, zs[i] * depth + t * 2);
            }
            for (int level = 0; level < 4; level++) for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                b.Quad(rings[level, i], rings[level, n], rings[level + 1, n], rings[level + 1, i],
                    b.Jitter(level == 0 ? Sludge : i % 3 == 0 ? Rust : 0x505d56));
                b.Beam(rings[level + 1, i], rings[level + 1, n], .075f, .075f, 4, C(Rust));
            }
            // Thick rim and recessed deck: reads as a hull from above, with empty cargo hatches.
            for (int i = 0; i < 6; i++)
            {
                int n = (i + 1) % 6;
                Vector3 a = rings[4, i], c = rings[4, n], insetA = V(a.x * .7f, a.y, 2 + (a.z - 2) * .7f);
                Vector3 insetC = V(c.x * .7f, c.y, 2 + (c.z - 2) * .7f);
                b.Quad(insetA, a, c, insetC, C(Rust));
                b.Quad(insetC, insetA, insetA - V(0, 1.2f, 0), insetC - V(0, 1.2f, 0), C(Soot));
            }
            Plate(b, V(0, 22.3f, 2), .85f, 1.1f, .1f, Soot);
            b.Beam(V(0, 17, -1), V(.6f, 28, -.5f), .16f, .07f, 6, C(Steel));
            b.Beam(V(-1.5f, 25, -.6f), V(2, 25, -.6f), .12f, .12f, 4, C(Rust));
            Cable(b, V(.6f, 27.5f, -.5f), V(2.9f, 12, 2.8f), .8f);
            Shell(b, V(-1.9f, 12, -.4f), .65f, .65f, 2.8f, .16f, Rust, .1f, 8);
            // Sloping forecastle fittings distinguish a ship's rising bow from a stone spire.
            // Rail feet interpolate the same tapered hull section as the visible skin.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int rail = 0; rail < 5; rail++)
                {
                    float y = 7 + rail * 2.6f, t = (y - 1.6f) / 22, next = (y + 2.6f - 1.6f) / 22;
                    Vector3 p = V(side * Mathf.Lerp(4.9f, 1.05f, t) * .72f, y,
                        2 * t + Mathf.Lerp(4.9f, 1.3f, t) * .604f + .08f);
                    Vector3 q = V(side * Mathf.Lerp(4.9f, 1.05f, next) * .72f, y + 2.6f,
                        2 * next + Mathf.Lerp(4.9f, 1.3f, next) * .604f + .48f);
                    b.Beam(p, p + V(0, 0, .4f), .08f, .08f, 4, C(Rust));
                    b.Beam(p + V(0, 0, .4f), q, .06f, .06f, 4, C(Steel));
                }
                b.Push(); b.Translate(side * 1.05f, 18.5f, 3.15f); b.RotateX(Pi * .5f);
                b.Cylinder(Vector3.zero, .45f, .45f, .17f, 10, C(Rust));
                b.Cylinder(V(0, .175f, 0), .28f, .28f, .015f, 10, C(Soot)); b.Pop();
                Cable(b, V(side * 1.05f, 18.5f, 3.35f), V(side * 2, 10, 3.6f), .7f);
                b.Beam(V(side * 2, 10, 3.6f), V(side * 2, 8.5f, 3.6f), .13f, .13f, 5, C(Steel));
                b.Beam(V(side * 2 - .5f, 9, 3.6f), V(side * 2 + .5f, 9, 3.6f), .13f, .13f, 5, C(Steel));
            }
            b.Push(); b.Translate(0, 10, -2.86f); b.RotateX(-1.32f);
            Plate(b, Vector3.zero, 1.5f, 1.6f, .25f, Steel);
            b.Cylinder(V(0, .25f, 0), .85f, .85f, .4f, 10, C(0x6c7770));
            b.Beam(V(-.7f, .7f, 0), V(.7f, .7f, 0), .06f, .06f, 4, C(Rust));
            b.Pop();
            // Hull's submerged bottom fills the same visible base; no separate giant round blocker.
            return Finish(b, "sunken tanker bow", 8.5f, 28.1f, cols, true);
        }

        static PropModel SealedDome(int seed)
        {
            var b = new MeshBuilder(seed); var cols = Foundation(b, 6.4f, 6.4f, 0x797f7e);
            const int segments = 16, levels = 7;
            var rings = new Vector3[levels, segments];
            for (int level = 0; level < levels; level++) for (int i = 0; i < segments; i++)
            {
                float a = i * Pi * 2 / segments, t = level / (float)levels * Pi * .5f;
                rings[level, i] = V(Mathf.Sin(a) * Mathf.Cos(t) * 6, 1.6f + Mathf.Sin(t) * 12.8f, Mathf.Cos(a) * Mathf.Cos(t) * 6);
            }
            for (int level = 0; level < levels - 1; level++) for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                int hex = level == 0 ? 0x687572 : (i + level * 3) % 11 == 0 ? 0x707e7d : 0xb0b7b4;
                b.Quad(rings[level, i], rings[level, n], rings[level + 1, n], rings[level + 1, i], b.Jitter(hex, .035f));
                if (i % 2 == 0) b.Beam(rings[level, i], rings[level + 1, i], .045f, .045f, 4, C(0x606b6c));
            }
            for (int i = 0; i < segments; i++) b.Tri(V(0, 14.4f, 0), rings[levels - 1, i], rings[levels - 1, (i + 1) % segments], C(0xb0b7b4));
            b.Cylinder(V(0, 14, 0), .85f, .65f, 1.1f, 10, C(Steel));
            Truss(b, V(0, 15, 0), V(0, 23, 0), .65f, .2f, 4, .08f);
            b.Beam(V(0, 23, 0), V(0, 27, 0), .09f, .025f, 6, C(0x8f9998));
            b.Beam(V(-2.3f, 21, 0), V(2.3f, 21, 0), .09f, .09f, 5, C(Steel));
            for (int i = -1; i <= 1; i += 2) b.Beam(V(i * 2, 20, 0), V(i * 2, 22.3f, 0), .08f, .055f, 5, C(Steel));
            // A shut, recessed service hatch and scuffed mounting lugs, no labels or emissive bars.
            b.Box(V(0, 3.2f, 5.81f), V(2.2f, 2.8f, .28f), C(Steel));
            b.Box(V(0, 3.2f, 5.97f), V(1.65f, 2.25f, .08f), C(0x87938f));
            for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 3; i++)
                b.Box(V(side * .94f, 2.2f + i * .95f, 6), V(.18f, .16f, .15f), C(Rust));
            cols.Add(PropCollider.Circle(0, 0, 5.9f).Between(1.6f, 6));
            cols.Add(PropCollider.Rect(0, 5.81f, 1.1f, .2f).Between(1.6f, 4.6f));
            return Finish(b, "sealed white dome and antenna", 9.1f, 27.1f, cols, true);
        }
    }
}
