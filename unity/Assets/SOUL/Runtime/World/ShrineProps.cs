using UnityEngine;

namespace Soul
{
    // SKILL SHRINE MODEL (Codex-owned file, job 09). One shrine pillar per zone (ZoneAreas.Shrines): the place
    // where Morrow-07 resonates to take the zone core (reveals the zone map, opens pod warps) and, later, spends
    // skill points. Like Genshin's statues in role, but an original design: weathered stone fins around a floating
    // core the dead scientist built. Placeholder by Claude. Collider circle r 1.6 at the origin; keep it.
    public static class ShrineProps
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);
        /// body mesh (lit) + glow mesh (unlit HDR: the core and rune seams) + collider
        public static PropModel Shrine(out Mesh glow)
        {
            var b = new MeshBuilder(404); var g = new MeshBuilder(405);
            b.Cylinder(V(0, 0, 0), 2.6f, 2.4f, .35f, 7, C(0x4a4743));
            b.Cylinder(V(0, .35f, 0), 1.9f, 1.8f, .3f, 7, C(0x5a5650));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3; var o = V(Mathf.Sin(a), 0, Mathf.Cos(a));
                b.Push(); b.Translate(o * 1.2f + V(0, .65f, 0)); b.RotateY(a); b.RotateX(-.12f);
                b.Box(V(0, 2.6f, 0), V(.7f, 5.2f, .45f), b.Jitter(0x6a655d, .06f), .06f); b.Pop();
                g.Push(); g.Translate(o * 1.2f + V(0, .65f, 0)); g.RotateY(a); g.RotateX(-.12f);
                g.Box(V(0, 2.6f, -.24f), V(.08f, 3.8f, .02f), C(0x7fe3ff)); g.Pop();
            }
            g.Rock(V(0, 4.2f, 0), .55f, C(0x9ff0ff), .15f, 1.3f, 1);
            var m = new PropModel { mesh = b.Build("skill-shrine"), footprint = 3, height = 6 };
            m.colliders.Add(PropCollider.Circle(0, 0, 1.6f));
            glow = g.Build("skill-shrine-glow");
            return m;
        }
    }
}
