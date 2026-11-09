using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 엘리트 기믹 파훼 (기획서/Z0_생태계_엘리트_파츠.md, 2026-11-09). Hints are motion / light / terrain only, no text.
    //  무리의 우두머리 (houndAlpha)
    //   - 4 cracked pillars stand in the lair. Dodge its big lunge in front of one: it crashes, stunned 4 s (takes 2x),
    //     and the pillar breaks (4 chances).
    //   - while its pack lives, cyan soul tethers run hound -> alpha and it takes 50 % damage: clear the pack first.
    //   - howl (circle) raises one fallen hound; any hit during the howl wind-up (its throat glows) cuts it off.
    //  부서진 집행자 (executor)
    //   - armoured front: 30 % damage (sparks, clang); cracked core on its back: 1.5x. It turns very slowly (turn 1.05).
    //   - the last slam of its 3-hit combo sticks in the ground: pinned 3 s, back hits 2x.
    //   - every 25 % of max HP dealt to the core: it kneels 5 s (2x from anywhere), then attacks faster (wind-ups x0.75).
    public partial class SoulGame
    {
        class EliteRig
        {
            public Enemy e; public Territory t;
            public float stunUntil, pinnedUntil, kneelUntil, coreDmg;
            public bool enraged;
            public Material glowMat; public GameObject glow;
            // alpha
            public readonly List<(Vector3 pos, GameObject go, List<WorldCollision.Col> cols)> pillars = new List<(Vector3, GameObject, List<WorldCollision.Col>)>();
            public readonly List<LineRenderer> tethers = new List<LineRenderer>();
            public bool linked;
        }
        readonly List<EliteRig> elites = new List<EliteRig>();
        const float PILLAR_SCALE = 1.7f;   // lair pillars sized for the near-boss-scale alpha
        bool eliteCam;
        static Mesh lairPillarMesh, glowBlob; Material tetherMat;
        bool elitesHooked;

        EliteRig RigOf(Enemy e) { foreach (var r in elites) if (r.e == e) return r; return null; }

        void EliteHooks()
        {
            elitesHooked = true;
            tetherMat = Mats.Unlit(new Color(.4f, 1.5f, 2.0f, .55f), Blend.Add, false, UnityEngine.Rendering.CullMode.Off, false);
            enemies.onAggro += e =>
            {   // a hit during the howl wind-up cuts it off
                var r = RigOf(e); if (r == null || e.type != "houndAlpha") return;
                if (e.state == "windup" && e.attack != null && e.attack.name == "howl") { e.ToStagger(1.6f); vfx.Burst(e.Center(), 14, .9f); SoulAudio.Play("parry", e.Center(), .8f, .8f); }
            };
            var prevImpact = enemies.onImpact;
            enemies.onImpact = (e, a, p) => { prevImpact?.Invoke(e, a, p); if (e.type == "houndAlpha" && a.name == "howl") HowlRevive(RigOf(e)); };
            enemies.onComboEnd += e =>
            {
                var r = RigOf(e); if (r == null || e.type != "executor") return;
                r.pinnedUntil = realTime + 3; e.ToStagger(3);
                var tip = e.pos + Sim.Forward(e.yaw) * (e.radius + 1.2f);
                vfx.Dust(tip + new Vector3(0, .05f, 0), 1.3f); cam.Shake(.07f); SoulAudio.Play("slam", tip, .8f, .7f);
            };
        }

        /// called by SpawnTerritory for an elite member
        void EliteSetup(Territory t, Enemy e)
        {
            if (!elitesHooked) EliteHooks();
            var r = new EliteRig { e = e, t = t };
            r.glowMat = Mats.Unlit(new Color(.4f, 1.6f, 2.2f, 1), Blend.Opaque);
            if (glowBlob == null) { var gb = new MeshBuilder(91); gb.Rock(Vector3.zero, .5f, Color.white, .25f, 1, 1); glowBlob = gb.Build("elite glow"); }
            float size = e.def.size, h = e.height / size, rad = e.radius / size;
            // stand-in bodies only (no own model): a glow blob marks the weak spot
            GameObject Blob(Vector3 local, float scale)
            {
                var go = new GameObject("elite weak spot"); go.transform.SetParent(e.Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = glowBlob; go.AddComponent<MeshRenderer>().sharedMaterial = r.glowMat;
                go.transform.localPosition = Sim.ToUnity(local); go.transform.localScale = Vector3.one * scale; return go;
            }
            if (e.type == "houndAlpha")
            {   // throat: front, low; lit only during the howl wind-up (the own model has a 'throat' glow mesh)
                var th = e.Model ? e.Model.Part("throat") : null;
                if (th) { r.glow = th.gameObject; if (th.TryGetComponent<MeshRenderer>(out var tr)) tr.sharedMaterial = r.glowMat; }
                else r.glow = Blob(new Vector3(0, h * .55f, rad * .95f), .45f);
                r.glow.SetActive(false);
                BuildPillars(r);
                e.hitScale = (amt, from) => (r.linked ? .5f : 1) * (realTime < r.stunUntil ? 2 : 1);
            }
            else if (e.type == "executor")
            {   // cracked core on its back (the own model has it there; the stand-in warden gets a blob)
                if (!e.custom) r.glow = Blob(new Vector3(0, h * .7f, -rad * .55f), .4f);
                e.hitScale = (amt, from) => ExecutorHit(r, amt, from);
            }
            elites.Add(r);
        }
        void EliteTeardown(Enemy e)
        {
            var r = RigOf(e); if (r == null) return;
            foreach (var pl in r.pillars) { if (pl.go) Object.Destroy(pl.go); if (pl.cols != null) WorldCollision.RemovePersistent(pl.cols); }
            foreach (var l in r.tethers) if (l) Object.Destroy(l.gameObject);
            if (r.glow && r.glow.name == "elite weak spot") Object.Destroy(r.glow); Object.Destroy(r.glowMat);
            elites.Remove(r);
        }

        // ---------- 무리의 우두머리 ----------
        void BuildPillars(EliteRig r)
        {
            if (lairPillarMesh == null) lairPillarMesh = CritterMeshes.LairPillar();   // Codex 17
            var t = r.t; var c = new Vector3(t.c.x, 0, t.c.y);
            for (int k = 0; k < 4; k++)
            {
                Vector3? at = null;
                for (int tries = 0; tries < 40 && !at.HasValue; tries++)
                {   // around its quarter of the ring, 7-18 m out
                    float a = Mathf.PI / 4 + k * Mathf.PI / 2 + ((tries % 8) - 3.5f) * .12f, rr = 13 + (tries / 8) * 3.2f;   // 13-26 m: room for a 7.5 m beast
                    float x = c.x + Mathf.Sin(a) * rr, z = c.z + Mathf.Cos(a) * rr;
                    if (Territories.Open(t.zone, x, z)) at = new Vector3(x, Terrain.Height(x, z), z);
                }
                if (!at.HasValue) continue;
                var go = new GameObject("lair pillar"); go.transform.SetParent(scene.actors, false);
                go.transform.localPosition = Sim.ToUnity(at.Value); go.transform.localRotation = Sim.YawToUnity(k * 1.7f); go.transform.localScale = Vector3.one * PILLAR_SCALE;
                go.AddComponent<MeshFilter>().sharedMesh = lairPillarMesh; var pm = Mats.Lit(Color.white, Color.black, .05f, .1f, true); pm.SetFloat("_Cutaway", 1); go.AddComponent<MeshRenderer>().sharedMaterial = pm;
                var cols = new List<WorldCollision.Col> { new WorldCollision.Col { c = new Vector2(at.Value.x, at.Value.z), r = .95f * PILLAR_SCALE } };
                WorldCollision.AddPersistent(cols, null);
                r.pillars.Add((at.Value, go, cols));
            }
        }
        void HowlRevive(EliteRig r)
        {
            if (r == null) return;
            var t = r.t;
            for (int i = 0; i < t.members.Length; i++)
            {
                if (!t.dead[i] || t.IsElite(i)) continue;
                var at = r.e.pos + Sim.Forward(r.e.yaw + 2.2f) * 3;
                var h = enemies.SpawnAt(t.members[i], at); h.territory = t; h.member = i; h.home = at; t.dead[i] = false; t.live.Add(h);
                return;
            }
        }
        void AlphaUpdate(EliteRig r, float dt)
        {
            var e = r.e;
            // crash: the big lunge into a pillar
            if (e.state == "attack" && e.attack != null && e.attack.name == "lunge")
                for (int i = 0; i < r.pillars.Count; i++)
                {
                    var pl = r.pillars[i]; if (pl.go == null) continue;
                    // the pillar lies on its charge line within 3 m ahead (collision stops the body before contact)
                    var f = Sim.Forward(e.yaw); var rel = Sim.FlatY(pl.pos - e.pos); float along = Vector3.Dot(rel, f);
                    if (along < 0 || along > e.radius + 3 || (rel - f * along).magnitude > e.radius * .8f + .95f * PILLAR_SCALE) continue;
                    r.stunUntil = realTime + 4; e.ToStagger(4);
                    vfx.Dust(pl.pos + new Vector3(0, .3f, 0), 1.8f); vfx.Burst(pl.pos + Vector3.up * 1.6f, 26, 1.3f); cam.Shake(.13f);
                    SoulAudio.Play("pillarBreak", pl.pos);
                    Object.Destroy(pl.go); WorldCollision.RemovePersistent(pl.cols);
                    r.pillars[i] = (pl.pos, null, null);
                    break;
                }
            // pack tethers
            var pack = new List<Enemy>(); foreach (var o in r.t.live) if (o != e && o.alive && o.state != "return") pack.Add(o);
            r.linked = pack.Count > 0 && e.alive;
            while (r.tethers.Count < pack.Count)
            {
                var lr = new GameObject("soul tether").AddComponent<LineRenderer>(); lr.transform.SetParent(scene.actors, false);
                lr.sharedMaterial = tetherMat; lr.widthMultiplier = .07f; lr.positionCount = 8; lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.tethers.Add(lr);
            }
            for (int i = 0; i < r.tethers.Count; i++)
            {
                var lr = r.tethers[i]; bool on = i < pack.Count; lr.enabled = on; if (!on) continue;
                Vector3 a = pack[i].Center(), b = e.Center();
                for (int k = 0; k < 8; k++)
                {
                    float u = k / 7f; var q = Vector3.Lerp(a, b, u);
                    q.y += Mathf.Sin(u * Mathf.PI) * .8f + Mathf.Sin(realTime * 6 + k + i) * .08f;
                    lr.SetPosition(k, Sim.ToUnity(q));
                }
            }
            // throat glow: only while howling
            bool howl = e.state == "windup" && e.attack != null && e.attack.name == "howl";
            r.glow.SetActive(howl);
            if (howl) Mats.SetColor(r.glowMat, new Color(2.6f, 1.2f, .3f, 1) * (1 + .5f * Mathf.Sin(realTime * 18)));
        }

        // ---------- 부서진 집행자 ----------
        float ExecutorHit(EliteRig r, float amt, Vector3 from)
        {
            var e = r.e; var d = Sim.FlatY(from - e.pos); if (d.sqrMagnitude < 1e-4f) return 1;
            float dot = Vector3.Dot(d.normalized, Sim.Forward(e.yaw));
            bool kneel = realTime < r.kneelUntil, pinned = realTime < r.pinnedUntil;
            if (kneel) return 2;
            if (dot > .3f)
            {   // armour: sparks and a clang, the hit glances off
                vfx.Burst(e.Center() + Sim.Forward(e.yaw) * e.radius, 10, .6f); SoulAudio.Play("parry", e.Center(), .5f, 1.4f);
                return .3f;
            }
            if (dot < -.3f)
            {   // the cracked core
                float m = pinned ? 2 : 1.5f; r.coreDmg += amt * m;
                if (r.coreDmg >= e.def.hp * .25f)
                {
                    r.coreDmg -= e.def.hp * .25f; r.kneelUntil = realTime + 5; e.ToStagger(5);
                    r.enraged = true; e.windupScale = .75f; e.eyeTint = new Color(1.1f, .3f, 1.7f, 1);
                    vfx.Burst(e.Center(), 30, 1.4f); cam.Shake(.1f); SoulAudio.Play("pillarBreak", e.Center(), .9f, .7f);
                }
                return m;
            }
            return 1;
        }
        void ExecutorUpdate(EliteRig r)
        {
            bool open = realTime < r.pinnedUntil || realTime < r.kneelUntil;
            float k = (r.enraged ? 1.5f : 1) * (open ? 1.8f + .6f * Mathf.Sin(realTime * 14) : .9f + .2f * Mathf.Sin(realTime * 3));
            if (r.glow) Mats.SetColor(r.glowMat, new Color(1.5f, .45f, 2.4f, 1) * k);
            r.e.coreBoost = r.glow ? 1 : k * 1.2f;
        }

        void ElitesUpdate(float dt)
        {
            // field-boss framing: while an elite fights within 40 m (and no arena boss), the camera eases out and
            // centres between player and elite, like the boss fight (FollowCam.fight*)
            Enemy near = null; float nd = 40;
            foreach (var r in elites) if (r.e.Engaged) { float d = Sim.FlatY(r.e.pos - ch.root.position).magnitude; if (d < nd) { nd = d; near = r.e; } }
            if (fightArena == null)
            {
                if (near != null) { cam.fightTarget = 1; cam.fightFocus = near.pos; cam.fightZoom = near.type == "houndAlpha" ? 1.15f : 1.3f; eliteCam = true; }
                else if (eliteCam) { cam.fightTarget = 0; eliteCam = false; }
            }
            for (int i = elites.Count - 1; i >= 0; i--)
            {
                var r = elites[i];
                if (!r.e.alive || !r.e.Exists) { EliteTeardown(r.e); continue; }
                if (r.e.type == "houndAlpha") AlphaUpdate(r, dt); else if (r.e.type == "executor") ExecutorUpdate(r);
            }
        }

        /// MCP tests
        public string EliteState()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in elites)
            {
                int standing = 0; foreach (var p in r.pillars) if (p.go != null) standing++;
                sb.Append($"{r.e.type} hp {r.e.hp:F0} state {r.e.state} linked {r.linked} pillars {standing} stun {Mathf.Max(0, r.stunUntil - realTime):F1} pinned {Mathf.Max(0, r.pinnedUntil - realTime):F1} kneel {Mathf.Max(0, r.kneelUntil - realTime):F1} core {r.coreDmg:F0} enraged {r.enraged}\n");
            }
            return sb.ToString();
        }
    }
}
