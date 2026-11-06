using System.Collections.Generic;
using System.Text;

namespace Soul
{
    // Movement parts (핵심 구동부 파츠). A part id doubles as a gate id on walkable surfaces
    // (PropSurface.gate) and on zone links: 0 = free, anything else needs that part.
    public static class Parts
    {
        public const int None = 0, Rope = 1, Jet = 2, Cannon = 3, Magnet = 4, Toxic = 5, LabKeys = 6;
        public static readonly string[] Names = { "없음", "로프 팔", "제트엔진", "캐논 포", "자석 다리", "내오염 코어", "연구소 열쇠 2개" };
        /// owned parts as a bit mask (bit = 1 << id). Gate 0 is always open.
        public static int owned;
        public static bool Has(int id) => id == None || (owned & (1 << id)) != 0;
        public static void Give(int id) { if (id != None) owned |= 1 << id; }
        public static void Toggle(int id) { if (id != None) owned ^= 1 << id; }
        public static bool Has(int mask, int id) => id == None || (mask & (1 << id)) != 0;
        public static string Owned()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < Names.Length; i++) if (Has(i)) sb.Append(sb.Length > 0 ? " · " : "").Append(Names[i]);
            return sb.Length > 0 ? sb.ToString() : "없음";
        }
    }

    // Zone graph (기획서 v0.3 slide 11, 기획_결정사항.md "월드 / 맵 구조"). Still a draft the user
    // will review: edit the two tables below only. Board layout = 4 columns x 3 rows like the slide.
    public static class ZoneGraph
    {
        public enum Kind { Free, Drop, Part }   // Free = walk both ways, Drop = one-way fall (no part), Part = needs `part`
        public class Zone
        {
            public int id; public string name, title, branch; public int tier; public int reward;   // tier 1 outer, 2 deep, 3 lab
            public int col0, col1, row0, row1;                                                       // board cells (slide layout)
            public string[] areas;                                                                   // 구역 2~3개
        }
        /// t = position along the shared border of a and b (0..1, toward +x / +z), same as the slide arrows
        public class Link { public int a, b; public Kind kind; public int part; public bool oneWay; public string note; public float t = .5f; }

        public static readonly Zone[] Zones =
        {
            Z(0, "잿빛 폐허", "ASHEN RUINS", "허브", 1, Parts.Rope, 1, 2, 1, 1, "포드 광장", "무너진 거리", "보스 지대"),
            Z(1, "무너진 도심", "FALLEN CITY", "A", 1, Parts.Jet, 0, 1, 0, 0, "포드 지대", "고층 잔해", "보스 지대"),
            Z(2, "마천루 묘지", "SKYSCRAPER GRAVE", "A", 2, Parts.LabKeys, 0, 0, 1, 2, "추락 지대", "수직 루트", "보스 지대"),
            Z(3, "고철 협곡", "SCRAP GORGE", "B", 1, Parts.Cannon, 2, 2, 0, 0, "포드 지대", "잔해 협로", "보스 지대"),
            Z(4, "전쟁 기계 공장", "WAR FOUNDRY", "B", 2, Parts.Toxic, 3, 3, 0, 0, "하역장", "조립 라인", "보스 지대"),
            Z(5, "죽은 숲", "DEAD WOODS", "C", 1, Parts.Magnet, 3, 3, 1, 1, "포드 지대", "뿌리 미로", "보스 지대"),
            Z(6, "독성 늪", "TOXIC MIRE", "C", 2, Parts.LabKeys, 3, 3, 2, 2, "슬러지 둑", "가라앉은 마을", "보스 지대"),
            Z(7, "과학자의 연구소", "THE LAB", "최심부", 3, Parts.None, 1, 2, 2, 2, "입구 홀", "연구실"),
        };

        public static readonly Link[] Links =
        {
            // free, both ways: neighbours you can walk between from the start
            L(0, 1, Kind.Free).At(.5f), L(0, 3, Kind.Free).At(.4f), L(0, 5, Kind.Free).At(.3f), L(1, 3, Kind.Free).At(.3f),
            // part shortcuts between neighbours that are already connected
            L(0, 5, Kind.Part, Parts.Rope, false, "지름길").At(.75f), L(1, 3, Kind.Part, Parts.Cannon, false, "지름길").At(.75f),
            // deep zones: two entrances needing different parts, a free drop back out
            L(1, 2, Kind.Part, Parts.Magnet, true, "묘지 입구 A").At(.5f), L(0, 2, Kind.Part, Parts.Rope, true, "묘지 입구 B · 높은 턱 오르기").At(.72f), L(2, 0, Kind.Drop, 0, true, "낙하").At(.3f),
            L(3, 4, Kind.Part, Parts.Jet, true, "공장 입구 A · 갭").At(.35f), L(5, 4, Kind.Part, Parts.Magnet, false, "공장 입구 B · 외벽 타기").At(.5f), L(4, 3, Kind.Drop, 0, true, "낙하").At(.7f),
            L(5, 6, Kind.Part, Parts.Toxic, false, "늪 입구 A · 슬러지").At(.3f), L(5, 6, Kind.Part, Parts.Cannon, false, "늪 입구 B · 잔해 벽").At(.7f),
            // the lab door under the hub: both keys (bosses of Z2 and Z6)
            L(0, 7, Kind.Part, Parts.LabKeys, true, "연구소 문").At(.5f),
        };

        static Zone Z(int id, string name, string title, string branch, int tier, int reward, int c0, int c1, int r0, int r1, params string[] areas) =>
            new Zone { id = id, name = name, title = title, branch = branch, tier = tier, reward = reward, col0 = c0, col1 = c1, row0 = r0, row1 = r1, areas = areas };
        static Link L(int a, int b, Kind k, int part = 0, bool oneWay = false, string note = null) =>
            new Link { a = a, b = b, kind = k, part = part, oneWay = oneWay || k == Kind.Drop, note = note };

        static Link At(this Link l, float t) { l.t = t; return l; }

        /// can a character with parts `mask` go from link.a to link.b (forward) / b to a (backward)?
        public static bool Passable(Link l, int mask, bool forward)
        {
            if (!forward && l.oneWay) return false;
            return l.kind != Kind.Part || Parts.Has(mask, l.part);
        }

        /// zones reachable from Z0 with the given parts (bit i = zone i)
        public static int Reachable(int mask, int start = 0)
        {
            int seen = 1 << start; var stack = new Stack<int>(); stack.Push(start);
            while (stack.Count > 0)
            {
                int z = stack.Pop();
                foreach (var l in Links)
                {
                    int to = l.a == z && Passable(l, mask, true) ? l.b : l.b == z && Passable(l, mask, false) ? l.a : -1;
                    if (to >= 0 && (seen & (1 << to)) == 0) { seen |= 1 << to; stack.Push(to); }
                }
            }
            return seen;
        }

        /// Plays the whole game greedily: keep beating every reachable boss, collect its part,
        /// until nothing new opens. LabKeys counts as owned once both Z2 and Z6 bosses are beaten.
        public static int Simulate(int startMask, out int bossesBeaten)
        {
            int mask = startMask; bossesBeaten = 0;
            for (bool changed = true; changed;)
            {
                changed = false;
                int reach = Reachable(mask);
                foreach (var z in Zones)
                {
                    if ((reach & (1 << z.id)) == 0 || (bossesBeaten & (1 << z.id)) != 0) continue;
                    bossesBeaten |= 1 << z.id; changed = true;
                    if (z.reward != Parts.LabKeys) mask |= 1 << z.reward;
                }
                if ((bossesBeaten & (1 << 2)) != 0 && (bossesBeaten & (1 << 6)) != 0) mask |= 1 << Parts.LabKeys;
            }
            return mask;
        }

        /// design checks from 기획_결정사항.md; returns "" when all pass, else one line per failure
        public static string Validate()
        {
            var sb = new StringBuilder();
            void Check(bool ok, string what) { if (!ok) sb.AppendLine("FAIL " + what); }
            int free = Reachable(0);
            Check(free == ((1 << 0) | (1 << 1) | (1 << 3) | (1 << 5)), "파츠 없이 Z0·Z1·Z3·Z5만 갈 수 있음 (got " + Bits(free) + ")");
            foreach (var z in Zones)
            {
                if (z.tier != 2) continue;
                var parts = new HashSet<int>();
                foreach (var l in Links) if (l.kind == Kind.Part && (l.b == z.id || (!l.oneWay && l.a == z.id))) parts.Add(l.part);
                Check(parts.Count >= 2, $"Z{z.id} 입구가 서로 다른 파츠 2개 이상 (got {parts.Count})");
                foreach (int p in parts) Check((Reachable(1 << p) & (1 << z.id)) != 0 || p == Parts.LabKeys, $"Z{z.id}: {Parts.Names[p]} 하나만으로 열림");
            }
            for (int p = Parts.Rope; p <= Parts.Toxic; p++)
            {
                bool opens = false;
                foreach (var z in Zones) if (z.tier == 2 && (free & (1 << z.id)) == 0 && (Reachable(1 << p) & (1 << z.id)) != 0) opens = true;
                Check(opens, $"{Parts.Names[p]}가 깊은 곳 하나 이상을 엶");
            }
            Check((Reachable((1 << Parts.LabKeys) - 2) & (1 << 7)) == 0, "연구소는 열쇠 없이 안 열림");
            Simulate(0, out int beaten);
            Check(beaten == 0xff, "모든 보스를 잡을 수 있음 (got " + Bits(beaten) + ")");
            return sb.ToString();
        }
        static string Bits(int m) { var sb = new StringBuilder(); for (int i = 0; i < Zones.Length; i++) if ((m & (1 << i)) != 0) sb.Append('Z').Append(i).Append(' '); return sb.ToString().Trim(); }
    }
}
