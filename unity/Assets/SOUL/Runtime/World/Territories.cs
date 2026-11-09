using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 영역 생태계 (기획서/Z0_생태계_엘리트_파츠.md, 2026-11-09): monsters live in fixed territories instead of being
    // summoned around the player. One hand-made table per zone (Z0 only so far). Positions are wishes: the first time a
    // territory is needed it is nudged to the nearest open, flat, dry ground (Place).
    //   kind: nest (mites + prey critters), pack (hounds: one notices, all come), guard (wardens), turret (sentries,
    //   rooted), lair (an elite + its retinue: a pack). Elites are killed for good; the rest come back when you rest.
    public class Territory
    {
        public int zone; public string name, kind; public Vector2 want, c; public float r;
        public string[] members;      // one entry per monster (type key)
        public int critters;          // prey organisms living here (재 쥐)
        // runtime (SoulGame.Ecology)
        public bool placed, spawned; public bool[] dead;
        public readonly List<Enemy> live = new List<Enemy>();
        public bool Pack => kind == "pack" || kind == "lair" || kind == "nest";
        public bool IsElite(int i) => EnemyType.TYPES[members[i]].eliteName != null;
    }

    public static class Territories
    {
        static Territory T(string name, string kind, float x, float z, float r, int critters, params string[] members) =>
            new Territory { zone = 0, name = name, kind = kind, want = new Vector2(x, z), r = r, critters = critters, members = members };

        public static readonly List<Territory> All = new List<Territory>
        {
            // 포드 광장 (0, 0): light, tutorial strength
            T("광장 재 둔덕 북", "nest", 40, 120, 22, 4, "ashMite", "ashMite", "ashMite", "ashMite", "ashMite"),
            T("광장 재 둔덕 남", "nest", -70, -125, 22, 4, "ashMite", "ashMite", "ashMite", "ashMite"),
            T("광장 북 폐허", "pack", 160, 170, 35, 0, "crawler", "crawler", "crawler"),
            T("광장 남 폐허", "guard", 120, -180, 28, 0, "warden"),
            // 무너진 거리 (-340, 0)
            T("거리 동쪽 무리", "pack", -200, 70, 40, 3, "crawler", "crawler", "crawler"),
            T("거리 중앙 경비", "guard", -320, -60, 35, 0, "warden", "warden"),
            T("거리 길목 포대", "turret", -420, 40, 14, 0, "sentry"),
            T("거리 남서 무리", "pack", -480, -110, 40, 0, "crawler", "crawler", "crawler"),
            T("거리 남쪽 폐허", "guard", -220, -170, 35, 0, "warden", "ashMite", "ashMite", "ashMite"),
            T("거리 북쪽 포대", "turret", -290, 190, 14, 0, "sentry"),
            T("거리 쥐 둥지", "nest", -380, 110, 25, 6, "ashMite", "ashMite", "ashMite"),
            T("우두머리 소굴", "lair", -545, 150, 55, 2, "houndAlpha", "crawler", "crawler", "crawler"),
            // 보스 지대 가는 길 (430, 0)
            T("보스 길목 포대", "turret", 300, 70, 14, 0, "sentry"),
            T("보스 길목 경비", "guard", 320, -90, 30, 0, "warden"),
            T("보스 북쪽 무리", "pack", 230, 150, 40, 3, "crawler", "crawler", "crawler"),
            T("집행자 광장", "lair", 400, -175, 45, 0, "executor"),
            // 외곽
            T("동쪽 끝 둥지", "nest", 580, 190, 25, 3, "ashMite", "ashMite", "ashMite", "ashMite", "ashMite"),
            T("남쪽 가장자리 무리", "pack", 20, -230, 35, 0, "crawler", "crawler", "ashMite", "ashMite"),
        };

        static Territories() { AddFiller(); }
        /// filler territories (2026-11-09, user: "맵 넓이에 비해 할 게 없음, 몬스터 개체수 늘려"): a jittered 110 m grid over
        /// the open parts of Z0 adds a territory wherever the hand-made table leaves a gap, typed by district
        static void AddFiller()
        {
            const float G = 110; var rng = new System.Random(7741); int n = 0;
            for (float gx = -640; gx < 700; gx += G)
                for (float gz = -270; gz < 280; gz += G)
                {
                    float x = gx + (float)(rng.NextDouble() - .5) * 60, z = gz + (float)(rng.NextDouble() - .5) * 60;
                    if (rng.NextDouble() < .2 || ZoneWorld.At(x, z) != 0 || ZoneWorld.Solid(x, z)) continue;
                    var p = new Vector2(x, z); bool clear = true;
                    foreach (var t in All) if ((t.want - p).magnitude < t.r + 55) { clear = false; break; }
                    foreach (var pod in ZoneAreas.Pods) if ((new Vector2(pod.pos.x, pod.pos.z) - p).magnitude < 70) clear = false;
                    foreach (var sh in ZoneAreas.Shrines) if ((new Vector2(sh.pos.x, sh.pos.z) - p).magnitude < 50) clear = false;
                    foreach (var a in ZoneAreas.Arenas) if ((new Vector2(a.center.x, a.center.z) - p).magnitude < a.radius + 45) clear = false;
                    if (!clear) continue;
                    double r = rng.NextDouble(); n++;
                    Territory T2;
                    if (x < -150)        // ruined streets: packs, guards, turrets
                        T2 = r < .45 ? T("거리 무리 " + n, "pack", x, z, 35, 2, "crawler", "crawler", "crawler", "ashMite")
                           : r < .75 ? T("거리 경비 " + n, "guard", x, z, 30, 0, "warden", "crawler")
                           : T("거리 포대 " + n, "turret", x, z, 14, 0, "sentry");
                    else if (x < 150)    // around the plaza: nests and small packs
                        T2 = r < .5 ? T("둔덕 둥지 " + n, "nest", x, z, 25, 4, "ashMite", "ashMite", "ashMite", "ashMite")
                           : T("광장 무리 " + n, "pack", x, z, 32, 2, "crawler", "crawler", "ashMite", "ashMite");
                    else                 // the road to the boss: guards and packs
                        T2 = r < .45 ? T("동쪽 경비 " + n, "guard", x, z, 30, 0, "warden", "warden")
                           : r < .8 ? T("동쪽 무리 " + n, "pack", x, z, 35, 2, "crawler", "crawler", "crawler")
                           : T("동쪽 포대 " + n, "turret", x, z, 14, 0, "sentry");
                    All.Add(T2);
                }
        }
        /// open ground for monsters at (x, z): this zone, no rock / water / steep ground, off pods, arenas and structure pads
        public static bool Open(int zone, float x, float z)
        {
            if (ZoneWorld.At(x, z) != zone || ZoneWorld.Solid(x, z)) return false;
            if (Terrain.Water(x, z, out _) > .05f) return false;
            float h = Terrain.Height(x, z);
            if (Mathf.Abs(Terrain.Height(x + 1.5f, z) - h) > .7f || Mathf.Abs(Terrain.Height(x, z + 1.5f) - h) > .7f) return false;
            foreach (var p in ZoneAreas.Pods) if (new Vector2(p.pos.x - x, p.pos.z - z).magnitude < 35) return false;
            foreach (var a in ZoneAreas.Arenas) if (new Vector2(a.center.x - x, a.center.z - z).magnitude < a.radius + 20) return false;
            foreach (var pp in WorldStreamer.PlotPads(x - 25, z - 25, 50))
            {
                var d = new Vector2(x, z) - pp.c; float cs = Mathf.Cos(pp.yaw), sn = Mathf.Sin(pp.yaw);
                float lx = Mathf.Abs(d.x * cs - d.y * sn), lz = Mathf.Abs(d.x * sn + d.y * cs);
                if (lx < pp.half.x + 1 && lz < pp.half.y + 1) return false;
            }
            foreach (var m in Caches.Spots) if ((m.c - new Vector2(x, z)).magnitude < m.r + 3) return false;
            return true;
        }
        /// nudge a territory's centre to open ground (spiral, up to 90 m), preferring 10 m+ from trails
        public static void Place(Territory t)
        {
            if (t.placed) return;
            t.placed = true; t.dead = new bool[t.members.Length]; t.c = t.want;
            for (int pass = 0; pass < 2; pass++)
                for (float r = 0; r <= 90; r += 3)
                {
                    int n = Mathf.Max(1, (int)(r * .7f));
                    for (int k = 0; k < n; k++)
                    {
                        float a = k * Mathf.PI * 2 / n; float x = t.want.x + Mathf.Cos(a) * r, z = t.want.y + Mathf.Sin(a) * r;
                        if (pass == 0 && ZoneRoutes.Dist(x, z) < 10) continue;
                        if (Open(t.zone, x, z)) { t.c = new Vector2(x, z); return; }
                    }
                }
        }
        /// a random open point inside the territory (null when none found quickly)
        public static Vector3? PointIn(Territory t, float frac, System.Random rng)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = t.r * frac * Mathf.Sqrt((float)rng.NextDouble());
                float x = t.c.x + Mathf.Cos(a) * r, z = t.c.y + Mathf.Sin(a) * r;
                if (Open(t.zone, x, z)) return new Vector3(x, Terrain.Height(x, z), z);
            }
            return null;
        }
    }

    // Supply caches on small authored mesas (2.1 m cliffs all round: only the double jump from 도약 관절 gets up).
    // Terrain raises the mesa; SoulGame.Ecology draws and opens the crate. No Terrain calls here (Terrain reads this).
    public static class Caches
    {
        public struct Spot { public Vector2 c; public float r, rise; public int scrap; public string name; }
        public static readonly Spot[] Spots =
        {
            new Spot { c = new Vector2(126, 97), r = 5, rise = 2.1f, scrap = 60, name = "광장 높은 단 보급 상자" },
            new Spot { c = new Vector2(-338, 206), r = 5, rise = 2.1f, scrap = 60, name = "거리 북쪽 높은 단 보급 상자" },
        };
        public static readonly bool[] opened = new bool[Spots.Length];
    }
}
