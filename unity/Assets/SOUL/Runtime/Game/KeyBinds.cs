using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Input map (Esc settings window, user decision 2026-11-08): basic controls are data, saved in PlayerPrefs.
    // 2026-11-10 (user decision, 기획_결정사항.md '시점과 조작'): two control schemes, switched in the settings window.
    //   WASD (default): W A S D move (camera-relative, fixed keys), left click attack, right click rope,
    //                   Left Shift dash, Space jump, slot actives on 1 2 3 4 Q E (grudge skills on R ...).
    //   Click (the old Lost Ark layout): right click move, A attack, left click rope, Space dash, F jump,
    //                   slot actives on the key the player picks (Q W E R ...).
    // Each scheme keeps its own key set (soul.key.wasd.* / soul.key.click.*). Debug keys stay hardcoded in
    // SoulGame.HandleInput and the binding conflict check treats them as reserved. Learned skill keys live in
    // Skills.owned (World/Skills.cs); switching the scheme re-keys them (SetScheme).
    public static class KeyBinds
    {
        public enum Scheme { Wasd, Click }
        public static Scheme scheme = Scheme.Wasd;
        public static bool Wasd => scheme == Scheme.Wasd;
        public static readonly string[] SchemeLabel = { "WASD", "클릭 이동" };
        static readonly string[] SchemeTag = { "wasd", "click" };

        public enum Act { Attack, Dash, Jump, Map, Rest, View, Growth }
        public static readonly Act[] All = { Act.Attack, Act.Dash, Act.Jump, Act.Map, Act.Rest, Act.View, Act.Growth };
        public static readonly string[] Label = { "공격", "대쉬", "점프", "지도", "휴식 / 상호작용", "시점 전환", "성장 화면" };
        static readonly KeyCode[][] SchemeDefaults =
        {
            new[] { KeyCode.Mouse0, KeyCode.LeftShift, KeyCode.Space, KeyCode.M, KeyCode.G, KeyCode.T, KeyCode.Tab },   // WASD
            new[] { KeyCode.A,      KeyCode.Space,     KeyCode.F,     KeyCode.M, KeyCode.G, KeyCode.T, KeyCode.Tab },   // click move
        };
        public static KeyCode[] Defaults => SchemeDefaults[(int)scheme];
        static readonly KeyCode[][] sets = { (KeyCode[])SchemeDefaults[0].Clone(), (KeyCode[])SchemeDefaults[1].Clone() };
        static KeyCode[] keys => sets[(int)scheme];

        // ---------- fixed controls (shown in the settings window, not bindable) ----------
        /// WASD scheme: movement keys (up, left, down, right), camera-relative
        public static readonly KeyCode[] MoveKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
        /// rope arm: right click in WASD, left click in click-move
        public static KeyCode RopeButton => Wasd ? KeyCode.Mouse1 : KeyCode.Mouse0;
        /// click-move scheme: the move order button
        public const KeyCode ClickMoveButton = KeyCode.Mouse1;
        static bool IsMoveKey(KeyCode k) => Wasd && System.Array.IndexOf(MoveKeys, k) >= 0;
        /// WASD axis: x = right (D - A), y = forward (W - S); zero in the click scheme
        public static Vector2 MoveAxis()
        {
            if (!Wasd) return Vector2.zero;
            return new Vector2((Held(MoveKeys[3]) ? 1 : 0) - (Held(MoveKeys[1]) ? 1 : 0), (Held(MoveKeys[0]) ? 1 : 0) - (Held(MoveKeys[2]) ? 1 : 0));
        }

        // ---------- slot skill keys ----------
        /// keys handed to equipped active skills in order (6 actives at most); grudge skills sit outside the slots
        static readonly KeyCode[][] SlotKeys =
        {
            new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Q, KeyCode.E },
            new[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.D, KeyCode.S },
        };
        static readonly KeyCode[][] GrudgeKeys = { new[] { KeyCode.R, KeyCode.F, KeyCode.Z }, new[] { KeyCode.Z, KeyCode.B, KeyCode.N } };
        /// WASD: a new active skill takes the next free slot key by itself; click-move keeps the "press a key" prompt
        public static bool AutoSkillKeys => Wasd;
        /// k is not used by a basic action, a fixed move key or another working skill
        public static bool FreeFor(KeyCode k, Skills.Owned o)
        {
            if (k == KeyCode.None || Blocked(k) || ActOf(k).HasValue) return false;
            foreach (var other in Skills.owned) if (other != o && other.key == k && other.On) return false;
            return true;
        }
        /// the key a skill gets without asking: click-move prefers its old fixed key (def.legacy), then the slot list
        public static KeyCode AutoSkillKey(Skills.Owned o)
        {
            int s = (int)scheme;
            if (!Wasd && o.def.legacy != KeyCode.None && FreeFor(o.def.legacy, o)) return o.def.legacy;
            foreach (var k in o.def.grudge ? GrudgeKeys[s] : SlotKeys[s]) if (FreeFor(k, o)) return k;
            foreach (var k in o.def.grudge ? SlotKeys[s] : GrudgeKeys[s]) if (FreeFor(k, o)) return k;
            return KeyCode.None;
        }
        /// give `o` a key if it has none or its key is taken (WASD: on learn / equip; both: after a scheme switch)
        public static void EnsureSkillKey(Skills.Owned o)
        {
            if (o == null || !o.Active) return;
            if (o.key != KeyCode.None && FreeFor(o.key, o)) return;
            Skills.Bind(o, AutoSkillKey(o)); Sync();
        }

        /// debug / system keys (SoulGame.HandleInput, SoulGame.Skills, SoulAudio): never bindable.
        /// 2026-11-10: the number keys and Shift left this list (slot skills, dash); debug moved to F3 / F4 / F5 and ` + number.
        public static readonly HashSet<KeyCode> DebugKeys = new HashSet<KeyCode>
        {
            KeyCode.BackQuote, KeyCode.P, KeyCode.H, KeyCode.C, KeyCode.V, KeyCode.K, KeyCode.Period, KeyCode.Escape, KeyCode.Return,
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.LeftWindows, KeyCode.RightWindows, KeyCode.LeftCommand, KeyCode.RightCommand,
        };
        /// a key that can never be bound (debug, F-keys, mouse, the fixed WASD move keys)
        public static bool Blocked(KeyCode k) => k == KeyCode.None || k >= KeyCode.Mouse0 || DebugKeys.Contains(k) || (k >= KeyCode.F1 && k <= KeyCode.F15) || IsMoveKey(k) || (baseline != null && baseline.Contains(k));

        public static KeyCode Get(Act a) => keys[(int)a];
        public static bool Down(Act a) => Pressed(keys[(int)a]);
        public static string Name(Act a) => KeyName(keys[(int)a]);
        /// the basic action holding k, or null
        public static Act? ActOf(KeyCode k) { if (k == KeyCode.None) return null; foreach (var a in All) if (keys[(int)a] == k) return a; return null; }
        /// who uses k: a basic action name, a skill name, "디버그" or null (free)
        public static string UsedBy(KeyCode k)
        {
            if (k == KeyCode.None) return null;
            var a = ActOf(k); if (a.HasValue) return Label[(int)a.Value];
            var o = Skills.ByKey(k); if (o != null) return o.def.name;
            if (IsMoveKey(k)) return "이동";
            return Blocked(k) ? "디버그 / 시스템" : null;
        }
        /// set without conflict handling (the settings window resolves conflicts first)
        public static void Set(Act a, KeyCode k) { keys[(int)a] = k; Sync(); Save(); }
        /// the current scheme's basic keys back to their defaults
        public static void ResetDefaults() { for (int i = 0; i < keys.Length; i++) keys[i] = Defaults[i]; Sync(); Save(); }

        // ---------- scheme switch ----------
        /// each scheme remembers the skill keys it had, so switching back and forth keeps custom keys
        static readonly Dictionary<Skills.Owned, KeyCode>[] skillStash = { new Dictionary<Skills.Owned, KeyCode>(), new Dictionary<Skills.Owned, KeyCode>() };
        public static void SetScheme(Scheme s)
        {
            if (s == scheme) return;
            var stash = skillStash[(int)scheme]; stash.Clear();
            foreach (var o in Skills.owned) { stash[o] = o.key; o.key = KeyCode.None; }
            scheme = s; Sync();
            foreach (var o in Skills.owned)
                if (o.Active && skillStash[(int)s].TryGetValue(o, out var k) && FreeFor(k, o)) o.key = k;
            foreach (var o in Skills.owned) if (o.Active && o.On && o.key == KeyCode.None) o.key = AutoSkillKey(o);
            Sync(); Save();
        }

        // ---------- Skills.Reserved: keep its own entries, add the debug keys, the current basic keys and the move keys ----------
        static HashSet<KeyCode> baseline;   // Skills.Reserved as written in Skills.cs (system keys such as X), minus what we manage
        static readonly HashSet<KeyCode> pushed = new HashSet<KeyCode>();
        public static void Sync()
        {
            if (baseline == null)
            {
                baseline = new HashSet<KeyCode>(Skills.Reserved);
                foreach (var set in SchemeDefaults) foreach (var d in set) baseline.Remove(d);
                baseline.ExceptWith(DebugKeys);   // keep only keys we do not already know (so Blocked() stays honest)
            }
            foreach (var k in pushed) if (!baseline.Contains(k) && !DebugKeys.Contains(k)) Skills.Reserved.Remove(k);
            pushed.Clear();
            Skills.Reserved.UnionWith(DebugKeys);
            foreach (var k in keys) if (k != KeyCode.None) { Skills.Reserved.Add(k); pushed.Add(k); }
            if (Wasd) foreach (var k in MoveKeys) { Skills.Reserved.Add(k); pushed.Add(k); }
        }

        // ---------- PlayerPrefs ----------
        // soul.ctl.scheme (0 WASD, 1 click) and soul.key.<wasd|click>.<Act> (KeyCode int).
        // Saves from before 2026-11-10 stored the click layout under soul.key.<Act>: Load moves them to the click set.
        const string PREFIX = "soul.key.", SCHEME_KEY = "soul.ctl.scheme";
        static bool loaded;
        public static void Load()
        {
            if (loaded) return; loaded = true;
            if (!PlayerPrefs.HasKey(SCHEME_KEY))
            {
                foreach (var a in All)
                {
                    if (!PlayerPrefs.HasKey(PREFIX + a)) continue;
                    PlayerPrefs.SetInt(PREFIX + SchemeTag[1] + "." + a, PlayerPrefs.GetInt(PREFIX + a)); PlayerPrefs.DeleteKey(PREFIX + a);
                }
                PlayerPrefs.SetInt(SCHEME_KEY, (int)Scheme.Wasd); PlayerPrefs.Save();
            }
            scheme = PlayerPrefs.GetInt(SCHEME_KEY, 0) == 1 ? Scheme.Click : Scheme.Wasd;
            for (int s = 0; s < sets.Length; s++)
                foreach (var a in All)
                {
                    int v = PlayerPrefs.GetInt(PREFIX + SchemeTag[s] + "." + a, -1);
                    sets[s][(int)a] = v < 0 ? SchemeDefaults[s][(int)a] : (KeyCode)v;
                }
            Sync();
        }
        public static void Save()
        {
            PlayerPrefs.SetInt(SCHEME_KEY, (int)scheme);
            for (int s = 0; s < sets.Length; s++) foreach (var a in All) PlayerPrefs.SetInt(PREFIX + SchemeTag[s] + "." + a, (int)sets[s][(int)a]);
            PlayerPrefs.Save();
        }
        /// MCP tests: forget loaded state and read PlayerPrefs again
        public static void Reload() { loaded = false; Load(); }

        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "-";
                case KeyCode.Space: return "SPACE";
                case KeyCode.Mouse0: return "좌클릭";
                case KeyCode.Mouse1: return "우클릭";
                case KeyCode.Mouse2: return "휠클릭";
                case KeyCode.LeftShift: return "SHIFT";
                case KeyCode.RightShift: return "R-SHIFT";
                case KeyCode.BackQuote: return "`";
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "Num" + s.Substring(6);
            return s.Length == 1 ? s : s.Replace("Arrow", "↑").Replace("Control", "Ctrl");
        }

        // ---------- test hook: simulated key presses (MCP) ----------
        /// keys pressed "this frame" for tests (HandleInput reads Down() / Pressed()); fakeHeld = keys held until removed
        public static readonly HashSet<KeyCode> fake = new HashSet<KeyCode>(), fakeHeld = new HashSet<KeyCode>();
        public static bool Pressed(KeyCode k) => k != KeyCode.None && (fake.Contains(k) || Input.GetKeyDown(k));
        public static bool Held(KeyCode k) => k != KeyCode.None && (fakeHeld.Contains(k) || fake.Contains(k) || Input.GetKey(k));
    }
}
