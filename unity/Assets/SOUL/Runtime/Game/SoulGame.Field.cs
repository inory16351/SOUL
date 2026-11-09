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
    //  - 붉은 조각 (the 원혼 부품 currency; in-game text never says 원혼): elites 5, specials 2, sentries 1, others 10 %.
    //  - 숨은 상인: a gentle machine hiding in the ruined streets. G to trade: 수리 키트 (X heals 45 %), and three
    //    one-time body parts (+20 max HP, +10 % damage, dash cooldown -15 %).
    // Models are PLACEHOLDERS (Claude): Codex job 21 replaces FieldPropModels.
    public partial class SoulGame
    {
        public int shards, kits;
        bool perkPlating, perkBlade, perkCoil;
        const int KIT_MAX = 3; const float KIT_HEAL = .45f;
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
        Vector3? merchantPos; GameObject merchantGo; bool shopOpen, shopPausedBefore; string shopNote = "";
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
            int shard = e.def.eliteName != null ? 5 : e.special ? 2 : e.type == "sentry" ? 1 : Random.value < .1f ? 1 : 0;
            if (e.special) pickups.Burst(e.Center(), 5, 5);
            for (int i = 0; i < shard; i++) pickups.Spawn("shard", e.Center() + new Vector3(0, .6f, 0), 1, new Vector3(Random.Range(-2f, 2f), 4.5f, Random.Range(-2f, 2f)));
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
            pickups.Burst(at, 6, 5); pickups.Spawn("shard", at + new Vector3(0, .6f, 0), 1, new Vector3(0, 5, 0));
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
                    d.open = true; pickups.Burst(d.pos + new Vector3(0, .8f, 0), 8, 5); pickups.Spawn("shard", d.pos + new Vector3(0, 1, 0), 1, new Vector3(0, 5, 0));
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
        bool NearMerchant() => player.alive && Sim.FlatY(MerchantPos() - ch.root.position).magnitude < 3.5f;
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
        }
        struct Ware { public string id, name, desc; public int price; }
        static readonly Ware[] Wares =
        {
            new Ware { id = "kit",     name = "수리 키트",   desc = "X: 최대 체력 45% 즉시 수리 (최대 3개)", price = 3 },
            new Ware { id = "plating", name = "덧댄 장갑판", desc = "최대 체력 +20",                     price = 6 },
            new Ware { id = "blade",   name = "벼린 칼날",   desc = "주는 피해 +10%",                    price = 8 },
            new Ware { id = "coil",    name = "냉각 코일",   desc = "대쉬 쿨다운 -15%",                  price = 6 },
        };
        bool Owned(string id) => id == "plating" ? perkPlating : id == "blade" ? perkBlade : id == "coil" && perkCoil;
        void OpenShop() { shopOpen = true; shopPausedBefore = paused; paused = true; moveTarget = null; shopNote = "…어서 와. 붉은 조각이면 무엇이든 고쳐 주지."; }
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
            var w = System.Array.Find(Wares, x => x.id == id);
            if (w.id == null) return "없는 물건";
            if (id == "kit" && kits >= KIT_MAX) return shopNote = "더는 못 들고 다녀.";
            if (id != "kit" && Owned(id)) return shopNote = "이미 달았잖아.";
            if (shards < w.price) return shopNote = "조각이 모자라.";
            shards -= w.price;
            if (id == "kit") kits++; else if (id == "plating") perkPlating = true; else if (id == "blade") perkBlade = true; else if (id == "coil") perkCoil = true;
            SoulAudio.Play("pickup", ch.root.position); return shopNote = $"{w.name}. 잘 쓰렴.";
        }
        void ShopGUI(float W, float H)
        {
            if (!shopOpen) return;
            var ev = Event.current;
            GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(0, 0, W, H), white);
            float w = Mathf.Min(560, W - 40), h = 400, x = (W - w) / 2, y = (H - h) / 2;
            GUI.color = new Color(.05f, .06f, .07f, .95f); GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = new Color(1, .85f, .55f); GUI.Label(new Rect(x + 20, y + 14, w - 40, 26), "숨은 상인", new GUIStyle(small) { fontSize = 19, fontStyle = FontStyle.Bold });
            GUI.color = new Color(.85f, .85f, .8f); GUI.Label(new Rect(x + 20, y + 44, w - 40, 22), shopNote, new GUIStyle(small) { fontSize = 13, wordWrap = true });
            GUI.color = new Color(1, .45f, .4f); GUI.Label(new Rect(x + w - 220, y + 16, 200, 22), $"붉은 조각 {shards}   ·   수리 키트 {kits}/{KIT_MAX}", new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleRight });
            for (int i = 0; i < Wares.Length; i++)
            {
                var it = Wares[i]; var row = new Rect(x + 20, y + 84 + i * 66, w - 40, 58);
                bool owned = it.id != "kit" && Owned(it.id), hov = row.Contains(ev.mousePosition);
                GUI.color = new Color(1, 1, 1, hov ? .1f : .05f); GUI.DrawTexture(row, white);
                GUI.color = owned ? new Color(.5f, .55f, .6f) : Color.white; GUI.Label(new Rect(row.x + 12, row.y + 6, 260, 22), it.name, new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold });
                GUI.color = new Color(.75f, .8f, .85f); GUI.Label(new Rect(row.x + 12, row.y + 30, 330, 20), it.desc, new GUIStyle(small) { fontSize = 12 });
                var btn = new Rect(row.xMax - 120, row.y + 12, 108, 34);
                GUI.color = owned ? new Color(.3f, .3f, .3f) : shards >= it.price ? new Color(.85f, .35f, .25f) : new Color(.4f, .25f, .22f); GUI.DrawTexture(btn, white);
                GUI.color = Color.white; GUI.Label(btn, owned ? "장착함" : $"조각 {it.price}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
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
            DepotsUpdate(p); MerchantStream(p);
        }
        void FieldGUI(float W, float H)
        {
            if (NearMerchant() && !shopOpen) { GUI.color = new Color(1, .85f, .55f); GUI.Label(new Rect(0, H * .62f, W, 24), $"{KeyBinds.Name(KeyBinds.Act.Rest)}  거래 · 숨은 상인", new GUIStyle(big) { fontSize = 17 }); GUI.color = Color.white; }
            ShopGUI(W, H);
        }

        /// MCP tests
        public void GiveShards(int n) => shards += n;
        public string FieldReport()
        {
            int sp = 0; foreach (var e in enemies.list) if (e.alive && e.special) sp++;
            return $"shards {shards} kits {kits} perks plate {perkPlating} blade {perkBlade} coil {perkCoil} | depots {depots.Count} open {depots.FindAll(d => d.open).Count} | specials alive {sp} | burrows paid {burrowPaid.Count} | merchant {MerchantPos():F0}";
        }
    }

    /// placeholder meshes for the side content (Codex 21 replaces the bodies; keep the method names)
    public static class FieldPropModels
    {
        static Mesh burrow, depot, depotLock, merchant, lantern;
        /// a dark burrow mouth in the ground, ~1 m
        public static Mesh Burrow()
        {
            if (burrow != null) return burrow;
            var b = new MeshBuilder(501);
            b.Disc(new Vector3(0, .01f, 0), 0, .55f, 10, MeshBuilder.Hex(0x14110f));
            b.Disc(new Vector3(0, .02f, 0), .45f, .75f, 10, MeshBuilder.Hex(0x4a4038));
            for (int i = 0; i < 5; i++) { float a = i * 1.26f; b.Rock(new Vector3(Mathf.Sin(a) * .8f, .06f, Mathf.Cos(a) * .8f), .14f, MeshBuilder.Hex(0x5a5048), .3f, .6f, 0); }
            return burrow = b.Build("burrow");
        }
        /// a sealed military container, ~1.6 x 1 x 1 m before the 1.5x scale
        public static Mesh Depot()
        {
            if (depot != null) return depot;
            var b = new MeshBuilder(502);
            b.Box(new Vector3(0, .45f, 0), new Vector3(1.6f, .9f, 1f), MeshBuilder.Hex(0x4f564a));
            b.Box(new Vector3(0, .92f, 0), new Vector3(1.66f, .06f, 1.06f), MeshBuilder.Hex(0x3e443a));
            for (int i = -1; i <= 1; i++) b.Box(new Vector3(i * .55f, .45f, .51f), new Vector3(.05f, .86f, .03f), MeshBuilder.Hex(0x30352e));
            b.Box(new Vector3(-.5f, .2f, -.6f), new Vector3(.3f, .18f, .2f), MeshBuilder.Hex(0x5a5048));
            return depot = b.Build("depot");
        }
        public static Mesh DepotLock()
        {
            if (depotLock != null) return depotLock;
            var b = new MeshBuilder(503); b.Box(new Vector3(0, .55f, .53f), new Vector3(.22f, .16f, .04f), Color.white);
            return depotLock = b.Build("depot lock");
        }
        /// a hunched, gentle scavenger machine sitting by its wares, ~1.4 m
        public static Mesh Merchant()
        {
            if (merchant != null) return merchant;
            var b = new MeshBuilder(504);
            b.Rock(new Vector3(0, .55f, 0), .45f, MeshBuilder.Hex(0x6a6458), .2f, .9f, 1);
            b.Rock(new Vector3(0, 1.05f, .15f), .24f, MeshBuilder.Hex(0x7a7468), .15f, 1, 1);
            b.Box(new Vector3(0, 1.08f, .36f), new Vector3(.22f, .06f, .04f), MeshBuilder.Hex(0x1a1c1e));
            for (int s = -1; s <= 1; s += 2) b.Beam(new Vector3(s * .35f, .75f, .1f), new Vector3(s * .45f, .25f, .35f), .06f, .05f, 5, MeshBuilder.Hex(0x5a5448));
            b.Box(new Vector3(.7f, .2f, .4f), new Vector3(.6f, .4f, .4f), MeshBuilder.Hex(0x4a4238));
            b.Box(new Vector3(-.65f, .15f, .3f), new Vector3(.4f, .3f, .4f), MeshBuilder.Hex(0x3e4a44));
            b.Beam(new Vector3(.2f, 1.2f, -.2f), new Vector3(.3f, 1.9f, -.25f), .02f, .02f, 4, MeshBuilder.Hex(0x2a2c2e));
            return merchant = b.Build("merchant");
        }
        public static Mesh MerchantLantern()
        {
            if (lantern != null) return lantern;
            var b = new MeshBuilder(505); b.Rock(new Vector3(.3f, 1.82f, -.25f), .09f, Color.white, .1f, 1.2f, 0);
            return lantern = b.Build("lantern");
        }
    }
}
