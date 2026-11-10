using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Port of web-proto/src/main.js: combat, survival, summoning, pause, HUD. Simulation runs in fixed 1/120 s
    // substeps (WorldStep) in sim space. Controls come from KeyBinds (two schemes, Esc settings, 2026-11-10):
    //   WASD (default): W A S D move (camera-relative)  left click combo (aims at cursor)  right click rope
    //                   Left Shift dash (move direction, else cursor)  Space jump  slot skills 1 2 3 4 Q E
    //   click move (old Lost Ark layout): right click move  A combo  left click rope  Space dash  F jump
    //   both: T view behind the character, M map, G rest, Tab growth, wheel zoom
    //   debug: F3 / F4 summon hound / warden, F5 clear, ` + 1..6 / 8 toggle parts, P passive, C pause,
    //          '.' one frame while paused, V side camera, H get hit, K purify, F1 debug HUD, F2 base skills
    // The move code is keyed on internal ids that happen to be KeyCodes (Q whirl, W wave, E counter, R storm,
    // Space dash, F jump: Skill(), COOLDOWN, SkillDef.legacy). They are not input keys.
    public partial class SoulGame : MonoBehaviour
    {
        public const float BOUNDS = 18;
        static readonly Dictionary<string, float> DMG = new Dictionary<string, float> { { "light1", 18 }, { "light2", 18 }, { "light3", 24 }, { "heavy", 13 }, { "flurry", 11 }, { "counter", 40 }, { "jump", 0 } };
        // hit areas deliberately LARGER than the visible effect (player comfort)
        static readonly Dictionary<string, (float r, float a)> AREA = new Dictionary<string, (float, float)> {
            { "light1", (3.6f, 3.0f) }, { "light2", (3.7f, 3.2f) }, { "light3", (3.8f, 0) }, { "heavy", (4.0f, 0) }, { "flurry", (3.8f, 0) }, { "counter", (3.8f, 3.3f) } };
        const float BLADE_REACH = 1.1f, TIP_EXTEND = .5f, BREAK_REACH = .8f, WAVE_R = 2.6f;
        static readonly Dictionary<KeyCode, float> COOLDOWN = new Dictionary<KeyCode, float> { { KeyCode.Q, 4 }, { KeyCode.W, 3 }, { KeyCode.E, 5 }, { KeyCode.R, 9 }, { KeyCode.Space, 1.1f }, { KeyCode.F, .8f } };

        public SoulScene scene;
        public CharacterRig ch; public SoulAnimator anim; public Vfx vfx; public EnemyManager enemies; public Breakables breakables;
        public WorldStreamer open; public RegionAtmosphere atmo;
        public LaserBlade laser; public PixelFace face; public Secondary secondary; public Ash ash; public FollowCam cam;
        public PlayerState player = new PlayerState();
        public float timeScale = 1;
        float hitStop, lastHitAt = -10, realTime, ghostClock, cueUntil, hurtFlash; int kills;
        bool paused, showMap, fastTest;
        /// F1: debug overlays (gate hints, owned parts, coordinates, map gate markers). Off = what a player sees.
        public static bool debugHud; int stepFrames; Vector3? moveTarget; float? aimYaw; Vector3? previousTip; string lastName = "idle", cueText = "";
        readonly List<(float at, Action fn)> timers = new List<(float, Action)>();
        readonly Dictionary<Enemy, float> hitSet = new Dictionary<Enemy, float>();
        readonly Dictionary<KeyCode, float> ready = new Dictionary<KeyCode, float>();
        Transform sky;

        void After(float s, Action fn) => timers.Add((realTime + s, fn));
        void Cue(string text, float seconds = 2) { cueText = text; cueUntil = realTime + seconds; }
        Vector3 GroundPoint() => ch.root.position + new Vector3(0, .05f, 0);

        void Awake()
        {
            Application.targetFrameRate = 120;
            MoveDB.Load();
            if (!scene) scene = GetComponent<SoulScene>();
            if (!scene || !scene.Complete) { Debug.LogError("[SOUL] Scene hierarchy is not configured. Run SOUL/Build Scene Hierarchy.", this); enabled = false; return; }
            var world = scene.world;
            var preview = GameObject.Find("Character (preview)"); if (preview) preview.SetActive(false);
            ch = new CharacterRig(scene.actors); ch.root.position = new Vector3(0, 0, -.25f);
            player.pos = ch.root.position;
            cam = new FollowCam(Camera.main);
            vfx = new Vfx(scene.transientFx, ch, scene.bladeTrail);
            anim = new SoulAnimator(ch)
            {
                willPlay = n => { if (aimYaw.HasValue && n.StartsWith("light")) { anim.pendingYaw = aimYaw; aimYaw = null; } },
                dust = () => vfx.Dust(GroundPoint(), 1), step = p => vfx.Dust(p, .35f),
                land = () => { vfx.Dust(GroundPoint(), .9f); cam.Shake(.035f); },
                impact = (n, m, c) => { if (m.slam) { var p = ch.LocalToWorld(new Vector3(0, .04f, 1)); vfx.Ring(p, 1.3f); vfx.Dust(p, 1.5f); cam.Shake(.08f); } },
                fx = (kind, n) =>
                {
                    if (kind == "wave") { var dir = ch.Forward; vfx.SpawnWave(ch.root.position + new Vector3(0, .35f, 0) + dir * .6f, dir); cam.Shake(.05f); }
                    else if (kind == "parry") { var p = Vector3.LerpUnclamped(ch.bladeBase.WorldPosition, ch.bladeTip.WorldPosition, .4f); vfx.Ring(p, 1, true); vfx.Flash(p, .8f); face.Set("wide", .3f); }
                    else if (kind == "dash") { ghostClock = 0; vfx.Dust(GroundPoint(), 1.2f); }
                    PartsFx(kind, n);   // slot parts (SoulGame.PartsActive)
                },
            };
            laser = new LaserBlade(ch); face = new PixelFace(ch);
            anim.groundAt = (x, z) => WorldCollision.Floor(x, z, ch.root.position.y, .6f);
            secondary = new Secondary(ch, scene.cloth);
            enemies = new EnemyManager(scene.actors, vfx, Regions.ENEMY_BOUNDS, scene.soulWisps)
            {
                onPlayerHit = OnPlayerHit,
                onImpact = (e, a, p) =>
                {
                    if (a.name == "slam" || a.name == "quake" || a.name == "throw") { float k = a.radius / 2.5f; vfx.Ring(Sim.FlatY(p, .03f), 1.7f * k); vfx.Dust(p, 1.8f * k); vfx.Burst(Sim.FlatY(p, .2f), (int)(14 * k), k); }
                    else if (a.name == "lunge" || a.name == "roll" || a.name == "claw") vfx.Dust(e.pos, a.name == "roll" ? 1.8f : .9f);
                    else vfx.Dust(p, .7f);
                    if (Vector3.Distance(p, ch.root.position) < 8) cam.Shake(a.name == "slam" ? .1f : .04f);
                },
                onKill = e => { kills++; GainXp(Growth.KillXp(e.def)); PartsOnKill(e); },   // 2026-11-10: xp from kills
                onStep = (p, s) => { vfx.Dust(p, .8f * s); if (Vector3.Distance(p, ch.root.position) < 18) cam.Shake(.02f * s); },   // heavy footfalls (boss)
            };
            breakables = new Breakables(scene.breakables, vfx, 12, BOUNDS - 1);
            ash = new Ash(scene.driftingAsh);
            open = new WorldStreamer(scene.openWorld); open.Update(ch.root.position, true);
            var sunGo = GameObject.Find("Sun"); atmo = new RegionAtmosphere(sunGo ? sunGo.GetComponent<Light>() : null, cam.camera);
            sky = scene.dustSky;
            scene.ruinsSkyline.SetActive(false);
            ZoneInit(); RopeInit();
            ch.UpdateWorld(); ch.Sync();
            cam.Update(1, ch.root.position);
        }

        // ---------- player survival ----------
        bool Invulnerable()
        {
            string n = anim.name; float t = anim.time;
            return RopeTravel || (n == "dash" && t < DashInvulnTime) || realTime < invulnUntil || (n == "roll" && t > .04f && t < .78f) || (n == "flurry" && t < 1.18f) || (n == "jump" && t > .2f && t < .85f);
        }
        void PlayerDie()
        {
            player.alive = false; moveTarget = null; PlayMove("stagger"); face.Set("hurt", 2.2f); Cue("SYSTEM FAILURE · 재기동 중", 2.2f);
            // reboot at the last pod you rested at (a lost boss fight resets the boss)
            After(2.2f, () => { RespawnAtPod(); player.hp = player.max; player.alive = true; enemies.ClearNear(ch.root.position, 7); anim.Play("idle"); face.Set("happy", 1); Cue("재기동 완료 · " + restPod.name, 1.6f); });
        }
        void OnPlayerHit(float dmg, Vector3 from, Enemy enemy)
        {
            if (enemy != null && enemy.special) dmg *= 1.3f;   // 특이 개체 hit harder
            if (!player.alive) return;
            if (enemy != null && blindUntil.TryGetValue(enemy, out var blind) && blind > realTime) { Cue("빗나감", .4f); return; }   // 섬광 렌즈
            IsRangedHit = enemy != null && Sim.FlatY(enemy.pos - ch.root.position).magnitude > 4.5f;
            if (Invulnerable()) { Cue("회피!", .6f); vfx.Flash(Sim.FlatY(ch.root.position, 1.1f), .7f); if (anim.name == "dash") OnPerfectDodge(enemy); return; }
            if (anim.name == "counter" && anim.time >= .03f && anim.time <= .27f + CounterWindowBonus)
            {
                enemy.ToStagger(1.4f); enemy.Damage(10, ch.root.position, true); face.Set("wide", .9f); Cue("반격!", .8f);   // 놀람 = every successful counter
                var p = ch.bladeBase.WorldPosition; vfx.Ring(p, 1.4f, true); vfx.Burst(p, 26, 1.3f); cam.Shake(.09f); hitStop = .1f;
                OnCounterSuccess(enemy); return;
            }
            if (InField(ch.root.position, "steam") && ventSafeUntil > realTime) dmg *= .85f;   // 과열 배출 ①
            dmg = PartsTakeHit(dmg * OverloadTaken, from, enemy);   // 슬롯 파츠: 장갑 · 보호막 · 버티기 ... (SoulGame.PartsPassive)
            if (dmg <= 0) return;
            if (enemy != null && (staticUntil > realTime || anchoredUntil > realTime) && Sim.FlatY(enemy.pos - ch.root.position).magnitude < 4) Apply(enemy, St.Shock);
            if (enemy != null && shieldReflect && shieldHp > 0) HitEnemy(enemy, dmg * .3f, false, "shield", false, false);
            player.hp = Mathf.Max(0, player.hp - dmg); hurtFlash = 1;
            bool hold = anchoredUntil > realTime || DamperSkipsStagger;   // 거미 발 고정: no knockback / 충격 완충 관절 ①
            if (!hold) { var d = from - ch.root.position; ch.yaw = Mathf.Atan2(d.x, d.z); ch.ApplyYaw(); moveTarget = null; GetHit(); }
            cam.Shake(.11f); hitStop = Mathf.Max(hitStop, .05f);
            if (player.hp <= 0) PlayerDie();
        }

        // ---------- hits ----------
        bool Heavy(string n) => n == "light3" || n == "counter" || (n == "flurry" && anim.time > 1.15f);
        bool CanHit(Enemy e, Move m) => !hitSet.TryGetValue(e, out var last) || (m.Has(m.multi) && anim.time - last >= m.multi);
        void AreaHits(Move m)
        {
            string n = anim.name; if (!AREA.TryGetValue(n, out var area)) return;
            var f = ch.Forward; bool heavy = Heavy(n); area.r *= PartsAreaScale(n);
            PillarArea(area.r, area.a);
            foreach (var e in enemies.list)
            {
                if (!e.Hittable ||Mathf.Abs(e.pos.y - ch.root.position.y) > 1.6f) continue;   // not through floors
                var d = Sim.FlatY(e.pos - ch.root.position); float dist = d.magnitude - e.radius * .7f;
                if (dist > area.r) continue;
                if (area.a > 0 && dist > .6f && Vector3.Dot(d.normalized, f) < Mathf.Cos(area.a / 2)) continue;
                if (!CanHit(e, m)) continue;
                hitSet[e] = anim.time; LegacyHit(e, n, heavy);
                vfx.Burst(e.Center(), heavy ? 28 : 18, heavy ? 1.3f : 1); cam.Shake(heavy ? .1f : .06f); hitStop = Mathf.Max(hitStop, heavy ? .07f : .045f);
                if (heavy) face.Set("happy", .7f);
            }
        }
        void BladeHits(Move m)
        {
            string n = anim.name; Vector3 b = ch.bladeBase.WorldPosition, tip = ch.bladeTip.WorldPosition;
            tip += (tip - b).normalized * TIP_EXTEND;
            var segs = new List<(Vector3, Vector3)> { (b, tip) }; if (previousTip.HasValue) segs.Add((previousTip.Value, tip)); previousTip = tip;
            bool heavy = Heavy(n);
            foreach (var (p, q) in segs)
            {
                breakables.Hit(p, q, BREAK_REACH, ch.root.position); PillarBlade(p, q);
                foreach (var h in enemies.HitsAlong(p, q, BLADE_REACH))
                {
                    if (!CanHit(h.e, m)) continue;
                    hitSet[h.e] = anim.time; LegacyHit(h.e, n, heavy);
                    vfx.Burst(h.point, heavy ? 30 : 20, heavy ? 1.4f : 1); cam.Shake(heavy ? .1f : .06f); hitStop = Mathf.Max(hitStop, heavy ? .07f : .045f);
                    if (heavy) face.Set("happy", .7f);
                }
            }
        }

        /// a hit of the A combo or a legacy skill move: through the parts pipeline (HitEnemy) + its extras
        void LegacyHit(Enemy e, string n, bool heavy)
        {
            string src = PartOfMove(n) ?? n; bool basic = n.StartsWith("light");
            HitEnemy(e, (DMG.TryGetValue(n, out var dm) ? dm : 15) * SkillDamageScale(n) * LegacyScale(e, src), heavy, src, basic);
            LegacyHitExtras(e, src, basic);
            if (n == "light3" && !comboDone) { comboDone = true; OnComboFinish(); }
        }
        bool comboDone;

        // ---------- actions ----------
        /// double jump (2026-11-09, needs the 도약 관절 leg part from the Z0 elite): F again during the jump arc. Starts from the current arc height with a 1.25x arc,
        /// so the peak is ~2.36 m above the take-off floor: low terrain shelves (Z0 2.1 m, Z3 / Z5 2.0 m) can be
        /// mounted; rope ledges (2.6 m) and the 3+ m cliffs of Z1 / Z2 / Z4 stay out of reach.
        bool airJumped; const float AIR_JUMP_SCALE = 1.25f, AIR_JUMP_REACH = 1.8f;
        /// running jump: forward reach multiplier on the jump root motion (0.9 m -> ~2.6 m); the move order to resume
        const float RUN_JUMP_REACH = 2.9f; Vector3? moveOrder, resumeMove;
        bool AirJump()
        {
            if (!Parts.Has(Parts.Leap)) return false;   // leg upgrade 도약 관절 (Z0 elite 무리의 우두머리): shelves above 1 jump are part-gated
            if (!player.alive || airJumped || anim.name != "jump" || !anim.InArc || Falling) return false;
            var p = ch.root.position; p.y += anim.ArcHeight; ch.root.position = p;
            previousTip = null; vfx.ResetTrail(); anim.AirJump(AIR_JUMP_SCALE, AIR_JUMP_REACH); airJumped = true;
            vfx.Dust(p + new Vector3(0, .05f, 0), .5f);
            return true;
        }
        bool PlayMove(string n) { previousTip = null; vfx.ResetTrail(); return anim.Play(n); }
        /// MCP tests: aim at this point instead of the mouse cursor
        public Vector3? testAim;
        float YawToCursor() { if (testAim.HasValue) { var td = testAim.Value - ch.root.position; return Mathf.Atan2(td.x, td.z); } var p = cam.Pick(Input.mousePosition, ch.root.position.y); if (!p.HasValue) return ch.yaw; var d = p.Value - ch.root.position; return Mathf.Atan2(d.x, d.z); }
        void FaceCursor() => anim.pendingYaw = YawToCursor();
        bool CanAct() { var m = anim.move; return !anim.busy || (anim.name.StartsWith("light") && anim.time > (m.Has(m.impact) ? m.impact : 0) + .04f) || anim.name == "heavyCharge"; }
        void Skill(KeyCode code)
        {
            if (code == KeyCode.Space && ready.TryGetValue(code, out var rr) && rr > realTime && UseDashCharge()) ready.Remove(code);   // 추가 연료통
            if (!player.alive || (ready.TryGetValue(code, out var r) && r > realTime)) return;
            if (code != KeyCode.Space && !CanAct()) return;
            moveOrder = moveTarget; moveTarget = null;
            if (code == KeyCode.Space)
            {   // WASD: dash along the held move direction; no move input (and the click scheme) = toward the cursor
                var md = MoveDir(out _);
                if (md != Vector3.zero) anim.pendingYaw = Mathf.Atan2(md.x, md.z);
                else if (cam.Pick(Input.mousePosition, ch.root.position.y).HasValue) FaceCursor();
                PlayMove("dash"); OnDash();
            }
            else if (code == KeyCode.Q) { FaceCursor(); PlayMove("heavyCharge"); After(.2f, () => { if (anim.name == "heavyCharge") PlayMove("heavy"); }); face.Set("wide", .2f); }
            else if (code == KeyCode.W) { FaceCursor(); PlayMove("wave"); }
            else if (code == KeyCode.E) { FaceCursor(); PlayMove("counter"); }
            else if (code == KeyCode.R) { FaceCursor(); PlayMove("jump"); After(.3f, () => { if (anim.name == "jump") PlayMove("flurry"); }); }
            else if (code == KeyCode.F)
            {   // running jump (2026-11-09): moving toward a target, the jump carries the run forward (~2.6 m) along the
                // move direction and the walk resumes on landing, so mounds and low ledges are hopped in stride
                Vector3? run = null;
                var md = MoveDir(out _);   // WASD: the held direction is the run (the walk resumes by itself, the keys are still down)
                if (md != Vector3.zero) run = ch.root.position + md * 4;
                else if (moveOrder.HasValue) { var dv = moveOrder.Value - ch.root.position; dv.y = 0; if (dv.magnitude > .6f) run = moveOrder; }
                if (run.HasValue) { var dv = run.Value - ch.root.position; anim.pendingYaw = Mathf.Atan2(dv.x, dv.z); }
                PlayMove("jump"); airJumped = false;
                if (run.HasValue) { anim.rootScale = RUN_JUMP_REACH; if (!KeyBinds.Wasd) resumeMove = run; }
            }
            ready[code] = realTime + COOLDOWN[code];
        }
        void Attack()
        {
            if (!player.alive) return;
            moveTarget = null;
            if (!anim.busy)
            {
                FaceCursor(); PlayMove("light1");
                if (lightframeReady) { anim.rootScale = 6; lightframeSlash = true; sprintFor = 0; if (P("lightframe") is var lf && lf != null && lf.B1) SpeedBuff(1.3f, 1.2f); }   // 가벼운 프레임: 4 m 돌진 베기
                return;
            }
            aimYaw = YawToCursor();
            var before = anim.name; anim.Light(); if (before != anim.name) { previousTip = null; vfx.ResetTrail(); }
        }
        void GetHit() { bool stagger = realTime - lastHitAt < 1; lastHitAt = realTime; PlayMove(stagger ? "stagger" : "hit"); cam.Shake(.055f); face.Set("hurt", .6f); }
        void Summon(string type)
        {
            var p = cam.Pick(Input.mousePosition, ch.root.position.y) ?? ch.root.position + ch.Forward * 4;
            if (Vector3.Distance(p, ch.root.position) < 1.6f) p = ch.root.position + Sim.SetLength(Sim.FlatY(p - ch.root.position), 1.6f);
            enemies.SpawnAt(type, p);
        }
        void TogglePassive() { enemies.passive = !enemies.passive; Cue(enemies.passive ? "허수아비 모드: 몬스터가 공격하지 않음" : "몬스터 공격 켜짐", 1.4f); }
        void TogglePause() { paused = !paused; stepFrames = 0; }
        void TogglePart(int id) { Parts.Toggle(id); Cue($"{Parts.Names[id]} {(Parts.Has(id) ? "장착" : "해제")} · 보유: {Parts.Owned()}", 1.6f); }

        void HandleInput()
        {
            if (ShopInput()) return;   // merchant window (SoulGame.Field) eats input while open
            if (SettingsInput()) return;   // Esc settings window (SoulGame.Settings): pauses and eats input while open
            if (SkillInput()) return;   // skill keys / the new-skill key prompt (SoulGame.Skills)
            // rope arm: right click in WASD, left click in click-move (KeyBinds.RopeButton); hold it through a swing
            var ropeBtn = KeyBinds.RopeButton;
            ropeHeld = KeyBinds.Held(ropeBtn);
            if (!showMap && KeyBinds.Pressed(ropeBtn)) { var c = cam.Pick(Input.mousePosition, ch.root.position.y); if (c.HasValue) FireRope(c.Value); }
            if (RopeHolds) { if (KeyBinds.Down(KeyBinds.Act.Map)) showMap = !showMap; return; }   // on the rope: nothing else
            // basic controls come from the key map (KeyBinds, Esc settings); Skill() still keys on its internal ids.
            // An action bound to a mouse button (WASD: attack = left click) leaves the click to the open map (pod travel, pin).
            if (BasicDown(KeyBinds.Act.Attack)) Attack();
            if (BasicDown(KeyBinds.Act.Dash)) Skill(KeyCode.Space);   // 스킬 = 파츠: only dash / jump are basic keys
            if (BasicDown(KeyBinds.Act.Jump)) { if (!AirJump()) Skill(KeyCode.F); }
            // debug (2026-11-10: moved off the number keys and Shift, which are slot skills and dash now)
            if (DebugLayer) { for (int i = Parts.Rope; i <= Parts.LabKeys; i++) if (Input.GetKeyDown(KeyCode.Alpha0 + i)) TogglePart(i); if (Input.GetKeyDown(KeyCode.Alpha8)) TogglePart(Parts.Leap); }   // ` + 1..6 parts, ` + 8 도약 관절
            if (Input.GetKeyDown(KeyCode.F3)) Summon("crawler");
            if (Input.GetKeyDown(KeyCode.F4)) Summon("warden");
            if (Input.GetKeyDown(KeyCode.F5)) enemies.ClearAll();
            if (Input.GetKeyDown(KeyCode.P)) TogglePassive();
            if (Input.GetKeyDown(KeyCode.H)) GetHit();
            if (Input.GetKeyDown(KeyCode.C)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Period) && paused) stepFrames++;
            if (Input.GetKeyDown(KeyCode.V)) cam.Toggle();
            // T (like LoL Space): swing the view behind the character (smooth); it stays there (press again to re-aim)
            if (KeyBinds.Down(KeyBinds.Act.View)) cam.TurnTo(Sim.WrapPi(ch.yaw + Mathf.PI));
            if (KeyBinds.Down(KeyBinds.Act.Map)) showMap = !showMap;
            if (Input.GetKeyDown(KeyCode.F1)) { debugHud = !debugHud; Cue(debugHud ? "디버그 표시 켜짐" : "디버그 표시 꺼짐", 1); }
            if (KeyBinds.Down(KeyBinds.Act.Rest)) RestAtPod();
            KitInput();   // X: repair kit (SoulGame.Field)
            if (Input.GetKeyDown(KeyCode.K)) TogglePurify();
            if (showMap && Input.GetMouseButtonDown(0))
            {   // debug: click the world map to teleport there
                var r = MapRect(); var mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (r.Contains(mp)) MapClick(r, mp);
            }
            if (showMap && Input.GetMouseButtonDown(1))
            {   // map pin: right-click sets / clears it (SoulGame.Guide)
                var r = MapRect(); var mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (r.Contains(mp)) MapPinClick(r, mp);
            }
            float wheel = Input.mouseScrollDelta.y; if (Mathf.Abs(wheel) > .01f) cam.ZoomBy(-Mathf.Sign(wheel) * .08f);
            // click-move scheme only: right click (hold) orders a move to the floor point under the cursor
            if (!KeyBinds.Wasd && !showMap && KeyBinds.Held(KeyBinds.ClickMoveButton) && player.alive) { var p = PickFloor(Input.mousePosition) ?? cam.Pick(Input.mousePosition, ch.root.position.y); if (p.HasValue) { if (KeyBinds.Pressed(KeyBinds.ClickMoveButton)) MoveMarkerClick(p.Value); moveTarget = p; } }
        }
        /// a basic action was pressed this frame; one bound to a mouse button does not fire while the map is open
        bool BasicDown(KeyBinds.Act a) => KeyBinds.Down(a) && !(showMap && KeyBinds.Get(a) >= KeyCode.Mouse0);
        /// debug layer: hold ` (backquote) and press a number (parts, grudge drop). Slot skills on the same numbers stay quiet.
        static bool DebugLayer => Input.GetKey(KeyCode.BackQuote);

        // ---------- frame ----------
        void Update()
        {
            if (settingsOpen) { HandleInput(); cam.Update(0, ch.root.position); return; }   // Esc settings: world and realTime frozen (the closing Esc must not reopen it this frame)
            float realDt = Mathf.Min(Time.unscaledDeltaTime, .05f); realTime += realDt;
            HandleInput();
            if (paused)
            {
                float stepDt = stepFrames > 0 ? 1 / 60f : 0; stepFrames = Mathf.Max(0, stepFrames - 1);
                if (stepDt == 0) { cam.Update(0, ch.root.position); return; }
            }
            for (int i = timers.Count - 1; i >= 0; i--) if (timers[i].at <= realTime) { var fn = timers[i].fn; timers.RemoveAt(i); fn(); }
            float dt = paused ? 1 / 60f : realDt * timeScale; if (hitStop > 0) { hitStop = Mathf.Max(0, hitStop - realDt); dt = 0; }
            var input = FrameInput();
            float remaining = dt;
            do { float step = Mathf.Min(remaining, 1 / 120f); WorldStep(step, input); remaining -= step; } while (remaining > 1e-7f);
            FrameTail(realDt);
        }
        // ---------- elevation: walls, ledges, step-up, jump-mount, falling ----------
        float fallV; public bool Falling { get; private set; }
        const float PLAYER_R = .38f, GRAVITY = 22;
        void Vertical(float dt, Vector3 before)
        {
            var p = ch.root.position;
            bool arc = anim.InArc;
            // what can be climbed this substep: a step, or the jump arc's clearance
            float climb = Falling ? .05f : arc ? Mathf.Max(WorldCollision.STEP, anim.ArcHeight + .12f) : WorldCollision.STEP;
            var moved = WorldCollision.Move(new Vector3(before.x, p.y, before.z), p, PLAYER_R, climb);
            p.x = moved.x; p.z = moved.z;
            float floor = WorldCollision.Floor(p.x, p.z, p.y, climb);
            if (floor > p.y + 1e-3f)
            {
                float rise = floor - p.y;
                if (arc && rise > WorldCollision.STEP * .5f) { anim.AirLift(rise); p.y = floor; }    // landed on a ledge mid-jump
                else p.y += rise * Mathf.Min(1, Sim.Damp(28, dt) + .25f);                            // stairs / ramps
                fallV = 0; Falling = false;
            }
            else if (floor < p.y - 1e-3f && !arc)
            {
                if (!Falling && p.y - floor < .14f) p.y = floor;                                       // walking down a slope
                else
                {
                    Falling = true; fallV -= GRAVITY * dt; p.y += fallV * dt;
                    if (p.y <= floor)
                    {
                        p.y = floor; Falling = false;
                        if (fallV < -5) { vfx.Dust(p + new Vector3(0, .05f, 0), Mathf.Min(1.6f, -fallV * .12f)); cam.Shake(Mathf.Min(.12f, -fallV * .01f)); }
                        fallV = 0;
                    }
                }
            }
            else { fallV = 0; Falling = false; }
            // left standing on a bare cliff face (a jump ran out of reach mid-face): slide off downhill
            if (!arc && !Falling && Mathf.Abs(p.y - Terrain.Height(p.x, p.z)) < .05f && WorldCollision.SteepAt(p.x, p.z, out var down))
            {
                var q = WorldCollision.Move(p, p + new Vector3(down.x, 0, down.y) * 3.5f * dt, PLAYER_R, WorldCollision.STEP);
                p.x = q.x; p.z = q.z; float f2 = WorldCollision.Floor(p.x, p.z, p.y, WorldCollision.STEP); if (f2 < p.y) p.y = f2;
            }
            anim.airborne = Falling;
            ch.root.position = p; ch.UpdateWorld();
        }

        /// One simulation substep: animation, hits, waves, enemies, effects (also usable for tests).
        public void WorldStep(float step, Vector3 input)
        {
            var before = ch.root.position;
            WaterStep(step);
            anim.moveRate = MoveRate(anim.name);   // 공격 속도 (SoulGame.PartsPassive)
            anim.Update(step, RopeHolds ? Vector3.zero : input, true);
            if (RopeTravel) RopeMove(step, before); else Vertical(step, before);
            RopeTick(step); RopeArmPose();
            if (anim.name != lastName) { previousTip = null; lastName = anim.name; hitSet.Clear(); pillarHitSet.Clear(); comboDone = false; }
            var m = anim.move; bool active = m?.active != null && anim.time >= m.active[0] && anim.time <= m.active[1];
            if (active && !m.parry && !PartMove(anim.name)) { BladeHits(m); if (anim.time >= (m.Has(m.impact) ? m.impact : m.active[0]) - .02f) AreaHits(m); } else if (!active) previousTip = null;
            foreach (var w in vfx.waves)
            {
                var c = w.pos; PillarWave(w);
                foreach (var e in enemies.list)
                    if (e.Hittable &&!w.hitSet.Contains(e) && Mathf.Abs(e.pos.y - c.y + .35f) < 1.8f && Mathf.Sqrt((c.x - e.pos.x) * (c.x - e.pos.x) + (c.z - e.pos.z) * (c.z - e.pos.z)) < WAVE_R + e.radius * .5f)
                    { w.hitSet.Add(e); HitEnemy(e, 30 * SkillDamageScale("wave") * LegacyScale(e, "wave") * w.scale, true, "wave"); LegacyHitExtras(e, "wave", false); vfx.Burst(e.Center(), 24, 1.2f); cam.Shake(.06f); }
                breakables.Hit(Sim.FlatY(c, .2f), Sim.FlatY(c, 1.2f), .9f, c);
            }
            player.pos = ch.root.position;
            enemies.Update(step * WorldSlow, player); breakables.Update(step); pickups.Update(step, ch.root.position);
            PartsUpdate(step); ActiveTick(step);   // slot parts: statuses, fields, heat, minions (SoulGame.Parts*)
            if (step > 0)
            {
                ghostClock -= step;
                bool dashing = anim.name == "dash" && anim.time > .02f && anim.time < .32f;
                bool spinning = (anim.name == "light3" || anim.name == "heavy" || anim.name == "flurry") && active;
                if ((dashing || spinning) && ghostClock <= 0) { ch.Sync(); vfx.Afterimage(dashing ? 1 : .55f); ghostClock = dashing ? .03f : .07f; }
            }
            Secondary.GroundY = ch.root.position.y; if (!fastTest) secondary.Update(step, anim.worldTime, anim.speed);
            vfx.Update(step, active, anim.name == "heavyCharge" ? anim.charge : 0);
            laser.Update(step, active ? 1 : anim.name == "heavyCharge" ? .6f : 0);
        }
        /// cutaway: world geometry this far above the feet, between camera and player, is dithered away (SoulLit)
        public const float CUT_ABOVE = 2.3f, CUT_RADIUS = 9.5f;
        void FrameTail(float realDt)
        {
            string n = anim.name;
            string state = n == "hit" || n == "stagger" ? "hurt" : n == "heavyCharge" ? "wide" : anim.busy && n != "jump" ? "focus" : "idle";
            face.Update(realDt, state);
            open.Update(ch.root.position); VistaUpdate(realDt); GrudgeUpdate(realDt); SkillsUpdate(realDt); atmo.Update(realDt, ch.root.position); ZoneUpdate(realDt);
            // top-down cutaway: dither out world geometry above head height between the camera and the player
            // (terrain shelves too, 2026-11-09: a higher shelf on the camera side used to cover the player like a roof)
            var camDirSim = Sim.ApplyAxisAngle(new Vector3(0, 0, 1), Vector3.up, cam.yaw); var camDirU = Sim.ToUnity(camDirSim);
            Shader.SetGlobalVector("_SoulCutCenter", (Vector4)Sim.ToUnity(ch.root.position + camDirSim * 3) + new Vector4(0, 0, 0, 9.5f));
            Shader.SetGlobalVector("_SoulCutParams", new Vector4(CUT_ABOVE, 2.5f, camDirU.x, camDirU.z));
            ash.Update(realDt, ch.root.position);
            if (sky) sky.position = Sim.ToUnity(ch.root.position);
            hurtFlash = Mathf.Max(0, hurtFlash - realDt * 2.5f);
            ch.Sync(); RopeVisuals();
            EcologyUpdate(realDt);   // 영역 생태계 (SoulGame.Ecology)
            LoreUpdate(realDt);   // 생존자 기록 (SoulGame.Lore)
            MoveMarkerUpdate(realDt);   // right-click destination (SoulGame.Move)
            GuideUpdate(realDt);   // 경로 유도: explore camera, wisps, map pin (SoulGame.Guide)
            CamFollowUpdate(realDt);   // WASD: the view eases behind a sustained forward run (SoulGame.Move, FollowCam.Follow)
            cam.Update(realDt, ch.root.position);
        }

        // ---------- automation / audit (same as web __proto) ----------
        /// Run every move from idle and collect the animator's quality stats (penetration, limits...).
        public Dictionary<string, Dictionary<string, float>> Audit(IEnumerable<string> list = null)
        {
            var outp = new Dictionary<string, Dictionary<string, float>>();
            foreach (var n in list ?? MoveDB.order)
            {
                ch.root.position = Vector3.zero; ch.yaw = 0; ch.ApplyYaw(); anim.Play("idle"); anim.ResetFeet();
                for (int i = 0; i < 60; i++) anim.Update(1 / 120f, Vector3.zero, false);
                anim.stats.Clear();
                if (n == "heavy") { anim.Play("heavyCharge"); for (int i = 0; i < 40; i++) anim.Update(1 / 120f, Vector3.zero, false); }
                anim.Play(n); int steps = Mathf.CeilToInt((MoveDB.moves[n].duration + .35f) * 120);
                for (int i = 0; i < steps; i++) { anim.Update(1 / 120f, Vector3.zero, false); if (anim.name == "heavyCharge" && i > 150) break; }
                var s = new Dictionary<string, float>(); foreach (var kv in anim.stats) if (kv.Value > .0005f) s[kv.Key] = Mathf.Round(kv.Value * 1000) / 1000; outp[n] = s;
            }
            ch.root.position = new Vector3(0, 0, -.25f); anim.Play("idle"); anim.ResetFeet(); anim.stats.Clear();
            return outp;
        }
        public string AuditText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in Audit()) { sb.Append(kv.Key).Append(": "); foreach (var s in kv.Value) sb.Append(s.Key).Append('=').Append(s.Value).Append("; "); sb.Append('\n'); }
            return sb.ToString();
        }
        /// Synchronous simulation (seconds of 120 Hz substeps); action: "attack", "Q", "W", "E", "R", "Space", "F", "summon:crawler".
        public void Simulate(float seconds, Vector3 input = default, bool clock = false)
        {
            int n = Mathf.RoundToInt(seconds * 120); fastTest = true;
            for (int i = 0; i < n; i++)
            {
                WorldStep(1 / 120f, input);
                if (clock) { realTime += 1 / 120f; for (int k = timers.Count - 1; k >= 0; k--) if (timers[k].at <= realTime) { var fn = timers[k].fn; timers.RemoveAt(k); fn(); } }   // parts tests: game clock + timers too
            }
            fastTest = false; secondary.Reset(); ch.Sync();
        }   // cloth skipped (7 ms / substep)
        /// Walk toward `target` (sim) for up to `seconds`; returns a trace "x,z,y" every 0.25 s (elevation tests).
        public string WalkTest(Vector3 target, float seconds, float jumpAtY = -1)
        {
            var sb = new System.Text.StringBuilder(); int n = Mathf.RoundToInt(seconds * 120); fastTest = true;   // cloth is 7 ms / substep; skipped here, reset after
            for (int i = 0; i < n; i++)
            {
                var d = target - ch.root.position; d.y = 0; if (d.magnitude < .3f) break;
                WorldStep(1 / 120f, d.normalized);
                if (i % 30 == 0) sb.Append($"{ch.root.position.x:F1},{ch.root.position.z:F1},y{ch.root.position.y:F2}{(Falling ? " fall" : "")} | ");
            }
            fastTest = false; secondary.Reset(); ch.Sync(); cam.Update(1, ch.root.position);
            sb.Append($"END {ch.root.position.x:F2},{ch.root.position.z:F2},y{ch.root.position.y:F2}");
            return sb.ToString();
        }
        /// move the player anywhere in the world (debug map click, MCP tests); streams the area in first
        public void Teleport(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, WorldCollision.worldRect.xMin, WorldCollision.worldRect.xMax); p.z = Mathf.Clamp(p.z, WorldCollision.worldRect.yMin, WorldCollision.worldRect.yMax);
            open.Update(p, true);
            p.y = 0; WorldCollision.Resolve(ref p, .4f); p.y = WorldCollision.Height(p.x, p.z, 3);
            moveTarget = null; ch.root.position = p; ch.UpdateWorld(); rs = RopeState.None;
            anim.Play("idle"); anim.ResetFeet(); secondary.Reset();
            open.Update(p, true); ch.Sync(); cam.Update(1, p);
            Cue($"순간이동 · {Regions.All[Regions.At(p)].name} ({p.x:F0}, {p.z:F0})", 1.4f);
        }
        public void Do(string action)
        {
            if (action == "attack") Attack();
            else if (action.StartsWith("summon:")) enemies.SpawnAt(action.Substring(7), ch.root.position + ch.Forward * 3);
            else if (Enum.TryParse<KeyCode>(action, out var k) && COOLDOWN.ContainsKey(k)) Skill(k);
            else PlayMove(action);
        }

        // ---------- HUD ----------
        static Texture2D white; GUIStyle small, big, keyStyle;
        void OnGUI()
        {
            if (!white) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (small == null) { small = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(.85f, .93f, 1f) } }; big = new GUIStyle(small) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; keyStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 }; }
            float W = Screen.width, H = Screen.height;
            // Esc settings: its controls see input events first and eat the rest, so HUD buttons underneath never fire
            if (settingsOpen && Event.current.type != EventType.Repaint) { SettingsGUI(W, H); if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use(); return; }
            if (hurtFlash > 0) { GUI.color = new Color(.8f, .05f, .02f, hurtFlash * .35f); GUI.DrawTexture(new Rect(0, 0, W, H), white); }
            enemies?.DrawGUI(cam.camera); atmo?.DrawGUI();
            // hp
            if (UiArt.Kit)
            {   // generated frame (UI/ui_kit.png): fill sits in the frame's inner slot
                var fr = new Rect(14, 10, 320, 52); float ix = fr.x + fr.width * .085f, iw = fr.width * .83f, iy = fr.y + fr.height * .36f, ih = fr.height * .28f;
                GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(ix, iy, iw, ih), white);
                GUI.color = new Color(.25f, .75f, 1f, .95f); GUI.DrawTexture(new Rect(ix, iy, iw * player.hp / player.max, ih), white);
                GUI.color = Color.white; UiArt.Piece(fr, UiArt.HpBar);
                GUI.Label(new Rect(ix + iw - 120, iy - 4, 116, 20), $"{Mathf.CeilToInt(player.hp)} / {player.max}", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 11 });
                // owned movement parts
                float px = 220;
                for (int pi = Parts.Rope; pi <= Parts.LabKeys; pi++) if (Parts.Has(pi)) { UiArt.Icon(new Rect(px, fr.yMax - 4, 26, 26), UiArt.PartIcon(pi)); px += 28; }
            }
            else
            {
                GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(new Rect(20, 20, 260, 18), white);
                GUI.color = new Color(.25f, .75f, 1f, .95f); GUI.DrawTexture(new Rect(22, 22, 256 * player.hp / player.max, 14), white);
                GUI.color = Color.white; GUI.Label(new Rect(26, 19, 250, 20), $"{Mathf.CeilToInt(player.hp)} / {player.max}", small);
            }
            if (debugHud) GUI.Label(new Rect(20, 162, 300, 20), $"처치 {kills}", small);
            if (debugHud && atmo?.Current != null) GUI.Label(new Rect(20, 142, 400, 20), $"{atmo.Current.name}  ·  {ch.root.position.x:F0}, {ch.root.position.z:F0}", small);
            if (debugHud) GUI.Label(new Rect(20, 182, 400, 20), anim.name.ToUpper() + (anim.name.StartsWith("light") ? $"   0{anim.name[anim.name.Length - 1]} / 03" : ""), small);
            // skill bar
            var slots = new System.Collections.Generic.List<(string key, string label, int icon, float cdLeft)>(); SkillBar(slots);   // learned skills only
            bool art = UiArt.Kit && UiArt.HasIcons;
            float sw = art ? 70 : 74, x0 = W / 2 - slots.Count * sw / 2;
            for (int i = 0; i < slots.Count; i++)
            {
                var r = art ? new Rect(x0 + i * sw + 3, H - 88, sw - 6, (sw - 6) * 196f / 188f) : new Rect(x0 + i * sw + 4, H - 74, sw - 8, 56);
                if (art)
                {   // generated slot frame + icon; cooldown darkens the icon from the top
                    var inner = new Rect(r.x + r.width * .14f, r.y + r.height * .14f, r.width * .72f, r.height * .72f);
                    GUI.color = new Color(.02f, .03f, .04f, .85f); GUI.DrawTexture(inner, white);
                    GUI.color = Color.white; if (slots[i].icon < 0) PartIconArt(inner, -slots[i].icon - 1); else UiArt.Icon(inner, slots[i].icon, .08f);
                    if (slots[i].cdLeft > 0) { GUI.color = new Color(0, 0, 0, .65f); GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width, inner.height * slots[i].cdLeft), white); }
                    GUI.color = Color.white; UiArt.Piece(r, UiArt.Slot);
                    GUI.Label(new Rect(r.x, r.yMax - 8, r.width, 18), slots[i].key, keyStyle);
                    continue;
                }
                GUI.color = new Color(.02f, .05f, .08f, .75f); GUI.DrawTexture(r, white);
                if (slots[i].cdLeft > 0) { GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * slots[i].cdLeft), white); }
                GUI.color = Color.white; GUI.Label(new Rect(r.x, r.y + 4, r.width, 22), slots[i].key, keyStyle); GUI.Label(new Rect(r.x, r.y + 28, r.width, 20), slots[i].label, keyStyle);
            }
            GUI.color = Color.white;
            string moveHint = KeyBinds.Wasd ? "WASD 이동" : "우클릭 이동", ropeHint = KeyBinds.KeyName(KeyBinds.RopeButton) + " 로프";
            string hint = realTime < cueUntil ? cueText : $"{moveHint} · {KeyBinds.Name(KeyBinds.Act.Attack)} 공격 · {ropeHint} · {KeyBinds.Name(KeyBinds.Act.Dash)} 대쉬 · {KeyBinds.Name(KeyBinds.Act.Jump)} 점프 · {KeyBinds.Name(KeyBinds.Act.Growth)} 스킬 · {KeyBinds.Name(KeyBinds.Act.Rest)} 휴식 · {KeyBinds.Name(KeyBinds.Act.Map)} 지도 · 휠 줌 · Esc 설정 · F3/F4 소환 · F5 제거 · P 허수아비 · C 정지";
            GUI.Label(new Rect(0, H - 104, W, 24), hint, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            // summon panel
            float bx = W - 170;
            if (GUI.Button(new Rect(bx, 20, 150, 26), "F3  하운드 소환")) Summon("crawler");
            if (GUI.Button(new Rect(bx, 50, 150, 26), "F4  워든 소환")) Summon("warden");
            if (GUI.Button(new Rect(bx, 80, 150, 26), "F5  전부 제거")) enemies.ClearAll();
            if (GUI.Button(new Rect(bx, 110, 150, 26), (enemies.passive ? "● " : "") + "P  허수아비 모드")) TogglePassive();
            if (paused) GUI.Label(new Rect(0, H * .18f, W, 40), "PAUSED   ·   C 다시 재생 · . 한 프레임 진행", big);
            // gate hint near a link site: debug only (F1). In play the terrain itself says what is needed.
            var site = debugHud ? ZoneWorld.NearestSite(ch.root.position, 22) : null;
            if (site != null && site.link.kind != ZoneGraph.Kind.Free)
            {
                bool pass = site.link.kind == ZoneGraph.Kind.Drop || Parts.Has(site.link.part);
                string to = $"Z{site.link.a} {ZoneGraph.Zones[site.link.a].name} → Z{site.link.b} {ZoneGraph.Zones[site.link.b].name}";
                string what = site.link.kind == ZoneGraph.Kind.Drop ? "낙하 · 한 방향" : pass ? $"{Parts.Names[site.link.part]} · 통과 가능" : $"{Parts.Names[site.link.part]} 필요";
                GUI.color = site.Gate ? MeshBuilder.Hex(ZoneGates.PartHex[site.link.part]) : Color.white;
                GUI.Label(new Rect(0, H * .68f, W, 30), $"[{what}]  {site.link.note}", new GUIStyle(big) { fontSize = 18 });
                GUI.color = Color.white; GUI.Label(new Rect(0, H * .68f + 28, W, 20), to, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
            if (debugHud) GUI.Label(new Rect(20, 102, 500, 20), $"파츠: {Parts.Owned()}   ·   {KeyBinds.Name(KeyBinds.Act.Map)} 지도 · ` + 1~6 파츠 토글 (` + 8 도약 관절) · F1 디버그 표시", small);
            ZoneGUI(W, H); EcologyGUI(W, H); SubtitleGUI(W, H); FieldGUI(W, H); RopeGUI(); GrudgeGUI(W, H); SkillsGUI(W, H);
            PinGUI(W, H);
            if (showMap) DrawMap();
            SettingsGUI(W, H);   // Esc settings window, drawn last (on top)
        }

        Rect MapRect()
        {
            float h = Screen.height * .78f, w = h * ZoneWorld.MAP_W / ZoneWorld.MAP_H;
            if (w > Screen.width * .9f) { w = Screen.width * .9f; h = w * ZoneWorld.MAP_H / ZoneWorld.MAP_W; }
            return new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        }
        void DrawMap()
        {
            var r = MapRect();
            GUI.color = new Color(0, 0, 0, .7f); GUI.DrawTexture(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 34), white);
            if (UiArt.Paper) { GUI.color = Color.white; GUI.DrawTexture(r, UiArt.Paper, ScaleMode.ScaleAndCrop); GUI.color = new Color(1, 1, 1, .8f); }   // scavenged survey paper under the map
            else GUI.color = Color.white;
            var painted = MapPainter.Texture();   // illustrated map (built over a few frames), flat map until then
            GUI.DrawTexture(r, painted ? painted : ZoneWorld.MapTexture()); GUI.color = Color.white;
            if (!painted && MapPainter.Progress > 0) GUI.Label(new Rect(r.x + 12, r.yMax - 26, 300, 20), $"지도 그리는 중 {MapPainter.Progress * 100:F0}%", small);
            if (!debugHud) GUI.DrawTexture(r, MapFog.Texture());   // fog of war (F1 shows everything)
            UiArt.Nine(new Rect(r.x - 22, r.y - 22, r.width + 44, r.height + 44), UiArt.Panel, .13f, 46);   // generated riveted frame
            Vector2 P(Vector2 world) { var uv = ZoneWorld.MapUV(new Vector3(world.x, 0, world.y)); return new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height); }
            var label = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
            foreach (var z in ZoneGraph.Zones)
            {
                if (!debugHud && !MapFog.ZoneTouched(z.id)) continue;
                var c = P(ZoneWorld.Center(z.id));
                GUI.color = new Color(0, 0, 0, .8f); GUI.Label(new Rect(c.x - 99, c.y - 9, 200, 20), $"Z{z.id} {z.name}", label);
                GUI.color = Color.white; GUI.Label(new Rect(c.x - 100, c.y - 10, 200, 20), $"Z{z.id} {z.name}", label);
            }
            if (debugHud) foreach (var st in ZoneWorld.Sites)
            {
                var c = P(st.pos); bool part = st.link.kind == ZoneGraph.Kind.Part, owned = !part || Parts.Has(st.link.part);
                GUI.color = part ? MeshBuilder.Hex(ZoneGates.PartHex[st.link.part]) : st.link.kind == ZoneGraph.Kind.Drop ? new Color(.6f, .6f, .6f) : new Color(.85f, .85f, .85f);
                float sz = owned ? 9 : 7; GUI.DrawTexture(new Rect(c.x - sz / 2, c.y - sz / 2, sz, sz), white);
                // short tick toward link.b
                for (int i = 1; i <= 3; i++) { var q = c + new Vector2(st.n.x, st.n.y) * (sz / 2 + i * 2.2f); GUI.DrawTexture(new Rect(q.x - 1.5f, q.y - 1.5f, 3, 3), white); }
            }
            var pp = P(new Vector2(ch.root.position.x, ch.root.position.z));
            DrawMapZone(r, pp); DrawMapPin(r);
            GUI.color = new Color(.3f, .85f, 1f); GUI.DrawTexture(new Rect(pp.x - 5, pp.y - 5, 10, 10), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.yMax + 4, r.width, 22), debugHud ? "M 닫기 · 포드 클릭 = 빠른 이동 · 우클릭 = 핀 · Ctrl+클릭 = 순간이동 · 색 점 = 파츠 게이트 · 회색 = 통로/낙하 · 빨강 = 보스" : "M 닫기 · 발견한 포드를 누르면 빠른 이동 · 우클릭으로 핀 꽂기", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
