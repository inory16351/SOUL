using System;
using UnityEngine;

namespace Soul
{
    // 스킬 = 파츠 (World/Skills.cs): learned skills, the "press a key for the new skill" prompt, casting, the
    // multipliers the combat code reads (skill level × growth nodes) and 코어 과부하 (heal % of max HP over time, faster, red aura,
    // more damage taken). The Tab screen (node tree + skill-part upgrades) is SoulGame.Growth.cs.
    // Debug: F2 grants the four base skills (1 2 3 4 in the WASD scheme, Q W E R in click-move).
    public partial class SoulGame
    {
        bool skillsInit; Skills.Owned bindPending; float overloadLeft;
        OverloadAura aura; static readonly Array allKeys = Enum.GetValues(typeof(KeyCode));

        void SkillsInit()
        {
            skillsInit = true; Skills.Reset(); Growth.Reset();
            aura = new OverloadAura(transform);   // Fx/OverloadAura (Codex 14): flame tongues, breathing ground glow, embers
        }

        /// a part gave a skill: learn it, put it in a free slot of its part if there is one (actives then ask for a key),
        /// otherwise it waits in storage until it is swapped in at a pod (부위 슬롯, 2026-11-10)
        public void GrantSkill(string id)
        {
            if (!skillsInit) SkillsInit();
            var o = Skills.Grant(id); if (o == null) return;
            string where = o.def.grudge ? "원혼 파츠" : Skills.SlotNames[(int)o.def.slot] + " 슬롯";
            if (o.def.grudge || Skills.Equip(o))
            {
                if (o.Active)
                {   // WASD: the next free slot key (1 2 3 4 Q E, grudge skills R) by itself; click-move asks, as before
                    if (KeyBinds.AutoSkillKeys) KeyBinds.EnsureSkillKey(o);
                    if (o.key != KeyCode.None) Cue($"새 스킬 · {o.def.name} ({where}) — {KeyName(o.key)} 키 · Esc 설정에서 바꿀 수 있습니다", 5);
                    else { bindPending = o; Cue($"새 스킬 · {o.def.name} ({where}) — 사용할 키를 누르세요 · Esc 나중에", 6); }
                }
                else Cue($"새 스킬 · {o.def.name} ({where}) · {o.def.desc}", 4);
            }
            else Cue($"새 스킬 · {o.def.name} — {Skills.EquipBlocker(o)} · 보관함에 넣었다 (충전 포드에서 교체)", 4.5f);
        }

        /// debug (MCP tests): every slot part, Lv1, in storage (equip at a pod or with SlotToggle(id, true))
        public void DebugGrantAll() { if (!skillsInit) SkillsInit(); foreach (var d in Skills.Defs) if (!d.grudge) Skills.Grant(d.id); Cue("[디버그] 슬롯 파츠 80개 획득", 1.6f); }
        public void DebugLevel(string id, int level, int branch) { var o = Skills.Get(id); if (o == null) return; o.level = Mathf.Clamp(level, 1, Skills.MAX_LEVEL); o.branch = branch; }
        public string PartsState => $"heat {heat:F1} over {overheatLeft:F1} white {whiteHotLeft:F1} shield {shieldHp:F0} fields {fields.Count} projs {projs.Count} minions {minions.Count} statuses {statusOf.Count} slow {WorldSlow:F2}";
        public string EnemyStatus(Enemy e) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < 5; i++) if (HasSt(e, (St)i)) sb.Append(StName[i]).Append(" "); return sb.ToString(); }
        /// MCP tests: cast a learned skill as if its key were pressed; overload time left
        public void SkillCast(string id) { var o = Skills.Get(id); if (o != null) CastSkill(o); }
        public float OverloadLeft => overloadLeft;
        public void SkillsTickForTest(float seconds) { for (float t = 0; t < seconds; t += .05f) SkillsUpdate(.05f); }

        /// called first in HandleInput; true = this frame's key went to the binding prompt (or a panel click)
        bool SkillInput()
        {
            if (!skillsInit) SkillsInit();
            if (GrowthInput()) return true;   // Tab growth screen: pauses and swallows every key / click (no rope)
            if (Input.GetKeyDown(KeyCode.F2)) DebugGrantBase();
            if (bindPending != null)
            {
                if (KeyBinds.Pressed(KeyCode.Escape)) { Cue($"{bindPending.def.name} · 키 미지정 (Esc 설정 창의 키 설정에서 지정)", 2.4f); bindPending = null; return true; }
                foreach (KeyCode k in allKeys)
                {
                    if (k == KeyCode.None || k >= KeyCode.Mouse0 || Skills.Reserved.Contains(k) || (k >= KeyCode.F1 && k <= KeyCode.F15) || !KeyBinds.Pressed(k)) continue;
                    Skills.Bind(bindPending, k); Cue($"{bindPending.def.name} → {KeyName(k)}", 1.8f); bindPending = null; return true;
                }
                return false;
            }
            if (DebugLayer) return false;   // ` held: the number keys are debug toggles, not skills
            foreach (var o in Skills.owned) if (o.On && o.Active && KeyBinds.Pressed(o.key)) CastSkill(o);
            return false;
        }
        /// debug (F2, MCP tests): the four base skills, equipped, on the scheme's keys in the old Q W E R order
        /// (회오리, 레이저 파동, 반격, 칼날 폭풍 -> 1 2 3 4 in WASD, Q W E R in click-move)
        public void DebugGrantBase()
        {
            if (!skillsInit) SkillsInit();
            var list = new System.Collections.Generic.List<Skills.Owned>();
            foreach (var id in new[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R })
                foreach (var d in Skills.Defs) if (d.legacy == id) { var o = Skills.Grant(d.id) ?? Skills.Get(d.id); o.equipped = true; o.key = KeyCode.None; list.Add(o); }
            foreach (var o in list) KeyBinds.EnsureSkillKey(o);
            bindPending = null;
            Cue($"[디버그] 기본 스킬 4개 획득 ({string.Join(" ", list.ConvertAll(o => KeyName(o.key)).ToArray())})", 1.6f);
        }

        void CastSkill(Skills.Owned o)
        {
            if (!player.alive || !o.On || !o.Active) return;
            if (overheatLeft > 0) { Cue("과열 · 스킬 봉인", .6f); return; }   // 코어 열 (SoulGame.Parts)
            if (o.ready > realTime && !TryOverclock(o)) return;   // 예비 배터리: pay HP (or heat) to skip the cooldown
            if (o.def.id == "overload")
            {
                overloadLeft = Skills.OVERLOAD_TIME + .5f * (o.level - 1) + Skills.Perk(o, Skills.PerkKind.Duration);
                vfx.Ring(Sim.FlatY(ch.root.position, .1f), 2.2f, true); vfx.Flash(Sim.FlatY(ch.root.position, 1.2f), 1.2f); cam.Shake(.06f);
                AddHeat(6);   // 붉은 부품의 대가 (2026-11-10)
            }
            else if (o.def.legacy != KeyCode.None)
            {
                if (!CanAct()) return;
                ready.Remove(o.def.legacy); Skill(o.def.legacy);   // the four original skills keep their move code (old fixed key)
                if (!anim.busy) return;
            }
            else if (!CastPart(o)) return;   // the slot parts (SoulGame.PartsActive)
            o.ready = realTime + SkillCooldown(o);
            OnActiveCast(o);
        }
        float batteryReady;
        bool TryOverclock(Skills.Owned o)
        {
            var bt = Skills.On("battery"); if (bt == null || realTime < batteryReady || !CanAct()) return false;
            batteryReady = realTime + 12; overclockedFor = o.def.id;
            if (bt.B1) AddHeat(4); else player.hp = Mathf.Max(1, player.hp - player.max * (bt.Lv3 ? .06f : .08f));
            vfx.Sparks(ch.root.position + Vector3.up, 12, .7f, RED); Cue("오버클럭", .6f); return true;
        }

        float SkillCooldown(Skills.Owned o) => Mathf.Max(.3f, Skills.Cooldown(o, o.def.id == "counter" ? Growth.CounterCdScale : 1) - (o.def.id == "storm" && o.B1 ? 1.5f : 0));
        // The three multipliers the combat code reads (one place each):
        /// damage for the move that is hitting = skill level (heavy = 회오리, flurry = 칼날 폭풍, ...) × growth nodes
        float SkillDamageScale(string move)
        {
            string id = PartOfMove(move);
            return (id == null ? 1 : Skills.DamageScale(Skills.Get(id))) * Growth.DamageScale(move) * (perkBlade ? 1.1f : 1);   // 벼린 칼날 (merchant)
        }
        /// move speed (anim.speedScale, SoulGame.Zone WaterStep) = 코어 과부하 × 다리 nodes
        float OverloadSpeed => (overloadLeft > 0 ? Skills.OVERLOAD_SPEED : 1) * Growth.SpeedScale * PartsSpeed;
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

        static string KeyName(KeyCode k) => KeyBinds.KeyName(k);   // one naming table (1 not Alpha1, 좌클릭, SHIFT)

        // ---------- HUD ----------
        /// skill bar entries: attack, learned skills (bound first), dash, jump — each with its current key
        void SkillBar(System.Collections.Generic.List<(string key, string label, int icon, float cdLeft)> list)
        {
            list.Add((KeyBinds.Name(KeyBinds.Act.Attack), "연속 베기", UiArt.SLASH, 0));
            foreach (var o in Skills.owned)
            {
                if (!o.On || !o.Active) continue;   // passives / stored skills have no button
                float left = o.ready > realTime ? (o.ready - realTime) / SkillCooldown(o) : 0;
                if (o.def.id == "overload" && overloadLeft > 0) left = 0;
                int pi = PartIconIndex(o.def.id);
                list.Add((o.key == KeyCode.None ? "-" : KeyName(o.key), o.def.name, pi >= 0 ? -(pi + 1) : o.def.icon, left));   // < 0 = part sheet cell
            }
            list.Add((KeyBinds.Name(KeyBinds.Act.Dash), "대쉬", UiArt.DASH, ready.TryGetValue(KeyCode.Space, out var ts) && ts > realTime ? (ts - realTime) / COOLDOWN[KeyCode.Space] : 0));
            list.Add((KeyBinds.Name(KeyBinds.Act.Jump), "점프", UiArt.JUMP, ready.TryGetValue(KeyCode.F, out var tf) && tf > realTime ? (tf - realTime) / COOLDOWN[KeyCode.F] : 0));
        }

        void SkillsGUI(float W, float H)
        {
            if (!skillsInit) return;
            if (overloadLeft > 0) { GUI.color = new Color(1, .4f, .35f); GUI.Label(new Rect(20, 40, 260, 20), $"코어 과부하 {overloadLeft:F1}s", small); GUI.color = Color.white; }
            GrowthHud(W, H); PartsGUI(W, H);
            if (growthOpen) GrowthGUI(W, H);
            GUI.color = Color.white;
        }
    }
}
