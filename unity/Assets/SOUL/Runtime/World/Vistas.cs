using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 전망 지점 (vista points, user decision 2026-11-07): the top-down camera only shows 15-30 m, so far landmarks
    // never enter the frame. Instead of changing the view, high spots and the skill shrines become lookouts:
    // standing there pulls the camera back and low (SoulGame.Vista), the fog lifts and the landmarks around show
    // as hazy silhouettes; the ones seen are revealed on the map.
    //   per zone: the shrine + up to 2 high points near a trail (reachable), >= 220 m from any other vista
    // Found lazily from the terrain relief, so they follow the terrain tuning.
    public class Vista
    {
        public Vector3 pos; public int zone; public bool shrine, visited;
        public float lookYaw;          // towards the richest group of landmarks (set when built)
    }

    public static class Vistas
    {
        public static readonly List<Vista> All = new List<Vista>();
        public const float TRIGGER = 3.5f, RANGE = 750;
        static bool built;

        public static void Reset() { built = false; All.Clear(); }
        public static void Build()
        {
            if (built) return; built = true; All.Clear();
            foreach (var s in ZoneAreas.Shrines) All.Add(new Vista { pos = s.pos, zone = s.zone, shrine = true });
            const float STEP = 32;
            for (int zone = 0; zone < ZoneGraph.Zones.Length; zone++)
            {
                var c = ZoneWorld.Center(zone); var cands = new List<(Vector3 p, float h)>();
                for (float x = c.x - 900; x <= c.x + 900; x += STEP)
                    for (float z = c.y - 900; z <= c.y + 900; z += STEP)
                    {
                        if (ZoneWorld.At(x, z) != zone || ZoneWorld.Solid(x, z)) continue;
                        float tr = ZoneRoutes.Dist(x, z); if (tr > 45 || tr < ZoneRoutes.HALF + 4) continue;   // just off a trail
                        if (ZoneAreas.Blocked(x, z, 20) || Terrain.Water(x, z, out _) > 0) continue;
                        bool arena = false; foreach (var ar in ZoneAreas.Arenas) if (Sim.FlatY(ar.center - new Vector3(x, 0, z)).magnitude < ar.radius + 40) arena = true;
                        if (arena) continue;   // never inside / against a boss arena
                        cands.Add((new Vector3(x, 0, z), Terrain.Relief(x, z)));
                    }
                cands.Sort((a, b) => b.h.CompareTo(a.h));
                int n = 0;
                foreach (var (p, h) in cands)
                {
                    if (n >= 2) break;
                    bool far = true; foreach (var v in All) if (Sim.FlatY(v.pos - p).magnitude < 220) { far = false; break; }
                    if (!far) continue;
                    All.Add(new Vista { pos = p, zone = zone }); n++;
                }
            }
            foreach (var v in All) v.lookYaw = BestYaw(v.pos);
        }
        /// the facing that sees the most landmarks within RANGE (weighted toward nearer ones)
        static float BestYaw(Vector3 p)
        {
            float best = 0, bestScore = -1;
            for (int i = 0; i < 16; i++)
            {
                float yaw = i * Mathf.PI * 2 / 16, score = 0; var f = Sim.Forward(yaw);
                foreach (var lm in Regions.Landmarks)
                {
                    var d = Sim.FlatY(lm.pos - p); float dist = d.magnitude; if (dist < 60 || dist > RANGE) continue;
                    if (Vector3.Dot(d / dist, f) > Mathf.Cos(.5f)) score += 1 - dist / RANGE * .6f;
                }
                if (score > bestScore) { bestScore = score; best = yaw; }
            }
            return best;
        }
        public static Vista Near(Vector3 p, float within)
        {
            foreach (var v in All) if (Sim.FlatY(v.pos - p).magnitude < within) return v;
            return null;
        }
        /// lookout marker (placeholder; final model → Codex): a rusted survey tripod on a concrete footing with a
        /// torn cloth streamer — reads as "a place to look from" without text
        public static PropModel Marker()
        {
            var b = new MeshBuilder(611); var p = new PropModel { footprint = 1.2f, height = 3.2f };
            b.Box(new Vector3(0, .12f, 0), new Vector3(1.6f, .24f, 1.3f), b.Jitter(0x6f6a64, .05f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3 + .3f;
                b.Beam(new Vector3(Mathf.Sin(a) * .62f, .24f, Mathf.Cos(a) * .5f), new Vector3(0, 2.6f, 0), .05f, .04f, 4, b.Jitter(0x7a4a2e, .06f));
            }
            b.Box(new Vector3(0, 2.7f, .05f), new Vector3(.34f, .2f, .42f), b.Jitter(0x3c3a38, .05f));   // instrument head
            b.Beam(new Vector3(0, 2.75f, .25f), new Vector3(0, 2.68f, .55f), .05f, .05f, 5, b.Jitter(0x2b2a29));   // lens barrel
            b.Beam(new Vector3(0, 2.6f, 0), new Vector3(0, 3.2f, 0), .025f, .02f, 3, b.Jitter(0x5a5550));
            b.Quad(new Vector3(0, 3.15f, 0), new Vector3(.75f, 3.05f, .12f), new Vector3(.7f, 2.82f, .1f), new Vector3(0, 2.9f, 0), b.Jitter(0x9a8f7a, .08f));   // cloth streamer
            b.Quad(new Vector3(0, 2.9f, 0), new Vector3(.7f, 2.82f, .1f), new Vector3(.75f, 3.05f, .12f), new Vector3(0, 3.15f, 0), b.Jitter(0x9a8f7a, .08f));
            p.mesh = b.Build("vista-marker");
            return p;
        }
    }
}
