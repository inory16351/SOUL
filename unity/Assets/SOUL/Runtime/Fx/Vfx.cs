using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of vfx.js: neon-blue effect kit. Colours are linear HDR (>1) so the URP bloom makes
    // them glow: blade trail, laser waves, dash afterimages, sparks, flashes, dust, rings.
    // All positions are sim space; objects are converted with Sim.ToUnity when rendered.
    // Particles share a few materials and get colour/opacity through MaterialPropertyBlocks.
    public class Wave
    {
        public Vector3 pos, dir;
        public HashSet<object> hitSet = new HashSet<object>();
        public float life, max = .62f, speed = 15, scale = 1;
        public Quaternion rot;                 // sim
        internal GameObject go; internal MeshRenderer[] renderers; internal Color[] colors; internal Light light;
        public bool hit;
    }

    public class Vfx
    {
        const float TRAIL_LIFE = .16f, TRAIL_GAIN = 1.25f; const int SUB = 4;
        static readonly Color TRAIL_NEW = new Color(.75f, .95f, 1.0f), TRAIL_OLD = new Color(.10f, .38f, 1.0f);
        static readonly Color NEON = new Color(.25f, .65f, 1.0f);

        class Particle { public GameObject go; public Transform t; public MeshRenderer r; public Vector3 pos, vel; public Color color; public float life, max, width, size; public bool spark; }
        class FlashFx { public GameObject go; public Transform t; public MeshRenderer r; public Vector3 pos; public float life, max, size; }
        class RingFx { public GameObject go; public Transform t; public MeshRenderer r; public float life, power; public bool upright; }
        class Ghost { public GameObject go; public Material mat; public float life, max, start; }
        struct Sample { public Vector3 b, tip; public float time; }

        public readonly List<Wave> waves = new List<Wave>();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<FlashFx> flashes = new List<FlashFx>();
        readonly List<RingFx> rings = new List<RingFx>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        List<Sample> samples = new List<Sample>();

        readonly Transform root; readonly CharacterRig ch;
        readonly Material sparkMat, flashMat, dustMat, ringMat, waveMat;
        readonly Mesh box, quad, ringQuad;
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly Texture2D glow, puff, ringTex;
        // trail
        readonly int maxSamples = 44;
        readonly Mesh trailMesh; readonly Vector3[] tv; readonly Color[] tc; readonly int[] tIndex;
        float trailClock, time, sparkDebt; bool hasLast; Vector3 lastBase, lastTip;
        // rune emission
        readonly Material runeMat; readonly Color runeEmissive;

        public Vfx(Transform fxRoot, CharacterRig ch, GameObject trailObject)
        {
            root = fxRoot; this.ch = ch;
            glow = Mats.Radial(64, (0, new Color(235 / 255f, 248 / 255f, 1, 1)), (.25f, new Color(90 / 255f, 180 / 255f, 1, .8f)), (1, new Color(30 / 255f, 110 / 255f, 1, 0)));
            puff = Mats.Radial(64, (0, new Color(150 / 255f, 135 / 255f, 110 / 255f, .5f)), (.6f, new Color(130 / 255f, 118 / 255f, 96 / 255f, .2f)), (1, new Color(120 / 255f, 110 / 255f, 90 / 255f, 0)));
            ringTex = Mats.Radial(128, 40 / 64f, (0, new Color(80 / 255f, 170 / 255f, 1, 0)), (.75f, new Color(170 / 255f, 225 / 255f, 1, .95f)), (1, new Color(80 / 255f, 170 / 255f, 1, 0)));
            sparkMat = Mats.Unlit(Color.white, Blend.Add, false, CullMode.Back);
            flashMat = Mats.Unlit(Color.white, Blend.Add, false, CullMode.Off, null, true, glow);
            dustMat = Mats.Unlit(Color.white, Blend.Alpha, false, CullMode.Off, false, true, puff);
            ringMat = Mats.Unlit(Color.white, Blend.Add, false, CullMode.Off, null, true, ringTex);
            waveMat = Mats.Unlit(Color.white, Blend.Add, false, CullMode.Off);
            box = FxMeshes.Box(); quad = FxMeshes.Quad(); ringQuad = FxMeshes.SimPlane(1, 1);

            // trail mesh (sim-space vertices converted to Unity; double sided)
            int maxVerts = maxSamples * SUB * 3;
            tv = new Vector3[maxVerts]; tc = new Color[maxVerts];
            var idx = new List<int>();
            for (int i = 0; i < maxSamples * SUB - 1; i++) { int a = i * 3, b = a + 3; idx.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b, a + 1, a + 2, b + 1, a + 2, b + 2, b + 1 }); }
            tIndex = idx.ToArray();
            trailMesh = new Mesh { name = "blade-trail" }; trailMesh.MarkDynamic();
            trailMesh.vertices = tv; trailMesh.colors = tc; trailMesh.SetIndices(tIndex, 0, 0, MeshTopology.Triangles, 0, false);
            trailMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000);
            var tgo = trailObject;
            tgo.GetComponent<MeshFilter>().sharedMesh = trailMesh;
            var tr = tgo.GetComponent<MeshRenderer>(); tr.sharedMaterial = Mats.Unlit(Color.white, Blend.Add, true, CullMode.Off); tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false;

            var rune = ch?.model ? ch.model.Part("rune") : null;
            var rr = rune ? rune.GetComponent<MeshRenderer>() : null;
            if (rr) { runeMat = rr.material; runeEmissive = ((Color)new Color32(0x69, 0x40, 0x19, 255)).linear; }
        }

        GameObject Obj(string name, Mesh mesh, Material mat, out MeshRenderer r)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }
        void Tint(Renderer r, Color c, float opacity) { c.a = opacity; mpb.SetVector(Mats.BaseColor, c); r.SetPropertyBlock(mpb); }
        static float R => Random.value;
        static Quaternion Billboard => Camera.main ? Camera.main.transform.rotation : Quaternion.identity;

        // ---------- one-shot effects ----------
        public void Burst(Vector3 pos, int count = 22, float power = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var c = i % 3 != 0 ? NEON * 3 : new Color(3, 3.2f, 3.4f);
                var dir = new Vector3(R * 2 - 1, R * .9f + .2f, R * 2 - 1).normalized;
                AddSpark(pos, dir * ((2.5f + R * 5.5f) * power), c, .16f + R * .24f, .008f + R * .01f);
            }
            if (count > 3) { Flash(pos, .5f * power); Ring(pos, .35f * power, true); }
        }
        public void EdgeSpark(Vector3 pos, Vector3 bladeVel)
        {
            bool hot = R < .3f;
            var c = hot ? new Color(3.2f, 2.2f, 1.1f) : R < .5f ? new Color(2.6f, 3.0f, 3.4f) : NEON * 3.2f;
            var v = bladeVel * (.25f + R * .25f) + new Vector3(R - .5f, R * .8f, R - .5f) * 3;
            AddSpark(pos, v, c, .12f + R * .2f, .006f + R * .006f);
        }
        void AddSpark(Vector3 pos, Vector3 vel, Color c, float max, float width)
        {
            var go = Obj("spark", box, sparkMat, out var r);
            var p = new Particle { go = go, t = go.transform, r = r, pos = pos, vel = vel, color = c, max = max, width = width, spark = true };
            go.transform.position = Sim.ToUnity(pos); Tint(r, c, 1);
            particles.Add(p);
        }
        public void Flash(Vector3 pos, float size = .5f)
        {
            var go = Obj("flash", quad, flashMat, out var r);
            go.transform.position = Sim.ToUnity(pos); go.transform.localScale = Vector3.one * size * .4f; go.transform.rotation = Billboard;
            Tint(r, new Color(2, 2, 2), 1);
            flashes.Add(new FlashFx { go = go, t = go.transform, r = r, pos = pos, max = .14f, size = size });
        }
        public void Dust(Vector3 pos, float power = .65f)
        {
            int n = Mathf.RoundToInt(5 + power * 5);
            for (int i = 0; i < n; i++)
            {
                float a = R * Mathf.PI * 2, rr = R * .12f;
                var p = pos + new Vector3(Mathf.Cos(a) * rr, .06f + R * .05f, Mathf.Sin(a) * rr);
                var go = Obj("dust", quad, dustMat, out var r);
                go.transform.position = Sim.ToUnity(p); go.transform.rotation = Billboard;
                float sp = (.35f + R * .8f) * power;
                var part = new Particle { go = go, t = go.transform, r = r, pos = p, vel = new Vector3(Mathf.Cos(a) * sp, .15f + R * .35f * power, Mathf.Sin(a) * sp), color = Color.white, max = .55f + R * .45f, size = .16f + R * .14f * power };
                go.transform.localScale = Vector3.one * part.size; Tint(r, Color.white, 1);
                particles.Add(part);
            }
        }
        public void Ring(Vector3 pos, float power = 1, bool upright = false)
        {
            var go = Obj("ring", ringQuad, ringMat, out var r);
            Quaternion q;
            if (!upright) { pos.y = .03f; q = Sim.EulerXYZ(-Mathf.PI / 2, 0, 0); }
            else q = Sim.AxisAngle(Vector3.up, ch != null ? ch.yaw : 0);
            go.transform.SetPositionAndRotation(Sim.ToUnity(pos), Sim.ToUnity(q));
            go.transform.localScale = Vector3.one * .3f;
            Tint(r, new Color(1.8f, 2.2f, 2.6f), 1);
            rings.Add(new RingFx { go = go, t = go.transform, r = r, power = power, upright = upright });
        }
        // Travelling laser crescent (W). The returned object is used by the game loop for hit tests.
        public Wave SpawnWave(Vector3 origin, Vector3 dir, float roll = -.35f)
        {
            var w = new Wave { pos = origin, dir = Sim.Norm(Sim.FlatY(dir)) };
            // three: group.lookAt(origin + dir) (+Z toward target), then rotateZ(roll)
            var z = Sim.Norm(dir); var x = Vector3.Cross(Vector3.up, z);
            if (x.sqrMagnitude < 1e-8f) x = Vector3.right; x.Normalize();
            var y = Vector3.Cross(z, x);
            w.rot = Sim.FromBasis(x, y, z) * Sim.AxisAngle(Vector3.forward, roll);
            w.go = new GameObject("laser-wave"); w.go.transform.SetParent(root, false);
            w.colors = new[] { NEON * 2.2f, new Color(3, 3.4f, 3.8f) };
            var meshes = new[] { FxMeshes.RingSector(.95f, 1.25f, 40, Mathf.PI * .12f, Mathf.PI * .76f), FxMeshes.RingSector(1.08f, 1.16f, 40, Mathf.PI * .12f, Mathf.PI * .76f) };
            w.renderers = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var part = Obj("crescent", meshes[i], waveMat, out var r); part.transform.SetParent(w.go.transform, false);
                w.renderers[i] = r; Tint(r, w.colors[i], 1);
            }
            var lgo = new GameObject("wave-light"); lgo.transform.SetParent(w.go.transform, false);
            w.light = lgo.AddComponent<Light>(); w.light.type = LightType.Point; w.light.color = new Color32(0x4a, 0xa8, 0xff, 255);
            w.light.range = 4; w.light.intensity = 3 * SoulLights.PointScale; w.light.shadows = LightShadows.None;
            PlaceWave(w);
            waves.Add(w); return w;
        }
        void PlaceWave(Wave w) { w.go.transform.SetPositionAndRotation(Sim.ToUnity(w.pos), Sim.ToUnity(w.rot)); w.go.transform.localScale = Vector3.one * w.scale; }

        // Afterimage: a translucent neon snapshot of the character's current pose.
        public void Afterimage(float strength = 1)
        {
            if (ch == null) return;
            var mat = Mats.Unlit(new Color(.12f, .42f, 1.1f, .2f * strength), Blend.Add, false, CullMode.Back, false);
            var g = new GameObject("afterimage"); g.transform.SetParent(root, false);
            foreach (var mr in ch.go.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled || !mr.sharedMaterial || mr.sharedMaterial.shader != Mats.LitShader) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var m = mr.transform.localToWorldMatrix;
                var o = new GameObject(mr.name); o.transform.SetParent(g.transform, false);
                o.transform.SetPositionAndRotation(m.GetPosition(), m.rotation); o.transform.localScale = m.lossyScale;
                o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = o.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
            ghosts.Add(new Ghost { go = g, mat = mat, max = .28f, start = .2f * strength });
        }

        // ---------- per-frame ----------
        public void Update(float dt, bool active, float charge)
        {
            time += dt;
            var bbase = ch.bladeBase.WorldPosition; var tip = ch.bladeTip.WorldPosition;
            var inner = Vector3.Lerp(bbase, tip, .25f);
            const float interval = 1 / 120f;
            // Only the fast part of the arc leaves a trail.
            bool fast = hasLast && dt > 0 && Vector3.Distance(tip, lastTip) / dt > 4.5f;
            if (active && fast)
            {
                // sparks thrown off the edge near the tip, more the faster the blade travels
                var vel = (tip - lastTip) / dt; float speed = vel.magnitude;
                sparkDebt += speed * dt * 2.2f;
                while (sparkDebt >= 1) { sparkDebt -= 1; EdgeSpark(Vector3.Lerp(Vector3.Lerp(lastTip, tip, R), bbase, R * .35f), vel); }
            }
            if (active && fast)
            {
                trailClock += dt;
                while (trailClock >= interval)
                {
                    trailClock -= interval; float a = 1 - trailClock / dt;
                    samples.Add(new Sample { b = Vector3.Lerp(lastBase, inner, a), tip = Vector3.Lerp(lastTip, tip, a), time = time - trailClock });
                }
            }
            else trailClock = 0;
            lastBase = inner; lastTip = tip; hasLast = true;
            samples.RemoveAll(s => time - s.time >= TRAIL_LIFE);
            if (samples.Count > maxSamples) samples.RemoveRange(0, samples.Count - maxSamples);
            BuildTrail();

            var bill = Billboard;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i]; p.life += dt;
                if (p.life >= p.max) { Object.Destroy(p.go); particles.RemoveAt(i); continue; }
                float t = p.life / p.max;
                if (p.spark)
                {
                    p.vel.y -= dt * 9; p.vel *= 1 - dt * 2.5f; p.pos += p.vel * dt;
                    float gy = ch.root.position.y + .02f; if (p.pos.y < gy) { p.pos.y = gy; p.vel.y *= -.35f; }
                    float speed = p.vel.magnitude;
                    p.t.position = Sim.ToUnity(p.pos);
                    p.t.localScale = new Vector3(p.width, p.width, Mathf.Max(.03f, speed * .045f));
                    if (speed > 1e-5f) p.t.rotation = Quaternion.LookRotation(Sim.ToUnity(p.vel));   // Unity-space vector
                    Tint(p.r, p.color, 1 - t * t);
                }
                else
                {
                    p.vel *= 1 - dt * 3; p.pos += p.vel * dt;
                    p.t.position = Sim.ToUnity(p.pos); p.t.rotation = bill;
                    p.t.localScale = Vector3.one * (p.size * (1 + t * 2.2f));
                    Tint(p.r, p.color, Mathf.Pow(1 - t, 1.5f));
                }
            }
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i]; f.life += dt; float t = f.life / f.max;
                if (t >= 1) { Object.Destroy(f.go); flashes.RemoveAt(i); continue; }
                f.t.localScale = Vector3.one * (f.size * (.4f + t * 1.1f)); f.t.rotation = bill;
                Tint(f.r, new Color(2, 2, 2), (1 - t) * (1 - t));
            }
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i]; r.life += dt; float t = r.life / (r.upright ? .26f : .45f);
                if (t >= 1) { Object.Destroy(r.go); rings.RemoveAt(i); continue; }
                r.t.localScale = Vector3.one * (.3f + (r.upright ? 1.6f : 4.2f) * Mathf.Sqrt(t) * r.power);
                Tint(r.r, new Color(1.8f, 2.2f, 2.6f), (1 - t) * (1 - t));
            }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i]; w.life += dt; float t = w.life / w.max;
                if (t >= 1) { Object.Destroy(w.go); waves.RemoveAt(i); continue; }
                w.pos += w.dir * (w.speed * dt); w.scale = 1 + t * .5f; PlaceWave(w);
                float op = Mathf.Pow(1 - t, 1.2f);
                for (int k = 0; k < w.renderers.Length; k++) Tint(w.renderers[k], w.colors[k], op);
                if (R < .5f) Burst(w.pos + new Vector3(0, .1f, 0), 1, .4f);
            }
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i]; g.life += dt; float t = g.life / g.max;
                if (t >= 1) { Object.Destroy(g.go); Object.Destroy(g.mat); ghosts.RemoveAt(i); continue; }
                Mats.SetOpacity(g.mat, g.start * Mathf.Pow(1 - t, 1.4f));
            }
            if (runeMat) runeMat.SetVector("_EmissionColor", (Vector4)(runeEmissive * (.12f + charge * 2)));
        }

        static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return p0 * (-.5f * t3 + t2 - .5f * t) + p1 * (1.5f * t3 - 2.5f * t2 + 1) + p2 * (-1.5f * t3 + 2 * t2 + .5f * t) + p3 * (.5f * t3 - .5f * t2);
        }
        void BuildTrail()
        {
            int n = samples.Count, v = 0;
            if (n >= 2)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    Sample a = samples[Mathf.Max(0, i - 1)], b = samples[i], c = samples[i + 1], d = samples[Mathf.Min(n - 1, i + 2)];
                    for (int k = 0; k < SUB; k++)
                    {
                        if (v / 3 >= maxSamples * SUB) break;
                        float u = (float)k / SUB, tm = b.time + (c.time - b.time) * u, age = Mathf.Clamp01((time - tm) / TRAIL_LIFE);
                        var pB = Catmull(a.b, b.b, c.b, d.b, u); var pT = Catmull(a.tip, b.tip, c.tip, d.tip, u);
                        float fade = Mathf.Pow(1 - age, 1.5f); var col = Color.Lerp(TRAIL_NEW, TRAIL_OLD, age);
                        var pM = Vector3.Lerp(pB, pT, .78f);
                        float peak = fade * TRAIL_GAIN * 1.6f, edge = peak * .45f;
                        tv[v] = Sim.ToUnity(pB); tv[v + 1] = Sim.ToUnity(pM); tv[v + 2] = Sim.ToUnity(pT);
                        tc[v] = new Color(0, 0, 0, 1); tc[v + 1] = new Color(col.r * peak, col.g * peak, col.b * peak, 1); tc[v + 2] = new Color(col.r * edge, col.g * edge, col.b * edge, 1);
                        v += 3;
                    }
                }
            }
            trailMesh.vertices = tv; trailMesh.colors = tc;
            int count = Mathf.Max(0, v / 3 - 1) * 12;
            trailMesh.SetIndices(tIndex, 0, count, MeshTopology.Triangles, 0, false);
            trailMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000);
        }
        public void ResetTrail() { samples.Clear(); hasLast = false; }
    }

    // Small procedural meshes for the effects.
    public static class FxMeshes
    {
        /// unit cube in Unity space (sparks: scaled thin, long along +Z)
        public static Mesh Box()
        {
            var m = new Mesh { name = "fx-box" };
            var v = new List<Vector3>(); var t = new List<int>();
            void Face(Vector3 n, Vector3 a, Vector3 b)
            {
                int s = v.Count; var c = n * .5f;
                v.Add(c - a * .5f - b * .5f); v.Add(c - a * .5f + b * .5f); v.Add(c + a * .5f + b * .5f); v.Add(c + a * .5f - b * .5f);
                t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward); Face(Vector3.down, Vector3.left, Vector3.forward);
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        /// camera-facing quad in Unity local XY (size 1), uv 0..1
        public static Mesh Quad()
        {
            var m = new Mesh { name = "fx-quad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        /// THREE.PlaneGeometry(w, h) in sim local XY, converted to Unity (use with a sim->Unity rotation)
        public static Mesh SimPlane(float w, float h)
        {
            var m = new Mesh { name = "fx-plane" };
            m.vertices = new[] { Sim.ToUnity(new Vector3(-w / 2, -h / 2, 0)), Sim.ToUnity(new Vector3(w / 2, -h / 2, 0)), Sim.ToUnity(new Vector3(w / 2, h / 2, 0)), Sim.ToUnity(new Vector3(-w / 2, h / 2, 0)) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        /// THREE.RingGeometry(inner, outer, segs, 1, thetaStart, thetaLength) in sim local XY, converted
        public static Mesh RingSector(float inner, float outer, int segs, float thetaStart, float thetaLength)
        {
            var m = new Mesh { name = "fx-ring" };
            var v = new Vector3[(segs + 1) * 2]; var uv = new Vector2[v.Length]; var t = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float a = thetaStart + thetaLength * i / segs, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = Sim.ToUnity(new Vector3(c * inner, s * inner, 0)); v[i * 2 + 1] = Sim.ToUnity(new Vector3(c * outer, s * outer, 0));
                uv[i * 2] = new Vector2((float)i / segs, 0); uv[i * 2 + 1] = new Vector2((float)i / segs, 1);
                if (i < segs) { int a0 = i * 2; t.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 1, a0 + 3, a0 + 2 }); }
            }
            m.vertices = v; m.uv = uv; m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
