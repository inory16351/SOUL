using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Things lying in the world that can be collected by walking over them or snatched with the
    // rope arm (떨어진 부위 낚아채기). Today: scrap (기본 부품 = 경험치) dropped by machines on death.
    // Stage 6 (soul loop) will drop the player's own parts through the same system.
    public class Pickups
    {
        public class Item
        {
            public string kind; public int amount; public Vector3 pos, vel; public float t; public bool reeling; public GameObject go;
        }
        public readonly List<Item> items = new List<Item>();
        public System.Action<Item> onCollect;
        readonly Transform parent; readonly Mesh mesh; readonly Material mat;
        public const float COLLECT_R = 1.3f;

        public Pickups(Transform parent)
        {
            this.parent = parent;
            mesh = ZoneProps.Scrap();
            mat = Mats.Unlit(new Color(1.6f, 1.35f, .8f, 1), Blend.Opaque, true);
        }

        public Item Spawn(string kind, Vector3 pos, int amount = 1, Vector3 vel = default)
        {
            var it = new Item { kind = kind, amount = amount, pos = pos, vel = vel };
            it.go = new GameObject("pickup " + kind); it.go.transform.SetParent(parent, false);
            it.go.AddComponent<MeshFilter>().sharedMesh = mesh; it.go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            items.Add(it); return it;
        }
        /// scatter `n` scrap pieces popping out of a destroyed machine
        public void Burst(Vector3 at, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2, s = 1.5f + Random.value * 2;
                Spawn("scrap", at + new Vector3(0, .6f, 0), 1, new Vector3(Mathf.Sin(a) * s, 4 + Random.value * 2, Mathf.Cos(a) * s));
            }
        }
        public Item Nearest(Vector3 p, float within)
        {
            Item best = null; float bd = within * within;
            foreach (var it in items) { if (it.reeling) continue; float d = Sim.FlatY(it.pos - p).sqrMagnitude; if (d < bd) { bd = d; best = it; } }
            return best;
        }

        public void Update(float dt, Vector3 player)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i]; it.t += dt;
                if (it.reeling)
                {   // flying back along the rope
                    var to = player + new Vector3(0, .9f, 0); var d = to - it.pos; float len = d.magnitude;
                    it.pos += d / Mathf.Max(len, 1e-3f) * Mathf.Min(len, 26 * dt);
                    if (len < .5f) { Collect(i); continue; }
                }
                else
                {
                    float floor = WorldCollision.Floor(it.pos.x, it.pos.z, it.pos.y + .2f, .6f) + .25f;
                    if (it.vel.sqrMagnitude > 1e-4f || it.pos.y > floor + .01f)
                    {
                        it.vel.y -= 22 * dt; it.pos += it.vel * dt;
                        if (it.pos.y <= floor) { it.pos.y = floor; it.vel = it.vel.y < -3 ? new Vector3(it.vel.x * .4f, -it.vel.y * .3f, it.vel.z * .4f) : Vector3.zero; }
                    }
                    if (it.t > .5f && Sim.FlatY(it.pos - player).magnitude < COLLECT_R && Mathf.Abs(it.pos.y - player.y) < 1.6f) { Collect(i); continue; }
                }
                float bob = it.reeling ? 0 : Mathf.Sin(it.t * 3 + i) * .06f;
                it.go.transform.localPosition = Sim.ToUnity(it.pos + new Vector3(0, bob, 0));
                it.go.transform.localRotation = Sim.YawToUnity(it.t * 1.7f);
            }
        }
        void Collect(int i) { var it = items[i]; items.RemoveAt(i); onCollect?.Invoke(it); Object.Destroy(it.go); }
        public void Clear() { foreach (var it in items) Object.Destroy(it.go); items.Clear(); }
    }
}
