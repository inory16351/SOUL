using UnityEngine;

namespace Soul
{
    // SKILL SHRINE MODEL (Codex-owned file, job 09). One shrine pillar per zone (ZoneAreas.Shrines): the place
    // where Morrow-07 resonates to take the zone core (reveals the zone map, opens pod warps) and, later, spends
    // skill points. Like Genshin's statues in role, but an original design: weathered stone fins around a floating
    // core the dead scientist built. Collider circle r 1.6 at the origin; keep it.
    public static class ShrineProps
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);
        const int Concrete = 0x8f8b86, Weathered = 0x7d7a75, Dark = 0x5a5752;
        const int Steel = 0x44494b, Rust = 0x78432c, Soot = 0x292c2b;

        static Vector3 Radial(float a, float radius, float y)
            => V(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);

        // Closed annular sectors: the aperture stays open from above and below.
        static void Ring(MeshBuilder b, float y, float inner, float outer, float height,
            int segments, int hex)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, next = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 lo = Radial(a, outer, y), ln = Radial(next, outer, y);
                Vector3 li = Radial(a, inner, y), lin = Radial(next, inner, y);
                Vector3 up = V(0, height, 0);
                b.Quad(li + up, lo + up, ln + up, lin + up, b.Jitter(hex, .06f));
                b.Quad(lo, li, lin, ln, C(Dark));
                b.Quad(lo, ln, ln + up, lo + up, b.Jitter(hex, .09f));
                b.Quad(lin, li, li + up, lin + up, C(Soot));
            }
        }

        // Chamfered, asymmetric cross-sections make broad concrete petals, not poles.
        // The lower two sections fit inside r 1.6; the crown flares above head height.
        static void Fin(MeshBuilder b, float top)
        {
            var rim = new[] { V(-.65f, 0, -1), V(.65f, 0, -1), V(1, 0, -.55f),
                V(1, 0, .55f), V(.58f, 0, 1), V(-.72f, 0, 1),
                V(-1, 0, .5f), V(-1, 0, -.62f) };
            // Reverse the outline's XZ order so its top faces point upwards.
            var levels = new[] { .65f, 2.25f, 3.5f, top - 1.15f, top };
            var centers = new[] { 1.04f, 1.08f, 1.35f, 1.82f, 1.94f };
            var widths = new[] { .42f, .39f, .48f, .56f, .46f };
            var depths = new[] { .39f, .36f, .34f, .32f, .25f };
            var rings = new Vector3[levels.Length][];
            for (int k = 0; k < levels.Length; k++)
            {
                rings[k] = new Vector3[8];
                for (int j = 0; j < 8; j++)
                {
                    Vector3 p = rim[7 - j];
                    float chip = k == 4 ? b.Range(.78f, 1) : b.Range(.94f, 1);
                    float y = levels[k] + (k == 4 ? b.Range(-.24f, .08f) : 0);
                    rings[k][j] = V(p.x * widths[k] * chip + k * .025f,
                        y, centers[k] + p.z * depths[k] * chip);
                }
            }
            for (int k = 0; k < levels.Length - 1; k++)
                for (int j = 0; j < 8; j++)
                {
                    int n = (j + 1) % 8;
                    int hex = j == 1 || j == 2 ? Dark : k == 0 ? Weathered : Concrete;
                    b.Quad(rings[k][j], rings[k][n], rings[k + 1][n], rings[k + 1][j],
                        b.Jitter(hex, .09f));
                }
            for (int j = 0; j < 8; j++)
            {
                int n = (j + 1) % 8;
                b.Tri(V(.1f, top - .1f, 1.94f), rings[4][j], rings[4][n], b.Jitter(Weathered));
                b.Tri(V(0, .65f, 1.04f), rings[0][n], rings[0][j], C(Dark));
            }

            // Exposed rods in the broken crown; each has a bent, oxidised end.
            for (int j = 0; j < 2; j++)
            {
                float x = -.13f + j * .3f;
                Vector3 a = V(x, top - .28f, 1.94f), bend = V(x, top + .12f, 1.96f);
                b.Beam(a, bend, .025f, .022f, 4, C(Rust));
                b.Beam(bend, bend + V(.09f, .12f, -.08f), .022f, .015f, 4, C(Steel));
            }

            // Soot and rust trails follow the sloping outer concrete face.
            b.Quad(V(-.14f, 3.5f, 1.695f), V(.02f, 3.5f, 1.695f),
                V(.15f, top - 1.15f, 2.145f), V(-.1f, top - 1.15f, 2.145f), C(0x66584e));
            b.Quad(V(.18f, 2.25f, 1.445f), V(.24f, 2.25f, 1.445f),
                V(.32f, 3.5f, 1.695f), V(.18f, 3.5f, 1.695f), C(0x494946));
        }

        static void Cable(MeshBuilder b, Vector3 start, Vector3 end, float sag)
        {
            Vector3 last = start;
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 6f;
                Vector3 p = Vector3.Lerp(start, end, t) - V(0, 4 * sag * t * (1 - t), 0);
                b.Beam(last, p, .045f, .045f, 5, C(Soot));
                last = p;
            }
        }

        // A faceted containment core with a dark equatorial cage, visibly unsupported.
        static void Core(MeshBuilder b, MeshBuilder g)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3, next = (i + 1) * Mathf.PI / 3;
                Vector3 low = Radial(a, .4f, 5.15f), ln = Radial(next, .4f, 5.15f);
                Vector3 high = Radial(a, .48f, 5.65f), hn = Radial(next, .48f, 5.65f);
                g.Tri(V(0, 4.78f, 0), ln, low, C(0x71abae));
                g.Quad(low, ln, hn, high, C(i % 2 == 0 ? 0xa0d5d4 : 0x83bfca));
                g.Tri(V(.08f, 6.15f, -.04f), high, hn, C(0xb5ded9));
                if (i % 2 == 0)
                    b.Beam(Radial(a, .43f, 5.2f), Radial(a, .51f, 5.66f), .055f, .04f, 4, C(Steel));
            }
            Ring(b, 5.32f, .43f, .51f, .1f, 12, Steel);
        }

        /// body mesh (lit) + glow mesh (unlit HDR: the core and rune seams) + collider
        public static PropModel Shrine(out Mesh glow)
        {
            var b = new MeshBuilder(404); var g = new MeshBuilder(405);
            // Solid drum makes the circular blocker visible. No low apron or rubble
            // projects into the walkable space outside the unchanged r 1.6 collider.
            b.Cylinder(V(0, 0, 0), 1.6f, 1.48f, .42f, 24, C(Dark));
            b.Cylinder(V(0, .42f, 0), 1.4f, 1.35f, 1.78f, 18, C(Weathered));
            Ring(b, .45f, 1.35f, 1.46f, .14f, 18, Steel);
            b.Cylinder(V(0, 2.2f, 0), 1.32f, 1.12f, .24f, 12, C(Dark));
            b.Cylinder(V(0, 2.44f, 0), .74f, .52f, .74f, 9, C(Steel));
            b.Cylinder(V(0, 3.18f, 0), .52f, .87f, .28f, 9, C(Dark));
            Ring(b, 3.46f, .42f, .88f, .14f, 12, Weathered);
            b.Cylinder(V(0, 3.47f, 0), .39f, .28f, .17f, 9, C(Soot));

            // Chunks embedded in the plinth edge and short recessed expansion joints.
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                b.Rock(Radial(a, 1.35f, .49f), b.Range(.12f, .2f), b.Jitter(Concrete), .15f, .65f, 0);
                b.Push(); b.RotateY(a);
                b.Box(V(0, .92f, 1.39f), V(.055f, .51f, .024f), C(Dark));
                b.Cylinder(V(0, .6f, 1.42f), .065f, .065f, .08f, 6, C(Rust));
                b.Pop();
            }

            for (int i = 0; i < 3; i++)
            {
                float a = .22f + i * Mathf.PI * 2 / 3;
                b.Push(); b.RotateY(a); g.Push(); g.RotateY(a);
                Fin(b, i == 0 ? 7.2f : i == 1 ? 6.78f : 7.02f);
                b.Box(V(0, 1.48f, 1.23f), V(.86f, .23f, .45f), b.Jitter(Steel));
                b.Box(V(.05f, 4.98f, 1.49f), V(1.5f, .26f, .72f), b.Jitter(Rust));
                // Mechanical cheeks tie the containment ring into the concrete fins.
                b.Beam(V(-.28f, 4.85f, 1.68f), V(-.28f, 5.2f, 1.23f), .095f, .095f, 4, C(Steel));
                b.Beam(V(.28f, 4.85f, 1.68f), V(.28f, 5.2f, 1.23f), .095f, .095f, 4, C(Steel));
                for (int j = 0; j < 2; j++)
                {
                    float x = j == 0 ? -.28f : .28f;
                    b.Cylinder(V(x, 5.12f, 1.58f), .06f, .06f, .075f, 6, C(0x898276));
                    float side = j == 0 ? -1 : 1;
                    Cable(b, V(side * .53f, 1.62f, 1.28f), V(side * .66f, 4.96f, 1.85f), .42f);
                }
                b.Box(V(-.66f, 4.86f, 1.85f), V(.15f, .21f, .15f), C(Steel));
                g.Box(V(-.66f, 4.86f, 1.933f), V(.06f, .08f, .016f), C(0x789f9c));
                // Small angular luminous joints are embedded in dark ring sockets;
                // they are material seams, with no lettering or full-height strips.
                b.Box(V(0, 5.21f, 1.37f), V(.38f, .07f, .3f), C(Soot));
                g.Box(V(-.06f, 5.252f, 1.37f), V(.035f, .012f, .2f), C(0x80b9ba));
                g.Box(V(.02f, 5.252f, 1.285f), V(.19f, .012f, .035f), C(0x80b9ba));
                b.Pop(); g.Pop();
            }
            Ring(b, 4.91f, 1.17f, 1.59f, .29f, 18, Steel);
            Ring(b, 4.87f, 1.2f, 1.53f, .06f, 18, Rust);
            Core(b, g);
            var m = new PropModel { mesh = b.Build("skill-shrine"), footprint = 3, height = 7.6f };
            m.colliders.Add(PropCollider.Circle(0, 0, 1.6f));
            glow = g.Build("skill-shrine-glow");
            return m;
        }
    }
}
