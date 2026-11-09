using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 영역 생태계 runtime (기획서/Z0_생태계_엘리트_파츠.md, 2026-11-09). World/Territories.cs holds the tables.
    //  - streaming: a territory's members appear (quietly, roaming) when the player comes within r + 120 m and are
    //    cleared beyond r + 200 m unless fighting. Kills are remembered per member.
    //  - life: roamers wander their territory; hounds hunt the prey critters (재 쥐) living there and drink them.
    //    Turrets stand. Sight per type: a member that sees the player engages, and in a pack / lair / nest all do.
    //  - leash: led past r + 32 m (or dragged past r + 48 m) they give up and walk home, healing on the way.
    //  - rest at a pod: every non-elite comes back. Elites are permanent kills and drop their reward:
    //    무리의 우두머리 -> 도약 관절 (double jump) + 40 scrap, 부서진 집행자 -> 레이저 파동 + 40 scrap.
    //  - caches: crates on 2.1 m mesas (Caches.Spots), opened by walking onto them (double jump needed).
    public partial class SoulGame
    {
        Critters critters; float ecoClock; bool ecoInit;
        readonly System.Random ecoRng = new System.Random(2209);
        readonly Dictionary<Enemy, float> roamUntil = new Dictionary<Enemy, float>();
        readonly Dictionary<Enemy, Critters.C> hunting = new Dictionary<Enemy, Critters.C>();
        readonly Dictionary<Territory, float> critterClock = new Dictionary<Territory, float>();
        readonly List<Vector3> threatTmp = new List<Vector3>();
        class Drop { public Vector3 pos; public string kind; public GameObject go; public float t; }
        readonly List<Drop> drops = new List<Drop>();
        GameObject[] crates; Material crateGlow, dropGlow;
        /// ecology on/off (tests, the old summon-only sandbox)
        public bool ecology = true;

        void EcologyInit()
        {
            ecoInit = true;
            critters = new Critters(scene.actors);
            enemies.onAggro += e => { if (e.territory is Territory t) Alert(t, e); };
            var prevKill = enemies.onKill;
            enemies.onKill = e => { prevKill?.Invoke(e); OnFieldKill(e); };
            crateGlow = Mats.Unlit(new Color(.5f, 1.6f, 2.1f, 1), Blend.Opaque);
            dropGlow = Mats.Unlit(new Color(2.2f, 1.7f, .6f, 1), Blend.Opaque);
            crates = new GameObject[Caches.Spots.Length];
        }

        void EcologyUpdate(float dt)
        {
            if (!ecoInit) EcologyInit();
            var p = ch.root.position; var pp = new Vector2(p.x, p.z);
            // ---- streaming (twice a second) ----
            ecoClock -= dt;
            if (ecoClock <= 0)
            {
                ecoClock = .5f;
                foreach (var t in Territories.All)
                {
                    float d = (t.want - pp).magnitude;
                    if (d > t.r + 320) { if (t.spawned) Despawn(t); continue; }
                    Territories.Place(t); d = (t.c - pp).magnitude;
                    if (!t.spawned && ecology && d < t.r + 120) SpawnTerritory(t);
                    else if (t.spawned && (d > t.r + 200 || !ecology) && !AnyEngaged(t)) Despawn(t);
                }
                CachesStream(pp);
            }
            if (!ecology) return;
            // ---- life ----
            threatTmp.Clear();
            foreach (var t in Territories.All)
            {
                if (!t.spawned) continue;
                t.live.RemoveAll(e => !e.alive);
                float pd = (t.c - pp).magnitude;
                foreach (var e in t.live)
                {
                    float de = Sim.FlatY(e.pos - p).magnitude; bool level = Mathf.Abs(e.pos.y - p.y) < 2.5f;
                    if (!e.Engaged)
                    {
                        bool sees = e.def.sight > 0 && de < e.def.sight && level && player.alive && !enemies.passive && (e.state != "return" || pd < t.r + 10);
                        if (sees) { Alert(t, e); continue; }
                        if (e.state == "roam") Roam(t, e, dt);
                    }
                    else
                    {   // leash: too far from home ground -> walk back
                        var ec = new Vector2(e.pos.x, e.pos.z);
                        float extra = e.def.eliteName != null ? 13 : 0;
                        if (pd > t.r + 32 + extra || (ec - t.c).magnitude > t.r + 48 + extra || !player.alive) { e.ReturnHome(); hunting.Remove(e); }
                    }
                    if (e.roamFast && e.state == "roam") threatTmp.Add(e.pos);
                }
                // prey upkeep: refill the nest slowly while the player is away
                if (t.critters > 0)
                {
                    int have = 0; foreach (var c in critters.list) if (c.home == t) have++;
                    critterClock.TryGetValue(t, out float cc); cc -= dt; critterClock[t] = cc;
                    if (have < t.critters && cc <= 0 && pd > 35) { SpawnCritter(t); critterClock[t] = 20; }
                }
            }
            critters.Update(dt, p, threatTmp);
            ElitesUpdate(dt); FieldUpdate(dt); MireUpdate(dt);
            // the player can catch a critter by striking next to it
            if (anim.busy && (anim.name.StartsWith("light") || anim.name == "heavy" || anim.name == "flurry"))
                foreach (var c in critters.list.ToArray()) if (Sim.FlatY(c.pos - p).magnitude < 1.4f) { enemies.Wisp(c.pos + Vector3.up * .2f, .4f); critters.Kill(c); }
            DropsUpdate(dt, p);
        }

        static bool AnyEngaged(Territory t) { foreach (var e in t.live) if (e.Engaged) return true; return false; }
        void Alert(Territory t, Enemy e)
        {
            e.Engage(); hunting.Remove(e);
            if (!t.Pack) return;
            foreach (var o in t.live) if (o != e && !o.Engaged) { o.Engage(); hunting.Remove(o); }
        }

        void SpawnTerritory(Territory t)
        {
            t.spawned = true;
            for (int i = 0; i < t.members.Length; i++)
            {
                if (t.dead[i]) continue;
                bool solo = t.kind == "turret" || (t.IsElite(i) && t.kind == "lair");
                var at = solo ? new Vector3(t.c.x, Terrain.Height(t.c.x, t.c.y), t.c.y) : Territories.PointIn(t, .55f, ecoRng);
                if (!at.HasValue) at = new Vector3(t.c.x + (i - 1) * 1.5f, 0, t.c.y);
                var e = enemies.SpawnQuiet(t.members[i], at.Value, (float)ecoRng.NextDouble() * Mathf.PI * 2);
                e.territory = t; e.member = i; t.live.Add(e);
                if (e.def.eliteName != null) EliteSetup(t, e); else MaybeSpecial(e);   // 특이 개체 (SoulGame.Field)   // gimmicks (SoulGame.Elites)
                roamUntil[e] = realTime + (float)ecoRng.NextDouble() * 4;
            }
            for (int i = 0; i < t.critters; i++) SpawnCritter(t);
        }
        void SpawnCritter(Territory t)
        {
            var at = Territories.PointIn(t, .6f, ecoRng); if (!at.HasValue) return;
            critters.Spawn(at.Value, t, t.c, t.r * .6f);
        }
        void Despawn(Territory t)
        {
            foreach (var e in t.live) { if (e.alive) e.Despawn(); roamUntil.Remove(e); hunting.Remove(e); }
            t.live.Clear(); t.spawned = false; critters.Remove(t);
        }

        /// roaming: wander points inside the territory; hounds go after prey; turrets stay put
        void Roam(Territory t, Enemy e, float dt)
        {
            if (e.def.speed <= 0) { e.roamTarget = null; return; }
            // hunt: hounds chase the nearest prey of their ground and drink it on contact
            if (e.hound && e.def.eliteName == null)
            {
                if (hunting.TryGetValue(e, out var prey) && (!prey.alive || !critters.list.Contains(prey))) { hunting.Remove(e); prey = null; e.roamFast = false; }
                if (prey == null)
                    foreach (var c in critters.list)
                        if (Sim.FlatY(c.pos - e.pos).magnitude < 14 && (new Vector2(c.pos.x, c.pos.z) - t.c).magnitude < t.r + 10) { prey = c; hunting[e] = c; break; }
                if (prey != null && (new Vector2(e.pos.x, e.pos.z) - t.c).magnitude > t.r + 12)
                {   // the prey got away past the edge of the hunting ground: give up, head back in
                    hunting.Remove(e); prey = null; e.roamFast = false;
                    e.roamTarget = Territories.PointIn(t, .5f, ecoRng); roamUntil[e] = realTime + 6; return;
                }
                if (prey != null)
                {
                    e.roamTarget = prey.pos; e.roamFast = true;
                    if (Sim.FlatY(prey.pos - e.pos).magnitude < e.radius + .5f)
                    {   // caught: the machine drinks the organism's soul
                        for (int k = 0; k < 4; k++) enemies.Wisp(prey.pos + Vector3.up * .2f, .5f, true);
                        critters.Kill(prey); hunting.Remove(e); e.roamFast = false; e.roamTarget = null; roamUntil[e] = realTime + 3;
                    }
                    return;
                }
            }
            e.roamFast = false;
            roamUntil.TryGetValue(e, out float until);
            bool arrived = e.roamTarget.HasValue && Sim.FlatY(e.roamTarget.Value - e.pos).magnitude < 1.2f;
            if (realTime >= until || (arrived && realTime >= until - 4))
            {
                bool idle = ecoRng.NextDouble() < .35f;   // sometimes just stand and look around
                e.roamTarget = idle ? (Vector3?)null : Territories.PointIn(t, e.def.eliteName != null ? .35f : .8f, ecoRng);
                roamUntil[e] = realTime + 3 + (float)ecoRng.NextDouble() * 6;
            }
        }

        void OnFieldKill(Enemy e)
        {
            FieldKill(e);   // drops, rescued prey (SoulGame.Field) - before the hunt record is cleared
            hunting.Remove(e); roamUntil.Remove(e);
            if (!(e.territory is Territory t) || e.member < 0) return;
            t.dead[e.member] = true;
            if (e.def.eliteName == null) return;
            MireKill(e);   // Z6: the dead shell becomes a platform, its core lies on the hidden ledge
            // elite reward: its part + 40 extra scrap (8 x 5)
            pickups.Burst(e.Center(), 8, 5);
            var kind = e.type == "houndAlpha" ? "leap" : e.type == "executor" ? "skill:wave" : e.type == "giantDredger" ? "skill:suck" : null;
            if (kind != null) SpawnDrop(kind, e.pos + Sim.Forward(e.yaw) * 1.5f);
        }
        void SpawnDrop(string kind, Vector3 at)
        {
            at.y = WorldCollision.Floor(at.x, at.z, at.y + 1, 1.5f);
            var go = new GameObject("drop " + kind); go.transform.SetParent(scene.actors, false);
            go.AddComponent<MeshFilter>().sharedMesh = CritterMeshes.PartDrop(kind); go.AddComponent<MeshRenderer>().sharedMaterial = dropGlow;
            drops.Add(new Drop { pos = at, kind = kind, go = go });
        }
        void DropsUpdate(float dt, Vector3 p)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i]; d.t += dt;
                d.go.transform.localPosition = Sim.ToUnity(d.pos + Vector3.up * (.35f + .12f * Mathf.Sin(d.t * 2.4f)));
                d.go.transform.localRotation = Sim.YawToUnity(d.t * 1.2f);
                if (Random.value < dt * 5) enemies.Wisp(d.pos + Vector3.up * .5f, .3f);
                if (Sim.FlatY(d.pos - p).magnitude > 1.6f || Mathf.Abs(d.pos.y - p.y) > 1.5f || !player.alive) continue;
                Collect(d); Object.Destroy(d.go); drops.RemoveAt(i);
            }
        }
        void Collect(Drop d)
        {
            vfx.Ring(Sim.FlatY(d.pos, .3f), 1.4f, true); vfx.Flash(d.pos + Vector3.up, 1);
            SoulAudio.Play("shrine", d.pos);
            if (d.kind == "leap") { Parts.Give(Parts.Leap); Cue("도약 관절 · 점프 중에 한 번 더 점프", 3.5f); }
            else if (d.kind.StartsWith("skill:")) GrantSkill(d.kind.Substring(6));
            else if (d.kind == "core:toxic") { Parts.Give(Parts.ToxicCore); Cue("독에 잠식된 코어 · 공격에 독이 묻는다 · 칼날이 보라색으로 물든다", 4); }
        }

        // ---- caches ----
        void CachesStream(Vector2 pp)
        {
            for (int i = 0; i < Caches.Spots.Length; i++)
            {
                var s = Caches.Spots[i]; bool near = (s.c - pp).magnitude < 120 && !Caches.opened[i];
                if (near && crates[i] == null)
                {
                    var g = new GameObject("cache " + s.name); g.transform.SetParent(scene.actors, false);
                    var top = new Vector3(s.c.x, Terrain.Height(s.c.x, s.c.y), s.c.y);
                    g.transform.localPosition = Sim.ToUnity(top); g.transform.localRotation = Sim.YawToUnity(i * 1.3f);
                    g.AddComponent<MeshFilter>().sharedMesh = CritterMeshes.Crate(); g.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, .2f, .2f, true);
                    var gl = new GameObject("glow"); gl.transform.SetParent(g.transform, false);
                    gl.AddComponent<MeshFilter>().sharedMesh = CritterMeshes.CrateGlow(); gl.AddComponent<MeshRenderer>().sharedMaterial = crateGlow;
                    crates[i] = g;
                }
                else if (!near && crates[i] != null) { Object.Destroy(crates[i]); crates[i] = null; }
            }
            // open: standing on the mesa top next to the crate
            var p = ch.root.position;
            for (int i = 0; i < Caches.Spots.Length; i++)
            {
                if (Caches.opened[i] || crates[i] == null) continue;
                var s = Caches.Spots[i]; var top = Terrain.Height(s.c.x, s.c.y);
                if ((s.c - pp).magnitude < 2.2f && Mathf.Abs(p.y - top) < .6f)
                {
                    Caches.opened[i] = true; pickups.Burst(new Vector3(s.c.x, top + .6f, s.c.y), s.scrap / 5, 5);
                    vfx.Flash(new Vector3(s.c.x, top + .8f, s.c.y), 1.2f); SoulAudio.Play("pickup", p);
                    Object.Destroy(crates[i]); crates[i] = null;
                }
            }
        }

        /// pod rest: every non-elite member comes back (territories in range respawn on the next tick)
        void RespawnField()
        {
            foreach (var t in Territories.All)
            {
                if (t.dead != null) for (int i = 0; i < t.dead.Length; i++) if (!t.IsElite(i)) t.dead[i] = false;
                if (t.spawned) Despawn(t);
            }
        }

        // ---- elite bar ----
        void EcologyGUI(float W, float H)
        {
            Enemy el = null; float best = 34;
            foreach (var e in enemies.list) if (e.def.eliteName != null && e.Engaged) { float d = Sim.FlatY(e.pos - ch.root.position).magnitude; if (d < best) { best = d; el = e; } }
            if (el == null) return;
            float bw = Mathf.Min(W * .36f, 440), x = (W - bw) / 2, y = 58;
            GUI.color = new Color(1, .93f, .85f, 1); GUI.Label(new Rect(x, y - 22, bw, 20), el.def.eliteName, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold });
            GUI.color = new Color(0, 0, 0, .7f); GUI.DrawTexture(new Rect(x, y, bw, 8), white);
            GUI.color = new Color(.85f, .35f, .12f, 1); GUI.DrawTexture(new Rect(x + 1, y + 1, (bw - 2) * Mathf.Max(0, el.hpFrac), 6), white);
            GUI.color = Color.white;
        }

        // ---- MCP tests ----
        public string EcologyReport()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in Territories.All)
            {
                if (!t.placed) continue;
                int alive = 0, eng = 0, ret = 0; foreach (var e in t.live) { if (e.alive) alive++; if (e.Engaged) eng++; if (e.state == "return") ret++; }
                int prey = 0; foreach (var c in critters.list) if (c.home == t) prey++;
                sb.Append($"{t.name} ({t.c.x:F0},{t.c.y:F0}) spawned {t.spawned} live {alive} engaged {eng} return {ret} prey {prey} dead {System.Array.FindAll(t.dead, b => b).Length}\n");
            }
            return sb.ToString();
        }
        /// step only the ecology + enemies (no player input), for MCP tests
        public void EcologyTick(float dt) { player.pos = ch.root.position; EcologyUpdate(dt); enemies.Update(dt, player); }
        public void RespawnFieldForTest() => RespawnField();
    }
}
