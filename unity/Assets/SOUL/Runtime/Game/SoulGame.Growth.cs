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

        /// basic parts came in (pickup, pod cleanse reward): wallet AND cumulative xp
        public void GainScrap(int n) { if (n <= 0) return; scrap += n; Growth.Gain(n); }

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

        float CounterWindowBonus => Growth.CounterWindow + Skills.Perk("counter", Skills.PerkKind.Window);
        float RopeRange => RopeAnchors.RANGE * Growth.RopeRangeScale;

        // ---------- effects ----------
        void GrowthApply(float dt)
        {
            float want = Growth.MaxHp;
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
            COOLDOWN[KeyCode.Space] = cdBase[KeyCode.Space] * Growth.DashCdScale;
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
        /// ui_skill_icons.png: 4 × 2 cells of 443.5 px, order of Skills.Defs (반격, 회오리, 레이저 파동, 칼날 폭풍, 코어 과부하, 로프 팔, ...)
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
            string[] tabs = { "노드 트리", "스킬 파츠" };
            for (int i = 0; i < 2; i++)
            {
                var tr = new Rect(x + pad + 90 + i * 140, y + pad - 6, 132, 34);
                Piece(tr, growthTab == i ? P_TAB_ON : P_TAB, 60, 24, growthTab == i ? new Color(.3f, .7f, 1f, .9f) : new Color(.15f, .2f, .25f, .9f));
                GUI.color = growthTab == i ? Color.white : new Color(.7f, .78f, .85f); GUI.Label(tr, $"{i + 1}  {tabs[i]}", new GUIStyle(gNum) { fontSize = 13 });
                if (ev.type == EventType.MouseDown && ev.button == 0 && tr.Contains(ev.mousePosition)) { growthTab = i; ev.Use(); }
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + pad + 380, y + pad - 2, w - 2 * pad - 390, 24),
                $"경험치 {Growth.xp}   ·   포인트 {Growth.Points}   ·   지갑 기본 부품 {scrap}   ·   {KeyBinds.Name(KeyBinds.Act.Growth)} / Esc 닫기", new GUIStyle(gNumRight) { fontSize = 13 });
            var body = new Rect(x + pad, y + pad + 36, w - 2 * pad, h - 2 * pad - 36);
            if (growthTab == 0) TreeTab(body, ev); else PartsTab(body, ev);
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
            GUI.Label(new Rect(sx, sy, sw, 60), "노드에 마우스를 올리면 설명, 클릭하면 엽니다. 포인트는 기본 부품을 주워 경험치가 쌓이면 생깁니다.", gBody);
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

        void PartsTab(Rect body, Event ev)
        {
            GUI.color = new Color(.02f, .035f, .05f, gPanel ? .55f : .9f); GUI.DrawTexture(body, white);
            float yy = body.y + 14, x = body.x + 18, w = body.width - 36;
            GUI.color = new Color(.8f, .85f, .9f);
            GUI.Label(new Rect(x, yy, w, 20), $"스킬 파츠 강화는 지갑의 기본 부품을 씁니다 (Lv2 {Skills.UPGRADE_COST[0]} · Lv3 {Skills.UPGRADE_COST[1]} · Lv4 {Skills.UPGRADE_COST[2]} · Lv5 {Skills.UPGRADE_COST[3]}).  키 변경은 설정 창(Esc)의 키 설정에서 합니다.", gSmall);
            yy += 32;
            if (Skills.owned.Count == 0) { GUI.color = Color.white; GUI.Label(new Rect(x, yy, w, 20), "아직 스킬이 없다. 파츠를 찾아 몸에 달면 스킬이 생긴다.", gSmall); return; }
            float rowH = Mathf.Min(96, (body.height - 60) / Mathf.Max(1, Skills.owned.Count));
            foreach (var o in Skills.owned)
            {
                var row = new Rect(x, yy, w, rowH - 8);
                GUI.color = new Color(.08f, .12f, .16f, .8f); GUI.DrawTexture(row, white);
                var ic = new Rect(row.x + 10, row.y + (row.height - 56) / 2, 56, 56);
                int idx = System.Array.IndexOf(Skills.Defs, o.def);
                GUI.color = Color.white; if (!SkillIconArt(ic, idx) && !UiArt.Icon(ic, o.def.icon)) { GUI.color = new Color(.3f, .6f, .9f); GUI.DrawTexture(ic, white); }
                float tx = ic.xMax + 14;
                GUI.color = Color.white;
                GUI.Label(new Rect(tx, row.y + 8, 360, 22), $"{o.def.name}   Lv {o.level} / {Skills.MAX_LEVEL}", gTitle);
                GUI.color = new Color(.75f, .8f, .85f);
                float cd = Skills.Cooldown(o, o.def.id == "counter" ? Growth.CounterCdScale : 1);
                string main = o.def.id == "overload" ? $"지속 {Skills.OVERLOAD_TIME + .5f * (o.level - 1) + Skills.Perk(o, Skills.PerkKind.Duration):F1}초 · 초당 회복 {(Skills.OVERLOAD_REGEN + Skills.Perk(o, Skills.PerkKind.Regen)) * 100:F0}%" : $"피해 ×{Skills.DamageScale(o):F2}";
                GUI.Label(new Rect(tx, row.y + 30, 520, 20), $"{o.def.part}  ·  키 {(o.key == KeyCode.None ? "-" : KeyName(o.key))}  ·  {main}  ·  쿨다운 {cd:F1}초", gSmall);
                // perks
                float px = tx;
                foreach (var p in Skills.Perks)
                {
                    if (p.skill != o.def.id) continue;
                    bool on = o.level >= p.level; string t = $"Lv{p.level} {p.desc}";
                    GUI.color = on ? new Color(.55f, 1, .65f) : new Color(.5f, .55f, .6f);
                    GUI.Label(new Rect(px, row.y + 52, 200, 20), (on ? "✔ " : "· ") + t, gSmall); px += 200;
                }
                // upgrade
                var br = new Rect(row.xMax - 190, row.y + (row.height - 40) / 2, 176, 40);
                bool max = o.level >= Skills.MAX_LEVEL; int cost = Skills.Cost(o); bool can = !max && scrap >= cost;
                Piece(br, max || !can ? P_BTN_OFF : br.Contains(ev.mousePosition) ? P_BTN_HI : P_BTN, 60, 45, max ? new Color(.2f, .22f, .25f) : can ? new Color(.2f, .55f, .9f) : new Color(.25f, .25f, .3f));
                GUI.color = can || max ? Color.white : new Color(1, .65f, .55f);
                GUI.Label(br, max ? "최대 레벨" : $"강화  ·  기본 부품 {cost}", new GUIStyle(gNum) { fontSize = 13 });
                if (ev.type == EventType.MouseDown && ev.button == 0 && br.Contains(ev.mousePosition))
                {
                    ev.Use();
                    if (Skills.LevelUp(o, ref scrap)) Cue($"{o.def.name} Lv {o.level}", 1.4f);
                    else if (!max) Cue($"기본 부품이 부족하다 ({scrap}/{cost})", 1.2f);
                }
                yy += rowH;
            }
        }
    }
}
