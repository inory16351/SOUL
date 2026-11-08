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
        public void SnapYaw(float y) { yaw = targetYaw = y; snapNext = true; }
        Vector3 target = new Vector3(0, 1, 0), position = new Vector3(0, 11, 8);
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
        public void Update(float dt, Vector3 simPos)
        {
            clock += dt; float aspect = camera.aspect;
            var offset = side ? new Vector3(8.8f, 2.5f, .25f) : new Vector3(0, 10.2f, 7.0f) * zoom;
            if (aspect < 1.2f) offset *= 1.2f;
            yaw += Sim.WrapPi(targetYaw - yaw) * (1 - Mathf.Exp(-dt * 6));
            if (!side) offset = Sim.ApplyAxisAngle(offset, Vector3.up, yaw);
            var tgt = simPos + new Vector3(0, .8f, 0);
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
            if (snapNext) { target = tgt; position = posGoal; snapNext = false; }
            else position = Vector3.LerpUnclamped(position, posGoal, 1 - Mathf.Exp(-dt * 7));
            shakeAmount *= Mathf.Exp(-dt * 17);
            var p = position; p.x += Mathf.Sin(clock * 103) * shakeAmount; p.y += Mathf.Sin(clock * 137) * shakeAmount * .6f;
            camera.transform.position = Sim.ToUnity(p);
            camera.transform.LookAt(Sim.ToUnity(target));
        }
    }
}
