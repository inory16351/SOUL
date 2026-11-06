using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Body colliders (port of body.js): capsules attached to joints, used to keep the blade out
    // of the body, for cloth collisions and for audits. All sim space.
    public class Capsule
    {
        public string name; public TNode joint; public Vector3 la, lb, a, b; public float r;
    }
    public class BodyColliders
    {
        static readonly (string name, string joint, Vector3 a, Vector3 b, float r)[] CAPSULES =
        {
            ("pelvis", "pelvis", new Vector3(-.10f, -.03f, 0), new Vector3(.10f, -.03f, 0), .13f),
            ("belly", "spine", new Vector3(-.06f, .02f, 0), new Vector3(.06f, .02f, 0), .135f),
            ("chest", "chest", new Vector3(-.11f, -.01f, 0), new Vector3(.11f, -.01f, 0), .165f),
            ("neck", "neck", new Vector3(0, -.02f, 0), new Vector3(0, .09f, 0), .09f),
            ("head", "head", new Vector3(0, .07f, .01f), new Vector3(0, .23f, -.03f), .20f),
            ("Lthigh", "Lthigh", new Vector3(0, -.03f, 0), new Vector3(0, -.33f, 0), .11f),
            ("Rthigh", "Rthigh", new Vector3(0, -.03f, 0), new Vector3(0, -.33f, 0), .11f),
            ("Lshin", "Lshin", new Vector3(0, -.06f, .02f), new Vector3(0, -.36f, .03f), .10f),
            ("Rshin", "Rshin", new Vector3(0, -.06f, .02f), new Vector3(0, -.36f, .03f), .10f),
            ("LupperArm", "LupperArm", new Vector3(0, -.02f, 0), new Vector3(0, -.30f, 0), .09f),
            ("RupperArm", "RupperArm", new Vector3(0, -.02f, 0), new Vector3(0, -.30f, 0), .09f),
        };
        public static readonly string[] TORSO = { "pelvis", "belly", "chest", "neck", "head" };
        public readonly List<Capsule> caps = new List<Capsule>();
        public readonly Dictionary<string, Capsule> byName = new Dictionary<string, Capsule>();

        public BodyColliders(Dictionary<string, TNode> joints)
        {
            foreach (var c in CAPSULES)
            {
                var cap = new Capsule { name = c.name, joint = joints[c.joint], la = c.a, lb = c.b, r = c.r };
                caps.Add(cap); byName[c.name] = cap;
            }
        }
        public void Update() { foreach (var c in caps) { c.a = c.joint.LocalToWorld(c.la); c.b = c.joint.LocalToWorld(c.lb); } }

        public struct Hit { public string name; public float depth; public Vector3 point, normal; }
        // Deepest penetration of segment p-q (radius r) into the listed capsules (null = none).
        public Hit? Deepest(IList<string> names, Vector3 p, Vector3 q, float r)
        {
            Hit? best = null;
            foreach (var name in names)
            {
                var c = byName[name];
                float dist = Sim.ClosestSegments(p, q, c.a, c.b, out var cA, out var cB), depth = c.r + r - dist;
                if (depth > 0 && (best == null || depth > best.Value.depth))
                {
                    var normal = cA - cB;
                    if (normal.sqrMagnitude < 1e-10f) normal = Vector3.Cross(q - p, c.b - c.a);
                    if (normal.sqrMagnitude < 1e-10f) normal = Vector3.up;
                    best = new Hit { name = name, depth = depth, point = cA, normal = normal.normalized };
                }
            }
            return best;
        }
    }

    // Anatomical joint limits (port of limits.js): local Euler XYZ degrees, right side / centre.
    public static class Limits
    {
        static readonly Dictionary<string, Vector2[]> L = new Dictionary<string, Vector2[]>
        {
            { "spine", new[] { new Vector2(-20, 45), new Vector2(-30, 30), new Vector2(-20, 20) } },
            { "chest", new[] { new Vector2(-25, 35), new Vector2(-45, 45), new Vector2(-20, 20) } },
            { "neck", new[] { new Vector2(-35, 40), new Vector2(-45, 45), new Vector2(-25, 25) } },
            { "head", new[] { new Vector2(-35, 35), new Vector2(-45, 45), new Vector2(-20, 20) } },
            { "clavicle", new[] { new Vector2(-15, 15), new Vector2(-25, 25), new Vector2(-10, 35) } },
            { "upperArm", new[] { new Vector2(-185, 65), new Vector2(-90, 90), new Vector2(-95, 180) } },
            { "forearm", new[] { new Vector2(-150, 2), new Vector2(-85, 85), new Vector2(-3, 3) } },
            { "thigh", new[] { new Vector2(-125, 35), new Vector2(-40, 40), new Vector2(-25, 45) } },
            { "shin", new[] { new Vector2(0, 150), new Vector2(-4, 4), new Vector2(-3, 3) } },
            { "foot", new[] { new Vector2(-35, 50), new Vector2(-20, 20), new Vector2(-25, 25) } },
        };
        static Vector2[] For(string name)
        {
            char s = name[0]; bool side = s == 'L' || s == 'R';
            string key = side ? char.ToLower(name[1]) + name.Substring(2) : name;
            if (!L.TryGetValue(key, out var b)) return null;
            if (s != 'L') return b;
            return new[] { b[0], new Vector2(-b[1].y, -b[1].x), new Vector2(-b[2].y, -b[2].x) };
        }
        /// Clamp a local quaternion in place; returns the largest excess in degrees.
        public static float ClampJoint(string name, ref Quaternion q)
        {
            var lim = For(name); if (lim == null) return 0;
            var e = Sim.ToEulerXYZ(q) * Mathf.Rad2Deg; float excess = 0;
            for (int i = 0; i < 3; i++)
            {
                float v = e[i], c = Mathf.Min(lim[i].y, Mathf.Max(lim[i].x, v));
                excess = Mathf.Max(excess, Mathf.Abs(v - c)); e[i] = c;
            }
            if (excess > 1e-4f) q = Sim.EulerXYZDeg(e.x, e.y, e.z);
            return excess;
        }
    }
}
