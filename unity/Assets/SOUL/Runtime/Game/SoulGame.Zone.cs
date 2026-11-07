using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Zone vertical slice (stage 3): charging pods (rest / respawn / fast travel), area titles,
    // the boss arena fight (fog gate, boss HP bar, reward part) and purification (되살아나는 세계:
    // the zone's look flips, grass + flowers, pollen instead of ash, butterflies and birds, one more face).
    // Debug: G rest at a pod · K toggle purification of the zone you stand in · M map (click a found pod = travel,
    // Shift+click = teleport anywhere).
    public partial class SoulGame
    {
        Fauna fauna; Pod restPod; Material zoneLit, zoneGlow, fogMat;
        BossArena fightArena; Enemy boss; GameObject fogGo; List<WorldCollision.Col> fogCols; float bossIntro = -1;
        string areaName, areaCandidate; float areaHold, areaT = -1; float revealUntil;
        static Mesh podMesh, podGlow; Transform zoneRoot;
        Material soilMat; Color soilBase; float soilK;   // the original web arena ground (Map/cratered ash soil) greens with Z0

        void ZoneInit()
        {
            zoneLit = Mats.Lit(Color.white, Color.black, .12f, .12f, true); zoneLit.SetFloat("_Cutaway", 1);
            zoneGlow = Mats.Unlit(Color.white * 2.4f, Blend.Opaque, true);
            fogMat = Mats.Unlit(new Color(.9f, 1.4f, 2.2f, .5f), Blend.Add, true, UnityEngine.Rendering.CullMode.Off, false);
            fauna = new Fauna(scene.fauna);
            var root = zoneRoot = scene.zoneObjects;
            // pods
            var podModel = ZoneProps.Pod(out var glow); podMesh = podModel.mesh; podGlow = glow;
            for (int i = 0; i < ZoneAreas.Pods.Count; i++)
            {
                var p = ZoneAreas.Pods[i];
                Bind(scene.pods[i], podMesh, zoneLit, p.pos, p.yaw, true);
                Bind(scene.podGlows[i], podGlow, zoneGlow, p.pos, p.yaw, false);
                var cols = new List<WorldCollision.Col>(); foreach (var pc in podModel.colliders) cols.Add(WorldCollision.ToWorld(pc, p.pos, p.yaw, 1));
                WorldCollision.AddPersistent(cols, null);
            }
            // boss arenas: ring wall + rope shortcut ledge
            for (int i = 0; i < ZoneAreas.Arenas.Count; i++)
            {
                var a = ZoneAreas.Arenas[i];
                var wall = ZoneProps.ArenaWall(a, out _);
                Bind(scene.arenas[i], wall.mesh, zoneLit, a.center, 0, true);
                var cols = new List<WorldCollision.Col>(); foreach (var pc in wall.colliders) cols.Add(WorldCollision.ToWorld(pc, a.center, 0, 1));
                var surfs = new List<WorldCollision.Surf>();
                var g = ZoneGates.Get(a.ropeSite); var sp = new Vector3(a.ropeSite.pos.x, 0, a.ropeSite.pos.y);
                if (g.fixedPart.mesh) Bind(scene.arenaRopeLedges[i], g.fixedPart.mesh, zoneLit, sp, a.ropeSite.yaw, true);
                if (g.fixedGlow) Bind(scene.arenaRopeGlows[i], g.fixedGlow, zoneGlow, sp, a.ropeSite.yaw, false);
                foreach (var pc in g.fixedPart.colliders) cols.Add(WorldCollision.ToWorld(pc, sp, a.ropeSite.yaw, 1));
                foreach (var pc in g.solid.colliders) cols.Add(WorldCollision.ToWorld(pc, sp, a.ropeSite.yaw, 1));
                foreach (var ps in g.fixedPart.surfaces) surfs.Add(WorldCollision.ToWorld(ps, sp, a.ropeSite.yaw, 1));
                WorldCollision.AddPersistent(cols, surfs);
            }
            // rope swing chasms
            for (int i = 0; i < ZoneAreas.Chasms.Count; i++)
            {
                var c = ZoneAreas.Chasms[i];
                var m = ZoneProps.Chasm(c, out var cg);
                Bind(scene.chasms[i], m.mesh, zoneLit, c.center, c.yaw, true); Bind(scene.chasmGlows[i], cg, zoneGlow, c.center, c.yaw, false);
                var cols = new List<WorldCollision.Col>(); foreach (var pc in m.colliders) cols.Add(WorldCollision.ToWorld(pc, c.center, c.yaw, 1));
                WorldCollision.AddPersistent(cols, null);
            }
            // skill shrines (one per zone): resonate with G to take the zone core
            var shrineModel = ShrineProps.Shrine(out var shrineGlow);
            for (int i = 0; i < ZoneAreas.Shrines.Count; i++)
            {
                var sh = ZoneAreas.Shrines[i];
                var p = sh.pos + new Vector3(0, Terrain.Height(sh.pos.x, sh.pos.z), 0);
                Bind(scene.shrines[i], shrineModel.mesh, zoneLit, p, 0, true); Bind(scene.shrineGlows[i], shrineGlow, zoneGlow, p, 0, false);
                var cols = new List<WorldCollision.Col>(); foreach (var pc in shrineModel.colliders) cols.Add(WorldCollision.ToWorld(pc, p, 0, 1));
                WorldCollision.AddPersistent(cols, null);
            }
            // static progress survives editor play sessions without a domain reload: start clean
            ZoneState.Reset(); Parts.owned = 0; MapFog.Reset(); foreach (var p in ZoneAreas.Pods) p.found = false;
            restPod = ZoneAreas.Pods[0]; restPod.found = true;
            soilMat = scene.crateredAshSoil.material; soilBase = Mats.GetColor(soilMat);
            ZoneState.Changed += OnZoneChanged;
            PixelFace.unlocked = ZoneState.Count;
        }
        void OnDestroy() { ZoneState.Changed -= OnZoneChanged; }
        static GameObject Bind(GameObject g, Mesh mesh, Material mat, Vector3 pos, float yaw, bool shadows)
        {
            g.transform.localPosition = Sim.ToUnity(pos); g.transform.localRotation = Sim.YawToUnity(yaw);
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            g.SetActive(true); return g;
        }

        /// MCP tests: the editor barely ticks frames while unfocused, so drive the zone logic by hand
        public void ZoneTick(float dt) => ZoneUpdate(dt);
        void ZoneUpdate(float dt)
        {
            var p = ch.root.position; int zone = Regions.At(p);
            MapFog.Reveal(p, 46);
            // pods: discover when close
            foreach (var pod in ZoneAreas.Pods)
                if (!pod.found && Sim.FlatY(pod.pos - p).magnitude < 14) { pod.found = true; Cue($"충전 포드 발견 · {pod.name}  (G 휴식 · M 지도에서 이동)", 2.6f); }
            // area title (구역): must hold 0.8 s, like the zone banner
            var area = ZoneAreas.AreaAt(p); string an = area?.name;
            if (an != areaName) { if (an != areaCandidate) { areaCandidate = an; areaHold = 0; } areaHold += dt; if (areaHold > .8f) { areaName = an; if (an != null) areaT = 0; } }
            else areaCandidate = an;
            if (areaT >= 0) { areaT += dt; if (areaT > 3.2f) areaT = -1; }
            // boss arena
            if (fightArena == null && player.alive)
            {
                var a = ZoneAreas.ArenaAt(p, 5);
                if (a != null && !ZoneState.Purified(a.zone)) StartFight(a);
            }
            else if (fightArena != null && boss != null && !boss.alive) EndFight(true);
            if (bossIntro >= 0) { bossIntro += dt; if (bossIntro > 3.5f) bossIntro = -1; }
            // purified life
            bool healed = ZoneState.Purified(zone);
            fauna.weight = healed ? 1 : 0; fauna.Update(dt, p);
            ash.bloom = healed ? 1 : 0;
            atmo.ease = realTime < revealUntil ? .22f : .9f;
            PixelFace.unlocked = ZoneState.Count;
            if (soilMat)
            {
                soilK = Mathf.MoveTowards(soilK, ZoneState.Purified(0) ? 1 : 0, dt * .15f);
                var c = Color.Lerp(soilBase, new Color(soilBase.r * .9f, soilBase.g * 1.06f, soilBase.b * .72f, soilBase.a), soilK); Mats.SetColor(soilMat, c);
            }
        }

        // ---------- water (Terrain) ----------
        float rippleClock, toxicCue;
        /// per substep: wading slows (animator speedScale), ripples while moving, deep toxic sludge corrodes
        /// unless the anti-toxin core is owned
        void WaterStep(float dt)
        {
            var p = ch.root.position;
            float d = Terrain.Water(p.x, p.z, out _);
            anim.speedScale = RopeTravel ? 1 : Terrain.Slow(p.x, p.z);
            if (d > .08f && anim.speed > .4f && !Falling)
            {
                rippleClock -= dt;
                if (rippleClock <= 0) { rippleClock = .32f; vfx.Ring(new Vector3(p.x, p.y + d - .02f, p.z), .45f); }
            }
            if (Terrain.Toxic(p.x, p.z) && !Parts.Has(Parts.Toxic) && player.alive && !Invulnerable())
            {
                player.hp -= 9 * dt; hurtFlash = Mathf.Max(hurtFlash, .25f);
                if (realTime > toxicCue) { toxicCue = realTime + 4; Cue("독성 슬러지 · 몸이 부식된다", 1.6f); face.Set("hurt", .5f); }
                if (player.hp <= 0) { player.hp = 0; PlayerDie(); }
            }
        }

        // ---------- pods ----------
        Pod NearPod(float within) { foreach (var pod in ZoneAreas.Pods) if (Sim.FlatY(pod.pos - ch.root.position).magnitude < within) return pod; return null; }
        Shrine NearShrine(float within) { foreach (var s in ZoneAreas.Shrines) if (Sim.FlatY(s.pos - ch.root.position).magnitude < within) return s; return null; }
        void Resonate(Shrine s)
        {
            if (ZoneState.HasCore(s.zone)) { Cue($"{s.name} · 이미 공명함 (스킬은 성장 단계에서 연다)", 1.6f); return; }
            ZoneState.GiveCore(s.zone); MapFog.RevealZone(s.zone);
            var at = s.pos + new Vector3(0, ch.root.position.y + 4.2f, 0);
            vfx.Ring(Sim.FlatY(ch.root.position, .1f), 2.4f, true); vfx.Flash(at, 1.4f); vfx.Burst(at, 30, 1.4f); cam.Shake(.05f);
            Cue($"{s.name} 공명 · {ZoneGraph.Zones[s.zone].name} 지도 공개 · 포드 워프 개방", 3);
        }
        void RestAtPod()
        {
            var sh = NearShrine(5); if (sh != null && player.alive && fightArena == null) { Resonate(sh); return; }
            var pod = NearPod(4.2f); if (pod == null || !player.alive || fightArena != null) return;
            pod.found = true; restPod = pod; player.hp = player.max;
            vfx.Ring(Sim.FlatY(pod.pos, .5f), 1.6f, true); vfx.Flash(Sim.FlatY(pod.pos, 1.4f), 1);
            Cue($"충전 완료 · {pod.name} 에 저장됨", 2);
        }
        Vector3 PodSpot(Pod pod) => pod.pos + Sim.Forward(pod.yaw) * 3.2f;
        void TravelTo(Pod pod)
        {
            if (fightArena != null) { Cue("전투 중에는 이동할 수 없습니다", 1.4f); return; }
            if (!ZoneState.HasCore(pod.zone) && !debugHud) { Cue($"{ZoneGraph.Zones[pod.zone].name}의 공명 기둥과 공명해야 워프할 수 있다", 2); return; }
            Teleport(PodSpot(pod)); showMap = false; Cue($"빠른 이동 · {pod.name}", 1.6f);
        }
        void RespawnAtPod()
        {
            if (fightArena != null) EndFight(false);
            Teleport(PodSpot(restPod));
        }

        // ---------- boss fight ----------
        void StartFight(BossArena a)
        {
            fightArena = a; bossIntro = 0;
            boss = enemies.SpawnAt(a.boss, a.center);
            // fog gate across the entrance (soul fog: blocks until the fight ends)
            float r = a.radius + ZoneAreas.WALL_T * .5f; var gp = a.center + Sim.Forward(a.entranceYaw) * r;
            fogGo = Bind(scene.fogGate, ZoneProps.FogGate(7.5f, 6), fogMat, gp, a.entranceYaw, false);
            fogCols = new List<WorldCollision.Col> { WorldCollision.ToWorld(PropCollider.Rect(0, 0, 7.6f, .9f), gp, a.entranceYaw, 1) };
            WorldCollision.AddPersistent(fogCols, null);
            cam.Shake(.06f);
        }
        void EndFight(bool won)
        {
            var a = fightArena; fightArena = null; bossIntro = -1;
            if (fogCols != null) { WorldCollision.RemovePersistent(fogCols); fogCols = null; }
            if (fogGo) { Destroy(fogGo.GetComponent<MeshFilter>().sharedMesh); fogGo.GetComponent<MeshFilter>().sharedMesh = null; fogGo.SetActive(false); }
            if (!won) { if (boss != null && boss.alive) boss.Despawn(); boss = null; return; }
            boss = null;
            if (a.reward != Parts.None && a.reward != Parts.LabKeys) Parts.Give(a.reward);
            ZoneState.Set(a.zone, true);
            int n = ZoneState.Count; string face = n >= 1 && n <= PixelFace.ORDER.Length ? PixelFace.ORDER[n - 1] : null;
            Cue($"{a.name} 정화 · {Parts.Names[a.reward]} 획득" + (face != null ? $" · 새 표정 ({n}/{PixelFace.ORDER.Length})" : ""), 4);
            if (face != null) After(1.2f, () => this.face.Preview(face, 1.6f));
        }

        void OnZoneChanged(int zone)
        {
            open.RebuildAll(ch.root.position);
            if (ZoneState.Purified(zone)) revealUntil = realTime + 9;
            PixelFace.unlocked = ZoneState.Count;
        }
        void TogglePurify()
        {
            int z = Regions.At(ch.root.position); bool on = !ZoneState.Purified(z);
            ZoneState.Set(z, on); Cue($"[디버그] Z{z} {ZoneGraph.Zones[z].name} {(on ? "정화" : "오염")} · 표정 {ZoneState.Count}/{PixelFace.ORDER.Length}", 1.8f);
        }

        // ---------- map / GUI ----------
        void MapClick(Rect r, Vector2 mp)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            foreach (var pod in ZoneAreas.Pods)
            {
                if (!pod.found) continue;
                var uv = ZoneWorld.MapUV(pod.pos); var c = new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height);
                if ((c - mp).magnitude < 12) { TravelTo(pod); return; }
            }
            if (shift) Teleport(ZoneWorld.FromMapUV(new Vector2((mp.x - r.x) / r.width, (mp.y - r.y) / r.height)));
        }
        void DrawMapZone(Rect r, Vector2 playerPx)
        {
            foreach (var sh in ZoneAreas.Shrines)
            {
                if (!debugHud && !MapFog.Seen(sh.pos)) continue;
                var uv = ZoneWorld.MapUV(sh.pos); var c = new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height);
                GUI.color = ZoneState.HasCore(sh.zone) ? Color.white : new Color(.55f, .6f, .62f);
                if (!MapPainter.Icon(new Rect(c.x - 13, c.y - 15, 26, 26), MapPainter.I_SHRINE) && !UiArt.Icon(new Rect(c.x - 11, c.y - 13, 22, 22), UiArt.SHRINE)) GUI.DrawTexture(new Rect(c.x - 3, c.y - 8, 6, 16), white);
            }
            foreach (var lm in Regions.Landmarks)
            {
                if (!debugHud && !MapFog.Seen(lm.pos)) continue;   // landmark silhouettes: what you would see from afar
                var uv = ZoneWorld.MapUV(lm.pos); var c = new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height);
                GUI.color = Color.white;
                if (!MapPainter.Icon(new Rect(c.x - 14, c.y - 24, 28, 28), MapPainter.LandmarkIcon(lm.id))) { GUI.color = new Color(.1f, .1f, .1f, .85f); GUI.DrawTexture(new Rect(c.x - 2, c.y - 7, 4, 10), white); GUI.DrawTexture(new Rect(c.x - 4, c.y + 2, 8, 2), white); }
            }
            foreach (var a in ZoneAreas.Arenas)
            {
                if (!debugHud && !MapFog.Seen(a.center)) continue;
                var uv = ZoneWorld.MapUV(a.center); var c = new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height);
                GUI.color = ZoneState.Purified(a.zone) ? new Color(.6f, .85f, .5f) : Color.white;
                if (!MapPainter.Icon(new Rect(c.x - 16, c.y - 16, 32, 32), MapPainter.I_BOSS)) { GUI.color = ZoneState.Purified(a.zone) ? new Color(.6f, .85f, .5f) : new Color(.85f, .2f, .15f); GUI.DrawTexture(new Rect(c.x - 6, c.y - 6, 12, 12), white); }
            }
            foreach (var pod in ZoneAreas.Pods)
            {
                if (!pod.found) continue;
                var uv = ZoneWorld.MapUV(pod.pos); var c = new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height);
                GUI.color = pod == restPod ? new Color(.6f, 1, 1) : new Color(.3f, .8f, 1);
                if (!MapPainter.Icon(new Rect(c.x - 12, c.y - 12, 24, 24), MapPainter.I_POD) && !UiArt.Icon(new Rect(c.x - 11, c.y - 11, 22, 22), UiArt.POD)) GUI.DrawTexture(new Rect(c.x - 5, c.y - 5, 10, 10), white);
                GUI.color = Color.white; GUI.Label(new Rect(c.x + 8, c.y - 9, 220, 18), pod.name, small);
            }
            GUI.color = Color.white;
        }
        void ZoneGUI(float W, float H)
        {
            // area subtitle under the zone banner
            if (areaT >= 0 && areaName != null)
            {
                float a = Mathf.Clamp01(areaT / .5f) * (1 - Sim.SmoothStep(areaT, 2.4f, 3.2f));
                GUI.color = new Color(.95f, .9f, .8f, a);
                GUI.Label(new Rect(0, H * .4f, W, 26), $"— {areaName} —", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 17 });
            }
            // pod prompt
            var pod = NearPod(4.2f);
            if (pod != null && fightArena == null) { GUI.color = new Color(.6f, .95f, 1); GUI.Label(new Rect(0, H * .62f, W, 24), $"G  휴식 · {pod.name}", new GUIStyle(big) { fontSize = 17 }); }
            var shr = NearShrine(5);
            if (shr != null && fightArena == null) { GUI.color = new Color(.75f, .95f, 1); GUI.Label(new Rect(0, H * .62f, W, 24), ZoneState.HasCore(shr.zone) ? $"{shr.name} · 공명함" : $"G  공명 · {shr.name}", new GUIStyle(big) { fontSize = 17 }); }
            // boss bar
            if (boss != null && fightArena != null)
            {
                float bw = Mathf.Min(W * .6f, 760), x = (W - bw) / 2, y = H - 150;
                float intro = bossIntro >= 0 ? Mathf.Clamp01(bossIntro / .6f) : 1;
                GUI.color = new Color(1, .95f, .85f, intro); GUI.Label(new Rect(x, y - 24, bw, 22), $"{fightArena.name}   {fightArena.title}", small);
                if (UiArt.Kit)
                {
                    var fr = new Rect(x - bw * .04f, y - 14, bw * 1.08f, bw * 1.08f * 148f / 1496f);
                    float ix = fr.x + fr.width * .1f, iw = fr.width * .8f, iy = fr.y + fr.height * .4f, ih = fr.height * .22f;
                    GUI.color = new Color(0, 0, 0, .7f * intro); GUI.DrawTexture(new Rect(ix, iy, iw, ih), white);
                    GUI.color = new Color(.75f, .12f, .08f, intro); GUI.DrawTexture(new Rect(ix, iy, iw * Mathf.Max(0, boss.hpFrac), ih), white);
                    GUI.color = new Color(1, 1, 1, intro); UiArt.Piece(fr, UiArt.BossBar);
                }
                else
                {
                    GUI.color = new Color(0, 0, 0, .7f * intro); GUI.DrawTexture(new Rect(x, y, bw, 10), white);
                    GUI.color = new Color(.75f, .12f, .08f, intro); GUI.DrawTexture(new Rect(x + 1, y + 1, (bw - 2) * Mathf.Max(0, boss.hpFrac), 8), white);
                }
                if (bossIntro >= 0 && bossIntro < 3.5f)
                {
                    float a = Mathf.Clamp01(bossIntro / .5f) * (1 - Sim.SmoothStep(bossIntro, 2.6f, 3.5f));
                    GUI.color = new Color(1, .9f, .8f, a); GUI.Label(new Rect(0, H * .3f, W, 50), fightArena.name, new GUIStyle(big) { fontSize = 38 });
                }
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(22, 64, 190, 20), $"기본 부품 {scrap}", small);
            if (debugHud) GUI.Label(new Rect(20, 122, 520, 20), $"표정 {ZoneState.Count}/{PixelFace.ORDER.Length}   ·   K 정화 토글   ·   Shift+클릭 지도 순간이동", small);
        }
    }
}
