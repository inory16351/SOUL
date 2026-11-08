using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 원혼 파츠 (기획_결정사항.md, user decision 2026-11-07): after a boss is purified, the grudge that is left
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
            new GrudgePart { zone = 0, name = "뒤틀린 다리", boss = "재의 감시자", ability = "고철 집어 던지기" },
            new GrudgePart { zone = 1, name = "원혼 파츠 Z1", boss = "Z1 보스", ability = "(미정)" },
            new GrudgePart { zone = 2, name = "원혼 파츠 Z2", boss = "Z2 보스", ability = "(미정)" },
            new GrudgePart { zone = 3, name = "원혼 파츠 Z3", boss = "Z3 보스", ability = "(미정)" },
            new GrudgePart { zone = 4, name = "원혼 파츠 Z4", boss = "Z4 보스", ability = "(미정)" },
            new GrudgePart { zone = 5, name = "원혼 파츠 Z5", boss = "Z5 보스", ability = "(미정)" },
            new GrudgePart { zone = 6, name = "원혼 파츠 Z6", boss = "Z6 보스", ability = "(미정)" },
            new GrudgePart { zone = 7, name = "원혼 파츠 Z7", boss = "Z7 보스", ability = "(미정)" },
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

        /// the dropped part (placeholder; final look → the boss model's own part): scrap limb segment wrapped in
        /// red-glowing seams. Returns the lit mesh; glow = the seams.
        public static Mesh Model(out Mesh glow)
        {
            var b = new MeshBuilder(919); var g = new MeshBuilder(920);
            b.Beam(new Vector3(-.55f, .25f, -.1f), new Vector3(.1f, .45f, .05f), .2f, .17f, 6, b.Jitter(0x6b4630, .08f));
            b.Beam(new Vector3(.1f, .45f, .05f), new Vector3(.6f, .2f, .25f), .16f, .12f, 6, b.Jitter(0x5a3a28, .08f));
            b.Box(new Vector3(.1f, .45f, .05f), new Vector3(.36f, .34f, .36f), b.Jitter(0x3a3634, .06f));
            for (int i = 0; i < 3; i++)
            {
                float a = -.5f + i * .5f;
                b.Beam(new Vector3(.6f, .2f, .25f), new Vector3(.95f, .06f, .25f + a * .5f), .07f, .02f, 4, b.Jitter(0xd8d0bc, .05f));   // bone toes
            }
            g.Beam(new Vector3(-.4f, .33f, -.05f), new Vector3(.05f, .5f, .05f), .07f, .07f, 5, Color.white);
            g.Beam(new Vector3(.2f, .45f, .1f), new Vector3(.5f, .27f, .22f), .055f, .05f, 5, Color.white);
            glow = g.Build("grudge-part-glow");
            return b.Build("grudge-part");
        }
    }
}
