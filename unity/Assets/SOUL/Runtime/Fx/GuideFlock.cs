using UnityEngine;

namespace Soul
{
    // Visuals only: SoulGame.Guide owns route, timing and fade. Like Fauna, animate
    // sim-space triangles directly in persistent buffers, then mirror on upload.
    public class GuideFlock
    {
        const int Members = 7;
        readonly System.Random random;
        readonly Batch glow, matter;
        readonly Vector3[] positions = new Vector3[Members];
        readonly Vector2[] spread = new Vector2[Members];
        readonly float[] phase = new float[Members], rate = new float[Members];
        readonly float[] size = new float[Members], lag = new float[Members];
        float time;
        int currentZone = -1;
        bool gathered;

        // Linear HDR only for luminous objects; material colours come from sRGB hex.
        static readonly Color SoulCyan = new Color(.5f, 1.7f, 2.2f);
        static readonly Color Orange = new Color(2.4f, .65f, .12f);
        static readonly Color HotMetal = new Color(2.5f, 1.65f, .75f);
        static readonly Color Toxic = new Color(.55f, 1.9f, .24f);
        static readonly Color Electric = new Color(.4f, 1.15f, 2.4f);
        static readonly Color Crow = MeshBuilder.Hex(0x25282c).linear;
        static readonly Color Feather = MeshBuilder.Hex(0x3e4247).linear;
        static readonly Color Moth = MeshBuilder.Hex(0xbcbcb2).linear;
        static readonly Color Paper = MeshBuilder.Hex(0xaca89b).linear;
        static readonly Color Leaf = MeshBuilder.Hex(0x876044).linear;
        static readonly Color LeafDark = MeshBuilder.Hex(0x584331).linear;
        static readonly Color Smoke = MeshBuilder.Hex(0x383532).linear;

        public GuideFlock(Transform parent)
        {
            random = new System.Random(150427);
            // Total allocated/uploaded vertices: 540, even when switching looks.
            glow = new Batch(parent, "guide fire and sparks", 300, Blend.Add);
            matter = new Batch(parent, "guide wings and debris", 240, Blend.Alpha);
            for (int i = 0; i < Members; i++)
            {
                float angle = Range(0, Mathf.PI * 2), radius = Range(1, 1.95f);
                spread[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                phase[i] = Range(0, Mathf.PI * 2);
                rate[i] = Range(.8f, 1.25f);
                size[i] = Range(.85f, 1.12f);
                lag[i] = Range(2.4f, 4.8f);
            }
        }

        /// zone 0..7; lead = ground centre (sim); dir = unit travel direction (sim XZ).
        /// alpha 0 disables renderers; restart re-gathers even while hidden.
        public void Update(float dt, int zone, Vector3 lead, Vector3 dir, float alpha, bool restart)
        {
            dt = Mathf.Max(0, dt);
            zone = Mathf.Clamp(zone, 0, 7);
            alpha = Mathf.Clamp01(alpha);
            bool reset = restart || !gathered || zone != currentZone;
            if (reset) { time = 0; currentZone = zone; gathered = true; }
            else if (alpha > 0) time += dt;
            if (alpha <= 0)
            {
                glow.renderer.enabled = false;
                matter.renderer.enabled = false;
                if (!reset) return;
            }

            dir.y = 0;
            dir = dir.sqrMagnitude > .0001f ? dir.normalized : Vector3.forward;
            Vector3 side = new Vector3(dir.z, 0, -dir.x);
            float ground = Terrain.Height(lead.x, lead.z);
            int count = zone == 2 ? 3 : zone == 4 ? 7 : zone == 0 || zone == 7 ? 5 : 6;
            glow.Begin(); matter.Begin();
            for (int i = 0; i < count; i++)
            {
                float p = phase[i], t = time * rate[i];
                float wander = zone == 1 ? .38f : zone == 2 ? .16f : .24f;
                Vector3 home = lead + side * (spread[i].x + Mathf.Sin(t * 1.9f + p) * wander)
                    + dir * (spread[i].y + Mathf.Sin(t * 1.3f + p * 2) * wander);
                home.y = ground;
                positions[i] = reset ? home : Vector3.Lerp(positions[i], home, 1 - Mathf.Exp(-dt * lag[i]));
                Vector3 c = positions[i];
                // Local ground keeps low motes and bouncing sparks above slopes.
                c.y = Terrain.Height(c.x, c.z);
                float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / 1.1f));
                switch (zone)
                {
                    case 0:
                        c.y += Mathf.Lerp(.8f, 1.2f + .28f * Mathf.Sin(t * 2 + p), rise);
                        Ember(c, dir, side, .10f * size[i], SoulCyan,
                            .65f + .25f * Mathf.Sin(t * 17 + p), t, p);
                        break;
                    case 1:
                        if (i < 4) c += side * Mathf.Sin(t * 9.2f + p) * .1f
                            + dir * Mathf.Sin(t * 6.7f + p * 2) * .06f;
                        c.y += i < 4 ? .65f + rise * (.55f + .3f * Mathf.Sin(t * 4.2f + p))
                            : .3f + .24f * (1 + Mathf.Sin(t * 2 + p));
                        if (i < 4) Wings(c, dir, side, .17f * size[i], t, p, true);
                        else Tumble(c, dir, side, .17f * size[i], t * 2.6f + p, true);
                        break;
                    case 2:
                        c.y += 3.15f + rise * (1.1f + .55f * Mathf.Sin(t * .8f + p));
                        Wings(c, dir, side, .24f * size[i], t, p, false);
                        break;
                    case 3:
                        // A parabola with a sharp velocity reversal at ground contact.
                        float hop = Mathf.Repeat(t * (1.35f + .08f * i) + p, 1);
                        c.y += .07f + 4 * hop * (1 - hop) * (.35f + .07f * i);
                        Vector3 flight = dir * .65f + Vector3.up * (1 - 2 * hop) * 2;
                        Ember(c, flight.normalized, side, .065f * size[i],
                            Color.Lerp(HotMetal, Orange, hop), .7f + .3f * (1 - hop), t, p);
                        break;
                    case 4:
                        if (i < 5)
                        {
                            c.y += .6f + rise * (.55f + .4f * Mathf.Sin(t * 1.6f + p));
                            Ember(c, (dir + Vector3.up * .55f).normalized, side, .09f * size[i],
                                Orange, .65f + .25f * Mathf.Sin(t * 19 + p), t, p);
                        }
                        else
                        {
                            float age = Mathf.Repeat(t * .42f + p, 1);
                            c.y += .8f + age * 1.2f;
                            c -= dir * age * .65f;
                            Puff(c, side, dir, age, p);
                        }
                        break;
                    case 5:
                        c.y += i == 5 ? 1.45f + rise * .55f + .18f * Mathf.Sin(t + p)
                            : .4f + rise * (.4f + .26f * Mathf.Sin(t * 1.8f + p));
                        if (i == 5) Wings(c, dir, side, .18f, t * 1.4f, p, false);
                        else Tumble(c, dir, side, .18f * size[i], t * 2.2f + p, false);
                        break;
                    case 6:
                        c.y += .44f + rise * (.25f + .22f * Mathf.Sin(t * .95f + p));
                        float pulse = .16f + .74f * Mathf.Pow(.5f + .5f * Mathf.Sin(t * 2.3f + p), 2);
                        GlowPatch(c, side * .10f * size[i], dir * .12f * size[i], Toxic, pulse);
                        // A dark wing pair gives the pulse a physical carrier.
                        float flutter = .025f + .035f * Mathf.Sin(t * 30 + p);
                        matter.Tri(c, c - dir * .09f + side * .09f + Vector3.up * flutter,
                            c + dir * .06f, WithAlpha(Feather, .6f));
                        matter.Tri(c, c + dir * .06f,
                            c - dir * .09f - side * .09f + Vector3.up * flutter, WithAlpha(Feather, .6f));
                        break;
                    default:
                        c.y += .6f + rise * (.45f + .22f * Mathf.Sin(t + p));
                        Crackle(c, dir, side, t, p);
                        break;
                }
            }
            if (alpha <= 0) return;
            // Bounds follow actual emitted geometry, including height changes and lag.
            glow.Upload(alpha); matter.Upload(alpha);
        }

        void Ember(Vector3 c, Vector3 forward, Vector3 side, float s, Color color, float flicker, float t, float p)
        {
            GlowPatch(c, side * s, (new Vector3(-side.z, 0, side.x) + Vector3.up) * s * .8f, color, flicker);
            // Separate flecks, never a continuous line or directional marker.
            for (int k = 1; k <= 3; k++)
            {
                Vector3 tail = c - forward * (.085f * k)
                    + side * Mathf.Sin(t * 9 + p + k) * .025f;
                float w = s * (.44f - k * .08f);
                tail.y = Mathf.Max(tail.y, Terrain.Height(tail.x, tail.z) + .025f);
                glow.Tri(tail - side * w, tail + forward * w + Vector3.up * w,
                    tail + side * w, WithAlpha(color, flicker * (.45f - k * .1f)));
            }
        }

        void GlowPatch(Vector3 c, Vector3 u, Vector3 v, Color color, float strength)
        {
            Color core = WithAlpha(color, strength), rim = WithAlpha(color, 0);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3, b = (k + 1) * Mathf.PI / 3;
                glow.Tri(c, c + u * Mathf.Cos(b) + v * Mathf.Sin(b),
                    c + u * Mathf.Cos(a) + v * Mathf.Sin(a), core, rim, rim);
            }
        }

        void Wings(Vector3 c, Vector3 forward, Vector3 side, float span, float t, float p, bool moth)
        {
            // Crow downstrokes come in short bouts separated by a banking glide.
            float bout = Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(.35f, .7f, .5f + .5f * Mathf.Sin(t * 1.1f + p)));
            float flap = moth ? Mathf.Sin(t * 25 + p) * .95f
                : -.08f + Mathf.Sin(t * 5.2f + p) * .55f * bout;
            float bank = Mathf.Sin(t * 1.6f + p) * .12f;
            Vector3 nose = c + forward * span * .64f;
            Vector3 tail = c - forward * span * .68f;
            Color baseColor = moth ? Moth : Crow;
            for (int k = 0; k < 2; k++)
            {
                float sign = k == 0 ? -1 : 1;
                Vector3 wing = side * (sign * Mathf.Cos(flap)) + Vector3.up * (Mathf.Sin(flap) + sign * bank);
                Vector3 elbow = c + wing * span * .53f + forward * span * .23f;
                Vector3 tip = c + wing * span - forward * span * .12f;
                Vector3 back = c + wing * span * .68f - forward * span * .49f;
                matter.Tri(nose, elbow, tail, baseColor);
                matter.Tri(elbow, tip, back, moth ? Shade(Moth, .8f) : Feather);
                matter.Tri(elbow, back, tail, baseColor);
                if (moth)
                    matter.Tri(tail, back, c + wing * span * .4f - forward * span * .82f, Shade(Moth, .72f));
                else
                    matter.Tri(back, tip, tip - forward * span * .25f, Crow);
            }
            Vector3 ridge = c + Vector3.up * span * .15f;
            matter.Tri(nose, tail + side * span * .12f, ridge, moth ? LeafDark : Crow);
            matter.Tri(nose, ridge, tail - side * span * .12f, moth ? LeafDark : Feather);
            if (!moth)
            {
                matter.Tri(tail + side * span * .19f - forward * span * .24f, tail,
                    tail - side * span * .19f - forward * span * .24f, Crow);
                matter.Tri(nose - side * span * .09f, nose + forward * span * .24f,
                    nose + side * span * .09f, Crow);
            }
        }

        void Tumble(Vector3 c, Vector3 forward, Vector3 side, float s, float angle, bool paper)
        {
            // Explicit orthogonal sim basis: yaw then roll, without Unity rotations.
            Vector3 longAxis = forward * Mathf.Cos(angle * .63f) + side * Mathf.Sin(angle * .63f);
            Vector3 across = new Vector3(longAxis.z, 0, -longAxis.x);
            Vector3 wide = across * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            Vector3 normal = Vector3.Cross(longAxis, wide);
            if (paper)
            {
                Vector3 foldTop = c + longAxis * s + normal * s * .12f;
                Vector3 foldBottom = c - longAxis * s + normal * s * .12f;
                Vector3 topLeft = c + longAxis * s * .84f - wide * s * .65f;
                Vector3 bottomLeft = c - longAxis * s * .91f - wide * s * .56f;
                Vector3 topRight = c + longAxis * s * .73f + wide * s * .58f;
                Vector3 bottomRight = c - longAxis * s * .88f + wide * s * .66f;
                matter.Tri(topLeft, bottomLeft, foldBottom, Paper);
                matter.Tri(topLeft, foldBottom, foldTop, Paper);
                matter.Tri(foldTop, foldBottom, bottomRight, Shade(Paper, .72f));
                matter.Tri(foldTop, bottomRight, topRight, Shade(Paper, .72f));
                return;
            }
            Vector3 a = c + longAxis * s, d = c - longAxis * s;
            Vector3 left = c - wide * s * .55f, right = c + wide * s * .65f;
            Vector3 crease = c + normal * s * .16f;
            matter.Tri(a, left, crease, Leaf);
            matter.Tri(left, d, crease, LeafDark);
            matter.Tri(d, right, crease, Leaf);
            matter.Tri(right, a, crease, Shade(Leaf, 1.12f));
            matter.Tri(d - wide * .012f, d - longAxis * s * .3f, d + wide * .012f, LeafDark);
        }

        void Puff(Vector3 c, Vector3 side, Vector3 forward, float age, float p)
        {
            float opacity = .22f * Mathf.Sin(age * Mathf.PI);
            float radius = .12f + .13f * age;
            for (int plane = 0; plane < 2; plane++)
            {
                Vector3 v = plane == 0 ? forward : Vector3.up;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3, b = (k + 1) * Mathf.PI / 3;
                    float r0 = radius * (.9f + .1f * Mathf.Sin(p + k * 2));
                    float r1 = radius * (.9f + .1f * Mathf.Sin(p + ((k + 1) % 6) * 2));
                    matter.Tri(c, c + (side * Mathf.Cos(b) + v * Mathf.Sin(b)) * r1,
                        c + (side * Mathf.Cos(a) + v * Mathf.Sin(a)) * r0,
                        WithAlpha(Smoke, opacity), WithAlpha(Smoke, 0), WithAlpha(Smoke, 0));
                }
            }
        }

        void Crackle(Vector3 c, Vector3 forward, Vector3 side, float t, float p)
        {
            // Short local chains run across the flight as often as along it.
            float clock = t * 2.1f + p;
            float cycle = Mathf.Repeat(clock, 1);
            float tick = Mathf.Floor(t * 24 + p);
            Vector3 axis = forward * Mathf.Sin(p * 3) + side * Mathf.Cos(p * 3);
            for (int k = 0; k < 3; k++)
            {
                float age = cycle - k * .07f;
                if (age < 0 || age > .14f) continue;
                Vector3 q = c + axis * ((k - 1) * .14f)
                    + side * Mathf.Sin(tick * 7.3f + p + k * 3.1f) * .055f
                    + Vector3.up * Mathf.Sin(tick * 5.7f + k) * .06f;
                float blink = 1 - age / .14f;
                GlowPatch(q, side * .07f, (forward + Vector3.up * .5f) * .08f, Electric, blink);
            }
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();
        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        static Color Shade(Color c, float gain) => new Color(c.r * gain, c.g * gain, c.b * gain, c.a);

        // Fixed triangle soup, allocated once. Two-sided for tumbling sheets and wings.
        sealed class Batch
        {
            internal readonly MeshRenderer renderer;
            readonly Mesh mesh;
            readonly Material material;
            readonly Vector3[] vertices;
            readonly Color[] colors;
            int used;
            Vector3 min, max;

            internal Batch(Transform parent, string name, int capacity, Blend blend)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                vertices = new Vector3[capacity]; colors = new Color[capacity];
                var triangles = new int[capacity];
                for (int i = 0; i < capacity; i += 3)
                {
                    triangles[i] = i; triangles[i + 1] = i + 2; triangles[i + 2] = i + 1;
                }
                mesh = new Mesh { name = name }; mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = go.AddComponent<MeshRenderer>();
                material = Mats.Unlit(Color.white, blend, true, UnityEngine.Rendering.CullMode.Off, false, true);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false; renderer.enabled = false;
            }

            internal void Begin() { used = 0; }
            internal void Tri(Vector3 a, Vector3 b, Vector3 c, Color color) => Tri(a, b, c, color, color, color);
            internal void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
            {
                Vertex(a, ca); Vertex(b, cb); Vertex(c, cc);
            }
            void Vertex(Vector3 p, Color color)
            {
                p = Sim.ToUnity(p);
                if (used == 0) { min = p; max = p; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                vertices[used] = p; colors[used++] = color;
            }
            internal void Upload(float alpha)
            {
                renderer.enabled = used > 0 && alpha > 0;
                if (!renderer.enabled) return;
                // Degenerate unused triangles so old zone contents cannot leak through.
                for (int i = used; i < vertices.Length; i++)
                {
                    vertices[i] = vertices[0]; colors[i] = Color.clear;
                }
                Mats.SetColor(material, new Color(1, 1, 1, alpha));
                mesh.vertices = vertices; mesh.colors = colors;
                mesh.bounds = new Bounds((min + max) * .5f, max - min + Vector3.one * .1f);
            }
        }
    }
}
