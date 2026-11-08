using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Procedural sound effects: every clip is built at startup from noise, oscillators, envelopes and
    // one-pole / state-variable filters (no audio files). Dry, heavy, metallic: low-poly scrap world.
    // Clips are mono 44.1 kHz; SoulAudio plays them in 3D with pitch / volume jitter.
    // To tweak a sound, edit its recipe in Build() and re-enter Play.
    public static class SfxSynth
    {
        public const int RATE = 44100;
        /// raw sample data, built off the main thread (pure math); AudioClips are made on first Get
        class Snd { public string name; public float[] data; }
        static Dictionary<string, Snd[]> raw = new Dictionary<string, Snd[]>();
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static System.Random rng = new System.Random(7);
        static volatile bool rawDone; static System.Threading.Tasks.Task job;
        static readonly System.Random pick = new System.Random();
        public static bool Ready => clips.Count > 0;
        public static IEnumerable<string> Names => clips.Keys;

        /// start building in the background (SoulAudio.Awake); Get returns null until it is done
        public static void BuildAsync()
        {
            if (rawDone || job != null) return;
            job = System.Threading.Tasks.Task.Run(() => { try { BuildRaw(); } catch (Exception e) { Debug.LogException(e); } });
        }
        /// synchronous build (tests / editor)
        public static void Build() { job = null; rawDone = false; clips.Clear(); BuildRaw(); Convert(); }
        static void Convert()
        {
            foreach (var kv in raw)
            {
                var v = new AudioClip[kv.Value.Length];
                for (int i = 0; i < v.Length; i++) { var d = kv.Value[i].data; v[i] = AudioClip.Create(kv.Value[i].name, d.Length, 1, RATE, false); v[i].SetData(d, 0); }
                clips[kv.Key] = v;
            }
        }

        public static AudioClip Get(string name)
        {
            if (clips.Count == 0) { if (!rawDone) return null; Convert(); }
            if (!clips.TryGetValue(name, out var v)) return null;
            return v[v.Length == 1 ? 0 : pick.Next(v.Length)];
        }

        // ---------- building blocks ----------
        static float R() => (float)(rng.NextDouble() * 2 - 1);
        static float[] Buf(float sec) => new float[Mathf.Max(1, (int)(sec * RATE))];
        static float T(int i) => i / (float)RATE;
        /// attack / exponential decay envelope
        static float Env(float t, float a, float d) => t < a ? t / Mathf.Max(a, 1e-4f) : Mathf.Exp(-(t - a) / Mathf.Max(d, 1e-4f));
        /// rise, hold, release (linear)
        static float Gate(float t, float a, float hold, float r) => t < a ? t / a : t < a + hold ? 1 : Mathf.Max(0, 1 - (t - a - hold) / r);

        /// state-variable filter run in place (mode 0 low, 1 band, 2 high); cutoff may change over time
        static void Svf(float[] s, Func<float, float> cutoff, float q, int mode)
        {
            float low = 0, band = 0, damp = 1 / Mathf.Max(.5f, q);
            for (int i = 0; i < s.Length; i++)
            {
                float f = 2 * Mathf.Sin(Mathf.PI * Mathf.Clamp(cutoff(T(i)), 20, RATE * .22f) / RATE);
                low += f * band; float high = s[i] - low - damp * band; band += f * high;
                s[i] = mode == 0 ? low : mode == 1 ? band : high;
            }
        }
        static void Svf(float[] s, float cutoff, float q, int mode) => Svf(s, _ => cutoff, q, mode);

        static float[] Noise(float sec) { var s = Buf(sec); for (int i = 0; i < s.Length; i++) s[i] = R(); return s; }
        /// brown-ish noise (rumble)
        static float[] Rumble(float sec) { var s = Buf(sec); float v = 0; for (int i = 0; i < s.Length; i++) { v = v * .985f + R() * .06f; s[i] = v * 4; } return s; }

        /// pitched oscillator with frequency curve; wave 0 sine, 1 saw, 2 square, 3 triangle
        static float[] Osc(float sec, Func<float, float> freq, int wave = 0)
        {
            var s = Buf(sec); double ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                ph += freq(T(i)) / RATE; float p = (float)(ph - Math.Floor(ph));
                s[i] = wave == 0 ? Mathf.Sin(p * Mathf.PI * 2) : wave == 1 ? p * 2 - 1 : wave == 2 ? (p < .5f ? 1 : -1) : 1 - 4 * Mathf.Abs(p - .5f);
            }
            return s;
        }

        /// inharmonic metal: sum of decaying partials (bell / anvil / plate)
        static float[] Metal(float sec, float f0, float[] ratios, float decay, float bright = 1)
        {
            var s = Buf(sec);
            for (int k = 0; k < ratios.Length; k++)
            {
                float f = f0 * ratios[k] * (1 + R() * .004f), d = decay / (1 + k * .35f * bright), a = 1 / (1 + k * .6f), ph = (float)rng.NextDouble() * 6.28f;
                for (int i = 0; i < s.Length; i++) { float t = T(i); s[i] += a * Mathf.Sin(ph + t * f * Mathf.PI * 2) * Mathf.Exp(-t / d); }
            }
            return s;
        }
        static readonly float[] ANVIL = { 1, 2.76f, 5.40f, 8.93f, 13.3f };
        static readonly float[] PLATE = { 1, 1.59f, 2.14f, 2.30f, 2.65f, 3.16f, 3.65f };
        static readonly float[] PIPE = { 1, 2.0f, 3.01f, 4.03f };

        static float[] Mix(params (float[] s, float g)[] parts)
        {
            int n = 0; foreach (var p in parts) n = Mathf.Max(n, p.s.Length);
            var o = new float[n];
            foreach (var p in parts) for (int i = 0; i < p.s.Length; i++) o[i] += p.s[i] * p.g;
            return o;
        }
        static float[] Shape(float[] s, Func<float, float> env) { for (int i = 0; i < s.Length; i++) s[i] *= env(T(i)); return s; }
        static float[] Delay(float[] s, float at) { int d = (int)(at * RATE); var o = new float[s.Length + d]; Array.Copy(s, 0, o, d, s.Length); return o; }
        static float[] Drive(float[] s, float k) { float n = (float)Math.Tanh(k); for (int i = 0; i < s.Length; i++) s[i] = (float)Math.Tanh(s[i] * k) / n; return s; }
        /// crude bit-crush / sample-hold (robot / digital glitch)
        static float[] Crush(float[] s, int hold, float steps) { float v = 0; for (int i = 0; i < s.Length; i++) { if (i % hold == 0) v = Mathf.Round(s[i] * steps) / steps; s[i] = v; } return s; }

        static Snd Clip(string name, float[] s, float peak = .9f, bool loop = false)
        {
            float m = 1e-6f; foreach (var v in s) m = Mathf.Max(m, Mathf.Abs(v));
            float g = peak / m;
            int fade = Mathf.Min(s.Length / 4, loop ? 0 : 220);
            for (int i = 0; i < s.Length; i++) { s[i] *= g; if (i >= s.Length - fade) s[i] *= (s.Length - i) / (float)fade; }
            if (loop) { int x = Mathf.Min(s.Length / 3, 2205); for (int i = 0; i < x; i++) { float k = i / (float)x; s[i] = s[i] * k + s[s.Length - x + i] * (1 - k); } Array.Resize(ref s, s.Length - x); }
            return new Snd { name = name, data = s };
        }
        static void Add(string name, params Snd[] v) => raw[name] = v;

        // ---------- recipes ----------
        static void BuildRaw()
        {
            raw = new Dictionary<string, Snd[]>(); rng = new System.Random(7);
            // player sword swings: 1 light whip, 2 mid, 3 heavy wide
            for (int k = 0; k < 3; k++)
            {
                var v = new Snd[3];
                for (int j = 0; j < 3; j++)
                {
                    float len = .26f + k * .08f, lo = 700 - k * 180, hi = 4200 - k * 900;
                    var n = Noise(len); Svf(n, t => Mathf.Lerp(lo, hi, Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI)), 2.2f + k * .3f, 1);
                    Shape(n, t => Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI), 1.6f));
                    var body = Osc(len, t => 140 - k * 25 + 60 * Mathf.Sin(t / len * Mathf.PI), 0); Shape(body, t => .25f * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI));
                    v[j] = Clip($"swing{k + 1}", Mix((n, 1), (body, k * .4f)), .7f);
                }
                Add("swing" + (k + 1), v);
            }
            // hits on scrap enemies: metal clank + crunch; heavy = deeper anvil + thump
            Add("hit", Variants(3, j =>
            {
                var m = Metal(.35f, 420 + j * 60, PLATE, .09f, 1.4f);
                var c = Noise(.12f); Svf(c, 2600, 1.2f, 1); Shape(c, t => Env(t, .001f, .025f));
                var th = Osc(.15f, t => 160 * Mathf.Exp(-t * 18) + 60); Shape(th, t => Env(t, .001f, .05f));
                return Clip("hit", Drive(Mix((m, .5f), (c, 1), (th, .8f)), 2), .85f);
            }));
            Add("hitHeavy", Variants(2, j =>
            {
                var m = Metal(.7f, 180 + j * 25, ANVIL, .22f, .8f);
                var c = Noise(.2f); Svf(c, 1500, 1, 1); Shape(c, t => Env(t, .001f, .05f));
                var th = Osc(.35f, t => 110 * Mathf.Exp(-t * 10) + 38); Shape(th, t => Env(t, .001f, .12f));
                return Clip("hitHeavy", Drive(Mix((m, .55f), (c, .9f), (th, 1.3f)), 2.5f), .95f);
            }));
            // Q cyclone / R blade storm: looping whirl (amplitude-modulated band noise)
            {
                float len = 1.2f; var n = Noise(len + .05f); Svf(n, t => 900 + 500 * Mathf.Sin(t * Mathf.PI * 2 * 5), 3, 1);
                Shape(n, t => .55f + .45f * Mathf.Sin(t * Mathf.PI * 2 * 5));
                var hum = Osc(len + .05f, t => 95, 1); Svf(hum, 400, 1, 0); Shape(hum, t => .3f);
                Add("whirl", Clip("whirl", Mix((n, 1), (hum, .6f)), .6f, true));
            }
            // W laser wave: falling sine chirp + buzzy saw + sizzle
            {
                var a = Osc(.7f, t => 1800 * Mathf.Exp(-t * 6) + 180, 0); Shape(a, t => Env(t, .005f, .25f));
                var b = Osc(.7f, t => 220 + 30 * Mathf.Sin(t * 70), 1); Svf(b, t => 3000 * Mathf.Exp(-t * 4) + 300, 2, 0); Shape(b, t => Env(t, .01f, .3f));
                var z = Noise(.7f); Svf(z, 6000, 1, 2); Shape(z, t => .3f * Env(t, .002f, .15f));
                Add("laser", Clip("laser", Mix((a, .7f), (b, .6f), (z, .5f)), .8f));
            }
            // E counter stance (small tick) and successful parry (bright ringing "쨍")
            {
                var m = Metal(.25f, 1600, PIPE, .05f); Add("guard", Clip("guard", m, .4f));
                var p = Metal(1.4f, 1250, new[] { 1, 2.32f, 4.25f, 6.63f, 9.38f }, .45f, .7f);
                var c = Noise(.05f); Svf(c, 7000, 1, 2); Shape(c, t => Env(t, .0005f, .01f));
                var lo = Osc(.3f, t => 90 * Mathf.Exp(-t * 6) + 50); Shape(lo, t => Env(t, .001f, .08f));
                Add("parry", Clip("parry", Mix((p, 1), (c, .8f), (lo, .6f)), .95f));
            }
            // dash: air rush + digital ghost shimmer
            {
                var n = Noise(.45f); Svf(n, t => 400 + 3000 * Mathf.Exp(-t * 7), 1.4f, 1); Shape(n, t => Env(t, .01f, .12f));
                var g = Osc(.45f, t => 900 + 400 * t, 3); Crush(g, 6, 6); Shape(g, t => .15f * Env(t, .02f, .1f));
                Add("dash", Clip("dash", Mix((n, 1), (g, .6f)), .6f));
            }
            // jump (servo + air) / land (heavy metal feet)
            {
                var s = Osc(.25f, t => 300 + 900 * t, 2); Svf(s, 1200, 1, 0); Shape(s, t => .3f * Env(t, .01f, .07f));
                var n = Noise(.25f); Svf(n, 1800, 1, 1); Shape(n, t => Env(t, .01f, .06f));
                Add("jump", Clip("jump", Mix((s, 1), (n, .7f)), .5f));
                var th = Osc(.3f, t => 80 * Mathf.Exp(-t * 12) + 40); Shape(th, t => Env(t, .001f, .07f));
                var cl = Metal(.25f, 520, PLATE, .04f); var d = Noise(.25f); Svf(d, 900, 1, 0); Shape(d, t => Env(t, .002f, .06f));
                Add("land", Clip("land", Mix((th, 1), (cl, .25f), (d, .8f)), .7f));
            }
            // footsteps: grit on ash / splash in water
            Add("step", Variants(4, j =>
            {
                var n = Noise(.11f); Svf(n, 1300 + j * 250, 1.3f, 1); Shape(n, t => Env(t, .002f, .025f));
                var k = Metal(.08f, 700 + j * 90, PLATE, .015f);
                return Clip("step", Mix((n, 1), (k, .25f)), .35f);
            }));
            Add("stepWater", Variants(3, j =>
            {
                var n = Noise(.3f); Svf(n, t => 900 + 2500 * Mathf.Exp(-t * 15) + j * 200, 2.5f, 1); Shape(n, t => Env(t, .005f, .06f));
                var b = Osc(.2f, t => 380 + j * 60 + 600 * t); Shape(b, t => .3f * Env(t, .002f, .04f));
                return Clip("stepWater", Mix((n, 1), (b, .5f)), .4f);
            }));
            // player hurt (crunch + error blip) / death (power-down) / reboot (power-up chime)
            {
                var c = Noise(.3f); Svf(c, 1100, 1, 1); Shape(c, t => Env(t, .001f, .06f));
                var m = Metal(.4f, 300, PLATE, .1f); var e = Osc(.2f, t => t < .08f ? 880 : 660, 2); Svf(e, 2500, 1, 0); Shape(e, t => .25f * Gate(t, .005f, .14f, .04f));
                Add("hurt", Clip("hurt", Drive(Mix((c, 1), (m, .5f), (Delay(e, .04f), 1)), 2), .85f));
                var pd = Osc(1.6f, t => 600 * Mathf.Exp(-t * 2.2f) + 30, 1); Svf(pd, t => 2500 * Mathf.Exp(-t * 2) + 100, 1.5f, 0); Shape(pd, t => Gate(t, .01f, 1.2f, .4f));
                var cr = Noise(1.6f); Svf(cr, 3000, 1, 1); Crush(cr, 40, 3); Shape(cr, t => .2f * Env(t, .01f, .4f));
                Add("death", Clip("death", Mix((pd, 1), (cr, .6f)), .8f));
                var up = Osc(1.1f, t => 90 + 700 * t * t, 1); Svf(up, t => 300 + 3000 * t, 1.5f, 0); Shape(up, t => Gate(t, .05f, .6f, .4f) * .6f);
                var ch = Mix((Delay(Metal(.6f, 880, PIPE, .18f), .7f), .5f), (Delay(Metal(.6f, 1320, PIPE, .18f), .85f), .4f));
                Add("reboot", Clip("reboot", Mix((up, 1), (ch, 1)), .7f));
            }
            // rope arm: launch (spring twang + whoosh) / winch reel loop
            {
                var tw = Osc(.4f, t => 240 * (1 + .3f * Mathf.Exp(-t * 20)), 1); Svf(tw, t => 3000 * Mathf.Exp(-t * 8) + 200, 3, 0); Shape(tw, t => Env(t, .002f, .1f));
                var wh = Noise(.4f); Svf(wh, t => 1200 + 3000 * t, 2, 1); Shape(wh, t => .5f * Env(t, .02f, .12f));
                Add("ropeFire", Clip("ropeFire", Mix((tw, 1), (wh, 1)), .6f));
                float len = .6f; var clk = Buf(len + .05f);
                for (int i = 0; i < clk.Length; i++) { float t = T(i), ph = t * 28 % 1; clk[i] = R() * Mathf.Exp(-ph * 60); }
                Svf(clk, 2400, 2, 1); var mot = Osc(len + .05f, t => 160, 1); Svf(mot, 700, 1, 0);
                Add("ropeReel", Clip("ropeReel", Mix((clk, 1), (mot, .3f)), .45f, true));
            }
            // enemy telegraph about to land: two-tone warning beep
            {
                var a = Osc(.22f, t => t < .1f ? 1320 : 990, 2); Svf(a, 3500, 1, 0); Shape(a, t => Gate(t, .004f, .08f, .01f) + Gate(t - .11f, .004f, .08f, .02f) * (t > .11f ? 1 : 0));
                Add("warn", Clip("warn", a, .45f));
            }
            // slam / quake impact: sub drop + debris
            Add("slam", Variants(2, j =>
            {
                var s = Osc(.9f, t => 70 * Mathf.Exp(-t * 4) + 28); Shape(s, t => Env(t, .002f, .3f));
                var d = Rumble(.9f); Svf(d, 500, 1, 0); Shape(d, t => Env(t, .005f, .25f));
                var cr = Noise(.5f); Svf(cr, 2200 + j * 300, 1, 1); Shape(cr, t => Env(t, .001f, .07f));
                var m = Metal(.6f, 140, ANVIL, .15f);
                return Clip("slam", Drive(Mix((s, 1.4f), (d, 1), (cr, .6f), (m, .35f)), 2.5f), .95f);
            }));
            // boss roar: detuned saw stack through a moving formant + grind
            {
                float len = 2.4f; Func<float, float> pitch = t => 62 + 20 * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI) + 3 * Mathf.Sin(t * 31);
                var a = Osc(len, pitch, 1); var b = Osc(len, t => pitch(t) * 1.013f, 1); var c = Osc(len, t => pitch(t) * .5f, 2);
                var v = Mix((a, 1), (b, 1), (c, .6f)); Svf(v, t => 380 + 900 * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI), 4, 1);
                var g = Noise(len); Svf(g, 700, 1.5f, 1); Crush(g, 12, 8);
                Add("roar", Clip("roar", Drive(Shape(Mix((v, 1), (g, .35f)), t => Gate(t, .25f, 1.4f, .75f)), 3), .95f));
            }
            // thrown scrap: whoosh + clatter on landing (landing comes with "slam")
            {
                var w = Noise(.6f); Svf(w, t => 500 + 1500 * Mathf.Sin(t / .6f * Mathf.PI), 2.5f, 1); Shape(w, t => Mathf.Sin(t / .6f * Mathf.PI));
                var cl = Mix((Metal(.5f, 380, PLATE, .08f), .4f), (Delay(Metal(.4f, 520, PLATE, .06f), .09f), .3f), (Delay(Metal(.3f, 300, PLATE, .05f), .2f), .3f));
                Add("throw", Clip("throw", w, .55f)); Add("clatter", Clip("clatter", cl, .6f));
            }
            // boss roll / charge: grinding rumble loop
            {
                float len = 1.0f; var r = Rumble(len + .05f); Svf(r, 220, 1.2f, 0);
                var gr = Noise(len + .05f); Svf(gr, t => 1400 + 300 * Mathf.Sin(t * 40), 4, 1); Shape(gr, t => .5f + .5f * Mathf.Sin(t * Mathf.PI * 2 * 8));
                Add("roll", Clip("roll", Mix((r, 1.5f), (gr, .35f)), .8f, true));
            }
            // energy pillars: rise (stone + hum swell) / light on (bright chord) / break (glass + metal) / wrong (buzzer)
            {
                var r = Rumble(1.4f); Svf(r, 300, 1, 0); Shape(r, t => Gate(t, .1f, .9f, .4f));
                var h = Osc(1.4f, t => 55 + 50 * t, 1); Svf(h, t => 200 + 800 * t, 2, 0); Shape(h, t => Gate(t, .6f, .5f, .3f) * .5f);
                Add("pillarRise", Clip("pillarRise", Mix((r, 1), (h, 1)), .8f));
                var on = Mix((Osc(.9f, t => 523, 3), .5f), (Osc(.9f, t => 784, 3), .35f), (Osc(.9f, t => 1046, 0), .3f)); Shape(on, t => Env(t, .01f, .3f));
                Add("pillarOn", Clip("pillarOn", Mix((on, 1), (Metal(.9f, 1046, PIPE, .25f), .4f)), .6f));
                var gl = Noise(.8f); Svf(gl, 5000, 1, 2); Shape(gl, t => Env(t, .001f, .12f));
                var mb = Metal(.9f, 340, PLATE, .2f); var zap = Osc(.5f, t => 800 * Mathf.Exp(-t * 9) + 60, 1); Shape(zap, t => Env(t, .001f, .1f));
                Add("pillarBreak", Clip("pillarBreak", Drive(Mix((gl, .8f), (mb, .7f), (zap, .7f)), 1.8f), .9f));
                var bz = Osc(.6f, t => 110, 2); var bz2 = Osc(.6f, t => 116, 1); var w = Mix((bz, 1), (bz2, .8f)); Svf(w, 1400, 1, 0); Shape(w, t => Gate(t, .005f, .2f, .02f) + (t > .28f ? Gate(t - .28f, .005f, .22f, .08f) : 0));
                Add("pillarWrong", Clip("pillarWrong", w, .65f));
            }
            // boss phase change: reversed-swell + heavy hit + low drone
            {
                var sw = Noise(1.6f); Svf(sw, t => 200 + 4000 * Mathf.Pow(Mathf.Clamp01(t / 1.1f), 3), 3, 1); Shape(sw, t => t < 1.1f ? Mathf.Pow(t / 1.1f, 2) : Mathf.Exp(-(t - 1.1f) * 15));
                var hit = Delay(Mix((Osc(1.5f, t => 55 * Mathf.Exp(-t * 3) + 30), 1.4f), (Metal(1.5f, 110, ANVIL, .5f), .5f)), 1.1f); Shape(hit, t => t < 1.1f ? 0 : Env(t - 1.1f, .002f, .6f));
                Add("phase", Clip("phase", Drive(Mix((sw, .7f), (hit, 1)), 2), .95f));
            }
            // world: pod rest (soft rising synth chord) / shrine resonance (deep bell swell) / purification wave / pickup / grudge part
            {
                float len = 2.2f; var c = Mix((Osc(len, t => 220, 3), .5f), (Osc(len, t => 277.2f, 3), .4f), (Osc(len, t => 329.6f, 3), .4f), (Osc(len, t => 440 * (1 + .003f * Mathf.Sin(t * 30)), 0), .3f));
                Shape(c, t => Gate(t, .5f, 1, .7f)); Add("rest", Clip("rest", c, .55f));
                var bell = Mix((Metal(3.5f, 98, ANVIL, 1.6f, .5f), 1), (Metal(3.5f, 147, PIPE, 1.2f), .5f)); Shape(bell, t => Mathf.Min(1, t / .25f));
                Add("shrine", Clip("shrine", bell, .75f));
                float pl = 4f; var pad = Mix((Osc(pl, t => 261.6f, 3), .5f), (Osc(pl, t => 329.6f, 3), .4f), (Osc(pl, t => 392, 0), .4f), (Osc(pl, t => 523.2f * (1 + .004f * Mathf.Sin(t * 25)), 0), .3f));
                var air = Noise(pl); Svf(air, t => 300 + 2500 * Mathf.Sin(Mathf.Clamp01(t / pl) * Mathf.PI), 2, 1);
                Add("purify", Clip("purify", Shape(Mix((pad, 1), (air, .3f)), t => Gate(t, 1.2f, 1.6f, 1.2f)), .7f));
                var pk = Mix((Osc(.25f, t => 1320, 3), 1), (Delay(Osc(.2f, t => 1760, 3), .06f), .8f)); Shape(pk, t => Env(t, .002f, .07f));
                Add("pickup", Clip("pickup", Mix((pk, 1), (Metal(.3f, 2200, PIPE, .05f), .3f)), .4f));
                float gl = 2.5f; var low = Mix((Osc(gl, t => 41, 1), 1), (Osc(gl, t => 43.6f, 1), 1), (Osc(gl, t => 61.7f, 0), .6f)); Svf(low, t => 120 + 200 * Mathf.Sin(t * 3), 3, 0);
                var whisper = Noise(gl); Svf(whisper, t => 900 + 400 * Mathf.Sin(t * 5), 6, 1);
                Add("grudge", Clip("grudge", Shape(Mix((low, 1), (whisper, .25f)), t => Gate(t, .4f, 1.3f, .8f)), .8f));
            }
            // breakable props and enemy death: wooden/concrete crunch + scrap collapse
            {
                var cr = Noise(.5f); Svf(cr, 900, 1, 1); Shape(cr, t => Env(t, .001f, .09f));
                var cl = Mix((Metal(.7f, 260, PLATE, .14f), .5f), (Delay(Metal(.5f, 410, PLATE, .1f), .12f), .35f), (Delay(Metal(.4f, 190, ANVIL, .12f), .25f), .4f));
                var th = Osc(.5f, t => 70 * Mathf.Exp(-t * 6) + 35); Shape(th, t => Env(t, .002f, .12f));
                Add("kill", Clip("kill", Drive(Mix((cr, .8f), (cl, 1), (th, 1)), 1.8f), .85f));
            }
            rawDone = true;
        }
        static Snd[] Variants(int n, Func<int, Snd> make) { var v = new Snd[n]; for (int j = 0; j < n; j++) v[j] = make(j); return v; }
    }
}
