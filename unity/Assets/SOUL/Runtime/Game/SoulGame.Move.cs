using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Right-click move (2026-11-09, user: "클릭하면 가는 위치 좀 더 직관적으로 — 덮여 있는 오브젝트 위에 표기되고 실제
    //움직이는 위치로 표시되게"):
    //  - PickFloor: march the mouse ray against the walkable floor (terrain + surfaces at the player's level). Roofs
    //    above the player and anything the top-down cutaway has dithered away are looked through, so the target is
    //    the ground you actually walk to, not the slab in front of it.
    //  - the click ring (Vfx.MoveRing) sits on that floor; where an object covers it, it is drawn over the object.
    public partial class SoulGame
    {

        /// floor point under the screen position (sim), or null when the ray finds none within 260 m
        Vector3? PickFloor(Vector2 screen)
        {
            var ray = cam.camera.ScreenPointToRay(screen);
            Vector3 o = Sim.FromUnity(ray.origin), d = Sim.FromUnity(ray.origin + ray.direction) - o;
            float foot = ch.root.position.y;
            var cutC = ch.root.position + Sim.ApplyAxisAngle(new Vector3(0, 0, 3), Vector3.up, cam.yaw);
            // the floor the ray can land on at q: surfaces up to 3 m above the feet (roofs above are looked through);
            // inside the cut circle a terrain shelf is drawn pressed down to CUT_ABOVE (SoulLit squash), so the ray
            // meets it there, where the player sees it
            float FloorAt(Vector3 q, out bool seeThrough)
            {
                seeThrough = false;
                float h = WorldCollision.Height(q.x, q.z, foot + 3);
                if (h > foot + CUT_ABOVE && Sim.FlatY(q - cutC).magnitude < CUT_RADIUS - 1) h = foot + CUT_ABOVE;
                return h;
            }
            float step = .3f, prev = 0;
            for (float t = 1; t < 260; t += step)
            {
                var q = o + d * t; float h = FloorAt(q, out bool see);
                if (!see && q.y <= h)
                {   // refine between the last point above the floor and this one
                    float a = prev, b = t;
                    for (int i = 0; i < 12; i++) { float m = (a + b) * .5f; var qm = o + d * m; if (qm.y <= FloorAt(qm, out bool s2) && !s2) b = m; else a = m; }
                    var hit = o + d * b; hit.y = WorldCollision.Height(hit.x, hit.z, foot + 3);
                    return hit;
                }
                prev = t; step = Mathf.Min(1.2f, .3f + t * .01f);
            }
            return null;
        }

        // the click shows the old expanding ring on the real floor; Vfx.MoveRing also draws it over whatever covers it
        // (user 2026-11-09: no pointer graphic, "옛날처럼, 오브젝트 위에만 겹쳐서")
        void MoveMarkerClick(Vector3 p) => vfx.MoveRing(p);
        void MoveMarkerUpdate(float dt) { }

        /// MCP tests: floor pick under a screen point (sim) and the marker state
        public string PickTest(Vector2 screen)
        {
            var f = PickFloor(screen); var plane = cam.Pick(screen, ch.root.position.y);
            return $"floor {(f.HasValue ? f.Value.ToString("F2") : "none")} | old plane pick {(plane.HasValue ? plane.Value.ToString("F2") : "none")} | player {ch.root.position:F2}";
        }
    }
}
