using UnityEngine;

namespace Soul
{
    // Port of camera.js: fixed-yaw top-down follow (~55 deg pitch), wheel zoom, shake, V = side view.
    // Positions are computed in sim space and converted for the Unity camera.
    public class FollowCam
    {
        public readonly Camera camera;
        public bool side; public float zoom = 1;
        /// view yaw around the player (0 = north-up). T snaps it behind the character and it stays there (SnapYaw).
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
                leadNow = Vector3.Lerp(leadNow, exploreLead * ek, 1 - Mathf.Exp(-dt * 1.6f));
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
            target = Vector3.LerpUnclamped(target, tgt, 1 - Mathf.Exp(-dt * 8));
            // follow lag is kept relative to the goal, so a T swing rides the orbit instead of cutting the chord
            if (snapNext) { target = tgt; position = posGoal; snapNext = false; }
            else position = posGoal + (position - lastGoal) * Mathf.Exp(-dt * 7);
            lastGoal = posGoal;
            shakeAmount *= Mathf.Exp(-dt * 17);
            var p = position; p.x += Mathf.Sin(clock * 103) * shakeAmount; p.y += Mathf.Sin(clock * 137) * shakeAmount * .6f;
            camera.transform.position = Sim.ToUnity(p);
            camera.transform.LookAt(Sim.ToUnity(target));
        }
    }
}
