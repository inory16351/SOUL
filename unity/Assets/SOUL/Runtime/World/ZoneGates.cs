using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Zone layout collision for gates and border ridges (meshes come from Codex-owned files):
    //  - border ridge chunk height-field mesh + wall colliders (colour / dressing: BorderDressing)
    //  - one gate per ZoneGraph link site, in the site's local frame (+Z = toward link.b,
    //    +X along the border; corridor |x| < ZoneWorld.HALF_W). Meshes: GateModels.
    //      Free    open corridor
    //      Drop    ledge block (LEDGE_H) with a ramp on the high side a -> fall off into b
    //      Rope    ledge block climbed with the rope via anchor rings (RopeAnchors)
    //      Jet     chasm             Cannon  cracked rubble wall     Magnet  rusted metal wall
    //      Toxic   sludge pool       LabKeys sealed lab door
    //    Wall-type gates are gated colliders (WorldCollision.Col.gate): they stop blocking once the
    //    part is owned, and their `solid` mesh is hidden then (the jet chasm stays visible).
    public static class ZoneGates
    {
        public static readonly int[] PartHex = { 0x8A8F96, 0xE0A030, 0x3F8FD8, 0xD9502F, 0x8E5BC8, 0x4CAF50, 0xC2562B };   // slide 11 arrow colours
        public class Gate { public PropModel fixedPart, solid; public Mesh fixedGlow, solidGlow; public int part; public bool hideWhenOwned; }
        static readonly Dictionary<int, Gate> cache = new Dictionary<int, Gate>();

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        /// rope anchor of a rope ledge, site-local: ring position (returned) and landing spot on the block top.
        /// sideA = climbed from link.a (local -Z); the other side exists only for two-way links.
        public static Vector3 AnchorLocal(bool sideA, out Vector3 landing)
        {
            float s = sideA ? -1 : 1, H = ZoneWorld.LEDGE_H;
            landing = V(0, H, s * 3.2f);
            return V(0, H + 1.5f, s * 4.9f);
        }
        public static Gate Get(ZoneWorld.Site s)
        {
            if (cache.TryGetValue(s.index, out var g)) return g;
            g = Build(s); cache[s.index] = g; return g;
        }

        static Gate Build(ZoneWorld.Site s)
        {
            var l = s.link; int part = l.kind == ZoneGraph.Kind.Part ? l.part : 0;
            var fb = new MeshBuilder(1000 + s.index); var fg = new MeshBuilder(2000 + s.index);
            var sb = new MeshBuilder(3000 + s.index); var sg = new MeshBuilder(4000 + s.index);
            var fixedPart = new PropModel { footprint = 14, height = 4 }; var solid = new PropModel { footprint = 14, height = 4 };
            Color glow = C(PartHex[Mathf.Clamp(part, 0, PartHex.Length - 1)]);
            float hw = ZoneWorld.GATE_HALF, H = ZoneWorld.LEDGE_H;
            bool hide = true;

            // meshes: GateModels (Codex-owned). Here only collision, walkable surfaces and anchor points.
            void LedgeBlock()
            {
                GateModels.Ledge(s, fb, hw, H);
                var top = PropSurface.Box(0, 0, hw, 6, H); top.noGrab = true; fixedPart.surfaces.Add(top);   // only the anchors climb it
                // the block is taller than WorldCollision.HEADROOM, so Ledge() would read it as a ceiling: wall it
                fixedPart.colliders.Add(PropCollider.Rect(0, 0, hw, 6).Between(0, H));
            }
            void Anchor(bool sideA) => GateModels.Anchor(s, fb, fg, AnchorLocal(sideA, out _), H, glow);

            switch (l.kind)
            {
                case ZoneGraph.Kind.Free: GateModels.FreeCorridor(s, fb); break;
                case ZoneGraph.Kind.Drop:
                    LedgeBlock();
                    GateModels.Ramp(s, fb, -18, -6, 6.5f, H);
                    fixedPart.surfaces.Add(PropSurface.Ramp(0, -12, 6.5f, 6, 0, H));
                    break;
                default:
                    switch (part)
                    {
                        case Parts.Rope: LedgeBlock(); Anchor(true); if (!l.oneWay) Anchor(false); break;
                        case Parts.Jet: hide = false; GateModels.JetGap(s, fb, fg, hw, glow); solid.colliders.Add(PropCollider.Rect(0, 0, hw, 5).Gated(part)); break;
                        case Parts.Cannon: GateModels.CannonWall(s, sb, sg, hw, glow); solid.colliders.Add(PropCollider.Rect(0, 0, hw, 1.8f).Gated(part)); break;
                        case Parts.Magnet: GateModels.MagnetWall(s, sb, sg, hw, glow); solid.colliders.Add(PropCollider.Rect(0, 0, hw, .9f).Gated(part)); break;
                        case Parts.Toxic: GateModels.Sludge(s, sb, sg, hw, glow); solid.colliders.Add(PropCollider.Rect(0, 0, hw, 6).Gated(part)); break;
                        case Parts.LabKeys:
                            GateModels.LabDoor(s, fb, fg, sb, hw, glow);
                            fixedPart.colliders.Add(PropCollider.Rect(-10, 0, 1.2f, 1.7f)); fixedPart.colliders.Add(PropCollider.Rect(10, 0, 1.2f, 1.7f));
                            solid.colliders.Add(PropCollider.Rect(0, 0, hw, 1.6f).Gated(part)); break;
                    }
                    break;
            }
            var g = new Gate { fixedPart = fixedPart, solid = solid, part = part, hideWhenOwned = hide };
            fixedPart.mesh = fb.TriangleCount > 0 ? fb.Build("gate" + s.index) : null;
            solid.mesh = sb.TriangleCount > 0 ? sb.Build("gate-solid" + s.index) : null;
            g.fixedGlow = fg.TriangleCount > 0 ? fg.Build("gate-glow" + s.index) : null;
            g.solidGlow = sg.TriangleCount > 0 ? sg.Build("gate-solid-glow" + s.index) : null;
            return g;
        }

        // ---------- border ridges ----------
        static float Hash01(int x, int z, int k) { unchecked { int h = x * 73856093 ^ z * 19349663 ^ k * 83492791; h ^= h >> 13; h *= 1274126177; return ((h >> 8) & 0xffff) / 65535f; } }

        /// ridge mesh for the chunk at (x0, z0); null if no ridge there. Wall colliders go to `cols`.
        public static Mesh Ridge(float x0, float z0, float size, List<WorldCollision.Col> cols)
        {
            const float STEP = 2;
            int n = Mathf.RoundToInt(size / STEP);
            var h = new float[n + 1, n + 1]; var p = new Vector3[n + 1, n + 1]; bool any = false;
            for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
            {
                float x = x0 + i * STEP, z = z0 + j * STEP;
                h[i, j] = ZoneWorld.RidgeHeight(x, z); if (h[i, j] > .05f) any = true;
                int gx = Mathf.RoundToInt(x / STEP), gz = Mathf.RoundToInt(z / STEP);
                float jx = h[i, j] > .05f ? (Hash01(gx, gz, 1) - .5f) * 1.2f : 0, jz = h[i, j] > .05f ? (Hash01(gx, gz, 2) - .5f) * 1.2f : 0;
                p[i, j] = new Vector3(x + jx, h[i, j] - .03f + Terrain.Height(x, z), z + jz);   // ridges stand on the terrain
            }
            // colliders on a ~3 m grid: solid cells next to a walkable one
            const int CN = 11; float cs = size / CN; var hc = new bool[CN + 2, CN + 2];
            for (int i = 0; i < CN + 2; i++) for (int j = 0; j < CN + 2; j++) hc[i, j] = ZoneWorld.Solid(x0 + (i - .5f) * cs, z0 + (j - .5f) * cs);
            for (int i = 1; i <= CN; i++) for (int j = 1; j <= CN; j++)
                if (hc[i, j] && (!hc[i - 1, j] || !hc[i + 1, j] || !hc[i, j - 1] || !hc[i, j + 1]))
                    cols.Add(new WorldCollision.Col { c = new Vector2(x0 + (i - .5f) * cs, z0 + (j - .5f) * cs), r = cs * .76f });
            if (!any) return null;
            var b = new MeshBuilder(1);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                float hm = Mathf.Max(Mathf.Max(h[i, j], h[i + 1, j]), Mathf.Max(h[i, j + 1], h[i + 1, j + 1]));
                if (hm < .05f) continue;
                Vector3 a = p[i, j], bb = p[i + 1, j], c = p[i + 1, j + 1], d = p[i, j + 1];
                for (int t = 0; t < 2; t++)
                {
                    Vector3 q0 = a, q1 = t == 0 ? c : d, q2 = t == 0 ? bb : c;
                    var nrm = Vector3.Cross(q1 - q0, q2 - q0).normalized; if (nrm.y < 0) { var tmp = q1; q1 = q2; q2 = tmp; nrm = -nrm; }
                    float y = (q0.y + q1.y + q2.y) / 3, steep = 1 - nrm.y;
                    var cx = (q0 + q1 + q2) / 3;
                    b.Tri(q0, q1, q2, BorderDressing.RidgeColor(cx, steep, Hash01(i + Mathf.RoundToInt(x0), j + Mathf.RoundToInt(z0), t + 3)));
                }
            }
            return b.TriangleCount > 0 ? b.Build("ridge") : null;
        }
    }
}
