using UnityEngine;

namespace Soul
{
    // Port of camera.js: fixed-yaw top-down follow (~55 deg pitch), wheel zoom, shake, V = side view.
    // Positions are computed in sim space and converted for the Unity camera.
    public class FollowCam
    {
        public readonly Camera camera;
        public bool side; public float zoom = 1;
        Vector3 target = new Vector3(0, 1, 0), position = new Vector3(0, 11, 8);
        float shakeAmount, clock;
        public FollowCam(Camera cam) { camera = cam; camera.fieldOfView = 38; camera.nearClipPlane = .1f; camera.farClipPlane = 140; }
        public bool Toggle() { side = !side; return side; }
        public void ZoomBy(float delta) { zoom = Mathf.Clamp(zoom * (1 + delta), .55f, 2.2f); }
        public void Shake(float power) { shakeAmount = Mathf.Max(shakeAmount, power); }
        /// ground point (sim space) under a screen position, or null
        public Vector3? Pick(Vector2 screen, float planeY = 0)
        {
            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return null;
            float t = (planeY - ray.origin.y) / ray.direction.y; if (t < 0) return null;
            return Sim.FromUnity(ray.origin + ray.direction * t);
        }
        public void Update(float dt, Vector3 simPos)
        {
            clock += dt; float aspect = camera.aspect;
            var offset = side ? new Vector3(8.8f, 2.5f, .25f) : new Vector3(0, 10.2f, 7.0f) * zoom;
            if (aspect < 1.2f) offset *= 1.2f;
            var tgt = simPos + new Vector3(0, .8f, 0); target = Vector3.LerpUnclamped(target, tgt, 1 - Mathf.Exp(-dt * 8));
            position = Vector3.LerpUnclamped(position, target + offset, 1 - Mathf.Exp(-dt * 7));
            shakeAmount *= Mathf.Exp(-dt * 17);
            var p = position; p.x += Mathf.Sin(clock * 103) * shakeAmount; p.y += Mathf.Sin(clock * 137) * shakeAmount * .6f;
            camera.transform.position = Sim.ToUnity(p);
            camera.transform.LookAt(Sim.ToUnity(target));
        }
    }
}
