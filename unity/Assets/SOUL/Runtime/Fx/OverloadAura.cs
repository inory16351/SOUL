using UnityEngine;
using System.Collections.Generic;

namespace Soul
{
    // Persistent, allocation-free (per frame) aura for the Core Overload skill.
    // The public API uses sim-space positions; all mesh vertices are converted on upload.
    public class OverloadAura
    {
        const int TongueCount = 8;
        const int TongueSegments = 6;
        const int TonguePlanes = 2;
        const int GroundSegments = 24;
        const int GroundRings = 3;
        const int EmberCount = 16;

        const int TongueVertices = TongueCount * TonguePlanes * (TongueSegments + 1) * 2;
        const int GroundVertices = GroundSegments * GroundRings;
        const int EmberVertices = EmberCount * 4;
        const int VertexCount = TongueVertices + GroundVertices + EmberVertices;

        readonly System.Random random = new System.Random(140611);
        readonly GameObject root;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer meshRenderer;
        readonly Vector3[] vertices = new Vector3[VertexCount];
        readonly Color[] colors = new Color[VertexCount];
        readonly int[] indices;

        readonly float[] tongueAngle = new float[TongueCount];
        readonly float[] tongueRadius = new float[TongueCount];
        readonly float[] tongueHeight = new float[TongueCount];
        readonly float[] tongueWidth = new float[TongueCount];
        readonly float[] tonguePhase = new float[TongueCount];
        readonly float[] tongueSpeed = new float[TongueCount];
        readonly float[] tongueCurl = new float[TongueCount];
        readonly float[] groundShape = new float[GroundSegments];

        readonly Vector3[] emberPosition = new Vector3[EmberCount];
        readonly Vector3[] emberVelocity = new Vector3[EmberCount];
        readonly float[] emberAge = new float[EmberCount];
        readonly float[] emberLife = new float[EmberCount];
        readonly float[] emberSize = new float[EmberCount];
        readonly float[] emberPhase = new float[EmberCount];
        readonly bool[] emberBright = new bool[EmberCount];

        float time;
        float fade;
        float crackleTimer;
        float crackleLife;
        bool disposed;

        public OverloadAura(Transform parent)
        {
            root = new GameObject("overload-aura-natural");
            root.transform.SetParent(parent, false);

            for (int i = 0; i < TongueCount; i++)
            {
                tongueAngle[i] = Mathf.PI * 2f * (i + Range(-.16f, .16f)) / TongueCount;
                tongueRadius[i] = Range(.48f, .82f);
                tongueHeight[i] = Range(1.15f, 2.35f);
                tongueWidth[i] = Range(.14f, .28f);
                tonguePhase[i] = Range(0f, Mathf.PI * 2f);
                tongueSpeed[i] = Range(3.4f, 6.8f);
                tongueCurl[i] = Range(-.32f, .32f);
            }
            for (int i = 0; i < GroundSegments; i++) groundShape[i] = Range(.92f, 1.08f);
            for (int i = 0; i < EmberCount; i++) ResetEmber(i, Range(0f, 1.6f));

            indices = BuildIndices();
            mesh = new Mesh { name = "overload-aura" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(new Vector3(0f, 1.25f, 0f), new Vector3(4f, 3.5f, 4f));

            material = Mats.Unlit(Color.white, Blend.Add, true,
                UnityEngine.Rendering.CullMode.Off, false, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            crackleTimer = Range(.6f, 1.5f);
        }

        public void Update(float dt, Vector3 simPos, float strength)
        {
            if (disposed) return;
            dt = Mathf.Max(0f, dt);
            time += dt;
            float target = Mathf.Clamp01(strength);
            float fadeTime = target > fade ? .3f : .5f;
            fade = Mathf.MoveTowards(fade, target, dt / fadeTime);
            float eased = fade * fade * (3f - 2f * fade);

            crackleTimer -= dt;
            crackleLife = Mathf.Max(0f, crackleLife - dt);
            if (crackleTimer <= 0f)
            {
                crackleLife = Range(.055f, .13f);
                crackleTimer = Range(.6f, 1.5f);
            }
            float crackle = crackleLife > 0f ? .7f + .3f * Noise(time * 31f, 91.7f) : 0f;

            root.transform.position = Sim.ToUnity(simPos);
            meshRenderer.enabled = fade > .001f;
            if (!meshRenderer.enabled) return;

            BuildTongues(eased, crackle);
            BuildGround(eased, crackle);
            BuildEmbers(dt, eased, crackle);
            mesh.vertices = vertices;
            mesh.colors = colors;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Object.Destroy(root);
            Object.Destroy(mesh);
            Object.Destroy(material);
        }

        void BuildTongues(float amount, float crackle)
        {
            int v = 0;
            for (int i = 0; i < TongueCount; i++)
            {
                float a = tongueAngle[i];
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float flicker = .78f + .22f * Noise(time * tongueSpeed[i], tonguePhase[i] * 4.1f);
                float height = tongueHeight[i] * flicker * (.35f + .65f * amount);
                float tongueCrackle = crackle * (i == ((int)(time * 19f) % TongueCount) ? 1f : .18f);

                for (int plane = 0; plane < TonguePlanes; plane++)
                {
                    Vector3 side = plane == 0 ? tangent : radial;
                    for (int s = 0; s <= TongueSegments; s++)
                    {
                        float t = (float)s / TongueSegments;
                        float bend = Mathf.Pow(t, 1.35f);
                        float sway = Mathf.Sin(time * tongueSpeed[i] + tonguePhase[i] + t * 4.7f) * (.08f + .15f * t);
                        sway += (Noise(time * 2.2f + t * 1.7f, tonguePhase[i]) - .5f) * .16f * t;
                        Vector3 center = radial * tongueRadius[i];
                        center += tangent * (sway * bend + tongueCurl[i] * t * t);
                        center -= radial * (.08f * t + .08f * Mathf.Sin(t * Mathf.PI));
                        center.y = .035f + height * t;

                        float profile = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                        float halfWidth = tongueWidth[i] * (.16f + .84f * profile) * (1f - .72f * t);
                        if (s == TongueSegments) halfWidth = 0f;
                        Vector3 p0 = center - side * halfWidth;
                        Vector3 p1 = center + side * halfWidth;
                        vertices[v] = Sim.ToUnity(p0);
                        vertices[v + 1] = Sim.ToUnity(p1);

                        Color c = FlameColor(t, amount, tongueCrackle);
                        colors[v] = c;
                        colors[v + 1] = c;
                        v += 2;
                    }
                }
            }
        }

        void BuildGround(float amount, float crackle)
        {
            int start = TongueVertices;
            float breath = .82f + .18f * Noise(time * .82f, 38.2f);
            float scale = (.7f + .3f * amount) * breath;
            for (int ring = 0; ring < GroundRings; ring++)
            {
                float baseRadius = ring == 0 ? .34f : ring == 1 ? .91f : 1.34f;
                float alpha = ring == 0 ? .018f : ring == 1 ? .17f : 0f;
                for (int i = 0; i < GroundSegments; i++)
                {
                    float a = Mathf.PI * 2f * i / GroundSegments;
                    float crawl = .96f + .04f * Noise(time * .65f, i * 3.17f);
                    float radius = baseRadius * groundShape[i] * crawl * scale;
                    Vector3 p = new Vector3(Mathf.Cos(a) * radius, .018f, Mathf.Sin(a) * radius);
                    int n = start + ring * GroundSegments + i;
                    vertices[n] = Sim.ToUnity(p);
                    float hot = ring == 1 ? 1f : .58f;
                    colors[n] = new Color(2.2f * hot, .22f * hot, .055f * hot,
                        alpha * amount * (1f + crackle * .65f));
                }
            }
        }

        void BuildEmbers(float dt, float amount, float crackle)
        {
            int start = TongueVertices + GroundVertices;
            for (int i = 0; i < EmberCount; i++)
            {
                emberAge[i] += dt;
                if (emberAge[i] >= emberLife[i]) ResetEmber(i, 0f);
                Vector3 p = emberPosition[i];
                p += emberVelocity[i] * dt;
                p.x += Mathf.Sin(time * 2.3f + emberPhase[i]) * dt * .09f;
                p.z += Mathf.Sin(time * 1.7f + emberPhase[i] * 1.31f) * dt * .07f;
                emberPosition[i] = p;

                float life = Mathf.Clamp01(emberAge[i] / emberLife[i]);
                float envelope = Mathf.Min(1f, life * 8f) * Mathf.Pow(1f - life, .7f) * amount;
                float size = emberSize[i] * (1f - .35f * life) * (.45f + .55f * amount);
                float angle = emberPhase[i] + life * .7f;
                Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * size;
                Vector3 up = Vector3.up * size * (emberBright[i] ? 2.8f : 2.1f);
                int v = start + i * 4;
                vertices[v] = Sim.ToUnity(p - side);
                vertices[v + 1] = Sim.ToUnity(p + side);
                vertices[v + 2] = Sim.ToUnity(p + side + up);
                vertices[v + 3] = Sim.ToUnity(p - side + up);

                float gain = emberBright[i] ? 1.65f : 1f;
                Color c = new Color(2.7f * gain, .31f * gain, .07f * gain,
                    envelope * (emberBright[i] ? .8f : .5f) * (1f + crackle * .35f));
                colors[v] = c;
                colors[v + 1] = c;
                colors[v + 2] = new Color(c.r * .65f, c.g * .42f, c.b * .3f, 0f);
                colors[v + 3] = colors[v + 2];
            }
        }

        Color FlameColor(float t, float amount, float crackle)
        {
            float tip = t * t;
            float rim = Mathf.Sin(t * Mathf.PI);
            float alpha = (.035f + .22f * rim) * (1f - tip) * amount;
            Color core = new Color(2.2f, .25f, .075f, alpha);
            Color dark = new Color(.62f, .018f, .004f, 0f);
            Color c = Color.Lerp(core, dark, tip);
            c.r += crackle * 1.25f;
            c.g += crackle * .18f;
            c.b += crackle * .025f;
            c.a *= 1f + crackle * .75f;
            return c;
        }

        void ResetEmber(int i, float age)
        {
            float a = Range(0f, Mathf.PI * 2f);
            float r = Range(.42f, .94f);
            emberPosition[i] = new Vector3(Mathf.Cos(a) * r, Range(.05f, .28f), Mathf.Sin(a) * r);
            emberVelocity[i] = new Vector3(Range(-.13f, .13f), Range(.48f, 1.05f), Range(-.13f, .13f));
            emberLife[i] = Range(.75f, 1.7f);
            emberAge[i] = Mathf.Min(age, emberLife[i] * .8f);
            emberSize[i] = Range(.018f, .045f);
            emberPhase[i] = Range(0f, Mathf.PI * 2f);
            emberBright[i] = random.NextDouble() < .24;
        }

        int[] BuildIndices()
        {
            var list = new List<int>(960);
            int v = 0;
            for (int i = 0; i < TongueCount * TonguePlanes; i++)
            {
                for (int s = 0; s < TongueSegments; s++)
                {
                    int a = v + s * 2;
                    list.Add(a); list.Add(a + 2); list.Add(a + 1);
                    list.Add(a + 1); list.Add(a + 2); list.Add(a + 3);
                }
                v += (TongueSegments + 1) * 2;
            }

            int ground = TongueVertices;
            for (int ring = 0; ring < GroundRings - 1; ring++)
            {
                for (int i = 0; i < GroundSegments; i++)
                {
                    int next = (i + 1) % GroundSegments;
                    int a = ground + ring * GroundSegments + i;
                    int b = ground + ring * GroundSegments + next;
                    int c = a + GroundSegments;
                    int d = b + GroundSegments;
                    list.Add(a); list.Add(c); list.Add(b);
                    list.Add(b); list.Add(c); list.Add(d);
                }
            }

            int embers = TongueVertices + GroundVertices;
            for (int i = 0; i < EmberCount; i++)
            {
                int a = embers + i * 4;
                list.Add(a); list.Add(a + 2); list.Add(a + 1);
                list.Add(a); list.Add(a + 3); list.Add(a + 2);
            }
            return list.ToArray();
        }

        float Range(float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        static float Noise(float x, float salt)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, salt), Hash(i + 1, salt), f);
        }

        static float Hash(int n, float salt)
        {
            float x = Mathf.Sin(n * 127.1f + salt * 311.7f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }
    }
}
