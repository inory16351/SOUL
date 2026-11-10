using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // 생존자 기록 runtime (World/Lore.cs holds the text). A record is a small recorder device on the ground; within
    // 120 m it is built, and walking within 3.5 m plays it: the survivor's flickering cyan hologram stands over the
    // device and speaks (a crackle + subtitles, ~3.2 s a line). Collected records can be replayed from the Tab growth
    // screen ('기록' tab). After a zone purification the next main-story beat plays (scientist, by purified count).
    // Models are PLACEHOLDERS (Claude): Codex job 21 replaces RecordModels.
    public partial class SoulGame
    {
        class RecordProp { public Lore.Record rec; public GameObject root, holo; public Material holoMat; }
        readonly List<RecordProp> recordProps = new List<RecordProp>();
        float loreClock;
        // subtitles, shown on the transmission screen (static + the speaker's silhouette + typed dialogue)
        string subWho; string[] subLines; int subLine; float subT; RecordProp subProp;
        Color subTint = new Color(.55f, 1.5f, 2f); int subBuild; float subHeight = 1.7f, txOpen;
        public bool SubtitlePlaying => subLines != null;
        float SubLineDur => subLines == null || subLine >= subLines.Length ? 3.4f : Mathf.Max(3.2f, subLines[subLine].Length * .085f + 1.8f);

        void LoreUpdate(float dt)
        {
            var p = ch.root.position; var pp = new Vector2(p.x, p.z);
            loreClock -= dt;
            if (loreClock <= 0)
            {
                loreClock = .5f;
                foreach (var r in Lore.Records)
                {
                    var rp = recordProps.Find(x => x.rec == r); bool near = (r.pos - pp).magnitude < 120;
                    if (near && rp == null) recordProps.Add(BuildRecord(r));
                    else if (!near && rp != null) { Object.Destroy(rp.root); Object.Destroy(rp.holoMat); recordProps.Remove(rp); }
                }
            }
            foreach (var rp in recordProps)
            {
                var r = rp.rec; var at = rp.root.transform.position;
                float d = (r.pos - pp).magnitude; bool level = Mathf.Abs(Sim.FromUnity(at).y - p.y) < 1.6f;
                if (!r.collected && d < 3.5f && level && player.alive && fightArena == null && !SubtitlePlaying) PlayRecord(rp);
                // the recorder's lens signal: a slow blink while unheard, steady (glitching) while it plays, dark after
                bool speaking = subProp == rp && SubtitlePlaying;
                float a = speaking ? .9f : r.collected ? 0 : (Mathf.Repeat(realTime * .8f + r.pos.x * .13f, 1) < .18f ? .9f : .15f);
                if (speaking && Random.value < .08f) a *= .3f;
                rp.holo.SetActive(a > .01f);
                var c = Lore.Person(r.who).tint; c.a = a; Mats.SetColor(rp.holoMat, c);
            }
            // subtitles advance; the transmission screen opens / closes around them
            if (SubtitlePlaying)
            {
                subT += dt; txOpen = Mathf.Min(1, txOpen + dt / .3f);
                if (subT >= SubLineDur) { subT = 0; subLine++; if (subLine < subLines.Length) SoulAudio.Play("ropeReel", p, .25f, .45f, .1f); }
                if (subLine >= subLines.Length) { subLines = null; subProp = null; }
            }
            else txOpen = Mathf.Max(0, txOpen - dt / .25f);
        }

        RecordProp BuildRecord(Lore.Record r)
        {
            var person = Lore.Person(r.who);
            float y = WorldCollision.Height(r.pos.x, r.pos.y, Terrain.Height(r.pos.x, r.pos.y) + 3);
            var root = new GameObject("record " + r.id); root.transform.SetParent(scene.actors, false);
            root.transform.localPosition = Sim.ToUnity(new Vector3(r.pos.x, y, r.pos.y));
            var dev = new GameObject("recorder"); dev.transform.SetParent(root.transform, false);
            dev.AddComponent<MeshFilter>().sharedMesh = RecordModels.Recorder(); dev.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, .2f, .2f, true);
            // no standing hologram any more: the person appears on the transmission screen. In the world only the
            // recorder's lens glows (a small additive dot over it)
            var holo = new GameObject("signal"); holo.transform.SetParent(root.transform, false); holo.transform.localPosition = new Vector3(0, .32f, 0); holo.transform.localScale = Vector3.one * .16f;
            var mat = Mats.Unlit(person.tint, Blend.Add, false, CullMode.Off, false);
            holo.AddComponent<MeshFilter>().sharedMesh = GlowDot(); var mr = holo.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return new RecordProp { rec = r, root = root, holo = holo, holoMat = mat };
        }
        static Mesh glowDot;
        static Mesh GlowDot()
        {
            if (glowDot != null) return glowDot;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); glowDot = g.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(g);
            return glowDot;
        }

        void PlayRecord(RecordProp rp)
        {
            var r = rp.rec; r.collected = true; PartsRecordCheck(r.zone);   // 생존자 기록 보상
            var person = Lore.Person(r.who);
            Subtitles(person.name, r.lines, person.tint, person.build, person.height); subProp = rp;
            SoulAudio.Play("ropeReel", Sim.FromUnity(rp.root.transform.position), .35f, .4f, .1f);
        }
        void Subtitles(string who, string[] lines, Color tint, int build, float height)
        {
            subWho = who; subLines = lines; subLine = 0; subT = 0; subTint = tint; subBuild = build; subHeight = height;
        }

        /// right after a zone purification: the next main-story beat (by purified count), the scientist's hologram
        void StoryBeat()
        {
            int n = Mathf.Clamp(ZoneState.Count, 1, Lore.Beats.Length) - 1;
            if (Lore.beatSeen[n]) return;
            Lore.beatSeen[n] = true;
            Subtitles(Lore.Scientist, Lore.Beats[n], new Color(.9f, 1.6f, 2.2f), 0, 1.75f); subProp = null;
        }

        // ---- transmission screen: a small crackling monitor (static, scanlines, tearing) with the speaker's
        // silhouette, and the line typed out beside it. The silhouette is the RecordModels hologram mesh flattened. ----
        static readonly Dictionary<int, Texture2D> silhouettes = new Dictionary<int, Texture2D>();
        Texture2D noiseTex, scanTex; Color32[] noisePx; float noiseT;

        static Texture2D Silhouette(int build, float height)
        {
            int key = build * 1000 + Mathf.RoundToInt(height * 100);
            if (silhouettes.TryGetValue(key, out var tex)) return tex;
            const int TW = 96, TH = 128; const float span = 2.05f, half = span * TW / TH * .5f;
            var px = new Color32[TW * TH];
            var mesh = RecordModels.Hologram(build, height); var v = mesh.vertices; var tri = mesh.triangles;
            for (int t = 0; t < tri.Length; t += 3)
            {
                Vector2 a = P(v[tri[t]]), b = P(v[tri[t + 1]]), c = P(v[tri[t + 2]]);
                int x0 = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.Min(TW - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
                int y0 = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.Min(TH - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x); if (Mathf.Abs(area) < 1e-5f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var q = new Vector2(x + .5f, y + .5f);
                        float w0 = ((b.x - q.x) * (c.y - q.y) - (b.y - q.y) * (c.x - q.x)) / area;
                        float w1 = ((c.x - q.x) * (a.y - q.y) - (c.y - q.y) * (a.x - q.x)) / area;
                        if (w0 < 0 || w1 < 0 || w0 + w1 > 1) continue;
                        byte shade = (byte)(200 + 55 * Mathf.Clamp01(y / (float)TH));   // a little brighter toward the head
                        px[y * TW + x] = new Color32(shade, shade, shade, 255);
                    }
            }
            // close the hologram mesh's thin scanline gaps so the figure reads as one solid body (the screen adds its own lines)
            var fill = new bool[px.Length];
            for (int x = 0; x < TW; x++)
                for (int y = 2; y < TH - 2; y++)
                    if (px[y * TW + x].a == 0 && (px[(y - 1) * TW + x].a > 0 || px[(y - 2) * TW + x].a > 0) && (px[(y + 1) * TW + x].a > 0 || px[(y + 2) * TW + x].a > 0)) fill[y * TW + x] = true;
            for (int i = 0; i < px.Length; i++) if (fill[i]) px[i] = new Color32(220, 220, 220, 255);
            tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            return silhouettes[key] = tex;
            Vector2 P(Vector3 p) => new Vector2((p.x + half) / (2 * half) * TW, (p.y + .08f) / span * TH);
        }

        void TxTextures()
        {
            if (noiseTex == null)
            {
                noiseTex = new Texture2D(80, 60, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; noisePx = new Color32[80 * 60];
                scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                scanTex.SetPixels32(new[] { new Color32(0, 0, 0, 150), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 60) }); scanTex.Apply();
            }
            if (realTime - noiseT > .045f)
            {
                noiseT = realTime;
                for (int i = 0; i < noisePx.Length; i++) { byte g = (byte)Random.Range(0, 256); noisePx[i] = new Color32(g, g, g, 255); }
                noiseTex.SetPixels32(noisePx); noiseTex.Apply(false);
            }
        }

        void SubtitleGUI(float W, float H)
        {
            if (txOpen <= 0 || subWho == null) return;
            TxTextures();
            bool live = SubtitlePlaying && subLine < subLines.Length;
            float open = Mathf.SmoothStep(0, 1, txOpen);
            // layout: monitor on the left, dialogue box on the right, centred low on screen
            float mw = 170, mh = 216, bw = Mathf.Min(580, W * .5f), gap = 10;
            float x = (W - (mw + gap + bw)) * .5f, y = H - mh - 150;
            var tint = new Color(Mathf.Min(1, subTint.r * .5f), Mathf.Min(1, subTint.g * .5f), Mathf.Min(1, subTint.b * .5f));
            // the monitor turns on like a CRT: a bright line that opens vertically
            float vh = mh * open; var mon = new Rect(x, y + (mh - vh) * .5f, mw, Mathf.Max(2, vh));
            GUI.color = new Color(.02f, .03f, .04f, .92f); GUI.DrawTexture(new Rect(mon.x - 4, mon.y - 4, mon.width + 8, mon.height + 8), white);
            float glitch = Random.value < .07f ? 1 : 0;   // a burst of heavy static now and then
            GUI.color = new Color(tint.r, tint.g, tint.b, .4f + .35f * glitch); GUI.DrawTexture(mon, noiseTex);
            if (open > .6f && glitch == 0)
            {
                // the silhouette, cut into horizontal strips that tear sideways for a frame
                var sil = Silhouette(subBuild, subHeight); int strips = 12;
                var area = new Rect(mon.x + 8, mon.y + 8, mon.width - 16, mon.height - 16);
                float flick = .75f + .25f * Mathf.Sin(realTime * 37) * (Random.value < .2f ? 1 : 0);
                // backlight halo first (the figure stands in front of the glowing static), then the dark body in strips
                GUI.color = new Color(tint.r, tint.g, tint.b, .55f * flick);
                GUI.DrawTexture(new Rect(area.x - 3, area.y - 3, area.width + 6, area.height + 4), sil);
                for (int s = 0; s < strips; s++)
                {
                    float v0 = s / (float)strips, v1 = (s + 1) / (float)strips;
                    float off = Random.value < .05f ? Random.Range(-10f, 10f) : 0;
                    var r = new Rect(area.x + off, area.yMax - area.height * v1, area.width, area.height / strips);
                    GUI.color = new Color(.01f, .02f, .03f, .97f * flick);
                    GUI.DrawTextureWithTexCoords(r, sil, new Rect(0, v0, 1, v1 - v0));
                }
            }
            // scanlines + a rolling bright band
            GUI.color = Color.white; GUI.DrawTextureWithTexCoords(mon, scanTex, new Rect(0, 0, 1, mon.height / 3));
            float band = Mathf.Repeat(realTime * .35f, 1.3f) - .15f;
            if (band > 0 && band < 1) { GUI.color = new Color(tint.r, tint.g, tint.b, .1f); GUI.DrawTexture(new Rect(mon.x, mon.y + mon.height * band, mon.width, 10), white); }
            // REC dot
            if (Mathf.Repeat(realTime, 1) < .6f && open > .9f) { GUI.color = new Color(1, .25f, .2f, .9f); GUI.DrawTexture(new Rect(mon.x + 7, mon.y + 7, 6, 6), white); }
            // dialogue box
            float ba = open;
            var box = new Rect(x + mw + gap, y + mh - 104, bw, 104);
            GUI.color = new Color(0, 0, 0, .62f * ba); GUI.DrawTexture(box, white);
            GUI.color = new Color(tint.r, tint.g, tint.b, .8f * ba); GUI.DrawTexture(new Rect(box.x, box.y, 3, box.height), white);
            GUI.color = new Color(tint.r, tint.g, tint.b, ba);
            GUI.Label(new Rect(box.x + 16, box.y + 10, box.width - 32, 22), subWho, new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold });
            if (live)
            {
                string line = subLines[subLine];
                int shown = Mathf.Clamp(Mathf.FloorToInt((subT - .15f) * 26), 0, line.Length);   // typed out
                float fade = Mathf.Clamp01((SubLineDur - subT) / .3f);
                GUI.color = new Color(1, 1, 1, ba * fade);
                GUI.Label(new Rect(box.x + 16, box.y + 36, box.width - 32, box.height - 44), line.Substring(0, shown) + (shown < line.Length && Mathf.Repeat(realTime * 3, 1) < .5f ? "▌" : ""), new GUIStyle(small) { fontSize = 17, wordWrap = true });
                GUI.color = new Color(.6f, .7f, .78f, ba * .8f);
                GUI.Label(new Rect(box.xMax - 60, box.yMax - 22, 50, 18), $"{subLine + 1}/{subLines.Length}", new GUIStyle(small) { fontSize = 11, alignment = TextAnchor.MiddleRight });
            }
            GUI.color = Color.white;
        }

        // ---- growth screen tab 2: 기록 ----
        Vector2 recordScroll; int recordSel = -1;
        void RecordsTab(Rect body, Event ev)
        {
            var list = new Rect(body.x, body.y, body.width * .38f, body.height);
            var view = new Rect(body.x + body.width * .4f, body.y, body.width * .6f, body.height);
            GUI.color = new Color(0, 0, 0, .35f); GUI.DrawTexture(list, white); GUI.DrawTexture(view, white); GUI.color = Color.white;
            float y = list.y + 10; int total = 0, got = 0;
            for (int z = 0; z < ZoneGraph.Zones.Length; z++)
            {
                bool header = false;
                for (int i = 0; i < Lore.Records.Length; i++)
                {
                    var r = Lore.Records[i]; if (r.zone != z) continue; total++;
                    if (!header) { GUI.color = new Color(.75f, .95f, 1); GUI.Label(new Rect(list.x + 12, y, list.width - 24, 22), $"Z{z} {ZoneGraph.Zones[z].name}", new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Bold }); y += 24; header = true; }
                    var row = new Rect(list.x + 20, y, list.width - 32, 24);
                    if (r.collected)
                    {
                        got++;
                        var person = Lore.Person(r.who);
                        bool sel = recordSel == i, hov = row.Contains(ev.mousePosition);
                        if (sel || hov) { GUI.color = new Color(.3f, .7f, 1, sel ? .35f : .18f); GUI.DrawTexture(row, white); }
                        GUI.color = Color.white; GUI.Label(row, $"{person.name} — {r.lines[0]}", new GUIStyle(small) { fontSize = 13, clipping = TextClipping.Clip });
                        if (ev.type == EventType.MouseDown && ev.button == 0 && hov) { recordSel = i; ev.Use(); }
                    }
                    else { GUI.color = new Color(.5f, .55f, .6f); GUI.Label(row, "— 찾지 못한 기록 —", new GUIStyle(small) { fontSize = 13 }); }
                    y += 26;
                }
            }
            // main story beats seen so far
            y += 10; GUI.color = new Color(.75f, .95f, 1); GUI.Label(new Rect(list.x + 12, y, list.width - 24, 22), "과학자의 메시지", new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Bold }); y += 24;
            for (int b = 0; b < Lore.Beats.Length; b++)
            {
                var row = new Rect(list.x + 20, y, list.width - 32, 24);
                if (Lore.beatSeen[b])
                {
                    bool sel = recordSel == 1000 + b, hov = row.Contains(ev.mousePosition);
                    if (sel || hov) { GUI.color = new Color(.3f, .7f, 1, sel ? .35f : .18f); GUI.DrawTexture(row, white); }
                    GUI.color = Color.white; GUI.Label(row, $"{b + 1}. {Lore.Beats[b][0]}", new GUIStyle(small) { fontSize = 13, clipping = TextClipping.Clip });
                    if (ev.type == EventType.MouseDown && ev.button == 0 && hov) { recordSel = 1000 + b; ev.Use(); }
                }
                else { GUI.color = new Color(.5f, .55f, .6f); GUI.Label(row, $"{b + 1}. —", new GUIStyle(small) { fontSize = 13 }); }
                y += 26;
            }
            GUI.color = new Color(.7f, .78f, .85f); GUI.Label(new Rect(list.x + 12, list.yMax - 26, list.width - 24, 20), $"생존자 기록 {got} / {total}", new GUIStyle(small) { fontSize = 12 });
            // the selected record's full text
            string who = null; string[] lines = null; string role = null;
            if (recordSel >= 1000) { who = Lore.Scientist; lines = Lore.Beats[recordSel - 1000]; }
            else if (recordSel >= 0) { var r = Lore.Records[recordSel]; var person = Lore.Person(r.who); who = person.name; role = person.role; lines = r.lines; }
            if (lines != null)
            {
                float vy = view.y + 18;
                GUI.color = new Color(.55f, .95f, 1); GUI.Label(new Rect(view.x + 20, vy, view.width - 40, 26), who, new GUIStyle(small) { fontSize = 18, fontStyle = FontStyle.Bold }); vy += 28;
                if (role != null) { GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(view.x + 20, vy, view.width - 40, 20), role, new GUIStyle(small) { fontSize = 12 }); vy += 26; }
                GUI.color = Color.white;
                foreach (var l in lines) { GUI.Label(new Rect(view.x + 20, vy, view.width - 40, 48), l, new GUIStyle(small) { fontSize = 15, wordWrap = true }); vy += 46; }
            }
            else { GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(view.x + 20, view.y + 18, view.width - 40, 24), "기록을 고르면 내용이 보입니다.", new GUIStyle(small) { fontSize = 13 }); }
            GUI.color = Color.white;
        }

        /// MCP tests
        public string LoreTest(string recordId)
        {
            foreach (var r in Lore.Records) if (r.id == recordId) { Teleport(new Vector3(r.pos.x, 0, r.pos.y + 2)); for (int i = 0; i < 4; i++) LoreUpdate(.5f); return $"{r.id} collected {r.collected}, subtitle {(SubtitlePlaying ? subWho + ": " + subLines[subLine] : "none")}"; }
            return "no record";
        }
    }

    /// placeholder meshes for the records (Codex 21 replaces the bodies; keep the method names)
    public static class RecordModels
    {
        static Mesh recorder;
        static readonly Dictionary<int, Mesh> holos = new Dictionary<int, Mesh>();
        /// a dented hand recorder / emitter puck on the ground, ~0.35 m
        public static Mesh Recorder()
        {
            if (recorder != null) return recorder;
            var b = new MeshBuilder(401);
            // The crushed corner and offset upper rim expose the rubber seam.
            var rim = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                rim[i] = V(Mathf.Sin(a) * (i == 2 ? .132f : .166f), .074f + (i == 2 ? -.018f : 0), Mathf.Cos(a) * .143f);
            }
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector3 lo = V(rim[i].x * .94f, .016f, rim[i].z * .94f);
                Vector3 next = V(rim[j].x * .94f, .016f, rim[j].z * .94f);
                b.Quad(lo, next, rim[j], rim[i], b.Jitter(0x44494b));
                b.Tri(V(0, .078f, 0), rim[i], rim[j], b.Jitter(0x66695d));
                b.Tri(V(0, .016f, 0), next, lo, MeshBuilder.Hex(0x292b29));
            }
            b.Cylinder(V(0, .076f, 0), .069f, .065f, .014f, 8, MeshBuilder.Hex(0x272c2d));
            b.Cylinder(V(0, .091f, 0), .043f, .037f, .012f, 8, MeshBuilder.Hex(0xa4c6c4));
            for (int i = 0; i < 3; i++)
                b.Box(V(.085f, .080f, -.043f + i * .036f), V(.037f, .008f, .012f), MeshBuilder.Hex(0x282b2b));
            b.Box(V(-.089f, .082f, -.055f), V(.056f, .012f, .043f), MeshBuilder.Hex(0x715140));
            b.Beam(V(-.106f, .068f, .04f), V(-.126f, .189f, .039f), .009f, .007f, 4, MeshBuilder.Hex(0x454748));
            b.Beam(V(-.126f, .189f, .039f), V(-.089f, .239f, .025f), .007f, .004f, 4, MeshBuilder.Hex(0x77766a));
            for (int i = 0; i < 3; i++)
            {
                float a = .6f + i * 2.1f;
                b.Cylinder(V(Mathf.Sin(a) * .119f, .079f, Mathf.Cos(a) * .106f), .008f, .008f, .006f, 4, MeshBuilder.Hex(0x9a9380));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.57f + .3f;
                b.Rock(V(Mathf.Sin(a) * .143f, .017f, Mathf.Cos(a) * .139f), .026f,
                    b.Jitter(0x80796f), .12f, .5f, 0);
            }
            return recorder = b.Build("recorder");
        }
        /// a standing human silhouette of stacked low-poly pieces (build: 0 slim, 1 stocky, 2 child), height in m
        public static Mesh Hologram(int build, float height)
        {
            int key = build * 1000 + Mathf.RoundToInt(height * 100);
            if (holos.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder(402 + key);
            float w = build == 1 ? 1.24f : build == 2 ? .91f : 1;
            float neck = build == 2 ? .815f : .85f;
            // Every part is sliced in the SAME world-Y phase, including hands,
            // boots and luggage. No solid primitive can bridge a scanline gap.
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side == 1 ? .035f : -.025f;
                ScanPart(b, height, V(side * .080f * w, 0, z + .025f), V(side * .080f * w, .055f, z + .025f), V(.040f, 0, .085f), V(.038f, 0, .070f));
                ScanPart(b, height, V(side * .080f * w, .055f, z), V(side * .057f * w, .48f, 0), V(.031f, 0, .037f), V(.050f * w, 0, .059f));
                // Two coat tails leave a visible split above the separated legs.
                ScanPart(b, height, V(side * .076f * w, build == 0 ? .395f : .47f, -.008f), V(side * .057f * w, .57f, 0), V(.063f * w, 0, .078f), V(.058f * w, 0, .070f));
            }
            ScanPart(b, height, V(0, .55f, 0), V(0, .78f, -.018f), V(.110f * w, 0, .074f), V(.139f * w, 0, .078f), 6);
            ScanPart(b, height, V(0, .78f, -.018f), V(0, neck - .018f, -.005f), V(.139f * w, 0, .078f), V(.056f, 0, .050f));
            ScanPart(b, height, V(0, neck - .018f, 0), V(0, neck + .025f, .012f), V(.034f, 0, .034f), V(.037f, 0, .037f));
            float head = build == 2 ? .087f : .070f;
            ScanPart(b, height, V(0, neck, .025f), V(0, .94f, .018f), V(head * .70f, 0, head * .72f), V(head, 0, head), 6);
            ScanPart(b, height, V(0, .94f, .018f), V(0, 1, .011f), V(head, 0, head), V(head * .58f, 0, head * .65f), 6);
            // Left hand at the ear; right hand cups a small radio / satchel.
            ScanPart(b, height, V(-.201f * w, .66f, .025f), V(-.140f * w, .79f, -.012f), V(.032f, 0, .036f), V(.045f, 0, .048f));
            ScanPart(b, height, V(-.201f * w, .66f, .025f), V(-.087f, .90f, .052f), V(.031f, 0, .033f), V(.023f, 0, .029f));
            ScanPart(b, height, V(.197f * w, .63f, .025f), V(.140f * w, .79f, -.012f), V(.032f, 0, .035f), V(.046f, 0, .048f));
            ScanPart(b, height, V(.105f * w, .56f, .122f), V(.197f * w, .63f, .025f), V(.026f, 0, .035f), V(.031f, 0, .034f));
            ScanPart(b, height, V(.070f, .52f, .112f), V(.070f, .65f, .112f), V(.060f, 0, .036f), V(.057f, 0, .033f));
            if (build == 1)
                ScanPart(b, height, V(0, .56f, -.105f), V(0, .79f, -.110f), V(.105f, 0, .058f), V(.098f, 0, .055f));
            return holos[key] = b.Build("hologram");
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // Closed tapered horizontal slices: 12 tris for a four-sided slice,
        // 20 for a six-sided slice. Thin missing bands repeat every .12 metres.
        static void ScanPart(MeshBuilder b, float height, Vector3 from, Vector3 to, Vector3 r0, Vector3 r1, int sides = 4)
        {
            from *= height; to *= height; r0 *= height; r1 *= height;
            const float pitch = .12f, gap = .018f;
            int first = Mathf.FloorToInt(from.y / pitch), last = Mathf.FloorToInt(to.y / pitch);
            for (int band = first; band <= last; band++)
            {
                float low = Mathf.Max(from.y, band * pitch), high = Mathf.Min(to.y, (band + 1) * pitch - gap);
                if (high - low < .0001f) continue;
                float t0 = (low - from.y) / (to.y - from.y), t1 = (high - from.y) / (to.y - from.y);
                Vector3 a = Vector3.Lerp(from, to, t0), c = Vector3.Lerp(from, to, t1);
                Vector3 ra = Vector3.Lerp(r0, r1, t0), rc = Vector3.Lerp(r0, r1, t1);
                var bot = new Vector3[sides]; var top = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides + (sides == 4 ? Mathf.PI / 4 : 0);
                    float x = Mathf.Sin(angle) * (sides == 4 ? 1.41421356f : 1), z = Mathf.Cos(angle) * (sides == 4 ? 1.41421356f : 1);
                    bot[i] = a + V(x * ra.x, 0, z * ra.z); top[i] = c + V(x * rc.x, 0, z * rc.z);
                }
                for (int i = 0; i < sides; i++)
                    b.Quad(bot[i], bot[(i + 1) % sides], top[(i + 1) % sides], top[i], Color.white);
                for (int i = 1; i < sides - 1; i++)
                {
                    b.Tri(top[0], top[i], top[i + 1], Color.white);
                    b.Tri(bot[0], bot[i + 1], bot[i], Color.white);
                }
            }
        }
    }
}
