using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Z0 boss fight flow + energy pillar gimmick (에너지 기둥). Replaces the tear-off fight: the rope arm is not owned yet in Z0.
    //  intro   : the boss lies switched off → fog closes (StartFight) → pillars rise → light up one by one (blue) →
    //            the boss stands up → name + HP bar.
    //  gimmick : every PILLAR_CYCLE s pillars rise and light up in a random order; break them in that order
    //            (phase 2: red pillars, reverse order) within PILLAR_LIMIT s of the last light. PILLAR_HITS hits each.
    //            Wrong pillar or timeout: the boss drains PILLAR_HEAL of its max HP per pillar left. All in order: it staggers.
    //  phase 2 : phase-1 boss at 0 HP falls (Enemy "down"); four red pillars burst up, its eyes flare red, it rises at PHASE2_HP.
    // Pillar mesh: World/BossPillarProps.cs (placeholder; final model = Codex job 11, unity/codex/11_boss_pillar.md).
    public partial class SoulGame
    {
        public const float PILLAR_CYCLE = 30, PILLAR_LIMIT = 12, PILLAR_HEAL = .08f, PHASE2_HP = .6f, PILLAR_FIRST = 8, STAGGER_REWARD = 2.5f;
        public const int PILLAR_HITS = 3;
        const float PILLAR_R = .6f, PILLAR_H = 3.6f, RISE = 1.6f, SHOW_STEP = .7f, DRAIN = 1.5f, WAKE = 3.6f, FIGHT_ZOOM = 1.1f;
        static readonly Color P_BLUE = new Color(.3f, 1.3f, 3.4f, 1), P_RED = new Color(3.6f, .3f, .15f, 1), EYE_RED = new Color(7, .45f, .2f, 1), EYE_BLUE = new Color(.5f, 1.8f, 4.2f, 1);

        public class Pillar
        {
            public int index, hp = PILLAR_HITS; public Vector3 pos; public bool lit, broken;
            public float rise, up = 1, speed = 1 / RISE, hitFlash, flare;
            public GameObject go; public Material mat; public LineRenderer beam;
            public List<WorldCollision.Col> cols;
        }
        public readonly List<Pillar> pillars = new List<Pillar>();
        /// fight flow: "" (none) · intro · fight · down (phase 1 → 2)
        public string bossFlow = "";
        /// gimmick step while fighting: none · rise · show · active · drain · sink
        public string gimmick = "none";
        public int[] pillarOrder = new int[0]; public int pillarNext, pillarFails, pillarWins; public float flowT, gimmickT, cycleT, lastHeal;
        readonly HashSet<Pillar> pillarHitSet = new HashSet<Pillar>();
        static Mesh pillarMesh, pillarGlowMesh; GameObject boltOrb; Material beamMat;
        public bool BossAwake => bossFlow == "fight" || bossFlow == "down";
        bool roared;
        Color PillarColor => boss != null && boss.phase == 2 ? P_RED : P_BLUE;

        // ---------- dormant boss: lying in the arena whenever you are near and the zone is not purified ----------
        void DormantBoss(Vector3 p)
        {
            if (boss != null && !boss.alive) boss = null;
            foreach (var a in ZoneAreas.Arenas)
            {
                float d = Sim.FlatY(a.center - p).magnitude; bool open = !ZoneState.Purified(a.zone);
                if (boss == null && open && d < a.radius + 60) boss = enemies.SpawnDormant(a.boss, a.center, a.entranceYaw);
                else if (boss != null && boss.state == "dormant" && (!open || d > a.radius + 90)) { boss.Despawn(); boss = null; }
            }
        }

        // ---------- flow ----------
        void PillarsStart(BossArena a)
        {
            PillarsClear(); bossFlow = "intro"; flowT = 0; roared = false; gimmick = "none"; pillarFails = pillarWins = 0; lastHeal = 0;
            SpawnPillars(a.pillars1, 1 / RISE);
            pillarOrder = new int[pillars.Count]; for (int i = 0; i < pillarOrder.Length; i++) pillarOrder[i] = i;   // intro lights in place order
            cam.Shake(.04f);
        }
        void PillarsClear()
        {
            foreach (var p in pillars) KillPillar(p, false);
            pillars.Clear(); bossFlow = ""; gimmick = "none";
            if (boltOrb) boltOrb.SetActive(false);
        }
        public void PillarsTick(float dt) => PillarsUpdate(dt);   // MCP tests (same as ZoneTick, pillars only)
        void PillarsUpdate(float dt)
        {
            BoltOrb();
            // boss framing (FollowCam): ease out so the 9.5 m beast and the player share the screen while fighting
            cam.fightTarget = bossFlow != "" && boss != null && boss.alive ? 1 : 0; cam.fightZoom = FIGHT_ZOOM;
            if (boss != null) cam.fightFocus = boss.pos;
            if (bossFlow == "" || boss == null) { foreach (var p in pillars) PillarVisual(p, dt); return; }
            flowT += dt;
            if (bossFlow == "intro")
            {
                int n = pillars.Count;
                for (int i = 0; i < n; i++) if (!pillars[i].lit && flowT >= RISE + SHOW_STEP * (i + .5f)) Light(pillars[i]);
                if (boss.state == "dormant" && flowT >= RISE + SHOW_STEP * n + .2f)
                {
                    // the pillars' light flows into the machine; its eyes / core come on blue as it stands up
                    boss.eyeTint = EYE_BLUE; boss.Wake(WAKE); cam.Shake(.08f); vfx.Ring(Sim.FlatY(boss.pos, .05f), 3); vfx.Flash(boss.Center(), 1.6f);
                    foreach (var p in pillars) { p.flare = 1; Beam(p, true); }
                }
                if (boss.state == "wake" && boss.roar > .5f && !roared)
                {   // stood up: head raised, jaw wide — the roar
                    roared = true; cam.Shake(.2f); vfx.Flash(boss.CoreWorld(), 2.6f); vfx.Ring(boss.CoreWorld(), 3, true); vfx.Ring(Sim.FlatY(boss.pos, .05f), 5);
                    foreach (var p in pillars) Beam(p, false);
                }
                if (boss.state == "chase")
                {   // standing: name card + HP bar, intro pillars go back into the ground, first gimmick PILLAR_FIRST s later
                    bossFlow = "fight"; bossIntro = 0; cycleT = PILLAR_CYCLE - PILLAR_FIRST;
                    foreach (var p in pillars) { p.up = 0; p.speed = 1 / .8f; p.lit = false; Beam(p, false); }
                    gimmick = "sink";
                }
            }
            else if (bossFlow == "fight")
            {
                if (boss.state == "down") EnterDown();
                else Gimmick(dt);
            }
            else if (bossFlow == "down")
            {
                if (pillars.Count == 0 && flowT >= 1.4f)
                {   // sudden: four red pillars burst up, eyes flare red
                    var a = fightArena; SpawnPillars(a.pillars2, 1 / .45f);
                    foreach (var p in pillars) p.flare = 1.6f;
                    boss.eyeTint = EYE_RED; boss.phase = 2;
                    vfx.Flash(boss.Center() + Vector3.up, 2.4f); vfx.Ring(Sim.FlatY(boss.pos, .05f), 4, true); cam.Shake(.14f); hitStop = Mathf.Max(hitStop, .12f);
                    Cue("감시자의 눈이 붉게 타오른다", 2.4f);
                }
                if (boss.state == "down" && flowT >= 2.1f) boss.Revive(PHASE2_HP, 1.8f);
                if (boss.state == "chase")
                {   // phase 2 starts: these pillars are the first red gimmick, order shown right away
                    bossFlow = "fight"; cycleT = 0; StartOrder(); gimmick = "show"; gimmickT = 0;
                    Cue("붉은 기둥 · 켜진 순서의 반대로 부숴라", 2.6f);
                }
            }
            foreach (var p in pillars) PillarVisual(p, dt);
        }
        void Gimmick(float dt)
        {
            cycleT += dt; gimmickT += dt; int n = pillars.Count;
            if (gimmick == "none")
            {
                if (cycleT >= PILLAR_CYCLE)
                {
                    cycleT = 0; gimmickT = 0; gimmick = "rise";
                    foreach (var p in pillars) KillPillar(p, false); pillars.Clear();
                    SpawnPillars(boss.phase == 1 ? fightArena.pillars1 : fightArena.pillars2, 1 / RISE); StartOrder();
                    Cue(boss.phase == 1 ? "에너지 기둥 · 빛나는 순서대로 부숴라" : "붉은 기둥 · 켜진 순서의 반대로 부숴라", 2.2f);
                }
            }
            else if (gimmick == "rise") { if (gimmickT >= RISE) { gimmick = "show"; gimmickT = 0; } }
            else if (gimmick == "show")
            {
                for (int k = 0; k < n; k++) { var p = pillars[pillarOrder[k]]; if (!p.lit && gimmickT >= SHOW_STEP * (k + .5f)) Light(p); }
                if (gimmickT >= SHOW_STEP * n + .3f) { gimmick = "active"; gimmickT = 0; }
            }
            else if (gimmick == "active") { if (gimmickT >= PILLAR_LIMIT) PillarFail("시간 초과"); }
            else if (gimmick == "drain")
            {
                if (Time.frameCount % 6 == 0) vfx.Burst(boss.CoreWorld(), 4, .5f);
                if (gimmickT >= DRAIN)
                {
                    int left = 0; foreach (var p in pillars) if (!p.broken) left++;
                    lastHeal = PILLAR_HEAL * boss.def.hp * left; boss.Heal(lastHeal);
                    vfx.Flash(boss.Center(), 1.8f); vfx.Ring(Sim.FlatY(boss.pos, .05f), 2.4f, true);
                    Cue($"감시자가 회복했다 · +{Mathf.RoundToInt(PILLAR_HEAL * 100 * left)}%", 1.8f);
                    foreach (var p in pillars) { p.up = 0; p.speed = 1 / .8f; p.lit = false; if (p.beam) p.beam.enabled = false; }
                    gimmick = "sink";
                }
            }
            if (gimmick == "sink")
            {
                bool gone = true; foreach (var p in pillars) if (!p.broken && p.rise > 0) gone = false;
                if (gone) { foreach (var p in pillars) KillPillar(p, false); pillars.Clear(); gimmick = "none"; }
            }
        }
        void EnterDown()
        {   // phase 1 knockout: drop any running gimmick (no heal), lie there, then the phase 2 pillars
            foreach (var p in pillars) KillPillar(p, !p.broken && p.rise > .5f);
            pillars.Clear(); gimmick = "none"; bossFlow = "down"; flowT = 0;
            cam.Shake(.1f); vfx.Dust(boss.pos, 2.2f); Cue("감시자가 쓰러졌다…", 1.6f);
        }
        void StartOrder()
        {
            int n = pillars.Count; pillarOrder = new int[n]; for (int i = 0; i < n; i++) pillarOrder[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = pillarOrder[i]; pillarOrder[i] = pillarOrder[j]; pillarOrder[j] = tmp; }
            pillarNext = 0; foreach (var p in pillars) { p.lit = false; p.hp = PILLAR_HITS; }
        }
        /// pillar index that must be hit next (phase 1: lit order, phase 2: reverse)
        public int ExpectedPillar() => pillarOrder.Length == 0 ? -1 : boss != null && boss.phase == 2 ? pillarOrder[pillarOrder.Length - 1 - pillarNext] : pillarOrder[pillarNext];
        void Light(Pillar p)
        {
            p.lit = true; p.flare = 1;
            var top = p.pos + new Vector3(0, PILLAR_H, 0);
            vfx.Flash(top, 1.1f); vfx.Ring(Sim.FlatY(p.pos, .05f), 1.1f, true);
        }
        void PillarFail(string why)
        {
            int left = 0; foreach (var p in pillars) if (!p.broken) left++;
            pillarFails++; gimmick = "drain"; gimmickT = 0;
            foreach (var p in pillars) if (!p.broken) { Beam(p, true); p.flare = 1; }
            Cue($"{why} · 감시자가 기둥 {left}개의 에너지를 빨아들인다", 2);
        }
        /// energy beam from the pillar top into the boss core (wake-up and drain)
        void Beam(Pillar p, bool on)
        {
            if (!on) { if (p.beam) p.beam.enabled = false; return; }
            if (!beamMat) beamMat = Mats.Unlit(Color.white, Blend.Add, true, UnityEngine.Rendering.CullMode.Off, false);
            if (!p.beam)
            {
                var g = new GameObject("energy-beam"); g.transform.SetParent(p.go.transform, false);
                p.beam = g.AddComponent<LineRenderer>(); p.beam.useWorldSpace = true; p.beam.positionCount = 12; p.beam.sharedMaterial = beamMat;
                p.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; p.beam.receiveShadows = false;
            }
            p.beam.enabled = true;
        }
        void PillarWin()
        {
            pillarWins++; gimmick = "none"; pillars.Clear();   // every pillar is already broken and destroyed
            boss.ToStagger(STAGGER_REWARD); vfx.Flash(boss.Center(), 1.6f); cam.Shake(.08f);
            Cue("순서 성공 · 감시자가 휘청인다", 1.8f);
        }

        // ---------- pillars ----------
        void SpawnPillars(Vector2[] spots, float speed)
        {
            var c = fightArena.center;
            for (int i = 0; i < spots.Length; i++)
            {
                var pos = new Vector3(c.x + spots[i].x, 0, c.z + spots[i].y); pos.y = Terrain.Height(pos.x, pos.z);
                var p = new Pillar { index = i, pos = pos, speed = speed };
                if (!pillarMesh) pillarMesh = BossPillarProps.Pillar(out pillarGlowMesh);
                p.go = new GameObject("boss-pillar"); p.go.transform.SetParent(zoneRoot, false);
                p.go.AddComponent<MeshFilter>().sharedMesh = pillarMesh; p.go.AddComponent<MeshRenderer>().sharedMaterial = zoneLit;
                var glow = new GameObject("glow"); glow.transform.SetParent(p.go.transform, false);
                glow.AddComponent<MeshFilter>().sharedMesh = pillarGlowMesh;
                var gr = glow.AddComponent<MeshRenderer>(); gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gr.sharedMaterial = p.mat = Mats.Unlit(Color.black, Blend.Opaque, true);
                pillars.Add(p); PillarVisual(p, 0);
                vfx.Dust(pos, 1.4f);
            }
        }
        void KillPillar(Pillar p, bool burst)
        {
            if (burst) { var top = p.pos + new Vector3(0, PILLAR_H * .6f, 0); vfx.Burst(top, 26, 1.3f); vfx.Flash(top, 1.2f); vfx.Dust(p.pos, 1.2f); }
            if (p.cols != null) { WorldCollision.RemovePersistent(p.cols); p.cols = null; }
            if (p.go) p.go.SetActive(false);   // Destroy lands at frame end; hide now
            if (p.mat) Destroy(p.mat); if (p.go) Destroy(p.go); p.go = null;
        }
        void PillarVisual(Pillar p, float dt)
        {
            if (!p.go) return;
            p.rise = Mathf.MoveTowards(p.rise, p.up, p.speed * dt);
            float e = 1 - Mathf.Pow(1 - p.rise, 3);
            // solid once mostly out of the ground
            if (p.rise > .6f && p.cols == null) { p.cols = new List<WorldCollision.Col> { WorldCollision.ToWorld(PropCollider.Circle(0, 0, PILLAR_R + .15f), p.pos, 0, 1) }; WorldCollision.AddPersistent(p.cols, null); }
            else if (p.rise <= .6f && p.cols != null) { WorldCollision.RemovePersistent(p.cols); p.cols = null; }
            p.hitFlash = Mathf.Max(0, p.hitFlash - dt * 5); p.flare = Mathf.Max(0, p.flare - dt * 1.4f);
            var jit = p.hitFlash > 0 ? new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * (.06f * p.hitFlash) : Vector3.zero;
            if (p.rise < 1 && p.up > 0 && Random.value < dt * 14) vfx.Dust(p.pos + jit, .5f);   // breaking out of the ground
            p.go.transform.localPosition = Sim.ToUnity(p.pos + jit + new Vector3(0, -(1 - e) * (PILLAR_H + .6f), 0));
            // glow: dark → dim phase colour, lit = bright pulse, flare on light / wake, white on hit, fading HP dims it
            var col = PillarColor; float pulse = .85f + .15f * Mathf.Sin(realTime * 7 + p.index);
            float k = (p.lit ? 1 * pulse : .1f) * (.55f + .45f * p.hp / (float)PILLAR_HITS) + p.flare * 1.2f;
            if (gimmick == "drain" && !p.broken) k = 1.4f + .8f * Mathf.Sin(realTime * 30);
            var c = Color.Lerp(col * k, new Color(5, 5, 5, 1), p.hitFlash * .7f); c.a = 1; Mats.SetColor(p.mat, c);
            if (p.beam && p.beam.enabled && boss != null)
            {   // energy pulled from the pillar into the boss core
                Vector3 a = p.pos + new Vector3(0, PILLAR_H, 0), b = boss.CoreWorld();
                for (int i = 0; i < 12; i++)
                {
                    float s = i / 11f; var q = Vector3.Lerp(a, b, s) + new Vector3(0, Mathf.Sin(s * Mathf.PI) * 1.4f, 0);
                    q += new Vector3(Mathf.Sin(realTime * 40 + i * 2.1f), 0, Mathf.Cos(realTime * 37 + i * 1.7f)) * (.12f * Mathf.Sin(s * Mathf.PI));
                    p.beam.SetPosition(i, Sim.ToUnity(q));
                }
                float flow = Mathf.Repeat(realTime * 2.5f, 1);
                p.beam.widthCurve = new AnimationCurve(new Keyframe(0, .35f), new Keyframe(flow, .55f), new Keyframe(1, .15f));
                var bc = col * 1.6f; bc.a = 1; p.beam.startColor = bc; p.beam.endColor = bc;
            }
        }
        // ---------- scrap throw (ranged attack): the hanging arm grabs a scrap lump beside the boss and hurls it ----------
        // wind-up 0..35 %: lump torn off the ground and hoisted over the back; 35..100 %: lobbed onto the telegraph, tumbling
        bool scrapGrabbed;
        void BoltOrb()
        {
            bool on = boss != null && boss.state == "windup" && boss.attack != null && boss.attack.name == "throw" && boss.Tele != null;
            if (!on) { if (boltOrb) boltOrb.SetActive(false); scrapGrabbed = false; return; }
            if (!boltOrb)
            {
                boltOrb = new GameObject("boss-scrap"); boltOrb.transform.SetParent(zoneRoot, false);
                boltOrb.AddComponent<MeshFilter>().sharedMesh = BossPillarProps.ScrapChunk();
                boltOrb.AddComponent<MeshRenderer>().sharedMaterial = zoneLit;
            }
            boltOrb.SetActive(true);
            var tele = boss.Tele; float k = Mathf.Clamp01(tele.t / tele.duration);
            var side = new Vector3(Mathf.Cos(boss.yaw), 0, -Mathf.Sin(boss.yaw));            // the arm hangs on the right
            Vector3 ground = boss.pos + side * (boss.radius + 1.2f) - Sim.Forward(boss.yaw) * 1.5f, hold = boss.pos + side * .8f + new Vector3(0, boss.height + 3.2f, 0);
            Vector3 to = tele.origin + new Vector3(0, .4f, 0), pos; float spin;
            if (k < .35f)
            {
                if (!scrapGrabbed) { scrapGrabbed = true; vfx.Dust(ground, 1.4f); }
                float u = Sim.SmoothStep(k, 0, .35f); pos = Vector3.Lerp(ground, hold, u) + new Vector3(0, Mathf.Sin(u * Mathf.PI) * 1.2f, 0); spin = u * .6f;
            }
            else
            {
                float u = (k - .35f) / .65f;
                pos = Vector3.Lerp(hold, to, u) + new Vector3(0, Mathf.Sin(u * Mathf.PI) * 4f, 0); spin = .6f + u * 9;
            }
            boltOrb.transform.localPosition = Sim.ToUnity(pos);
            boltOrb.transform.localRotation = Sim.ToUnity(Sim.AxisAngle(new Vector3(.6f, .3f, .74f).normalized, spin) * Sim.AxisAngle(Vector3.up, boss.yaw));
        }

        // ---------- hits (called from SoulGame's blade / area / wave hit tests) ----------
        bool PillarOpen(Pillar p) => !p.broken && p.go && p.rise > .8f && bossFlow == "fight" && gimmick == "active";
        void PillarBlade(Vector3 a, Vector3 b)
        {
            foreach (var p in pillars.ToArray())
            {
                if (!PillarOpen(p) || pillarHitSet.Contains(p)) continue;
                float d = Sim.SegDist(a, b, p.pos + new Vector3(0, .2f, 0), p.pos + new Vector3(0, PILLAR_H, 0), out var s, out var q) - PILLAR_R;
                if (d < BLADE_REACH) PillarStrike(p, Vector3.Lerp(s, q, .5f));
            }
        }
        void PillarArea(float r, float angle)
        {
            var f = ch.Forward;
            foreach (var p in pillars.ToArray())
            {
                if (!PillarOpen(p) || pillarHitSet.Contains(p) || Mathf.Abs(p.pos.y - ch.root.position.y) > 1.6f) continue;
                var d = Sim.FlatY(p.pos - ch.root.position); float dist = d.magnitude - PILLAR_R * .7f;
                if (dist > r || (angle > 0 && dist > .6f && Vector3.Dot(d.normalized, f) < Mathf.Cos(angle / 2))) continue;
                PillarStrike(p, p.pos + new Vector3(0, 1.4f, 0));
            }
        }
        void PillarWave(Wave w)
        {
            foreach (var p in pillars.ToArray())
            {
                if (!PillarOpen(p) || w.hitSet.Contains(p)) continue;
                if (Sim.FlatY(w.pos - p.pos).magnitude < WAVE_R + PILLAR_R * .5f) { w.hitSet.Add(p); PillarStrike(p, p.pos + new Vector3(0, 1.4f, 0)); }
            }
        }
        /// one hit per move per pillar; a wrong pillar fails the gimmick at once
        void PillarStrike(Pillar p, Vector3 at)
        {
            pillarHitSet.Add(p);
            if (p.index != ExpectedPillar()) { vfx.Burst(at, 14, .9f); p.hitFlash = 1; PillarFail("틀린 기둥"); return; }
            p.hp--; p.hitFlash = 1; vfx.Burst(at, 18, 1); cam.Shake(.05f); hitStop = Mathf.Max(hitStop, .04f);
            if (p.hp > 0) return;
            p.broken = true; KillPillar(p, true); cam.Shake(.09f); pillarNext++;
            if (pillarNext >= pillars.Count) PillarWin();
        }
        /// MCP tests: hit pillar `index` once (as if struck by a move)
        public void DebugStrikePillar(int index) { pillarHitSet.Clear(); var p = pillars.Find(x => x.index == index); if (p != null && PillarOpen(p)) PillarStrike(p, p.pos + Vector3.up); }

        void PillarsGUI(float W, float H)
        {
            if (bossFlow != "fight" || (gimmick != "show" && gimmick != "active")) return;
            int n = pillars.Count;
            var st = new GUIStyle(big) { fontSize = 16 };
            GUI.color = boss != null && boss.phase == 2 ? new Color(1, .55f, .45f) : new Color(.6f, .85f, 1);
            string rule = boss != null && boss.phase == 2 ? "켜진 순서의 반대로" : "켜진 순서대로";
            string text = gimmick == "show" ? $"에너지 기둥 · 순서를 기억하라 ({rule})" : $"에너지 기둥 {pillarNext}/{n} · {rule} · {Mathf.Max(0, PILLAR_LIMIT - gimmickT):F1}s";
            GUI.Label(new Rect(0, 86, W, 26), text, st);
            if (debugHud) { GUI.color = Color.white; GUI.Label(new Rect(20, 202, 500, 20), $"order {string.Join(",", System.Array.ConvertAll(pillarOrder, i => i.ToString()))} · next {ExpectedPillar()}", small); }
            GUI.color = Color.white;
        }
    }
}
