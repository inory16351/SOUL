using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 성장 노드 트리 (기획_결정사항.md '성장 노드 트리', user decision 2026-11-08, 오리와 눈먼 숲 방식).
    // Basic parts do two jobs: picking one up adds to xp (cumulative, never goes down) AND to the wallet
    // (SoulGame.scrap, spent on skill-part upgrades, dropped on death). xp crosses thresholds → points → nodes.
    // Edit the Nodes table only: id, branch, name, effect, cost (points), position, required core part, prerequisites.
    // Position = the socket centre in Resources/UI/growth_tree_bg.png pixels (1672 x 941, origin top-left; socket list
    // in ui_atlas.md). A branch's first node has no prerequisite: it hangs off the body (Roots).
    public enum GStat
    {
        MaxHp,          // + flat max HP
        Regen,          // fraction of max HP per second, 1.5 .. 5.5 s after taking a hit
        Taken,          // − damage taken (fraction)
        OverloadTaken,  // − of 코어 과부하's extra damage taken (fraction of the +50 %)
        LightDmg,       // + basic attack (A combo) damage
        FinisherDmg,    // + 3rd hit (light3) damage
        Reach,          // + attack hit area radius (A combo)
        SkillDmg,       // + skill damage (회오리 / 칼날 폭풍 / 파동 / 반격)
        AllDmg,         // + every hit
        CounterWindow,  // + seconds of the counter (parry) window
        CounterDmg,     // + counter damage
        CounterCd,      // − counter cooldown (fraction)
        RopeRange,      // + rope reach (fraction)
        Speed,          // + move speed (fraction)
        DashCd,         // − dash cooldown (fraction)
        FogRadius,      // + map fog reveal radius (fraction)
        PodSense,       // reveals charge pods within N m
        ShrineSense,    // on-screen arrow to the nearest unresonated shrine within N m
        EnemyHp,        // HP numbers over enemies
    }

    public class GrowthNode
    {
        public string id, name, desc; public int branch, cost, part; public string[] pre; public GStat stat; public float value; public Vector2 pos;
        public GrowthNode(string id, int branch, string name, string desc, GStat stat, float value, int cost, Vector2 pos, int part = Parts.None, params string[] pre)
        { this.id = id; this.branch = branch; this.name = name; this.desc = desc; this.stat = stat; this.value = value; this.cost = cost; this.pos = pos; this.part = part; this.pre = pre; }
    }

    public static class Growth
    {
        public const int CORE = 0, RIGHT = 1, LEFT = 2, LEGS = 3, HEAD = 4;
        public static readonly string[] BranchNames = { "코어", "오른팔", "왼팔", "다리", "머리" };
        public static readonly Color[] BranchColors = { new Color(1f, .45f, .35f), new Color(1f, .8f, .35f), new Color(.45f, .85f, 1f), new Color(.5f, 1f, .55f), new Color(.8f, .6f, 1f) };
        public const float BASE_HP = 100;

        static Vector2 P(float x, float y) => new Vector2(x, y);
        /// background art size and the body points the branches grow from (core gem, blade wrist, cable wrist, legs, head)
        public static readonly Vector2 BG_SIZE = new Vector2(1672, 941);
        public static readonly Vector2[] Roots = { P(850, 430), P(637, 535), P(1023, 519), P(767, 673), P(835, 228) };
        // draft numbers (기획_결정사항.md 노드 표). The robot faces the viewer: blade arm (오른팔) on the screen's left,
        // cable arm (왼팔) right-lower, core up-right of the chest, legs along the bottom, head at the top.
        public static readonly GrowthNode[] Nodes =
        {
            // ---- 코어: max HP, regen, damage taken (first node: 강화 프레임 I) ----
            new GrowthNode("c_frame1",  CORE, "강화 프레임 I",   "최대 체력 +10",                          GStat.MaxHp, 10, 1, P(1228, 377)),
            new GrowthNode("c_frame2",  CORE, "강화 프레임 II",  "최대 체력 +15",                          GStat.MaxHp, 15, 2, P(1357, 310), Parts.None, "c_frame1"),
            new GrowthNode("c_frame3",  CORE, "강화 프레임 III", "최대 체력 +20",                          GStat.MaxHp, 20, 3, P(1519, 195), Parts.None, "c_frame2"),
            new GrowthNode("c_regen1",  CORE, "재생 회로",       "피격 1.5초 뒤부터 4초간 초당 최대 체력 1.5% 회복", GStat.Regen, .015f, 2, P(1325, 454), Parts.None, "c_frame1"),
            new GrowthNode("c_regen2",  CORE, "재생 회로 II",    "피격 뒤 재생 초당 +1.5% (총 3%)",        GStat.Regen, .015f, 3, P(1449, 355), Parts.None, "c_regen1"),
            new GrowthNode("c_plating", CORE, "충격 흡수판",     "받는 피해 −8%",                          GStat.Taken, .08f, 2, P(1352, 212), Parts.None, "c_frame1"),
            new GrowthNode("c_stable",  CORE, "출력 안정화",     "코어 과부하의 받는 피해 증가 절반 (+50% → +25%)", GStat.OverloadTaken, .5f, 2, P(1245, 128), Parts.None, "c_frame1"),
            new GrowthNode("c_filter",  CORE, "내오염 순환기",   "최대 체력 +25 (내오염 코어 필요)",       GStat.MaxHp, 25, 3, P(1415, 89), Parts.Toxic, "c_stable"),
            // ---- 오른팔(칼날): basic attack damage, finisher, reach ----
            new GrowthNode("r_edge1",   RIGHT, "날 연마 I",      "평타 피해 +8%",                          GStat.LightDmg, .08f, 1, P(416, 535)),
            new GrowthNode("r_edge2",   RIGHT, "날 연마 II",     "평타 피해 +10%",                         GStat.LightDmg, .10f, 2, P(348, 455), Parts.None, "r_edge1"),
            new GrowthNode("r_finish",  RIGHT, "마무리 일격",    "3타 피해 +25%",                          GStat.FinisherDmg, .25f, 2, P(320, 310), Parts.None, "r_edge2"),
            new GrowthNode("r_reach",   RIGHT, "긴 궤적",        "평타 판정 범위 +12%",                    GStat.Reach, .12f, 2, P(256, 500), Parts.None, "r_edge2"),
            new GrowthNode("r_edge3",   RIGHT, "날 연마 III",    "평타 피해 +12%",                         GStat.LightDmg, .12f, 3, P(224, 356), Parts.None, "r_finish"),
            new GrowthNode("r_skill",   RIGHT, "스킬 증폭",      "스킬 피해 +10%",                         GStat.SkillDmg, .10f, 3, P(147, 562), Parts.None, "r_reach"),
            new GrowthNode("r_cannon",  RIGHT, "캐논 연동",      "3타 피해 +25% (캐논 포 필요)",           GStat.FinisherDmg, .25f, 3, P(449, 379), Parts.Cannon, "r_edge3"),
            // ---- 왼팔(도구): counter window / damage / cooldown, rope reach ----
            new GrowthNode("l_guard1",  LEFT, "반격 감각 I",     "반격 판정 시간 +0.06초",                 GStat.CounterWindow, .06f, 1, P(1254, 538)),
            new GrowthNode("l_guard2",  LEFT, "반격 감각 II",    "반격 판정 시간 +0.06초",                 GStat.CounterWindow, .06f, 2, P(1403, 605), Parts.None, "l_guard1"),
            new GrowthNode("l_riposte", LEFT, "되받아치기",      "반격 피해 +30%",                         GStat.CounterDmg, .30f, 2, P(1422, 499), Parts.None, "l_guard2"),
            new GrowthNode("l_quick",   LEFT, "빠른 복귀",       "반격 쿨다운 −20%",                       GStat.CounterCd, .20f, 2, P(1244, 691), Parts.None, "l_guard2"),
            new GrowthNode("l_guard3",  LEFT, "반격 감각 III",   "반격 판정 시간 +0.06초",                 GStat.CounterWindow, .06f, 3, P(1536, 563), Parts.None, "l_riposte"),
            new GrowthNode("l_rope1",   LEFT, "로프 와인더",     "로프 사거리 +15% (로프 팔 필요)",        GStat.RopeRange, .15f, 2, P(1409, 719), Parts.Rope, "l_quick"),
            new GrowthNode("l_rope2",   LEFT, "로프 와인더 II",  "로프 사거리 +15% (로프 팔 필요)",        GStat.RopeRange, .15f, 3, P(1346, 827), Parts.Rope, "l_rope1"),
            // ---- 다리: move speed, dash cooldown ----
            new GrowthNode("g_run1",    LEGS, "구동 모터 I",     "이동 속도 +5%",                          GStat.Speed, .05f, 1, P(769, 861)),
            new GrowthNode("g_dash1",   LEGS, "급가속 I",        "대쉬 쿨다운 −10%",                       GStat.DashCd, .10f, 2, P(649, 839), Parts.None, "g_run1"),
            new GrowthNode("g_run2",    LEGS, "구동 모터 II",    "이동 속도 +5%",                          GStat.Speed, .05f, 2, P(517, 826), Parts.None, "g_dash1"),
            new GrowthNode("g_dash2",   LEGS, "급가속 II",       "대쉬 쿨다운 −10%",                       GStat.DashCd, .10f, 2, P(897, 842), Parts.None, "g_run1"),
            new GrowthNode("g_magnet",  LEGS, "자석 접지",       "이동 속도 +6% (자석 다리 필요)",         GStat.Speed, .06f, 3, P(425, 692), Parts.Magnet, "g_run2"),
            new GrowthNode("g_jet",     LEGS, "제트 보조",       "대쉬 쿨다운 −15% (제트엔진 필요)",       GStat.DashCd, .15f, 3, P(1131, 823), Parts.Jet, "g_dash2"),
            new GrowthNode("g_run3",    LEGS, "구동 모터 III",   "이동 속도 +6%",                          GStat.Speed, .06f, 3, P(318, 826), Parts.None, "g_run2"),
            // ---- 머리: map fog, pods, shrines, enemy info ----
            new GrowthNode("h_scan1",   HEAD, "광역 스캐너 I",   "지도 공개 반경 +20%",                    GStat.FogRadius, .20f, 1, P(835, 47)),
            new GrowthNode("h_scan2",   HEAD, "광역 스캐너 II",  "지도 공개 반경 +25%",                    GStat.FogRadius, .25f, 2, P(736, 78), Parts.None, "h_scan1"),
            new GrowthNode("h_pod",     HEAD, "포드 신호 감지",  "40m 안의 충전 포드를 자동으로 발견",     GStat.PodSense, 40, 2, P(474, 129), Parts.None, "h_scan2"),
            new GrowthNode("h_lens",    HEAD, "분석 렌즈",       "적 머리 위에 남은 체력 수치 표시",       GStat.EnemyHp, 1, 2, P(940, 80), Parts.None, "h_scan1"),
            new GrowthNode("h_scan3",   HEAD, "광역 스캐너 III", "지도 공개 반경 +30%",                    GStat.FogRadius, .30f, 3, P(366, 214), Parts.None, "h_pod"),
            new GrowthNode("h_weak",    HEAD, "약점 분석",       "모든 피해 +6%",                          GStat.AllDmg, .06f, 3, P(325, 91), Parts.None, "h_pod"),
            new GrowthNode("h_shrine",  HEAD, "공명 감지",       "120m 안의 공명하지 않은 기둥 방향 표시", GStat.ShrineSense, 120, 3, P(204, 187), Parts.None, "h_scan3"),
        };

        // ---------- state ----------
        /// cumulative basic parts ever picked up (never decreases; dying keeps it)
        public static int xp;
        public static readonly HashSet<string> open = new HashSet<string>();
        public static int spent;
        public static void Reset() { xp = 0; spent = 0; open.Clear(); }
        /// a basic part (or a reward) came in: xp here, the wallet (SoulGame.scrap) at the call site
        public static void Gain(int n) { if (n > 0) xp += n; }

        // ---------- points ----------
        /// cumulative xp needed for the n-th point: 20, 43, 69, 98, 130, ... (+3 each step)
        public static int Threshold(int n) => n <= 0 ? 0 : 20 * n + 3 * n * (n - 1) / 2;
        public static int Earned(int x) { int n = 0; while (Threshold(n + 1) <= x) n++; return n; }
        public static int Earned() => Earned(xp);
        public static int Points => Earned(xp) - spent;
        public static int NextAt => Threshold(Earned(xp) + 1);

        public static GrowthNode Node(string id) { foreach (var n in Nodes) if (n.id == id) return n; return null; }
        public static bool IsOpen(string id) => open.Contains(id);
        public static bool PreMet(GrowthNode n) { foreach (var p in n.pre) if (!open.Contains(p)) return false; return true; }
        public static bool PartMet(GrowthNode n) => Parts.Has(n.part);
        public enum State { Locked, Available, Open }
        /// Available = prerequisites and part met (points may still be short)
        public static State StateOf(GrowthNode n) => open.Contains(n.id) ? State.Open : PreMet(n) && PartMet(n) ? State.Available : State.Locked;
        public static bool CanOpen(GrowthNode n) => StateOf(n) == State.Available && Points >= n.cost;
        /// why a node can't be opened (null = it can)
        public static string Blocker(GrowthNode n)
        {
            if (open.Contains(n.id)) return "열림";
            foreach (var p in n.pre) if (!open.Contains(p)) return "먼저 열기: " + Node(p).name;
            if (!PartMet(n)) return "필요 파츠: " + Parts.Names[n.part];
            if (Points < n.cost) return $"포인트 부족 ({Points}/{n.cost})";
            return null;
        }
        public static bool TryOpen(string id)
        {
            var n = Node(id); if (n == null || !CanOpen(n)) return false;
            open.Add(id); spent += n.cost; return true;
        }

        // ---------- effects: every multiplier lives here ----------
        public static float Sum(GStat s) { float v = 0; foreach (var n in Nodes) if (n.stat == s && open.Contains(n.id)) v += n.value; return v; }
        public static float MaxHp => BASE_HP + Sum(GStat.MaxHp);
        public static float RegenRate => Sum(GStat.Regen);
        public static float TakenScale => Mathf.Max(.2f, 1 - Sum(GStat.Taken));
        /// 코어 과부하 damage-taken multiplier after 출력 안정화
        public static float OverloadTakenScale(float baseTaken) => 1 + (baseTaken - 1) * Mathf.Max(0, 1 - Sum(GStat.OverloadTaken));
        /// damage multiplier for a move name (light1..3 = A combo, counter, heavy/flurry/wave = skills)
        public static float DamageScale(string move)
        {
            float s = 1 + Sum(GStat.AllDmg);
            if (move != null && move.StartsWith("light")) { s *= 1 + Sum(GStat.LightDmg); if (move == "light3") s *= 1 + Sum(GStat.FinisherDmg); }
            else if (move == "counter") s *= (1 + Sum(GStat.CounterDmg)) * (1 + Sum(GStat.SkillDmg));
            else if (move == "heavy" || move == "heavyCharge" || move == "flurry" || move == "wave") s *= 1 + Sum(GStat.SkillDmg);
            return s;
        }
        public static float ReachScale => 1 + Sum(GStat.Reach);
        public static float CounterWindow => Sum(GStat.CounterWindow);
        public static float CounterCdScale => Mathf.Max(.2f, 1 - Sum(GStat.CounterCd));
        public static float RopeRangeScale => 1 + Sum(GStat.RopeRange);
        public static float SpeedScale => 1 + Sum(GStat.Speed);
        public static float DashCdScale => Mathf.Max(.2f, 1 - Sum(GStat.DashCd));
        public static float FogScale => 1 + Sum(GStat.FogRadius);
        public static float PodSense => Sum(GStat.PodSense);
        public static float ShrineSense => Sum(GStat.ShrineSense);
        public static bool EnemyHp => Sum(GStat.EnemyHp) > 0;
    }
}
