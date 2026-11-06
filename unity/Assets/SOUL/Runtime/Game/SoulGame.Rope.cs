using UnityEngine;

namespace Soul
{
    // 로프 팔 (left-arm grapple, stage 4). Left click fires toward the cursor; what it does depends on
    // what is under / along the cursor (RopeAim):
    //   small enemy  -> yank it in front of you + stagger      big enemy -> zip to it and open with a slash
    //   pickup       -> snatch it back                         swing hook -> pendulum arc across the gap
    //   climb anchor / ledge edge -> reel up, hang under the lip, mantle on top
    // Hold the button through a swing; letting go after ~30 % releases you ballistically.
    // Travel ignores walls (the path was checked when aiming) and makes you untouchable.
    public partial class SoulGame
    {
        enum RopeState { None, Fly, Retract, Yank, Reel, Mantle, Swing, Ballistic, Zip }
        public enum RopeGoal { Miss, Climb, Edge, Swing, Yank, Zip, Item }
        public struct RopeAim { public RopeGoal goal; public Vector3 point, land; public RopeAnchor anchor; public Enemy enemy; public Pickups.Item item; public string Label; }

        RopeState rs; RopeAim aim; float rt, ropeClock, ropeReady, ropeCombatReady; bool ropeHeld;
        Vector3 hook, ropeStart, hang, swA, swDir, swVel, prevRopeP; float travelT, swLen, swTh0, swTh1;
        LineRenderer cable; Transform hookT; public Pickups pickups; public int scrap;
        const float HOOK_SPEED = 75, HAND = 1.25f, COMBAT_CD = 2.2f;
        public bool RopeTravel => rs >= RopeState.Reel;
        bool RopeHolds => rs != RopeState.None && rs != RopeState.Retract;
        public string RopeStateName => rs.ToString();

        void RopeInit(Transform world)
        {
            pickups = new Pickups(world) { onCollect = it => { scrap += it.amount; vfx.Flash(Sim.FlatY(ch.root.position, 1), .35f); } };
            var prevKill = enemies.onKill;
            enemies.onKill = e => { prevKill?.Invoke(e); pickups.Burst(e.Center(), e.def.bossName != null ? 10 : e.type == "crawler" ? 2 : 4); };
            var cg = new GameObject("rope cable"); cg.transform.SetParent(world, false);
            cable = cg.AddComponent<LineRenderer>(); cable.positionCount = 2; cable.useWorldSpace = true; cable.widthMultiplier = .035f;
            cable.sharedMaterial = Mats.Unlit(new Color(.16f, .14f, .12f, 1), Blend.Opaque); cable.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cable.enabled = false;
            hookT = Spawn(world, "rope hook", ZoneProps.RopeHook(), Mats.Unlit(new Color(1.8f, 1.4f, .8f, 1), Blend.Opaque, true), Vector3.zero, 0, false).transform;
            hookT.gameObject.SetActive(false);
        }

        // ---------- aiming ----------
        public RopeAim Aim(Vector3 cursor)
        {
            var p = ch.root.position; var to = Sim.FlatY(cursor - p);
            Enemy best = null; float bd = float.PositiveInfinity;
            foreach (var e in enemies.list)
            {
                if (!e.alive || e.state == "spawn") continue;
                float dc = Sim.FlatY(e.pos - cursor).magnitude, dp = Sim.FlatY(e.pos - p).magnitude;
                if (dc < e.radius + 1 && dp < RopeAnchors.RANGE && dp > 1.2f && Mathf.Abs(e.pos.y - p.y) < 2.5f && dc < bd) { bd = dc; best = e; }
            }
            if (best != null)
            {
                bool big = best.def.size > 1.2f || best.type != "crawler";
                return new RopeAim { goal = big ? RopeGoal.Zip : RopeGoal.Yank, point = best.Center(), enemy = best, Label = big ? "돌진 베기" : "끌어오기" };
            }
            var item = pickups.Nearest(cursor, 1.4f);
            if (item != null && Sim.FlatY(item.pos - p).magnitude < RopeAnchors.RANGE) return new RopeAim { goal = RopeGoal.Item, point = item.pos, item = item, Label = "낚아채기" };
            RopeAnchor ba = null; bd = 3.5f;
            foreach (var a in RopeAnchors.All)
            {
                float d = new Vector2(a.pos.x - cursor.x, a.pos.z - cursor.z).magnitude;
                if (d < bd && RopeAnchors.Usable(a, p)) { bd = d; ba = a; }
            }
            if (ba != null)
                return ba.kind == RopeAnchor.Kind.Swing
                    ? new RopeAim { goal = RopeGoal.Swing, point = ba.pos, anchor = ba, land = RopeAnchors.SwingLanding(ba, p), Label = "스윙" }
                    : new RopeAim { goal = RopeGoal.Climb, point = ba.pos, anchor = ba, land = ba.land, Label = "당겨 오르기" };
            var dir = new Vector2(to.x, to.z);
            if (RopeAnchors.FindEdge(p, dir, Mathf.Min(RopeAnchors.RANGE, dir.magnitude + 3), out var edge, out var land))
                return new RopeAim { goal = RopeGoal.Edge, point = edge, land = land, Label = "당겨 오르기" };
            var miss = p + Sim.SetLength(to.sqrMagnitude > 1e-4f ? to : ch.Forward, Mathf.Min(RopeAnchors.RANGE, Mathf.Max(3, to.magnitude)));
            return new RopeAim { goal = RopeGoal.Miss, point = miss + new Vector3(0, HAND, 0) };
        }

        /// fire the rope at a cursor point (left click; tests call it directly)
        public bool FireRope(Vector3 cursor)
        {
            if (!Parts.Has(Parts.Rope)) { Cue("로프 팔이 없습니다 · 재의 감시자를 쓰러뜨리면 얻습니다", 1.4f); return false; }
            if (rs != RopeState.None || ropeClock < ropeReady || !player.alive || anim.busy) return false;
            var a = Aim(cursor);
            if ((a.goal == RopeGoal.Yank || a.goal == RopeGoal.Zip) && ropeClock < ropeCombatReady) { Cue("로프 재정비 중", .6f); return false; }
            aim = a; rs = RopeState.Fly; rt = 0; ropeHeld = true; moveTarget = null;
            hook = HandPos();
            var d = Sim.FlatY(a.point - ch.root.position); if (d.sqrMagnitude > 1e-4f) { ch.yaw = Mathf.Atan2(d.x, d.z); ch.ApplyYaw(); }
            return true;
        }
        Vector3 HandPos() => ch.joints.TryGetValue("Lhand", out var h) ? h.WorldPosition : ch.root.position + new Vector3(0, HAND, 0);
        Vector3 HookTarget() => aim.enemy != null ? aim.enemy.Center() : aim.item != null ? aim.item.pos : aim.point;

        // ---------- per substep ----------
        /// hook flight / retract / yank, and the start of travel (called every WorldStep)
        void RopeTick(float dt)
        {
            ropeClock += dt; if (rs == RopeState.None) return; rt += dt;
            switch (rs)
            {
                case RopeState.Fly:
                {
                    var to = HookTarget(); var d = to - hook; float len = d.magnitude;
                    if (aim.enemy != null && !aim.enemy.alive) { rs = RopeState.Retract; break; }
                    if (len > HOOK_SPEED * dt) { hook += d / len * HOOK_SPEED * dt; break; }
                    hook = to; rt = 0; ropeStart = ch.root.position;
                    switch (aim.goal)
                    {
                        case RopeGoal.Miss: rs = RopeState.Retract; break;
                        case RopeGoal.Item: aim.item.reeling = true; rs = RopeState.Retract; break;
                        case RopeGoal.Yank: rs = RopeState.Yank; aim.enemy.ToStagger(1.2f); break;
                        case RopeGoal.Zip: rs = RopeState.Zip; travelT = 0; break;
                        case RopeGoal.Swing: StartSwing(); break;
                        default:
                        {   // climb: reel to a hang point just under the lip, facing it
                            var flat = Sim.FlatY(aim.land - ropeStart); var dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : ch.Forward;
                            hang = new Vector3(aim.goal == RopeGoal.Climb ? aim.land.x - dir.x * 1.6f : aim.point.x - dir.x * .35f, 0,
                                               aim.goal == RopeGoal.Climb ? aim.land.z - dir.z * 1.6f : aim.point.z - dir.z * .35f);
                            hang.y = Mathf.Max(ropeStart.y, aim.land.y - 1.05f);
                            travelT = Mathf.Max(.28f, Sim.FlatY(hang - ropeStart).magnitude / 17 + (hang.y - ropeStart.y) * .05f);
                            rs = RopeState.Reel; break;
                        }
                    }
                    break;
                }
                case RopeState.Retract:
                {
                    var to = HandPos(); var d = to - hook; float len = d.magnitude;
                    if (aim.item != null && aim.item.reeling) hook = aim.item.pos;
                    else if (len > 60 * dt) hook += d / len * 60 * dt;
                    if (len < .3f || rt > 1.2f || (aim.item != null && !pickups.items.Contains(aim.item))) { rs = RopeState.None; ropeReady = ropeClock + .2f; }
                    break;
                }
                case RopeState.Yank:
                {
                    var e = aim.enemy;
                    if (e == null || !e.alive) { rs = RopeState.Retract; break; }
                    var p = ch.root.position; var front = p + ch.Forward * (1.3f + e.radius);
                    var d = Sim.FlatY(front - e.pos); float len = d.magnitude;
                    var next = e.pos + (len > 1e-3f ? d / len * Mathf.Min(len, 18 * dt) : Vector3.zero);
                    var mv = WorldCollision.Move(e.pos, next, e.radius * .8f, WorldCollision.STEP); e.pos.x = mv.x; e.pos.z = mv.z;
                    hook = e.Center();
                    if (len < .25f || rt > .45f)
                    {
                        e.Damage(6, p, false); e.ToStagger(.9f); vfx.Burst(e.Center(), 12, .8f); cam.Shake(.05f);
                        rs = RopeState.Retract; rt = 0; ropeCombatReady = ropeClock + COMBAT_CD;
                    }
                    break;
                }
            }
        }

        void StartSwing()
        {
            var p = ch.root.position; swA = aim.anchor.pos;
            var flat = Sim.FlatY(aim.land - p); swDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : ch.Forward;
            var hand = p + new Vector3(0, HAND, 0);
            float h0 = Vector3.Dot(Sim.FlatY(hand - swA), swDir), v0 = swA.y - hand.y;
            swLen = Mathf.Sqrt(h0 * h0 + v0 * v0); swTh0 = Mathf.Atan2(h0, v0); swTh1 = -swTh0;
            travelT = Mathf.Clamp(.5f + swLen * .045f, .6f, 1f);
            prevRopeP = p; rs = RopeState.Swing;
        }
        /// pendulum timing across the hook, but the body rises in an arc (a real pendulum would dip below the
        /// ground: the hook hangs only ~9 m up). Peak = 35 % of the rope length, at least 1.6 m.
        Vector3 SwingPoint(float s)
        {
            float th = swTh0 + (swTh1 - swTh0) * (.5f - .5f * Mathf.Cos(Mathf.PI * s));
            var p = swA + swDir * (Mathf.Sin(th) * swLen); p.y = ropeStart.y + Mathf.Sin(Mathf.PI * s) * Mathf.Max(1.6f, swLen * .35f);
            return p;
        }

        /// moves the player while travelling on the rope (instead of Vertical)
        void RopeMove(float dt, Vector3 before)
        {
            var p = before; anim.airborne = true; Falling = false; fallV = 0;
            float s = Mathf.Clamp01(rt / Mathf.Max(1e-3f, travelT));
            switch (rs)
            {
                case RopeState.Reel:
                {
                    float e = s * s * (3 - 2 * s);
                    p = Vector3.Lerp(ropeStart, new Vector3(hang.x, ropeStart.y, hang.z), e); p.y = ropeStart.y + (hang.y - ropeStart.y) * Mathf.Pow(s, 1.4f);
                    hook = aim.point;
                    if (s >= 1) { rs = RopeState.Mantle; rt = 0; travelT = .24f; ropeStart = p; }
                    break;
                }
                case RopeState.Mantle:
                {
                    p = Vector3.Lerp(ropeStart, aim.land, s); p.y += Mathf.Sin(Mathf.PI * s) * .35f;
                    if (s >= 1) { RopeLand(aim.land); return; }
                    break;
                }
                case RopeState.Swing:
                {
                    p = SwingPoint(s); swVel = (p - prevRopeP) / Mathf.Max(dt, 1e-4f); prevRopeP = p; hook = swA;
                    if (!ropeHeld && s > .3f && s < .85f) { rs = RopeState.Ballistic; rt = 0; break; }   // let go: fly on
                    if (s >= 1) { rs = RopeState.Ballistic; rt = 0; swVel *= .25f; }
                    break;
                }
                case RopeState.Ballistic:
                {
                    swVel.y -= GRAVITY * dt; var next = p + swVel * dt;
                    var mv = WorldCollision.Move(new Vector3(p.x, p.y, p.z), next, PLAYER_R, WorldCollision.STEP); next.x = mv.x; next.z = mv.z;
                    float floor = WorldCollision.Floor(next.x, next.z, p.y + .3f, .6f);
                    p = next; hook = Vector3.MoveTowards(hook, HandPos(), 40 * dt);
                    if ((p.y <= floor && swVel.y <= 0) || rt > 3) { p.y = floor; RopeLand(p); return; }
                    break;
                }
                case RopeState.Zip:
                {
                    var e = aim.enemy; if (e == null || !e.alive) { RopeLand(new Vector3(p.x, WorldCollision.Floor(p.x, p.z, p.y + .3f, .6f), p.z)); return; }
                    var d = Sim.FlatY(e.pos - p); float len = d.magnitude, stop = e.radius + 1.2f;
                    if (len > stop) { var next = p + d / len * Mathf.Min(len - stop, 20 * dt); next.y = WorldCollision.Floor(next.x, next.z, p.y + .5f, .6f); p = next; }
                    hook = e.Center();
                    if (len <= stop + .05f || rt > 1.2f)
                    {
                        RopeLand(p); ropeCombatReady = ropeClock + COMBAT_CD;
                        ch.yaw = Mathf.Atan2(d.x, d.z); ch.ApplyYaw(); Attack(); return;
                    }
                    break;
                }
            }
            ch.root.position = p; ch.UpdateWorld();
        }
        void RopeLand(Vector3 at)
        {
            at.y = Mathf.Max(at.y, WorldCollision.Floor(at.x, at.z, at.y + .2f, .3f));
            ch.root.position = at; ch.UpdateWorld();
            anim.airborne = false; Falling = false; fallV = 0; anim.ResetFeet();
            vfx.Dust(at + new Vector3(0, .05f, 0), .7f);
            rs = RopeState.Retract; rt = 0;
        }

        /// left arm reaches along the rope while it is out
        void RopeArmPose()
        {
            if (rs == RopeState.None || !ch.joints.ContainsKey("LupperArm")) return;
            var up = ch["LupperArm"]; up.UpdateWorldFromRoot(true); var sh = up.WorldPosition;
            var d = hook - sh; if (d.sqrMagnitude < 1e-4f) return;
            var target = sh + d.normalized * (SoulAnimator.ARM_UPPER + SoulAnimator.ARM_FORE) * .97f;
            var pole = ch.LocalToWorld(new Vector3(.8f, .5f, -.4f));
            SoulAnimator.SolveTwoBone(up, ch["Lforearm"], ch["Lhand"], target, pole, SoulAnimator.ARM_UPPER, SoulAnimator.ARM_FORE);
            ch.UpdateWorld();
        }
        void RopeVisuals()
        {
            bool on = rs != RopeState.None;
            if (cable.enabled != on) cable.enabled = on;
            if (hookT.gameObject.activeSelf != on) hookT.gameObject.SetActive(on);
            if (!on) return;
            var hand = HandPos();
            cable.SetPosition(0, Sim.ToUnity(hand)); cable.SetPosition(1, Sim.ToUnity(hook));
            hookT.localPosition = Sim.ToUnity(hook);
            var d = hook - hand; if (d.sqrMagnitude > 1e-4f) hookT.localRotation = Sim.ToUnity(Sim.FromTo(Vector3.forward, d.normalized));
        }

        void RopeGUI()
        {
            if (!Parts.Has(Parts.Rope) || showMap || rs != RopeState.None || Event.current.type != EventType.Repaint) return;
            var c = cam.Pick(Input.mousePosition, ch.root.position.y); if (!c.HasValue) return;
            var a = Aim(c.Value); if (a.goal == RopeGoal.Miss) return;
            var sp = cam.camera.WorldToScreenPoint(Sim.ToUnity(a.point)); if (sp.z <= 0) return;
            float x = sp.x, y = Screen.height - sp.y, r = 11;
            bool cd = (a.goal == RopeGoal.Yank || a.goal == RopeGoal.Zip) && ropeClock < ropeCombatReady;
            GUI.color = cd ? new Color(.6f, .6f, .6f, .8f) : MeshBuilder.Hex(ZoneGates.PartHex[Parts.Rope]);
            foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            { GUI.DrawTexture(new Rect(x + dx * r - (dx < 0 ? 0 : 6), y + dy * r - (dy < 0 ? 0 : 2), 6, 2), white); GUI.DrawTexture(new Rect(x + dx * r - (dx < 0 ? 0 : 2), y + dy * r - (dy < 0 ? 0 : 6), 2, 6), white); }
            if (debugHud) GUI.Label(new Rect(x + 16, y - 9, 160, 20), "좌클릭 · " + a.Label, small);
            GUI.color = Color.white;
        }
    }
}
