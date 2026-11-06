using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Soul
{
    // Move data (moves.js) loaded from Resources/Data/moves.json, exported from the web build by
    // web-proto/tools/export-unity.js. Edit motion in web-proto/src/moves.js, re-export, re-import
    // (menu SOUL/Import Web Export) - or edit the JSON directly. Angles: degrees, local Euler XYZ.
    public class MoveKey { public float t; public Dictionary<string, Quaternion> q = new Dictionary<string, Quaternion>(); public Vector3 body; public string ease; }
    public class Guide { public float t; public Vector3 u, f, d; }
    public class Step { public string side; public float a, b; }

    public class Move
    {
        public string name, label, key, chain, weaponSpace, rootEase;
        public float duration, blend = float.NaN, impact = float.NaN, lag = float.NaN, chainAt = float.NaN, dyn = float.NaN, whipHz = float.NaN, multi = float.NaN, land = float.NaN;
        public float[] active, buffer, air, arc, parryWindow;
        public float[][] yaw, root;
        public bool loopHold, roll, slam, parry, stab;
        public List<Step> steps;
        public List<(float t, string name)> fx = new List<(float, string)>();
        public MoveKey[] keys;
        public Guide[] weapon;
        public bool Has(float v) => !float.IsNaN(v);
    }

    public static class MoveDB
    {
        public static Dictionary<string, Move> moves;
        public static List<string> order;                   // moves in authoring order
        public static Dictionary<string, Vector3> neutral;  // degrees
        public static List<string> poseNames;               // Object.keys(neutral)
        public static Vector3 BASE_BODY;
        public static Dictionary<string, Vector2> STANCE_FEET;
        public static Guide[] idleGuide;
        public static string[] ARM_BLADE_BLOCKERS;
        static bool loaded;

        static float[] Arr(JToken t) => t?.Select(x => x.Value<float>()).ToArray();
        static Vector3 V(JToken t) { var a = Arr(t); return new Vector3(a[0], a[1], a[2]); }
        static float F(JObject o, string k) => o[k] != null && o[k].Type != JTokenType.Null ? o.Value<float>(k) : float.NaN;
        public static Quaternion Quat(Vector3 deg) => Sim.EulerXYZDeg(deg.x, deg.y, deg.z);

        public static void Load(bool force = false)
        {
            if (loaded && !force) return;
            var ta = Resources.Load<TextAsset>("Data/moves");
            var j = JObject.Parse(ta.text);
            neutral = new Dictionary<string, Vector3>(); poseNames = new List<string>();
            foreach (var kv in (JObject)j["neutral"]) { neutral[kv.Key] = V(kv.Value); poseNames.Add(kv.Key); }
            BASE_BODY = V(j["BASE_BODY"]);
            STANCE_FEET = new Dictionary<string, Vector2>();
            foreach (var kv in (JObject)j["STANCE_FEET"]) { var a = Arr(kv.Value); STANCE_FEET[kv.Key] = new Vector2(a[0], a[1]); }
            ARM_BLADE_BLOCKERS = j["ARM_BLADE_BLOCKERS"].Select(x => (string)x).ToArray();
            var ig = j["IDLE_GRIP"];
            idleGuide = new[] { new Guide { t = 0, u = V(ig[0]).normalized, f = V(ig[1]).normalized, d = V(ig[2]).normalized } };
            moves = new Dictionary<string, Move>(); order = new List<string>();
            foreach (var kv in (JObject)j["moves"])
            {
                var o = (JObject)kv.Value;
                var m = new Move
                {
                    name = kv.Key, label = (string)o["label"], key = (string)o["key"], chain = (string)o["chain"], weaponSpace = (string)o["weaponSpace"], rootEase = (string)o["rootEase"],
                    duration = F(o, "duration"), blend = F(o, "blend"), impact = F(o, "impact"), lag = F(o, "lag"), chainAt = F(o, "chainAt"), dyn = F(o, "dyn"), whipHz = F(o, "whipHz"), multi = F(o, "multi"), land = F(o, "land"),
                    active = Arr(o["active"]), buffer = Arr(o["buffer"]), air = Arr(o["air"]), arc = Arr(o["arc"]), parryWindow = Arr(o["parryWindow"]),
                    yaw = o["yaw"]?.Select(Arr).ToArray(), root = o["root"]?.Select(Arr).ToArray(),
                    loopHold = (bool?)o["loopHold"] ?? false, roll = (bool?)o["roll"] ?? false, slam = (bool?)o["slam"] ?? false, parry = (bool?)o["parry"] ?? false, stab = (bool?)o["stab"] ?? false,
                };
                if (o["steps"] != null) m.steps = o["steps"].Select(s => new Step { side = (string)s[0], a = (float)s[1], b = (float)s[2] }).ToList();
                if (o["fx"] != null) foreach (var f in o["fx"]) m.fx.Add(((float)f[0], (string)f[1]));
                // compile keys (anim.js compile): body offset on top of BASE_BODY, quaternions with sign continuity
                var keys = new List<MoveKey>();
                foreach (JObject k in o["keys"])
                {
                    var mk = new MoveKey { t = (float)k["t"], body = V(k["body"]) + BASE_BODY, ease = (string)k["ease"] };
                    foreach (var a in (JObject)k["angles"]) mk.q[a.Key] = Quat(V(a.Value));
                    keys.Add(mk);
                }
                foreach (var name in poseNames)
                    for (int i = 1; i < keys.Count; i++)
                        if (Sim.Dot(keys[i].q[name], keys[i - 1].q[name]) < 0) { var q = keys[i].q[name]; keys[i].q[name] = new Quaternion(-q.x, -q.y, -q.z, -q.w); }
                m.keys = keys.ToArray();
                if (o["weapon"] != null)
                    m.weapon = o["weapon"].Select(w => new Guide { t = (float)w[0], u = V(w[1]).normalized, f = V(w[2]).normalized, d = V(w[3]).normalized }).ToArray();
                moves[m.name] = m; order.Add(m.name);
            }
            loaded = true;
        }
    }
}
