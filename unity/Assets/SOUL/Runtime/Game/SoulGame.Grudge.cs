using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 원혼 파츠 flow (World/GrudgeParts.cs): the boss purification drops a red part (GrudgeDrop, called by the boss
    // fight); walking over it picks it up; at a pod a panel offers "submit for purification" or "equip" (equip asks
    // twice: it cannot be undone). Pod purification runs on play time; when it finishes the zone greens further
    // (WorldStreamer bloom ×1.8 via GrudgeParts.IsCleansed) and basic parts are paid out.
    // Debug: ` + 7 drops the Z0 part at the player.
    public partial class SoulGame
    {
        class GrudgeDropFx { public int zone; public GameObject go, glow; public Vector3 pos; public float t; }
        readonly List<GrudgeDropFx> grudgeDrops = new List<GrudgeDropFx>();
        Mesh grudgeMesh, grudgeGlowMesh; Material grudgeLit, grudgeGlowMat;
        bool grudgeInit; int grudgeConfirm = -1; float grudgeConfirmT;

        void GrudgeInit()
        {
            grudgeInit = true; GrudgeParts.Reset();
            grudgeMesh = GrudgeParts.Model(out grudgeGlowMesh);
            grudgeLit = Mats.Lit(Color.white, Color.black, .2f, .2f, true);
            grudgeGlowMat = Mats.Unlit(new Color(3.2f, .35f, .2f, 1), Blend.Opaque, true);
            GrudgeParts.Cleansed += z =>
            {
                GainScrap(GrudgeParts.PURIFY_REWARD); open.RebuildAll(ch.root.position);
                Cue($"{ZoneGraph.Zones[z].name} · 포드의 정화가 끝났다 · 기본 부품 +{GrudgeParts.PURIFY_REWARD}", 3.2f);
            };
        }

        /// boss fight → the condensed grudge part falls here (sim position)
        public void GrudgeDrop(int zone, Vector3 pos)
        {
            if (!grudgeInit) GrudgeInit();
            var root = new GameObject("grudge part Z" + zone); root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = grudgeMesh; root.AddComponent<MeshRenderer>().sharedMaterial = grudgeLit;
            var glow = new GameObject("glow"); glow.transform.SetParent(root.transform, false);
            glow.AddComponent<MeshFilter>().sharedMesh = grudgeGlowMesh; glow.AddComponent<MeshRenderer>().sharedMaterial = grudgeGlowMat;
            grudgeDrops.Add(new GrudgeDropFx { zone = zone, go = root, glow = glow, pos = pos });
            vfx.Flash(pos + new Vector3(0, 1, 0), 1.2f);
        }

        /// MCP tests: the editor barely ticks while unfocused
        public void GrudgeTick(float dt) => GrudgeUpdate(dt);
        void GrudgeUpdate(float dt)
        {
            if (!grudgeInit) GrudgeInit();
            var p = ch.root.position;
            if (Input.GetKeyDown(KeyCode.Alpha7) && DebugLayer) GrudgeDrop(0, p + ch.Forward * 2.5f);   // debug: ` + 7
            for (int i = grudgeDrops.Count - 1; i >= 0; i--)
            {
                var d = grudgeDrops[i]; d.t += dt;
                float bob = .35f + Mathf.Sin(d.t * 2.2f) * .12f;
                // resting on the floor under the drop point (pos.y only picks the storey: callers may pass 0 or the floor)
                var at = new Vector3(d.pos.x, WorldCollision.Height(d.pos.x, d.pos.z, d.pos.y + 1.5f) + bob, d.pos.z);
                d.go.transform.localPosition = Sim.ToUnity(at); d.go.transform.localRotation = Sim.YawToUnity(d.t * .6f);
                if (d.t % .5f < dt) vfx.Burst(at + new Vector3(0, .3f, 0), 3, .4f);
                if (Sim.FlatY(d.pos - p).magnitude < 1.6f && player.alive)
                {
                    Object.Destroy(d.go); grudgeDrops.RemoveAt(i);
                    GrudgeParts.carried.Add(d.zone);
                    var def = GrudgeParts.Defs[d.zone];
                    Cue($"붉게 빛나는 부품 · {def.name} — 포드에서 정화하거나 장착할 수 있다", 3.4f);
                }
            }
            GrudgeParts.Tick(dt);
            if (grudgeConfirmT > 0) { grudgeConfirmT -= dt; if (grudgeConfirmT <= 0) grudgeConfirm = -1; }
        }

        void GrudgeGUI(float W, float H)
        {
            if (!grudgeInit) return;
            var pod = NearPod(4.2f);
            if (pod != null && fightArena == null && GrudgeParts.carried.Count > 0)
            {
                float w = 440, h = 64 + GrudgeParts.carried.Count * 74, x = W - w - 28, y = H * .32f;
                GUI.color = new Color(0, 0, 0, .72f); GUI.DrawTexture(new Rect(x, y, w, h), white);
                GUI.color = new Color(1, .55f, .5f); GUI.Label(new Rect(x + 16, y + 10, w - 32, 22), "붉게 빛나는 부품", new GUIStyle(big) { fontSize = 17, alignment = TextAnchor.UpperLeft });
                float yy = y + 40;
                foreach (int z in GrudgeParts.carried.ToArray())
                {
                    var def = GrudgeParts.Defs[z];
                    GUI.color = Color.white; GUI.Label(new Rect(x + 16, yy, w - 32, 20), $"{def.name}  ·  능력: {def.ability}", small);
                    if (GUI.Button(new Rect(x + 16, yy + 24, 190, 30), "포드에 맡겨 정화 (15분)")) { GrudgeParts.Submit(z, pod); Cue($"{def.name}을(를) {pod.name}에 맡겼다 · 정화 중", 2.4f); }
                    bool confirming = grudgeConfirm == z;
                    GUI.color = confirming ? new Color(1, .45f, .4f) : new Color(1, .8f, .78f);
                    if (GUI.Button(new Rect(x + 220, yy + 24, 204, 30), confirming ? "정말 장착? 되돌릴 수 없다" : "장착 (영구)"))
                    {
                        if (!confirming) { grudgeConfirm = z; grudgeConfirmT = 4; }
                        else
                        {
                            grudgeConfirm = -1; GrudgeParts.Equip(z);
                            if (Skills.GrudgeSkill[z] != null) After(1.4f, () => GrantSkill(Skills.GrudgeSkill[z]));
                            vfx.Flash(Sim.FlatY(ch.root.position, 1.4f), 1.4f); cam.Shake(.08f); face.Preview("blank", 1.2f);
                            Cue($"{def.name} 장착 · {def.ability}", 3);
                        }
                    }
                    yy += 74;
                }
                GUI.color = Color.white;
            }
            // pods with a part being purified
            float py = H * .32f - 26;
            foreach (var pd in GrudgeParts.pending)
            {
                GUI.color = new Color(1, .7f, .65f, .9f);
                int m = Mathf.CeilToInt(pd.left / 60);
                GUI.Label(new Rect(W - 380, py, 360, 20), $"정화 중 · {GrudgeParts.Defs[pd.zone].name} @ {pd.pod.name} · {m}분", small);
                py -= 20;
            }
            if (debugHud) { GUI.color = Color.white; GUI.Label(new Rect(20, 142, 520, 20), $"원혼 장착 {GrudgeParts.Count} · 엔딩 루트 {GrudgeParts.Route} · ` + 7 원혼 파츠 드롭", small); }
            GUI.color = Color.white;
        }
    }
}
