using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 재 쥐 (작은 유기체, 2026-11-09): prey that scurries around its nest, bolts from the player and from hunting hounds,
    // and gets caught by them (the machine drinks its soul: SoulGame.Ecology). Any hit from the player kills it too.
    // Positions in sim space. PLACEHOLDER look (Claude): a small dark body + tail; final model = Codex 16 (Critters.Mesh).
    public class Critters
    {
        public class C { public Vector3 pos, vel; public float yaw, t, panic, hop; public bool alive = true; public bool saved; public Vector3 burrow; public object home; public Vector2 nest; public float nestR; public GameObject go; }
        public readonly List<C> list = new List<C>();
        /// a rescued critter reached its burrow (it is removed right after)
        public System.Action<C> onBurrow;
        readonly Transform parent; readonly Mesh mesh; readonly Material mat;
        readonly System.Random rng = new System.Random(5309);

        public Critters(Transform parent)
        {
            this.parent = parent;
            mesh = CritterMeshes.Rat(); mat = Mats.Lit(Color.white, Color.black, 0, .25f, true);
        }
        float R() => (float)rng.NextDouble();

        public C Spawn(Vector3 p, object home, Vector2 nest, float nestR)
        {
            var c = new C { pos = p, yaw = R() * 6.28f, home = home, nest = nest, nestR = nestR, t = R() * 5 };
            c.go = new GameObject("critter"); c.go.transform.SetParent(parent, false);
            c.go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = c.go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            list.Add(c); Place(c); return c;
        }
        public void Kill(C c) { if (!c.alive) return; c.alive = false; Object.Destroy(c.go); list.Remove(c); }
        public void Remove(object home) { for (int i = list.Count - 1; i >= 0; i--) if (list[i].home == home) { Object.Destroy(list[i].go); list.RemoveAt(i); } }

        /// threats: player + hunting machines (positions). Critters flee anything within 7 m, else potter about the nest.
        public void Update(float dt, Vector3 player, List<Vector3> threats)
        {
            for (int ci = list.Count - 1; ci >= 0; ci--)
            {
                var c = list[ci];
                if (c.saved)
                {   // rescued: bolt straight home and dive in
                    var home = Sim.FlatY(c.burrow - c.pos); float L = home.magnitude;
                    if (L < .8f) { onBurrow?.Invoke(c); Kill(c); continue; }
                    c.vel = home / L * 5.5f; c.yaw = Mathf.Atan2(home.x, home.z);
                    c.pos += c.vel * dt; c.pos.y = WorldCollision.Floor(c.pos.x, c.pos.z, c.pos.y + .5f, .8f); c.hop += dt * 50; Place(c); continue;
                }
                c.t += dt;
                Vector3 away = Vector3.zero; float near = 99;
                void Threat(Vector3 q) { var d = Sim.FlatY(c.pos - q); float l = d.magnitude; if (l < 7 && l > .01f) { away += d / l * (7 - l); near = Mathf.Min(near, l); } }
                Threat(player); foreach (var q in threats) Threat(q);
                c.panic = Mathf.MoveTowards(c.panic, near < 7 ? 1 : 0, dt * (near < 7 ? 4 : .5f));
                Vector3 want;
                if (c.panic > .1f) want = Sim.SetLength(away, 5.5f);                                     // bolt
                else
                {   // potter: short dashes and pauses, drifting back toward the nest
                    float phase = Mathf.Repeat(c.t * .45f + c.yaw, 2);
                    var toNest = new Vector3(c.nest.x - c.pos.x, 0, c.nest.y - c.pos.z);
                    var wander = Sim.Forward(c.yaw + Mathf.Sin(c.t * .7f) * 1.5f);
                    want = phase < .6f ? (wander + toNest / Mathf.Max(c.nestR, 1) * .8f).normalized * 1.6f : Vector3.zero;
                }
                c.vel = Vector3.Lerp(c.vel, want, 1 - Mathf.Exp(-dt * 6));
                var to = c.pos + c.vel * dt;
                var mv = WorldCollision.Move(c.pos, to, .15f, WorldCollision.STEP);
                if (Terrain.Water(mv.x, mv.z, out _) > .1f) mv = c.pos;   // they won't swim
                c.pos.x = mv.x; c.pos.z = mv.z; c.pos.y = WorldCollision.Floor(c.pos.x, c.pos.z, c.pos.y + .3f, .5f);
                if (c.vel.sqrMagnitude > .04f) c.yaw = Mathf.Atan2(c.vel.x, c.vel.z);
                c.hop += dt * c.vel.magnitude * 9;
                Place(c);
            }
        }
        void Place(C c)
        {
            var tr = c.go.transform;
            tr.localPosition = Sim.ToUnity(c.pos + new Vector3(0, Mathf.Abs(Mathf.Sin(c.hop)) * .05f, 0));
            tr.localRotation = Sim.YawToUnity(c.yaw);
        }
    }
}
