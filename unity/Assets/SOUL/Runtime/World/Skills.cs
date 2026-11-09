using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 스킬 = 파츠 (기획_결정사항.md, user decision 2026-11-08): the start is attack + dash + jump only; every other
    // skill comes from a part. No slot limit: each new skill asks the player for a key. Skill levels cost basic
    // parts from the wallet (2026-11-08; xp points go to the growth node tree, World/GrowthTree.cs). Grudge parts (붉게 빛나는 부품) give a fixed skill or imprint.
    public class SkillDef
    {
        public string id, name, part, desc; public float cooldown; public int icon; public KeyCode legacy = KeyCode.None;
    }

    public static class Skills
    {
        public const int MAX_LEVEL = 5;
        /// legacy = the old fixed key the move code still uses internally (SoulGame.Skill)
        public static readonly SkillDef[] Defs =
        {
            new SkillDef { id = "counter",  name = "반격",        part = "방어 장갑 (왼팔)",      cooldown = 5, icon = UiArt.PARRY, legacy = KeyCode.E, desc = "공격을 받아쳐 경직과 피해" },
            new SkillDef { id = "whirl",    name = "회오리",      part = "회전 관절 (오른팔)",    cooldown = 4, icon = UiArt.WHIRL, legacy = KeyCode.Q, desc = "몸을 돌려 주위를 벤다" },
            new SkillDef { id = "wave",     name = "레이저 파동", part = "코어 방출기",          cooldown = 3, icon = UiArt.WAVE,  legacy = KeyCode.W, desc = "칼날로 빛의 파동을 날린다" },
            new SkillDef { id = "storm",    name = "칼날 폭풍",   part = "고속 구동부 (오른팔)", cooldown = 9, icon = UiArt.STORM, legacy = KeyCode.R, desc = "뛰어올라 칼날을 휘몰아친다" },
            new SkillDef { id = "overload", name = "코어 과부하", part = "뒤틀린 에너지 코어",   cooldown = 45, icon = UiArt.CORE, desc = "체력 회복 · 이동 속도 증가 · 받는 피해 증가" },
        };
        /// draft sources: skill shrine (공명 기둥) of each zone; grudge parts by zone
        public static readonly string[] ShrineSkill = { "counter", "whirl", null, "storm", null, null, null, null };   // wave moved to the Z0 elite (2026-11-09); Z2 shrine: new skill TBD
        public static readonly string[] GrudgeSkill = { "overload", null, null, null, null, null, null, null };
        /// keys the game already uses (or reserves for debug): never offered for binding
        public static readonly HashSet<KeyCode> Reserved = new HashSet<KeyCode>
        {
            KeyCode.A, KeyCode.Space, KeyCode.F, KeyCode.M, KeyCode.G, KeyCode.T, KeyCode.V, KeyCode.C, KeyCode.P, KeyCode.H, KeyCode.K, KeyCode.X,
            KeyCode.Tab, KeyCode.Escape, KeyCode.Return, KeyCode.Period, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl,
            KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftWindows, KeyCode.RightWindows,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };

        public class Owned { public SkillDef def; public KeyCode key = KeyCode.None; public int level = 1; public float ready; }
        public static readonly List<Owned> owned = new List<Owned>();

        public static void Reset() { owned.Clear(); }
        public static SkillDef Def(string id) { foreach (var d in Defs) if (d.id == id) return d; return null; }
        public static Owned Get(string id) { foreach (var o in owned) if (o.def.id == id) return o; return null; }
        public static Owned ByKey(KeyCode k) { foreach (var o in owned) if (o.key == k) return o; return null; }
        public static bool Has(string id) => Get(id) != null;
        public static Owned Grant(string id)
        {
            var d = Def(id); if (d == null || Has(id)) return null;
            var o = new Owned { def = d }; owned.Add(o); return o;
        }
        /// binding a key that another skill holds swaps it off that skill (left unbound)
        public static void Bind(Owned o, KeyCode k) { var other = ByKey(k); if (other != null && other != o) other.key = KeyCode.None; o.key = k; }

        // ---------- upgrades: spend basic parts from the wallet (SoulGame.scrap) ----------
        /// wallet cost to go from level i+1 to i+2 (Lv2 30, Lv3 60, Lv4 100, Lv5 150)
        public static readonly int[] UPGRADE_COST = { 30, 60, 100, 150 };
        public static int Cost(Owned o) => o == null || o.level >= MAX_LEVEL ? 0 : UPGRADE_COST[o.level - 1];
        /// spends from `wallet` (by ref) and raises the level; false = max level or not enough parts
        public static bool LevelUp(Owned o, ref int wallet)
        {
            if (o == null || o.level >= MAX_LEVEL || wallet < Cost(o)) return false;
            wallet -= Cost(o); o.level++; return true;
        }
        /// per level above 1: damage +12 %, cooldown -8 %
        public static float DamageScale(Owned o) => o == null ? 1 : (1 + .12f * (o.level - 1)) * (1 + Perk(o, PerkKind.Damage));
        public static float CooldownScale(Owned o) => o == null ? 1 : 1 - .08f * (o.level - 1);

        // ---------- extra effects per skill level (edit this table) ----------
        public enum PerkKind { Range, Damage, CooldownSec, Window, Duration, Regen }
        public class SkillPerk { public string skill; public int level; public PerkKind kind; public float value; public string desc; }
        public static readonly SkillPerk[] Perks =
        {
            new SkillPerk { skill = "whirl",    level = 3, kind = PerkKind.Range,       value = .20f, desc = "범위 +20%" },
            new SkillPerk { skill = "whirl",    level = 5, kind = PerkKind.Damage,      value = .15f, desc = "피해 +15%" },
            new SkillPerk { skill = "storm",    level = 3, kind = PerkKind.Range,       value = .20f, desc = "범위 +20%" },
            new SkillPerk { skill = "storm",    level = 5, kind = PerkKind.CooldownSec, value = 1.5f, desc = "쿨다운 −1.5초" },
            new SkillPerk { skill = "wave",     level = 3, kind = PerkKind.CooldownSec, value = .5f,  desc = "쿨다운 −0.5초" },
            new SkillPerk { skill = "wave",     level = 5, kind = PerkKind.Damage,      value = .20f, desc = "피해 +20%" },
            new SkillPerk { skill = "counter",  level = 3, kind = PerkKind.Window,      value = .05f, desc = "판정 시간 +0.05초" },
            new SkillPerk { skill = "counter",  level = 5, kind = PerkKind.Damage,      value = .25f, desc = "피해 +25%" },
            new SkillPerk { skill = "overload", level = 3, kind = PerkKind.Duration,    value = 2f,   desc = "지속 +2초" },
            new SkillPerk { skill = "overload", level = 5, kind = PerkKind.Regen,       value = .01f, desc = "초당 회복 +1% (최대 체력)" },
        };
        /// sum of the unlocked perks of one kind for an owned skill (0 if not owned)
        public static float Perk(Owned o, PerkKind k) { if (o == null) return 0; float v = 0; foreach (var p in Perks) if (p.skill == o.def.id && p.kind == k && o.level >= p.level) v += p.value; return v; }
        public static float Perk(string id, PerkKind k) => Perk(Get(id), k);
        public static float Cooldown(Owned o, float growthScale = 1) => Mathf.Max(.3f, o.def.cooldown * CooldownScale(o) * growthScale - Perk(o, PerkKind.CooldownSec));

        // ---------- 코어 과부하 (Z0 grudge part) ----------
        /// draft numbers (기획_결정사항.md): 8 s, 4 % of max HP per second, move +30 %, damage taken +50 %; per level
        /// above 1 the duration grows by 0.5 s; Perks add Lv3 +2 s, Lv5 +1 %/s
        public const float OVERLOAD_TIME = 8, OVERLOAD_REGEN = .04f, OVERLOAD_SPEED = 1.3f, OVERLOAD_TAKEN = 1.5f;
    }
}
