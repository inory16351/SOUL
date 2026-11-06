using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of props.js: breakable scrap objects (rusted barrels, crates, dead signal lamps).
    // A blade hit shatters them into tumbling fragments with sparks; they regrow after 14 s.
    // Sim space; the parent Transform must sit at the world origin.
    public class Breakables
    {
        public class Prop
        {
            public string kind; public GameObject go; public Vector3 pos; public float yaw, rotZ, scale = 1;
            public float radius, height, hp, maxHp, cool, wobble, grow = -1, respawnAt = -1;
            public bool dead; public Material debrisMat; public Mesh debrisMesh;
            public void Write()
            {
                var t = go.transform; t.localPosition = Sim.ToUnity(pos);
                t.localRotation = Sim.ToUnity(Sim.EulerXYZ(0, yaw, rotZ)); t.localScale = Vector3.one * scale;
            }
        }
        class Debris { public GameObject go; public Vector3 pos, v, w, rot, size; public float life; }

        public readonly List<Prop> list = new List<Prop>();
        readonly List<Debris> debris = new List<Debris>();
        readonly Transform parent; readonly Vfx vfx; float clock;
        static Mesh cube;

        public Breakables(Transform parent, Vfx vfx, int count = 12, float bounds = 17)
        {
            this.parent = parent; this.vfx = vfx;
            var kinds = new[] { "Barrel", "Barrel", "Crate", "Crate", "Lamp" };
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.zero;
                for (int k = 0; k < 20; k++)
                {
                    float a = Random.value * Mathf.PI * 2, r = 4 + Random.value * (bounds - 6);
                    p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    bool clear = true; foreach (var o in list) if (Vector3.Distance(o.pos, p) <= 2.2f) { clear = false; break; }
                    if (clear) break;
                }
                var kind = kinds[i % kinds.Length];
                var go = Object.Instantiate(Resources.Load<GameObject>("Prefabs/" + kind), parent); go.name = kind;
                var m = go.GetComponent<SoulModel>();
                var prop = new Prop { kind = kind, go = go, pos = p, yaw = Random.value * 6, radius = m.radius, height = m.height, hp = m.hp, maxHp = m.hp };
                var mr = go.GetComponentInChildren<MeshRenderer>(); var mf = mr ? mr.GetComponent<MeshFilter>() : null;
                prop.debrisMat = mr ? mr.sharedMaterial : null; prop.debrisMesh = DebrisMesh(mf ? mf.sharedMesh : null);
                prop.Write(); list.Add(prop);
            }
        }
        // a cube tinted with the average vertex colour of the prop's first part (the shared
        // vertex-coloured material would otherwise render the fragments white)
        static Mesh DebrisMesh(Mesh src)
        {
            if (!cube) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp); }
            var m = Object.Instantiate(cube); m.name = "debris";
            Color avg = new Color(.45f, .3f, .2f, 1);
            if (src && src.isReadable) { var cs = src.colors; if (cs.Length > 0) { Color s = Color.clear; foreach (var c in cs) s += c; avg = s / cs.Length; avg.a = 1; } }
            var cols = new Color[m.vertexCount]; for (int i = 0; i < cols.Length; i++) cols[i] = avg; m.colors = cols;
            return m;
        }
        void Shatter(Prop o, Vector3 from)
        {
            o.dead = true; o.go.SetActive(false); o.respawnAt = clock + 14;
            var c = Sim.FlatY(o.pos, o.height * .5f); vfx?.Burst(c, 18, 1); vfx?.Dust(o.pos, 1.2f);
            for (int i = 0; i < 9; i++)
            {
                var d = new Debris { go = new GameObject("debris") };
                d.go.transform.SetParent(parent, false);
                d.go.AddComponent<MeshFilter>().sharedMesh = o.debrisMesh;
                var mr = d.go.AddComponent<MeshRenderer>(); mr.sharedMaterial = o.debrisMat; mr.shadowCastingMode = ShadowCastingMode.On;
                d.size = new Vector3(.08f + Random.value * .18f, .05f + Random.value * .12f, .08f + Random.value * .18f);
                d.pos = c + new Vector3((Random.value - .5f) * .4f, (Random.value - .5f) * o.height * .6f, (Random.value - .5f) * .4f);
                var away = Sim.Norm(Sim.FlatY(c - from));
                d.v = away * (2 + Random.value * 3) + new Vector3((Random.value - .5f) * 3, 2.5f + Random.value * 3, (Random.value - .5f) * 3);
                d.w = new Vector3(Random.value * 9, Random.value * 9, Random.value * 9);
                debris.Add(d); WriteDebris(d, d.size);
            }
        }
        static void WriteDebris(Debris d, Vector3 size)
        {
            var t = d.go.transform; t.localPosition = Sim.ToUnity(d.pos);
            t.localRotation = Sim.ToUnity(Sim.EulerXYZ(d.rot.x, d.rot.y, d.rot.z)); t.localScale = size;
        }
        void Respawn(Prop o) { o.dead = false; o.hp = o.maxHp; o.scale = .01f; o.grow = 0; o.go.SetActive(true); o.Write(); }

        // blade segment p-q (sim world) with reach: returns true if anything was struck
        public bool Hit(Vector3 p, Vector3 q, float reach, Vector3 from)
        {
            bool any = false;
            foreach (var o in list)
            {
                if (o.dead || o.cool > 0) continue;
                Vector3 a = Sim.FlatY(o.pos, .1f), b = Sim.FlatY(o.pos, o.height);
                var x = Sim.ClosestOnSegment(a, b, Sim.ClosestOnSegment(p, q, Sim.FlatY(o.pos, o.height * .5f)));
                var closest = Sim.ClosestOnSegment(p, q, x);
                if (Vector3.Distance(closest, x) < o.radius + reach)
                {
                    any = true; o.cool = .25f; o.hp--;
                    if (o.hp <= 0) Shatter(o, from); else { vfx?.Burst(closest, 8, .6f); o.wobble = 1; }
                }
            }
            return any;
        }
        public void Update(float dt)
        {
            clock += dt;
            foreach (var o in list)
            {
                o.cool = Mathf.Max(0, o.cool - dt);
                if (o.dead) { if (clock >= o.respawnAt) Respawn(o); continue; }
                bool dirty = false;
                if (o.grow >= 0) { o.grow += dt * 2; o.scale = Mathf.Min(1, o.grow); if (o.grow >= 1) o.grow = -1; dirty = true; }
                if (o.wobble > 0) { o.wobble = Mathf.Max(0, o.wobble - dt * 3); o.rotZ = Mathf.Sin(o.wobble * 30) * .08f * o.wobble; dirty = true; }
                if (dirty) o.Write();
            }
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i]; d.life += dt; d.v.y -= 9.8f * dt; d.pos += d.v * dt;
                if (d.pos.y < .04f) { d.pos.y = .04f; d.v.y *= -.3f; d.v.x *= .6f; d.v.z *= .6f; d.w *= .6f; }
                d.rot += d.w * dt;
                var size = d.size;
                if (d.life > 3.5f)
                {
                    d.size *= 1 - dt * 3; size = d.size;
                    if (d.life > 4.5f) { Object.Destroy(d.go); debris.RemoveAt(i); continue; }
                }
                WriteDebris(d, size);
            }
        }
    }
}
