using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Boss purification scene (정화 연출), phase-2 kill → the zone comes back to life from the arena outwards. ~11 s,
    // Esc / Enter skips to the reward.
    //  0.0  kill: slow motion (x.25 for 1.3 s), the beast collapses (Enemy dead pose, the hanging leg drops), core cracks
    //  0.3  hatred released: red souls burst from the core, swirl, turn white and rise into the sky
    //  1.6  camera pulls up and out over the arena (FollowCam fight framing, zoomed far out)
    //  2.4  purification wave: ZoneState flips (chunks rebuild healed) but SoulLit's _SoulHeal keeps everything outside the
    //       expanding ring ashen grey; the ring front glows. Fog / light ease over the same time; fauna / pollen follow the ring.
    //  6.8  camera returns to the player
    //  7.0  the rope arm rises glowing from the wreck; walk into it to take it → first new face preview
    //  7.0  leftover grudge: red souls are drawn into the twisted fourth leg, condense and drop as a 원혼 파츠 (GrudgeDrop, 8.8)
    public partial class SoulGame
    {
        public const float GRUDGE_DROP = 8.8f, WAVE_START = 2.4f, WAVE_TIME = 9, WAVE_REACH = 1700, PURIFY_CAM_OUT = 1.6f, PURIFY_CAM_BACK = 6.8f, REWARD_AT = 7, SLOWMO = 1.3f;
        static readonly int HealId = Shader.PropertyToID("_SoulHeal");
        /// seconds since the kill (-1 = not running); the wave keeps its own clock so a skip does not cut it short
        public float purifyT = -1, waveT = -1;
        public bool rewardTaken;
        BossArena purifyArena; Enemy wreck; Vector3 rewardPos, rewardFrom; GameObject rewardGo; float rewardT;
        class Mote { public GameObject go; public Material mat; public Vector3 pos, vel, from; public float age, ang, rad, h, spin; public bool grudge; }
        readonly List<Mote> motes = new List<Mote>(); static Texture2D moteTex; static Mesh moteQuad;
        int motesSpawned, grudgeSpawned; public bool grudgeDropped;

        /// ring radius: crosses the camera's view first (28 m/s for 2.5 s), then accelerates out over the zone
        public float HealRadius => waveT < 0 ? 0 : waveT < 2.5f ? waveT * 28 : 70 + (WAVE_REACH - 70) * Mathf.Pow(Mathf.Clamp01((waveT - 2.5f) / (WAVE_TIME - 2.5f)), 1.8f);
        /// purified effects (fauna, pollen) only where the wave has already passed
        bool HealReached(Vector3 p) => waveT < 0 || waveT >= WAVE_TIME || purifyArena == null || Sim.FlatY(p - purifyArena.center).magnitude < HealRadius;
        public bool Purifying => purifyT >= 0;

        void StartPurify(BossArena a, Enemy dead)
        {
            purifyArena = a; wreck = dead; purifyT = 0; waveT = -1; motesSpawned = grudgeSpawned = 0; rewardTaken = grudgeDropped = false;
            var core = dead != null ? dead.CoreWorld() : a.center + Vector3.up * 2;
            rewardFrom = core;
            // the rope arm floats just outside the wreck on the player's side (the core is inside the body)
            var wp = dead != null ? dead.pos : a.center; var toP = Sim.FlatY(ch.root.position - wp);
            if (toP.magnitude < .1f) toP = Sim.Forward(a.entranceYaw);
            rewardPos = wp + toP.normalized * ((dead != null ? dead.radius : 3) + 1.8f);
            timeScale = .25f; hitStop = 0;
            vfx.Flash(core, 3); vfx.Burst(core, 40, 1.8f); vfx.Ring(core, 2.4f, true); cam.Shake(.25f);
            face.Set("wide", 1.2f);
        }
        void PurifyUpdate(float dt)
        {
            if (purifyT < 0) { Shader.SetGlobalVector(HealId, Vector4.zero); return; }
            if (wreck != null && !wreck.Exists) wreck = null;   // wreck disposed (debug clear, despawn): fall back to the arena centre
            float prev = purifyT; purifyT += dt;
            bool skip = purifyT < PURIFY_CAM_BACK && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return));
            if (skip) { purifyT = PURIFY_CAM_BACK; foreach (var m in motes) m.age = 99; if (waveT < 0) BeginWave(); }
            bool At(float s) => prev < s && purifyT >= s;
            var center = purifyArena.center;
            // slow motion right after the kill
            if (purifyT < SLOWMO) timeScale = .25f; else if (prev < SLOWMO || skip) timeScale = 1;
            // hatred: red souls burst from the cracked core, swirl up, turn white and leave
            var core = wreck != null ? wreck.CoreWorld() : center + Vector3.up * 2;
            if (purifyT > .3f && purifyT < 1.7f) { int want = Mathf.Min(80, (int)((purifyT - .3f) / 1.4f * 80)); while (motesSpawned < want) { SpawnMote(core); motesSpawned++; } }
            UpdateMotes(dt, core, core);   // leftover grudge spirals back into the cracked core
            if (At(.35f)) { vfx.Flash(core, 2.2f); cam.Shake(.12f); }
            // camera: far out over the arena while the wave leaves, then back to the player
            if (purifyT >= PURIFY_CAM_OUT && purifyT < PURIFY_CAM_BACK) { cam.fightTarget = 1; cam.fightZoom = 3.6f; cam.fightFocus = center; }
            else if (purifyT >= PURIFY_CAM_BACK) { cam.fightTarget = 0; cam.fightZoom = FIGHT_ZOOM; }
            // the wave
            if (waveT < 0 && purifyT >= WAVE_START) BeginWave();
            if (waveT >= 0)
            {
                waveT += dt;
                var cu = Sim.ToUnity(center);
                Shader.SetGlobalVector(HealId, waveT < WAVE_TIME ? new Vector4(cu.x, HealRadius, cu.z, 1) : Vector4.zero);
            }
            // reward: the rope arm rises out of the wreck, glowing; walk into it
            if (purifyT >= REWARD_AT && !rewardTaken)
            {
                if (!rewardGo) MakeReward();
                rewardT += dt;
                float up = Sim.SmoothStep(rewardT, 0, 1.6f);
                var ground = new Vector3(rewardPos.x, Terrain.Height(rewardPos.x, rewardPos.z), rewardPos.z);
                var p = Vector3.Lerp(rewardFrom, ground + new Vector3(0, 1.5f + .15f * Mathf.Sin(realTime * 2.2f), 0), up) + new Vector3(0, Mathf.Sin(up * Mathf.PI) * 2, 0);
                rewardGo.transform.localPosition = Sim.ToUnity(p);
                rewardGo.transform.localRotation = Sim.ToUnity(Sim.AxisAngle(Vector3.up, realTime * 1.4f) * Sim.AxisAngle(Vector3.right, .35f));
                if (Random.value < dt * 10) enemies.Wisp(p, .35f);
                if (up > .9f && player.alive && Sim.FlatY(ch.root.position - p).magnitude < 2.2f) TakeReward(p);
            }
            // leftover grudge condenses in the cracked core, which breaks out as the red 뒤틀린 에너지 코어 part
            // (World/GrudgeParts, SoulGame.Grudge)
            var knee = core;
            if (purifyArena.zone == 6) grudgeDropped = true;   // organic boss: no grudge part (기획_결정사항, 2026-11-09)
            if (purifyT >= REWARD_AT && purifyT < GRUDGE_DROP - .4f && !grudgeDropped) { int want = Mathf.Min(36, (int)((purifyT - REWARD_AT) / 1.2f * 36)); while (grudgeSpawned < want) { SpawnMote(center + new Vector3(Random.Range(-7f, 7f), Random.Range(.5f, 4f), Random.Range(-7f, 7f)), true); grudgeSpawned++; } }
            if (!grudgeDropped && purifyT >= GRUDGE_DROP)
            {
                grudgeDropped = true;
                // under the core, pushed out past the wreck's body and kept 3 m from the rope arm
                var wp = wreck != null ? wreck.pos : center; float wr = (wreck != null ? wreck.radius : 3) + 1.5f;
                var drop = new Vector3(knee.x, 0, knee.z); var fromBody = Sim.FlatY(drop - wp);
                if (fromBody.magnitude < wr) drop = Sim.FlatY(wp) + (fromBody.magnitude > .1f ? fromBody.normalized : Sim.Forward(purifyArena.entranceYaw + 1.6f)) * wr;
                var away = Sim.FlatY(drop - rewardPos);
                if (away.magnitude < 3) drop = Sim.FlatY(rewardPos) + (away.magnitude > .1f ? away.normalized : Sim.Forward(purifyArena.entranceYaw + 1.6f)) * 3;
                drop.y = 0;   // GrudgeUpdate adds the floor height itself
                vfx.Flash(knee, 2.2f); vfx.Burst(knee, 24, 1.2f); cam.Shake(.07f);
                GrudgeDrop(purifyArena.zone, drop);
            }
            if (rewardTaken && grudgeDropped && waveT >= WAVE_TIME && purifyT > REWARD_AT + 2) EndPurify();
        }
        void BeginWave()
        {
            waveT = 0;
            ZoneState.Set(purifyArena.zone, true);              // chunks rebuild healed; _SoulHeal hides it outside the ring
            revealUntil = realTime + WAVE_TIME;                 // fog / light ease over the wave's time
            vfx.Ring(Sim.FlatY(purifyArena.center, .1f), 6); vfx.Flash(purifyArena.center + Vector3.up * 2, 3);
            cam.Shake(.1f);
        }
        void MakeReward()
        {
            rewardT = 0;
            var body = BossPillarProps.RopeArm(out var glowMesh);
            rewardGo = new GameObject("rope-arm-reward"); rewardGo.transform.SetParent(zoneRoot, false);
            rewardGo.AddComponent<MeshFilter>().sharedMesh = body; rewardGo.AddComponent<MeshRenderer>().sharedMaterial = zoneLit;
            var g = new GameObject("glow"); g.transform.SetParent(rewardGo.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = glowMesh; g.AddComponent<MeshRenderer>().sharedMaterial = zoneGlow;
            vfx.Flash(rewardFrom, 1.6f); vfx.Ring(rewardFrom, 1.4f, true);
        }
        void TakeReward(Vector3 p)
        {
            rewardTaken = true;
            var a = purifyArena;
            if (a.reward != Parts.None && a.reward != Parts.LabKeys) Parts.Give(a.reward);
            vfx.Flash(p, 2); vfx.Burst(p, 30, 1.4f); vfx.Ring(Sim.FlatY(ch.root.position, .1f), 2, true); cam.Shake(.06f);
            if (rewardGo) { rewardGo.SetActive(false); Destroy(rewardGo); rewardGo = null; }
            Cue(Parts.Names[a.reward], 2.4f);
            // the pixel face makes a different expression for the first time
            int n = ZoneState.Count; string f = n >= 1 && n <= PixelFace.ORDER.Length ? PixelFace.ORDER[n - 1] : null;
            if (f != null) After(1.0f, () => face.Preview(f, 2.2f));
        }
        void EndPurify()
        {
            purifyT = -1; waveT = -1; timeScale = 1; cam.fightTarget = 0; cam.fightZoom = FIGHT_ZOOM;
            Shader.SetGlobalVector(HealId, Vector4.zero);
            foreach (var m in motes) m.go.SetActive(false);
            StoryBeat();   // main story: the beat for this many purified zones (SoulGame.Lore)
        }

        // ---------- hatred motes: red → white, swirl round the core then rise ----------
        void SpawnMote(Vector3 core, bool grudge = false)
        {
            if (!moteTex)
            {
                moteTex = Mats.Radial(64, (0, new Color(1, 1, 1, 1)), (.35f, new Color(1, 1, 1, .55f)), (1, new Color(1, 1, 1, 0)));
                moteQuad = FxMeshes.Quad();
            }
            Mote m = motes.Find(x => !x.go.activeSelf);
            if (m == null)
            {
                m = new Mote { go = new GameObject("hate-soul") }; m.go.transform.SetParent(zoneRoot, false);
                m.go.AddComponent<MeshFilter>().sharedMesh = moteQuad;
                var r = m.go.AddComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.sharedMaterial = m.mat = Mats.Unlit(Color.white, Blend.Add, false, UnityEngine.Rendering.CullMode.Off, false, true, moteTex);
                motes.Add(m);
            }
            m.go.SetActive(true); m.age = 0; m.ang = Random.value * Mathf.PI * 2; m.rad = .3f + Random.value * .5f; m.h = Random.value * .6f;
            m.spin = (2.2f + Random.value * 1.6f) * (Random.value < .5f ? 1 : -1);
            m.pos = m.from = core; m.grudge = grudge;
        }
        void UpdateMotes(float dt, Vector3 core, Vector3 knee)
        {
            var camT = Camera.main ? Camera.main.transform : null;
            foreach (var m in motes)
            {
                if (!m.go.activeSelf) continue;
                m.age += dt;
                if (m.grudge)
                {   // drawn in along a tightening spiral, staying red, shrinking into the core
                    float u = Mathf.Clamp01(m.age / 1.4f); if (u >= 1) { m.go.SetActive(false); continue; }
                    float e = u * u * (3 - 2 * u); m.ang += m.spin * dt * (1 + u * 3);
                    m.pos = Vector3.Lerp(m.from, knee, e) + new Vector3(Mathf.Cos(m.ang), 0, Mathf.Sin(m.ang)) * (1.2f * (1 - e));
                    var gc = new Color(3.6f, .2f, .12f, 1) * (.6f + .6f * e); gc.a = 1; Mats.SetColor(m.mat, gc);
                    m.go.transform.localPosition = Sim.ToUnity(m.pos); m.go.transform.localScale = Vector3.one * (.4f * (1 - .6f * e));
                    if (camT) m.go.transform.rotation = camT.rotation;
                    continue;
                }
                if (m.age > 4.6f) { m.go.SetActive(false); continue; }
                // swirl: radius opens, height climbs; after 2.2 s they shoot up
                m.ang += m.spin * dt; m.rad = Mathf.Min(4.5f, m.rad + dt * 1.6f); m.h += dt * (m.age < 2.2f ? .9f : 9);
                float r = m.age < 2.2f ? m.rad : m.rad * Mathf.Max(.15f, 1 - (m.age - 2.2f) * .5f);
                m.pos = core + new Vector3(Mathf.Cos(m.ang) * r, m.h, Mathf.Sin(m.ang) * r);
                float white = Sim.SmoothStep(m.age, 1.1f, 2.4f), fade = 1 - Sim.SmoothStep(m.age, 3.4f, 4.6f);
                var c = Color.Lerp(new Color(3.2f, .25f, .1f, 1), new Color(2.6f, 2.6f, 2.4f, 1), white) * fade; c.a = 1;
                Mats.SetColor(m.mat, c);
                m.go.transform.localPosition = Sim.ToUnity(m.pos); m.go.transform.localScale = Vector3.one * (.35f + .25f * white);
                if (camT) m.go.transform.rotation = camT.rotation;
            }
        }
    }
}
