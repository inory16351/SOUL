using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Right-click move (2026-11-09, user: "클릭하면 가는 위치 좀 더 직관적으로 — 덮여 있는 오브젝트 위에 표기되고 실제
    //움직이는 위치로 표시되게"):
    //  - PickFloor: march the mouse ray against the walkable floor (terrain + surfaces at the player's level). Roofs
    //    above the player and anything the top-down cutaway has dithered away are looked through, so the target is
    //    the ground you actually walk to, not the slab in front of it.
    //  - the click ring (Vfx.MoveRing) sits on that floor; where an object covers it, it is drawn over the object.
    //
    // WASD move (2026-11-10, user decision: 탑뷰는 유지, 조작만 WASD): the held keys give a camera-relative direction
    // every frame (MoveDir / FrameInput); the click scheme keeps the move order above. In WASD the view eases behind a
    // sustained forward run (CamFollowUpdate -> FollowCam.Follow; the numbers live in FollowCam).
    public partial class SoulGame
    {
        /// WASD: camera-relative move direction in sim space (unit length, or zero when no key is held / click scheme).
        /// `axis` is the raw input: x = right (D - A), y = forward (W - S). Screen-up is -Z and screen-right is +X at view yaw 0.
        /// The view yaw is sampled when the held keys CHANGE and kept while they stay the same (moveBasisYaw): the heading
        /// stays straight in the world while the view turns under it (T swing, auto follow). Without this, W + D with the
        /// follow camera runs in a circle forever (the direction would turn with the view that is chasing it).
        Vector3 MoveDir(out Vector2 axis)
        {
            axis = KeyBinds.MoveAxis();
            if (axis != moveAxisHeld) { moveAxisHeld = axis; moveBasisYaw = cam.yaw; }
            if (axis == Vector2.zero) return Vector3.zero;
            var fwd = Sim.ApplyAxisAngle(new Vector3(0, 0, -1), Vector3.up, moveBasisYaw); var right = Sim.ApplyAxisAngle(new Vector3(1, 0, 0), Vector3.up, moveBasisYaw);
            return (fwd * axis.y + right * axis.x).normalized;
        }
        Vector2 moveAxisHeld; float moveBasisYaw;
        /// WASD: how much harder the run brakes once the keys are released (SoulAnimator.stopSharpness). 1 = the click
        /// scheme's coast (~0.8 m from a full run); 1.8 stops in ~0.45 m so ledges and jump take-offs can be placed.
        const float WASD_STOP_SHARPNESS = 1.8f;

        /// this frame's locomotion input for WorldStep: the held WASD direction, or the walk toward the click order
        Vector3 FrameInput()
        {
            var input = Vector3.zero;
            anim.stopSharpness = KeyBinds.Wasd ? WASD_STOP_SHARPNESS : 1;
            if (KeyBinds.Wasd)
            {   // no move orders in this scheme (a leftover one from before a scheme switch is dropped)
                moveTarget = null; resumeMove = null;
                if (!anim.busy && player.alive) input = MoveDir(out _);
                return input;
            }
            if (resumeMove.HasValue && !anim.busy) { if (!moveTarget.HasValue) moveTarget = resumeMove; resumeMove = null; }
            if (moveTarget.HasValue && !anim.busy)
            {
                var d = moveTarget.Value - ch.root.position; d.y = 0; float dist = d.magnitude;
                if (dist < .12f) moveTarget = null; else input = d.normalized * Mathf.Min(1, dist / .45f + .25f);
            }
            return input;
        }

        /// 조건부 추적 카메라 (WASD only): while W is held (with or without A / D) and the character is running, jumping or
        /// dashing, outside combat, the view turns behind the MOVE direction (not the facing: attacks aim at the cursor).
        /// Strafing (A / D alone) and backing up (S) never turn it: movement is camera-relative, so following there
        /// would make the character circle or flip the view by 180 degrees.
        void CamFollowUpdate(float dt)
        {
            var dir = MoveDir(out var axis);
            bool travelling = !anim.busy || anim.name == "jump" || anim.name == "dash";
            bool forward = axis.y > 0 && travelling && player.alive && !RopeHolds;
            bool calm = VistaCalm() && cam.vista <= .01f && cam.fightTarget <= 0;   // no threat, no boss / elite framing, no vista
            cam.Follow(dt, forward, calm, forward ? Sim.WrapPi(Mathf.Atan2(dir.x, dir.z) + Mathf.PI) : cam.yaw);
        }

        /// MCP tests: forget the dash / jump cooldowns (realTime does not advance inside one execute_code call)
        public void ClearCooldownsForTest() { ready.Clear(); foreach (var o in Skills.owned) o.ready = 0; }
        /// MCP tests (the editor barely ticks while unfocused): hold `keys` for `seconds` at 60 fps, running the same
        /// input -> world substeps -> follow camera path as Update, and return "t yaw rate | pos" every `every` s plus
        /// the peak turn rate. Angles in degrees.
        public string ControlTest(float seconds, float every, params KeyCode[] keys)
        {
            var sb = new System.Text.StringBuilder(); KeyBinds.fakeHeld.Clear(); KeyBinds.fakeHeld.UnionWith(keys);
            const float dt = 1 / 60f; int n = Mathf.RoundToInt(seconds * 60), k = Mathf.Max(1, Mathf.RoundToInt(every * 60));
            float peak = 0, yaw0 = cam.yaw, total = 0; var p0 = ch.root.position; fastTest = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    float before = cam.yaw; var input = FrameInput();
                    WorldStep(1 / 120f, input); WorldStep(1 / 120f, input);
                    CamFollowUpdate(dt); cam.Update(dt, ch.root.position);
                    float rate = Sim.WrapPi(cam.yaw - before) / dt * Mathf.Rad2Deg; peak = Mathf.Max(peak, Mathf.Abs(rate)); total += rate * dt;
                    if ((i + 1) % k == 0) sb.Append($"{(i + 1) * dt:F2}s yaw {cam.yaw * Mathf.Rad2Deg:F1} rate {rate:F1} | {ch.root.position.x:F1},{ch.root.position.z:F1}\n");
                }
            }
            finally { KeyBinds.fakeHeld.Clear(); fastTest = false; secondary.Reset(); ch.Sync(); }
            var moved = Sim.FlatY(ch.root.position - p0);
            sb.Append($"TOTAL turn {total:F1} deg (yaw {yaw0 * Mathf.Rad2Deg:F1} -> {cam.yaw * Mathf.Rad2Deg:F1}), peak rate {peak:F1} deg/s, moved {moved.magnitude:F2} m ({moved.x:F2},{moved.z:F2})");
            return sb.ToString();
        }

        /// floor point under the screen position (sim), or null when the ray finds none within 260 m
        Vector3? PickFloor(Vector2 screen)
        {
            var ray = cam.camera.ScreenPointToRay(screen);
            Vector3 o = Sim.FromUnity(ray.origin), d = Sim.FromUnity(ray.origin + ray.direction) - o;
            float foot = ch.root.position.y;
            var cutC = ch.root.position + Sim.ApplyAxisAngle(new Vector3(0, 0, 3), Vector3.up, cam.yaw);
            // the floor the ray can land on at q: surfaces up to 3 m above the feet (roofs above are looked through);
            // inside the cut circle a terrain shelf is drawn pressed down to CUT_ABOVE (SoulLit squash), so the ray
            // meets it there, where the player sees it
            float FloorAt(Vector3 q, out bool seeThrough)
            {
                seeThrough = false;
                float h = WorldCollision.Height(q.x, q.z, foot + 3);
                if (h > foot + CUT_ABOVE && Sim.FlatY(q - cutC).magnitude < CUT_RADIUS - 1) h = foot + CUT_ABOVE;
                return h;
            }
            float step = .3f, prev = 0;
            for (float t = 1; t < 260; t += step)
            {
                var q = o + d * t; float h = FloorAt(q, out bool see);
                if (!see && q.y <= h)
                {   // refine between the last point above the floor and this one
                    float a = prev, b = t;
                    for (int i = 0; i < 12; i++) { float m = (a + b) * .5f; var qm = o + d * m; if (qm.y <= FloorAt(qm, out bool s2) && !s2) b = m; else a = m; }
                    var hit = o + d * b; hit.y = WorldCollision.Height(hit.x, hit.z, foot + 3);
                    return hit;
                }
                prev = t; step = Mathf.Min(1.2f, .3f + t * .01f);
            }
            return null;
        }

        // the click shows the old expanding ring on the real floor; Vfx.MoveRing also draws it over whatever covers it
        // (user 2026-11-09: no pointer graphic, "옛날처럼, 오브젝트 위에만 겹쳐서")
        void MoveMarkerClick(Vector3 p) => vfx.MoveRing(p);
        void MoveMarkerUpdate(float dt) { }

        /// MCP tests: floor pick under a screen point (sim) and the marker state
        public string PickTest(Vector2 screen)
        {
            var f = PickFloor(screen); var plane = cam.Pick(screen, ch.root.position.y);
            return $"floor {(f.HasValue ? f.Value.ToString("F2") : "none")} | old plane pick {(plane.HasValue ? plane.Value.ToString("F2") : "none")} | player {ch.root.position:F2}";
        }
    }
}
