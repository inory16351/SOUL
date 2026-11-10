using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 부위 슬롯 스킬 (기획_결정사항.md '부위 슬롯 + 공용 노드 + 모듈 업그레이드', user decision 2026-11-10):
    //   every skill reward belongs to one body part (slot) and works only while it sits in one of that part's slots.
    //   Slots: 1 per part at the start, +1 per 'slot' growth node of the matching branch (max 3). At most 6 actives
    //   are equipped at once (each gets a key); passives just fill slots. Swapping is done at a charging pod; the
    //   upgrade level (basic parts, Lv1-5) stays with the skill. Grudge skills (원혼 파츠, e.g. 코어 과부하) attach
    //   outside the slots, permanently. Movement abilities are 모듈 업그레이드 (Parts bits, ZoneGraph.cs), always on.
    //   The full ~60-skill catalog comes from 기획서/스킬_슬롯_카탈로그.md: add rows to Defs.
    public enum Slot { Head, Body, Weapon, Arm, Legs }
    public class SkillDef
    {
        public string id, name, part, desc; public float cooldown; public int icon; public KeyCode legacy = KeyCode.None;
        public Slot slot; public bool passive, grudge;
    }

    public static class Skills
    {
        public const int MAX_LEVEL = 5, MAX_ACTIVE = 6, START_SLOTS = 1, MAX_SLOTS = 3;
        public static readonly string[] SlotNames = { "머리", "몸통", "무기", "팔", "다리" };
        /// growth branch (Growth.CORE .. HEAD) -> the slot its 'slot' nodes extend
        public static readonly Slot[] BranchSlot = { Slot.Body, Slot.Weapon, Slot.Arm, Slot.Legs, Slot.Head };
        /// legacy = the old fixed key the move code still uses internally (SoulGame.Skill)
        public static readonly SkillDef[] Defs =
        {
            new SkillDef { id = "counter",  name = "반격",        part = "방어 장갑",           slot = Slot.Arm,    cooldown = 5, icon = UiArt.PARRY, legacy = KeyCode.E, desc = "공격을 받아쳐 경직과 피해" },
            new SkillDef { id = "whirl",    name = "회오리",      part = "회전 관절",           slot = Slot.Weapon, cooldown = 4, icon = UiArt.WHIRL, legacy = KeyCode.Q, desc = "몸을 돌려 주위를 벤다" },
            new SkillDef { id = "wave",     name = "레이저 파동", part = "칼날 방출기",         slot = Slot.Weapon, cooldown = 3, icon = UiArt.WAVE,  legacy = KeyCode.W, desc = "칼날로 빛의 파동을 날린다" },
            new SkillDef { id = "storm",    name = "칼날 폭풍",   part = "고속 구동부",         slot = Slot.Weapon, cooldown = 9, icon = UiArt.STORM, legacy = KeyCode.R, desc = "뛰어올라 칼날을 휘몰아친다" },
            new SkillDef { id = "suck",     name = "흡입",        part = "준설기 흡입구",       slot = Slot.Arm,    cooldown = 8, icon = UiArt.WAVE, desc = "앞쪽 적을 끌어당겨 모은다" },
            new SkillDef { id = "toxic",    name = "독에 잠식된 코어", part = "껍질 숙주의 코어", slot = Slot.Body, passive = true, icon = UiArt.CORE, desc = "공격에 독이 묻는다 (3초) · 칼날이 보라색으로 물든다" },
            new SkillDef { id = "overload", name = "코어 과부하", part = "뒤틀린 에너지 코어",  slot = Slot.Body, grudge = true, cooldown = 45, icon = UiArt.CORE, desc = "체력 회복 · 이동 속도 증가 · 받는 피해 증가" },
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

        public class Owned
        {
            public SkillDef def; public KeyCode key = KeyCode.None; public int level = 1; public float ready; public bool equipped;
            public bool Active => !def.passive;
            /// works right now: grudge skills always, slot skills while equipped
            public bool On => def.grudge || equipped;
        }
        public static readonly List<Owned> owned = new List<Owned>();

        public static void Reset() { owned.Clear(); }
        public static SkillDef Def(string id) { foreach (var d in Defs) if (d.id == id) return d; return null; }
        public static Owned Get(string id) { foreach (var o in owned) if (o.def.id == id) return o; return null; }
        public static Owned ByKey(KeyCode k) { foreach (var o in owned) if (o.key == k && o.On) return o; return null; }
        public static bool Has(string id) => Get(id) != null;
        /// owned and working (equipped, or a grudge skill)
        public static bool IsOn(string id) { var o = Get(id); return o != null && o.On; }
        public static Owned Grant(string id)
        {
            var d = Def(id); if (d == null || Has(id)) return null;
            var o = new Owned { def = d }; owned.Add(o); return o;
        }
        /// binding a key that another skill holds swaps it off that skill (left unbound)
        public static void Bind(Owned o, KeyCode k) { foreach (var other in owned) if (other != o && other.key == k) other.key = KeyCode.None; o.key = k; }

        // ---------- slots ----------
        /// slots of a part: 1 + its branch's open 'slot' nodes (Growth.SlotBonus), max 3
        public static int SlotCount(Slot s) => Mathf.Min(MAX_SLOTS, START_SLOTS + Growth.SlotBonus(s));
        public static int Used(Slot s) { int n = 0; foreach (var o in owned) if (o.equipped && !o.def.grudge && o.def.slot == s) n++; return n; }
        public static int ActiveCount { get { int n = 0; foreach (var o in owned) if (o.equipped && !o.def.grudge && o.Active) n++; return n; } }
        /// why `o` can't be equipped now (null = it can)
        public static string EquipBlocker(Owned o)
        {
            if (o == null || o.def.grudge || o.equipped) return "";
            if (Used(o.def.slot) >= SlotCount(o.def.slot)) return $"{SlotNames[(int)o.def.slot]} 슬롯이 가득 찼다 ({Used(o.def.slot)}/{SlotCount(o.def.slot)})";
            if (o.Active && ActiveCount >= MAX_ACTIVE) return $"액티브 스킬은 {MAX_ACTIVE}개까지 장착한다";
            return null;
        }
        public static bool Equip(Owned o) { if (EquipBlocker(o) != null) return false; o.equipped = true; return true; }
        public static void Unequip(Owned o) { if (o != null && !o.def.grudge) o.equipped = false; }
        /// slots shrank (never in play, but tests / resets): unequip the overflow
        public static void ClampSlots()
        {
            foreach (Slot s in System.Enum.GetValues(typeof(Slot)))
                for (int i = owned.Count - 1; i >= 0 && Used(s) > SlotCount(s); i--) if (owned[i].def.slot == s && owned[i].equipped && !owned[i].def.grudge) owned[i].equipped = false;
        }

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
