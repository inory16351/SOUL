using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 액티브 30개 (기획서/스킬_슬롯_카탈로그.md 8절). The four original skills (회오리 · 레이저 파동 · 반격 · 칼날 폭풍) keep
    // their old cast path (SoulGame.Skill + legacy key) and get their new effects through the hooks; the other 26 play
    // their web move (moves.js SLOT_MOVES, same name as the part id) and act on the move's fx events (PartsFx).
    // Part moves don't use the generic blade / area hit test: every hit here is explicit (HitEnemy), so the damage
    // and the statuses stay exactly what the catalogue says.
    public partial class SoulGame
    {
        Skills.Owned casting;   // the part whose move is playing
        static readonly HashSet<string> PART_MOVES = new HashSet<string> { "harpoon", "heatcut", "crusher", "sawblade", "thornwhip", "net", "bulwark", "shockpalm", "mine", "acid", "suck", "spiderfoot", "tremor", "piston", "pounce", "slam", "smoke", "shield", "discharge", "vent", "spore", "resonance", "flash", "screech", "mark", "drone" };
        static bool PartMove(string n) => PART_MOVES.Contains(n);
        /// legacy move name -> part id (damage scale, triggers)
        static string PartOfMove(string n) => n == "heavy" || n == "heavyCharge" ? "whirl" : n == "flurry" ? "storm" : n == "wave" ? "wave" : n == "counter" ? "counter" : PartMove(n) ? n : null;

        float Dmg(Skills.Owned o, float b) => b * Skills.DamageScale(o);

        /// cast an active slot part (CastSkill). false = not cast (busy, overheated ...)
        bool CastPart(Skills.Owned o)
        {
            if (!CanAct()) return false;
            moveOrder = moveTarget; moveTarget = null;
            string id = o.def.id;
            if (id == "pounce") { var t = AimedEnemy(9); if (t != null) { var d = Sim.FlatY(t.pos - ch.root.position); anim.pendingYaw = Mathf.Atan2(d.x, d.z); pounceTarget = t; } else FaceCursor(); }
            else FaceCursor();
            if (!PlayMove(o.def.move)) return false;
            casting = o; partHit.Clear();
            if (id == "pounce" && pounceTarget != null) anim.rootScale = Mathf.Clamp(Sim.FlatY(pounceTarget.pos - ch.root.position).magnitude / 5f, .3f, 1.6f);
            if (id == "piston" && o.Lv3) anim.rootScale = 1.4f;
            if (id == "piston" && o.B1) Invuln(.45f);
            if (id == "heatcut") { heatDrag.Clear(); heatTrailT = 0; }
            if (id == "pounce" && o.B1) Invuln(.6f);
            return true;
        }
        Enemy pounceTarget; readonly HashSet<Enemy> partHit = new HashSet<Enemy>(); readonly List<Enemy> heatDrag = new List<Enemy>(); float heatTrailT;
        Enemy AimedEnemy(float range)
        {
            var p = testAim ?? cam.Pick(Input.mousePosition, ch.root.position.y) ?? ch.root.position + ch.Forward * 4;
            Enemy best = null; float bd = 3.5f;
            foreach (var e in enemies.list) { if (!e.Hittable || Vector3.Distance(e.pos, ch.root.position) > range) continue; float d = Sim.FlatY(e.pos - p).magnitude; if (d < bd) { bd = d; best = e; } }
            return best;
        }

        // ---------- move fx events ----------
        void PartsFx(string kind, string move)
        {
            var o = casting != null && casting.def.move == move ? casting : Skills.Get(PartOfMove(move) ?? "");
            var p = ch.root.position; var f = ch.Forward; var up = Vector3.up;
            switch (kind)
            {
                case "wave": WaveExtra(); break;
                // ---- 무기 ----
                case "harpoonOut":
                {
                    var hits = Line(1.4f, 8); hits.Sort((a, b) => Vector3.Distance(a.pos, p).CompareTo(Vector3.Distance(b.pos, p)));
                    int n = o.B1 ? 3 : 1; bool chipped = false;
                    vfx.Streak(p + up, p + up + f * 8, WHITE, 16);
                    for (int i = 0; i < hits.Count && i < n; i++)
                    {
                        var e = hits[i]; partHit.Add(e); HitEnemy(e, Dmg(o, 20), true, "harpoon");
                        if (!Organic(e) && !chipped) { chipped = true; GainScrap(2); vfx.Sparks(e.Center(), 8, .6f, GOLD); }
                        if (o.Lv3 || o.B1) { if (Big(e) && !o.B1) Teleport(e.pos - Sim.Norm(Sim.FlatY(e.pos - p)) * (e.radius + 1.2f)); else Push(e, p - e.pos, Mathf.Max(0, Vector3.Distance(e.pos, p) - 1.6f)); }
                        if (o.B2) for (int k = 0; k < 3; k++) { var ee = e; After(.4f * k, () => { if (ee.Hittable) { Apply(ee, St.Shock); vfx.Streak(ch.bladeTip.WorldPosition, ee.Center(), StColor[2], 8); } }); }
                    }
                    break;
                }
                case "harpoonIn": foreach (var e in partHit) if (e.Hittable) HitEnemy(e, Dmg(o, 15), false, "harpoon"); break;
                case "heatTrail": break;   // ActiveTick drops fire along the dash
                case "heatEnd":
                    if (o.B1) Blast(p + up + f, 3, Dmg(o, 25), St.Burn, "heatcut", StColor[1]);
                    if (o.B2) foreach (var e in heatDrag) Push(e, f, 3, true);
                    heatDrag.Clear();
                    break;
                case "grab":
                {
                    grabbed = null; foreach (var e in Cone(2.8f, 1.6f)) { grabbed = e; break; }
                    if (grabbed != null) { grabbed.Flinch(.6f); vfx.Sparks(grabbed.Center(), 8, .6f, WHITE); if (o.B2) bulwarkUntil = realTime + 2; }
                    break;
                }
                case "crush":
                {
                    var e = grabbed; grabbed = null; if (e == null || !e.Hittable) break;
                    SOf(e).armourGoneUntil = Mathf.Max(SOf(e).armourGoneUntil, realTime + .2f);
                    HitEnemy(e, Dmg(o, 40), true, "crusher");
                    if (!Organic(e)) { tornHits = 2; Cue("부위를 뜯었다", .6f); }
                    if (o.B1 && Big(e)) e.Flinch(1);
                    if (o.Lv3 && e.Hittable && !Big(e))
                    {
                        Enemy to = null; foreach (var x in Near(e.pos, 7)) if (x != e) { to = x; break; }
                        var dir = to != null ? to.pos - e.pos : f; Push(e, dir, 5, true);
                        if (to != null) { HitEnemy(to, Dmg(o, 20), true, "crusher"); vfx.Sparks(to.Center(), 12, .8f, WHITE); }
                    }
                    break;
                }
                case "sawRev": sawUntil = realTime + (o.B1 ? 5 : o.Lv3 ? 4 : 3); sawPart = o; Cue("톱날 모드", .6f); break;
                case "whip":
                {
                    var pulled = new List<Enemy>();
                    foreach (var e in Cone(6, 1.75f))
                    {
                        float k = (HasSt(e, St.Poison) || HasSt(e, St.Burn)) ? (o.B1 ? 1.5f : 1.3f) : 1;
                        HitEnemy(e, Dmg(o, 22) * k, false, "thornwhip"); Push(e, p - e.pos, 2.5f); pulled.Add(e);
                        if (o.Lv3) e.Flinch(.5f);
                        if (o.B2) { var ee = e; for (int i = 1; i <= 4; i++) After(i, () => { if (ee.Hittable) Apply(ee, St.Poison, -1, false, 1); }); }
                    }
                    for (int i = 0; i < pulled.Count; i++) for (int j = i + 1; j < pulled.Count; j++) if (Vector3.Distance(pulled[i].pos, pulled[j].pos) < 1.4f) { HitEnemy(pulled[i], 8, true, "thornwhip", false, false); HitEnemy(pulled[j], 8, true, "thornwhip", false, false); }
                    break;
                }
                case "whipPull": foreach (var e in Cone(4, 1.4f)) Push(e, p - e.pos, 1.2f); break;
                // ---- 팔 ----
                case "net":
                    Shoot(p + up, f, 22, 12, WHITE, e =>
                    {
                        float t = Big(e) ? .5f : o.Lv3 ? 3 : 2; e.Flinch(t);
                        foreach (var x in Near(e.pos, 1.6f)) if (x != e) x.Flinch(t * .8f);
                        if (o.B1) for (int k = 0; k < (int)(t / .7f); k++) After(.7f * k, () => { if (e.Hittable) Apply(e, St.Shock); });
                    }).end = q => { if (o.B2 && MinionCount("trap") < 2) Summon("trap", q, 30, WHITE * .5f, .3f); };
                    break;
                case "shield":   // 방패 전개
                    bulwarkUntil = realTime + 3; bulwarkStuck = 0; bulwarkPart = o;
                    After(3, () => BulwarkEnd(o));
                    break;
                case "shock":
                    foreach (var e in Cone(2.8f, 2.1f))
                    {
                        bool wasParalyzed = HasSt(e, St.Shock) && e.state == "stagger";
                        HitEnemy(e, Dmg(o, 24), true, "shockpalm"); Apply(e, St.Shock);
                        if (!Organic(e)) e.Flinch(o.Lv3 ? 2 : 1.5f);
                        if (wasParalyzed) { var ee = e; Cue("과부하", .6f); After(2, () => { if (ee.Hittable) Blast(ee.Center(), 2.5f, Dmg(o, 30), St.Shock, "shockpalm", StColor[2]); }); }
                        if (o.B1) foreach (var x in Near(e.pos, 4)) if (x != e) { vfx.Streak(e.Center(), x.Center(), StColor[2], 10); Apply(x, St.Shock); break; }
                        if (o.B2 && !Organic(e) && !Big(e)) { hacked[e] = realTime + 4; Cue("해킹", .6f); }
                    }
                    ElectrifyFields(p + f * 1.5f, 2);
                    break;
                case "mine":
                {
                    Enemy stick = null; foreach (var e in Cone(6, 1.2f)) { stick = e; break; }
                    if (stick != null) { var e = stick; vfx.Sparks(e.Center(), 6, .4f, StColor[1]); After(2, () => MineBoom(o, e.Hittable ? e.Center() : e.pos, e)); }
                    else
                    {
                        int max = o.Lv3 ? 3 : 2; var mines = minions.FindAll(m => m.kind == "mine"); if (mines.Count >= max) mines[0].until = 0;
                        var m = Summon("mine", p + f * 1.2f + up * .15f, 60, StColor[1] * .6f, .22f); m.target = null;
                    }
                    break;
                }
                case "acid":
                    for (int i = 0; i < 4; i++) After(.12f * i, () =>
                    {
                        foreach (var e in Cone(5, 1.25f))
                        {
                            Apply(e, St.Corrode, 4, true); HitEnemy(e, Dmg(o, 4), false, "acid", false, false);
                            if (o.B2) SOf(e).armourGoneUntil = Big(e) ? realTime + 15 : 1e9f;
                        }
                        vfx.Puff(ch.root.position + up + ch.Forward * 2.5f, StColor[3], 1.2f, .5f, ch.Forward * 4);
                    });
                    if (o.Lv3) After(.5f, () => AddField("acid", ch.root.position + ch.Forward * 3, 1.8f, 3));
                    break;
                case "suck":
                {
                    CastSuck();
                    foreach (var fl in fields) if (fl.until > realTime && (fl.kind == "cloud" || fl.kind == "smoke" || fl.kind == "poison" || fl.kind == "neuro") && Vector3.Distance(fl.p, p) < 8) { fl.until = 0; suckedCloud = fl.kind; vfx.Streak(fl.p + up, p + up, StColor[0], 10); }
                    if (o.Lv3 || o.B2) After(.5f, () =>
                    {
                        var got = Near(ch.root.position + ch.Forward * 2.2f, 2.6f); var all = new List<Enemy>(got);
                        foreach (var e in all) { if (o.Lv3) e.Flinch(1); if (o.B2) { HitEnemy(e, 8, true, "suck", false, false); for (int i = 0; i < 5; i++) foreach (var x in all) if (HasSt(x, (St)i)) { Apply(e, (St)i); break; } } }
                    });
                    if (o.B1) bulwarkUntil = Mathf.Max(bulwarkUntil, realTime + .6f);
                    break;
                }
                // ---- 다리 ----
                case "anchor":
                    anchoredUntil = realTime + (o.Lv3 ? 5.5f : 4); anchorPart = o;   // 공속 +30 % (AttackSpeed)
                    vfx.FloorRing(p, 1, StColor[2]);
                    if (o.B2) After(o.Lv3 ? 5.5f : 4, () => { if (realTime >= anchoredUntil - .1f) { Invuln(.5f); ready.Remove(KeyCode.Space); Skill(KeyCode.Space); } });
                    break;
                case "stomp": tremorUntil = realTime + 4; tremorPart = o; tremorT = 0; break;
                case "charge": break;   // ActiveTick sweeps the path
                case "pounce":
                {
                    float hgt = 1.4f; fallStrikeUntil = realTime + .2f; fallStrikeHeight = hgt;
                    foreach (var e in Near(p, 2.2f)) { HitEnemy(e, Dmg(o, 30) * (o.Lv3 ? 1.3f : 1), true, "pounce"); e.Flinch(1.2f); }
                    vfx.FloorRing(p, .8f, WHITE); cam.Shake(.07f);
                    if (o.B1 && pounceChain < 2) { pounceChain++; o.ready = 0; After(2, () => pounceChain = 0); }
                    if (o.B2) airJumped = false;
                    pounceTarget = null;
                    break;
                }
                case "slamWave":
                {
                    float r = 3 * (o.Lv3 ? 1.2f : 1);
                    foreach (var fl in fields.ToArray()) if (fl.until > realTime && Vector3.Distance(fl.p, p) < fl.r) { fl.r *= 2; fl.until = Mathf.Max(fl.until, realTime + 2); }
                    foreach (var e in Near(p, r)) { HitEnemy(e, Dmg(o, 30), true, "slam"); if (o.B1) e.Flinch(1); }
                    vfx.FloorRing(p, r / 3, CYAN);
                    if (o.B2) for (int i = 1; i <= 2; i++) { float rr = r + 1.5f * i; After(.25f * i, () => { foreach (var e in Near(ch.root.position, rr)) HitEnemy(e, Dmg(o, 15), false, "slam", false, false); vfx.FloorRing(ch.root.position, rr / 3, CYAN); }); }
                    break;
                }
                case "smoke":
                    AddField("smoke", p, 3, 3);
                    foreach (var e in Near(p, 9)) e.ReturnHome();
                    smokeCrit = o.B1; if (o.B2) Summon("decoy", p, 3, new Color(.5f, .5f, .55f), .45f);
                    break;
                // ---- 몸통 ----
                case "shieldUp": GiveShield(player.max * (o.Lv3 ? .25f : .2f), 6); shieldReflect = o.B1; vfx.FloorRing(p, 1, CYAN); break;
                case "discharge":
                {
                    float r = 4; if (heat >= 3) { SpendHeat(); r *= 2; }
                    Blast(p + up, r, Dmg(o, 28), St.Shock, "discharge", StColor[2]); ElectrifyFields(p, r);
                    if (o.Lv3) dischargeBasicUntil = realTime + 3;
                    if (o.B1) After(1, () => { Blast(ch.root.position + up, r, Dmg(o, 20), St.Shock, "discharge", StColor[2]); });
                    if (o.B2) staticUntil = realTime + 4;
                    break;
                }
                case "vent":
                {
                    float h = SpendHeat(); float r = 3 * (1 + .05f * h);
                    foreach (var e in Near(p, r)) { HitEnemy(e, Dmg(o, 20) * (1 + .15f * h), true, "vent"); Push(e, e.pos - p, 2.5f, true); if (o.Lv3) e.Flinch(1); }
                    var fl = AddField("steam", p, r * .8f, 3); vfx.FloorRing(p, r / 3, new Color(1.6f, 1.6f, 1.7f));
                    if (o.B2) stealthUntil = realTime + 2;
                    ventSafeUntil = o.B1 ? realTime + 3 : 0;
                    break;
                }
                case "spore": { var fl = AddField("cloud", p, 3, o.Lv3 ? 7 : 5); if (o.B2) fl.followPlayer = true; break; }
                case "resonance":
                {
                    float t = o.Lv3 ? 2 : 1.5f;
                    foreach (var e in Near(p, 6))
                    {
                        if (Big(e)) { if (o.B1) e.Flinch(1); } else e.Flinch(t);
                        SOf(e).frozenUntil = realTime + t; resonanceStunned.Add(e); HitEnemy(e, Dmg(o, 10), false, "resonance", false, false);
                    }
                    vfx.FloorRing(p, 2, new Color(.6f, 1.4f, 3f)); vfx.Ring(Sim.FlatY(p, p.y + 1), 2, true); cam.Shake(.08f);
                    After(t + .1f, () => resonanceStunned.Clear());
                    break;
                }
                // ---- 머리 ----
                case "flash":
                {
                    var list = o.B2 ? new List<Enemy>(Near(p, 6)) : Cone(6, 1.75f);
                    foreach (var e in list) { blindUntil[e] = realTime + (o.Lv3 ? 2 : 1.5f); vfx.Flash(e.Center() + up * .5f, .5f); }
                    vfx.Flash(p + up * 1.6f + f * .4f, 2.2f); face.Set("wide", .4f);
                    blindBrawl = o.B1;
                    break;
                }
                case "screech":
                {
                    float r = 5 * (o.Lv3 ? 1.2f : 1);
                    foreach (var e in Near(p, r))
                    {
                        float k = HasSt(e, St.Mark) && P("echolens") is var el && el != null && el.B1 ? 2 : 1;
                        HitEnemy(e, Dmg(o, 12) * k, false, "screech"); Push(e, e.pos - p, 3, true);
                        if (Organic(e)) { e.ReturnHome(); if (o.B1 && e.def.hp <= 30) e.Damage(999, p); }
                        else if (o.B2) e.Flinch(1.5f);
                    }
                    breakables.Hit(p + up, p + up + f * .1f, r, p);
                    for (int i = 0; i < 3; i++) { float rr = r * (i + 1) / 3f; After(.06f * i, () => vfx.Ring(Sim.FlatY(ch.root.position, ch.root.position.y + 1.4f), rr / 2, true)); }
                    break;
                }
                case "mark":
                {
                    int n = o.Lv3 ? 2 : 1; var t = AimedEnemy(14); var done = new List<Enemy>();
                    if (t != null) done.Add(t);
                    if (n > 1) foreach (var e in Near(p, 14)) { if (done.Count >= n) break; if (!done.Contains(e)) done.Add(e); }
                    foreach (var e in done)
                    {
                        Apply(e, St.Mark, 8, true); vfx.Streak(p + up * 1.6f, e.Center(), StColor[4], 10);
                        if (executeMark > 0) { executeMark = 0; var tl = P("towerlens"); float lim = Big(e) ? (tl != null && tl.B1 ? .10f : 0) : .25f; if (e.hpFrac <= lim) { e.Damage(99999, p, true); Cue("처형", .8f); } }
                    }
                    break;
                }
                case "drone":
                {
                    int n = o.B1 ? 2 : 1; for (int i = 0; i < n; i++) Summon("drone", p + up * 2, o.Lv3 ? 14 : 10, CYAN, .3f);
                    break;
                }
            }
        }
        Enemy grabbed; int tornHits, pounceChain; float sawUntil, anchoredUntil, tremorUntil, tremorT, dischargeBasicUntil, staticUntil, ventSafeUntil, bulwarkUntil; int bulwarkStuck;
        Skills.Owned sawPart, anchorPart, tremorPart, bulwarkPart; bool shieldReflect, blindBrawl; string suckedCloud;
        readonly Dictionary<Enemy, float> hacked = new Dictionary<Enemy, float>();

        void MineBoom(Skills.Owned o, Vector3 at, Enemy host)
        {
            var sts = new List<St>(); if (host != null) for (int i = 0; i < 5; i++) if (HasSt(host, (St)i)) sts.Add((St)i);
            Blast(at, 2.5f, Dmg(o, 30), null, "mine", StColor[1]);
            foreach (var e in Near(at, 2.5f)) { foreach (var s in sts) Apply(e, s); if (o.B1) e.Flinch(1); }
            if (o.B2) for (int i = 0; i < 3; i++) { float a = i * 2.1f; var q = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.8f; After(.3f + .1f * i, () => Blast(q, 1.4f, Dmg(o, 12), null, "mine", StColor[1])); }
        }
        void BulwarkBlock(Enemy e)
        {
            bulwarkStuck++; vfx.Sparks(ch.root.position + Vector3.up + ch.Forward * .6f, 10, .7f, WHITE); cam.Shake(.04f);
            if (bulwarkPart != null && bulwarkPart.Lv3 && e != null) HitEnemy(e, 10, false, "bulwark", false, false);
        }
        void BulwarkEnd(Skills.Owned o)
        {
            var p = ch.root.position;
            for (int i = 0; i < bulwarkStuck; i++) { float a = (i - (bulwarkStuck - 1) / 2f) * .15f; Shoot(p + Vector3.up, Sim.ApplyAxisAngle(ch.Forward, Vector3.up, a), 24, 12, WHITE, e => HitEnemy(e, Dmg(o, 12), false, "bulwark")); }
            if (o.B1) foreach (var e in Cone(3.5f, 1.8f)) { Push(e, e.pos - p, 3, true); e.Flinch(1); }
            if (o.B2) Summon("decoy", p + ch.Forward * 1.2f, 4, new Color(.5f, .55f, .6f), .5f);
            bulwarkStuck = 0;
        }
        /// 연쇄 칼날 ①: the active fires a second time at 50 %
        void RepeatActive(Skills.Owned o) { if (o.def.move != null && PartMove(o.def.move)) { dmgBuff = Mathf.Max(dmgBuff, -.5f); PartsFx(FirstFx(o.def.move), o.def.move); } }
        static string FirstFx(string move) { var m = MoveDB.moves.TryGetValue(move, out var mv) ? mv : null; return m?.fx != null && m.fx.Count > 0 ? m.fx[0].name : move; }

        /// laser wave extras: ① three-way split
        void WaveExtra()
        {
            var o = Skills.Get("wave"); if (o == null || !o.B1) return;
            foreach (var a in new[] { -.42f, .42f }) { var dir = Sim.ApplyAxisAngle(ch.Forward, Vector3.up, a); var w = vfx.SpawnWave(ch.root.position + new Vector3(0, .35f, 0) + dir * .6f, dir); w.scale = .8f; }
        }

        // ---------- per frame ----------
        void ActiveTick(float dt)
        {
            var p = ch.root.position; var m = anim.move;
            bool act = m?.active != null && anim.time >= m.active[0] && anim.time <= m.active[1];
            // dash-type parts: hit along the path once each
            if (act && casting != null && (anim.name == "heatcut" || anim.name == "piston"))
            {
                var o = casting;
                foreach (var e in Near(p, 1.7f))
                {
                    if (partHit.Contains(e)) continue; partHit.Add(e);
                    if (anim.name == "heatcut") { HitEnemy(e, Dmg(o, 18), true, "heatcut"); Apply(e, St.Burn); if (o.B2) heatDrag.Add(e); }
                    else { HitEnemy(e, Dmg(o, 20) * (1 + .2f * pistonBreaks), true, "piston"); Push(e, ch.Forward, 2, true); if (e.state == "stagger") pistonStun.Add(e); }
                }
                if (anim.name == "heatcut" && (heatTrailT -= dt) <= 0) { heatTrailT = .1f; AddField("fire", p, .9f, o.Lv3 ? 4 : 3); }
                if (anim.name == "heatcut" && o.B2) foreach (var e in heatDrag) if (e.Hittable) e.pos = Vector3.Lerp(e.pos, p + ch.Forward * 1.2f, Sim.Damp(12, dt));
                if (anim.name == "piston" && o.B2) { if (breakables.Hit(p + Vector3.up * .2f, p + Vector3.up * 1.2f + ch.Forward, 1.2f, p)) pistonBreaks++; }
            }
            if (anim.name != "piston") pistonBreaks = 0;
            if (casting != null && anim.name != casting.def.move) casting = null;
            // 회전 톱날: extra saw ticks on basic hits (OnHitDealt), heat, ① grinding contact
            if (sawUntil > realTime)
            {
                AddHeat(dt * (sawPart != null && sawPart.B1 ? 2 : 1));
                if (sawPart != null && sawPart.B1) foreach (var e in Near(p, 1.4f)) if (UnityEngine.Random.value < dt * 4) HitEnemy(e, 5, false, "sawblade", false, false);
                if (UnityEngine.Random.value < dt * 20) vfx.Sparks(ch.bladeTip.WorldPosition, 1, .4f, StColor[1]);
            }
            else if (sawPart != null) { var o = sawPart; sawPart = null; if (o.B2) { var dir = ch.Forward; var start = p + Vector3.up; var pj = Shoot(start, dir, 16, 8, StColor[1], e => HitEnemy(e, Dmg(o, 20), true, "sawblade"), true); pj.end = q => Shoot(q, start - q, 16, Vector3.Distance(q, start), StColor[1], e => HitEnemy(e, Dmg(o, 20), true, "sawblade"), true); } }
            // 진동 걸음: pulses every 0.5 s while walking
            if (tremorUntil > realTime && (tremorT -= dt) <= 0)
            {
                tremorT = .5f; float r = 3 * (tremorPart != null && tremorPart.Lv3 ? 1.3f : 1);
                foreach (var e in Near(p, r)) { HitEnemy(e, Dmg(tremorPart, 8), false, "tremor", false, false); e.Engage(); if (HasSt(e, St.Corrode)) Apply(e, St.Corrode); if (tremorPart != null && tremorPart.B1) e.Flinch(1); }
                vfx.FloorRing(p, r / 3, new Color(1.6f, 1.4f, 1.1f)); vfx.Dust(p, .8f); cam.Shake(.02f);
            }
            // 해킹: a hacked machine is frozen and fires at its friends
            if (hacked.Count > 0)
                foreach (var kv in new List<KeyValuePair<Enemy, float>>(hacked))
                {
                    var e = kv.Key; if (!e.Hittable || kv.Value < realTime) { hacked.Remove(e); continue; }
                    e.Flinch(.3f);
                    if (UnityEngine.Random.value < dt * 1.5f) foreach (var x in Near(e.pos, 8)) if (x != e) { vfx.Streak(e.Center(), x.Center(), StColor[2], 8); HitEnemy(x, 10, false, "hack", false, false); break; }
                }
            // 섬광 렌즈 ①: blinded enemies swing at each other
            if (blindBrawl && UnityEngine.Random.value < dt * 2)
                foreach (var kv in blindUntil) if (kv.Value > realTime && kv.Key.Hittable) { foreach (var x in Near(kv.Key.pos, 3)) if (x != kv.Key) { x.Damage(8, kv.Key.pos); vfx.Sparks(x.Center(), 5, .5f, WHITE); break; } break; }
            // traps and mines on the floor
            foreach (var mn in minions)
            {
                if (mn.kind != "trap" && mn.kind != "mine") continue;
                foreach (var e in Near(mn.p, 1.2f))
                {
                    if (mn.kind == "trap") { e.Flinch(Big(e) ? .5f : 2); vfx.Sparks(mn.p, 8, .5f, WHITE); }
                    else MineBoom(Skills.Get("mine"), mn.p, e);
                    mn.until = 0; break;
                }
            }
            if (anchoredUntil > realTime && UnityEngine.Random.value < dt * 6) vfx.Sparks(p + Vector3.up * .1f, 1, .3f, StColor[2]);
        }
        int pistonBreaks;
        /// area hit radius scale of the legacy moves (칼날 과충전, 거미 발 ① 포탑 모드, 회오리 / 칼날 폭풍 Lv3)
        float PartsAreaScale(string n)
        {
            float k = 1;
            if (n.StartsWith("light") && chargedBlade > realTime) k *= 1.4f;
            if ((n.StartsWith("light") || n == "heavy") && anchoredUntil > realTime && anchorPart != null && anchorPart.B1) k *= 2;
            if (n == "heavy" && Skills.Get("whirl") is var wh && wh != null && wh.Lv3) k *= 1.2f;
            if (n == "flurry" && Skills.Get("storm") is var st && st != null && st.Lv3) k *= 1.2f;
            return k;
        }
        /// legacy skill hit extras: 회오리 퍼뜨리기, 레이저 파동 표식 폭발 / 응축 감전, 톱날 추가 타격, 뜯은 부위
        void LegacyHitExtras(Enemy e, string src, bool basic)
        {
            if (src == "whirl") for (int i = 0; i < 5; i++) if (i != 4 && HasSt(e, (St)i)) foreach (var x in Near(e.pos, 3)) if (x != e) Apply(x, (St)i);
            if (src == "wave" && Skills.Get("wave") is var wv && wv != null && wv.B2) Apply(e, St.Shock);
            if (basic && sawUntil > realTime) { var ee = e; for (int i = 1; i <= 3; i++) After(.07f * i, () => { if (ee.Hittable) { ee.Damage(4, ch.root.position); vfx.Sparks(ee.Center(), 3, .4f, StColor[1]); } }); }
            if (basic && dischargeBasicUntil > realTime) Apply(e, St.Shock);
            if (basic && tornHits > 0) { tornHits--; e.Flinch(.5f); }
            if (basic && suckedCloud != null) { var k = suckedCloud; suckedCloud = null; Apply(e, St.Poison); if (k == "neuro") Apply(e, St.Shock); Blast(e.Center(), 1.6f, 8, St.Poison, "suck", StColor[0]); }
            if (basic) EchoBasic(e, 18);
        }
        /// damage multiplier on the legacy skills (wave: a mark pops for +30 %; ② charged double)
        float LegacyScale(Enemy e, string src)
        {
            float k = 1;
            if (src == "wave" && HasSt(e, St.Mark)) { k *= 1.3f; SOf(e).until[4] = 0; }
            if (src == "wave" && Skills.Get("wave") is var wv && wv != null && wv.B2) k *= 2;
            if (basicTornBonus && tornHits > 0) k *= 1.5f;
            return k;
        }
        const bool basicTornBonus = true;
    }
}
