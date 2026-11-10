using UnityEngine;

namespace Soul
{
    // Port of camera.js: fixed-yaw top-down follow (~55 deg pitch), wheel zoom, shake, V = side view.
    // Positions are computed in sim space and converted for the Unity camera.
    public class FollowCam
    {
        public readonly Camera camera;
        public bool side; public float zoom = 1;
        /// view yaw around the player (0 = north-up). T swings it behind the character and it stays there (TurnTo);
        /// in the WASD scheme a sustained forward run also turns it, slowly (Follow below).
        public float yaw, targetYaw; bool snapNext;
        /// turn the view to `y` instantly (no swing), position included
        public void SnapYaw(float y) { yaw = targetYaw = y; turnT = 1; snapNext = true; }
        /// swing the view to `y` smoothly (T key): ease-in-out over 0.35-0.6 s by angle (2026-11-09: was a snap)
        public void TurnTo(float y)
        {
            turnFrom = yaw; turnDelta = Sim.WrapPi(y - yaw); targetYaw = y; turnT = 0;
            turnDur = .4f + .35f * Mathf.Abs(turnDelta) / Mathf.PI;   // 180 deg in 0.75 s
        }
        float turnFrom, turnDelta, turnT = 1, turnDur = .5f;

        // ---------- 조건부 추적 (WASD scheme, user decision 2026-11-10): every number of the auto turn lives here ----------
        // The top-down pitch never changes; only the yaw turns, slowly, behind the direction the player keeps running in.
        /// Esc settings · 화면: on / off, and the top turn speed in deg/s (clamped to FOLLOW_SPEED_MIN..MAX)
        public static bool followOn = true;
        public static float followSpeed = FOLLOW_SPEED_DEFAULT;
        public const float FOLLOW_SPEED_MIN = 20, FOLLOW_SPEED_MAX = 60, FOLLOW_SPEED_DEFAULT = 45;   // deg/s (spec: 40-60 at most)
        public const float FOLLOW_DELAY = 1f;       // s of continuous forward movement before the view starts to turn
        public const float FOLLOW_GRACE = .2f;      // s: a gap this short in the forward input does not restart the delay
        public const float FOLLOW_GAIN = 2.2f;      // 1/s: turn speed per radian of error, so it slows down as it lines up
        public const float FOLLOW_ACCEL = 110;      // deg/s^2: speeding up (0 -> 45 deg/s in ~0.4 s)
        public const float FOLLOW_DECEL = 160;      // deg/s^2: slowing down / stopping (combat, key released). Keep it above GAIN * SPEED_MAX (132) or it overshoots
        public const float FOLLOW_DEADZONE = 3;     // deg: smaller errors do not START a turn (no hunting)
        public const float FOLLOW_SETTLE = .6f;     // deg: a turn already under way runs on until the error is this small
        float followVel, followHold, followGap;     // rad/s, s, s
        /// current auto turn speed (deg/s, signed) and how long the forward run has lasted (tests, debug)
        public float FollowRate => followVel * Mathf.Rad2Deg;
        public float FollowHold => followHold;
        /// Once per frame, before Update. `forward`: the player is moving with a forward share (W held, running);
        /// `allowed`: no threat (out of combat); `behindYaw`: the view yaw that looks along the move direction.
        public void Follow(float dt, bool forward, bool allowed, float behindYaw)
        {
            if (dt <= 0) return;
            if (forward) { followHold += dt; followGap = 0; }
            else { followGap += dt; if (followGap > FOLLOW_GRACE) followHold = 0; }
            if (turnT < 1 || side) { followVel = 0; return; }   // a T swing (or the debug side view) owns the yaw
            float want = 0;
            if (followOn && allowed && forward && followHold >= FOLLOW_DELAY)
            {
                float err = Sim.WrapPi(behindYaw - yaw), max = Mathf.Clamp(followSpeed, FOLLOW_SPEED_MIN, FOLLOW_SPEED_MAX) * Mathf.Deg2Rad;
                float dead = (followVel != 0 ? FOLLOW_SETTLE : FOLLOW_DEADZONE) * Mathf.Deg2Rad;
                if (Mathf.Abs(err) > dead) want = Mathf.Clamp(err * FOLLOW_GAIN, -max, max);
            }
            bool speedingUp = Mathf.Abs(want) > Mathf.Abs(followVel) && want * followVel >= 0;
            followVel = Mathf.MoveTowards(followVel, want, (speedingUp ? FOLLOW_ACCEL : FOLLOW_DECEL) * Mathf.Deg2Rad * dt);
            if (followVel != 0) { yaw = Sim.WrapPi(yaw + followVel * dt); targetYaw = yaw; }
        }
        Vector3 target = new Vector3(0, 1, 0), position = new Vector3(0, 11, 8), lastGoal = new Vector3(0, 11, 8);
        float shakeAmount, clock;
        public FollowCam(Camera cam) { camera = cam; camera.fieldOfView = 38; camera.nearClipPlane = .1f; camera.farClipPlane = 140; }
        public bool Toggle() { side = !side; return side; }
        public void ZoomBy(float delta) { zoom = Mathf.Clamp(zoom * (1 + delta), .55f, 2.2f); }
        /// screen shake strength (Esc settings · 화면): 0 = off, 1 = default
        public static float shakeScale = 1;
        public void Shake(float power) { shakeAmount = Mathf.Max(shakeAmount, power * shakeScale); }
        /// ground point (sim space) under a screen position, or null
        public Vector3? Pick(Vector2 screen, float planeY = 0)
        {
            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return null;
            float t = (planeY - ray.origin.y) / ray.direction.y; if (t < 0) return null;
            return Sim.FromUnity(ray.origin + ray.direction * t);
        }
        /// 전망 지점 (vista): 0..1 blend toward a low, pulled-back view looking along `vistaYaw` at `vistaLook`
        /// (sim). SoulGame.Vista drives these; at 0 the camera is the normal top-down follow.
        public float vista, vistaYaw; public Vector3 vistaLook;
        /// boss fight framing: set fightTarget 1 (0 to release) and fightFocus = boss position (sim) every frame;
        /// the camera eases out by fightZoom and centres between player and boss
        public float fightTarget, fightZoom = .75f; public Vector3 fightFocus; float fight;
        /// explore framing (경로 유도 A, SoulGame.Guide): 0..1, pulls out 30 % and shifts the view centre by exploreLead (sim)
        public float explore; public Vector3 exploreLead; Vector3 leadNow;
        /// how fast the view centre catches up: lead (explore shift) and target, 1/s. WASD sets them higher (SoulGame.Guide):
        /// direct control changes direction often, a slow lead reads as the camera dragging behind (user 2026-11-10)
        public float leadRate = 1.6f, targetRate = 8, posRate = 7;
        public void Update(float dt, Vector3 simPos)
        {
            clock += dt; float aspect = camera.aspect;
            var offset = side ? new Vector3(8.8f, 2.5f, .25f) : new Vector3(0, 10.2f, 7.0f) * zoom;
            if (aspect < 1.2f) offset *= 1.2f;
            if (turnT < 1)
            {
                turnT = Mathf.Min(1, turnT + dt / turnDur); float e = turnT * turnT * turnT * (turnT * (turnT * 6 - 15) + 10);   // smootherstep
                yaw = Sim.WrapPi(turnFrom + turnDelta * e);
            }
            else yaw += Sim.WrapPi(targetYaw - yaw) * (1 - Mathf.Exp(-dt * 6));
            if (!side) offset = Sim.ApplyAxisAngle(offset, Vector3.up, yaw);
            var tgt = simPos + new Vector3(0, .8f, 0);
            if (!side)
            {
                float ek = explore * explore * (3 - 2 * explore);
                leadNow = Vector3.Lerp(leadNow, exploreLead * ek, 1 - Mathf.Exp(-dt * leadRate));
                offset *= 1 + .3f * ek; tgt += leadNow;
            }
            // boss framing: pull out and centre between player and boss so the whole beast stays in frame
            fight = Mathf.MoveTowards(fight, fightTarget, dt * .8f);
            if (fight > 0 && !side)
            {
                float k = fight * fight * (3 - 2 * fight);
                offset *= 1 + fightZoom * k;
                tgt = Vector3.Lerp(tgt, (tgt + fightFocus) * .5f, .55f * k);
            }
            var posGoal = tgt + offset;
            if (vista > 0 && !side)
            {
                // 6.5 m up, 8 m behind the player, looking at a point far ahead: about 7 deg below the horizon,
                // so the horizon (and the far landmark silhouettes) sits in the upper third of the frame
                float k = vista * vista * (3 - 2 * vista);
                posGoal = Vector3.Lerp(posGoal, simPos + Sim.ApplyAxisAngle(new Vector3(0, 6.5f, -8f), Vector3.up, vistaYaw), k);
                tgt = Vector3.Lerp(tgt, vistaLook, k);
                camera.farClipPlane = Mathf.Lerp(140, 1100, k);
            }
            else camera.farClipPlane = 140;
            target = Vector3.LerpUnclamped(target, tgt, 1 - Mathf.Exp(-dt * targetRate));
            // follow lag is kept relative to the goal, so a T swing rides the orbit instead of cutting the chord
            if (snapNext) { target = tgt; position = posGoal; snapNext = false; }
            else position = posGoal + (position - lastGoal) * Mathf.Exp(-dt * posRate);
            lastGoal = posGoal;
            shakeAmount *= Mathf.Exp(-dt * 17);
            var p = position; p.x += Mathf.Sin(clock * 103) * shakeAmount; p.y += Mathf.Sin(clock * 137) * shakeAmount * .6f;
            camera.transform.position = Sim.ToUnity(p);
            camera.transform.LookAt(Sim.ToUnity(target));
        }
    }
}
