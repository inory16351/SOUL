using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Input map (Esc settings window, user decision 2026-11-08): basic controls are data, saved in PlayerPrefs.
    // Mouse is fixed (right = move, left = rope) and shown only. Debug keys stay hardcoded in SoulGame.HandleInput,
    // and the binding conflict check treats them as reserved. Learned skill keys live in Skills.owned (World/Skills.cs).
    public static class KeyBinds
    {
        public enum Act { Attack, Dash, Jump, Map, Rest, View, Growth }
        public static readonly Act[] All = { Act.Attack, Act.Dash, Act.Jump, Act.Map, Act.Rest, Act.View, Act.Growth };
        public static readonly string[] Label = { "공격", "대쉬", "점프", "지도", "휴식 / 상호작용", "시점 전환", "성장 화면" };
        public static readonly KeyCode[] Defaults = { KeyCode.A, KeyCode.Space, KeyCode.F, KeyCode.M, KeyCode.G, KeyCode.T, KeyCode.Tab };
        static readonly KeyCode[] keys = (KeyCode[])Defaults.Clone();

        /// debug / system keys (SoulGame.HandleInput, SoulGame.Skills, SoulAudio): never bindable
        public static readonly HashSet<KeyCode> DebugKeys = new HashSet<KeyCode>
        {
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.P, KeyCode.H, KeyCode.C, KeyCode.V, KeyCode.K, KeyCode.Period, KeyCode.Escape, KeyCode.Return,
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.LeftWindows, KeyCode.RightWindows, KeyCode.LeftCommand, KeyCode.RightCommand,
        };
        /// a key that can never be bound (debug, F-keys, mouse)
        public static bool Blocked(KeyCode k) => k == KeyCode.None || k >= KeyCode.Mouse0 || DebugKeys.Contains(k) || (k >= KeyCode.F1 && k <= KeyCode.F15) || (baseline != null && baseline.Contains(k));

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
            return Blocked(k) ? "디버그 / 시스템" : null;
        }
        /// set without conflict handling (the settings window resolves conflicts first)
        public static void Set(Act a, KeyCode k) { keys[(int)a] = k; Sync(); Save(); }
        public static void ResetDefaults() { for (int i = 0; i < keys.Length; i++) keys[i] = Defaults[i]; Sync(); Save(); }

        // ---------- Skills.Reserved: keep its own entries, replace the default basic keys by the current ones ----------
        static HashSet<KeyCode> baseline;   // Skills.Reserved minus our defaults (snapshot of the growth-tree literal)
        static readonly HashSet<KeyCode> pushed = new HashSet<KeyCode>();
        public static void Sync()
        {
            if (baseline == null)
            {
                baseline = new HashSet<KeyCode>(Skills.Reserved); foreach (var d in Defaults) baseline.Remove(d);
                baseline.ExceptWith(DebugKeys);   // keep only keys we do not already know (so Blocked() stays honest)
            }
            foreach (var k in pushed) if (!baseline.Contains(k) && !DebugKeys.Contains(k)) Skills.Reserved.Remove(k);
            pushed.Clear();
            Skills.Reserved.UnionWith(DebugKeys);
            foreach (var k in keys) if (k != KeyCode.None) { Skills.Reserved.Add(k); pushed.Add(k); }
        }

        // ---------- PlayerPrefs ----------
        const string PREFIX = "soul.key.";
        static bool loaded;
        public static void Load()
        {
            if (loaded) return; loaded = true;
            foreach (var a in All)
            {
                int v = PlayerPrefs.GetInt(PREFIX + a, -1);
                keys[(int)a] = v < 0 ? Defaults[(int)a] : (KeyCode)v;
            }
            Sync();
        }
        public static void Save() { foreach (var a in All) PlayerPrefs.SetInt(PREFIX + a, (int)keys[(int)a]); PlayerPrefs.Save(); }
        /// MCP tests: forget loaded state and read PlayerPrefs again
        public static void Reload() { loaded = false; Load(); }

        public static string KeyName(KeyCode k)
        {
            if (k == KeyCode.None) return "-";
            if (k == KeyCode.Space) return "SPACE";
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "Num" + s.Substring(6);
            return s.Length == 1 ? s : s.Replace("Arrow", "↑").Replace("Control", "Ctrl");
        }

        // ---------- test hook: simulated key presses (MCP) ----------
        /// keys pressed "this frame" for tests; HandleInput reads Down() which also checks this
        public static readonly HashSet<KeyCode> fake = new HashSet<KeyCode>();
        public static bool Pressed(KeyCode k) => k != KeyCode.None && (fake.Contains(k) || Input.GetKeyDown(k));
    }
}
