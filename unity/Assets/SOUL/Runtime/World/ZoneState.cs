using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Per-zone progress: purified (boss beaten) or not. The purified count also drives how many
    // faces Morrow-07 can make (PixelFace.ORDER). Debug: K toggles the zone you stand in.
    public static class ZoneState
    {
        static readonly bool[] purified = new bool[ZoneGraph.Zones.Length];
        public static event System.Action<int> Changed;
        public static bool Purified(int zone) => zone >= 0 && zone < purified.Length && purified[zone];
        public static int Count { get { int n = 0; foreach (var p in purified) if (p) n++; return n; } }
        public static void Set(int zone, bool on)
        {
            if (zone < 0 || zone >= purified.Length || purified[zone] == on) return;
            purified[zone] = on; Changed?.Invoke(zone);
        }
        public static void Reset() { for (int i = 0; i < purified.Length; i++) { purified[i] = false; cores[i] = false; } }
        // zone cores: resonating with a zone skill shrine (like the Genshin statues) reveals the whole zone map
        // and unlocks warping between its pods
        static readonly bool[] cores = new bool[ZoneGraph.Zones.Length];
        public static bool HasCore(int zone) => zone >= 0 && zone < cores.Length && cores[zone];
        public static void GiveCore(int zone) { if (zone >= 0 && zone < cores.Length) cores[zone] = true; }
    }

    // Areas inside a zone (구역: 포드 지대 · 탐험 · 보스 지대), charging pods and boss arenas.
    // Hand-placed per zone; only Z0 (vertical slice) is filled in so far.
    public class Area { public int zone; public string name; public Vector2 center; public float radius; public float plotFill = -1; }
    public class Pod { public int id, zone; public string name; public Vector3 pos; public float yaw; public bool found; }
    /// skill shrine pillar: one per zone; resonate (G) to take the zone core (and later spend skill points there)
    public class Shrine { public int zone; public string name; public Vector3 pos; }
    public class BossArena
    {
        public int zone; public string boss, name, title; public Vector3 center; public float radius;
        public float entranceYaw;            // direction from the centre to the main (free) entrance
        public ZoneWorld.Site ropeSite;      // rope-gated shortcut over the wall (one way in)
        public int reward;                   // part given on the kill (ZoneGraph zone reward)
        /// energy pillar spots (offsets from the centre, x / z) for phase 1 and phase 2 (BossPillars).
        /// Neighbours ≥ 9 m apart so a 4 m area skill never reaches two at once.
        public Vector2[] pillars1, pillars2;
    }

    /// a gap only the rope swing crosses (hook hanging over the middle)
    public class Chasm
    {
        public int zone; public Vector3 center; public float yaw, halfLength, halfWidth, hookHeight = 9;
        public Vector2 Normal => new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));            // across the gap
        public Vector3 HookPos => center + new Vector3(0, hookHeight, 0);
    }

    public static class ZoneAreas
    {
        public static readonly List<Chasm> Chasms = new List<Chasm>();
        static List<Shrine> shrines;
        /// Z0 shrine is hand placed by the plaza; the other zones use their interior centre for now (ZoneWorld.Center)
        public static List<Shrine> Shrines
        {
            get
            {
                if (shrines != null) return shrines;
                shrines = new List<Shrine> { new Shrine { zone = 0, name = "잿빛 공명 기둥", pos = new Vector3(-110, 0, -95) } };
                for (int z = 1; z < ZoneGraph.Zones.Length; z++) { var c = ZoneWorld.Center(z); shrines.Add(new Shrine { zone = z, name = ZoneGraph.Zones[z].name + " 공명 기둥", pos = new Vector3(c.x, 0, c.y) }); }
                return shrines;
            }
        }
        public static readonly List<Area> Areas = new List<Area>();
        public static readonly List<Pod> Pods = new List<Pod>();
        public static readonly List<BossArena> Arenas = new List<BossArena>();
        public const float WALL_T = 3;      // arena wall thickness

        static ZoneAreas()
        {
            // Z0 잿빛 폐허: pod plaza on the original arena, ruined streets to the west, boss grounds east
            var z0 = ZoneGraph.Zones[0];
            Areas.Add(new Area { zone = 0, name = z0.areas[0], center = new Vector2(0, 0), radius = 170, plotFill = .25f });
            Areas.Add(new Area { zone = 0, name = z0.areas[1], center = new Vector2(-340, 0), radius = 270, plotFill = 1f });
            Areas.Add(new Area { zone = 0, name = z0.areas[2], center = new Vector2(430, 0), radius = 150, plotFill = .45f });
            Pods.Add(new Pod { id = 0, zone = 0, name = "폐허 광장 포드", pos = new Vector3(0, 0, -14), yaw = 0 });
            Pods.Add(new Pod { id = 1, zone = 0, name = "감시자 문 앞 포드", pos = new Vector3(372, 0, 22), yaw = -Mathf.PI / 2 });
            var arena = new BossArena { zone = 0, boss = "ashWarden", name = "재의 감시자", title = "WARDEN OF ASH", center = new Vector3(480, 0, 0), radius = 42, entranceYaw = -Mathf.PI / 2, reward = z0.reward };
            // rope shortcut: a ledge through the north wall (-Z), climbed from outside, dropping into the arena
            var n = new Vector2(0, 1);
            arena.ropeSite = new ZoneWorld.Site
            {
                index = 100, pos = new Vector2(arena.center.x, arena.center.z - arena.radius), n = n, tan = new Vector2(n.y, -n.x), yaw = Mathf.Atan2(n.x, n.y),
                link = new ZoneGraph.Link { a = 0, b = 0, kind = ZoneGraph.Kind.Part, part = Parts.Rope, oneWay = true, note = "보스 지대 지름길" },
            };
            // pillar rings around the boss's bed: phase 1 three at r 6 (10.4 m apart), phase 2 four at r 6.5 (9.2 m)
            arena.pillars1 = Ring(6, 3, 0); arena.pillars2 = Ring(6.5f, 4, Mathf.PI / 4);
            Arenas.Add(arena);
            // rope swing demo in the ruined streets: a collapsed street-level gap with a crane hook over it
            Chasms.Add(new Chasm { zone = 0, center = new Vector3(-210, 0, 60), yaw = 0, halfLength = 16, halfWidth = 5 });
        }

        static Vector2[] Ring(float r, int n, float a0)
        {
            var o = new Vector2[n]; for (int i = 0; i < n; i++) { float a = a0 + i * Mathf.PI * 2 / n; o[i] = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r); }
            return o;
        }
        public static Area AreaAt(Vector3 p)
        {
            Area best = null; float bd = float.PositiveInfinity; int zone = ZoneWorld.At(p.x, p.z);
            foreach (var a in Areas) { if (a.zone != zone) continue; float d = (new Vector2(p.x, p.z) - a.center).magnitude / a.radius; if (d < 1 && d < bd) { bd = d; best = a; } }
            return best;
        }
        /// share of structure plots built at (x, z): the area's override, else the zone default
        public static float PlotFill(float x, float z, float zoneDefault)
        {
            var a = AreaAt(new Vector3(x, 0, z)); return a != null && a.plotFill >= 0 ? a.plotFill : zoneDefault;
        }
        /// keep scatter / plots off pods and arena walls
        public static bool Blocked(float x, float z, float margin)
        {
            foreach (var p in Pods) { float dx = x - p.pos.x, dz = z - p.pos.z; if (dx * dx + dz * dz < (7 + margin) * (7 + margin)) return true; }
            if (shrines != null) foreach (var s in shrines) { float dx = x - s.pos.x, dz = z - s.pos.z; if (dx * dx + dz * dz < (9 + margin) * (9 + margin)) return true; }
            foreach (var c in Chasms)
            {
                var d = new Vector2(x - c.center.x, z - c.center.z); var n = c.Normal; var t = new Vector2(n.y, -n.x);
                if (Mathf.Abs(Vector2.Dot(d, t)) < c.halfLength + 6 + margin && Mathf.Abs(Vector2.Dot(d, n)) < c.halfWidth + 8 + margin) return true;
            }
            foreach (var a in Arenas)
            {
                float d = new Vector2(x - a.center.x, z - a.center.z).magnitude;
                if (d < a.radius + WALL_T + 6 + margin) return true;
                var rs = a.ropeSite.pos; if ((new Vector2(x, z) - rs).magnitude < 28 + margin) return true;
            }
            return false;
        }
        public static BossArena ArenaAt(Vector3 p, float inset = 0)
        {
            foreach (var a in Arenas) if (new Vector2(p.x - a.center.x, p.z - a.center.z).magnitude < a.radius - inset) return a;
            return null;
        }
    }
}
