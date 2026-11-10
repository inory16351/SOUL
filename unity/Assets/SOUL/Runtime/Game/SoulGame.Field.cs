using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Z0 곁가지 활동 (기획서/Z0_퀘스트_동선.md, 사용자 결정 2026-11-09):
    //  - 특이 개체: field members roll 7 % on spawn to be a strengthened variant (x1.3 size, x2.2 HP, x1.3 damage,
    //    magenta eyes). Drop 25 scrap + 2 붉은 조각. Re-rolled whenever the territory respawns (pod rest).
    //  - 잠긴 보급고: a sealed container beside every turret territory; its lock glows red until that territory's
    //    members are all dead, then green; walk up to open: 40 scrap + 1 조각.
    //  - 사냥당하는 유기체 구하기: kill a hound while it is chasing a 재 쥐 and the rat runs for its burrow; two rats
    //    safely home and the burrow gives 30 scrap + 1 조각, and a wisp flock rises and drifts toward the nearest record
    //    you have not heard yet.
    //  - (2026-11-10) the 붉은 조각 currency is removed: those drops are extra basic parts; normal mobs very rarely drop a slot skill.
    //  - 숨은 상인: a gentle machine hiding in the ruined streets. G to trade (basic parts): 수리 키트 (X heals 45 %), and three
    //    one-time body parts (+20 max HP, +10 % damage, dash cooldown -15 %).
    // Models are PLACEHOLDERS (Claude): Codex job 21 replaces FieldPropModels.
    public partial class SoulGame
    {
        public int kits;
        // 종별 드롭 (카탈로그 7-4, 2026-11-10): each normal species carries ONE part of its own body, 0.5 % per kill, fixed
        // (no bad-luck protection), only while it is not owned. Types not in the game yet are listed under their planned keys.
        public const float SPECIES_DROP = .005f;
        public static readonly Dictionary<string, string> SpeciesPart = new Dictionary<string, string>
        {
            { "crawler", "lightframe" },     // Z0 스크랩 하운드 -> 가벼운 프레임
            { "sentry", "aimlens" },         // Z0 녹슨 파수꾼 -> 조준 렌즈
            { "shockDrone", "decoder" },     // Z1 감전 드론 (자리표시) -> 신호 해독기
            { "glassBat", "glidecloth" },    // Z2 유리 박쥐 -> 날개막 천
            { "scrapDigger", "vibroblade" }, // Z3 고철 굴착기 -> 진동 칼날
            { "assemblyDrone", "sparkplug" },// Z4 조립 드론 (자리표시) -> 점화 플러그
            { "fly", "compound" },           // Z5 숲 날벌레 -> 겹눈 렌즈
            { "thornVine", "thornshell" },   // Z5 가시 덩굴 (자리표시) -> 가시 껍질
            { "swarmBug", "swarmnest" },     // Z6 떼벌레 -> 떼벌레 둥지
        };
        bool perkPlating, perkBlade, perkCoil;
        int KIT_MAX => 3 + kitMaxBonus; const float KIT_HEAL = .45f;   // 은닉처 rewards raise the max (SoulGame.PartsWorld)
        float kitReady;
        // locked caches
        class Depot { public Territory t; public Vector3 pos; public GameObject go; public Material lockMat; public bool open; }
        readonly List<Depot> depots = new List<Depot>();
        // burrows
        readonly Dictionary<Territory, Vector3> burrows = new Dictionary<Territory, Vector3>();
        readonly Dictionary<Territory, int> savedHome = new Dictionary<Territory, int>();
        readonly Dictionary<Territory, GameObject> burrowGo = new Dictionary<Territory, GameObject>();
        readonly HashSet<Territory> burrowPaid = new HashSet<Territory>();
        // merchant
        Vector3? merchantPos; GameObject merchantGo, merchantGo6; bool shopOpen, shopPausedBefore; string shopNote = ""; int shopZone;
        bool fieldHooked;

        void FieldHooks()
        {
            fieldHooked = true;
            critters.onBurrow = c => { if (c.home is Territory t) { savedHome.TryGetValue(t, out int n); savedHome[t] = ++n; if (n >= 2 && !burrowPaid.Contains(t)) BurrowReward(t); } };
        }

        // ---------- specials / drops (called from Ecology) ----------
        void MaybeSpecial(Enemy e) { if (e.def.eliteName == null && Random.value < .07f) e.MakeSpecial(); }
        void FieldKill(Enemy e)
        {
            // a hound killed mid-hunt: its prey runs home
            if (hunting.TryGetValue(e, out var prey) && prey != null && prey.alive && prey.home is Territory pt) { prey.saved = true; prey.burrow = BurrowOf(pt); }
            // 2026-11-10: the 붉은 조각 currency is gone; what used to drop as shards drops as extra basic parts (5 each)
            int shard = e.def.eliteName != null ? 5 : e.special ? 2 : e.type == "sentry" ? 1 : Random.value < .1f ? 1 : 0;
            if (shard > 0) pickups.Burst(e.Center(), shard, 5);
            // 종별 드롭: the species' own part, 0.5 %, only while unowned
            if (e.def.eliteName == null && e.def.bossName == null && SpeciesPart.TryGetValue(e.type, out var sp) && !Skills.Has(sp) && Random.value < SPECIES_DROP) SpawnDrop("skill:" + sp, e.pos);
            if (e.special) pickups.Burst(e.Center(), 5, 5);
        }

        // ---------- burrows ----------
        Vector3 BurrowOf(Territory t)
        {
            if (burrows.TryGetValue(t, out var b)) return b;
            var at = Territories.PointIn(t, .9f, new System.Random(t.name.GetHashCode())) ?? new Vector3(t.c.x, 0, t.c.y);
            at.y = Terrain.Height(at.x, at.z); burrows[t] = at;
            var go = new GameObject("burrow"); go.transform.SetParent(scene.actors, false); go.transform.localPosition = Sim.ToUnity(at + new Vector3(0, .02f, 0));
            go.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.Burrow(); go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, 0, .1f, true);
            burrowGo[t] = go;
            return at;
        }
        void BurrowReward(Territory t)
        {
            burrowPaid.Add(t); var at = BurrowOf(t);
            pickups.Burst(at, 6, 5); pickups.Burst(at + new Vector3(0, .6f, 0), 1, 5);
            vfx.Flash(at + Vector3.up, 1); SoulAudio.Play("shrine", at, .6f, 1.3f);
            // the rescued creatures lead on: a wisp flock drifts toward the nearest record not heard yet
            Lore.Record best = null; float bd = 1e9f;
            foreach (var r in Lore.Records) { if (r.collected || r.zone != ZoneWorld.At(at.x, at.z)) continue; float d = (r.pos - new Vector2(at.x, at.z)).magnitude; if (d < bd) { bd = d; best = r; } }
            if (best != null) FlockToward(at, new Vector3(best.pos.x, 0, best.pos.y), Mathf.Min(bd, 45));
        }

        // ---------- locked caches ----------
        void DepotsUpdate(Vector3 p)
        {
            foreach (var t in Territories.All)
            {
                if (t.kind != "turret" || !t.placed || depots.Exists(d => d.t == t)) continue;
                var at = Territories.PointIn(t, 1.4f, new System.Random(t.name.GetHashCode() + 3)) ?? new Vector3(t.c.x + 6, 0, t.c.y);
                at.y = Terrain.Height(at.x, at.z); depots.Add(new Depot { t = t, pos = at });
            }
            foreach (var d in depots)
            {
                bool near = Sim.FlatY(d.pos - p).magnitude < 110;
                if (near && d.go == null && !d.open)
                {
                    d.go = new GameObject("locked depot"); d.go.transform.SetParent(scene.actors, false);
                    d.go.transform.localPosition = Sim.ToUnity(d.pos); d.go.transform.localRotation = Sim.YawToUnity(d.t.name.Length * .7f); d.go.transform.localScale = Vector3.one * 1.5f;
                    var m = Mats.Lit(Color.white, Color.black, .2f, .2f, true); m.SetFloat("_Cutaway", 1);
                    d.go.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.Depot(); d.go.AddComponent<MeshRenderer>().sharedMaterial = m;
                    var l = new GameObject("lock"); l.transform.SetParent(d.go.transform, false);
                    d.lockMat = Mats.Unlit(new Color(2.4f, .3f, .2f, 1), Blend.Opaque);
                    l.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.DepotLock(); l.AddComponent<MeshRenderer>().sharedMaterial = d.lockMat;
                }
                else if (!near && d.go != null) { Object.Destroy(d.go); d.go = null; }
                if (d.go == null || d.open) continue;
                bool unlocked = d.t.dead != null && System.Array.TrueForAll(d.t.dead, x => x);
                Mats.SetColor(d.lockMat, unlocked ? new Color(.3f, 2.2f, .6f, 1) : new Color(2.4f, .3f, .2f, 1) * (.8f + .2f * Mathf.Sin(realTime * 4)));
                if (unlocked && Sim.FlatY(d.pos - p).magnitude < 2.6f && Mathf.Abs(d.pos.y - p.y) < 1.5f)
                {
                    d.open = true; pickups.Burst(d.pos + new Vector3(0, .8f, 0), 8, 5); pickups.Burst(d.pos + new Vector3(0, 1, 0), 1, 5);
                    vfx.Flash(d.pos + Vector3.up, 1.2f); SoulAudio.Play("pickup", d.pos); Object.Destroy(d.go); d.go = null;
                }
            }
        }

        // ---------- merchant ----------
        Vector3 MerchantPos()
        {
            if (merchantPos.HasValue) return merchantPos.Value;
            // a quiet corner of the ruined streets, off the trails
            var want = new Vector2(-470, -20); Vector3 at = new Vector3(want.x, 0, want.y);
            for (float r = 0; r < 80; r += 3)
            {
                bool done = false; int n = Mathf.Max(1, (int)(r * .7f));
                for (int k = 0; k < n && !done; k++)
                {
                    float a = k * Mathf.PI * 2 / n, x = want.x + Mathf.Cos(a) * r, z = want.y + Mathf.Sin(a) * r;
                    if (ZoneRoutes.Dist(x, z) > 16 && Territories.Open(0, x, z)) { at = new Vector3(x, Terrain.Height(x, z), z); done = true; }
                }
                if (done) break;
            }
            merchantPos = at; return at;
        }
        /// Z6 떠다니는 배 상인: beside the 흡입 gate (잔해에 막힌 뱃길), there once that gate is open
        Vector3 Merchant6Pos() { var g = gates.Find(x => x.id == "z6_merchant"); return g != null ? g.pos + new Vector3(2.5f, 0, 2.5f) : Vector3.one * 1e6f; }
        int MerchantNear() { if (!player.alive) return -1; if (Sim.FlatY(MerchantPos() - ch.root.position).magnitude < 3.5f) return 0; if (revealedMerchants.Contains(6) && Sim.FlatY(Merchant6Pos() - ch.root.position).magnitude < 3.5f) return 6; return -1; }
        bool NearMerchant() => MerchantNear() >= 0;
        void MerchantStream(Vector3 p)
        {
            var at = MerchantPos(); bool near = Sim.FlatY(at - p).magnitude < 110;
            if (near && merchantGo == null)
            {
                merchantGo = new GameObject("hidden merchant"); merchantGo.transform.SetParent(scene.actors, false);
                merchantGo.transform.localPosition = Sim.ToUnity(at); merchantGo.transform.localRotation = Sim.YawToUnity(1.1f);
                merchantGo.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.Merchant(); merchantGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, .15f, .2f, true);
                var lamp = new GameObject("lantern"); lamp.transform.SetParent(merchantGo.transform, false);
                lamp.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.MerchantLantern(); lamp.AddComponent<MeshRenderer>().sharedMaterial = Mats.Unlit(new Color(2.4f, 1.6f, .6f, 1), Blend.Opaque);
            }
            else if (!near && merchantGo != null) { Object.Destroy(merchantGo); merchantGo = null; }
            bool near6 = revealedMerchants.Contains(6) && Sim.FlatY(Merchant6Pos() - p).magnitude < 120;
            if (near6 && merchantGo6 == null)
            {
                merchantGo6 = new GameObject("boat merchant"); merchantGo6.transform.SetParent(scene.actors, false);
                merchantGo6.transform.localPosition = Sim.ToUnity(Merchant6Pos()); merchantGo6.transform.localRotation = Sim.YawToUnity(-.6f);
                merchantGo6.AddComponent<MeshFilter>().sharedMesh = FieldPropModels.Merchant(); merchantGo6.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(Color.white, Color.black, .15f, .2f, true);
            }
            else if (!near6 && merchantGo6 != null) { Object.Destroy(merchantGo6); merchantGo6 = null; }
        }
        struct Ware { public string id, name, desc; public int price; }
        /// 존별 상인 상품 (카탈로그 10절): a repair kit + the zone's slot parts (bought once). Z1 ~ Z5, Z7 merchants come with their zones.
        static readonly Dictionary<int, (string id, int price)[]> ZoneWares = new Dictionary<int, (string, int)[]>
        {
            { 0, new[] { ("riposte", 80), ("hazard", 80), ("plate", 70) } }, { 1, new[] { ("harpoon", 90), ("wirespool", 80) } },
            { 2, new[] { ("chainblade", 200), ("dropcalc", 180) } }, { 3, new[] { ("piston", 90), ("rustblade", 80) } },
            { 4, new[] { ("shockpalm", 200), ("battery", 220) } }, { 5, new[] { ("thornwhip", 90), ("tailreflex", 80) } },
            { 6, new[] { ("damper", 180), ("stinger", 200) } }, { 7, new[] { ("glassblade", 350), ("weakspot", 320), ("combatcalc", 380) } },
        };
        Ware[] Wares
        {
            get
            {
                var l = new List<Ware> { new Ware { id = "kit", name = "수리 키트", desc = $"X: 최대 체력 45% 즉시 수리 (최대 {KIT_MAX}개)", price = 30 } };
                if (ZoneWares.TryGetValue(shopZone, out var ws)) foreach (var (id, price) in ws) { var d = Skills.Def(id); if (d != null) l.Add(new Ware { id = id, name = d.name, desc = $"{Skills.SlotNames[(int)d.slot]} · {(d.passive ? "패시브" : "액티브")} · {Skills.GradeNames[d.grade]} · {d.desc}", price = price }); }
                return l.ToArray();
            }
        }
        bool Owned(string id) => id == "plating" ? perkPlating : id == "blade" ? perkBlade : id == "coil" ? perkCoil : id != "kit" && Skills.Has(id);
        void OpenShop() { shopZone = Mathf.Max(0, MerchantNear()); shopOpen = true; shopPausedBefore = paused; paused = true; moveTarget = null; shopNote = "…어서 와. 쓸 만한 부품이면 무엇이든 고쳐 주지."; }
        void CloseShop() { shopOpen = false; paused = shopPausedBefore; }
        /// eats input while the shop is open (G / Esc close it)
        bool ShopInput()
        {
            if (!shopOpen) return false;
            if (KeyBinds.Down(KeyBinds.Act.Rest) || KeyBinds.Pressed(KeyCode.Escape)) CloseShop();
            return true;
        }
        public string Buy(string id)
        {
            var all = Wares; var w = System.Array.Find(all, x => x.id == id);
            if (w.id == null) return "없는 물건";
            if (id == "kit" && kits >= KIT_MAX) return shopNote = "더는 못 들고 다녀.";
            if (id != "kit" && Owned(id)) return shopNote = "이미 달았잖아.";
            if (scrap < w.price) return shopNote = "부품이 모자라.";
            scrap -= w.price;
            if (id == "kit") kits++; else if (id == "plating") perkPlating = true; else if (id == "blade") perkBlade = true; else if (id == "coil") perkCoil = true;
            else After(.3f, () => GrantSkill(id));   // a slot part
            SoulAudio.Play("pickup", ch.root.position); return shopNote = $"{w.name}. 잘 쓰렴.";
        }
        void ShopGUI(float W, float H)
        {
            if (!shopOpen) return;
            var ev = Event.current;
            GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(0, 0, W, H), white);
            float w = Mathf.Min(560, W - 40), h = 400, x = (W - w) / 2, y = (H - h) / 2;
            GUI.color = new Color(.05f, .06f, .07f, .95f); GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = new Color(1, .85f, .55f); GUI.Label(new Rect(x + 20, y + 14, w - 40, 26), shopZone == 6 ? "떠다니는 배 상인" : "고물 수집가", new GUIStyle(small) { fontSize = 19, fontStyle = FontStyle.Bold });
            GUI.color = new Color(.85f, .85f, .8f); GUI.Label(new Rect(x + 20, y + 44, w - 40, 22), shopNote, new GUIStyle(small) { fontSize = 13, wordWrap = true });
            GUI.color = new Color(1, .45f, .4f); GUI.Label(new Rect(x + w - 220, y + 16, 200, 22), $"기본 부품 {scrap}   ·   수리 키트 {kits}/{KIT_MAX}", new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleRight });
            var wares = Wares;
            for (int i = 0; i < wares.Length; i++)
            {
                var it = wares[i]; var row = new Rect(x + 20, y + 84 + i * 66, w - 40, 58);
                bool owned = it.id != "kit" && Owned(it.id), hov = row.Contains(ev.mousePosition);
                GUI.color = new Color(1, 1, 1, hov ? .1f : .05f); GUI.DrawTexture(row, white);
                GUI.color = owned ? new Color(.5f, .55f, .6f) : Color.white; GUI.Label(new Rect(row.x + 12, row.y + 6, 260, 22), it.name, new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold });
                GUI.color = new Color(.75f, .8f, .85f); GUI.Label(new Rect(row.x + 12, row.y + 30, 330, 20), it.desc, new GUIStyle(small) { fontSize = 12 });
                var btn = new Rect(row.xMax - 120, row.y + 12, 108, 34);
                GUI.color = owned ? new Color(.3f, .3f, .3f) : scrap >= it.price ? new Color(.85f, .35f, .25f) : new Color(.4f, .25f, .22f); GUI.DrawTexture(btn, white);
                GUI.color = Color.white; GUI.Label(btn, owned ? "장착함" : $"부품 {it.price}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                if (!owned && ev.type == EventType.MouseDown && ev.button == 0 && btn.Contains(ev.mousePosition)) { Buy(it.id); ev.Use(); }
            }
            GUI.color = new Color(.7f, .78f, .85f); GUI.Label(new Rect(x, y + h - 30, w, 20), "G / Esc 닫기", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUI.color = Color.white;
            if (ev.isMouse) ev.Use();
        }

        // ---------- repair kits ----------
        void KitInput()
        {
            if (!Input.GetKeyDown(KeyCode.X) || kits <= 0 || !player.alive || realTime < kitReady || player.hp >= player.max) return;
            kits--; kitReady = realTime + 1; player.hp = Mathf.Min(player.max, player.hp + player.max * KIT_HEAL);
            vfx.Ring(Sim.FlatY(ch.root.position, .3f), .8f); vfx.Flash(ch.root.position + Vector3.up, .6f); SoulAudio.Play("shrine", ch.root.position, .5f, 1.6f);
        }

        void FieldUpdate(float dt)
        {
            if (!fieldHooked) FieldHooks();
            var p = ch.root.position;
            DepotsUpdate(p); MerchantStream(p); PartsWorldUpdate(dt);
        }
        void FieldGUI(float W, float H)
        {
            if (NearMerchant() && !shopOpen) { GUI.color = new Color(1, .85f, .55f); GUI.Label(new Rect(0, H * .62f, W, 24), $"{KeyBinds.Name(KeyBinds.Act.Rest)}  거래 · 숨은 상인", new GUIStyle(big) { fontSize = 17 }); GUI.color = Color.white; }
            ShopGUI(W, H); PartsWorldGUI(W, H);
        }

        /// MCP tests
        public void GiveShards(int n) => scrap += n;   // legacy test hook: shards are basic parts now
        public string FieldReport()
        {
            int sp = 0; foreach (var e in enemies.list) if (e.alive && e.special) sp++;
            return $"scrap {scrap} kits {kits} perks plate {perkPlating} blade {perkBlade} coil {perkCoil} | depots {depots.Count} open {depots.FindAll(d => d.open).Count} | specials alive {sp} | burrows paid {burrowPaid.Count} | merchant {MerchantPos():F0}";
        }
    }

    /// placeholder meshes for the side content (Codex 21 replaces the bodies; keep the method names)
    public static class FieldPropModels
    {
        static Mesh burrow, depot, depotLock, merchant, lantern;
        const int Steel = 0x484c49, Rust = 0x75503b, Soot = 0x292b28;
        static readonly Vector3 LockCenter = new Vector3(0, .55f, .53f);
        static readonly Vector3 BulbCenter = new Vector3(.39f, 1.62f, -.12f);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);
        /// a dark burrow mouth in the ground, ~1 m
        public static Mesh Burrow()
        {
            if (burrow != null) return burrow;
            var b = new MeshBuilder(501);
            // A half-buried broken drain: the entrance is an actual recess,
            // with a dark back wall, floor and an open, thick semicircular lip.
            b.Quad(V(-.29f, .012f, -.29f), V(-.29f, .012f, .39f), V(.29f, .012f, .39f), V(.29f, .012f, -.29f), C(0x211c18));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 6, c = (i + 1) * Mathf.PI / 6;
                Vector3 inner0 = V(Mathf.Cos(a) * .30f, .025f + Mathf.Sin(a) * .27f, .29f);
                Vector3 inner1 = V(Mathf.Cos(c) * .30f, .025f + Mathf.Sin(c) * .27f, .29f);
                Vector3 outer0 = V(Mathf.Cos(a) * .44f, .025f + Mathf.Sin(a) * .40f, .28f + (i % 2) * .035f);
                Vector3 outer1 = V(Mathf.Cos(c) * .44f, .025f + Mathf.Sin(c) * .40f, .28f + ((i + 1) % 2) * .035f);
                Vector3 back = V(0, 0, -.51f);
                b.Quad(inner0, outer0, outer1, inner1, b.Jitter(0x8a8175));
                b.Quad(inner0, inner1, inner1 + back, inner0 + back, C(0x3b3530));
                b.Quad(outer0, outer0 + back, outer1 + back, outer1, b.Jitter(0x716b60));
                b.Tri(V(0, .025f, -.22f), inner0 + back, inner1 + back, C(0x151512));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = .65f + i * (Mathf.PI * 2 - 1.3f) / 7;
                float x = Mathf.Sin(a) * .43f, z = Mathf.Cos(a) * .40f;
                Pebble(b, V(x, .055f, z), V(.13f, .07f, .12f), i % 3 == 0 ? 0x787164 : 0x50453a);
            }
            // Rebar lies along the broken upper lip, safely within the rim.
            b.Beam(V(-.23f, .355f, .28f), V(-.08f, .426f, .26f), .012f, .011f, 3, C(Rust));
            b.Beam(V(-.08f, .426f, .26f), V(.02f, .417f, .31f), .011f, .007f, 3, C(Rust));
            for (int i = 0; i < 2; i++)
            {
                Vector3 a = V(-.25f + i * .45f, .032f, .40f), end = a + V(.075f, .008f, .10f - i * .14f);
                b.Beam(a, end, .014f, .012f, 4, C(0xb4a58a));
                Pebble(b, a, V(.023f, .018f, .020f), 0xa99b83);
                Pebble(b, end, V(.022f, .017f, .020f), 0xb4a58a);
            }
            return burrow = b.Build("burrow");
        }
        /// a sealed military container, ~1.6 x 1 x 1 m before the 1.5x scale
        public static Mesh Depot()
        {
            if (depot != null) return depot;
            var b = new MeshBuilder(502);
            // 1.6 x 1.0 m shell; only the specified front hasp projects to z=.55.
            Case(b, V(0, .47f, 0), V(1.54f, .76f, .92f), 0x535b4d, true);
            b.Box(V(0, .859f, 0), V(1.53f, .027f, .91f), C(Soot));
            Case(b, V(0, .917f, 0), V(1.58f, .09f, .98f), 0x656a59);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * .31f;
                b.Box(V(x, .977f, 0), V(.045f, .030f, .87f), b.Jitter(0x7a7d69));
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(V(x, .47f, side * .470f), V(.045f, .72f, .035f), b.Jitter(Steel));
                    // Rust run-off has an uneven, pointed lower edge.
                    if (i != 0)
                        b.Tri(V(x + side * .028f, .78f, side * .489f), V(x - side * .024f, .78f, side * .489f), V(x + .008f, .41f + i * .027f, side * .489f), C(Rust));
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    b.Box(V(side * .67f, .045f, end * .37f), V(.24f, .09f, .23f), C(Steel));
                    b.Cylinder(V(side * .73f, .091f, end * .42f), .022f, .022f, .014f, 3, C(0x9b947c));
                }
                b.Beam(V(side * .48f - .09f, .857f, -.477f), V(side * .48f + .09f, .857f, -.477f), .020f, .020f, 4, C(Rust));
            }
            b.Box(V(0, .65f, .485f), V(.25f, .34f, .030f), C(Soot));
            b.Box(V(0, .70f, .509f), V(.075f, .27f, .024f), C(0x85816b));
            b.Box(LockCenter - V(0, 0, .015f), V(.25f, .19f, .03f), C(Steel));
            b.Beam(V(-.077f, .635f, .524f), V(.077f, .635f, .524f), .016f, .016f, 4, C(Rust));
            // Folded tarp corner, with a low ridge across the lid and a torn flap.
            Vector3 a = V(-.76f, .969f, -.43f), c = V(-.28f, .969f, -.43f), d = V(-.43f, .980f, -.08f), e = V(-.76f, .970f, .06f);
            b.Quad(a, e, d, c, C(0x76725a));
            b.Quad(a, V(-.771f, .73f, -.39f), V(-.771f, .68f, .04f), e, C(0x5d5c48));
            b.Tri(e, V(-.68f, .987f, -.16f), d, C(0x89836a));
            b.Tri(V(.22f, .965f, .12f), V(.43f, .965f, .27f), V(.55f, .965f, .08f), C(Rust));
            return depot = b.Build("depot");
        }
        public static Mesh DepotLock()
        {
            if (depotLock != null) return depotLock;
            var b = new MeshBuilder(503);
            // Small inset lock plate; the dark hasp and hinge stay in the lit mesh.
            b.Box(LockCenter + V(0, 0, .012f), V(.16f, .108f, .016f), Color.white);
            return depotLock = b.Build("depot lock");
        }
        /// a hunched, gentle scavenger machine sitting by its wares, ~1.4 m
        public static Mesh Merchant()
        {
            if (merchant != null) return merchant;
            var b = new MeshBuilder(504);
            // Low mat, round seated haunches and lowered head give an inviting pose.
            Case(b, V(0, .018f, .42f), V(1.92f, .028f, 1.18f), 0x555549);
            Pebble(b, V(0, .53f, -.035f), V(.39f, .45f, .31f), 0x716c5e, 1);
            Pebble(b, V(0, 1.105f, .125f), V(.27f, .287f, .235f), 0x8d8672, 1);
            Pebble(b, V(0, 1.105f, .304f), V(.208f, .105f, .071f), 0x303d3b);
            // Two short, downward-sloping eyelids; muted paint, never glow.
            for (int side = -1; side <= 1; side += 2)
                b.Beam(V(side * .042f, 1.119f, .369f), V(side * .120f, 1.108f, .359f), .010f, .008f, 3, C(0xb0b6a0));
            b.Box(V(-.19f, .71f, .239f), V(.18f, .16f, .030f), C(0x647567));
            b.Box(V(.11f, .49f, .273f), V(.23f, .12f, .027f), C(0x87745c));
            b.Box(V(.03f, 1.349f, .08f), V(.17f, .028f, .15f), C(0x676c5d));
            for (int side = -1; side <= 1; side += 2)
            {
                Pebble(b, V(side * .27f, .24f, .24f), V(.19f, .16f, .21f), 0x625f53);
                b.Beam(V(side * .25f, .19f, .29f), V(side * .32f, .12f, .48f), .075f, .057f, 4, C(Steel));
                b.Box(V(side * .32f, .083f, .52f), V(.19f, .10f, .24f), C(0x777362));
                Vector3 shoulder = V(side * .34f, .80f, .055f), elbow = V(side * .52f, .47f, .23f);
                Vector3 wrist = V(side * .32f, .32f + (side == 1 ? .025f : 0), .58f);
                Pebble(b, shoulder, V(.10f, .115f, .10f), 0x8a806b);
                b.Beam(shoulder, elbow, .061f, .048f, 5, C(0x65695b));
                b.Beam(elbow, wrist, .043f, .029f, 5, C(0x8c8672));
                b.Box(wrist + V(0, -.015f, .035f), V(.12f, .048f, .12f), C(0x6a7063));
                // Delicate curved fingers rest beside the laid-out parts.
                for (int finger = 0; finger < 2; finger++)
                    b.Beam(wrist + V(-.033f + finger * .065f, -.01f, .07f), wrist + V(-.028f + finger * .055f, -.055f, .14f), .012f, .008f, 3, C(0xaaa08a));
                // Backpack rails and woven retaining straps are visible above.
                b.Beam(V(side * .27f, .28f, -.30f), V(side * .27f, 1.24f, -.37f), .025f, .022f, 4, C(Rust));
                b.Box(V(side * .16f, .88f, -.515f), V(.040f, .79f, .025f), C(0x454b40));
            }
            b.Beam(V(-.28f, 1.22f, -.37f), V(.28f, 1.22f, -.37f), .024f, .024f, 4, C(Steel));
            b.Box(V(0, .55f, -.38f), V(.49f, .33f, .27f), C(0x515b50));
            b.Box(V(-.025f, .88f, -.385f), V(.43f, .26f, .25f), C(0x7b7059));
            b.Beam(V(-.22f, 1.105f, -.39f), V(.20f, 1.105f, -.39f), .105f, .083f, 6, C(0x686c60));
            // Two patched boxes, each with a recessed lid and retaining bands.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 at = V(side * .73f, side == 1 ? .19f : .15f, .37f);
                Vector3 size = V(.42f, side == 1 ? .31f : .23f, .37f);
                Case(b, at, size, side == 1 ? 0x6b6250 : 0x526055);
                for (int strap = -1; strap <= 1; strap += 2)
                    b.Box(at + V(strap * .13f, size.y * .5f + .006f, 0), V(.035f, .012f, .34f), C(0x93907a));
            }
            Case(b, V(-.08f, .092f, .73f), V(.24f, .10f, .19f), 0x887d67);
            b.Disc(V(.26f, .044f, .83f), .047f, .095f, 8, C(0x918b75));
            b.Disc(V(.26f, .054f, .83f), .047f, .086f, 8, C(Steel));
            b.Beam(V(-.40f, .062f, .85f), V(-.20f, .062f, .89f), .024f, .016f, 4, C(0x858a7c));
            // The pole is a physical lantern support on the pack, not a marker.
            b.Beam(V(.25f, .92f, -.36f), V(.32f, 1.83f, -.34f), .018f, .014f, 4, C(Steel));
            b.Beam(V(.32f, 1.83f, -.34f), V(.39f, 1.80f, -.12f), .014f, .012f, 4, C(Rust));
            b.Beam(V(.39f, 1.80f, -.12f), BulbCenter + V(0, .10f, 0), .009f, .009f, 3, C(Steel));
            b.Cylinder(BulbCenter + V(0, .074f, 0), .084f, .047f, .035f, 6, C(0x6e6856));
            b.Cylinder(BulbCenter - V(0, .098f, 0), .070f, .070f, .024f, 6, C(Steel));
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                Vector3 offset = V(Mathf.Sin(a) * .068f, 0, Mathf.Cos(a) * .068f);
                b.Beam(BulbCenter + offset - V(0, .075f, 0), BulbCenter + offset + V(0, .079f, 0), .006f, .006f, 3, C(Steel));
            }
            return merchant = b.Build("merchant");
        }
        public static Mesh MerchantLantern()
        {
            if (lantern != null) return lantern;
            var b = new MeshBuilder(505);
            b.Cylinder(BulbCenter - V(0, .068f, 0), .043f, .058f, .068f, 6, Color.white);
            b.Cylinder(BulbCenter, .058f, .041f, .068f, 6, Color.white);
            return lantern = b.Build("lantern");
        }

        static void Pebble(MeshBuilder b, Vector3 at, Vector3 radii, int hex, int detail = 0)
        {
            b.Push(); b.Translate(at); b.Scale(radii.x, radii.y, radii.z);
            b.Rock(Vector3.zero, 1, C(hex), .045f, 1, detail); b.Pop();
        }

        // Chamfered, optionally dented steel case. No primitive exceeds size.
        static void Case(MeshBuilder b, Vector3 at, Vector3 size, int hex, bool dent = false)
        {
            float x = size.x * .5f, z = size.z * .5f, y = size.y * .5f;
            float cut = Mathf.Min(x, z) * .16f;
            Vector3[] rim = { V(-x + cut, 0, -z), V(-x, 0, -z + cut), V(-x, 0, z - cut), V(-x + cut, 0, z),
                V(x - cut, 0, z), V(x, 0, z - cut), V(x, 0, -z + cut), V(x - cut, 0, -z) };
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector3 lo = at + rim[i] * .97f - V(0, y, 0), next = at + rim[j] * .97f - V(0, y, 0);
                Vector3 hi = at + rim[i] + V(0, y, 0), highNext = at + rim[j] + V(0, y, 0);
                if (dent)
                {
                    Vector3 mid = at + rim[i] * (i == 5 ? .91f : 1), midNext = at + rim[j] * (j == 5 ? .91f : 1);
                    b.Quad(lo, next, midNext, mid, b.Jitter(hex));
                    b.Quad(mid, midNext, highNext, hi, b.Jitter(hex));
                }
                else b.Quad(lo, next, highNext, hi, b.Jitter(hex));
                b.Tri(at + V(0, y, 0), hi, highNext, b.Jitter(hex, .04f));
                b.Tri(at - V(0, y, 0), next, lo, C(Soot));
            }
        }
    }
}
