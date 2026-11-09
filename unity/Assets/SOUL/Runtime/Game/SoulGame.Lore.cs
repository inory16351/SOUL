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
        // subtitles
        string subWho; string[] subLines; int subLine; float subT, subLineDur = 3.4f; RecordProp subProp; GameObject beatHolo; Material beatHoloMat;
        public bool SubtitlePlaying => subLines != null;

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
                // hologram: shown while it speaks (flicker), a faint idle shimmer while uncollected, gone after
                bool speaking = subProp == rp && SubtitlePlaying;
                float a = speaking ? .55f : r.collected ? 0 : .18f + .08f * Mathf.Sin(realTime * 2 + r.pos.x);
                if (Random.value < (speaking ? .08f : .03f)) a *= .2f;   // glitch
                rp.holo.SetActive(a > .01f);
                var c = Lore.Person(r.who).tint; c.a = a; Mats.SetColor(rp.holoMat, c);
                if (speaking) rp.holo.transform.localPosition = new Vector3(Random.value < .05f ? .04f : 0, 0, 0);
            }
            // subtitles advance
            if (SubtitlePlaying)
            {
                subT += dt;
                if (subT >= subLineDur) { subT = 0; subLine++; if (subLine < subLines.Length) SoulAudio.Play("ropeReel", p, .25f, .45f, .1f); }
                if (subLine >= subLines.Length) { subLines = null; subProp = null; if (beatHolo) beatHolo.SetActive(false); }
            }
            if (beatHolo && beatHolo.activeSelf)
            {
                var c = new Color(.9f, 1.6f, 2.2f, .5f * (Random.value < .06f ? .2f : 1)); Mats.SetColor(beatHoloMat, c);
            }
        }

        RecordProp BuildRecord(Lore.Record r)
        {
            var person = Lore.Person(r.who);
            float y = WorldCollision.Height(r.pos.x, r.pos.y, Terrain.Height(r.pos.x, r.pos.y) + 3);
            var root = new GameObject("record " + r.id); root.transform.SetParent(scene.actors, false);
            root.transform.localPosition = Sim.ToUnity(new Vector3(r.pos.x, y, r.pos.y));
            var dev = new GameObject("recorder"); dev.transform.SetParent(root.transform, false);
            dev.AddComponent<MeshFilter>().sharedMesh = RecordModels.Recorder(); dev.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, .2f, .2f, true);
            var holo = new GameObject("hologram"); holo.transform.SetParent(root.transform, false);
            var mat = Mats.Unlit(person.tint, Blend.Add, false, CullMode.Off, false);
            holo.AddComponent<MeshFilter>().sharedMesh = RecordModels.Hologram(person.build, person.height); var mr = holo.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            return new RecordProp { rec = r, root = root, holo = holo, holoMat = mat };
        }

        void PlayRecord(RecordProp rp)
        {
            var r = rp.rec; r.collected = true;
            var person = Lore.Person(r.who);
            Subtitles(person.name, r.lines); subProp = rp;
            SoulAudio.Play("ropeReel", Sim.FromUnity(rp.root.transform.position), .35f, .4f, .1f);
        }
        void Subtitles(string who, string[] lines) { subWho = who; subLines = lines; subLine = 0; subT = 0; }

        /// right after a zone purification: the next main-story beat (by purified count), the scientist's hologram
        void StoryBeat()
        {
            int n = Mathf.Clamp(ZoneState.Count, 1, Lore.Beats.Length) - 1;
            if (Lore.beatSeen[n]) return;
            Lore.beatSeen[n] = true;
            if (beatHolo == null)
            {
                beatHolo = new GameObject("scientist hologram"); beatHolo.transform.SetParent(scene.actors, false);
                beatHoloMat = Mats.Unlit(new Color(.9f, 1.6f, 2.2f, .5f), Blend.Add, false, CullMode.Off, false);
                beatHolo.AddComponent<MeshFilter>().sharedMesh = RecordModels.Hologram(0, 1.75f); var mr = beatHolo.AddComponent<MeshRenderer>(); mr.sharedMaterial = beatHoloMat; mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            var at = ch.root.position + Sim.Forward(ch.yaw) * 2.5f; at.y = WorldCollision.Floor(at.x, at.z, ch.root.position.y + .5f, 1);
            beatHolo.transform.localPosition = Sim.ToUnity(at); beatHolo.transform.localRotation = Sim.YawToUnity(ch.yaw + Mathf.PI);
            beatHolo.SetActive(true);
            Subtitles(Lore.Scientist, Lore.Beats[n]); subProp = null;
        }

        void SubtitleGUI(float W, float H)
        {
            if (!SubtitlePlaying || subLine >= subLines.Length) return;
            float a = Mathf.Clamp01(subT / .25f) * Mathf.Clamp01((subLineDur - subT) / .3f);
            float y = H * .74f;
            GUI.color = new Color(0, 0, 0, .55f * a); GUI.DrawTexture(new Rect(W * .2f, y - 6, W * .6f, 58), white);
            GUI.color = new Color(.55f, .95f, 1, a); GUI.Label(new Rect(0, y, W, 20), subWho, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold });
            GUI.color = new Color(1, 1, 1, a); GUI.Label(new Rect(W * .2f, y + 20, W * .6f, 30), subLines[subLine], new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 17, wordWrap = true });
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
            b.Cylinder(new Vector3(0, 0, 0), .2f, .17f, .08f, 8, MeshBuilder.Hex(0x3e4246));
            b.Box(new Vector3(.05f, .11f, 0), new Vector3(.16f, .06f, .1f), MeshBuilder.Hex(0x575b52));
            b.Beam(new Vector3(-.08f, .08f, .02f), new Vector3(-.12f, .34f, .04f), .008f, .006f, 3, MeshBuilder.Hex(0x2e3033));
            b.Disc(new Vector3(0, .085f, 0), 0, .06f, 8, MeshBuilder.Hex(0x7fe8ff));
            return recorder = b.Build("recorder");
        }
        /// a standing human silhouette of stacked low-poly pieces (build: 0 slim, 1 stocky, 2 child), height in m
        public static Mesh Hologram(int build, float height)
        {
            int key = build * 1000 + Mathf.RoundToInt(height * 100);
            if (holos.TryGetValue(key, out var m)) return m;
            var b = new MeshBuilder(402 + key); float s = height / 1.7f, w = build == 1 ? 1.25f : build == 2 ? .9f : 1;
            var c = Color.white;
            b.Box(new Vector3(0, 1.18f * s, 0), new Vector3(.4f * w * s, .55f * s, .22f * s), c);         // torso
            b.Rock(new Vector3(0, 1.58f * s, 0), .13f * s, c, .1f, 1.1f, 1);                           // head
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .1f * w * s, .9f * s, 0), new Vector3(side * .12f * w * s, .05f * s, .02f), .06f * s * w, .05f * s, 5, c);   // legs
                b.Beam(new Vector3(side * .23f * w * s, 1.42f * s, 0), new Vector3(side * .3f * w * s, .92f * s, .1f * s), .045f * s * w, .04f * s, 5, c);   // arms
            }
            return holos[key] = b.Build("hologram");
        }
    }
}
