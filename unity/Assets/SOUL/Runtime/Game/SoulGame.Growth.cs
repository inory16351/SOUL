using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // 성장 화면 (Tab) and the growth effects (World/GrowthTree.cs = data, multipliers).
    //   tab 0 노드 트리: 오리 방식 node tree (hover = description, click = open with points from xp)
    //   tab 1 스킬 파츠: owned skills, level, next cost in basic parts (wallet), upgrade button
    // While open the game is paused (the `paused` flag, restored on close) and SkillInput swallows every input,
    // so clicks never reach the rope. Esc / Tab close it.
    // Effects that need no edit elsewhere are applied here every frame: max HP, post-hit regen, hit-area radii
    // (AREA), dash cooldown (COOLDOWN), fog reveal, pod sense. Damage / speed / damage taken fold into
    // SkillDamageScale / OverloadSpeed / OverloadTaken (SoulGame.Skills.cs), which the combat code multiplies.
    // UI art (Resources/UI, rects from ui_atlas.md): growth_tree_bg (nodes sit in its sockets), ui_nodes (5 branches ×
    // 3 states), ui_panel (9-slice window, tabs, buttons), ui_skill_icons (4 × 2); missing files fall back to flat shapes.
    public partial class SoulGame
    {
        bool growthOpen, growthPausedBefore; int growthTab; float growthFogT, growthPodT, growthRegenFrom = -99, growthLastHp = -1;
        static Dictionary<string, (float r, float a)> areaBase; static Dictionary<KeyCode, float> cdBase;

        /// basic parts came in (pickup, pod cleanse reward): the wallet only (2026-11-10: xp comes from kills, GainXp)
        public void GainScrap(int n) { if (n <= 0) return; scrap += n; PartsScrapPicked(n); }
        /// a kill (SoulGame onKill) gave xp: a new point is announced once
        public void GainXp(int n)
        {
            if (n <= 0 || !skillsInit) return;
            int before = Growth.Earned(); Growth.Gain(n);
            if (Growth.Earned() > before) Cue($"성장 포인트 +{Growth.Earned() - before}  ·  {KeyBinds.Name(KeyBinds.Act.Growth)} 성장 화면", 2.2f);
        }

        // ---------- input / open-close ----------
        /// true = the growth screen has this frame's input
        bool GrowthInput()
        {
            bool was = growthOpen;
            if (KeyBinds.Down(KeyBinds.Act.Growth)) SetGrowthOpen(!growthOpen);
            else if (growthOpen && KeyBinds.Pressed(KeyCode.Escape)) SetGrowthOpen(false);
            if (growthOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q)) growthTab = 0;
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.E)) growthTab = 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) growthTab = 2;
                if (Input.GetKeyDown(KeyCode.Alpha4)) growthTab = 3;
            }
            return was || growthOpen;
        }
        public bool GrowthOpen => growthOpen;
        /// MCP captures: show this node's tooltip as if hovered (null = real mouse)
        public string growthHoverForTest;
        public void SetGrowthOpen(bool on, int tab = -1)
        {
            if (tab >= 0) growthTab = tab;
            if (on == growthOpen) return;
            growthOpen = on;
            if (on) { growthPausedBefore = paused; paused = true; moveTarget = null; ropeHeld = false; }
            else paused = growthPausedBefore;
        }

        // ---------- MCP test hooks ----------
        public bool GrowthOpenNode(string id) { bool ok = Growth.TryOpen(id); if (ok) GrowthApply(0); return ok; }
        public void GrowthApplyNow() => GrowthApply(0);
        public float DamageScaleFor(string move) => SkillDamageScale(move);
        public float MoveSpeedScale => OverloadSpeed;
        public float DamageTakenScale => OverloadTaken;
        public float DashCooldown => COOLDOWN[KeyCode.Space];
        public float AreaRadius(string move) => AREA[move].r;
        public float CounterWindowEnd => .27f + CounterWindowBonus;
        public bool SkillUpgrade(string id) { var o = Skills.Get(id); bool ok = Skills.LevelUp(o, ref scrap); return ok; }

        float CounterWindowBonus => Growth.CounterWindow + (Skills.Get("counter") is var co && co != null && co.Lv3 ? .05f : 0) + (realTime < counterWindowBoostUntil || realTime < counterChainUntil ? .27f : 0);
        float RopeRange => RopeAnchors.RANGE * Growth.RopeRangeScale;

        // ---------- effects ----------
        void GrowthApply(float dt)
        {
            float want = Growth.MaxHp + (perkPlating ? 20 : 0);   // + 덧댄 장갑판 (merchant)
            if (!Mathf.Approximately(player.max, want))
            {
                float d = want - player.max; player.max = want;
                player.hp = d > 0 && player.alive ? player.hp + d : Mathf.Min(player.hp, want);
            }
            // 재생 회로: 1.5 .. 5.5 s after the last hit
            if (growthLastHp >= 0 && player.hp < growthLastHp - .01f) growthRegenFrom = realTime + 1.5f;
            float rr = Growth.RegenRate;
            if (rr > 0 && dt > 0 && player.alive && realTime >= growthRegenFrom && realTime < growthRegenFrom + 4) player.hp = Mathf.Min(player.max, player.hp + player.max * rr * dt);
            growthLastHp = player.hp;
            // hit areas (평타 범위, skill perks) and dash cooldown, scaled from the untouched base tables
            if (areaBase == null) { areaBase = new Dictionary<string, (float, float)>(AREA); cdBase = new Dictionary<KeyCode, float>(COOLDOWN); }
            foreach (var kv in areaBase)
            {
                float k = kv.Key.StartsWith("light") ? Growth.ReachScale : kv.Key == "heavy" ? 1 + Skills.Perk("whirl", Skills.PerkKind.Range) : kv.Key == "flurry" ? 1 + Skills.Perk("storm", Skills.PerkKind.Range) : 1;
                AREA[kv.Key] = (kv.Value.r * k, kv.Value.a);
            }
            COOLDOWN[KeyCode.Space] = cdBase[KeyCode.Space] * Growth.DashCdScale * (perkCoil ? .85f : 1);   // 냉각 코일 (merchant)
            if (dt <= 0) return;
            // 광역 스캐너: wider fog reveal (ZoneUpdate keeps revealing the base 46 m)
            if ((growthFogT -= dt) <= 0) { growthFogT = .25f; if (Growth.FogScale > 1) MapFog.Reveal(ch.root.position, 46 * Growth.FogScale); }
            // 포드 신호 감지
            if (Growth.PodSense > 0 && (growthPodT -= dt) <= 0)
            {
                growthPodT = .5f;
                foreach (var pod in ZoneAreas.Pods)
                    if (!pod.found && Sim.FlatY(pod.pos - ch.root.position).magnitude < Growth.PodSense) { pod.found = true; Cue($"포드 신호 감지 · {pod.name}  (M 지도)", 2.4f); }
            }
        }

        // ---------- art ----------
        static Texture2D gBg, gNodes, gPanel, gSkillIcons, gDisc, gRing; static bool gLoaded;
        static void GrowthArt()
        {
            if (gLoaded) return; gLoaded = true;
            gBg = Resources.Load<Texture2D>("UI/growth_tree_bg"); gNodes = Resources.Load<Texture2D>("UI/ui_nodes");
            gPanel = Resources.Load<Texture2D>("UI/ui_panel"); gSkillIcons = Resources.Load<Texture2D>("UI/ui_skill_icons");
            gDisc = Circle(false); gRing = Circle(true);
        }
        /// soft disc / ring for the flat fallback
        static Texture2D Circle(bool ring)
        {
            const int N = 64; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = new Vector2(x - N / 2f + .5f, y - N / 2f + .5f).magnitude / (N / 2f);
                float a = ring ? Mathf.Clamp01((1 - Mathf.Abs(d - .86f) / .1f) * 1.5f) : Mathf.Clamp01((1 - d) * 24);
                px[y * N + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px); t.Apply(); return t;
        }
        // atlas rects in image pixels (x, y, w, h; origin top-left) from Resources/UI/ui_atlas.md
        static readonly RectInt[,] NODE_RECT =
        {   // [state, branch]: locked / available / open × core, right arm, left arm, legs, head
            { new RectInt(23, 219, 232, 236), new RectInt(267, 219, 231, 236), new RectInt(511, 218, 232, 236), new RectInt(756, 218, 232, 236), new RectInt(1000, 219, 233, 235) },
            { new RectInt(22, 507, 233, 237), new RectInt(267, 507, 230, 236), new RectInt(511, 507, 231, 237), new RectInt(756, 507, 232, 236), new RectInt(1001, 506, 232, 237) },
            { new RectInt(23, 796, 232, 244), new RectInt(267, 796, 231, 245), new RectInt(512, 796, 231, 244), new RectInt(756, 796, 231, 244), new RectInt(1001, 797, 232, 243) },
        };
        static readonly RectInt P_PANEL = new RectInt(21, 96, 893, 676), P_BTN = new RectInt(938, 109, 571, 147), P_BTN_HI = new RectInt(937, 272, 574, 149),
            P_BTN_OFF = new RectInt(939, 437, 572, 146), P_TAB = new RectInt(950, 639, 275, 96), P_TAB_ON = new RectInt(1229, 639, 279, 96);
        /// source image sizes from ui_atlas.md: the importer may rescale (ui_nodes → 1024, NPOT → power of two), so uv
        /// fractions are taken against these, never against Texture2D.width / height
        static readonly Vector2 SZ_NODES = new Vector2(1254, 1254), SZ_PANEL = new Vector2(1536, 1024), SZ_ICONS = new Vector2(1774, 887);
        /// image-pixel rect (top-left origin) → Unity uv rect (bottom-left origin)
        static Rect UVof(Vector2 size, float x, float y, float w, float h) => new Rect(x / size.x, 1 - (y + h) / size.y, w / size.x, h / size.y);
        /// draw `src` of `t` into r as a 9-slice; border in source pixels, drawn `k` screen px per source px
        static void Nine(Rect r, Texture2D t, Vector2 size, RectInt src, float bl, float br, float bt, float bb, float k)
        {
            float[] sx = { src.x, src.x + bl, src.xMax - br, src.xMax }, sy = { src.y, src.y + bt, src.yMax - bb, src.yMax };
            float L = Mathf.Min(bl * k, r.width / 2), R = Mathf.Min(br * k, r.width / 2), T = Mathf.Min(bt * k, r.height / 2), B = Mathf.Min(bb * k, r.height / 2);
            float[] dx = { r.x, r.x + L, r.xMax - R, r.xMax }, dy = { r.y, r.y + T, r.yMax - B, r.yMax };
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                if (dx[i + 1] - dx[i] < .5f || dy[j + 1] - dy[j] < .5f) continue;
                GUI.DrawTextureWithTexCoords(new Rect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]), t, UVof(size, sx[i], sy[j], sx[i + 1] - sx[i], sy[j + 1] - sy[j]), true);
            }
        }
        /// ui_nodes.png: 5 columns (branch) × 3 rows (locked, available, open); r = the 232 px ring
        static bool NodeArt(Rect r, Growth.State s, int branch)
        {
            if (!gNodes) return false;
            GUI.color = Color.white;
            var src = NODE_RECT[(int)s, branch];
            float k = r.width / 232f;   // the open row is 8 px taller (glow below): keep the ring where the others are
            var dst = new Rect(r.x, r.y, src.width * k, src.height * k);
            GUI.DrawTextureWithTexCoords(dst, gNodes, UVof(SZ_NODES, src.x, src.y, src.width, src.height), true); return true;
        }
        // ---------- slot part icons: ui_part_icons_a / _b.png (2026-11-10, ChatGPT sheets), 8 × 5 cells of 192 × 204.8 px,
        // catalogue order (Skills.Defs 0 .. 79: sheet a = 0 .. 39, sheet b = 40 .. 79), transparent background ----------
        static Texture2D gPartA, gPartB; static bool gPartLoaded;
        public static int PartIconIndex(string id) { var d = Skills.Defs; for (int i = 0; i < d.Length; i++) if (d[i].id == id) return d[i].grudge ? -1 : i; return -1; }
        public static bool PartIconArt(Rect r, int index)
        {
            if (!gPartLoaded) { gPartLoaded = true; gPartA = Resources.Load<Texture2D>("UI/ui_part_icons_a"); gPartB = Resources.Load<Texture2D>("UI/ui_part_icons_b"); }
            if (index < 0 || index >= 80) return false;
            var tex = index < 40 ? gPartA : gPartB; if (!tex) return false;
            int i = index % 40, c = i % 8, row = i / 8;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(c / 8f, 1 - (row + 1) / 5f, 1 / 8f, 1 / 5f), true); return true;
        }
        /// ui_skill_icons.png cell of a skill id (-1 = no art yet: UiArt.Icon fallback)
        static int AtlasIndex(string id) => System.Array.IndexOf(new[] { "counter", "whirl", "wave", "storm", "overload" }, id);
        /// ui_skill_icons.png: 4 × 2 cells of 443.5 px (반격, 회오리, 레이저 파동, 칼날 폭풍, 코어 과부하, 로프 팔, ...)
        static bool SkillIconArt(Rect r, int index)
        {
            if (!gSkillIcons || index < 0 || index > 7) return false;
            int c = index % 4, row = index / 4; const float cell = 443.5f;
            GUI.DrawTextureWithTexCoords(r, gSkillIcons, UVof(SZ_ICONS, c * cell, row * cell, cell, cell), true); return true;
        }
        void GLine(Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a; float len = d.magnitude; if (len < 1) return;
            var m = GUI.matrix; GUI.color = c;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2, len, width), white);
            GUI.matrix = m;
        }
        void Frame(Rect r, Color fill)
        {
            if (gPanel) { GUI.color = Color.white; Nine(r, gPanel, SZ_PANEL, P_PANEL, 120, 120, 120, 120, .42f); return; }
            GUI.color = fill; GUI.DrawTexture(r, white);
            GUI.color = new Color(.45f, .8f, 1f, .5f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), white);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), white); GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), white);
        }
        /// tab / button pieces of ui_panel.png, flat colour without it
        void Piece(Rect r, RectInt src, float bx, float by, Color flat)
        {
            if (gPanel) { GUI.color = Color.white; Nine(r, gPanel, SZ_PANEL, src, bx, bx, by, by, Mathf.Min(.5f, r.height / src.height)); }
            else { GUI.color = flat; GUI.DrawTexture(r, white); }
        }

        // ---------- HUD extras (head nodes) ----------
        Vector2? ScreenOf(Vector3 simPos)
        {
            var c = cam.camera; if (!c) return null;
            var s = c.WorldToScreenPoint(transform.TransformPoint(Sim.ToUnity(simPos)));
            if (s.z <= 0) return null; return new Vector2(s.x, Screen.height - s.y);
        }
        void GrowthHud(float W, float H)
        {
            if (Growth.EnemyHp)
                foreach (var e in enemies.list)
                {
                    if (!e.Hittable || e.def.bossName != null || Sim.FlatY(e.pos - ch.root.position).magnitude > 26) continue;
                    var s = ScreenOf(e.pos + new Vector3(0, e.height + .6f, 0)); if (!s.HasValue) continue;
                    GUI.color = new Color(1, .9f, .75f); GUI.Label(new Rect(s.Value.x - 40, s.Value.y - 22, 80, 18), Mathf.CeilToInt(e.hp).ToString(), keyStyle);
                }
            if (Growth.ShrineSense > 0)
            {
                Shrine best = null; float bd = Growth.ShrineSense;
                foreach (var sh in ZoneAreas.Shrines) { float d = Sim.FlatY(sh.pos - ch.root.position).magnitude; if (!ZoneState.HasCore(sh.zone) && d < bd) { bd = d; best = sh; } }
                var me = ScreenOf(ch.root.position + new Vector3(0, 1, 0));
                if (best != null && me.HasValue)
                {
                    var tgt = ScreenOf(best.pos + new Vector3(0, ch.root.position.y + 3, 0)) ?? me.Value;
                    var dir = (tgt - me.Value); if (dir.sqrMagnitude < 1) dir = Vector2.up; dir.Normalize();
                    var at = me.Value + dir * 90;
                    GUI.color = new Color(.6f, .95f, 1f, .9f); GUI.Label(new Rect(at.x - 70, at.y - 10, 140, 20), $"◆ 공명 기둥 {bd:F0}m", keyStyle);
                }
            }
            GUI.color = Color.white;
        }

        // ---------- the growth screen ----------
        void GrowthGUI(float W, float H)
        {
            GrowthArt(); GrowthStyles();
            var ev = Event.current;
            GUI.color = new Color(0, 0, 0, .72f); GUI.DrawTexture(new Rect(0, 0, W, H), white);
            float w = Mathf.Min(W - 24, 1600), h = Mathf.Min(H - 24, 900), x = (W - w) / 2, y = (H - h) / 2;
            var panel = new Rect(x, y, w, h);
            Frame(panel, new Color(.03f, .05f, .07f, .94f));
            float pad = gPanel ? Mathf.Clamp(h * .06f, 18, 40) : 12;
            // header + tabs
            GUI.color = new Color(.75f, .95f, 1);
            GUI.Label(new Rect(x + pad + 8, y + pad - 4, 200, 30), "성장", new GUIStyle(gTitle) { fontSize = 21 });
            string[] tabs = { "노드 트리", "슬롯", "모듈", "기록" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tr = new Rect(x + pad + 90 + i * 140, y + pad - 6, 132, 34);
                Piece(tr, growthTab == i ? P_TAB_ON : P_TAB, 60, 24, growthTab == i ? new Color(.3f, .7f, 1f, .9f) : new Color(.15f, .2f, .25f, .9f));
                GUI.color = growthTab == i ? Color.white : new Color(.7f, .78f, .85f); GUI.Label(tr, $"{i + 1}  {tabs[i]}", new GUIStyle(gNum) { fontSize = 13 });
                if (ev.type == EventType.MouseDown && ev.button == 0 && tr.Contains(ev.mousePosition)) { growthTab = i; ev.Use(); }
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + pad + 660, y + pad - 2, w - 2 * pad - 670, 24),
                $"경험치 {Growth.xp}   ·   포인트 {Growth.Points}   ·   지갑 기본 부품 {scrap}   ·   {KeyBinds.Name(KeyBinds.Act.Growth)} / Esc 닫기", new GUIStyle(gNumRight) { fontSize = 13 });
            var body = new Rect(x + pad, y + pad + 36, w - 2 * pad, h - 2 * pad - 36);
            if (growthTab == 0) TreeTab(body, ev); else if (growthTab == 1) SlotsTab(body, ev); else if (growthTab == 2) ModulesTab(body, ev); else RecordsTab(body, ev);
            GUI.color = Color.white;
        }

        void TreeTab(Rect body, Event ev)
        {
            // background fitted (16:9) left of the side panel; node positions are background pixels
            float sideW = Mathf.Min(250, body.width * .3f);
            var area = new Rect(body.x, body.y, body.width - sideW - 8, body.height - 26);
            float S = Mathf.Min(area.width / Growth.BG_SIZE.x, area.height / Growth.BG_SIZE.y);
            var bg = new Rect(area.x + (area.width - Growth.BG_SIZE.x * S) / 2, area.y + (area.height - Growth.BG_SIZE.y * S) / 2, Growth.BG_SIZE.x * S, Growth.BG_SIZE.y * S);
            if (gBg) { GUI.color = Color.white; GUI.DrawTexture(bg, gBg, ScaleMode.StretchToFill, false); }
            else { GUI.color = new Color(.02f, .035f, .05f, 1); GUI.DrawTexture(bg, white); }
            System.Func<Vector2, Vector2> At = p => bg.position + p * S;
            float R = Mathf.Max(32 * S, 12);
            // links (a branch's first node hangs off its body point)
            foreach (var n in Growth.Nodes)
            {
                var st = Growth.StateOf(n); var col = Growth.BranchColors[n.branch];
                Color lc = st == Growth.State.Open ? col : st == Growth.State.Available ? new Color(col.r, col.g, col.b, .6f) : new Color(.35f, .38f, .42f, gBg ? .0f : .45f);
                float lw = st == Growth.State.Open ? Mathf.Max(3, 6 * S) : 2;
                if (n.pre.Length == 0) GLine(At(Growth.Roots[n.branch]), At(n.pos), lw, lc);
                foreach (var pid in n.pre) GLine(At(Growth.Node(pid).pos), At(n.pos), lw, lc);
            }
            if (!gBg) for (int b = 0; b < 5; b++) { var q = At(Growth.Roots[b]); GUI.color = Growth.BranchColors[b]; GUI.DrawTexture(new Rect(q.x - 5, q.y - 5, 10, 10), gDisc); }
            // nodes
            GrowthNode hover = null; Vector2 hoverAt = Vector2.zero;
            var badge = new GUIStyle(gNum) { fontSize = R < 18 ? 12 : 13 };
            foreach (var n in Growth.Nodes)
            {
                var q = At(n.pos); var rr = new Rect(q.x - R, q.y - R, R * 2, R * 2);
                var st = Growth.StateOf(n); var col = Growth.BranchColors[n.branch];
                bool afford = st == Growth.State.Available && Growth.Points >= n.cost;
                bool over = (ev.mousePosition - q).sqrMagnitude < R * R; if (over) { hover = n; hoverAt = q; }
                float pulse = afford ? .65f + .35f * Mathf.Sin(realTime * 5) : 1;
                if (afford) { GUI.color = new Color(col.r, col.g, col.b, .35f * pulse); GUI.DrawTexture(new Rect(rr.x - R * .35f, rr.y - R * .35f, rr.width + R * .7f, rr.height + R * .7f), gDisc); }
                if (NodeArt(rr, st, n.branch)) { if (st == Growth.State.Open) { GUI.color = new Color(col.r, col.g, col.b, .9f); GUI.DrawTexture(rr, gRing); } }
                else
                {
                    GUI.color = st == Growth.State.Open ? col : st == Growth.State.Available ? new Color(col.r * .25f, col.g * .25f, col.b * .25f, 1) : new Color(.1f, .11f, .12f, 1);
                    GUI.DrawTexture(rr, gDisc);
                    GUI.color = st == Growth.State.Locked ? new Color(.4f, .42f, .45f, .8f) : new Color(col.r, col.g, col.b, pulse);
                    GUI.DrawTexture(rr, gRing);
                }
                if (over) { GUI.color = new Color(1, 1, 1, .95f); GUI.DrawTexture(new Rect(rr.x - 3, rr.y - 3, rr.width + 6, rr.height + 6), gRing); }
                if (st != Growth.State.Open)
                {   // cost badge (bottom-right)
                    float bs = Mathf.Max(R * .8f, 15); var bb = new Rect(rr.xMax - bs * .7f, rr.yMax - bs * .7f, bs, bs);
                    GUI.color = new Color(.03f, .04f, .05f, .92f); GUI.DrawTexture(bb, gDisc);
                    GUI.color = afford ? new Color(.6f, 1, .7f) : st == Growth.State.Available ? Color.white : new Color(.6f, .62f, .65f);
                    GUI.Label(bb, n.cost.ToString(), badge);
                }
                else if (!gNodes) { GUI.color = new Color(.05f, .05f, .05f); GUI.Label(rr, "✔", keyStyle); }
                if (n.part != Parts.None)
                {   // required core part (top-right)
                    float ps = Mathf.Max(R * .9f, 16); var pr = new Rect(rr.xMax - ps * .6f, rr.y - ps * .35f, ps, ps);
                    GUI.color = new Color(.03f, .04f, .05f, .85f); GUI.DrawTexture(pr, gDisc);
                    GUI.color = Parts.Has(n.part) ? Color.white : new Color(1, 1, 1, .4f); UiArt.Icon(pr, UiArt.PartIcon(n.part));
                }
                if (over && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    ev.Use();
                    if (GrowthOpenNode(n.id)) Cue($"{n.name} · {n.desc}", 1.8f);
                    else Cue(Growth.Blocker(n) ?? "", 1.2f);
                }
            }
            if (hover == null && growthHoverForTest != null) { var hn = Growth.Node(growthHoverForTest); if (hn != null) { hover = hn; hoverAt = At(hn.pos); } }
            // legend (under the background)
            float lx = area.x + 4, ly = area.yMax + 4;
            for (int b = 0; b < 5; b++) { GUI.color = Growth.BranchColors[b]; GUI.DrawTexture(new Rect(lx, ly + 5, 12, 12), gDisc); GUI.Label(new Rect(lx + 16, ly, 60, 22), Growth.BranchNames[b], gSmall); lx += 64; }
            GUI.color = new Color(.7f, .75f, .8f); GUI.Label(new Rect(lx + 8, ly, area.xMax - lx - 8, 22), "밝음 = 열림 · 파란 테두리 = 열 수 있음 · 회색 = 잠김 · 숫자 = 비용(포인트)", gSmall);
            // tooltip next to the hovered node: framed card with the branch node art, name, cost chip, effect, status
            if (hover != null)
            {
                var col = Growth.BranchColors[hover.branch]; string blk = Growth.Blocker(hover); var hst = Growth.StateOf(hover);
                float tw = 300, th = 164;
                var tr = new Rect(hoverAt.x + R + 20, hoverAt.y - th / 2, tw, th);
                if (tr.xMax > area.xMax) tr.x = hoverAt.x - R - 20 - tw;
                tr.y = Mathf.Clamp(tr.y, area.y, area.yMax - th);
                Card(tr, col);
                var ic = new Rect(tr.x + 26, tr.y + 22, 46, 46);
                if (!NodeArt(ic, hst == Growth.State.Locked ? Growth.State.Available : hst, hover.branch)) { GUI.color = col; GUI.DrawTexture(ic, gDisc); }
                GUI.color = col; GUI.Label(new Rect(ic.xMax + 10, tr.y + 20, tw - 150, 26), hover.name, gTitle);
                GUI.color = new Color(.72f, .78f, .84f); GUI.Label(new Rect(ic.xMax + 10, tr.y + 46, tw - 150, 20), Growth.BranchNames[hover.branch] + " 가지", gSmall);
                // cost chip (top-right)
                var chip = new Rect(tr.xMax - 70, tr.y + 24, 44, 22);
                GUI.color = new Color(.03f, .05f, .07f, .95f); GUI.DrawTexture(chip, white);
                GUI.color = new Color(col.r, col.g, col.b, .9f); GUI.DrawTexture(new Rect(chip.x, chip.yMax - 2, chip.width, 2), white);
                GUI.color = Color.white; GUI.Label(chip, $"{hover.cost} P", gNum);
                // divider + effect
                GUI.color = new Color(col.r, col.g, col.b, .45f); GUI.DrawTexture(new Rect(tr.x + 26, tr.y + 78, tw - 52, 1), white);
                GUI.color = Color.white; GUI.Label(new Rect(tr.x + 26, tr.y + 86, tw - 52, 40), hover.desc, gBody);
                // status
                GUI.color = blk == null ? new Color(.5f, 1, .6f) : blk == "열림" ? col : new Color(1, .62f, .5f);
                GUI.Label(new Rect(tr.x + 26, tr.yMax - 42, tw - 80, 22), blk == null ? "▶ 클릭해서 열기" : blk == "열림" ? "✔ 열림" : "✖ " + blk, gSmallBold);
                if (hover.part != Parts.None) { GUI.color = Parts.Has(hover.part) ? Color.white : new Color(1, 1, 1, .45f); UiArt.Icon(new Rect(tr.xMax - 54, tr.yMax - 48, 28, 28), UiArt.PartIcon(hover.part)); }
            }
            // side panel: how-to + current totals with icons
            var side = new Rect(body.xMax - sideW, body.y, sideW, body.height);
            Card(side, new Color(.45f, .8f, 1f));
            float sy = side.y + 24, sx = side.x + 26, sw = sideW - 52;
            GUI.color = new Color(.6f, .9f, 1); GUI.Label(new Rect(sx, sy, sw, 24), "성장", gTitle); sy += 26;
            GUI.color = new Color(.74f, .8f, .86f);
            GUI.Label(new Rect(sx, sy, sw, 60), "노드에 마우스를 올리면 설명, 클릭하면 엽니다. 포인트는 적을 처치해 경험치가 쌓이면 생깁니다. 슬롯 확장 노드는 그 부위 슬롯을 1칸 늘립니다.", gBody);
            // xp bar to the next point
            sy += 62;
            int prevAt = Growth.Threshold(Growth.Earned()), nextAt = Growth.NextAt; float f = Mathf.Clamp01((Growth.xp - prevAt) / (float)Mathf.Max(1, nextAt - prevAt));
            GUI.color = new Color(.05f, .08f, .1f, 1); GUI.DrawTexture(new Rect(sx, sy, sw, 8), white);
            GUI.color = new Color(.35f, .8f, 1f); GUI.DrawTexture(new Rect(sx, sy, sw * f, 8), white);
            GUI.color = new Color(.74f, .8f, .86f); GUI.Label(new Rect(sx, sy + 9, sw, 20), $"다음 포인트까지 {nextAt - Growth.xp}", gSmall);
            var rows = new (int icon, string label, string value)[]
            {
                (UiArt.CORE, "최대 체력", $"{player.max:F0}"),
                (UiArt.SLASH, "평타 피해", $"×{Growth.DamageScale("light1"):F2}"),
                (UiArt.SLASH, "3타 피해", $"×{Growth.DamageScale("light3"):F2}"),
                (UiArt.WHIRL, "평타 범위", $"×{Growth.ReachScale:F2}"),
                (UiArt.DASH, "이동 속도", $"×{Growth.SpeedScale:F2}"),
                (UiArt.DASH, "대쉬 쿨다운", $"{COOLDOWN[KeyCode.Space]:F2}초"),
                (UiArt.PARRY, "반격 판정", $"+{Growth.CounterWindow:F2}초"),
                (UiArt.POD, "받는 피해", $"×{Growth.TakenScale:F2}"),
                (UiArt.SHRINE, "지도 공개", $"×{Growth.FogScale:F2}"),
            };
            float rh = 22, top = side.yMax - 28 - rows.Length * rh;
            sy = Mathf.Max(sy + 30, top - 28);
            GUI.color = new Color(.6f, .9f, 1); GUI.Label(new Rect(sx, sy, sw, 22), $"현재 능력", gSmallBold);
            GUI.color = new Color(.74f, .8f, .86f); GUI.Label(new Rect(sx, sy, sw, 22), $"노드 {Growth.open.Count}/{Growth.Nodes.Length}", gNumRight);
            sy += 26;
            for (int i = 0; i < rows.Length; i++)
            {
                var rr2 = new Rect(sx, sy, sw, rh);
                if (i % 2 == 0) { GUI.color = new Color(1, 1, 1, .04f); GUI.DrawTexture(rr2, white); }
                GUI.color = Color.white; if (!UiArt.Icon(new Rect(sx + 2, sy + 2, rh - 4, rh - 4), rows[i].icon)) { GUI.color = new Color(.4f, .7f, .9f); GUI.DrawTexture(new Rect(sx + 6, sy + 7, 8, 8), gDisc); }
                GUI.color = new Color(.8f, .85f, .9f); GUI.Label(new Rect(sx + rh + 4, sy, sw - rh - 70, rh), rows[i].label, gSmall);
                GUI.color = Color.white; GUI.Label(new Rect(sx, sy, sw - 4, rh), rows[i].value, gNumRight);
                sy += rh;
            }
        }

        // ---------- typography / cards ----------
        static Font gFont; static bool gFontTried, gWarm;
        static void WarmFont()
        {
            var sb = new System.Text.StringBuilder("0123456789+-−×.,·:/()%[]?!▶✔✖◆ PLvTabEsc 성장노드트리스킬파츠경험치포인트지갑기본부품닫기다음까지현재능력최대체력평타피해범위이동속도대쉬쿨다운반격판정받는지도공개초가지비용클릭해서열기열림먼저필요부족강화레벨설명마우스를올리면주워쌓이면생깁니다밝음파란테두리있음회색잠김숫자키변경설정창에서합니다쓰는아직없다찾아몸에달면생긴지속회복");
            foreach (var n in Growth.Nodes) sb.Append(n.name).Append(n.desc);
            foreach (var b in Growth.BranchNames) sb.Append(b);
            foreach (var d in Skills.Defs) sb.Append(d.name).Append(d.part);
            foreach (var pk in Skills.Perks) sb.Append(pk.desc);
            foreach (var pn in Parts.Names) sb.Append(pn);
            string all = sb.ToString();
            foreach (int size in new[] { 12, 13, 17, 21 }) { gFont.RequestCharactersInTexture(all, size, FontStyle.Normal); gFont.RequestCharactersInTexture(all, size, FontStyle.Bold); }
        }
        GUIStyle gTitle, gBody, gSmall, gSmallBold, gNum, gNumRight;
        /// growth-screen font: Resources/UI/Fonts/ui_font (bundle a licensed Korean .ttf/.otf there), else Unity's default font
        void GrowthStyles()
        {
            if (!gFontTried)
            {
                gFontTried = true;
                gFont = Resources.Load<Font>("UI/Fonts/ui_font");
                // (Font.CreateDynamicFontFromOSFont garbles IMGUI labels here - every label drew the same cached string -
                // so without a bundled font the default font is used)
            }
            if (gFont && !gWarm) { gWarm = true; WarmFont(); }
            if (gTitle != null) return;
            var b = new GUIStyle(GUI.skin.label) { font = gFont, fontSize = 13, wordWrap = false, richText = false, normal = { textColor = Color.white }, padding = new RectOffset(0, 0, 0, 0) };
            gTitle = new GUIStyle(b) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            gBody = new GUIStyle(b) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft };
            gSmall = new GUIStyle(b) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            gSmallBold = new GUIStyle(gSmall) { fontStyle = FontStyle.Bold };
            gNum = new GUIStyle(b) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            gNumRight = new GUIStyle(gNum) { alignment = TextAnchor.MiddleRight };
        }
        /// small framed card: the ui_panel window frame shrunk (9-slice), with a dark fill and a branch-coloured top line
        void Card(Rect r, Color accent)
        {
            GUI.color = new Color(.02f, .03f, .045f, .93f); GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), white);
            if (gPanel) { GUI.color = Color.white; Nine(r, gPanel, SZ_PANEL, P_PANEL, 120, 120, 120, 120, .16f); }
            else { GUI.color = new Color(accent.r, accent.g, accent.b, .5f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), white); }
            GUI.color = new Color(accent.r, accent.g, accent.b, .85f); GUI.DrawTexture(new Rect(r.x + 18, r.y + 6, r.width - 36, 2), white);
        }

        // ---------- tab 1: 부위 슬롯 (2026-11-10) ----------
        string slotSel;   // selected skill id (detail card on the right)
        /// swapping happens at a charging pod (outside a boss fight)
        bool AtPod => NearPod(4.2f) != null && fightArena == null;
        static readonly Color[] SlotColors = { new Color(.8f, .6f, 1f), new Color(1f, .45f, .35f), new Color(1f, .8f, .35f), new Color(.45f, .85f, 1f), new Color(.5f, 1f, .55f) };

        /// equip / unequip by id (UI clicks and MCP tests); `force` skips the pod rule
        public string SlotToggle(string id, bool force = false)
        {
            var o = Skills.Get(id); if (o == null) return "없는 스킬";
            if (o.def.grudge) return "원혼 파츠는 뗄 수 없다";
            if (!force && !AtPod) return "충전 포드 앞에서만 바꿀 수 있다";
            if (o.equipped) { Skills.Unequip(o); return $"{o.def.name} 해제"; }
            var blk = Skills.EquipBlocker(o); if (blk != null) return blk;
            Skills.Equip(o);
            if (o.Active && KeyBinds.AutoSkillKeys) KeyBinds.EnsureSkillKey(o);   // WASD: the next free slot key (also when its old key was taken meanwhile)
            if (o.Active && o.key == KeyCode.None) { bindPending = o; return $"{o.def.name} 장착 — 사용할 키를 누르세요"; }
            return o.Active ? $"{o.def.name} 장착 · {KeyName(o.key)} 키" : $"{o.def.name} 장착";
        }

        void SlotsTab(Rect body, Event ev)
        {
            GUI.color = new Color(.02f, .035f, .05f, gPanel ? .55f : .9f); GUI.DrawTexture(body, white);
            float sideW = Mathf.Min(300, body.width * .3f);
            var left = new Rect(body.x + 14, body.y + 10, body.width - sideW - 36, body.height - 20);
            // status line
            bool pod = AtPod;
            GUI.color = pod ? new Color(.55f, 1, .65f) : new Color(1, .7f, .5f);
            GUI.Label(new Rect(left.x, left.y, left.width, 20), pod ? "◆ 충전 포드 — 클릭해서 장착 / 해제" : "◆ 교체는 충전 포드 앞에서만 할 수 있다 (강화는 어디서나)", gSmallBold);
            GUI.color = Skills.ActiveCount >= Skills.MAX_ACTIVE ? new Color(1, .75f, .5f) : new Color(.8f, .86f, .92f);
            GUI.Label(new Rect(left.x, left.y, left.width, 20), $"액티브 {Skills.ActiveCount} / {Skills.MAX_ACTIVE}   ·   기본 부품 {scrap}", gNumRight);
            // five part columns
            float colW = (left.width - 4 * 10) / 5, slotH = 62, cy = left.y + 30;
            Skills.Owned hover = null;
            for (int p = 0; p < 5; p++)
            {
                var s = (Slot)p; var col = SlotColors[p];
                float cx = left.x + p * (colW + 10);
                GUI.color = col; GUI.Label(new Rect(cx, cy, colW, 22), Skills.SlotNames[p], gTitle);
                GUI.color = new Color(.7f, .76f, .82f); GUI.Label(new Rect(cx, cy, colW, 22), $"{Skills.Used(s)}/{Skills.SlotCount(s)}", gNumRight);
                var eq = Skills.Ordered(s);   // slot order 1-2-3 (회로 연결 neighbours)
                for (int k = 0; k < Skills.MAX_SLOTS; k++)
                {
                    var r = new Rect(cx, cy + 26 + k * (slotH + 6), colW, slotH);
                    bool open = k < Skills.SlotCount(s);
                    GUI.color = open ? new Color(.07f, .1f, .13f, .95f) : new Color(.04f, .045f, .05f, .8f); GUI.DrawTexture(r, white);
                    GUI.color = open ? new Color(col.r, col.g, col.b, .55f) : new Color(.3f, .32f, .35f, .5f);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), white); GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), white);
                    if (!open) { GUI.color = new Color(.45f, .48f, .52f); GUI.Label(r, "잠김 · 노드로 확장", new GUIStyle(gSmall) { alignment = TextAnchor.MiddleCenter }); continue; }
                    if (k >= eq.Count) { GUI.color = new Color(.45f, .5f, .55f); GUI.Label(r, "빈 슬롯", new GUIStyle(gSmall) { alignment = TextAnchor.MiddleCenter }); continue; }
                    var o = eq[k]; bool over = r.Contains(ev.mousePosition); if (over) hover = o;
                    SkillChip(r, o, col, over || slotSel == o.def.id);
                    if (over && ev.type == EventType.MouseDown && ev.button == 0) { ev.Use(); slotSel = o.def.id; if (pod) Cue(SlotToggle(o.def.id), 1.6f); }
                }
                // grudge parts of this body part: outside the slots, permanent
                float gy = cy + 26 + Skills.MAX_SLOTS * (slotH + 6);
                foreach (var o in Skills.owned)
                {
                    if (!o.def.grudge || o.def.slot != s) continue;
                    var r = new Rect(cx, gy, colW, 34);
                    GUI.color = new Color(.25f, .04f, .05f, .9f); GUI.DrawTexture(r, white);
                    GUI.color = new Color(1, .45f, .4f); GUI.Label(new Rect(r.x + 8, r.y, r.width - 10, r.height), "● " + o.def.name, gSmallBold);
                    if (r.Contains(ev.mousePosition)) { hover = o; if (ev.type == EventType.MouseDown && ev.button == 0) { ev.Use(); slotSel = o.def.id; } }
                    gy += 38;
                }
            }
            // storage: owned, not equipped
            float sy = cy + 26 + Skills.MAX_SLOTS * (slotH + 6) + 48;
            GUI.color = new Color(.75f, .85f, .95f); GUI.Label(new Rect(left.x, sy, 300, 22), "보관함", gTitle); sy += 26;
            int shown = 0; float bw = (left.width - 3 * 8) / 4, bh = 48;
            foreach (var o in Skills.owned)
            {
                if (o.equipped || o.def.grudge) continue;
                var r = new Rect(left.x + (shown % 4) * (bw + 8), sy + (shown / 4) * (bh + 6), bw, bh); shown++;
                if (r.yMax > left.yMax) break;
                bool over = r.Contains(ev.mousePosition); if (over) hover = o;
                GUI.color = new Color(.06f, .08f, .1f, .95f); GUI.DrawTexture(r, white);
                SkillChip(r, o, SlotColors[(int)o.def.slot], over || slotSel == o.def.id);
                if (over && ev.type == EventType.MouseDown && ev.button == 0) { ev.Use(); slotSel = o.def.id; if (pod) Cue(SlotToggle(o.def.id), 1.6f); }
            }
            if (shown == 0) { GUI.color = new Color(.55f, .6f, .65f); GUI.Label(new Rect(left.x, sy, left.width, 20), Skills.owned.Count == 0 ? "아직 스킬이 없다. 엘리트, 숨은 곳, 상인에게서 부품을 얻으면 생긴다." : "보관 중인 스킬 없음", gSmall); }
            // detail card
            var sel = Skills.Get(hover != null ? hover.def.id : slotSel ?? "");
            var side = new Rect(body.xMax - sideW - 10, body.y + 10, sideW, body.height - 20);
            Card(side, sel != null ? SlotColors[(int)sel.def.slot] : new Color(.45f, .8f, 1f));
            float x = side.x + 24, w = side.width - 48, yy = side.y + 22;
            if (sel == null)
            {
                GUI.color = new Color(.74f, .8f, .86f);
                GUI.Label(new Rect(x, yy, w, 200), "스킬은 정해진 부위 슬롯에 넣어야 작동한다. 슬롯은 부위마다 1칸에서 시작해 노드로 3칸까지 늘어난다. 액티브는 동시에 6개까지. 원혼 파츠는 슬롯 밖에 붙는다.", gBody);
                return;
            }
            var c = SlotColors[(int)sel.def.slot];
            var ic = new Rect(x, yy, 54, 54); int idx = AtlasIndex(sel.def.id);
            GUI.color = Color.white; if (!PartIconArt(ic, PartIconIndex(sel.def.id)) && !SkillIconArt(ic, idx) && !UiArt.Icon(ic, sel.def.icon)) { GUI.color = c; GUI.DrawTexture(ic, gDisc); }
            GUI.color = c; GUI.Label(new Rect(ic.xMax + 10, yy + 2, w - 64, 24), sel.def.name, gTitle);
            GUI.color = new Color(.72f, .78f, .84f);
            GUI.Label(new Rect(ic.xMax + 10, yy + 28, w - 64, 20), $"{Skills.SlotNames[(int)sel.def.slot]} · {(sel.def.grudge ? "붉은 부품" : sel.def.passive ? "패시브" : "액티브")} · {sel.def.part}", gSmall);
            GUI.color = Skills.GradeColors[sel.def.grade]; GUI.Label(new Rect(ic.xMax + 10, yy + 46, w - 64, 18), Skills.GradeNames[sel.def.grade] + (sel.def.link != Link.None ? "  ◆ 회로" : "") + (sel.def.costly ? "  ⚠ 대가" : ""), gSmall);
            yy += 68;
            GUI.color = Color.white; float dh = gBody.CalcHeight(new GUIContent(sel.def.desc), w); GUI.Label(new Rect(x, yy, w, dh), sel.def.desc, gBody); yy += dh + 6;
            GUI.color = new Color(.8f, .85f, .9f);
            string st = sel.def.passive ? $"Lv {sel.level} / {Skills.MAX_LEVEL}" : $"Lv {sel.level} / {Skills.MAX_LEVEL}  ·  피해 ×{Skills.DamageScale(sel):F2}  ·  쿨 {Skills.Cooldown(sel, sel.def.id == "counter" ? Growth.CounterCdScale : 1):F1}초";
            GUI.Label(new Rect(x, yy, w, 20), st, gSmall); yy += 22;
            if (sel.Active) { GUI.Label(new Rect(x, yy, w, 20), $"키 {(sel.key == KeyCode.None ? "-" : KeyName(sel.key))}  (Esc 설정 · 키 설정)", gSmall); yy += 22; }
            foreach (var pk in Skills.Perks)
            {
                if (pk.skill != sel.def.id) continue;
                bool on = sel.level >= pk.level; GUI.color = on ? new Color(.55f, 1, .65f) : new Color(.5f, .55f, .6f);
                GUI.Label(new Rect(x, yy, w, 20), (on ? "✔ " : "· ") + $"Lv{pk.level} {pk.desc}", gSmall); yy += 20;
            }
            if (sel.def.lv3 != null)
            {   // Lv3 fixed, Lv5 two branches (click to pick at a pod)
                void Line(string head, string text, bool on, bool picked, int branch)
                {
                    float h = gSmall.CalcHeight(new GUIContent(head + text), w - 6); var r = new Rect(x, yy, w, h + 2);
                    if (picked) { GUI.color = new Color(.3f, .6f, 1f, .18f); GUI.DrawTexture(r, white); }
                    GUI.color = on ? new Color(.55f, 1, .65f) : picked ? new Color(.8f, .88f, 1f) : new Color(.55f, .6f, .66f);
                    GUI.Label(new Rect(x + 3, yy, w - 6, h + 2), head + text, gSmall);
                    if (branch > 0 && ev.type == EventType.MouseDown && ev.button == 0 && r.Contains(ev.mousePosition))
                    {
                        ev.Use();
                        if (!pod) Cue("갈래는 충전 포드 앞에서 고른다", 1.4f);
                        else { sel.branch = branch; Cue($"{sel.def.name} · Lv5 {(branch == 1 ? "①" : "②")}", 1.2f); }
                    }
                    yy += h + 4;
                }
                Line("Lv3  ", sel.def.lv3, sel.Lv3, false, 0);
                Line("Lv5 ①  ", sel.def.lv5a, sel.B1, sel.branch != 2, 1);
                Line("Lv5 ②  ", sel.def.lv5b, sel.B2, sel.branch == 2, 2);
                if (pod && sel.equipped && Skills.Used(sel.def.slot) > 1)
                {
                    var sb = new Rect(x, yy + 2, w, 24);
                    GUI.color = sb.Contains(ev.mousePosition) ? new Color(.6f, .85f, 1f) : new Color(.5f, .65f, .8f);
                    GUI.Label(sb, $"▶ 칸 옮기기 (지금 {sel.pos + 1}번 칸, 옆칸끼리 회로 연결)", gSmall);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && sb.Contains(ev.mousePosition)) { ev.Use(); Skills.Shift(sel); }
                    yy += 26;
                }
            }
            GUI.color = sel.On ? new Color(.55f, 1, .65f) : new Color(1, .75f, .5f);
            GUI.Label(new Rect(x, yy + 4, w, 20), sel.def.grudge ? "● 영구 장착 (뗄 수 없음)" : sel.equipped ? "● 장착 중" : "○ 보관 중 — " + (Skills.EquipBlocker(sel) ?? "장착 가능"), gSmallBold);
            // buttons: upgrade (anywhere), equip / unequip (pod)
            var ub = new Rect(x, side.yMax - 104, w, 40);
            bool max = sel.level >= Skills.MAX_LEVEL; int cost = Skills.Cost(sel); bool can = !max && scrap >= cost;
            Piece(ub, max || !can ? P_BTN_OFF : ub.Contains(ev.mousePosition) ? P_BTN_HI : P_BTN, 60, 45, max ? new Color(.2f, .22f, .25f) : can ? new Color(.2f, .55f, .9f) : new Color(.25f, .25f, .3f));
            GUI.color = can || max ? Color.white : new Color(1, .65f, .55f);
            GUI.Label(ub, max ? "최대 레벨" : $"강화  ·  기본 부품 {cost}", new GUIStyle(gNum) { fontSize = 13 });
            if (ev.type == EventType.MouseDown && ev.button == 0 && ub.Contains(ev.mousePosition))
            {
                ev.Use();
                if (Skills.LevelUp(sel, ref scrap)) Cue($"{sel.def.name} Lv {sel.level}", 1.4f);
                else if (!max) Cue($"기본 부품이 부족하다 ({scrap}/{cost})", 1.2f);
            }
            if (!sel.def.grudge)
            {
                var eb = new Rect(x, side.yMax - 56, w, 40);
                bool ok = pod && (sel.equipped || Skills.EquipBlocker(sel) == null);
                Piece(eb, !ok ? P_BTN_OFF : eb.Contains(ev.mousePosition) ? P_BTN_HI : P_BTN, 60, 45, ok ? new Color(.2f, .55f, .9f) : new Color(.25f, .25f, .3f));
                GUI.color = ok ? Color.white : new Color(.6f, .64f, .68f);
                GUI.Label(eb, sel.equipped ? "해제" : "장착", new GUIStyle(gNum) { fontSize = 13 });
                if (ev.type == EventType.MouseDown && ev.button == 0 && eb.Contains(ev.mousePosition)) { ev.Use(); Cue(SlotToggle(sel.def.id), 1.6f); }
            }
        }
        /// one skill in a slot / storage cell: icon, name, Lv, active/passive tag
        void SkillChip(Rect r, Skills.Owned o, Color col, bool hi)
        {
            if (hi) { GUI.color = new Color(col.r, col.g, col.b, .18f); GUI.DrawTexture(r, white); }
            if (!o.def.grudge) { var gc = Skills.GradeColors[o.def.grade]; GUI.color = new Color(gc.r, gc.g, gc.b, .9f); GUI.DrawTexture(new Rect(r.x, r.y, 3, r.height), white); }   // 등급 색
            float s = Mathf.Min(r.height - 12, 44); var ic = new Rect(r.x + 6, r.y + (r.height - s) / 2, s, s);
            int idx = AtlasIndex(o.def.id);
            GUI.color = Color.white; if (!PartIconArt(ic, PartIconIndex(o.def.id)) && !SkillIconArt(ic, idx) && !UiArt.Icon(ic, o.def.icon)) { GUI.color = col; GUI.DrawTexture(ic, gDisc); }
            float tx = ic.xMax + 8, tw = r.xMax - tx - 4;
            GUI.color = Color.white; GUI.Label(new Rect(tx, r.y + r.height / 2 - 19, tw, 20), o.def.name, gSmallBold);
            GUI.color = o.def.passive ? new Color(.6f, .9f, .7f) : new Color(.6f, .8f, 1f);
            GUI.Label(new Rect(tx, r.y + r.height / 2 + 1, tw, 18), $"{(o.def.passive ? "패시브" : "액티브")} · Lv {o.level}" + (o.Active && o.equipped && o.key != KeyCode.None ? $" · {KeyName(o.key)}" : ""), gSmall);
        }

        // ---------- tab 2: 모듈 업그레이드 (movement, always on) ----------
        static readonly (int part, string what)[] ModuleRows =
        {
            (Parts.Rope, "벽 너머 고정점에 로프를 걸어 끌려 올라간다"),
            (Parts.Leap, "점프 중에 한 번 더 점프 (이단 점프)"),
            (Parts.Jet, "제트 분사로 넓은 틈을 건넌다"),
            (Parts.Cannon, "캐논 포로 잔해 벽을 부순다"),
            (Parts.Magnet, "자석 다리로 철 외벽을 탄다"),
            (Parts.Toxic, "내오염 코어로 슬러지를 건넌다"),
            (Parts.LabKeys, "연구소 문을 연다"),
        };
        void ModulesTab(Rect body, Event ev)
        {
            GUI.color = new Color(.02f, .035f, .05f, gPanel ? .55f : .9f); GUI.DrawTexture(body, white);
            float x = body.x + 18, w = body.width - 36, yy = body.y + 14;
            GUI.color = new Color(.8f, .85f, .9f);
            GUI.Label(new Rect(x, yy, w, 20), "모듈 업그레이드는 몸의 제어 소프트웨어에 새겨진다. 슬롯과 상관없이 항상 켜져 있다.", gSmall); yy += 30;
            float rh = Mathf.Min(72, (body.height - 60) / ModuleRows.Length);
            foreach (var m in ModuleRows)
            {
                bool has = Parts.Has(m.part);
                var row = new Rect(x, yy, w, rh - 8);
                GUI.color = has ? new Color(.06f, .12f, .14f, .9f) : new Color(.04f, .05f, .06f, .8f); GUI.DrawTexture(row, white);
                GUI.color = has ? new Color(.45f, .95f, 1f, .7f) : new Color(.3f, .33f, .36f, .5f); GUI.DrawTexture(new Rect(row.x, row.y, 3, row.height), white);
                var ic = new Rect(row.x + 14, row.y + (row.height - 40) / 2, 40, 40);
                GUI.color = has ? Color.white : new Color(1, 1, 1, .25f); UiArt.Icon(ic, UiArt.PartIcon(m.part));
                GUI.color = has ? Color.white : new Color(.5f, .54f, .58f);
                GUI.Label(new Rect(ic.xMax + 14, row.y + row.height / 2 - 20, 300, 22), has ? Parts.Names[m.part] : "??? 미획득", gTitle);
                GUI.color = has ? new Color(.75f, .82f, .88f) : new Color(.45f, .48f, .52f);
                GUI.Label(new Rect(ic.xMax + 14, row.y + row.height / 2 + 2, w - 200, 20), has ? m.what : "어딘가에서 이 모듈을 얻을 수 있다", gSmall);
                GUI.color = has ? new Color(.55f, 1, .65f) : new Color(.45f, .48f, .52f);
                GUI.Label(new Rect(row.xMax - 140, row.y, 126, row.height), has ? "● 항상 켜짐" : "○ 없음", gNumRight);
                yy += rh;
            }
        }
    }
}
