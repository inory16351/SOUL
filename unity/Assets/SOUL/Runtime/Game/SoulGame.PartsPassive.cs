using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 패시브 50개 (기획서/스킬_슬롯_카탈로그.md 8절). Every passive is a trigger ('~하면 ~한다'); the hooks below are
    // called by the combat code (SoulGame.cs OnPlayerHit / HitEnemy / Skill / Attack) and the framework (SoulGame.Parts).
    // Numbers are the catalogue drafts; Skills.Power(o) scales a part's main number by level (1.0 .. 1.4) and 회로 연결.
    // 공격 속도 (AttackSpeed): the A combo and the weapon actives play faster (SoulAnimator.moveRate), capped at 1.8x.
    public partial class SoulGame
    {
        static Skills.Owned P(string id) => Skills.On(id);
        // counters / timers of the passives
        int riposteLeft, riposteHits, chainLinks, relayCharge, scrapStacks, mirrorBolts, scrapPlates, lockStacks, glowMotes, proteCharge, compoundEyes, reactionChecks;
        float scrapStackUntil, riposteLock, glassBrokenUntil, hazardReady, rebootUsedAt = -1, walkerReady, tailReady, sparkReady, sparkLeftHits, lastDashAt = -9, secondDashFlag, overchargeUntil, chargedBlade, recentKillsUntil;
        int recentKills, plateCracks, dodgeCritNext, executeMark; bool wireCharged, rebootReady = true, ashCrit, smokeCrit, glideOn;
        readonly List<float> killTimes = new List<float>();
        readonly List<(string id, float at)> castLog = new List<(string, float)>();
        float speedMul = 1, speedUntil, dmgBuff, dmgBuffUntil, calcUntil, calcPredict, dashAt, dashFrom;
        Vector3 dashStart; float doubleDashReady;

        // ---------- 공격 속도 ----------
        float atkBuff, atkBuffUntil;
        void AttackSpeedBuff(float add, float t) { if (realTime > atkBuffUntil) atkBuff = 0; atkBuff = Mathf.Max(atkBuff, add); atkBuffUntil = Mathf.Max(atkBuffUntil, realTime + t); }
        /// attack speed of the A combo and the weapon actives (1 = normal, max 1.8)
        public float AttackSpeed
        {
            get
            {
                float k = 1;
                if (realTime < atkBuffUntil) k += atkBuff;
                if (anchoredUntil > realTime) k += .30f * Skills.Power(anchorPart);
                if (P("chainblade") is var cb && cb != null && cb.B2) k += .05f * chainLinks;
                if (sawUntil > realTime) k += .15f;
                if (P("ember") is var em && em != null && !em.B2 && heat >= 6) k += .15f;
                if (whiteHotLeft > 0 && P("furnace") is var fu && fu != null && !fu.B2) k += .20f;
                if (riposteLeft > 0) k += .25f;
                if (chargedBlade > realTime) k += .20f;
                if (P("tailreflex") is var tr && tr != null && tr.B1 && player.hp < player.max * (tr.Lv3 ? .4f : .3f)) k += .15f;
                if (overloadLeft > 0) k += .20f;
                return Mathf.Min(1.8f, k);
            }
        }
        /// moves sped up by attack speed: the A combo and the weapon-slot actives (blade moves)
        float MoveRate(string n)
        {
            if (n.StartsWith("light")) return AttackSpeed;
            var id = PartOfMove(n); var o = id != null ? Skills.Get(id) : null;
            return o != null && o.def.slot == Slot.Weapon ? 1 + (AttackSpeed - 1) * .5f : 1;
        }

        /// move speed multiplier from the parts (OverloadSpeed reads it)
        float PartsSpeed
        {
            get
            {
                float k = realTime < speedUntil ? speedMul : 1;
                if (whiteHotLeft > 0 && P("furnace") is var fu && fu != null) k *= fu.B2 ? .9f : 1.15f;
                var wk = P("walker"); if (wk != null && player.hp < player.max * (wk.Lv3 ? .6f : .5f)) k *= 1 + .10f * Skills.Power(wk);
                var tr = P("tailreflex"); if (tr != null && tr.B1 && player.hp < player.max * (tr.Lv3 ? .4f : .3f)) k *= 1.15f;   // and attack speed (AttackSpeed)
                var pl = P("plate"); if (pl != null && !pl.Lv3) k *= .96f;
                if (InField(ch.root.position, "smoke") && P("smoke") is var sm && sm != null && sm.Lv3) k *= 1.2f;
                if (P("webfoot") is var wf && wf != null && (InField(ch.root.position, "poison") || realTime < webfootUntil)) k *= 1 + .15f * Skills.Power(wf);
                if (calcUntil > realTime) k *= 1.0f;
                return k;
            }
        }
        float webfootUntil;
        void SpeedBuff(float mul, float t) { speedMul = Mathf.Max(realTime < speedUntil ? speedMul : 1, mul); speedUntil = Mathf.Max(speedUntil, realTime + t); }
        /// 완벽 회피 window (dash i-frames): 꼬리 반사 / 전투 연산 ② double it
        float DashInvulnTime
        {
            get
            {
                float t = .34f;
                var tr = P("tailreflex"); if (tr != null && player.hp < player.max * (tr.Lv3 ? .4f : .3f)) t *= 2;
                if (calcPredict > realTime) t *= 2;
                return Mathf.Min(t, .7f);
            }
        }

        // ---------- status hooks ----------
        float StatusTimeScale(Enemy e, St st)
        {
            float k = 1, b = ST_TIME[(int)st];
            if (st == St.Mark)
            {
                if (P("aimlens") is var al && al != null && al.Lv3) k += 2 / b;
                if (P("echolens") is var el && el != null && el.Lv3) k += 2 / b;
                if (P("relay") is var rl && rl != null && rl.B2) k += 3 / b;
            }
            if (st == St.Shock && P("decoder") is var dc && dc != null && dc.Lv3) k += .5f / b;
            if (st == St.Burn && P("ember") is var em && em != null && em.Lv3) k += 1 / b;
            if (st == St.Burn && P("furnace") is var fu && fu != null && fu.B2) k += 1 / b;
            if (P("ganglion") != null && HasSt(e, St.Mark) && st != St.Mark) k *= 1.3f;
            return k;
        }
        float ShockTimeScale(Enemy e) => 1;
        float PoisonDamageScale(Enemy e)
        {
            float k = 1;
            if (P("toxic") is var tx && tx != null) k *= Skills.Power(tx);
            if (P("pollen") is var po && po != null && po.B1 && HasSt(e, St.Mark)) k *= 1.3f;
            if (P("stinger") is var sg && sg != null && sg.Lv3 && statusOf.TryGetValue(e, out var s)) k *= 1 + .1f * s.poisonStacks;
            return k;
        }
        float BurnDamageScale()
        {
            float k = 1; var em = P("ember"); if (em != null && !em.B2) k *= 1 + .1f * heat;
            return k;
        }
        float AcidStrength() { var ac = Skills.Get("acid"); return ac != null && ac.B1 ? .25f : .15f; }
        void OnStatusApplied(Enemy e, St st)
        {
            if (st == St.Poison && P("pollen") != null) Apply(e, St.Mark);
            if (st == St.Mark && P("relay") is var rl && rl != null) { relayCharge = Mathf.Min(5, relayCharge + 1); if (relayCharge >= 5 && rl.B1) { relayCharge = 0; foreach (var o in enemies.list) if (o.Hittable && Vector3.Distance(o.pos, ch.root.position) < 22) Apply(o, St.Mark, 2); Cue("중계 · 전체 표식", .8f); } }
        }
        void OnReaction(Enemy e, Re r)
        {
            if (P("sparkplug") is var sp && sp != null && sp.B2) heat = Mathf.Max(0, heat - 2);
        }
        void OnWallSlam(Enemy e) { }

        // ---------- damage scale / crits ----------
        float PassiveDamageScale(Enemy e, string src, bool basic, bool ambush)
        {
            float k = 1;
            if (basic && riposteLeft > 0) k *= 1 + .40f * Skills.Power("riposte");
            if (basic && lightframeSlash) k *= P("lightframe") is var lf && lf != null && lf.Lv3 ? 1.6f : 1.3f;
            if (fallStrikeUntil > realTime && P("dropcalc") is var dp && dp != null) { k *= 1 + Mathf.Min(.9f, .15f * fallStrikeHeight) * Skills.Power(dp); fallStrikeUntil = 0; Apply(e, St.Mark); if (dp.B1) Blast(ch.root.position, 3, 15, null, "dropcalc", WHITE, true, e); }
            if (HasSt(e, St.Mark) && P("towerlens") is var tl && tl != null) k *= 1 + .15f * Skills.Power(tl);
            if (HasSt(e, St.Mark) && P("aimlens") is var al && al != null && al.B2 && (src == "wave" || src == "harpoon" || src == "drone")) k *= 1.4f;
            if (HasSt(e, St.Burn) && P("furnace") is var fu && fu != null) k *= 1 + .15f * Skills.Power(fu);
            if (basic && sparkLeftHits > 0 && P("sparkplug") is var spk && spk != null && spk.Lv3) k *= 1.2f;
            if (basic && P("glassblade") is var gb && gb != null && realTime > glassBrokenUntil) k *= 1 + .35f * Skills.Power(gb);
            if (Big(e) && P("weakspot") is var ws && ws != null) { k *= 1 + .15f * Skills.Power(ws); if (statusOf.TryGetValue(e, out var s) && s.hitsOnMe >= 3) k *= 1.4f; }
            if (basic && !Organic(e) && P("vibroblade") != null) k *= 1.15f;
            if (realTime < slowUntil && P("hazard") is var hz && hz != null && hz.Lv3) k *= 1.25f;
            if (basic && dodgeBuffUntil > realTime && P("compound") != null) { k *= 1.3f; dodgeBuffUntil = 0; }
            if (src == "counter" && P("damper") != null) { k *= 1 + Mathf.Min(.5f, damperCharge); damperCharge = 0; }
            if (P("protocore") != null && Skills.CircuitCount >= 4) k *= 1.08f;
            if (realTime < dmgBuffUntil) k *= 1 + dmgBuff;
            if (Skills.Get(src) is var so && so != null && so.Active && so.On) k *= ActiveBonus(so, false);
            if (Big(e) && ambush) k *= 1;   // big enemies: ambush crits only (no extra)
            return k;
        }
        float dodgeBuffUntil, damperCharge;
        float CritChance(Enemy e, string src, bool basic, bool ambush)
        {
            if (ambush) return 1;
            if (statusOf.TryGetValue(e, out var s) && s.armourGoneUntil > realTime && src == "piston") return 1;
            if (dodgeCritNext > 0) { dodgeCritNext--; return 1; }
            if (ashCrit) { ashCrit = false; return 1; }
            if (e.state == "stagger" && pistonStun.Contains(e)) { pistonStun.Remove(e); return 1; }
            float c = 0;
            if (P("glassblade") != null && realTime > glassBrokenUntil) c += .10f;
            if (P("compound") is var cp && cp != null && cp.B2) c += .03f * compoundEyes;
            return c;
        }
        readonly HashSet<Enemy> pistonStun = new HashSet<Enemy>();
        float CritScale(Enemy e, string src, bool basic, bool ambush)
        {
            float k = 2;
            if (smokeCrit && P("smoke") is var sm && sm != null && sm.B1) { smokeCrit = false; k = 4; }
            return k;
        }

        // ---------- on hit ----------
        void OnHitDealt(Enemy e, float dmg, string src, bool basic, bool crit, bool ambush)
        {
            var s = SOf(e);
            var srcPart = Skills.Get(src);
            bool active = srcPart != null && srcPart.Active && srcPart.On;
            if (P("toxic") != null) Apply(e, St.Poison);
            if (whiteHotLeft > 0) Apply(e, St.Burn);
            if (realTime < weldBladeUntil && basic) Apply(e, St.Burn);
            // 무기 옆칸 회로: neighbouring weapon actives carry the passive's status
            if (active && srcPart.def.slot == Slot.Weapon)
            {
                foreach (var n in Skills.Neighbours(srcPart))
                {
                    if (n.def.id == "wirespool") Apply(e, St.Shock);
                    if (n.def.id == "rustblade") Apply(e, St.Corrode);
                    if (n.def.id == "stinger" && HasSt(e, St.Poison)) Apply(e, St.Poison, -1, false, 0);
                }
            }
            if (active && srcPart.def.slot == Slot.Arm)
            {
                if (P("torch") != null) Apply(e, St.Burn);
                foreach (var n in Skills.Neighbours(srcPart)) if (n.def.id == "coilglove") Apply(e, St.Shock);
            }
            if (active && srcPart.def.slot == Slot.Head)
                foreach (var n in Skills.Neighbours(srcPart)) { if (n.def.id == "echolens") Apply(e, St.Mark); if (n.def.id == "pollen") Apply(e, St.Poison); }
            if (basic)
            {
                basicHits++;
                if (P("wirespool") is var ws && ws != null)
                {
                    if (basicHits % (ws.B1 || ws.B2 ? 3 : 4) == 0) { if (Apply(e, St.Shock) && ws.B1 && ++wireShocks >= 3) { wireShocks = 0; AddField("fence", e.pos, 2, 2); } }
                    if (ws.Lv3 && HasSt(e, St.Shock) && anim.name == "light3") e.Flinch(1);
                }
                if (P("rustblade") is var rb && rb != null)
                {
                    if (s.hitsOnMe >= (rb.B2 ? 2 : 3)) Apply(e, St.Corrode);
                    if (rb.Lv3 && HasSt(e, St.Corrode) && s.scrapChips < 3) { s.scrapChips++; GainScrap(1); }
                }
                if (P("stinger") is var sg && sg != null && HasSt(e, St.Poison)) { Apply(e, St.Poison, -1, false, 1); if (sg.B2 && anim.name == "light3") Shoot(ch.root.position + Vector3.up, ch.Forward, 18, 5, StColor[0], t => { HitEnemy(t, 8, false, "stinger"); Apply(t, St.Poison); }); }
                if (P("ember") is var em && em != null)
                {
                    if (em.B2) { if (basicHits % 3 == 0) Apply(e, St.Burn); }
                    else { AddHeat(1); if (heat >= 3) Apply(e, St.Burn); }
                }
                if (sparkLeftHits > 0 && P("sparkplug") is var sp && sp != null)
                {
                    sparkLeftHits--; Apply(e, St.Burn);
                    if (StCount(e) > 1) { if (HasSt(e, St.Poison)) React(e, Re.Ignite, true); else if (HasSt(e, St.Shock)) React(e, Re.Overvolt, true); else if (HasSt(e, St.Corrode)) React(e, Re.Melt, true); else if (HasSt(e, St.Mark)) React(e, Re.AimBurn, true); }
                }
                if (riposteLeft > 0)
                {
                    riposteLeft--; riposteHits++; e.Flinch(.3f);
                    var rp = P("riposte"); if (rp != null && rp.Lv3) Apply(e, St.Mark);
                    if (riposteLeft == 0 && rp != null && rp.B2 && riposteHits >= 3) Blast(ch.root.position, 4, 25 * Skills.Power(rp), null, "riposte", CYAN);
                }
                if (P("magnet") is var mg && mg != null && mg.B1 && scrapStacks >= 10 && realTime < scrapStackUntil) Shoot(ch.root.position + Vector3.up, ch.Forward, 22, 7, GOLD, t => HitEnemy(t, 6, false, "magnet"), true);
                if (P("aimlens") != null && s.hitsOnMe >= 2 && realTime - s.firstHitAt >= (P("aimlens").Lv3 ? 2.5f : 3)) Apply(e, St.Mark);
                if (Big(e) && P("weakspot") is var wk && wk != null && s.hitsOnMe == 3) { Cue("균열!", .6f); vfx.Sparks(e.Center(), 14, .8f, GOLD); if (wk.B1) ShaveCooldowns(.3f); }
                if (P("vibroblade") is var vb && vb != null && vb.B2 && s.hitsOnMe % 5 == 0) { s.armourGoneUntil = realTime + 4; Blast(e.Center(), 2, 10, null, "vibroblade", WHITE, true); }
                if (P("afterglow") is var ag && ag != null && glowMotes >= 5 && !ag.B2) { glowMotes = 0; Shoot(ch.root.position + Vector3.up, ch.Forward, 20, 10, CYAN, t => { HitEnemy(t, 25, true, "afterglow"); if (ag.B1) t.Flinch(1); }, true); }
            }
        }
        int basicHits, wireShocks; float weldBladeUntil;
        /// the A combo's third hit landed (3타 완주)
        void OnComboFinish()
        {
            var cb = P("chainblade"); if (cb != null) chainLinks = Mathf.Min(cb.Lv3 ? 4 : 3, chainLinks + 1);
        }

        // ---------- active bonus (the damage of an active part when it hits) ----------
        float ActiveBonus(Skills.Owned o, bool consume)
        {
            float k = 1;
            if (realTime < overchargeFor.at && overchargeFor.id == o.def.id) k *= 1.3f;
            if (chainLinks > 0 && P("chainblade") is var cb && cb != null) k *= cb.B1 ? 1 : 1 + .2f;
            if (wireCharged) k *= 1.3f;
            if (recentKillsUntil > realTime) k *= 1 + .05f * recentKills;
            if (overclockedFor == o.def.id && P("battery") is var bt && bt != null && bt.B2) k *= 1.3f;
            if (P("relay") is var rl && rl != null && o.def.slot == Slot.Head) k *= 1;
            return k;
        }
        (string id, float at) overchargeFor; string overclockedFor;

        /// an active part was cast (from CastSkill)
        void OnActiveCast(Skills.Owned o)
        {
            castLog.Add((o.def.id, realTime)); castLog.RemoveAll(c => realTime - c.at > 5);
            // 구형 코어 버스: two weapon actives within 2 s -> the second one is overcharged; ① three -> blade overcharge 4 s
            var cbus = P("corebus");
            if (cbus != null && o.def.slot == Slot.Weapon)
            {
                int w = 0; foreach (var c in castLog) if (realTime - c.at <= 2 && Skills.Def(c.id)?.slot == Slot.Weapon) w++;
                if (w >= 2) { overchargeFor = (o.def.id, realTime + 1.5f); if (cbus.Lv3) o.ready -= 1; }
                if (w >= 3 && cbus.B1) { chargedBlade = realTime + 4; Cue("칼날 과충전", .8f); }
            }
            // 연쇄 칼날: spend links
            var chb = P("chainblade");
            if (chb != null && chainLinks > 0)
            {
                if (chb.B1 && chainLinks >= 3) { chainLinks = 0; After(.35f, () => { anim.speedScale = anim.speedScale; RepeatActive(o); }); }
                else chainLinks--;
                foreach (var n in Skills.Neighbours(chb)) if (n == o) o.ready -= chb.B1 || chb.B2 ? 1 : .5f;
            }
            if (wireCharged) wireCharged = false;
            var sp = P("sparkplug"); if (sp != null) sparkLeftHits = sp.B1 ? 2 : 1;
            // 전투 연산: three different skills inside 5 s
            var cc = P("combatcalc");
            if (cc != null)
            {
                var ids = new HashSet<string>(); foreach (var c in castLog) ids.Add(c.id);
                if (ids.Count >= (cc.Lv3 ? 2 : 3) && calcUntil < realTime && calcPredict < realTime)
                {
                    castLog.Clear();
                    if (cc.B2) { calcPredict = realTime + 3; Cue("예측", .8f); }
                    else { calcUntil = realTime + 3; SlowWorld(3, .5f); Cue("연산 가속", .8f); After(3, () => AddHeat(4)); }
                }
            }
            if (recentKillsUntil > realTime && o.def.id != "resonance") { recentKills = 0; recentKillsUntil = 0; }
            if (P("protocore") is var pc && pc != null && Skills.Amp(o) > 0) { if (++proteCharge >= 5) { proteCharge = 0; Cue("공명", .7f); if (pc.B1) SpendHeat(); foreach (var a in Skills.owned) if (a.On && a.Active && a.ready > realTime) a.ready -= (a.ready - realTime) * .5f; } }
            if (overclockedFor != null && overclockedFor != o.def.id) overclockedFor = null;
        }

        // ---------- kills ----------
        void PartsOnKill(Enemy e)
        {
            bool poisoned = HasSt(e, St.Poison) || (lastStatusDeath.TryGetValue(e, out var pd) && pd);
            bool shocked = HasSt(e, St.Shock), marked = HasSt(e, St.Mark), corroded = HasSt(e, St.Corrode);
            var c = e.Center();
            killTimes.Add(realTime); killTimes.RemoveAll(t => realTime - t > 10);
            recentKills = killTimes.Count; recentKillsUntil = realTime + 10;
            if (marked && poisoned) React(e, Re.Contagion, true);
            if (shocked && P("coilglove") is var cg && cg != null)
            {
                int bounces = cg.B1 ? 4 : 1; Enemy from = e; var hit = new HashSet<Enemy> { e };
                for (int b = 0; b < bounces; b++)
                {
                    Enemy best = null; float bd = 3 * Skills.Power(cg);
                    foreach (var o in enemies.list) if (o.Hittable && !hit.Contains(o)) { float d = Vector3.Distance(o.pos, from.pos); if (d < bd) { bd = d; best = o; } }
                    if (best == null) break;
                    vfx.Streak(from.Center(), best.Center(), StColor[2], 12); Apply(best, St.Shock); hit.Add(best); from = best;
                    if (cg.Lv3) AttackSpeedBuff(.05f * hit.Count, 1);
                    if (cg.B2) GiveShield(shieldHp + player.max * .03f, 4);
                }
            }
            if (shocked && P("decoder") is var dc && dc != null && dc.B1) ShaveCooldowns(.3f);
            if (shocked && P("wirespool") is var ws && ws != null && ws.B2) wireCharged = true;
            if (marked && P("aimlens") is var al && al != null && al.B1) foreach (var o in Near(c, 4)) if (o != e) { Apply(o, St.Mark); break; }
            if (marked && P("towerlens") is var tl && tl != null)
            {
                lockStacks++; if (tl.Lv3) foreach (var o in Skills.owned) if (o.On && o.Active && o.def.slot == Slot.Head && o.ready > realTime) o.ready -= 1;
                if (lockStacks >= 3) { lockStacks = 0; executeMark = 1; Cue("처형 표식 준비", .8f); }
            }
            if (marked && Skills.Get("mark") is var mk && mk != null && mk.B1) mk.ready = 0;
            if (poisoned && P("toxic") is var tx && tx != null && tx.B1) AddField("poison", e.pos, 1.8f, 4);
            if (poisoned && P("stinger") is var sg && sg != null && sg.B1 && statusOf.TryGetValue(e, out var ss) && ss.poisonStacks >= PoisonMax()) foreach (var o in Near(c, 3)) Apply(o, St.Poison, -1, false, 2);
            if (corroded && P("rustblade") is var rb && rb != null && rb.B1) { foreach (var o in Near(c, 3)) Apply(o, St.Corrode); vfx.Puff(c, StColor[3], 1.4f, .8f); }
            if (whiteHotLeft > 0 && P("furnace") is var fu && fu != null && fu.B1) whiteHotLeft = Mathf.Min(5, whiteHotLeft + 1);
            if (P("swarmnest") is var sn && sn != null && MinionCount("bug") < (sn.B1 ? 5 : 3)) { var m = Summon("bug", c, 6, StColor[3] * .6f, .18f); }
            if (P("afterglow") is var ag && ag != null) { player.hp = Mathf.Min(player.max, player.hp + player.max * .02f * Skills.Power(ag)); glowMotes = Mathf.Min(5, glowMotes + 1); vfx.Streak(c, ch.root.position + Vector3.up, CYAN, 10); }
            if (P("moss") is var ms && ms != null && ms.B1) player.hp = Mathf.Min(player.max, player.hp + player.max * .005f);
            if (calcUntil > realTime && P("combatcalc") is var cc && cc != null && cc.B1) { calcUntil += .5f; slowUntil += .5f; }
            if (!Organic(e) && P("scraparmor") != null) scrapPlates = Mathf.Min(5, scrapPlates + 1);
            if (e.state == "dead" && resonanceStunned.Contains(e) && Skills.Get("resonance") is var rs && rs != null && rs.B2 && !Organic(e)) GainXp(Growth.KillXp(e.def) / 2);
            if (P("nightvision") is var nv && nv != null && nv.B1 && InDark()) stealthUntil = realTime + 1;
            PartsEliteKilled(e);
            statusOf.Remove(e); lastStatusDeath.Remove(e); blindUntil.Remove(e);
        }
        readonly HashSet<Enemy> resonanceStunned = new HashSet<Enemy>();
        bool InDark() { int z = Regions.At(ch.root.position); return z == 2 || z == 7; }

        // ---------- perfect dodge / counter / dash / landing ----------
        void OnPerfectDodge(Enemy e)
        {
            var hz = P("hazard");
            if (hz != null && realTime >= hazardReady) { SlowWorld(hz.B1 ? .8f : .4f, .3f); hazardReady = realTime + (hz.B1 ? 10 : 6); if (hz.B2 && e != null) { Apply(e, St.Mark); counterWindowBoostUntil = realTime + 3; } }
            var ai = P("afterimage"); if (ai != null && ai.B1) { var m = Summon("echo", ch.root.position, 3, CYAN * .7f, .5f); }
            var gb = P("glassblade"); if (gb != null && gb.B2) { glassBrokenUntil = 0; dodgeCritNext = 1; }
            var wk = P("walker"); if (wk != null && player.hp < player.max * (wk.Lv3 ? .6f : .5f)) player.hp = Mathf.Min(player.max, player.hp + player.max * .02f);
            var cp = P("compound"); if (cp != null && e != null && IsRangedHit) { SpeedBuff(1 + .2f * Skills.Power(cp), .5f); dodgeBuffUntil = realTime + 2; if (cp.B2) compoundEyes = Mathf.Min(5, compoundEyes + 1); }
            Cue("완벽 회피", .5f);
        }
        bool IsRangedHit;
        public float counterWindowBoostUntil;
        void OnCounterSuccess(Enemy e)
        {
            SlowWorld(.3f, .25f);
            var co = Skills.Get("counter");
            if (co != null && co.B1) { co.ready = 0; counterChainUntil = realTime + 1.5f; }
            if (co != null && co.B2 && e != null) Blast(ch.root.position + Vector3.up, 3, 30 * Skills.DamageScale(co), null, "counter", CYAN, true, e);
            var rp = P("riposte"); if (rp != null) { riposteLeft = Skills.NextTo(rp, "counter") ? 5 : 3; riposteHits = 0; if (rp.B1) ready.Remove(KeyCode.Space); }
            var tc = P("torch");
            if (tc != null && e != null && HasSt(e, St.Burn)) { Blast(e.Center(), 3 * (tc.Lv3 ? 1.3f : 1), 26 * Skills.Power(tc), St.Burn, "torch", StColor[1]); if (tc.B1) AddField("spark", e.pos, 1.8f, 3); }
            if (tc != null && tc.B2) weldBladeUntil = realTime + 2;
            var ts = P("thornshell"); if (ts != null && ts.Lv3 && e != null) Apply(e, St.Poison, -1, false, ts.B1 ? 2 : 1);
            if (P("damper") != null) { }
        }
        float counterChainUntil;
        void OnDash()
        {
            var from = ch.root.position;
            if (realTime - lastDashAt < .8f) OnSecondDash(from);
            if (doubleDashReady < realTime && P("afterimage") is var ai && ai != null && ai.B2 && realTime - lastDashAt < .35f) { doubleDashReady = realTime + 5; After(.05f, () => Teleport(dashStart)); Cue("귀환", .5f); }
            lastDashAt = realTime; dashStart = from;
            if (P("ashcloak") is var ac && ac != null) { AddField("ash", from, 1.8f, 2); ashCrit = ac.Lv3; if (ac.B1) Summon("decoy", from, 2, new Color(.5f, .5f, .5f), .45f); if (ac.B2) { } }
            if (P("afterimage") is var af && af != null) After(1, () => { if (P("afterimage") == null) return; Blast(from + ch.Forward * 1.5f, 2.4f, 18 * .6f * Skills.Power(af), null, "afterimage", CYAN, false); });
            if (P("afterimage") is var af3 && af3 != null && af3.Lv3) AddField("shards", from, 1.2f, .5f);
            if (P("webfoot") is var wf && wf != null && wf.B2) AddField("poison", from, 1.2f, 3);
            if (P("gripclaw") is var gc && gc != null && WaterDepthAt(from) > 0) foreach (var e in Near(from, 2.5f)) e.Flinch(.4f);
            if (P("magcore") is var mc && mc != null) After(.34f, () => { foreach (var e in Near(ch.root.position, 4)) if (!Organic(e)) { Push(e, ch.root.position - e.pos, 1.2f); if (mc.B2) Apply(e, St.Corrode); } });
        }
        float WaterDepthAt(Vector3 p) => Regions.At(p) == 6 ? 1 : 0;   // the mire (Z6): sludge almost everywhere
        void OnSecondDash(Vector3 from)
        {
            var ft = P("fueltank"); if (ft == null) return;
            AddHeat(2);
            if (ft.Lv3) AddField("fire", from, 1.4f, 2);
            if (ft.B2 && heat >= 5) { Blast(from, 2.5f, 22, St.Burn, "fueltank", StColor[1]); anim.rootScale = 1.5f; }
        }
        /// extra dash charges (추가 연료통): charges refill one per dash cooldown
        int DashCharges => P("fueltank") is var ft && ft != null ? (ft.B1 ? 3 : 2) : 1;
        int dashUsed; float dashChargeAt;
        bool UseDashCharge()
        {
            if (DashCharges <= 1) return false;
            if (realTime - dashChargeAt > COOLDOWN[KeyCode.Space] * (P("fueltank").Lv3 ? 1 : 1.2f)) dashUsed = 0;
            if (dashUsed + 1 >= DashCharges) return false;
            dashUsed++; dashChargeAt = realTime; return true;
        }
        void OnLanded(float drop)
        {
            var gl = P("glidecloth"); if (gl != null && glideOn && gl.B1) Blast(ch.root.position, 2, 10, null, "glidecloth", WHITE);
            glideOn = false;
        }

        // ---------- getting hit ----------
        /// modifies an incoming hit; returns the damage that is left (0 = absorbed / dodged)
        float PartsTakeHit(float dmg, Vector3 from, Enemy enemy)
        {
            bool front = enemy != null && Vector3.Dot(Sim.Norm(Sim.FlatY(from - ch.root.position)), ch.Forward) > .3f;
            if (bulwarkUntil > realTime && front) { BulwarkBlock(enemy); return 0; }
            if (P("plate") is var pl && pl != null && front)
            {
                if (plateCracks == 0 && pl.B2) { var co = Skills.Get("counter"); if (co != null) co.ready -= 1; }
                dmg *= 1 - .20f * (1 - plateCracks / 3f) * Skills.Power(pl);
                if (plateCracks < 3) { plateCracks++; plateHealAt = realTime + 5; if (plateCracks == 3 && pl.B1) { Blast(ch.root.position + Vector3.up, 3, 20, null, "plate", WHITE); After(10, () => plateCracks = 0); } }
            }
            if (P("scraparmor") is var sa && sa != null)
            {
                dmg *= 1 - .03f * scrapPlates;
                if (enemy != null && HasSt(enemy, St.Corrode) && sa.Lv3) dmg *= .85f;
                if (!sa.B2) { if (scrapPlates >= 5) { scrapPlates = 0; Blast(ch.root.position + Vector3.up, 4, 18, St.Corrode, "scraparmor", StColor[3]); if (sa.B1) counterWindowBoostUntil = realTime + .3f; } else scrapPlates++; }
                else scrapPlates = Mathf.Min(5, scrapPlates + 1);
            }
            if (Still && P("rootnet") != null) dmg *= .85f;
            if (P("walker") is var wk && wk != null && player.hp < player.max * (wk.Lv3 ? .6f : .5f)) dmg *= 1 - .08f * Skills.Power(wk);
            if (IsRangedHit && Sprinting && P("lightframe") is var lf && lf != null && lf.B2) dmg *= .6f;
            if (afterglowGuard) dmg *= .85f;
            if (P("glassblade") is var gb && gb != null && realTime > glassBrokenUntil) { glassBrokenUntil = realTime + (gb.Lv3 ? 4 : 6); if (gb.B1) Blast(ch.root.position + Vector3.up, 4, 20, null, "glassblade", WHITE); Cue("칼날에 금이 갔다", .8f); }
            if (enemy != null && P("thornshell") is var ts && ts != null && Vector3.Distance(enemy.pos, ch.root.position) < 4)
            { Apply(enemy, St.Poison, -1, false, ts.B1 ? 2 : 1); if (ts.B2) foreach (var o in Near(ch.root.position, 3)) if (o != enemy) Apply(o, St.Poison); }
            if (P("moss") is var ms && ms != null && ms.B2) AddField("cloud", ch.root.position, 2, 2);
            // shields
            if (shieldHp > 0)
            {
                float a = Mathf.Min(shieldHp, dmg); shieldHp -= a; shieldStored += a; dmg -= a;
                vfx.Sparks(ch.root.position + Vector3.up, 8, .6f, CYAN);
                if (shieldHp <= 0) ShieldEnd(true);
            }
            // lethal saves: 응급 재부팅, 보행 보조기 ②
            if (dmg >= player.hp)
            {
                var rb = P("reboot");
                if (rb != null && rebootReady)
                {
                    rebootReady = false; player.hp = player.max * (rb.B1 || rb.B2 ? .2f : .1f) * Skills.Power(rb) / (1 + .1f * (rb.level - 1)); SlowWorld(2, .3f); face.Set("hurt", .2f); Cue("응급 재부팅", 1.4f);
                    if (rb.Lv3) foreach (var e in Near(ch.root.position, 4)) Push(e, e.pos - ch.root.position, 3);
                    if (rb.B1) Invuln(2); if (rb.B2) { rebootFastUntil = realTime + 10; }
                    return 0;
                }
                var wl = P("walker");
                if (wl != null && wl.B2 && realTime >= walkerReady) { walkerReady = realTime + 60; Invuln(1); player.hp = 1; Cue("버텼다", 1); return 0; }
            }
            // 꼬리 반사 ②: auto dash when HP falls below 30 %
            var tr = P("tailreflex");
            if (tr != null && tr.B2 && realTime >= tailReady && player.hp > player.max * .3f && player.hp - dmg <= player.max * .3f) { tailReady = realTime + 30; After(.02f, () => { ready.Remove(KeyCode.Space); Skill(KeyCode.Space); }); }
            var dm = P("damper"); if (dm != null) damperCharge = Mathf.Min(.5f, damperCharge + .15f);
            return dmg;
        }
        float plateHealAt, rebootFastUntil;
        bool afterglowGuard => P("afterglow") is var ag && ag != null && ag.B2 && glowMotes >= 5;
        /// the hit reaction after a hit got through: 충격 완충 관절 shortens it, ② dashes out at its end
        bool DamperSkipsStagger => P("damper") is var dm && dm != null && (dm.B1 && player.hp < player.max * .3f);

        // ---------- per frame ----------
        void PassiveTick(float dt)
        {
            if (plateCracks > 0 && P("plate") is var pl && pl != null && pl.Lv3 && realTime > plateHealAt) { plateCracks--; plateHealAt = realTime + 5; }
            // 공생 이끼: out of combat regen; 뿌리 신경망: rooted regen; 늪 물갈퀴: poison field regen
            if (player.alive && player.hp < player.max)
            {
                float rate = 0;
                if (P("moss") is var ms && ms != null && realTime - lastHitAt > (ms.Lv3 ? 4 : 5)) rate += .01f * Skills.Power(ms) * (ms.B1 || ms.B2 ? 2 : 1) * .5f;
                if (P("moss") is var ms2 && ms2 != null && InField(ch.root.position, "poison")) rate += .01f;
                if (Still && P("rootnet") is var rn && rn != null) rate += .01f * Skills.Power(rn);
                if (P("webfoot") is var wf && wf != null && InField(ch.root.position, "poison")) rate += .01f * Skills.Power(wf) * (wf.Lv3 ? 1 : 1);
                if (afterglowGuard) rate += 0;
                player.hp = Mathf.Min(player.max, player.hp + player.max * rate * dt);
            }
            if (P("webfoot") is var wf2 && wf2 != null && wf2.B1 && InField(ch.root.position, "poison")) webfootUntil = realTime + 2;
            if (Still && P("rootnet") is var rn2 && rn2 != null && rn2.B1) foreach (var e in Near(ch.root.position, 3)) if (UnityEngine.Random.value < dt * 2) e.Flinch(.15f);
            // 가벼운 프레임: a sprint primes the next first hit as a 4 m lunge
            lightframeReady = Sprinting && P("lightframe") != null;
            if (lightframeSlash && anim.name != "light1") lightframeSlash = false;
            if (realTime > scrapStackUntil) scrapStacks = 0;
            if (rebootFastUntil > realTime) foreach (var o in Skills.owned) if (o.On && o.Active && o.ready > realTime) o.ready -= dt * .5f;
            if (glassBrokenUntil > realTime && UnityEngine.Random.value < dt * 3) vfx.Sparks(ch.bladeTip.WorldPosition, 1, .3f, WHITE);
            // 날개막 천: hold jump in the air = glide (the fall slows)
            glideOn = P("glidecloth") != null && Falling && KeyBinds.Held(KeyBinds.Get(KeyBinds.Act.Jump));
            if (chargedBlade > realTime && UnityEngine.Random.value < dt * 10) vfx.Sparks(ch.bladeTip.WorldPosition, 1, .3f, WHITE);
        }
        bool lightframeReady, lightframeSlash;
        /// pod rest: one-per-rest parts recharge
        void PartsRest() { rebootReady = true; plateCracks = 0; heat = 0; overheatLeft = 0; }
        /// pickups of basic parts (GainScrap from pickups): 부품 자석 stacks
        void PartsScrapPicked(int n)
        {
            var mg = P("magnet"); if (mg == null) return;
            scrapStacks = Mathf.Min(10, scrapStacks + 1); scrapStackUntil = realTime + 4;
            AttackSpeedBuff(.03f * scrapStacks * Skills.Power(mg), 4);
        }

        // ---------- minions (drones, swarm bugs, decoys, echo clones) ----------
        void MinionStep(Minion m, float dt)
        {
            var me = ch.root.position;
            switch (m.kind)
            {
                case "drone":
                {
                    m.phase += dt * 2; var want = me + new Vector3(Mathf.Cos(m.phase) * 1.4f, 2.2f, Mathf.Sin(m.phase) * 1.4f);
                    m.p = Vector3.Lerp(m.p, want, Sim.Damp(6, dt));
                    if ((m.cd -= dt) > 0) break;
                    m.cd = .45f;
                    Enemy t = Marked(m.p, 12); if (t == null) foreach (var e in Near(me, 12)) { t = e; break; }
                    if (t == null) break;
                    vfx.Streak(m.p, t.Center(), CYAN, 6); HitEnemy(t, 6 * Skills.DamageScale(Skills.Get("drone")), false, "drone", false, false);
                    for (int i = 0; i < 5; i++) if (HasSt(t, (St)i) && i != 4) { Apply(t, (St)i); break; }
                    m.tally ??= new Dictionary<Enemy, int>(); m.tally[t] = m.tally.TryGetValue(t, out var n) ? n + 1 : 1;
                    break;
                }
                case "bug":
                {
                    if (m.target == null || !m.target.Hittable) { m.target = null; var sn = P("swarmnest"); foreach (var e in Near(m.p, 10)) { if (sn != null && sn.B2 && !HasSt(e, St.Mark)) continue; m.target = e; break; } }
                    if (m.target == null) { m.p = Vector3.Lerp(m.p, me + Vector3.up * 1.2f, Sim.Damp(3, dt)); break; }
                    var to = m.target.Center(); m.p = Vector3.MoveTowards(m.p, to, 7 * dt);
                    if (Vector3.Distance(m.p, to) < .8f && (m.cd -= dt) <= 0)
                    {
                        m.cd = .5f; HitEnemy(m.target, 3, false, "swarmnest", false, false);
                        if (!Organic(m.target)) Apply(m.target, St.Corrode);
                        var sn2 = P("swarmnest"); if (sn2 != null && sn2.Lv3) Apply(m.target, St.Poison);
                        if (sn2 != null && sn2.B2) { m.until = 0; for (int i = 0; i < 4; i++) if (HasSt(m.target, (St)i)) { Blast(to, 1.6f, 10, (St)i, "swarmnest", StColor[i]); break; } }
                    }
                    break;
                }
                case "decoy":
                    // draws nearby machines: they walk to it instead of me
                    foreach (var e in Near(m.p, 7)) if (!Organic(e)) e.roamTarget = m.p;
                    break;
                case "echo":
                    // 잔상 분신: copies the player's basic hits at 50 %
                    m.p = Vector3.Lerp(m.p, me - ch.Forward * 1.2f + Vector3.up * .9f, Sim.Damp(5, dt));
                    break;
                case "turret":
                    if ((m.cd -= dt) > 0) break;
                    m.cd = .6f; foreach (var e in Near(m.p, 9)) { vfx.Streak(m.p, e.Center(), StColor[1], 6); HitEnemy(e, 6, false, "turret", false, false); Apply(e, St.Burn); break; }
                    break;
            }
        }
        void MinionEnd(Minion m)
        {
            if (m.kind == "drone" && Skills.Get("drone") is var dr && dr != null && dr.B2 && m.tally != null)
            {
                Enemy best = null; int bn = 0; foreach (var kv in m.tally) if (kv.Key.Hittable && kv.Value > bn) { bn = kv.Value; best = kv.Key; }
                if (best != null) { vfx.Streak(m.p, best.Center(), CYAN, 10); Blast(best.Center(), 2.5f, 30, St.Mark, "drone", StColor[4]); }
            }
        }
        /// the echo clones repeat a basic hit at 50 %
        void EchoBasic(Enemy e, float dmg) { foreach (var m in minions) if (m.kind == "echo") { vfx.Streak(m.p, e.Center(), CYAN, 5); e.Damage(dmg * .5f, m.p); } }

        // ---------- shields ----------
        void ShieldEnd(bool broken)
        {
            var sh = Skills.Get("shield");
            if (shieldStored > 0 && sh != null) Blast(ch.root.position + Vector3.up, 3, shieldStored, null, "shield", CYAN);
            if (!broken && sh != null && sh.B2 && shieldHp > 0) player.hp = Mathf.Min(player.max, player.hp + shieldHp * .5f);
            shieldHp = 0; shieldUntil = 0; shieldStored = 0;
        }
    }
}
