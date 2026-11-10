using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 부위 슬롯 스킬 (기획서/스킬_슬롯_카탈로그.md 7 ~ 8절, decisions 2026-11-10):
    //   80 slot parts (30 actives + 50 passives, 16 per body part), generated into SkillCatalog.cs from the catalogue.
    //   Every part belongs to one body part and works only while it sits in one of that part's slots (1 + growth
    //   nodes, max 3, ordered 1-2-3: 회로 연결 neighbours). At most 6 actives (keys 1 2 3 4 Q E in WASD).
    //   Swapping / reordering at a charging pod; the level (Lv1-5) and the Lv5 branch stay with the part.
    //   Grudge skills (붉게 빛나는 부품, e.g. 코어 과부하) attach outside the slots, permanently.
    //   Behaviour: Game/SoulGame.Parts.cs (framework: statuses, reactions, heat, circuits, triggers),
    //   SoulGame.PartsActive.cs (the 30 actives), SoulGame.PartsPassive.cs (the 50 passives).
    public enum Slot { Head, Body, Weapon, Arm, Legs }
    /// 회로 연결: Adjacent = works with the neighbouring slots of its own part; Core* = a body part that amplifies a part
    public enum Link { None, Adjacent, CoreWeapon, CoreHead, CoreLegs, CoreArm, BodyAdjacent, CoreAll }
    public class SkillDef
    {
        public string id, name, part, desc; public float cooldown; public int icon; public KeyCode legacy = KeyCode.None;
        public Slot slot; public bool passive, grudge;
        /// 0 일반 · 1 정밀 · 2 희귀 · 3 고대
        public int grade; public int zone = -1; public Link link; public bool costly;
        /// web move played when cast (actives); legacy skills keep their old cast path
        public string move;
        /// effect text per level step (catalogue): Lv3 fixed, Lv5 two branches
        public string lv3, lv5a, lv5b;
    }

    public static partial class Skills
    {
        public const int MAX_LEVEL = 5, MAX_ACTIVE = 6, START_SLOTS = 1, MAX_SLOTS = 3;
        public static readonly string[] SlotNames = { "머리", "몸통", "무기", "팔", "다리" };
        public static readonly string[] GradeNames = { "일반", "정밀", "희귀", "고대" };
        public static readonly Color[] GradeColors = { new Color(.62f, .65f, .68f), new Color(.35f, .65f, 1f), new Color(.72f, .45f, 1f), new Color(1f, .78f, .3f) };
        /// growth branch (Growth.CORE .. HEAD) -> the slot its 'slot' nodes extend
        public static readonly Slot[] BranchSlot = { Slot.Body, Slot.Weapon, Slot.Arm, Slot.Legs, Slot.Head };
        static readonly SkillDef Overload = new SkillDef { id = "overload", name = "코어 과부하", part = "뒤틀린 에너지 코어", slot = Slot.Body, grudge = true, cooldown = 45, icon = UiArt.CORE, desc = "체력 회복 · 이동 · 공격 속도 증가 · 받는 피해 증가 · 코어 열 +6", grade = 3 };
        static SkillDef[] defs;
        /// the 80 catalogue parts + the grudge skills. legacy = an internal move id of the four original skills
        /// (Q whirl, W wave, E counter, R storm; SoulGame.Skill / COOLDOWN), NOT an input key.
        public static SkillDef[] Defs { get { if (defs == null) { var l = new List<SkillDef>(Catalog) { Overload }; defs = l.ToArray(); } return defs; } }
        /// 공명 기둥 reward per zone (catalogue 7-4); grudge parts by zone
        public static readonly string[] ShrineSkill = { "counter", "whirl", "discharge", "storm", "mark", "slam", "resonance", null };
        public static readonly string[] GrudgeSkill = { "overload", null, null, null, null, null, null, null };
        /// keys never offered for a skill. Written here: only the fixed system keys nobody else manages (X = repair kit).
        /// KeyBinds.Sync() adds the rest at run time: the debug keys, the current basic keys (attack, dash, jump, ...)
        /// and, in the WASD scheme, the W A S D move keys. The number keys are free (2026-11-10: slot skills use 1-4).
        public static readonly HashSet<KeyCode> Reserved = new HashSet<KeyCode> { KeyCode.X, KeyCode.Escape, KeyCode.Return };

        public class Owned
        {
            public SkillDef def; public KeyCode key = KeyCode.None; public int level = 1; public float ready; public bool equipped;
            /// Lv5 branch: 0 = not chosen yet (picks ① by default), 1 = ①, 2 = ②
            public int branch;
            /// slot order inside its part (0..2): 회로 연결 neighbours
            public int pos;
            public bool Active => !def.passive;
            /// works right now: grudge skills always, slot skills while equipped
            public bool On => def.grudge || equipped;
            public bool Lv3 => level >= 3;
            public bool B1 => level >= 5 && branch != 2;
            public bool B2 => level >= 5 && branch == 2;
        }
        public static readonly List<Owned> owned = new List<Owned>();

        public static void Reset() { owned.Clear(); }
        public static SkillDef Def(string id) { foreach (var d in Defs) if (d.id == id) return d; return null; }
        public static Owned Get(string id) { foreach (var o in owned) if (o.def.id == id) return o; return null; }
        public static Owned ByKey(KeyCode k) { foreach (var o in owned) if (o.key == k && o.On) return o; return null; }
        public static bool Has(string id) => Get(id) != null;
        /// owned and working (equipped, or a grudge skill)
        public static bool IsOn(string id) { var o = Get(id); return o != null && o.On; }
        /// the working part, or null
        public static Owned On(string id) { var o = Get(id); return o != null && o.On ? o : null; }
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
        public static bool Equip(Owned o)
        {
            if (EquipBlocker(o) != null) return false;
            o.equipped = true; o.pos = Used(o.def.slot) - 1; Renumber(o.def.slot); return true;
        }
        public static void Unequip(Owned o) { if (o != null && !o.def.grudge) { o.equipped = false; Renumber(o.def.slot); } }
        /// the equipped parts of a body part in slot order (1-2-3)
        public static List<Owned> Ordered(Slot s)
        {
            var l = new List<Owned>(); foreach (var o in owned) if (o.equipped && !o.def.grudge && o.def.slot == s) l.Add(o);
            l.Sort((a, b) => a.pos.CompareTo(b.pos)); return l;
        }
        static void Renumber(Slot s) { var l = Ordered(s); for (int i = 0; i < l.Count; i++) l[i].pos = i; }
        /// pod: move a part one slot to the right inside its part (wraps around)
        public static void Shift(Owned o)
        {
            var l = Ordered(o.def.slot); int i = l.IndexOf(o); if (i < 0 || l.Count < 2) return;
            int j = (i + 1) % l.Count; l[i].pos = j; l[j].pos = i; Renumber(o.def.slot);
        }
        /// the parts in the neighbouring slots (slot 2 touches 1 and 3; 1 and 3 touch only 2)
        public static List<Owned> Neighbours(Owned o)
        {
            var r = new List<Owned>(); if (o == null || !o.equipped) return r;
            var l = Ordered(o.def.slot); int i = l.IndexOf(o);
            if (i > 0) r.Add(l[i - 1]); if (i >= 0 && i + 1 < l.Count) r.Add(l[i + 1]);
            return r;
        }
        public static bool NextTo(Owned o, string id) { foreach (var n in Neighbours(o)) if (n.def.id == id) return true; return false; }
        /// slots shrank (never in play, but tests / resets): unequip the overflow
        public static void ClampSlots()
        {
            foreach (Slot s in System.Enum.GetValues(typeof(Slot)))
                for (int i = owned.Count - 1; i >= 0 && Used(s) > SlotCount(s); i--) if (owned[i].def.slot == s && owned[i].equipped && !owned[i].def.grudge) owned[i].equipped = false;
        }

        // ---------- 회로 연결 (catalogue 7-3) ----------
        public const float AMP_CAP = .5f;
        /// amplification of a part from the circuit parts around it (sum, capped; circuit parts don't amplify each
        /// other except 원형 코어). Amplifies the main number of a passive / the damage of an active.
        public static float Amp(Owned o)
        {
            if (o == null || !o.equipped || o.def.grudge) return 0;
            float a = 0, cap = AMP_CAP;
            var proto = On("protocore");
            if (proto != null && proto.B2) cap = .7f;
            bool circuit = o.def.link != Link.None;
            foreach (var c in owned)
            {
                if (!c.equipped || c == o || c.def.grudge) continue;
                float lv = 1 + .1f * (c.level - 1);
                switch (c.def.link)
                {
                    case Link.CoreWeapon: if (o.def.slot == Slot.Weapon && !circuit) a += (c.B2 ? .25f : .15f) * lv; break;
                    case Link.CoreHead: if (o.def.slot == Slot.Head && !circuit) a += (c.B2 ? .25f : .15f) * lv; break;
                    case Link.CoreLegs: if (o.def.slot == Slot.Legs && !circuit) a += .15f * lv; break;
                    case Link.CoreArm: if (o.def.slot == Slot.Arm && !circuit) a += .20f * lv; break;
                    case Link.BodyAdjacent: if (NextTo(c, o.def.id) && !circuit) a += (c.B2 ? .30f : .20f) * lv; break;
                    case Link.CoreAll: if (NextTo(c, o.def.id)) a += .15f * lv; break;
                }
                if (c.def.id == "towerlens" && NextTo(c, o.def.id) && !circuit) a += (c.B2 ? .35f : .25f) * lv;
            }
            if (proto != null && proto != o && circuit) a += .10f;
            return Mathf.Min(a, cap);
        }
        /// level and circuit scale of a part's main number: Lv1 1.0 .. Lv5 1.4, × (1 + Amp)
        public static float Power(Owned o) => o == null ? 0 : (1 + .1f * (o.level - 1)) * (1 + Amp(o));
        public static float Power(string id) => Power(On(id));
        public static int CircuitCount { get { int n = 0; foreach (var o in owned) if (o.equipped && o.def.link != Link.None) n++; return n; } }

        // ---------- upgrades: spend basic parts from the wallet (SoulGame.scrap) ----------
        /// wallet cost to go from level i+1 to i+2 (Lv2 30, Lv3 60, Lv4 100, Lv5 150)
        public static readonly int[] UPGRADE_COST = { 30, 60, 100, 150 };
        public static int Cost(Owned o) => o == null || o.level >= MAX_LEVEL ? 0 : UPGRADE_COST[o.level - 1];
        /// spends from `wallet` (by ref) and raises the level; false = max level or not enough parts
        public static bool LevelUp(Owned o, ref int wallet)
        {
            if (o == null || o.level >= MAX_LEVEL || wallet < Cost(o)) return false;
            wallet -= Cost(o); o.level++; if (o.level >= MAX_LEVEL && o.branch == 0) o.branch = 1; return true;
        }
        /// actives: per level above 1 damage +12 % (× circuit amp), cooldown -8 %
        public static float DamageScale(Owned o) => o == null ? 1 : (1 + .12f * (o.level - 1)) * (1 + Amp(o)) * (1 + Perk(o, PerkKind.Damage));
        public static float CooldownScale(Owned o) => o == null ? 1 : 1 - .08f * (o.level - 1);

        // ---------- numeric perks still read by the old code paths (grudge skills); slot parts use lv3 / lv5 ----------
        public enum PerkKind { Range, Damage, CooldownSec, Window, Duration, Regen }
        public class SkillPerk { public string skill; public int level; public PerkKind kind; public float value; public string desc; }
        public static readonly SkillPerk[] Perks =
        {
            new SkillPerk { skill = "overload", level = 3, kind = PerkKind.Duration,    value = 2f,   desc = "지속 +2초" },
            new SkillPerk { skill = "overload", level = 5, kind = PerkKind.Regen,       value = .01f, desc = "초당 회복 +1% (최대 체력)" },
        };
        /// sum of the unlocked perks of one kind for an owned skill (0 if not owned)
        public static float Perk(Owned o, PerkKind k) { if (o == null) return 0; float v = 0; foreach (var p in Perks) if (p.skill == o.def.id && p.kind == k && o.level >= p.level) v += p.value; return v; }
        public static float Perk(string id, PerkKind k) => Perk(Get(id), k);
        public static float Cooldown(Owned o, float growthScale = 1) => Mathf.Max(.3f, o.def.cooldown * CooldownScale(o) * growthScale - Perk(o, PerkKind.CooldownSec));

        // ---------- 코어 과부하 (Z0 grudge part) ----------
        /// draft numbers (기획_결정사항.md): 8 s, 4 % of max HP per second, move +30 %, damage taken +50 %; per level
        /// above 1 the duration grows by 0.5 s; Perks add Lv3 +2 s, Lv5 +1 %/s. 2026-11-10: also core heat +6 (a price)
        public const float OVERLOAD_TIME = 8, OVERLOAD_REGEN = .04f, OVERLOAD_SPEED = 1.3f, OVERLOAD_TAKEN = 1.5f;
    }
}
