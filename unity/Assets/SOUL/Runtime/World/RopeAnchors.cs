using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Rope arm targets (로프 팔, stage 4). Fixed anchors are registered here from the layout
    // (rope gate ledges, the boss arena shortcut, swing hooks over chasms); ledge edges of climbable
    // structures are found on the fly by probing the walkable surfaces (gate ledges are noGrab).
    public class RopeAnchor
    {
        public enum Kind { Climb, Swing }
        public Kind kind; public Vector3 pos;          // ring / hook position (sim)
        public Vector3 land;                           // Climb: landing on top
        public Vector2 approach;                       // Climb: unit XZ direction from the anchor toward where you must stand
        public Vector2 across; public float span;      // Swing: chasm normal and landing distance from the hook
        public string label;
    }

    public static class RopeAnchors
    {
        public const float RANGE = 14;                 // horizontal reach
        static List<RopeAnchor> all;
        public static List<RopeAnchor> All { get { if (all == null) Build(); return all; } }

        static Vector3 W(ZoneWorld.Site s, Vector3 local) => new Vector3(s.pos.x + s.tan.x * local.x + s.n.x * local.z, local.y, s.pos.y + s.tan.y * local.x + s.n.y * local.z);
        static void Build()
        {
            all = new List<RopeAnchor>();
            var sites = new List<ZoneWorld.Site>(ZoneWorld.Sites); foreach (var a in ZoneAreas.Arenas) sites.Add(a.ropeSite);
            foreach (var s in sites)
            {
                if (s.link.kind != ZoneGraph.Kind.Part || s.link.part != Parts.Rope) continue;
                for (int side = 0; side < (s.link.oneWay ? 1 : 2); side++)
                {
                    var pos = W(s, ZoneGates.AnchorLocal(side == 0, out var land)); var landW = W(s, land);
                    var ap = side == 0 ? -s.n : s.n;
                    all.Add(new RopeAnchor { kind = RopeAnchor.Kind.Climb, pos = pos, land = landW, approach = ap, label = s.link.note });
                }
            }
            foreach (var c in ZoneAreas.Chasms)
                all.Add(new RopeAnchor { kind = RopeAnchor.Kind.Swing, pos = c.HookPos, across = c.Normal, span = c.halfWidth + 3.5f, label = "갭 스윙" });
        }

        /// can a character standing at p use this anchor right now?
        public static bool Usable(RopeAnchor a, Vector3 p)
        {
            var d = new Vector2(p.x - a.pos.x, p.z - a.pos.z);
            if (d.magnitude > RANGE) return false;
            if (a.kind == RopeAnchor.Kind.Climb) return Vector2.Dot(d, a.approach) > 1.2f && p.y < a.land.y - .8f;
            return Mathf.Abs(Vector2.Dot(d, a.across)) > 2.5f && d.magnitude < 11.5f;
        }
        /// swing landing for a character starting at p: the mirror point on the far side of the hook
        public static Vector3 SwingLanding(RopeAnchor a, Vector3 p)
        {
            float side = Mathf.Sign(Vector2.Dot(new Vector2(p.x - a.pos.x, p.z - a.pos.z), a.across));
            var l = new Vector2(a.pos.x, a.pos.z) - a.across * side * a.span;
            return new Vector3(l.x, WorldCollision.Height(l.x, l.y, p.y + 3), l.y);
        }

        /// First climbable ledge edge along the horizontal ray from the feet at `p` toward `dir`.
        /// Returns false when a wall stops the line first or nothing is in reach. edge = lip point (y = top),
        /// land = a spot just past the lip on top.
        public static bool FindEdge(Vector3 p, Vector2 dir, float maxDist, out Vector3 edge, out Vector3 land)
        {
            edge = land = Vector3.zero; if (dir.sqrMagnitude < 1e-6f) return false; dir.Normalize();
            float foot = p.y;
            for (float t = .6f; t <= maxDist; t += .35f)
            {
                float x = p.x + dir.x * t, z = p.z + dir.y * t;
                float h = WorldCollision.HeightGrab(x, z, foot + 8, out bool grab);
                if (h - foot >= 1.0f)
                {
                    if (!grab) return false;                                     // gate ledges: anchors only
                    if (h - foot > 7.5f) return false;
                    float lx = x + dir.x * .8f, lz = z + dir.y * .8f;
                    float hl = WorldCollision.HeightGrab(lx, lz, h + .3f, out _);
                    edge = new Vector3(x, h, z); land = Mathf.Abs(hl - h) < .35f ? new Vector3(lx, hl, lz) : new Vector3(x, h, z);
                    return true;
                }
                var q = new Vector3(x, foot, z); var q0 = q;
                if (WorldCollision.Resolve(ref q, .15f) && (q - q0).sqrMagnitude > .01f) return false;   // a wall in the way
            }
            return false;
        }
    }
}
