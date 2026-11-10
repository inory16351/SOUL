using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 슬롯 파츠 획득처의 월드 배치 (기획서/스킬_슬롯_카탈로그.md 7-4 · 7-5 · 9 · 10절, 2026-11-10). Zones built so far: Z0, Z6.
    //  - 스킬 게이트: an obstacle opened by OWNING a skill (not equipping it), interact (G) next to it. No guide text:
    //    the obstacle's look tells what opens it (gates-read-from-terrain); F1 shows the requirement.
    //    Behind: 존 금고 (needs the zone's key chip too = both elites killed), 지름길 (a linked pair: G teleports),
    //    은닉처 (basic parts / repair kit max +1), 숨은 상인.
    //  - 모듈 숨은 장소: the same, opened by a movement module (Parts bit).
    //  - 정화 보상: when the zone is purified, a part appears at a spot (a light beam marks it for a while).
    //  - 생존자 기록 보상: all of a zone's records collected -> the survivor's gear.
    //  - 존별 상인: Merchants (Z0 street, Z6 sunken village behind the 흡입 gate).
    public partial class SoulGame
    {
        public class Gate
        {
            public string id, look, needSkill, reward; public int zone, needPart = -1, pair = -1; public Vector2 want; public Vector3 pos;
            public bool open, vault; public GameObject go; public Material glow;
        }
        public readonly List<Gate> gates = new List<Gate>
        {
            // ---- Z0 잿빛 폐허 ----
            new Gate { id = "z0_vault",  zone = 0, want = new Vector2(250, -160),  look = "elevator", needSkill = "shockpalm", vault = true, reward = "skill:corebus" },
            new Gate { id = "z0_short_a", zone = 0, want = new Vector2(60, -60),   look = "crystal",  needSkill = "wave", pair = 2, reward = "shortcut" },
            new Gate { id = "z0_short_b", zone = 0, want = new Vector2(330, -40),  look = "crystal",  needSkill = "wave", pair = 1, reward = "shortcut" },
            new Gate { id = "z0_stash_a", zone = 0, want = new Vector2(-250, -180), look = "glass",   needSkill = "screech", reward = "scrap:80" },
            new Gate { id = "z0_stash_b", zone = 0, want = new Vector2(-120, 150),  look = "mound",   needSkill = "tremor", reward = "kitmax" },
            new Gate { id = "z0_hidden",  zone = 0, want = new Vector2(-60, 230),   look = "ledge",   needPart = Parts.Rope, reward = "skill:magnet" },
            // ---- Z6 독성 늪 ----
            new Gate { id = "z6_vault",   zone = 6, want = new Vector2(1180, 640),  look = "pump",    needSkill = "shockpalm", vault = true, reward = "skill:ganglion" },
            new Gate { id = "z6_short_a", zone = 6, want = new Vector2(1320, 560),  look = "lever",   needSkill = "harpoon", pair = 8, reward = "shortcut" },
            new Gate { id = "z6_short_b", zone = 6, want = new Vector2(1240, 820),  look = "lever",   needSkill = "harpoon", pair = 7, reward = "shortcut" },
            new Gate { id = "z6_stash_a", zone = 6, want = new Vector2(1100, 600),  look = "glass",   needSkill = "screech", reward = "scrap:150" },
            new Gate { id = "z6_stash_b", zone = 6, want = new Vector2(1000, 840),  look = "lock",    needSkill = "acid", reward = "kitmax" },
            new Gate { id = "z6_merchant", zone = 6, want = new Vector2(1210, 760), look = "debris",  needSkill = "suck", reward = "merchant:6" },
            new Gate { id = "z6_hidden",  zone = 6, want = new Vector2(980, 700),   look = "tank",    needPart = Parts.Magnet, reward = "skill:gripclaw" },
        };
        /// 정화 보상 per zone (part, spot); 생존자 기록 보상 per zone
        static readonly (string part, Vector2 at)[] PurifyReward =
        {
            ("ashcloak", new Vector2(150, 180)), ("relay", Vector2.zero), ("echolens", Vector2.zero), ("scraparmor", Vector2.zero),
            ("torch", Vector2.zero), ("rootnet", Vector2.zero), ("webfoot", new Vector2(1150, 900)), (null, Vector2.zero),
        };
        static readonly string[] RecordReward = { "repair", "net", "nightvision", "bulwark", "mine", "smoke", "reboot", "walker" };
        /// the elites of a zone (key chip = all of them killed)
        static readonly string[][] ZoneElites = { new[] { "houndAlpha", "executor" }, null, null, null, null, null, new[] { "shellHost", "giantDredger" }, null };
        readonly HashSet<string> elitesDown = new HashSet<string>(); readonly bool[] keyChip = new bool[8];
        public int kitMaxBonus; bool partsWorldHooked; float beamUntil; Vector3 beamAt;

        static Vector3 FreeSpot(int zone, Vector2 want)
        {
            for (float r = 0; r < 60; r += 2)
            {
                int n = Mathf.Max(1, (int)(r * .7f));
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2 / n, x = want.x + Mathf.Cos(a) * r, z = want.y + Mathf.Sin(a) * r;
                    if (ZoneRoutes.Dist(x, z) > 10 && Territories.Open(zone, x, z)) return new Vector3(x, Terrain.Height(x, z), z);
                }
            }
            return new Vector3(want.x, Terrain.Height(want.x, want.y), want.y);
        }

        void PartsWorldHook()
        {
            partsWorldHooked = true;
            foreach (var g in gates) g.pos = FreeSpot(g.zone, g.want);
            ZoneState.Changed += z =>
            {
                if (!ZoneState.Purified(z) || z >= PurifyReward.Length || PurifyReward[z].part == null || PurifyReward[z].at == Vector2.zero) return;
                var at = FreeSpot(z, PurifyReward[z].at); var id = PurifyReward[z].part;
                After(9, () => { if (!Skills.Has(id)) { SpawnDrop("skill:" + id, at + Vector3.up * .3f); beamAt = at; beamUntil = realTime + 90; } });
            };
        }
        /// elite kills -> the zone's key chip (PartsOnKill)
        void PartsEliteKilled(Enemy e)
        {
            if (e.def.eliteName == null) return;
            elitesDown.Add(e.type);
            for (int z = 0; z < ZoneElites.Length; z++)
            {
                if (ZoneElites[z] == null || keyChip[z]) continue;
                bool all = true; foreach (var t in ZoneElites[z]) if (!elitesDown.Contains(t)) all = false;
                if (all) { keyChip[z] = true; After(2, () => Cue("열쇠 칩을 얻었다", 2)); }
            }
        }
        /// survivor records: all of a zone's records -> the zone's survivor gear
        void PartsRecordCheck(int zone)
        {
            if (zone < 0 || zone >= RecordReward.Length || Skills.Has(RecordReward[zone])) return;
            int total = 0, got = 0; foreach (var r in Lore.Records) if (r.zone == zone) { total++; if (r.collected) got++; }
            if (total > 0 && got >= total) After(4, () => GrantSkill(RecordReward[zone]));
        }

        // ---------- gates ----------
        Gate NearGate(float r = 3.2f) { foreach (var g in gates) if (g.go != null && Sim.FlatY(g.pos - ch.root.position).magnitude < r) return g; return null; }
        bool GateNeedMet(Gate g) => (g.needSkill == null || Skills.Has(g.needSkill)) && (g.needPart < 0 || Parts.Has(g.needPart)) && (!g.vault || keyChip[g.zone]);
        /// RestAtPod (G): a gate next to the player -> open it / use the shortcut. true = the key was used here
        bool GateInteract()
        {
            var g = NearGate(); if (g == null || !player.alive) return false;
            if (g.open)
            {
                if (g.reward == "shortcut" && g.pair >= 0 && gates[g.pair].open) { Teleport(gates[g.pair].pos + Vector3.up * .1f + Sim.Forward(0) * 1.8f); vfx.Flash(ch.root.position + Vector3.up, 1); return true; }
                return false;
            }
            if (!GateNeedMet(g)) { if (debugHud) Cue(GateNeedText(g), 1.6f); return true; }
            OpenGate(g); return true;
        }
        string GateNeedText(Gate g) => "[F1] 필요: " + (g.needSkill != null ? Skills.Def(g.needSkill)?.name : "") + (g.needPart >= 0 ? Parts.Names[g.needPart] : "") + (g.vault ? (keyChip[g.zone] ? "" : " + 열쇠 칩") : "");
        void OpenGate(Gate g)
        {
            g.open = true;
            var c = g.pos + Vector3.up;
            vfx.Flash(c, 1.4f); vfx.Sparks(c, 24, 1, g.glow ? CYAN : WHITE); cam.Shake(.06f); SoulAudio.Play("pickup", g.pos);
            if (g.go) { Object.Destroy(g.go); g.go = null; BuildGate(g); }
            if (g.reward.StartsWith("skill:")) SpawnDrop(g.reward, g.pos + Sim.Forward(0) * 1.2f + Vector3.up * .3f);
            else if (g.reward.StartsWith("scrap:")) pickups.Burst(c, int.Parse(g.reward.Substring(6)) / 5, 5);
            else if (g.reward == "kitmax") { kitMaxBonus++; kits++; Cue("수리 키트를 하나 더 들 수 있다", 2); }
            else if (g.reward.StartsWith("merchant:")) { revealedMerchants.Add(int.Parse(g.reward.Substring(9))); Cue("뱃길이 열렸다", 1.6f); }
            else if (g.reward == "shortcut" && g.pair >= 0 && !gates[g.pair].open) { gates[g.pair].open = true; if (gates[g.pair].go) { Object.Destroy(gates[g.pair].go); gates[g.pair].go = null; } }
        }
        public readonly HashSet<int> revealedMerchants = new HashSet<int>();

        void PartsWorldUpdate(float dt)
        {
            if (!partsWorldHooked) PartsWorldHook();
            var p = ch.root.position;
            foreach (var g in gates)
            {
                bool near = Sim.FlatY(g.pos - p).magnitude < 110;
                if (near && g.go == null) BuildGate(g);
                else if (!near && g.go != null) { Object.Destroy(g.go); g.go = null; }
                if (g.glow != null && !g.open) Mats.SetColor(g.glow, GlowOf(g) * (.75f + .25f * Mathf.Sin(realTime * 2.2f + g.want.x)));
            }
            if (beamUntil > realTime && Random.value < dt * 8) vfx.Puff(beamAt + Vector3.up * Random.Range(.5f, 6f), new Color(.8f, 1.6f, 2.4f), .6f, 1.2f, Vector3.up * 1.5f);
        }
        static Color GlowOf(Gate g) => g.look switch
        {
            "crystal" => new Color(.6f, 2.2f, 3f), "elevator" or "pump" => new Color(2.6f, 2.4f, .5f) * .5f, "glass" => new Color(1.6f, 2f, 2.4f) * .5f,
            "lock" => new Color(1.2f, 1.6f, .4f) * .5f, "lever" => new Color(2.4f, 1.4f, .5f) * .6f, _ => new Color(.6f, .6f, .6f) * .3f,
        };
        /// the obstacle prop (blocky, ~2 m). Open gates leave a short, broken / switched-on version.
        void BuildGate(Gate g)
        {
            var root = new GameObject("gate " + g.id); root.transform.SetParent(scene.actors, false);
            root.transform.localPosition = Sim.ToUnity(g.pos);
            var metal = Mats.Lit(new Color(.28f, .27f, .26f), Color.black, .5f, .35f, false);
            var rust = Mats.Lit(new Color(.42f, .26f, .17f), Color.black, .2f, .2f, false);
            var dirt = Mats.Lit(new Color(.3f, .26f, .22f), Color.black, 0, .1f, false);
            g.glow = Mats.Unlit(GlowOf(g), Blend.Opaque);
            void Box(Vector3 at, Vector3 size, Material m, PrimitiveType t = PrimitiveType.Cube, float yaw = 0)
            {
                var o = GameObject.CreatePrimitive(t); Object.Destroy(o.GetComponent<Collider>()); o.transform.SetParent(root.transform, false);
                o.transform.localPosition = Sim.ToUnity(at); o.transform.localScale = size; o.transform.localRotation = Quaternion.Euler(0, yaw, 0); o.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            bool o2 = g.open;
            switch (g.look)
            {
                case "elevator": Box(new Vector3(0, 1.4f, 0), new Vector3(2.2f, 2.8f, 2.2f), metal); Box(new Vector3(0, 1.3f, 1.12f), new Vector3(1.4f, 2.2f, .06f), o2 ? metal : rust); Box(new Vector3(.9f, 1.4f, 1.15f), new Vector3(.18f, .18f, .05f), g.glow); break;
                case "pump": Box(new Vector3(0, .8f, 0), new Vector3(2.4f, 1.6f, 1.6f), rust); Box(new Vector3(0, 1.9f, 0), new Vector3(.5f, 1.2f, .5f), metal, PrimitiveType.Cylinder); Box(new Vector3(.9f, 1.2f, .82f), new Vector3(.2f, .2f, .05f), g.glow); break;
                case "crystal": Box(new Vector3(0, .6f, 0), new Vector3(.8f, 1.2f, .8f), metal); if (!o2) Box(new Vector3(0, 1.6f, 0), new Vector3(.45f, .9f, .45f), g.glow, PrimitiveType.Cube, 45); else Box(new Vector3(0, 1.3f, 0), new Vector3(.3f, .3f, .3f), Mats.Unlit(new Color(.4f, 2f, 1f), Blend.Opaque)); break;
                case "glass": if (!o2) { Box(new Vector3(0, 1.3f, 0), new Vector3(3, 2.6f, .12f), Mats.Unlit(new Color(.5f, .7f, .8f, .35f), Blend.Alpha)); Box(new Vector3(0, 1.3f, 0), new Vector3(3.1f, 2.7f, .06f), g.glow); } Box(new Vector3(0, .1f, 0), new Vector3(3.2f, .2f, .5f), metal); break;
                case "mound": if (!o2) Box(new Vector3(0, .4f, 0), new Vector3(3f, .9f, 3f), dirt, PrimitiveType.Sphere); else Box(new Vector3(0, .1f, 0), new Vector3(3f, .2f, 3f), dirt, PrimitiveType.Sphere); break;
                case "ledge": Box(new Vector3(0, 1.4f, 0), new Vector3(3, 2.8f, 3), dirt); Box(new Vector3(0, 2.9f, 0), new Vector3(.3f, .3f, .3f), Mats.Unlit(new Color(.8f, 1.8f, 2.4f), Blend.Opaque)); break;
                case "lever": Box(new Vector3(0, .5f, 0), new Vector3(.6f, 1, .6f), metal); Box(new Vector3(0, 1.3f, 0), new Vector3(.12f, .9f, .12f), rust, PrimitiveType.Cube, 0); Box(new Vector3(0, 1.8f, 0), new Vector3(.25f, .25f, .25f), g.glow, PrimitiveType.Sphere); break;
                case "lock": Box(new Vector3(0, 1.2f, 0), new Vector3(2.4f, 2.4f, .4f), rust); if (!o2) Box(new Vector3(0, 1.1f, .3f), new Vector3(.5f, .6f, .25f), g.glow); break;
                case "debris": if (!o2) { Box(new Vector3(-.6f, .6f, 0), new Vector3(1.6f, 1.2f, 1.4f), rust, PrimitiveType.Cube, 20); Box(new Vector3(.7f, .5f, .3f), new Vector3(1.4f, 1f, 1.2f), dirt, PrimitiveType.Sphere); Box(new Vector3(0, 1.1f, -.2f), new Vector3(1.2f, .6f, 1f), metal, PrimitiveType.Cube, -15); } break;
                case "tank": Box(new Vector3(0, 2, 0), new Vector3(3, 4, 3), rust, PrimitiveType.Cylinder); Box(new Vector3(0, 4.1f, 0), new Vector3(.3f, .3f, .3f), Mats.Unlit(new Color(.8f, 1.8f, 2.4f), Blend.Opaque)); break;
            }
            g.go = root;
        }
        void PartsWorldGUI(float W, float H)
        {
            var g = NearGate(); if (g == null) return;
            if (g.open && g.reward == "shortcut" && g.pair >= 0 && gates[g.pair].open) { GUI.color = new Color(.7f, .9f, 1); GUI.Label(new Rect(0, H * .62f, W, 24), $"{KeyBinds.Name(KeyBinds.Act.Rest)}  지름길", new GUIStyle(big) { fontSize = 17 }); }
            else if (!g.open && GateNeedMet(g)) { GUI.color = new Color(.7f, .9f, 1); GUI.Label(new Rect(0, H * .62f, W, 24), $"{KeyBinds.Name(KeyBinds.Act.Rest)}  연다", new GUIStyle(big) { fontSize = 17 }); }
            else if (!g.open && debugHud) { GUI.color = new Color(1, .8f, .5f); GUI.Label(new Rect(0, H * .62f, W, 24), GateNeedText(g), new GUIStyle(big) { fontSize = 15 }); }
            GUI.color = Color.white;
        }
        /// MCP tests
        public string GateList() { var sb = new System.Text.StringBuilder(); foreach (var g in gates) sb.Append($"{g.id} {(g.open ? "open" : "shut")} need {g.needSkill}{(g.needPart >= 0 ? "/part" + g.needPart : "")} at {g.pos.x:F0},{g.pos.z:F0}\n"); return sb.ToString(); }
        public string GateOpenForTest(string id) { var g = gates.Find(x => x.id == id); if (g == null) return "none"; Teleport(g.pos + Sim.Forward(0) * 2); return GateInteract() ? (g.open ? "opened" : "blocked") : "not near"; }
        public void GiveKeyChip(int zone) { keyChip[zone] = true; }
    }
}
