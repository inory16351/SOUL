using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 전망 지점 (World/Vistas.cs): step onto a lookout outside combat and the camera pulls back and low behind the
    // player, sweeps slowly across the landmarks, the fog lifts, and far landmarks show as hazy silhouettes
    // (they are outside streaming range, so they are drawn here from their cached meshes). Landmarks in view are
    // revealed on the map. First visit 6.5 s, later visits 3.5 s; any click / key cancels.
    public partial class SoulGame
    {
        bool vistaInit; Vista vistaActive, vistaArmedBlock; float vistaT, vistaLen, vistaOut = -1, vistaPeak;
        Vector3 skyScale = Vector3.one;   // the dust-sky dome is sized for the top-down view; a vista would see its rim
        Material vistaSil; MaterialPropertyBlock vistaMpb; Transform vistaRoot;
        readonly Dictionary<string, Mesh> vistaMeshes = new Dictionary<string, Mesh>();

        void VistaInit()
        {
            vistaInit = true; Vistas.Reset(); Vistas.Build();
            if (sky) skyScale = sky.localScale;
            vistaSil = Mats.Unlit(Color.white, Blend.Opaque, false, UnityEngine.Rendering.CullMode.Off, true, false);
            vistaMpb = new MaterialPropertyBlock();
            vistaRoot = new GameObject("vista markers").transform; vistaRoot.SetParent(transform, false);
            var marker = Vistas.Marker(); var mat = Mats.Lit(Color.white, Color.black, .12f, .12f, true);
            foreach (var v in Vistas.All)
            {
                if (v.shrine) continue;   // the shrine is its own landmark
                var g = new GameObject("vista"); g.transform.SetParent(vistaRoot, false);
                var p = v.pos + new Vector3(0, Terrain.Height(v.pos.x, v.pos.z), 0);
                g.transform.localPosition = Sim.ToUnity(p); g.transform.localRotation = Sim.YawToUnity(v.lookYaw);
                g.AddComponent<MeshFilter>().sharedMesh = marker.mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        bool VistaCalm()
        {
            if (fightArena != null || !player.alive) return false;
            var p = ch.root.position;
            foreach (var e in enemies.list) if (e.alive && Sim.FlatY(e.pos - p).magnitude < 22) return false;
            return true;
        }

        /// MCP tests: start the vista at `index` from where the player stands
        public void VistaStart(int index) { if (!vistaInit) VistaInit(); StartVista(Vistas.All[index]); }
        /// MCP tests: one frame of vista + atmosphere + camera (the editor barely ticks while unfocused)
        public void VistaTick(float dt) { VistaUpdate(dt); atmo.Update(dt, ch.root.position); cam.Update(dt, ch.root.position); }

        void StartVista(Vista v)
        {
            vistaActive = v; vistaT = 0; vistaOut = -1; vistaLen = v.visited ? 3.5f : 6.5f; v.visited = true;
        }

        void VistaUpdate(float dt)
        {
            if (!vistaInit) VistaInit();
            var p = ch.root.position;
            if (vistaActive == null)
            {
                // trigger: on the lookout, calm, and not the one we just finished standing on
                if (vistaArmedBlock != null && Sim.FlatY(vistaArmedBlock.pos - p).magnitude > 10) vistaArmedBlock = null;
                var v = Vistas.Near(p, Vistas.TRIGGER);
                if (v != null && v != vistaArmedBlock && VistaCalm() && !anim.busy) StartVista(v);
                cam.vista = Mathf.MoveTowards(cam.vista, 0, dt * 1.5f); atmo.vista = cam.vista * .8f;
                DrawSilhouettes(cam.vista);
                return;
            }
            var a = vistaActive; vistaT += dt;
            bool cancel = vistaOut < 0 && vistaT > .4f && (Input.anyKeyDown || !VistaCalm());
            if (cancel || (vistaOut < 0 && vistaT > vistaLen)) { vistaOut = 0; vistaPeak = cam.vista; }
            float k;
            if (vistaOut >= 0)
            {
                vistaOut += dt; k = vistaPeak * Mathf.Clamp01(1 - vistaOut / 1.0f);
                if (k <= 0) { vistaActive = null; vistaArmedBlock = a; cam.vista = 0; atmo.vista = 0; return; }
            }
            else k = Mathf.Clamp01(vistaT / 1.3f);
            // slow pan across the landmark side
            float u = Mathf.Clamp01(vistaT / Mathf.Max(1, vistaLen));
            float yaw = a.lookYaw + Mathf.Lerp(-.38f, .38f, u * u * (3 - 2 * u));
            var basePos = new Vector3(p.x, p.y, p.z);
            cam.vista = k; cam.vistaYaw = yaw;
            cam.vistaLook = basePos + Sim.Forward(yaw) * 45 + new Vector3(0, .5f, 0);
            atmo.vista = k * .8f;
            DrawSilhouettes(k);
            // map: landmarks inside the view cone are now known
            if (k > .9f)
            {
                var f = Sim.Forward(yaw);
                foreach (var lm in Regions.Landmarks)
                {
                    var d = Sim.FlatY(lm.pos - p); float dist = d.magnitude;
                    if (dist < Vistas.RANGE && dist > 1 && Vector3.Dot(d / dist, f) > Mathf.Cos(.62f)) MapFog.Reveal(lm.pos, 40);
                }
            }
        }

        /// far landmarks as flat hazy silhouettes (unlit, no fog: tinted toward the fog colour by distance).
        /// One pooled renderer per landmark, created on first use and only enabled while a vista shows.
        readonly Dictionary<Landmark, MeshRenderer> vistaSils = new Dictionary<Landmark, MeshRenderer>();
        void DrawSilhouettes(float k)
        {
            if (sky) sky.localScale = skyScale * (1 + 14 * k);
            var p = ch.root.position; var fogC = RenderSettings.fogColor.linear;
            foreach (var lm in Regions.Landmarks)
            {
                float dist = Sim.FlatY(lm.pos - p).magnitude;
                bool show = k > .02f && dist >= 110 && dist <= Vistas.RANGE;   // near ones are streamed in for real
                vistaSils.TryGetValue(lm, out var r);
                if (!show) { if (r) r.enabled = false; continue; }
                if (!r)
                {
                    if (!vistaMeshes.TryGetValue(lm.id, out var mesh)) { mesh = lm.build(0).mesh; vistaMeshes[lm.id] = mesh; }
                    var g = new GameObject("silhouette " + lm.id); g.transform.SetParent(vistaRoot, false);
                    var pos = lm.pos + new Vector3(0, Terrain.Height(lm.pos.x, lm.pos.z), 0);
                    g.transform.localPosition = Sim.ToUnity(pos); g.transform.localRotation = Sim.YawToUnity(lm.yaw);
                    g.AddComponent<MeshFilter>().sharedMesh = mesh; r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = vistaSil;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                    vistaSils[lm] = r;
                }
                float haze = Mathf.Lerp(.2f, .62f, Mathf.InverseLerp(110, Vistas.RANGE, dist));
                var c = Color.Lerp(fogC * .3f, fogC, haze); c = Color.Lerp(fogC, c, k);   // fades in with the view
                vistaMpb.SetVector("_BaseColor", new Vector4(c.r, c.g, c.b, 1)); r.SetPropertyBlock(vistaMpb);
                r.enabled = true;
            }
        }
    }
}
