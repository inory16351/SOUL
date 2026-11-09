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
    //  독에 잠식된 코어 (Parts.ToxicCore): every hit poisons for 3 s; the blade turns violet (LaserBlade.toxic).
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
            if (mireBall == null) { var b = new MeshBuilder(612); b.Rock(Vector3.zero, .5f, Color.white, .3f, 1, 1); mireBall = b.Build("sludge ball"); }
            var prevImpact = enemies.onImpact;
            enemies.onImpact = (e, a, p) => { prevImpact?.Invoke(e, a, p); MireImpact(e, a, p); };
            enemies.onAggro += e => { if (Parts.Has(Parts.ToxicCore) && e.alive) e.poisonUntil = realTime + 3; };   // 독에 잠식된 코어
        }

        void MireUpdate(float dt)
        {
            if (!mireInit) MireInit();
            LaserBlade.toxic = Parts.Has(Parts.ToxicCore);
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
        const float SHELL_TOP = 2.2f, SHELL_HALF = 3.6f;   // top 2.2 m over the pool floor: bank -> shell and shell -> ledge both within the 2.36 m double jump
        void ShellPlatform()
        {
            if (shellBuilt) return;
            shellBuilt = true;
            var c = MirePools.Host; float floor = Terrain.Height(c.x, c.y), top = floor + SHELL_TOP;
            var surfs = new List<WorldCollision.Surf> { new WorldCollision.Surf { kind = PropSurface.BOX, c = c, half = new Vector2(SHELL_HALF, SHELL_HALF), cos = 1, sin = 0, top = top } };
            var cols = new List<WorldCollision.Col> { new WorldCollision.Col { box = true, c = c, half = new Vector2(SHELL_HALF + .3f, SHELL_HALF + .3f), cos = 1, sin = 0, ranged = true, y0 = floor - 3, y1 = top - .15f } };
            WorldCollision.AddPersistent(cols, surfs);
            vfx.Dust(new Vector3(c.x, top, c.y), 2); cam.Shake(.08f);
        }
        void ShellStream(Vector3 p)
        {
            if (!shellBuilt) return;
            bool near = Sim.FlatY(new Vector3(MirePools.Host.x, 0, MirePools.Host.y) - p).magnitude < 140;
            if (near && shellGo == null)
            {
                if (shellMesh == null)
                {
                    var b = new MeshBuilder(613);
                    b.Rock(new Vector3(0, SHELL_TOP * .55f, 0), SHELL_HALF * 1.35f, new Color(.36f, .33f, .27f), .18f, .5f, 2);
                    for (int k = 0; k < 7; k++) { float a = k * .9f; b.Rock(new Vector3(Mathf.Sin(a) * 2.6f, SHELL_TOP * .9f, Mathf.Cos(a) * 2.6f), .9f, new Color(.3f, .28f, .23f), .3f, .4f, 1); }
                    b.Rock(new Vector3(0, SHELL_TOP - .25f, 0), 2.6f, new Color(.42f, .38f, .3f), .1f, .25f, 1);
                    shellMesh = b.Build("dead shell");
                }
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
            if (roofMesh == null)
            {
                var b = new MeshBuilder(614);
                b.Box(new Vector3(0, ROOF_TOP * .5f - .3f, 0), new Vector3(6.4f, ROOF_TOP + .6f, 4.8f), new Color(.32f, .3f, .27f));
                b.Box(new Vector3(0, ROOF_TOP + .05f, 0), new Vector3(6.8f, .14f, 5.2f), new Color(.4f, .3f, .24f));
                b.Box(new Vector3(-1.6f, ROOF_TOP + .3f, 0), new Vector3(.5f, .5f, .5f), new Color(.25f, .24f, .22f));
                roofMesh = b.Build("drowned roof");
            }
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
            if (eggMesh == null) { var b = new MeshBuilder(615); b.Rock(new Vector3(0, EGG_TOP * .5f, 0), 1.7f, new Color(.62f, .6f, .48f), .15f, .45f, 1); eggMesh = b.Build("egg shell"); }
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
            if (what == "state") return MireState + $" | hosts {hosts.Count} dredges {dredges.Count} puddles {puddles.Count} globs {globs.Count} shell {shellBuilt} core {Parts.Has(Parts.ToxicCore)}";
            if (what == "host")
            {
                if (hosts.Count == 0) return "no host";
                var h = hosts[0]; return $"host hp {h.e.hp:F0} phase {h.phase} sac {h.sac} sets {h.sets} burst {h.burst[0]},{h.burst[1]},{h.burst[2]} stun {Mathf.Max(0, h.stunUntil - realTime):F1} engaged {h.e.Engaged}";
            }
            if (what == "core") { Parts.Give(Parts.ToxicCore); return "core given"; }
            if (what == "suck") { GrantSkill("suck"); return "suck granted"; }
            return "?";
        }
        public void MireTick(float dt) => MireUpdate(dt);
        public bool MireFightTick(float dt) => MireFightUpdate(dt);
    }
}
