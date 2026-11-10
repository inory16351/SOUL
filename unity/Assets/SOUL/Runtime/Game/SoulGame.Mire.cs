using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Z6 독성 늪 runtime (기획서/Z6_독성늪_보스_생태계.md, 2026-11-09). Tables: World/Territories.cs (AddMire, MirePools,
    // Caches), World/ZoneState.cs (areas, pods, the mother's arena), Combat/Enemies.cs (Z6 roster).
    //  hazards : thrown sludge balls (telegraph + arc) and poison puddles (spore vines, sludge crawlers, the shell host).
    //  껍질 숙주 (shellHost, elite): sunk in its pool; inflates a sac on its shell and throws a ball of sludge, three per set.
    //            Hit an inflating sac from the air / from range (or standing on the shell): it bursts, the throw is cancelled.
    //            Three bursts: 8 s stun (2x). After two sets it lifts its head to breathe for 4 s (normal damage).
    //            Otherwise its shell turns blows aside. Dead: the shell hardens into a platform; from it the double jump
    //            reaches the hidden ledge east of the pool, where its 독에 잠식된 코어 lies.
    //  거대 준설기 (giantDredger, elite) / 녹슨 준설기: the intake (suck) pulls you in. Swarm bugs caught in the suck jam
    //            it: stunned 5 s (2x). Drops the skill 흡입.
    //  독에 잠식된 코어 (body-slot passive skill "toxic"): every hit poisons for 3 s; the blade turns violet (LaserBlade.toxic).
    //  늪의 어미 (mireMother, boss): tide fight. Low tide: three tentacles rise (cut them); the mother is under the sludge.
    //            High tide: the square floods (sludge hurts), drowned roofs and floating egg shells are the dry ground;
    //            the mother surfaces, exposed, throwing sludge and breathing poison. All tentacles cut: the tide comes early.
    //            Organic: no grudge part (BossPurify skips it for zone 6).
    public partial class SoulGame
    {
        class Puddle { public Vector3 pos; public float r, until, dps; public GameObject go; }
        class Glob { public Telegraph tele; public Vector3 from, to; public float dmg, puddleR, puddleT; public int split; public Enemy src; public GameObject go; public bool breath; }
        readonly List<Puddle> puddles = new List<Puddle>();
        readonly List<Glob> globs = new List<Glob>();
        static Mesh mireDisc, mireBall, shellMesh, eggMesh, roofMesh;
        Material puddleMat, ballMat, sacMat, tideMat, shellMat;
        bool mireInit; float puddleCue, suckUntil; Vector3 suckTo;
        class HostRig { public Enemy e; public string phase = "wait"; public float t, stunUntil; public int sac, sets; public bool[] burst = new bool[3]; public GameObject[] sacs = new GameObject[3]; }
        class DredgeRig { public Enemy e; public float jamUntil; }
        readonly List<HostRig> hosts = new List<HostRig>();
        readonly List<DredgeRig> dredges = new List<DredgeRig>();
        GameObject shellGo; bool shellBuilt;
        // suck skill (흡입): enemies being dragged to a point in front of the player
        readonly List<Enemy> sucked = new List<Enemy>(); float suckSkillUntil; Vector3 suckSkillTo;

        void MireInit()
        {
            mireInit = true;
            puddleMat = Mats.Unlit(new Color(.5f, 1.25f, .2f, .55f), Blend.Add, false, UnityEngine.Rendering.CullMode.Off, false);
            ballMat = Mats.Unlit(new Color(.9f, 1.8f, .35f, 1), Blend.Opaque);
            sacMat = Mats.Unlit(new Color(1.2f, .4f, 2.2f, 1), Blend.Opaque);
            tideMat = Mats.Unlit(new Color(.16f, .28f, .08f, .78f), Blend.Alpha, false, UnityEngine.Rendering.CullMode.Off, false);
            shellMat = Mats.Lit(Color.white, Color.black, .05f, .15f, true);
            if (mireDisc == null) { var b = new MeshBuilder(611); b.Disc(Vector3.zero, 0, 1, 28, Color.white); mireDisc = b.Build("mire disc"); }
            if (mireBall == null) mireBall = MireModels.SludgeBall();
            var prevImpact = enemies.onImpact;
            enemies.onImpact = (e, a, p) => { prevImpact?.Invoke(e, a, p); MireImpact(e, a, p); };
            enemies.onAggro += e => { if (Skills.IsOn("toxic") && e.alive) e.poisonUntil = realTime + 3; };   // 독에 잠식된 코어
        }

        void MireUpdate(float dt)
        {
            if (!mireInit) MireInit();
            LaserBlade.toxic = Skills.IsOn("toxic");
            var p = ch.root.position;
            // poison ticks (독에 잠식된 코어): 7 / s, a green wisp now and then
            foreach (var e in enemies.list)
            {
                if (!e.alive || e.poisonUntil <= realTime) continue;
                e.Poison(7 * dt);
                if (Random.value < dt * 4) enemies.Wisp(e.Center() + new Vector3(Random.Range(-.3f, .3f), .3f, Random.Range(-.3f, .3f)), .25f);
            }
            GlobsUpdate(dt, p);
            PuddlesUpdate(dt, p);
            for (int i = hosts.Count - 1; i >= 0; i--) { var h = hosts[i]; if (!h.e.alive || !h.e.Exists) { HostTeardown(h); hosts.RemoveAt(i); continue; } HostUpdate(h, dt, p); }
            for (int i = dredges.Count - 1; i >= 0; i--) { var d = dredges[i]; if (!d.e.alive || !d.e.Exists) { dredges.RemoveAt(i); continue; } if (realTime < d.jamUntil && Random.value < dt * 6) vfx.Burst(d.e.Center(), 3, .4f); }
            // the intake pull: drag the player toward the dredger for a moment
            if (realTime < suckUntil && player.alive && !Invulnerable())
            {
                var q = WorldCollision.Move(p, Vector3.MoveTowards(p, new Vector3(suckTo.x, p.y, suckTo.z), 11 * dt), PLAYER_R, WorldCollision.STEP);
                q.y = WorldCollision.Floor(q.x, q.z, p.y, WorldCollision.STEP); ch.root.position = q; ch.UpdateWorld(); moveTarget = null;
            }
            SuckSkillUpdate(dt);
            ShellStream(p);
        }

        // ---------- hazards ----------
        /// a ball of sludge thrown from `from` to land on `to` after `windup` s (red telegraph meanwhile); a puddle stays
        void ThrowGlob(Vector3 from, Vector3 to, float dmg, float radius, float windup, float puddleR, float puddleT, Enemy src, int split = 0)
        {
            to.y = WorldCollision.Floor(to.x, to.z, to.y + .5f, 1);
            var tele = new Telegraph(enemies.parent, "circle", to, 0, new EnemyAttack { radius = radius }, windup);
            var go = new GameObject("sludge ball"); go.transform.SetParent(scene.actors, false);
            go.AddComponent<MeshFilter>().sharedMesh = mireBall; go.AddComponent<MeshRenderer>().sharedMaterial = ballMat;
            go.transform.localScale = Vector3.one * (split > 0 ? 1.5f : .9f); go.SetActive(false);
            globs.Add(new Glob { tele = tele, from = from, to = to, dmg = dmg, puddleR = puddleR, puddleT = puddleT, split = split, src = src, go = go });
        }
        /// a straight poison breath (line telegraph, no ball)
        void Breath(Vector3 from, float yaw, float width, float length, float dmg, float windup, Enemy src)
        {
            var tele = new Telegraph(enemies.parent, "line", from, yaw, new EnemyAttack { width = width, length = length }, windup);
            globs.Add(new Glob { tele = tele, from = from, to = from, dmg = dmg, src = src, breath = true });
        }
        void GlobsUpdate(float dt, Vector3 p)
        {
            for (int i = globs.Count - 1; i >= 0; i--)
            {
                var g = globs[i]; bool done = g.tele.Update(dt);
                if (g.go)
                {   // flight: the last 0.85 s of the wind-up, a high arc
                    float u = Mathf.Clamp01((g.tele.t - (g.tele.duration - .85f)) / .85f);
                    g.go.SetActive(u > 0);
                    var q = Vector3.Lerp(g.from, g.to, u); q.y += Mathf.Sin(u * Mathf.PI) * (4 + .25f * Sim.FlatY(g.to - g.from).magnitude);
                    g.go.transform.localPosition = Sim.ToUnity(q);
                }
                if (!done) continue;
                if (player.alive && g.tele.Contains(p)) OnPlayerHit(g.dmg, g.breath ? g.from : g.to, g.src);
                if (g.breath) { vfx.Burst(g.from + Sim.Forward(g.tele.yaw) * 4 + Vector3.up, 14, .9f); }
                else
                {
                    vfx.Burst(g.to + Vector3.up * .3f, 16, 1); vfx.Dust(g.to + new Vector3(0, .05f, 0), .9f); SoulAudio.Play("stepWater", g.to, .9f, .55f);
                    if (g.puddleT > 0) AddPuddle(g.to, g.puddleR, g.puddleT, 9);
                    for (int k = 0; k < g.split; k++)
                    {   // a big ball bursts into small ones around the landing
                        float a = k * Mathf.PI * 2 / g.split + Random.Range(-.3f, .3f);
                        ThrowGlob(g.to + Vector3.up, g.to + Sim.Forward(a) * Random.Range(3.5f, 5.5f), g.dmg * .6f, 1.8f, 1.0f, 1.6f, g.puddleT * .7f, g.src);
                    }
                }
                g.tele.Dispose(); if (g.go) Object.Destroy(g.go); globs.RemoveAt(i);
            }
        }
        void AddPuddle(Vector3 at, float r, float time, float dps)
        {
            var go = new GameObject("poison puddle"); go.transform.SetParent(scene.actors, false);
            go.AddComponent<MeshFilter>().sharedMesh = mireDisc; go.AddComponent<MeshRenderer>().sharedMaterial = puddleMat;
            go.transform.localPosition = Sim.ToUnity(at + new Vector3(0, .06f, 0)); go.transform.localScale = new Vector3(r, 1, r);
            puddles.Add(new Puddle { pos = at, r = r, until = realTime + time, dps = dps, go = go });
        }
        void PuddlesUpdate(float dt, Vector3 p)
        {
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var d = puddles[i]; float left = d.until - realTime;
                if (left <= 0) { Object.Destroy(d.go); puddles.RemoveAt(i); continue; }
                float k = Mathf.Clamp01(left / .6f); d.go.transform.localScale = new Vector3(d.r * (.6f + .4f * k), 1, d.r * (.6f + .4f * k));
                if (!player.alive || Invulnerable() || Sim.FlatY(p - d.pos).magnitude > d.r || Mathf.Abs(p.y - d.pos.y) > 1) continue;
                player.hp -= d.dps * (Parts.Has(Parts.Toxic) ? .5f : 1) * dt; hurtFlash = Mathf.Max(hurtFlash, .25f);
                if (realTime > puddleCue) { puddleCue = realTime + 4; face.Set("hurt", .5f); }
                if (player.hp <= 0) { player.hp = 0; PlayerDie(); }
            }
        }
        void MireImpact(Enemy e, EnemyAttack a, Vector3 p)
        {
            if (a.name == "spore") AddPuddle(p, 2.3f, 4, 8);
            else if (a.name == "spit") AddPuddle(p, 1.6f, 3.5f, 7);
            else if (a.name == "suck") Suck(e, a);
        }

        // ---------- 준설기: the intake ----------
        void Suck(Enemy e, EnemyAttack a)
        {
            var f = Sim.Forward(e.yaw); var mouth = e.pos + f * (e.radius + .5f);
            bool InCone(Vector3 q) { var d = Sim.FlatY(q - e.pos); float L = d.magnitude; return L < a.radius + e.radius && (L < .5f || Vector3.Dot(d / L, f) > Mathf.Cos(a.angle * .5f + .15f)); }
            // swarm bugs caught in the suck are swallowed: the intake jams
            int eaten = 0;
            foreach (var o in enemies.list) if (o != e && o.alive && o.type == "swarmBug" && InCone(o.pos)) { eaten++; vfx.Burst(o.Center(), 8, .6f); o.Die(); }
            if (eaten > 0)
            {
                var r = DredgeOf(e); if (r != null) r.jamUntil = realTime + (e.def.eliteName != null ? 5 : 3);
                e.ToStagger(e.def.eliteName != null ? 5 : 3);
                vfx.Flash(mouth + Vector3.up * e.height * .5f, 1.6f); vfx.Burst(mouth + Vector3.up * e.height * .5f, 30, 1.3f); cam.Shake(.08f);
                SoulAudio.Play("pillarBreak", mouth, .8f, .7f);
                return;
            }
            if (player.alive && InCone(ch.root.position)) { suckUntil = realTime + .55f; suckTo = mouth; }
        }
        DredgeRig DredgeOf(Enemy e) { foreach (var d in dredges) if (d.e == e) return d; return null; }

        // ---------- elites ----------
        /// called by EliteSetup first: true = a Z6 elite (registered here; the shared EliteRig keeps the camera / bar)
        bool MireElite(Territory t, Enemy e)
        {
            if (!mireInit) MireInit();
            if (e.type == "shellHost")
            {
                var h = new HostRig { e = e };
                float size = e.def.size, hh = e.height / size, rad = e.radius / size;
                for (int k = 0; k < 3; k++)
                {   // three sacs on the shell's crown
                    float an = k * Mathf.PI * 2 / 3 + .4f;
                    var go = new GameObject("shell sac"); go.transform.SetParent(e.Root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mireBall; go.AddComponent<MeshRenderer>().sharedMaterial = sacMat;
                    go.transform.localPosition = Sim.ToUnity(new Vector3(Mathf.Sin(an) * rad * .42f, hh * 1.0f, Mathf.Cos(an) * rad * .42f)); go.transform.localScale = Vector3.one * .3f;
                    h.sacs[k] = go;
                }
                e.hitScale = (amt, from) => HostHit(h, amt, from);
                hosts.Add(h); elites.Add(new EliteRig { e = e, t = t });
                return true;
            }
            if (e.type == "giantDredger")
            {
                var d = new DredgeRig { e = e }; dredges.Add(d);
                e.hitScale = (amt, from) => realTime < d.jamUntil ? 2 : 1;
                elites.Add(new EliteRig { e = e, t = t });
                return true;
            }
            return false;
        }
        Vector3 SacWorld(HostRig h, int k) => h.sacs[k] ? Sim.FromUnity(h.sacs[k].transform.position) : h.e.Center() + Vector3.up * h.e.height * .5f;
        float HostHit(HostRig h, float amt, Vector3 from)
        {
            var e = h.e;
            if (realTime < h.stunUntil) return 2;
            if (h.phase == "breathe") return 1;
            var p = ch.root.position; float floor = WorldCollision.Floor(p.x, p.z, p.y, .3f);
            bool high = p.y > floor + .45f || p.y > e.pos.y + 1.4f, far = Sim.FlatY(from - e.pos).magnitude > e.radius + 3.5f;
            if (h.phase == "inflate" && !h.burst[h.sac] && (high || far))
            {   // the swollen sac bursts over its own shell: the throw is cancelled, the sludge burns it
                h.burst[h.sac] = true; var at = SacWorld(h, h.sac);
                if (h.sacs[h.sac]) h.sacs[h.sac].SetActive(false);
                vfx.Burst(at, 26, 1.2f); vfx.Flash(at, 1.2f); SoulAudio.Play("stepWater", at, 1, .4f); cam.Shake(.06f);
                e.Poison(e.MaxHp * .03f);
                h.phase = "wait"; h.t = 0; h.sac = (h.sac + 1) % 3;
                int n = 0; foreach (var b in h.burst) if (b) n++;
                if (n >= 3) { h.stunUntil = realTime + 8; e.ToStagger(8); vfx.Ring(Sim.FlatY(e.pos, .1f), 4); cam.Shake(.12f); }
                return .5f;
            }
            // the shell turns the blow aside
            vfx.Burst(e.Center() + Sim.FlatY(from - e.pos).normalized * e.radius * .8f, 8, .5f); SoulAudio.Play("parry", e.Center(), .45f, 1.3f);
            return .08f;
        }
        void HostUpdate(HostRig h, float dt, Vector3 p)
        {
            var e = h.e; h.t += dt;
            bool low = e.hpFrac < .5f;
            for (int k = 0; k < 3; k++)
            {   // sacs: hidden once burst (they grow back after it breathes); the inflating one swells and glows
                var s = h.sacs[k]; if (!s) continue;
                s.SetActive(!h.burst[k]);
                float swell = h.phase == "inflate" && h.sac == k ? Mathf.Clamp01(h.t / 1.1f) : 0;
                s.transform.localScale = Vector3.one * (.3f + .32f * swell + .03f * Mathf.Sin(realTime * (swell > 0 ? 22 : 3) + k));
            }
            Mats.SetColor(sacMat, new Color(1.2f, .4f, 2.2f, 1) * (1 + .6f * Mathf.Sin(realTime * 5)));
            if (!e.Engaged || realTime < h.stunUntil || e.state == "stagger") return;
            switch (h.phase)
            {
                case "wait":
                    if (h.t > (low ? .7f : 1.1f))
                    {
                        if (h.sets >= 2) { h.phase = "breathe"; h.t = 0; h.sets = 0; e.coreBoost = 2; vfx.Ring(Sim.FlatY(e.pos, .1f), 3); SoulAudio.Play("stepWater", e.pos, 1, .35f); break; }
                        int guard = 0; while (h.burst[h.sac] && guard++ < 3) h.sac = (h.sac + 1) % 3;
                        if (h.burst[h.sac]) { h.sets = 2; h.t = 0; break; }   // nothing left to throw with: breathe
                        h.phase = "inflate"; h.t = 0;
                    }
                    break;
                case "inflate":
                    if (h.t > 1.1f)
                    {   // throw: every third one after half HP is a big ball that bursts into four
                        bool big = low && h.sac == 2;
                        ThrowGlob(SacWorld(h, h.sac), p, big ? 26 : 22, big ? 3.4f : 2.4f, 1.3f, big ? 2.8f : 2.2f, 5, e, big ? 4 : 0);
                        SoulAudio.Play("stepWater", e.pos, .8f, .6f);
                        h.sac++; h.t = 0; h.phase = "wait";
                        if (h.sac >= 3) { h.sac = 0; h.sets++; }
                    }
                    break;
                case "breathe":
                    if (h.t > 4) { h.phase = "wait"; h.t = 0; e.coreBoost = 1; for (int k = 0; k < 3; k++) h.burst[k] = false; }
                    break;
            }
        }
        void HostTeardown(HostRig h) { foreach (var s in h.sacs) if (s) Object.Destroy(s); }

        /// field kill hook (OnFieldKill): the shell host's death leaves its shell as a platform
        void MireKill(Enemy e)
        {
            if (e.type == "shellHost") { ShellPlatform(); SpawnDrop("core:toxic", LedgeTop() + new Vector3(-1.2f, 0, 1.2f)); Cue("굳은 껍질 · 발판이 되었다", 2.4f); }
        }
        static Vector3 LedgeTop() { var c = MirePools.Ledge; return new Vector3(c.x, Terrain.Height(c.x, c.y), c.y); }

        // ---------- the dead shell: a platform in the pool ----------
        // top 2.2 m over the pool floor: bank -> shell and shell -> ledge both within the 2.36 m double jump.
        // The crown is round (MireModels.DeadShell, flat to r 3.05): four boxes at 0/45/90/135° cover it as a
        // 16-gon (edge 2.8 m on the axes, 3.03 m between); the corpse is the living host's size (dome r ~3.1).
        const float SHELL_TOP = 2.2f, SHELL_LONG = 2.8f, SHELL_WIDE = 1.2f, SHELL_WALL = 3.6f;
        void ShellPlatform()
        {
            if (shellBuilt) return;
            shellBuilt = true;
            var c = MirePools.Host; float floor = Terrain.Height(c.x, c.y), top = floor + SHELL_TOP;
            var surfs = new List<WorldCollision.Surf>();
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * .25f;
                surfs.Add(new WorldCollision.Surf { kind = PropSurface.BOX, c = c, half = new Vector2(SHELL_LONG, SHELL_WIDE), cos = Mathf.Cos(a), sin = Mathf.Sin(a), top = top });
            }
            var cols = new List<WorldCollision.Col> { new WorldCollision.Col { box = false, c = c, r = SHELL_WALL, ranged = true, y0 = floor - 3, y1 = top - .15f } };
            WorldCollision.AddPersistent(cols, surfs);
            vfx.Dust(new Vector3(c.x, top, c.y), 2); cam.Shake(.08f);
        }
        void ShellStream(Vector3 p)
        {
            if (!shellBuilt) return;
            bool near = Sim.FlatY(new Vector3(MirePools.Host.x, 0, MirePools.Host.y) - p).magnitude < 140;
            if (near && shellGo == null)
            {
                if (shellMesh == null) shellMesh = MireModels.DeadShell();
                var c = MirePools.Host;
                shellGo = new GameObject("dead shell host"); shellGo.transform.SetParent(scene.actors, false);
                shellGo.transform.localPosition = Sim.ToUnity(new Vector3(c.x, Terrain.Height(c.x, c.y), c.y));
                shellGo.AddComponent<MeshFilter>().sharedMesh = shellMesh; shellGo.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
            }
            else if (!near && shellGo != null) { Object.Destroy(shellGo); shellGo = null; }
        }

        // ---------- 흡입 skill (giant dredger's intake) ----------
        void CastSuck()
        {
            var p = ch.root.position; var f = ch.Forward; suckSkillTo = p + f * 2.2f; suckSkillUntil = realTime + .5f; sucked.Clear();
            foreach (var e in enemies.list)
            {
                if (!e.Hittable || e.IsBoss) continue;
                var d = Sim.FlatY(e.pos - p); float L = d.magnitude;
                if (L > 10 || L < .3f || Vector3.Dot(d / L, f) < .55f) continue;
                if (e.def.eliteName == null) sucked.Add(e);
                e.Damage(8, p);
            }
            vfx.Ring(Sim.FlatY(p, .1f) + f * 3, 1.6f); vfx.Burst(p + f * 5 + Vector3.up, 20, 1); cam.Shake(.05f); SoulAudio.Play("laser", p, .7f, .6f);
        }
        void SuckSkillUpdate(float dt)
        {
            if (realTime > suckSkillUntil) { sucked.Clear(); return; }
            foreach (var e in sucked)
            {
                if (!e.alive) continue;
                var q = Vector3.MoveTowards(e.pos, new Vector3(suckSkillTo.x, e.pos.y, suckSkillTo.z), 16 * dt);
                WorldCollision.Resolve(ref q, e.radius); e.pos = q;
                if (Random.value < dt * 10) vfx.Burst(e.Center(), 2, .3f);
            }
        }

        // ---------- 늪의 어미: the tide fight ----------
        const float TIDE_LOW = -.45f, TIDE_HIGH = .75f, ROOF_TOP = 1.0f, EGG_TOP = .95f;
        string tidePhase = "low"; float tide = TIDE_LOW, tideT, motherT, eggT; int motherMove;
        readonly List<Enemy> tentacles = new List<Enemy>();
        GameObject tideGo, roofsGo; readonly List<(GameObject go, List<WorldCollision.Col> cols, List<WorldCollision.Surf> surfs)> eggs = new List<(GameObject, List<WorldCollision.Col>, List<WorldCollision.Surf>)>();
        bool roofsBuilt;
        bool MireFight => fightArena != null && fightArena.boss == "mireMother";
        public string MireState => $"flow {bossFlow} tide {tidePhase} {tide:F2} t {tideT:F1} tentacles {tentacles.FindAll(x => x.alive).Count} eggs {eggs.Count} boss {(boss != null ? boss.state + " " + boss.hp.ToString("F0") : "none")}";

        void MireStart(BossArena a)
        {
            if (!mireInit) MireInit();
            bossFlow = "intro"; flowT = 0; tidePhase = "low"; tide = TIDE_LOW; tideT = 0; motherT = 0;
            Roofs(a);
            if (tideGo == null)
            {
                tideGo = new GameObject("mire tide"); tideGo.transform.SetParent(scene.actors, false);
                tideGo.AddComponent<MeshFilter>().sharedMesh = mireDisc; tideGo.AddComponent<MeshRenderer>().sharedMaterial = tideMat;
            }
            tideGo.SetActive(true); tideGo.transform.localScale = new Vector3(a.radius, 1, a.radius);
            boss.hitScale = (amt, from) => MotherHit(amt, from);
            Cue("늪이 숨을 쉰다", 2);
        }
        void MireClear()
        {
            foreach (var t in tentacles) if (t.alive) t.Despawn();
            tentacles.Clear(); ClearEggs();
            if (tideGo) tideGo.SetActive(false);
            tidePhase = "low"; tide = TIDE_LOW;
        }
        float ArenaFloor(BossArena a) => Terrain.Height(a.center.x, a.center.z);
        float MotherHit(float amt, Vector3 from)
        {
            if (tidePhase == "high") return 1;
            vfx.Burst(boss.Center(), 6, .4f); SoulAudio.Play("stepWater", boss.Center(), .5f, 1.3f);
            return .05f;   // under the sludge
        }
        void Roofs(BossArena a)
        {
            if (roofsBuilt) return;
            roofsBuilt = true;
            float floor = ArenaFloor(a);
            if (roofMesh == null) roofMesh = MireModels.Roof();
            roofsGo = new GameObject("mire roofs"); roofsGo.transform.SetParent(scene.actors, false);
            var cols = new List<WorldCollision.Col>(); var surfs = new List<WorldCollision.Surf>();
            for (int k = 0; k < 4; k++)
            {
                float an = Mathf.PI / 4 + k * Mathf.PI / 2; var c = new Vector2(a.center.x + Mathf.Sin(an) * 15, a.center.z + Mathf.Cos(an) * 15);
                var go = new GameObject("roof"); go.transform.SetParent(roofsGo.transform, false);
                go.transform.localPosition = Sim.ToUnity(new Vector3(c.x, floor, c.y)); go.transform.localRotation = Sim.YawToUnity(an);
                go.AddComponent<MeshFilter>().sharedMesh = roofMesh; go.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                float cs = Mathf.Cos(an), sn = Mathf.Sin(an);
                surfs.Add(new WorldCollision.Surf { kind = PropSurface.BOX, c = c, half = new Vector2(3.2f, 2.4f), cos = cs, sin = sn, top = floor + ROOF_TOP });
                cols.Add(new WorldCollision.Col { box = true, c = c, half = new Vector2(3.3f, 2.5f), cos = cs, sin = sn, ranged = true, y0 = floor - 2, y1 = floor + ROOF_TOP - .15f });
            }
            WorldCollision.AddPersistent(cols, surfs);
        }
        void SpawnEggs(BossArena a)
        {
            ClearEggs();
            if (eggMesh == null) eggMesh = MireModels.EggShell();
            float floor = ArenaFloor(a);
            for (int k = 0; k < 5; k++)
            {
                float an = k * Mathf.PI * 2 / 5 + Random.Range(-.25f, .25f), rr = Random.Range(7.5f, 10.5f);
                var c = new Vector2(a.center.x + Mathf.Sin(an) * rr, a.center.z + Mathf.Cos(an) * rr);
                var go = new GameObject("egg shell"); go.transform.SetParent(scene.actors, false);
                go.transform.localPosition = Sim.ToUnity(new Vector3(c.x, floor - 1, c.y));
                go.AddComponent<MeshFilter>().sharedMesh = eggMesh; go.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                var surfs = new List<WorldCollision.Surf> { new WorldCollision.Surf { kind = PropSurface.BOX, c = c, half = new Vector2(1.3f, 1.3f), cos = 1, sin = 0, top = floor + EGG_TOP } };
                var cols = new List<WorldCollision.Col> { new WorldCollision.Col { box = true, c = c, half = new Vector2(1.4f, 1.4f), cos = 1, sin = 0, ranged = true, y0 = floor - 2, y1 = floor + EGG_TOP - .15f } };
                WorldCollision.AddPersistent(cols, surfs);
                eggs.Add((go, cols, surfs));
            }
        }
        void ClearEggs()
        {
            foreach (var g in eggs) { if (g.go) Object.Destroy(g.go); WorldCollision.RemovePersistent(g.cols); WorldCollision.RemovePersistentSurfs(g.surfs); }
            eggs.Clear();
        }
        void SpawnTentacles(BossArena a)
        {
            foreach (var t in tentacles) if (t.alive) t.Despawn();
            tentacles.Clear();
            float floor = ArenaFloor(a), a0 = Random.Range(0, Mathf.PI * 2);
            for (int k = 0; k < 3; k++)
            {
                float an = a0 + k * Mathf.PI * 2 / 3; var at = new Vector3(a.center.x + Mathf.Sin(an) * 9.5f, floor, a.center.z + Mathf.Cos(an) * 9.5f);
                var t = enemies.SpawnAt("mireTentacle", at); t.Engage(); tentacles.Add(t);
                vfx.Burst(at + Vector3.up, 18, 1); vfx.Dust(at, 1.2f);
            }
            SoulAudio.Play("stepWater", a.center, 1, .3f); cam.Shake(.06f);
        }

        /// called from PillarsUpdate while the mother's fight runs; false = not this fight
        bool MireFightUpdate(float dt)
        {
            if (!MireFight || boss == null) return false;
            var a = fightArena; var p = ch.root.position; flowT += dt;
            cam.fightTarget = boss.alive ? 1 : 0; cam.fightZoom = 1.3f; cam.fightFocus = a.center;
            float floor = ArenaFloor(a);
            if (bossFlow == "intro")
            {
                if (boss.state == "dormant" && flowT > 1.4f) { boss.Wake(2.6f); boss.eyeTint = new Color(1.4f, 2.6f, .5f, 1); vfx.Ring(Sim.FlatY(boss.pos, floor + .05f), 4); cam.Shake(.1f); SoulAudio.Play("stepWater", boss.pos, 1, .25f); }
                if (boss.state == "chase") { bossFlow = "fight"; bossIntro = 0; tidePhase = "low"; tideT = 0; SpawnTentacles(a); }
            }
            else if (bossFlow == "fight")
            {
                if (boss.state == "down") { boss.Die(); MireClear(); return true; }
                bool low = boss.hpFrac < .5f; tideT += dt;
                tentacles.RemoveAll(t => !t.alive && !t.Exists);
                int alive = 0; foreach (var t in tentacles) if (t.alive) alive++;
                if (tidePhase == "low")
                {
                    if ((alive == 0 && tideT > 2) || tideT > (low ? 13 : 17))
                    {   // the tide comes: the square floods, egg shells float up, the mother surfaces
                        tidePhase = "high"; tideT = 0; eggT = 0; motherT = 1.2f; motherMove = 0;
                        foreach (var t in tentacles) if (t.alive) t.Despawn();
                        tentacles.Clear(); SpawnEggs(a);
                        Cue(alive == 0 ? "뿌리가 끊겼다 · 어미가 떠오른다" : "물이 차오른다", 2);
                        vfx.Ring(Sim.FlatY(boss.pos, floor + .1f), 5); cam.Shake(.12f); SoulAudio.Play("stepWater", a.center, 1, .2f);
                    }
                }
                else
                {
                    eggT += dt; motherT -= dt;
                    if (motherT <= 0 && player.alive)
                    {   // surfaced: throws sludge, breathes poison (faster below half HP)
                        motherT = low ? 1.9f : 2.6f;
                        if (motherMove++ % 2 == 0) ThrowGlob(boss.Center() + Vector3.up * 2, p, 20, 2.8f, 1.3f, 0, 0, boss, low ? 3 : 0);
                        else { var d = Sim.FlatY(p - boss.pos); Breath(new Vector3(boss.pos.x, floor + TIDE_HIGH, boss.pos.z), Mathf.Atan2(d.x, d.z), 3.4f, 24, 22, 1.2f, boss); }
                    }
                    // egg shells sink after a while (they were only ever floating)
                    if (eggs.Count > 0 && eggT > (low ? 4.5f : 6.5f)) ClearEggs();
                    if (tideT > (low ? 10 : 13))
                    {
                        tidePhase = "low"; tideT = 0; ClearEggs(); SpawnTentacles(a);
                        Cue("물이 빠진다 · 뿌리가 솟는다", 1.8f);
                    }
                }
                // the egg shells bob up with the tide
                foreach (var g in eggs) if (g.go) { var lp = g.go.transform.localPosition; float want = Sim.ToUnity(new Vector3(0, floor, 0)).y + Mathf.Min(0, eggT * 1.5f - 1) + .05f * Mathf.Sin(realTime * 2); g.go.transform.localPosition = new Vector3(lp.x, want, lp.z); }
            }
            // the tide level and its burn
            float want2 = tidePhase == "high" && bossFlow == "fight" ? TIDE_HIGH : TIDE_LOW;
            tide = Mathf.MoveTowards(tide, want2, dt * .7f);
            if (tideGo) tideGo.transform.localPosition = Sim.ToUnity(new Vector3(a.center.x, floor + tide, a.center.z));
            if (tide > 0 && player.alive && !Invulnerable() && Sim.FlatY(p - a.center).magnitude < a.radius && p.y < floor + tide - .03f)
            {
                player.hp -= (Parts.Has(Parts.Toxic) ? 4 : 11) * dt; hurtFlash = Mathf.Max(hurtFlash, .25f);
                if (player.hp <= 0) { player.hp = 0; PlayerDie(); }
            }
            return true;
        }

        /// MCP tests
        public string MireTest(string what)
        {
            if (what == "state") return MireState + $" | hosts {hosts.Count} dredges {dredges.Count} puddles {puddles.Count} globs {globs.Count} shell {shellBuilt} core {Skills.IsOn("toxic")}";
            if (what == "host")
            {
                if (hosts.Count == 0) return "no host";
                var h = hosts[0]; return $"host hp {h.e.hp:F0} phase {h.phase} sac {h.sac} sets {h.sets} burst {h.burst[0]},{h.burst[1]},{h.burst[2]} stun {Mathf.Max(0, h.stunUntil - realTime):F1} engaged {h.e.Engaged}";
            }
            if (what == "core") { GrantSkill("toxic"); return "core given (equipped " + Skills.IsOn("toxic") + ")"; }
            if (what == "suck") { GrantSkill("suck"); return "suck granted"; }
            return "?";
        }
        public void MireTick(float dt) => MireUpdate(dt);
        public bool MireFightTick(float dt) => MireFightUpdate(dt);
    }

    // Mesh-only models. The three bearing planes cover the existing BOX surfaces exactly;
    // all relief is below them, inside the corresponding wall footprints. Sim space throughout.
    static class MireModels
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        // CCW perimeter seen from above, with exact corners (unlike a circular fan).
        static Vector3 SquarePoint(int i, int perSide, float halfX, float halfZ, float y)
        {
            int side = i / perSide % 4;
            float t = (i % perSide) / (float)perSide * 2 - 1;
            if (side == 0) return V(-halfX, y, t * halfZ);
            if (side == 1) return V(t * halfX, y, halfZ);
            if (side == 2) return V(halfX, y, -t * halfZ);
            return V(-t * halfX, y, -halfZ);
        }

        // Shell-host helpers. All coordinates are sim-space; only Build mirrors.
        static Vector3 Pol(float r, float deg, float y)
        {
            float a = deg * Mathf.Deg2Rad;
            return V(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }
        static void TwoSided(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Color col)
        {
            b.Tri(a, c, d, col); b.Tri(a, d, c, col);
        }
        // Closed olive-bevelled chitin scute. Low relief on the crown, sloping
        // layered folds on the skirt. Correct the perimeter before emitting.
        static void Scute(MeshBuilder b, Vector3[] outer, int hex, float rise = .025f)
        {
            Vector3 center = Vector3.zero;
            float winding = 0;
            for (int i = 0; i < outer.Length; i++)
            {
                center += outer[i];
                winding += Vector3.Cross(outer[i], outer[(i + 1) % outer.Length]).y;
            }
            center /= outer.Length;
            if (winding < 0)
                for (int i = 0; i < outer.Length / 2; i++)
                {
                    Vector3 swap = outer[i]; outer[i] = outer[outer.Length - 1 - i];
                    outer[outer.Length - 1 - i] = swap;
                }
            var inset = new Vector3[outer.Length];
            for (int i = 0; i < outer.Length; i++)
                inset[i] = Vector3.Lerp(outer[i], center, .12f) + V(0, rise * .5f, 0);
            Vector3 ridge = center + V(0, rise, 0), bottom = center - V(0, .045f, 0);
            Color plate = b.Jitter(hex, .08f);
            for (int i = 0; i < outer.Length; i++)
            {
                int n = (i + 1) % outer.Length;
                b.Tri(ridge, inset[i], inset[n], b.Jitter(plate, .035f));
                b.Quad(outer[i], outer[n], inset[n], inset[i], b.Jitter(0x797d50, .08f));
                Vector3 lo = outer[i] - V(0, .045f, 0), ln = outer[n] - V(0, .045f, 0);
                b.Quad(lo, ln, outer[n], outer[i], C(0x33222f));
                b.Tri(bottom, ln, lo, C(0x30232b));
            }
        }
        static void ShellLobe(MeshBuilder b, Vector3 center, Vector3 size, int hex, int detail = 0)
        {
            b.Push(); b.Translate(center); b.Scale(size.x, size.y, size.z);
            b.Rock(Vector3.zero, 1, b.Jitter(hex, .08f), 0, 1, detail); b.Pop();
        }
        static void ShellHook(MeshBuilder b, Vector3 a, Vector3 bend, Vector3 tip, float radius, int hex)
        {
            b.Beam(a, bend, radius, radius * .57f, 4, b.Jitter(hex, .06f));
            b.Beam(bend, tip, radius * .57f, .008f, 4, C(hex));
        }
        static void Rivets(MeshBuilder b, Vector3 from, Vector3 to, int n, float h)
        {
            for (int k = 0; k < n; k++)
                b.Cylinder(Vector3.Lerp(from, to, n == 1 ? .5f : k / (float)(n - 1)),
                    .035f, .027f, h, 5, C(0x81765e));
        }

        public static Mesh DeadShell()
        {
            var b = new MeshBuilder(613);
            const float top = 2.2f, crown = 3.06f;
            const int chitin = 0x49323f, plate = 0x624650, flesh = 0x785064;
            const int horn = 0x99946c, dark = 0x30212b, slime = 0x455239;
            // Continuous bearing skin: r=3.06 (inradius >3.05) covers the four
            // fixed half(2.8,1.2) boxes. Relief on r<3 remains below y=2.32.
            b.Disc(V(0, top - .01f, 0), 0, crown, 48, C(0x49323f));
            float Outer(float z) => Mathf.Sqrt(Mathf.Max(0, crown * crown - z * z));
            float Inner(float z) => Mathf.Sqrt(Mathf.Max(.22f * .22f, 1.64f * 1.64f - z * z));
            // Same five paired costal scutes as the living host, collapsed flat.
            for (int row = 0; row < 5; row++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = -2.02f + row * 1.01f, z0 = z - .47f, z1 = z + .56f;
                    Scute(b, new[] {
                        V(side * Inner(z0), top, z0), V(side * Outer(z0) * .94f, top, z0),
                        V(side * Outer(z + .07f), top, z + .07f),
                        V(side * Outer(z1) * .94f, top, z1), V(side * Inner(z1), top, z1),
                        V(side * Inner(z + .07f), top, z + .07f)
                    }, row % 2 == 0 ? chitin : plate);
                }
            // Keel spines have folded sideways: low, broad horn scars, never
            // upright spikes in the walking area. Crown maximum here = 2.28.
            for (int row = 0; row < 5; row++)
            {
                float z = -2.35f + row * 1.14f;
                Scute(b, new[] { V(-.24f, top, z - .25f), V(.24f, top, z - .25f),
                    V(.33f, top, z + .18f), V(.02f, top, z + .34f),
                    V(-.30f, top, z + .18f) }, chitin, .055f);
                b.Beam(V(-.13f, 2.265f, z), V(.25f, 2.235f, z - .16f),
                    .015f, .008f, 4, C(horn));
            }
            // Underlying dome slopes down to the sludge. A left flank window
            // is genuinely open: exposed ribs replace the missing outer skin.
            const int count = 48;
            float[] radii = { crown, 3.38f, 3.58f, 3.73f, 3.52f };
            float[] heights = { top - .01f, 1.92f, 1.18f, .72f, -.24f };
            bool Socket(int i) => i <= 1 || i >= 46 || (i >= 7 && i <= 9)
                || (i >= 14 && i <= 16) || (i >= 31 && i <= 33) || (i >= 38 && i <= 40);
            for (int row = 0; row < radii.Length - 1; row++)
                for (int i = 0; i < count; i++)
                {
                    if (row >= 1 && (Socket(i) || (i >= 32 && i <= 36))) continue;
                    float a = i * 360f / count, next = (i + 1) * 360f / count;
                    int color = row == 0 ? chitin : row == 1 ? dark : row == 2 ? slime : 0x292e25;
                    float lowerR = row == 0 && Socket(i) ? 3.13f : radii[row + 1];
                    b.Quad(Pol(lowerR, a, heights[row + 1]),
                        Pol(lowerR, next, heights[row + 1]),
                        Pol(radii[row], next, heights[row]), Pol(radii[row], a, heights[row]),
                        b.Jitter(color, .09f));
                }
            // Twenty-four serrated skirt shingles echo the live shell's rim.
            // The flared tooth tips are at the waterline, within r=3.86.
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                if (i == 0 || i == 23 || i == 3 || i == 4 || i == 7 || i == 8
                    || (i >= 15 && i <= 18) || i == 19 || i == 20) continue;
                Scute(b, new[] { Pol(2.79f, a - 7, top - .005f),
                    Pol(3.35f, a - 7, 1.92f), Pol(3.86f, a + 1.5f, .76f),
                    Pol(3.33f, a + 7, 1.88f), Pol(2.79f, a + 7, top - .005f) },
                    i % 3 == 0 ? plate : chitin, .025f);
                if (i % 2 == 0)
                    ShellHook(b, Pol(3.33f, a, 1.63f), Pol(3.57f, a - 1, 1.89f),
                        Pol(3.78f, a - 4, 1.66f), .095f, horn);
                // Low olive knob at a plate edge; never protrudes onto crown.
                if (i % 2 == 0) ShellLobe(b, Pol(3.23f, a + 4, 1.98f), V(.08f, .05f, .08f), 0x7e8155);
            }
            // Torn flank at -X: dark recessed flesh and five bowed pale ribs.
            ShellLobe(b, V(-3.12f, 1.16f, -.55f), V(.28f, .63f, .99f), dark);
            for (int i = 0; i < 5; i++)
            {
                float z = -1.33f + i * .36f;
                Vector3 a = V(-2.98f, 1.84f, z), bend = V(-3.42f, 1.39f, z + .06f),
                    end = V(-3.40f, .83f, z + .15f);
                b.Beam(a, bend, .085f, .07f, 5, C(0xb0a88b));
                b.Beam(bend, end, .07f, .035f, 5, C(0x8b826a));
                ShellHook(b, a + V(.035f, -.04f, .10f), bend + V(.025f, -.08f, .12f),
                    end + V(.03f, -.20f, .10f), .042f, flesh);
            }
            // Exactly two small machine fragments, mostly swallowed by tissue.
            for (int side = -1; side <= 1; side += 2)
            {
                b.Push(); b.Translate(side * 2.91f, 1.94f, side * .77f);
                b.RotateZ(-side * .63f); b.RotateY(side * .25f);
                b.Box(V(0, 0, 0), V(.46f, .055f, .64f), C(0x624b3c));
                Rivets(b, V(-.15f, .035f, -.23f), V(-.15f, .035f, .23f), 3, .025f);
                ShellLobe(b, V(.15f, .015f, .04f), V(.16f, .08f, .35f), flesh);
                b.Pop();
            }
            // Three burst sacs at the living host's angles: ragged hollow cups,
            // folded petals and veins. No glow, all tissue <= .12 above top.
            for (int sac = 0; sac < 3; sac++)
            {
                float angle = sac * 120f + .4f * Mathf.Rad2Deg;
                Vector3 center = Pol(1.5456f, angle, top + .012f);
                b.Disc(center, 0, .33f, 10, C(0x392435));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    Vector3 p = center + Pol(.33f, a - 17, .025f),
                        q = center + Pol(.33f, a + 17, .025f),
                        tip = center + Pol(b.Range(.43f, .58f), a + 4, b.Range(.035f, .085f)),
                        fold = center + Pol(.43f, a, .095f);
                    TwoSided(b, p, q, fold, b.Jitter(flesh, .12f));
                    TwoSided(b, q, tip, fold, C(0x946277));
                    TwoSided(b, tip, p, fold, C(0x59374e));
                    Vector3 vein = center + Pol(.18f, a, .02f),
                        end = center + Pol(.68f, a + 7, .025f);
                    TwoSided(b, vein + Pol(.018f, a - 90, 0),
                        vein + Pol(.018f, a + 90, 0), end, C(0x815366));
                }
            }
            // Slumped neck exits +Z. It is thick and wrinkled, not an empty
            // socket; the skull is rolled to one side and the beak hangs open.
            ShellLobe(b, V(0, .94f, 2.95f), V(.57f, .35f, .63f), flesh, 1);
            for (int i = 0; i < 4; i++)
            {
                float z = 2.64f + i * .17f;
                b.Beam(V(-.43f, 1.02f, z), V(-.24f, 1.25f - i * .045f, z + .03f),
                    .05f, .04f, 5, C(0x57394c));
                b.Beam(V(-.24f, 1.25f - i * .045f, z + .03f), V(.36f, 1.10f - i * .045f, z + .04f),
                    .04f, .05f, 5, C(0x94647b));
            }
            b.Push(); b.Translate(-.13f, .98f, 3.37f); b.RotateZ(-.26f);
            ShellLobe(b, V(0, 0, 0), V(.48f, .27f, .36f), chitin, 1);
            ShellLobe(b, V(0, .17f, -.06f), V(.42f, .12f, .30f), plate);
            // Upper hooked beak, lower jaw gaping down into the sludge.
            ShellHook(b, V(0, .13f, .20f), V(0, -.01f, .43f), V(0, -.25f, .46f), .17f, horn);
            ShellLobe(b, V(.06f, -.25f, .17f), V(.35f, .11f, .30f), flesh);
            ShellHook(b, V(.06f, -.30f, .17f), V(.07f, -.30f, .45f), V(.07f, -.18f, .48f), .12f, horn);
            for (int side = -1; side <= 1; side += 2)
            {
                ShellLobe(b, V(side * .39f, .08f, .09f), V(.12f, .095f, .13f), 0x6f7350);
                ShellLobe(b, V(side * .43f, .08f, .15f), V(.055f, .042f, .055f), 0x47404d);
                ShellHook(b, V(side * .34f, -.07f, .10f), V(side * .62f, -.15f, .20f),
                    V(side * .50f, -.29f, .45f), .12f, horn);
            }
            b.Pop();
            // Four recognisable sprawling limbs; knees bend outward rather
            // than standing vertically. Distal paws and curled claws sink.
            for (int side = -1; side <= 1; side += 2)
                for (int index = 0; index < 2; index++)
                {
                    float dir = index == 0 ? 1 : -1;
                    Vector3 hip = V(side * 2.35f, 1.02f, dir * 1.30f),
                        knee = V(side * 3.10f, .95f, dir * 1.54f),
                        ankle = V(side * 3.26f, .76f, dir * 1.53f);
                    b.Beam(hip, knee, .30f, .21f, 7, C(chitin));
                    ShellLobe(b, Vector3.Lerp(hip, knee, .50f) + V(0, .10f, 0),
                        V(.46f, .16f, .28f), plate);
                    ShellLobe(b, knee, V(.23f, .19f, .23f), flesh);
                    b.Beam(knee, ankle, .20f, .15f, 6, C(chitin));
                    ShellLobe(b, ankle + V(0, .015f, 0), V(.24f, .18f, .29f), flesh);
                    ShellLobe(b, ankle + V(0, .13f, 0), V(.23f, .07f, .25f), chitin);
                    for (int claw = -1; claw <= 1; claw++)
                    {
                        Vector3 a = ankle + V(claw * .13f, .08f, dir * .19f);
                        Vector3 bend = a + V(side * .035f, .04f, dir * .06f);
                        ShellHook(b, a, bend, bend + V(-side * .03f, -.23f, -dir * .04f),
                            .065f, horn);
                    }
                }
            // Short segmented tail lies in the sludge at the rear.
            b.Beam(V(0, .65f, -3.20f), V(.18f, .49f, -3.63f), .25f, .10f, 6, C(chitin));
            ShellHook(b, V(.18f, .50f, -3.58f), V(.27f, .57f, -3.77f),
                V(.31f, .40f, -3.85f), .09f, horn);
            // Algae clings to the waterline; drips taper downward into the pool.
            for (int i = 0; i < 32; i++)
            {
                float a = i * 11.25f;
                if (i >= 21 && i <= 24) continue;
                Vector3 p = Pol(3.70f, a - 1.6f, .83f),
                    q = Pol(3.70f, a + 1.6f, .86f),
                    tip = Pol(3.72f, a + .5f, b.Range(.28f, .65f));
                TwoSided(b, p, q, tip, b.Jitter(i % 3 == 0 ? 0x617244 : slime, .12f));
                if (i % 3 == 0)
                    b.Beam(Pol(3.58f, a, 1.18f), Pol(3.76f, a + 1, .69f),
                        .035f, .012f, 4, C(slime));
            }
            return b.Build("dead shell");
        }

        public static Mesh Roof()
        {
            var b = new MeshBuilder(614);
            // Collapsed hipped roof: its tile deck is flat at ROOF_TOP, not above it.
            for (int i = 0; i < 16; i++)
            {
                int n = (i + 1) % 16;
                Vector3 a = SquarePoint(i, 4, 2.98f, 2.18f, 0), c = SquarePoint(n, 4, 2.98f, 2.18f, 0);
                Vector3 d = SquarePoint(i, 4, 3.2f, 2.4f, .69f), e = SquarePoint(n, 4, 3.2f, 2.4f, .69f);
                Vector3 f = d + V(0, .10f + (i % 3) * .012f, 0), g = e + V(0, .10f + (n % 3) * .012f, 0);
                Vector3 h = V(d.x, 1, d.z), j = V(e.x, 1, e.z);
                b.Quad(a, c, e, d, b.Jitter(i % 3 == 0 ? 0x363a31 : 0x565346));
                b.Quad(d, e, g, f, b.Jitter(0x4d613b, .16f)); // integrated algae / tide band
                b.Quad(f, g, j, h, b.Jitter(i % 4 == 0 ? 0x3b3430 : 0x6d5141));
            }
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 6; col++)
                {
                    Vector3 center = V(-3.2f + (col + .5f) * (6.4f / 6), 1, -1.8f + row * 1.2f);
                    if (row == 3 && col == 1)
                    {
                        // A chimney broken down to its roof-level stump; the flue is plugged
                        // with soot and rubble so there is no unmodelled hole in the BOX floor.
                        for (int k = 0; k < 8; k++)
                        {
                            int n = (k + 1) % 8;
                            Vector3 a = center + SquarePoint(k, 2, 6.4f / 12, .6f, 0);
                            Vector3 c = center + SquarePoint(n, 2, 6.4f / 12, .6f, 0);
                            Vector3 d = center + SquarePoint(k, 2, .27f, .32f, 0);
                            Vector3 e = center + SquarePoint(n, 2, .27f, .32f, 0);
                            b.Quad(a, c, e, d, b.Jitter(k % 2 == 0 ? 0x7c6a55 : 0x605749));
                            b.Tri(center, d, e, b.Jitter(k % 3 == 0 ? 0x615e4e : 0x2f322b));
                        }
                        continue;
                    }
                    float x = 6.4f / 12, z = .6f, chip = b.Range(.12f, .27f);
                    Vector3[] edge = { V(-x, 0, -z), V(-x, 0, z), V(x - chip, 0, z), V(x, 0, z), V(x, 0, -z) };
                    Vector3[] tile = { V(-x + .025f, 0, -z + .025f), V(-x + .025f, 0, z - .025f),
                        V(x - chip, 0, z - .025f), V(x - .025f, 0, z - chip), V(x - .025f, 0, -z + .025f) };
                    int hex = (row + col) % 4 == 0 ? 0x777063 : (row % 2 == 0 ? 0x785f4c : 0x685544);
                    Color tileColor = b.Jitter(hex, .10f);
                    for (int k = 0; k < 5; k++)
                    {
                        int n = (k + 1) % 5;
                        b.Quad(center + edge[k], center + edge[n], center + tile[n], center + tile[k], C(0x3c3b31));
                        b.Tri(center, center + tile[k], center + tile[n], tileColor);
                    }
                }
            // Broken fascia, exposed nail heads and trapped fragments below the walking deck.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                {
                    b.Box(V(-1.8f + k * 3.6f, .86f, side * 2.37f), V(.64f, .09f, .04f), b.Jitter(0x403a31));
                    b.Beam(V(-1.9f + k * 3.6f, .85f, side * 2.37f), V(-1.86f + k * 3.6f, .94f, side * 2.38f), .014f, .009f, 3, C(0x796047));
                }
            b.Quad(V(-2.98f, 0, -2.18f), V(2.98f, 0, -2.18f), V(2.98f, 0, 2.18f), V(-2.98f, 0, 2.18f), C(0x30342b));
            return b.Build("drowned roof");
        }

        public static Mesh EggShell()
        {
            var b = new MeshBuilder(615);
            const int count = 24;
            var baseRing = new Vector3[count]; var belly = new Vector3[count];
            var broken = new Vector3[count]; var floor = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                baseRing[i] = SquarePoint(i, 6, .74f, .74f, 0);
                Vector3 p = SquarePoint(i, 6, 1.39f, 1.39f, .55f);
                float corner = Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.z)) / 1.39f;
                belly[i] = V(p.x * (1 - .13f * corner * corner), p.y, p.z * (1 - .13f * corner * corner));
                // Only the narrow outer lip is fractured; the full 1.3 m half-box is intact.
                broken[i] = SquarePoint(i, 6, b.Range(1.32f, 1.4f), b.Range(1.32f, 1.4f), i % 3 == 0 ? .95f : b.Range(.80f, .92f));
                floor[i] = SquarePoint(i, 6, 1.3f, 1.3f, .95f);
            }
            for (int i = 0; i < count; i++)
            {
                int n = (i + 1) % count;
                b.Quad(baseRing[i], baseRing[n], belly[n], belly[i], b.Jitter(0x6e7359, .12f));
                b.Quad(belly[i], belly[n], broken[n], broken[i], b.Jitter(i % 4 == 0 ? 0x888b6d : 0xaaa58a));
                b.Quad(broken[i], broken[n], floor[n], floor[i], b.Jitter(0xc3bca0, .07f));
                // Empty, crushed-open cup: pale inner membrane forms the standing floor.
                // Two concentric, coplanar regions suggest the scraped interior without
                // making the player hover over an unsupported concave bowl.
                Vector3 a = V(floor[i].x, 0, floor[i].z).normalized * 1.02f + V(0, .95f, 0);
                Vector3 c = V(floor[n].x, 0, floor[n].z).normalized * 1.02f + V(0, .95f, 0);
                b.Quad(floor[i], floor[n], c, a, b.Jitter(i % 7 == 0 ? 0x95967a : 0xc5bda1, .025f));
                b.Tri(V(.07f, .95f, -.09f), a, c, b.Jitter(0xa6a18a, .025f));
                b.Tri(Vector3.zero, baseRing[n], baseRing[i], C(0x555e46));
            }
            return b.Build("egg shell");
        }

        public static Mesh SludgeBall()
        {
            var b = new MeshBuilder(612);
            // Ground-origin, roughly one metre tall. Silhouette supplies all detail because
            // the existing opaque unlit material deliberately ignores vertex colours.
            b.Rock(V(0, .52f, 0), .41f, Color.white, .14f, 1, 1);
            b.Rock(V(-.28f, .65f, .12f), .145f, Color.white, .06f, 1, 0);
            b.Rock(V(.20f, .80f, -.07f), .13f, Color.white, .06f, 1, 0);
            b.Rock(V(.22f, .43f, .27f), .12f, Color.white, .06f, 1, 0);
            b.Cylinder(V(.08f, 0, .10f), .012f, .10f, .29f, 4, Color.white);
            b.Beam(V(-.21f, .31f, -.09f), V(-.24f, .10f, -.07f), .085f, .018f, 4, Color.white);
            return b.Build("sludge ball");
        }
    }
}
