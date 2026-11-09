using System;
using UnityEngine;

namespace Soul
{
    // 스킬 = 파츠 (World/Skills.cs): learned skills, the "press a key for the new skill" prompt, casting, the
    // multipliers the combat code reads (skill level × growth nodes) and 코어 과부하 (heal % of max HP over time, faster, red aura,
    // more damage taken). The Tab screen (node tree + skill-part upgrades) is SoulGame.Growth.cs.
    // Debug: F2 grants the four base skills on Q W E R.
    public partial class SoulGame
    {
        bool skillsInit; Skills.Owned bindPending; float overloadLeft;
        OverloadAura aura; static readonly Array allKeys = Enum.GetValues(typeof(KeyCode));

        void SkillsInit()
        {
            skillsInit = true; Skills.Reset(); Growth.Reset();
            aura = new OverloadAura(transform);   // Fx/OverloadAura (Codex 14): flame tongues, breathing ground glow, embers
        }

        /// a part gave a skill: learn it and ask for a key (World/Skills.ShrineSkill, GrudgeSkill)
        public void GrantSkill(string id)
        {
            if (!skillsInit) SkillsInit();
            var o = Skills.Grant(id); if (o == null) return;
            bindPending = o; Cue($"새 스킬 · {o.def.name} ({o.def.part}) — 사용할 키를 누르세요 · Esc 나중에", 6);
        }

        /// MCP tests: cast a learned skill as if its key were pressed; overload time left
        public void SkillCast(string id) { var o = Skills.Get(id); if (o != null) CastSkill(o); }
        public float OverloadLeft => overloadLeft;
        public void SkillsTickForTest(float seconds) { for (float t = 0; t < seconds; t += .05f) SkillsUpdate(.05f); }

        /// called first in HandleInput; true = this frame's key went to the binding prompt (or a panel click)
        bool SkillInput()
        {
            if (!skillsInit) SkillsInit();
            if (GrowthInput()) return true;   // Tab growth screen: pauses and swallows every key / click (no rope)
            if (Input.GetKeyDown(KeyCode.F2)) { foreach (var d in Skills.Defs) if (d.legacy != KeyCode.None) { var o = Skills.Grant(d.id) ?? Skills.Get(d.id); Skills.Bind(o, d.legacy); } bindPending = null; Cue("[디버그] 기본 스킬 4개 획득 (Q W E R)", 1.6f); }
            if (bindPending != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { Cue($"{bindPending.def.name} · 키 미지정 (Esc 설정 창의 키 설정에서 지정)", 2.4f); bindPending = null; return true; }
                foreach (KeyCode k in allKeys)
                {
                    if (k >= KeyCode.Mouse0 || Skills.Reserved.Contains(k) || (k >= KeyCode.F1 && k <= KeyCode.F15) || !Input.GetKeyDown(k)) continue;
                    Skills.Bind(bindPending, k); Cue($"{bindPending.def.name} → {KeyName(k)}", 1.8f); bindPending = null; return true;
                }
                return false;
            }
            foreach (var o in Skills.owned) if (o.key != KeyCode.None && Input.GetKeyDown(o.key)) CastSkill(o);
            return false;
        }

        void CastSkill(Skills.Owned o)
        {
            if (!player.alive || o.ready > realTime) return;
            if (o.def.id == "suck") { if (!CanAct()) return; FaceCursor(); CastSuck(); }   // 거대 준설기 skill (SoulGame.Mire)
            else if (o.def.id == "overload")
            {
                overloadLeft = Skills.OVERLOAD_TIME + .5f * (o.level - 1) + Skills.Perk(o, Skills.PerkKind.Duration);
                vfx.Ring(Sim.FlatY(ch.root.position, .1f), 2.2f, true); vfx.Flash(Sim.FlatY(ch.root.position, 1.2f), 1.2f); cam.Shake(.06f);
            }
            else
            {
                if (o.def.legacy == KeyCode.None || !CanAct()) return;
                ready.Remove(o.def.legacy); Skill(o.def.legacy);   // the move code still keys on the old fixed key
                if (!anim.busy) return;
            }
            o.ready = realTime + SkillCooldown(o);
        }

        float SkillCooldown(Skills.Owned o) => Skills.Cooldown(o, o.def.id == "counter" ? Growth.CounterCdScale : 1);
        // The three multipliers the combat code reads (one place each):
        /// damage for the move that is hitting = skill level (heavy = 회오리, flurry = 칼날 폭풍, ...) × growth nodes
        float SkillDamageScale(string move)
        {
            string id = move == "heavy" || move == "heavyCharge" ? "whirl" : move == "flurry" ? "storm" : move == "counter" ? "counter" : move == "wave" ? "wave" : null;
            return (id == null ? 1 : Skills.DamageScale(Skills.Get(id))) * Growth.DamageScale(move) * (perkBlade ? 1.1f : 1);   // 벼린 칼날 (merchant)
        }
        /// move speed (anim.speedScale, SoulGame.Zone WaterStep) = 코어 과부하 × 다리 nodes
        float OverloadSpeed => (overloadLeft > 0 ? Skills.OVERLOAD_SPEED : 1) * Growth.SpeedScale;
        /// damage taken (OnPlayerHit) = 코어 과부하 (softened by 출력 안정화) × 충격 흡수판
        float OverloadTaken => (overloadLeft > 0 ? Growth.OverloadTakenScale(Skills.OVERLOAD_TAKEN) : 1) * Growth.TakenScale;

        void SkillsUpdate(float dt)
        {
            if (!skillsInit) SkillsInit();
            GrowthApply(dt);
            if (overloadLeft > 0)
            {
                overloadLeft -= dt;
                if (player.alive) player.hp = Mathf.Min(player.max, player.hp + player.max * (Skills.OVERLOAD_REGEN + Skills.Perk("overload", Skills.PerkKind.Regen)) * dt);
            }
            aura.Update(dt, ch.root.position, overloadLeft > .5f ? 1 : 0);   // fades itself in / out
        }

        static string KeyName(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Keypad")) return "Num" + s.Substring(6);
            return s.Length == 1 ? s : s.Replace("Arrow", "↑").Replace("Control", "Ctrl");
        }

        // ---------- HUD ----------
        /// skill bar entries: A, learned skills (bound first), SPACE, F
        void SkillBar(System.Collections.Generic.List<(string key, string label, int icon, float cdLeft)> list)
        {
            list.Add((KeyBinds.Name(KeyBinds.Act.Attack), "연속 베기", UiArt.SLASH, 0));
            foreach (var o in Skills.owned)
            {
                float left = o.ready > realTime ? (o.ready - realTime) / SkillCooldown(o) : 0;
                if (o.def.id == "overload" && overloadLeft > 0) left = 0;
                list.Add((o.key == KeyCode.None ? "-" : KeyName(o.key), o.def.name, o.def.icon, left));
            }
            list.Add((KeyBinds.Name(KeyBinds.Act.Dash), "대쉬", UiArt.DASH, ready.TryGetValue(KeyCode.Space, out var ts) && ts > realTime ? (ts - realTime) / COOLDOWN[KeyCode.Space] : 0));
            list.Add((KeyBinds.Name(KeyBinds.Act.Jump), "점프", UiArt.JUMP, ready.TryGetValue(KeyCode.F, out var tf) && tf > realTime ? (tf - realTime) / COOLDOWN[KeyCode.F] : 0));
        }

        void SkillsGUI(float W, float H)
        {
            if (!skillsInit) return;
            if (overloadLeft > 0) { GUI.color = new Color(1, .4f, .35f); GUI.Label(new Rect(20, 40, 260, 20), $"코어 과부하 {overloadLeft:F1}s", small); GUI.color = Color.white; }
            GrowthHud(W, H);
            if (growthOpen) GrowthGUI(W, H);
            GUI.color = Color.white;
        }
    }
}
