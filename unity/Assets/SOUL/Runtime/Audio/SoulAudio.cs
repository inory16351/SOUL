using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Sound: music manager (crossfading loops per game state) + sound-effect player (3D position, volume /
    // pitch jitter, per-sound voice limits). Installs itself on the SoulGame object after the scene loads, so no
    // scene or SoulGame.Awake change is needed; the game-side event hooks live in SoulGame.Audio.cs.
    //   Music: Resources/Audio/Music/*.mp3 (Gemini, see Resources/Audio/CREDITS.md)
    //   Effects: SfxSynth (procedural, built at startup)
    //   F8 = mute toggle (debug)
    public class SoulAudio : MonoBehaviour
    {
        public static SoulAudio I;
        public static float musicVolume = .5f, sfxVolume = .8f;
        /// overall volume (Esc settings · 사운드), applied through AudioListener.volume
        public static float masterVolume = 1;
        public static bool muted;
        /// sim-space listener anchor (the player): sounds fade with distance from here, not from the high camera
        public Vector3 listener;
        public const float NEAR = 10, FAR = 45;

        /// music keys → Resources paths
        static readonly Dictionary<string, string> TRACKS = new Dictionary<string, string>
        {
            { "field", "Audio/Music/field_ashen_ruins" },
            { "boss", "Audio/Music/boss_ash_warden" },
            { "purify", "Audio/Music/purify_bloom" },
            { "rest", "Audio/Music/rest_pod" },
        };
        /// tracks that play once, then fall back to the state music
        static readonly HashSet<string> ONCE = new HashSet<string> { "purify" };
        readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>();

        AudioSource[] music = new AudioSource[2]; int cur; float fade = 1, fadeTime = 2;
        public string musicKey = "", wantKey = "field";
        string onceKey; float onceUntil;

        // ---- effects ----
        const int VOICES = 28;
        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();
        /// how many times each sound was triggered (verification / debug)
        public static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        public static readonly List<string> log = new List<string>();
        static readonly Dictionary<string, int> MAX_VOICES = new Dictionary<string, int> { { "step", 2 }, { "stepWater", 2 }, { "hit", 3 }, { "warn", 2 }, { "pickup", 3 } };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var g = FindFirstObjectByType<SoulGame>();
            if (g && g.enabled && !g.GetComponent<SoulAudio>()) g.gameObject.AddComponent<SoulAudio>();
        }

        SoulGame game;
        void Awake()
        {
            I = this; game = GetComponent<SoulGame>();
            if (!FindFirstObjectByType<AudioListener>() && Camera.main) Camera.main.gameObject.AddComponent<AudioListener>();
            var root = new GameObject("Audio").transform; root.SetParent(transform, false);
            for (int i = 0; i < 2; i++)
            {
                var s = new GameObject("music" + i).AddComponent<AudioSource>(); s.transform.SetParent(root, false);
                s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; s.priority = 0; music[i] = s;
            }
            for (int i = 0; i < VOICES; i++) voices.Add(NewVoice(root, "sfx" + i));
            SfxSynth.BuildAsync();
            counts.Clear(); log.Clear();
        }
        void Start() { if (game) game.AudioHook(this); }
        void OnDestroy() { if (I == this) I = null; }

        AudioSource NewVoice(Transform root, string name)
        {
            var s = new GameObject(name).AddComponent<AudioSource>(); s.transform.SetParent(root, false);
            s.playOnAwake = false; s.spatialBlend = .55f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 60; s.maxDistance = 200; s.dopplerLevel = 0;
            return s;
        }

        AudioClip Track(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (loaded.TryGetValue(key, out var c)) return c;
            c = TRACKS.TryGetValue(key, out var path) ? Resources.Load<AudioClip>(path) : null;
            loaded[key] = c; return c;
        }
        public bool HasTrack(string key) => Track(key) != null;

        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F8)) { muted = !muted; Debug.Log("[SOUL audio] " + (muted ? "muted" : "unmuted")); }
            AudioListener.volume = muted ? 0 : masterVolume;
            if (game) game.AudioTick(Time.unscaledDeltaTime);
            MusicUpdate(Time.unscaledDeltaTime);
        }

        // ---------- music ----------
        /// one-shot track over the state music (e.g. purification); returns to the state music when it ends
        public void MusicOnce(string key)
        {
            var c = Track(key); if (!c) return;
            onceKey = key; onceUntil = Time.unscaledTime + c.length - 2.5f; Count("music:" + key);
        }
        public string OnceKey => onceKey;
        public void MusicCancelOnce() => onceKey = null;
        /// jump the current track to a fraction of its length (boss phase 2 = second half)
        public void MusicSeek(float frac)
        {
            var s = music[cur]; if (s.clip) s.time = Mathf.Clamp(frac * s.clip.length, 0, s.clip.length - .1f);
        }
        void MusicUpdate(float dt)
        {
            if (onceKey != null && Time.unscaledTime > onceUntil) onceKey = null;
            string key = onceKey ?? wantKey;
            if (!HasTrack(key)) key = HasTrack(wantKey) ? wantKey : "field";
            if (key != musicKey)
            {
                var c = Track(key);
                cur = 1 - cur; var s = music[cur];
                s.clip = c; s.loop = !ONCE.Contains(key); s.time = 0; if (c) s.Play(); else s.Stop();
                fade = 0; fadeTime = key == "boss" ? 1.2f : 2.5f; musicKey = key; Count("music:" + key);
            }
            fade = Mathf.Min(1, fade + dt / fadeTime);
            music[cur].volume = musicVolume * Mathf.SmoothStep(0, 1, fade);
            var o = music[1 - cur]; o.volume = musicVolume * (1 - Mathf.SmoothStep(0, 1, fade)) * (o.clip ? 1 : 0);
            if (fade >= 1 && o.isPlaying) o.Stop();
        }
        public string MusicState => $"{musicKey} t={(music[cur].clip ? music[cur].time : 0):0.0}/{(music[cur].clip ? music[cur].clip.length : 0):0} vol={music[cur].volume:0.00} playing={music[cur].isPlaying}";

        // ---------- effects ----------
        static void Count(string name)
        {
            counts[name] = (counts.TryGetValue(name, out var n) ? n : 0) + 1;
            log.Add($"{Time.frameCount}:{name}"); if (log.Count > 200) log.RemoveAt(0);
        }
        /// play `name` at sim-space `pos` (null = at the listener, 2D). vol/pitch jitter ±jit.
        public static AudioSource Play(string name, Vector3? pos = null, float vol = 1, float pitch = 1, float jit = .06f, float delay = 0, float minGap = .03f)
        {
            if (!I) return null; return I.PlayImpl(name, pos, vol, pitch, jit, delay, minGap);
        }
        AudioSource PlayImpl(string name, Vector3? pos, float vol, float pitch, float jit, float delay, float minGap)
        {
            float now = Time.unscaledTime;
            if (lastAt.TryGetValue(name, out var last) && now - last < minGap) return null;
            float att = Attenuation(pos); if (att <= 0) return null;
            var clip = SfxSynth.Get(name); if (!clip) return null;
            int busy = 0; AudioSource free = null, oldest = null; float oldestT = -1;
            foreach (var v in voices)
            {
                if (v.isPlaying) { if (v.clip && v.clip.name == name) busy++; if (v.time > oldestT) { oldestT = v.time; oldest = v; } }
                else if (free == null) free = v;
            }
            if (MAX_VOICES.TryGetValue(name, out var max) && busy >= max) return null;
            var s = free ?? oldest; if (!s) return null;
            lastAt[name] = now; Count(name);
            s.Stop(); s.clip = clip; s.loop = false;
            s.volume = sfxVolume * vol * att * (1 + Random.Range(-jit, jit));
            s.pitch = pitch * (1 + Random.Range(-jit, jit) * .8f);
            s.spatialBlend = pos.HasValue ? .55f : 0;
            s.transform.position = pos.HasValue ? Sim.ToUnity(pos.Value) : Vector3.zero;
            if (delay > 0) s.PlayDelayed(delay); else s.Play();
            return s;
        }
        float Attenuation(Vector3? pos)
        {
            if (!pos.HasValue) return 1;
            float d = Vector3.Distance(Sim.FlatY(pos.Value), Sim.FlatY(listener));
            return d <= NEAR ? 1 : 1 - Mathf.Clamp01((d - NEAR) / (FAR - NEAR));
        }
        /// keep a looping sound on while `on` (call every frame); fades out when switched off
        public static void Loop(string key, string clipName, bool on, Vector3? pos = null, float vol = 1, float pitch = 1)
        {
            if (!I) return; I.LoopImpl(key, clipName, on, pos, vol, pitch);
        }
        void LoopImpl(string key, string clipName, bool on, Vector3? pos, float vol, float pitch)
        {
            loops.TryGetValue(key, out var s);
            if (!on) { if (s && s.isPlaying) { s.volume -= Time.unscaledDeltaTime * 4 * sfxVolume; if (s.volume <= .01f) s.Stop(); } return; }
            if (!s) { s = NewVoice(voices[0].transform.parent, "loop:" + key); loops[key] = s; }
            if (!s.isPlaying) { s.clip = SfxSynth.Get(clipName); if (!s.clip) return; s.loop = true; s.volume = 0; s.Play(); Count(clipName + "(loop)"); }
            float target = sfxVolume * vol * Attenuation(pos);
            s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * 6);
            s.pitch = pitch; s.spatialBlend = pos.HasValue ? .55f : 0;
            if (pos.HasValue) s.transform.position = Sim.ToUnity(pos.Value);
        }
        public static string Stats()
        {
            var parts = new List<string>(); foreach (var kv in counts) parts.Add(kv.Key + "=" + kv.Value);
            parts.Sort(); return string.Join(", ", parts.ToArray());
        }
    }
}
