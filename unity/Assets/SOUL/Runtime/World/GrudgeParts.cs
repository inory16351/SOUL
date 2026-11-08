using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 원혼 파츠 (design term only — in-game text never says 원혼/원한; players infer it. 기획_결정사항.md, 2026-11-07): after a boss is purified, the grudge that is left
    // condenses into one of its body parts, which drops as a red-glowing part. Per part the player chooses:
    //   submit at a pod  → ~15 min of play time later the zone greens further (Cleansed) + basic parts (XP)
    //   equip            → the boss's ability, permanently (no removal, like a mind-flayer tadpole); the face
    //                      reddens and loses expressions (PixelFace.grudge) and the ending route follows the count
    public class GrudgePart
    {
        public int zone; public string name, boss, ability;
    }

    public static class GrudgeParts
    {
        /// one per zone boss; names of Z1+ are placeholders until those bosses are designed
        public static readonly GrudgePart[] Defs =
        {
            new GrudgePart { zone = 0, name = "뒤틀린 에너지 코어", boss = "재의 감시자", ability = "코어 과부하 (최대 체력 % 회복 · 이동 속도 ↑ · 받는 피해 ↑)" },
            new GrudgePart { zone = 1, name = "붉은 부품 Z1", boss = "Z1 보스", ability = "(미정)" },
            new GrudgePart { zone = 2, name = "붉은 부품 Z2", boss = "Z2 보스", ability = "(미정)" },
            new GrudgePart { zone = 3, name = "붉은 부품 Z3", boss = "Z3 보스", ability = "(미정)" },
            new GrudgePart { zone = 4, name = "붉은 부품 Z4", boss = "Z4 보스", ability = "(미정)" },
            new GrudgePart { zone = 5, name = "붉은 부품 Z5", boss = "Z5 보스", ability = "(미정)" },
            new GrudgePart { zone = 6, name = "붉은 부품 Z6", boss = "Z6 보스", ability = "(미정)" },
            new GrudgePart { zone = 7, name = "붉은 부품 Z7", boss = "Z7 보스", ability = "(미정)" },
        };
        public const float PURIFY_SECONDS = 15 * 60;   // play time at the pod
        public const int PURIFY_REWARD = 60;           // basic parts (XP) when the pod finishes

        public class Pending { public int zone; public Pod pod; public float left; }
        public static readonly List<int> carried = new List<int>();     // picked up, not decided yet
        public static readonly List<int> equipped = new List<int>();    // permanent
        public static readonly List<Pending> pending = new List<Pending>();
        static readonly bool[] cleansed = new bool[8];
        public static event System.Action<int> Cleansed;

        public static void Reset() { carried.Clear(); equipped.Clear(); pending.Clear(); for (int i = 0; i < cleansed.Length; i++) cleansed[i] = false; PixelFace.grudge = 0; }
        public static bool IsCleansed(int zone) => zone >= 0 && zone < cleansed.Length && cleansed[zone];
        public static int Count => equipped.Count;
        /// ending route 1..4 by equipped grudge parts (draft bounds 0-1 / 2-3 / 4-5 / 6+, same as the face stages)
        public static int Route => PixelFace.GrudgeStage + 1;

        public static void Equip(int zone)
        {
            if (!carried.Remove(zone) || equipped.Contains(zone)) return;
            equipped.Add(zone); PixelFace.grudge = equipped.Count;
        }
        public static void Submit(int zone, Pod pod)
        {
            if (!carried.Remove(zone)) return;
            pending.Add(new Pending { zone = zone, pod = pod, left = PURIFY_SECONDS });
        }
        /// play-time tick; returns the zone whose purification finished this tick (or -1)
        public static int Tick(float dt)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var p = pending[i]; p.left -= dt;
                if (p.left <= 0) { pending.RemoveAt(i); cleansed[p.zone] = true; Cleansed?.Invoke(p.zone); return p.zone; }
            }
            return -1;
        }

        /// the dropped part (placeholder; final look → the boss model's own core): 뒤틀린 에너지 코어 — a cracked,
        /// faceted furnace core in a bent iron cage, red light leaking from the cracks. Returns the lit mesh;
        /// glow = the inner core showing through.
        public static Mesh Model(out Mesh glow)
        {
            var b = new MeshBuilder(919); var g = new MeshBuilder(920);
            b.Rock(new Vector3(0, .45f, 0), .31f, b.Jitter(0x2e2a28, .06f), .5f, .95f, 1);   // scorched shell, very irregular: the glow core shows through its dents like cracks
            for (int i = 0; i < 4; i++)
            {
                float a0 = i * Mathf.PI / 2 + .2f;
                var lo = new Vector3(Mathf.Sin(a0) * .42f, .08f, Mathf.Cos(a0) * .42f);
                var mid = new Vector3(Mathf.Sin(a0 + .5f) * .5f, .48f, Mathf.Cos(a0 + .5f) * .5f);
                var hi = new Vector3(Mathf.Sin(a0 + .9f) * .3f, .86f, Mathf.Cos(a0 + .9f) * .3f);
                b.Beam(lo, mid, .04f, .04f, 4, b.Jitter(0x6b4630, .08f));   // twisted cage ribs
                b.Beam(mid, hi, .04f, .03f, 4, b.Jitter(0x5a3a28, .08f));
            }
            b.Cylinder(new Vector3(0, .26f, 0), .44f, .46f, .06f, 10, b.Jitter(0x4a4542, .05f));   // clamp ring
            b.Cylinder(new Vector3(0, .62f, 0), .4f, .38f, .05f, 10, b.Jitter(0x4a4542, .05f));
            b.Beam(new Vector3(.3f, .7f, .1f), new Vector3(.62f, .95f, .25f), .03f, .02f, 3, b.Jitter(0x2b2a29));   // torn cable
            g.Rock(new Vector3(0, .45f, 0), .3f, Color.white, .15f, 1, 1);
            glow = g.Build("grudge-core-glow");
            return b.Build("grudge-core");
        }
    }
}
