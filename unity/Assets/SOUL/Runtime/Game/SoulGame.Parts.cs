using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 슬롯 파츠 공통 뼈대 (기획서/스킬_슬롯_카탈로그.md 7-2 · 7-6 ~ 7-9, 2026-11-10):
    //  - 상태이상 5종 (독 · 화상 · 감전 · 부식 · 표식) + 기계 / 유기체 상성, 상태 반응 8가지
    //  - 코어 열 (0 .. 10, 10 = 3 s 과열; 용광로 코어는 백열)
    //  - one damage pipeline for the player's hits (HitEnemy): status / passive multipliers, crits, triggers
    //  - ground fields (장판: 독 웅덩이 · 포자 구름 · 불길 · 산성 · 연막 · 증기 · 재 구름 · 감전 울타리 ...), projectiles,
    //    minions (드론 · 떼벌레 · 미끼), player shield, world slow (시간 감속), stealth
    //  - trigger hooks the game code calls: perfect dodge, counter, kill, dash, hit taken, active cast, pickup
    // The 30 actives live in SoulGame.PartsActive.cs, the 50 passives in SoulGame.PartsPassive.cs.
    public partial class SoulGame
    {
        public enum St { Poison, Burn, Shock, Corrode, Mark }
        static readonly string[] StName = { "독", "화상", "감전", "부식", "표식" };
        public static readonly Color[] StColor = { new Color(1.6f, .35f, 2.6f), new Color(3.2f, 1.2f, .25f), new Color(3f, 2.8f, .5f), new Color(1.1f, 2.0f, .4f), new Color(2.8f, .4f, 1.9f) };
        public static readonly Color CYAN = new Color(.5f, 1.8f, 3f), WHITE = new Color(2.6f, 2.8f, 3f), RED = new Color(3f, .5f, .4f), GOLD = new Color(3f, 2.2f, .6f);
        class Status
        {
            public readonly float[] until = new float[5];
            public int poisonStacks; public bool strongMark, strongCorrode;
            public float shockLock, frozenUntil, armourGoneUntil;
            public readonly float[] reactAt = new float[8];
            public int hitsOnMe;   // same-target hit counters (녹슨 칼날 · 진동 칼날 · 약점 투시 ...)
            public float firstHitAt = -1, lastHitAt = -9;
            public int scrapChips;   // 녹슨 칼날 Lv3: max 3 chips per enemy
        }
        readonly Dictionary<Enemy, Status> statusOf = new Dictionary<Enemy, Status>();
        Status SOf(Enemy e) { if (!statusOf.TryGetValue(e, out var s)) statusOf[e] = s = new Status(); return s; }
        /// organisms (기계 / 유기체 상성): everything else is a machine possessed by the red light
        static readonly HashSet<string> ORGANIC = new HashSet<string> { "swarmBug", "sporeVine", "shellHost", "mireMother", "mireTentacle", "frog", "ashRat", "glassBat", "sporeBear", "locust", "fly" };
        public static bool Organic(Enemy e) => e != null && ORGANIC.Contains(e.type);
        static bool Big(Enemy e) => e.def.eliteName != null || e.IsBoss;
        public bool HasSt(Enemy e, St s) => e != null && statusOf.TryGetValue(e, out var x) && x.until[(int)s] > realTime;
        public int StCount(Enemy e) { if (e == null || !statusOf.TryGetValue(e, out var x)) return 0; int n = 0; for (int i = 0; i < 5; i++) if (x.until[i] > realTime) n++; return n; }

        // ---------- 상태 부여 ----------
        public static readonly float[] ST_TIME = { 3, 2, 2, 5, 6 };
        /// apply a status (dur < 0 = default). Returns true when it was newly applied (not just refreshed).
        public bool Apply(Enemy e, St st, float dur = -1, bool strong = false, int stacks = 1)
        {
            if (e == null || !e.Hittable) return false;
            var s = SOf(e); int i = (int)st;
            if (dur < 0) dur = ST_TIME[i];
            dur *= StatusTimeScale(e, st);
            bool fresh = s.until[i] <= realTime;
            s.until[i] = Mathf.Max(s.until[i], realTime + dur);
            if (st == St.Poison) s.poisonStacks = Mathf.Clamp((fresh ? 0 : s.poisonStacks) + stacks, 1, PoisonMax());
            if (st == St.Mark && strong) s.strongMark = true; if (st == St.Mark && fresh && !strong) s.strongMark = false;
            if (st == St.Corrode && strong) s.strongCorrode = true;
            if (st == St.Shock) Paralyze(e, s, 1);
            if (fresh)
            {
                vfx.Sparks(e.Center(), 6, .5f, StColor[i]);
                OnStatusApplied(e, st);
                CheckReactions(e, st);
            }
            return fresh;
        }
        void Paralyze(Enemy e, Status s, float scale)
        {
            if (realTime < s.shockLock) return;
            bool neuro = HasSt(e, St.Poison);   // 신경 독: organisms too
            if (Organic(e) && !neuro) return;
            float t = (Big(e) ? .3f : Organic(e) ? .6f : 1f) * scale * ShockTimeScale(e);
            e.Flinch(t); if (Big(e)) s.shockLock = realTime + 8;
        }
        int PoisonMax() { int m = 1; var tx = Skills.On("toxic"); if (tx != null && tx.Lv3) m = 2; if (tx != null && tx.B2) m = 5; var sg = Skills.On("stinger"); if (sg != null) m = Mathf.Max(m, sg.B1 || sg.B2 ? 5 : 3); return m; }

        // ---------- 상태 반응 (7-7) ----------
        public enum Re { Ignite, Short, Neuro, Melt, Overvolt, Guide, Contagion, AimBurn }
        static readonly string[] ReName = { "인화", "합선", "신경 독", "용해", "과전압", "유도", "전염", "조준 점화" };
        void CheckReactions(Enemy e, St added)
        {
            bool P = HasSt(e, St.Poison), B = HasSt(e, St.Burn), S = HasSt(e, St.Shock), C = HasSt(e, St.Corrode), M = HasSt(e, St.Mark);
            if (P && B) React(e, Re.Ignite);
            if (S && C) React(e, Re.Short);
            if (S && P) React(e, Re.Neuro);
            if (B && C) React(e, Re.Melt);
            if (B && S) React(e, Re.Overvolt);
            if (M && S && added == St.Shock) React(e, Re.Guide);
            if (M && B) React(e, Re.AimBurn);
            // 전염 (표식 + 독) fires on the poisoned death: PartsOnKill
        }
        /// force a reaction (점화 플러그, 용접 토치); returns true if it fired (4 s lockout per enemy and reaction)
        public bool React(Enemy e, Re r, bool forced = false)
        {
            if (e == null || !e.Hittable) return false;
            var s = SOf(e); int i = (int)r; if (realTime < s.reactAt[i]) return false;
            s.reactAt[i] = realTime + 4;
            float k = ReactScale(r); var c = e.Center(); bool twice = false;
            switch (r)
            {
                case Re.Ignite:
                {
                    float left = Mathf.Max(0, s.until[0] - realTime) * PoisonDps(e, s) * 1.5f + (forced ? 18 : 0);
                    s.until[0] = 0; s.poisonStacks = 0;
                    Blast(c, 3 * ReactRange(), (12 + left) * k, St.Burn, "react", StColor[1]);
                    IgniteFields(c, 3);
                    break;
                }
                case Re.Short:
                    Paralyze(e, s, 2);
                    foreach (var o in Near(c, 3)) if (o != e && !Organic(o)) { vfx.Streak(c, o.Center(), StColor[2]); Apply(o, St.Shock); break; }
                    break;
                case Re.Neuro: Paralyze(e, s, 1); break;
                case Re.Melt: s.armourGoneUntil = realTime + (Big(e) ? 8 : 999); s.until[1] = realTime + 4; vfx.Sparks(c, 14, .8f, StColor[3]); break;
                case Re.Overvolt:
                    Blast(c, 2.2f * ReactRange(), 14 * k, null, "react", StColor[2]); Push(e, Sim.FlatY(e.pos - ch.root.position), 2.5f, true);
                    break;
                case Re.Guide:
                    foreach (var o in enemies.list) if (o != e && o.Hittable && HasSt(o, St.Mark) && Vector3.Distance(o.pos, e.pos) < 12) { vfx.Streak(c, o.Center(), StColor[2], 14); Apply(o, St.Shock); }
                    break;
                case Re.AimBurn: s.until[1] = realTime + Mathf.Max(0, s.until[1] - realTime) * 2; break;
                case Re.Contagion:
                    foreach (var o in Near(c, 4)) if (o != e) Apply(o, St.Poison, -1, false, Mathf.Max(1, s.poisonStacks));
                    vfx.Puff(c, StColor[0], 1.6f, .8f);
                    break;
            }
            var gn = Skills.On("ganglion");
            if (gn != null && !forced)
            {
                colony++; if (gn.B1) ShaveCooldowns(.5f);
                if (colony >= 5) { colony = 0; twice = true; }
            }
            Cue(ReName[i] + "!", .7f); vfx.Flash(c, .9f); cam.Shake(.04f);
            OnReaction(e, r);
            if (twice) { s.reactAt[i] = 0; After(.15f, () => { if (e.Hittable) { reactRange2 = true; React(e, r, true); reactRange2 = false; } }); }
            return true;
        }
        int colony; bool reactRange2;
        float ReactRange() => reactRange2 ? 2 : 1;
        float ReactScale(Re r) { var gn = Skills.On("ganglion"); if (gn != null && gn.B2) return r == pickedReaction ? 2 : .7f; return 1; }
        /// 군체 신경절 Lv5 ②: the reaction picked at a pod (cycled by the slot screen)
        public Re pickedReaction = Re.Ignite;

        // ---------- 상태 틱 ----------
        float PoisonDps(Enemy e, Status s) => 3 * Mathf.Max(1, s.poisonStacks) * (Organic(e) ? 1.5f : HasNeuro(e) ? 1 : .5f) * PoisonDamageScale(e);
        bool HasNeuro(Enemy e) => HasSt(e, St.Shock);
        void StatusTick(float dt)
        {
            if (statusOf.Count == 0) return;
            var dead = new List<Enemy>();
            foreach (var kv in statusOf)
            {
                var e = kv.Key; var s = kv.Value;
                if (!e.alive) { dead.Add(e); continue; }
                if (s.frozenUntil > realTime) { for (int i = 0; i < 5; i++) if (s.until[i] > realTime) s.until[i] += dt; }   // 공명 파동: statuses hold while stunned
                if (s.until[0] > realTime) { e.Poison(PoisonDps(e, s) * dt); if (UnityEngine.Random.value < dt * 4) vfx.Puff(e.Center(), StColor[0], .35f, .6f, Vector3.up * .5f); }
                else s.poisonStacks = 0;
                if (s.until[1] > realTime) { e.Poison(4 * (Organic(e) ? 1.5f : 1) * BurnDamageScale() * dt); if (UnityEngine.Random.value < dt * 6) vfx.Sparks(e.Center(), 1, .4f, StColor[1]); }
                if (s.until[2] > realTime && UnityEngine.Random.value < dt * 5) vfx.Sparks(e.Center() + Vector3.up * .3f, 1, .3f, StColor[2]);
                if (s.until[4] > realTime && UnityEngine.Random.value < dt * 2) vfx.Puff(e.Center() + Vector3.up * (e.height * .6f + .4f), StColor[4], .4f, .5f);
                if (!e.alive) lastStatusDeath[e] = s.until[0] > realTime;
            }
            foreach (var e in dead) statusOf.Remove(e);
        }
        readonly Dictionary<Enemy, bool> lastStatusDeath = new Dictionary<Enemy, bool>();

        /// damage multiplier of an enemy from its statuses (부식 · 표식 · 용해)
        float StatusTaken(Enemy e)
        {
            if (!statusOf.TryGetValue(e, out var s)) return 1;
            float k = 1;
            if (s.until[3] > realTime) k *= 1 + (Organic(e) ? .10f : .20f) + (s.strongCorrode ? AcidStrength() : 0);
            if (s.until[4] > realTime) k *= s.strongMark ? 1.2f : 1.1f;
            return k;
        }
        bool ArmourIgnored(Enemy e) => statusOf.TryGetValue(e, out var s) && (s.armourGoneUntil > realTime || (s.until[3] > realTime && !Organic(e)));

        // ---------- 코어 열 (7-8) ----------
        public float heat; float heatAt = -9, overheatLeft, whiteHotLeft;
        public bool Overheated => overheatLeft > 0;
        public bool WhiteHot => whiteHotLeft > 0;
        bool HeatVisible => heat > 0 || overheatLeft > 0 || whiteHotLeft > 0;
        public void AddHeat(float n)
        {
            if (n <= 0 || overheatLeft > 0) return;
            heat = Mathf.Min(10, heat + n); heatAt = realTime;
            if (heat >= 10)
            {
                var fu = Skills.On("furnace");
                if (fu != null && !fu.B2) { whiteHotLeft = 5; heat = 10; Cue("백열", 1); face.Set("wide", .4f); }
                else if (fu != null && fu.B2) { whiteHotLeft = 5; heat = 10; Cue("용융", 1); }
                else
                {
                    overheatLeft = 3; Cue("과열 · 스킬 봉인", 1.2f); face.Set("hurt", .5f);
                    var em = Skills.On("ember"); if (em != null && em.Lv3) Blast(ch.root.position + Vector3.up, 3, 30 * Skills.Power(em), St.Burn, "ember", StColor[1]);
                }
            }
        }
        public float SpendHeat() { float h = heat; heat = 0; return h; }
        void HeatTick(float dt)
        {
            if (overheatLeft > 0) { overheatLeft -= dt; if (overheatLeft <= 0) heat = 0; return; }
            if (whiteHotLeft > 0)
            {
                whiteHotLeft -= dt; var fu = Skills.On("furnace");
                if (fu != null && !fu.B2 && player.alive) player.hp = Mathf.Max(1, player.hp - player.max * .02f * dt);
                if (fu != null && fu.B2 && UnityEngine.Random.value < dt * 4) foreach (var e in Near(ch.root.position, 3)) Apply(e, St.Burn);
                if (whiteHotLeft <= 0) heat = 0;
                return;
            }
            if (realTime - heatAt > 2) heat = Mathf.Max(0, heat - dt);
        }

        // ---------- the player's hits ----------
        /// every player hit on an enemy goes through here: multipliers (statuses, passives, crits, ambush), the hit,
        /// then the triggers. src = move name or a part id; basic = the A combo (light1..3).
        public void HitEnemy(Enemy e, float dmg, bool heavy, string src, bool basic = false, bool canCrit = true)
        {
            if (e == null || !e.Hittable || dmg <= 0) return;
            bool ambush = Ambush(e);
            dmg *= StatusTaken(e) * PassiveDamageScale(e, src, basic, ambush);
            float crit = canCrit ? CritChance(e, src, basic, ambush) : 0;
            bool isCrit = crit > 0 && UnityEngine.Random.value < crit;
            if (isCrit) { dmg *= CritScale(e, src, basic, ambush); vfx.Flash(e.Center(), .7f); }
            var s = SOf(e); if (realTime - s.lastHitAt > 3) { s.hitsOnMe = 0; s.firstHitAt = realTime; } s.hitsOnMe++; s.lastHitAt = realTime;
            e.Damage(dmg, ch.root.position, heavy || isCrit);
            lastHitTarget = e;
            OnHitDealt(e, dmg, src, basic, isCrit, ambush);
        }
        Enemy lastHitTarget;
        /// 암습: the enemy can't see me (not engaged / blinded / I'm in smoke, ash, steam or stealth)
        bool Ambush(Enemy e) => !e.Engaged || blindUntil.TryGetValue(e, out var b) && b > realTime || stealthUntil > realTime || InField(ch.root.position, "smoke") || InField(ch.root.position, "ash") && !Organic(e);
        readonly Dictionary<Enemy, float> blindUntil = new Dictionary<Enemy, float>();
        float stealthUntil;
        /// generic area hit (blasts, shockwaves) with an optional status
        public void Blast(Vector3 c, float r, float dmg, St? st, string src, Color col, bool heavy = true, Enemy skip = null)
        {
            foreach (var e in Near(c, r)) { if (e == skip) continue; HitEnemy(e, dmg, heavy, src, false, false); if (st.HasValue) Apply(e, st.Value); }
            vfx.FloorRing(Sim.FlatY(c, ch.root.position.y), r / 3f, col); vfx.Sparks(c, 18, 1, col); vfx.Flash(c, .9f); cam.Shake(.05f);
        }
        public IEnumerable<Enemy> Near(Vector3 c, float r)
        {
            var l = new List<Enemy>();
            foreach (var e in enemies.list) if (e.Hittable && Mathf.Abs(e.pos.y - c.y) < 2.6f && Sim.FlatY(e.pos - c).magnitude < r + e.radius * .5f) l.Add(e);
            return l;
        }
        /// enemies in a forward cone from the player (angle = full width, radians)
        public List<Enemy> Cone(float r, float angle, Vector3? dir = null)
        {
            var l = new List<Enemy>(); var p = ch.root.position; var f = dir ?? ch.Forward;
            foreach (var e in enemies.list)
            {
                if (!e.Hittable || Mathf.Abs(e.pos.y - p.y) > 2.2f) continue;
                var d = Sim.FlatY(e.pos - p); float dist = d.magnitude - e.radius * .5f;
                if (dist > r) continue;
                if (dist > .6f && Vector3.Dot(d.normalized, f) < Mathf.Cos(angle / 2)) continue;
                l.Add(e);
            }
            return l;
        }
        /// enemies along a line from the player (width, length)
        public List<Enemy> Line(float width, float length, Vector3? from = null, Vector3? dir = null)
        {
            var l = new List<Enemy>(); var p = from ?? ch.root.position; var f = dir ?? ch.Forward;
            foreach (var e in enemies.list)
            {
                if (!e.Hittable || Mathf.Abs(e.pos.y - p.y) > 2.2f) continue;
                var d = Sim.FlatY(e.pos - p); float along = Vector3.Dot(d, f);
                if (along < -.5f || along > length + e.radius) continue;
                if ((d - f * along).magnitude > width / 2 + e.radius * .6f) continue;
                l.Add(e);
            }
            return l;
        }
        /// push (+) / pull (−) an enemy along dir; bosses and elites barely move; wall = 벽꽝 check
        public void Push(Enemy e, Vector3 dir, float dist, bool wallStun = false)
        {
            if (e == null || !e.Hittable || e.IsBoss) return;
            if (Big(e)) dist *= .25f;
            dir = Sim.Norm(Sim.FlatY(dir)); var q = e.pos + dir * dist; var want = q;
            WorldCollision.Resolve(ref q, e.radius); e.pos = q;
            if (wallStun && Sim.FlatY(want - q).magnitude > .4f) { e.Flinch(2); HitEnemy(e, 12, true, "wall", false, false); vfx.Sparks(e.Center(), 10, .8f, WHITE); Cue("벽꽝!", .6f); OnWallSlam(e); }
        }

        // ---------- ground fields (장판) ----------
        public class Field { public Vector3 p; public float r, until, tick; public string kind; public bool mine; public Vector3 vel; public Enemy follow; public bool followPlayer; }
        public readonly List<Field> fields = new List<Field>();
        static readonly Dictionary<string, Color> FieldColor = new Dictionary<string, Color>
        {
            { "poison", new Color(1.3f, .3f, 2.1f) }, { "cloud", new Color(1.1f, 1.6f, .5f) }, { "fire", new Color(3f, 1.1f, .2f) }, { "acid", new Color(1f, 2.2f, .3f) },
            { "smoke", new Color(.6f, .62f, .65f) }, { "steam", new Color(1.6f, 1.6f, 1.7f) }, { "ash", new Color(.55f, .52f, .5f) }, { "fence", new Color(3f, 2.8f, .5f) },
            { "spark", new Color(2.4f, 1.8f, .8f) }, { "neuro", new Color(2.2f, 2f, 1.4f) }, { "shards", new Color(2.2f, 2.6f, 3f) }, { "slick", new Color(.3f, .3f, .32f) },
        };
        public Field AddField(string kind, Vector3 p, float r, float time)
        {
            var f = new Field { kind = kind, p = p, r = r, until = realTime + time };
            fields.Add(f); vfx.FloorRing(p, r / 3f, FieldColor.TryGetValue(kind, out var c) ? c : WHITE);
            // fire / sparks touching clouds = explosion; electricity touching acid / smoke
            if (kind == "fire" || kind == "spark") IgniteFields(p, r);
            if (kind == "fence") ElectrifyFields(p, r);
            return f;
        }
        public bool InField(Vector3 p, string kind) { foreach (var f in fields) if (f.kind == kind && f.until > realTime && Sim.FlatY(p - f.p).magnitude < f.r) return true; return false; }
        /// fire reached clouds / smoke / ash / spores: they explode (장판 반응)
        public void IgniteFields(Vector3 p, float r)
        {
            foreach (var f in fields.ToArray())
            {
                if (f.until <= realTime || Sim.FlatY(f.p - p).magnitude > f.r + r) continue;
                if (f.kind == "cloud" || f.kind == "smoke" || f.kind == "ash" || f.kind == "poison")
                {
                    f.until = 0; float br = f.kind == "ash" ? 4 : f.r + 1;
                    Blast(f.p + Vector3.up * .6f, br, f.kind == "poison" ? 26 : 20, St.Burn, "fieldBlast", StColor[1]);
                    Cue(f.kind == "ash" ? "분진 폭발!" : "폭발!", .7f);
                }
            }
        }
        /// electricity reached acid / smoke / spore clouds: shock clouds, short-circuited puddles
        public void ElectrifyFields(Vector3 p, float r)
        {
            foreach (var f in fields.ToArray())
            {
                if (f.until <= realTime || Sim.FlatY(f.p - p).magnitude > f.r + r) continue;
                if (f.kind == "acid") { foreach (var e in Near(f.p, f.r)) { Apply(e, St.Shock); Apply(e, St.Corrode); } vfx.FloorRing(f.p, f.r / 3f, StColor[2]); Cue("합선!", .6f); }
                else if (f.kind == "smoke" || f.kind == "cloud") { f.kind = "neuro"; f.until = Mathf.Max(f.until, realTime + 3); }
            }
        }
        void FieldTick(float dt)
        {
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var f = fields[i];
                if (f.until <= realTime) { fields.RemoveAt(i); continue; }
                if (f.followPlayer) f.p = ch.root.position;
                else if (f.follow != null && f.follow.alive) f.p = f.follow.pos;
                f.p += f.vel * dt;
                var col = FieldColor.TryGetValue(f.kind, out var c) ? c : WHITE;
                if (UnityEngine.Random.value < dt * (6 + f.r * 3))
                {
                    float a = UnityEngine.Random.value * Mathf.PI * 2, rr = Mathf.Sqrt(UnityEngine.Random.value) * f.r;
                    var q = f.p + new Vector3(Mathf.Cos(a) * rr, .15f + UnityEngine.Random.value * .4f, Mathf.Sin(a) * rr);
                    if (f.kind == "fire" || f.kind == "spark" || f.kind == "fence") vfx.Sparks(q, 1, .35f, col); else vfx.Puff(q, col * (f.kind == "smoke" || f.kind == "ash" ? .5f : .8f), .5f + f.r * .12f, 1.1f, Vector3.up * .25f);
                }
                if ((f.tick -= dt) > 0) continue;
                f.tick = .5f;
                var inside = Near(f.p, f.r);
                switch (f.kind)
                {
                    case "poison": foreach (var e in inside) Apply(e, St.Poison); break;
                    case "cloud": foreach (var e in inside) { Apply(e, St.Poison); Confuse(e, Skills.On("spore") is var sp && sp != null && sp.B1 ? .25f : .10f); } break;
                    case "neuro": foreach (var e in inside) { Apply(e, St.Poison); Apply(e, St.Shock); } break;
                    case "fire": foreach (var e in inside) { Apply(e, St.Burn); HitEnemy(e, 4, false, "field", false, false); } break;
                    case "spark": foreach (var e in inside) Apply(e, St.Burn); break;
                    case "acid": foreach (var e in inside) Apply(e, St.Corrode, -1, true); break;
                    case "steam": foreach (var e in inside) { Apply(e, St.Burn); HitEnemy(e, 3, false, "field", false, false); } break;
                    case "fence": foreach (var e in inside) Apply(e, St.Shock); break;
                    case "shards": foreach (var e in inside) HitEnemy(e, 6, false, "field", false, false); break;
                    case "ash": foreach (var e in inside) if (!Organic(e)) e.ReturnHome(); break;
                }
            }
        }
        /// enemies inside a spore cloud may swing at each other (헷갈림)
        void Confuse(Enemy e, float chance)
        {
            if (UnityEngine.Random.value > chance * .5f) return;
            foreach (var o in Near(e.pos, 3)) if (o != e) { o.Damage(8, e.pos); vfx.Sparks(o.Center(), 6, .6f, StColor[0]); return; }
        }

        // ---------- projectiles ----------
        public class Proj { public Vector3 p, dir; public float speed, left, r = .9f; public Action<Enemy> hit; public HashSet<Enemy> done = new HashSet<Enemy>(); public bool pierce; public Color col; public Enemy homing; public Action<Vector3> end; }
        readonly List<Proj> projs = new List<Proj>();
        public Proj Shoot(Vector3 from, Vector3 dir, float speed, float range, Color col, Action<Enemy> hit, bool pierce = false)
        {
            var pj = new Proj { p = from, dir = Sim.Norm(Sim.FlatY(dir)), speed = speed, left = range, col = col, hit = hit, pierce = pierce };
            var mk = Skills.On("mark"); if (mk != null && mk.B2) pj.homing = Marked(from, 14);
            projs.Add(pj); return pj;
        }
        Enemy Marked(Vector3 from, float r) { Enemy best = null; float bd = r; foreach (var e in enemies.list) if (e.Hittable && HasSt(e, St.Mark)) { float d = Vector3.Distance(e.pos, from); if (d < bd) { bd = d; best = e; } } return best; }
        void ProjTick(float dt)
        {
            for (int i = projs.Count - 1; i >= 0; i--)
            {
                var pj = projs[i];
                if (pj.homing != null && pj.homing.Hittable) pj.dir = Vector3.RotateTowards(pj.dir, Sim.Norm(Sim.FlatY(pj.homing.pos - pj.p)), dt * 6, 0);
                float step = pj.speed * dt; pj.p += pj.dir * step; pj.left -= step;
                vfx.Puff(pj.p, pj.col, .35f, .2f);
                bool stop = pj.left <= 0;
                foreach (var e in enemies.list)
                {
                    if (stop) break;
                    if (!e.Hittable || pj.done.Contains(e) || Mathf.Abs(e.pos.y - pj.p.y) > 2.2f || Sim.FlatY(e.pos - pj.p).magnitude > pj.r + e.radius * .5f) continue;
                    pj.done.Add(e); pj.hit?.Invoke(e); if (!pj.pierce) stop = true;
                }
                if (stop) { pj.end?.Invoke(pj.p); projs.RemoveAt(i); }
            }
        }

        // ---------- minions (드론 · 떼벌레 · 미끼 · 포탑 · 잔상) ----------
        public class Minion { public string kind; public Vector3 p; public float until, cd, phase; public GameObject go; public Enemy target; public int hits; public Dictionary<Enemy, int> tally; }
        readonly List<Minion> minions = new List<Minion>();
        public Minion Summon(string kind, Vector3 p, float time, Color col, float size = .35f)
        {
            var m = new Minion { kind = kind, p = p, until = realTime + time, phase = UnityEngine.Random.value * 6 };
            m.go = GameObject.CreatePrimitive(kind == "decoy" ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(m.go.GetComponent<Collider>());
            m.go.transform.SetParent(scene.transientFx, false); m.go.transform.localScale = Vector3.one * size * (kind == "decoy" ? 2.4f : 1);
            var mr = m.go.GetComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Unlit(col, Blend.Add, false, UnityEngine.Rendering.CullMode.Back);
            minions.Add(m); return m;
        }
        public int MinionCount(string kind) { int n = 0; foreach (var m in minions) if (m.kind == kind) n++; return n; }
        void MinionTick(float dt)
        {
            for (int i = minions.Count - 1; i >= 0; i--)
            {
                var m = minions[i];
                if (m.until <= realTime) { MinionEnd(m); UnityEngine.Object.Destroy(m.go); minions.RemoveAt(i); continue; }
                MinionStep(m, dt);
                if (m.go) m.go.transform.position = Sim.ToUnity(m.p);
            }
        }

        // ---------- player-side states ----------
        public float shieldHp, shieldUntil, shieldStored; float invulnUntil, slowUntil, slowK = 1;
        /// world slow (시간 감속): enemies update at k × time while the player keeps full speed
        public void SlowWorld(float seconds, float k = .35f) { slowUntil = Mathf.Max(slowUntil, realTime + seconds); slowK = k; }
        public float WorldSlow => realTime < slowUntil ? slowK : 1;
        public void GiveShield(float hp, float time) { shieldHp = Mathf.Max(shieldHp, hp); shieldUntil = realTime + time; shieldStored = 0; }
        public void Invuln(float t) { invulnUntil = Mathf.Max(invulnUntil, realTime + t); }
        void ShaveCooldowns(float sec) { foreach (var o in Skills.owned) if (o.On && o.Active && o.ready > realTime) o.ready -= sec; }
        void ResetCooldown(string id) { var o = Skills.Get(id); if (o != null) o.ready = 0; }

        // ---------- per-frame ----------
        Vector3 partsLastPos; float stillFor, sprintFor, airTopY = float.NaN; public float fallStrikeUntil, fallStrikeHeight;
        void PartsUpdate(float dt)
        {
            if (dt <= 0) return;
            HeatTick(dt); StatusTick(dt); FieldTick(dt); ProjTick(dt); MinionTick(dt);
            if (shieldUntil > 0 && realTime > shieldUntil) ShieldEnd(false);
            // movement states: 질주 (3 s moving), 정지 (0.8 s still), 낙하 (track the top of a fall)
            var p = ch.root.position; float moved = Sim.FlatY(p - partsLastPos).magnitude / dt; partsLastPos = p;
            if (moved > 1.2f && !anim.busy) { sprintFor += dt; stillFor = 0; } else if (moved < .1f) { stillFor += dt; sprintFor = 0; } else { stillFor = 0; if (anim.busy) sprintFor = Mathf.Max(0, sprintFor - dt); }
            bool air = Falling || anim.airborne || RopeTravel;
            if (air) airTopY = float.IsNaN(airTopY) ? p.y : Mathf.Max(airTopY, p.y);
            else if (!float.IsNaN(airTopY)) { float drop = airTopY - p.y; airTopY = float.NaN; if (drop > 1.5f) { fallStrikeUntil = realTime + 2; fallStrikeHeight = drop; OnLanded(drop); } }
            PassiveTick(dt);
        }
        public bool Sprinting => sprintFor > 3;
        public bool Still => stillFor > .8f;

        void PartsGUI(float W, float H)
        {
            if (!HeatVisible && shieldHp <= 0) return;
            float x = 20, y = H - 118;
            if (HeatVisible)
            {
                GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(new Rect(x, y, 204, 12), Texture2D.whiteTexture);
                var c = whiteHotLeft > 0 ? new Color(1, .95f, .8f) : overheatLeft > 0 ? new Color(1, .25f, .2f) : Color.Lerp(new Color(1, .75f, .3f), new Color(1, .35f, .15f), heat / 10);
                GUI.color = c; GUI.DrawTexture(new Rect(x + 2, y + 2, 200 * (overheatLeft > 0 ? overheatLeft / 3 : whiteHotLeft > 0 ? whiteHotLeft / 5 : heat / 10), 8), Texture2D.whiteTexture);
                GUI.Label(new Rect(x + 210, y - 4, 160, 20), overheatLeft > 0 ? "과열" : whiteHotLeft > 0 ? "백열" : $"코어 열 {heat:F0}", small);
                y -= 16;
            }
            if (shieldHp > 0) { GUI.color = new Color(.5f, .85f, 1); GUI.Label(new Rect(x, y - 4, 200, 20), $"보호막 {shieldHp:F0}", small); }
            GUI.color = Color.white;
        }
    }
}
