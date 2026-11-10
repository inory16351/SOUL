using System;
using UnityEngine;

namespace Soul
{
    // Esc settings window (user decision 2026-11-08, 기획_결정사항.md '설정 창'). Esc opens / closes it and freezes the
    // game (Update returns before the world step and realTime does not advance). While open, clicks and keys never
    // reach the game. Tabs: 키 설정 (조작 방식 WASD / 클릭 이동, basic controls from KeyBinds + learned skills) · 사운드 ·
    // 화면 (zoom, shake, 카메라 자동 회전 on / off + speed) · 게임.
    // Esc priority: the new-skill key prompt (bindPending) > boss purify skip > close map / Tab growth screen > settings.
    // Art: SettingsArt.cs (Resources/UI/ui_panel.png > UI/ui_kit.png frame > flat IMGUI).
    public partial class SoulGame
    {
        public bool settingsOpen; int settingsTab; bool settingsInit;
        static readonly string[] TabNames = { "키 설정", "사운드", "화면", "게임" };

        /// a row in the key tab: a basic action or a learned skill
        struct BindTarget
        {
            public KeyBinds.Act? act; public Skills.Owned skill;
            public KeyCode Key => act.HasValue ? KeyBinds.Get(act.Value) : skill != null ? skill.key : KeyCode.None;
            public string Name => act.HasValue ? KeyBinds.Label[(int)act.Value] : skill != null ? skill.def.name : "";
            public bool Same(BindTarget o) => act.HasValue ? o.act == act : o.skill != null && o.skill == skill;
            public void Assign(KeyCode k) { if (act.HasValue) KeyBinds.Set(act.Value, k); else if (skill != null) { skill.key = k; KeyBinds.Sync(); } }
        }
        BindTarget? listenFor, conflictFor, conflictHolder; KeyCode conflictKey; string settingsNote = ""; Vector2 keyScroll;
        static readonly Array settingsKeys = Enum.GetValues(typeof(KeyCode));

        // ---------- persistence (PlayerPrefs) ----------
        public float zoomDefault = 1;
        void SettingsInitOnce()
        {
            settingsInit = true; KeyBinds.Load();
            SoulAudio.masterVolume = PlayerPrefs.GetFloat("soul.vol.master", 1);
            SoulAudio.musicVolume = PlayerPrefs.GetFloat("soul.vol.music", .5f);
            SoulAudio.sfxVolume = PlayerPrefs.GetFloat("soul.vol.sfx", .8f);
            zoomDefault = PlayerPrefs.GetFloat("soul.cam.zoom", 1); if (cam != null) cam.zoom = zoomDefault;
            FollowCam.shakeScale = PlayerPrefs.GetFloat("soul.cam.shake", 1);
            FollowCam.followOn = PlayerPrefs.GetInt("soul.cam.follow", 1) == 1;
            FollowCam.followSpeed = Mathf.Clamp(PlayerPrefs.GetFloat("soul.cam.followSpeed", FollowCam.FOLLOW_SPEED_DEFAULT), FollowCam.FOLLOW_SPEED_MIN, FollowCam.FOLLOW_SPEED_MAX);
            debugHud = PlayerPrefs.GetInt("soul.debugHud", 0) == 1;
        }
        public void SettingsSave()
        {
            PlayerPrefs.SetFloat("soul.vol.master", SoulAudio.masterVolume); PlayerPrefs.SetFloat("soul.vol.music", SoulAudio.musicVolume); PlayerPrefs.SetFloat("soul.vol.sfx", SoulAudio.sfxVolume);
            PlayerPrefs.SetFloat("soul.cam.zoom", zoomDefault); PlayerPrefs.SetFloat("soul.cam.shake", FollowCam.shakeScale); PlayerPrefs.SetInt("soul.debugHud", debugHud ? 1 : 0);
            PlayerPrefs.SetInt("soul.cam.follow", FollowCam.followOn ? 1 : 0); PlayerPrefs.SetFloat("soul.cam.followSpeed", FollowCam.followSpeed);
            KeyBinds.Save(); PlayerPrefs.Save();
        }
        /// 조작 방식 (2026-11-10): WASD / click move. Each scheme has its own basic keys; learned skills are re-keyed
        /// (KeyBinds.SetScheme). Pending move orders and the rope hold are dropped so nothing carries over.
        public void SetControlScheme(KeyBinds.Scheme s)
        {
            if (s == KeyBinds.scheme) return;
            KeyBinds.SetScheme(s); moveTarget = null; moveOrder = null; resumeMove = null; ropeHeld = false;
            listenFor = conflictFor = conflictHolder = null;
            settingsNote = s == KeyBinds.Scheme.Wasd ? "WASD 조작으로 바꿨습니다. 스킬 키는 1 2 3 4 Q E 순서로 다시 배치했습니다." : "클릭 이동 조작으로 바꿨습니다. 예전 키 배치(우클릭 이동, A 공격, Space 대쉬, F 점프)입니다.";
        }
        /// MCP tests: forget the in-memory settings and read PlayerPrefs again
        public void SettingsReload() { KeyBinds.Reload(); settingsInit = false; SettingsInitOnce(); }

        public void OpenSettings() { settingsOpen = true; showMap = false; if (growthOpen) SetGrowthOpen(false); listenFor = conflictFor = conflictHolder = null; settingsNote = ""; moveTarget = null; ropeHeld = false; }
        public void CloseSettings() { settingsOpen = false; listenFor = conflictFor = conflictHolder = null; SettingsSave(); }

        /// first in HandleInput; true = this frame's input belongs to the settings window
        bool SettingsInput()
        {
            if (!settingsInit) SettingsInitOnce();
            bool esc = KeyBinds.Pressed(KeyCode.Escape);
            if (settingsOpen)
            {
                if (listenFor.HasValue) { SettingsCapture(); return true; }
                if (conflictFor.HasValue) { if (esc) { conflictFor = conflictHolder = null; settingsNote = "취소했습니다."; } return true; }
                if (esc) CloseSettings();
                return true;
            }
            if (!esc) return false;
            if (bindPending != null) return false;                              // the new-skill prompt: Esc = 키 미지정 (SoulGame.Skills)
            if (purifyT >= 0 && purifyT < PURIFY_CAM_BACK) return false;          // Esc skips the purify cinematic (BossPurify)
            if (showMap) { showMap = false; return true; }
            if (growthOpen) return false;                                       // Esc closes the Tab growth screen (SoulGame.Growth)
            OpenSettings(); return true;
        }

        /// listening for a key: Esc cancels, blocked keys are refused, a used key asks swap / clear
        void SettingsCapture()
        {
            if (KeyBinds.Pressed(KeyCode.Escape)) { listenFor = null; settingsNote = "취소했습니다."; return; }
            foreach (KeyCode k in settingsKeys)
            {
                if (k == KeyCode.None || k >= KeyCode.Mouse0 || !KeyBinds.Pressed(k)) continue;
                SettingsPress(k); return;
            }
        }
        void SettingsPress(KeyCode k)
        {
            var t = listenFor.Value;
            if (KeyBinds.Blocked(k)) { settingsNote = $"{KeyBinds.KeyName(k)} 키는 {(KeyBinds.UsedBy(k) == "이동" ? "이동 키(고정)" : "디버그 / 시스템 키")}라 쓸 수 없습니다. 다른 키를 누르세요."; return; }
            listenFor = null;
            if (k == t.Key) { settingsNote = $"{t.Name} → {KeyBinds.KeyName(k)} (그대로)"; return; }
            var holder = HolderOf(k);
            if (holder.HasValue && !holder.Value.Same(t)) { conflictFor = t; conflictHolder = holder; conflictKey = k; settingsNote = ""; return; }
            t.Assign(k); settingsNote = $"{t.Name} → {KeyBinds.KeyName(k)}"; SettingsSave();
        }
        BindTarget? HolderOf(KeyCode k)
        {
            var a = KeyBinds.ActOf(k); if (a.HasValue) return new BindTarget { act = a };
            var o = Skills.ByKey(k); if (o != null) return new BindTarget { skill = o };
            return null;
        }
        /// swap = the other item takes my old key; otherwise the other item is left empty
        void ResolveConflict(bool swap)
        {
            var t = conflictFor.Value; var h = conflictHolder.Value; var old = t.Key;
            h.Assign(swap ? old : KeyCode.None); t.Assign(conflictKey);
            settingsNote = swap ? $"{t.Name} → {KeyBinds.KeyName(conflictKey)}, {h.Name} → {KeyBinds.KeyName(old)} (교체)" : $"{t.Name} → {KeyBinds.KeyName(conflictKey)}, {h.Name} 키 비움";
            conflictFor = conflictHolder = null; SettingsSave();
        }

        // ---------- MCP test API ----------
        /// run one HandleInput with these keys "pressed" (KeyBinds.fake); returns the animation name after it
        public string InputForTest(params KeyCode[] keys) { KeyBinds.fake.Clear(); KeyBinds.fake.UnionWith(keys); try { HandleInput(); } finally { KeyBinds.fake.Clear(); } return anim.name; }
        /// settings key tab: start listening for a basic action (or skill id) and press k; returns the note / conflict text
        public string RebindForTest(string target, KeyCode k)
        {
            if (!settingsOpen) OpenSettings();
            listenFor = Enum.TryParse<KeyBinds.Act>(target, out var a) ? new BindTarget { act = a } : new BindTarget { skill = Skills.Get(target) };
            SettingsPress(k);
            return conflictFor.HasValue ? $"CONFLICT {conflictHolder.Value.Name}" : settingsNote;
        }
        public void ResolveForTest(bool swap) { if (conflictFor.HasValue) ResolveConflict(swap); }
        public bool Frozen => settingsOpen;
        public float RealTime => realTime;

        // ---------- GUI ----------
        GUIStyle clearBtn, setLabel, setTitle, setCenter;
        void SettingsGUI(float W, float H)
        {
            if (!settingsOpen) return;
            if (clearBtn == null)
            {
                clearBtn = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, normal = { textColor = new Color(.85f, .93f, 1f) } };
                clearBtn.hover.textColor = Color.white; clearBtn.active.textColor = new Color(.5f, .9f, 1f);
                setLabel = new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
                setCenter = new GUIStyle(setLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                setTitle = new GUIStyle(big) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            }
            GUI.color = new Color(0, 0, 0, .62f); GUI.DrawTexture(new Rect(0, 0, W, H), white); GUI.color = Color.white;
            float w = Mathf.Min(760, W - 40), h = Mathf.Min(580, H - 40); var r = new Rect((W - w) / 2, (H - h) / 2, w, h);
            SettingsArt.Panel(r);
            GUI.color = new Color(.75f, .95f, 1); GUI.Label(new Rect(r.x + 34, r.y + 22, 200, 34), "설정", setTitle);
            GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(r.xMax - 260, r.y + 26, 226, 28), "일시정지 · Esc 닫기", new GUIStyle(setLabel) { alignment = TextAnchor.MiddleRight, fontSize = 12 });
            GUI.color = Color.white;
            // tabs
            float tw = (w - 68) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
                if (SettingsArt.Tab(new Rect(r.x + 34 + i * tw, r.y + 62, tw - 6, 36), TabNames[i], settingsTab == i, clearBtn, white)) { settingsTab = i; listenFor = conflictFor = conflictHolder = null; settingsNote = ""; }
            var body = new Rect(r.x + 34, r.y + 112, w - 68, h - 140);
            if (settingsTab == 0) KeysTab(body); else if (settingsTab == 1) SoundTab(body); else if (settingsTab == 2) ScreenTab(body); else GameTab(body);
            GUI.color = Color.white;
        }

        void KeysTab(Rect b)
        {
            // 조작 방식 (2026-11-10): the two schemes side by side, so they can be compared in play
            GUI.Label(new Rect(b.x, b.y, 100, 30), "조작 방식", setLabel);
            for (int i = 0; i < KeyBinds.SchemeLabel.Length; i++)
                if (SettingsArt.Button(new Rect(b.x + 104 + i * 138, b.y, 130, 30), KeyBinds.SchemeLabel[i], clearBtn, white, (int)KeyBinds.scheme == i)) SetControlScheme((KeyBinds.Scheme)i);
            GUI.color = new Color(.6f, .7f, .78f);
            GUI.Label(new Rect(b.x + 104 + 2 * 138 + 6, b.y, b.width - (104 + 2 * 138 + 6), 30), KeyBinds.Wasd ? "WASD 이동 · 좌클릭 공격 · 우클릭 로프" : "우클릭 이동 · A 공격 · 좌클릭 로프", new GUIStyle(setLabel) { fontSize = 12 });
            GUI.color = Color.white;
            b = new Rect(b.x, b.y + 40, b.width, b.height - 40);
            float rowH = 32, footer = 80;
            int skillRows = Skills.owned.FindAll(o => o.Active).Count;   // passives have no key
            bool two = b.width >= 600;   // wide: basic controls | skills side by side
            float basicH = 28 + (KeyBinds.All.Length + 2) * rowH, skillH = 28 + Mathf.Max(1, skillRows) * rowH;
            float contentH = two ? Mathf.Max(basicH, skillH) : basicH + 10 + skillH;
            var view = new Rect(b.x, b.y, b.width, b.height - footer);
            var inner = new Rect(0, 0, b.width - (contentH > view.height ? 20 : 0), Mathf.Max(contentH, view.height));
            keyScroll = GUI.BeginScrollView(view, keyScroll, inner);
            float cw = two ? (inner.width - 24) / 2 : inner.width, y = 0;
            Section(new Rect(0, y, cw, 24), "기본 조작"); y += 28;
            foreach (var a in KeyBinds.All) { KeyRow(new Rect(0, y, cw, rowH - 4), new BindTarget { act = a }); y += rowH; }
            FixedRow(new Rect(0, y, cw, rowH - 4), "이동", KeyBinds.Wasd ? "W A S D" : KeyBinds.KeyName(KeyBinds.ClickMoveButton)); y += rowH;
            FixedRow(new Rect(0, y, cw, rowH - 4), "로프", KeyBinds.KeyName(KeyBinds.RopeButton)); y += rowH;
            float sx = two ? cw + 24 : 0; y = two ? 0 : y + 10;
            Section(new Rect(sx, y, cw, 24), "스킬 (얻은 파츠)"); y += 28;
            if (skillRows == 0) { GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(sx + 8, y, cw, rowH - 4), "아직 얻은 스킬이 없습니다.", setLabel); GUI.color = Color.white; }
            foreach (var o in Skills.owned) { if (!o.Active) continue; KeyRow(new Rect(sx, y, cw, rowH - 4), new BindTarget { skill = o }); y += rowH; }
            GUI.EndScrollView();
            // footer: listening / conflict / note + defaults
            var f = new Rect(b.x, b.yMax - footer + 8, b.width, footer - 8);
            if (conflictFor.HasValue)
            {
                var t = conflictFor.Value; var hd = conflictHolder.Value;
                GUI.color = new Color(1, .8f, .45f);
                GUI.Label(new Rect(f.x, f.y, f.width, 22), $"{KeyBinds.KeyName(conflictKey)} 키는 '{hd.Name}'에 쓰고 있습니다. '{t.Name}'에 어떻게 줄까요?", setLabel);
                GUI.color = Color.white;
                if (SettingsArt.Button(new Rect(f.x, f.y + 30, 200, 34), $"교체 ({hd.Name} → {KeyBinds.KeyName(t.Key)})", clearBtn, white)) ResolveConflict(true);
                if (SettingsArt.Button(new Rect(f.x + 210, f.y + 30, 200, 34), $"'{hd.Name}' 키 비우기", clearBtn, white)) ResolveConflict(false);
                if (SettingsArt.Button(new Rect(f.x + 420, f.y + 30, 110, 34), "취소", clearBtn, white)) { conflictFor = conflictHolder = null; settingsNote = "취소했습니다."; }
                return;
            }
            GUI.color = listenFor.HasValue ? new Color(.5f, .95f, 1) : new Color(.7f, .8f, .88f);
            string msg = listenFor.HasValue ? $"'{listenFor.Value.Name}'에 쓸 키를 누르세요 · Esc 취소" : settingsNote != "" ? settingsNote : "항목을 누르고 새 키를 누르세요. 디버그 키(P, H, C, V, K, X, `, F1~F12)와 마우스 버튼은 지정할 수 없습니다.";
            GUI.Label(new Rect(f.x, f.y, f.width, 22), msg, setLabel); GUI.color = Color.white;
            if (SettingsArt.Button(new Rect(f.x, f.y + 30, 160, 34), "기본값으로", clearBtn, white)) { KeyBinds.ResetDefaults(); listenFor = null; settingsNote = $"{KeyBinds.SchemeLabel[(int)KeyBinds.scheme]} 조작의 기본 키를 기본값으로 돌렸습니다."; SettingsSave(); }
        }
        void Section(Rect r, string text) { GUI.color = new Color(.55f, .85f, 1f); GUI.Label(r, text, new GUIStyle(setLabel) { fontStyle = FontStyle.Bold }); GUI.color = new Color(.4f, .6f, .7f, .5f); GUI.DrawTexture(new Rect(r.x, r.yMax, r.width, 1), white); GUI.color = Color.white; }
        void KeyRow(Rect r, BindTarget t)
        {
            bool listening = listenFor.HasValue && listenFor.Value.Same(t);
            GUI.color = listening ? new Color(.2f, .55f, .7f, .35f) : new Color(1, 1, 1, .04f); GUI.DrawTexture(r, white); GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10, r.y, r.width - 135, r.height), t.Name + (t.skill != null ? $"   <color=#7f99a8>Lv {t.skill.level}</color>" : ""), new GUIStyle(setLabel) { richText = true });
            var cap = new Rect(r.xMax - 120, r.y + 1, 112, r.height - 2);
            string label = listening ? "키를 누르세요…" : KeyBinds.KeyName(t.Key);
            if (t.Key == KeyCode.None && !listening) GUI.color = new Color(1, .7f, .5f);
            if (SettingsArt.Keycap(cap, label, listening, clearBtn, white)) { listenFor = t; conflictFor = conflictHolder = null; settingsNote = ""; }
            GUI.color = Color.white;
        }
        void FixedRow(Rect r, string name, string key)
        {
            GUI.color = new Color(1, 1, 1, .04f); GUI.DrawTexture(r, white);
            GUI.color = new Color(.65f, .72f, .78f); GUI.Label(new Rect(r.x + 10, r.y, r.width * .55f, r.height), name + "   <size=11>(고정)</size>", new GUIStyle(setLabel) { richText = true });
            GUI.Label(new Rect(r.xMax - 120, r.y, 112, r.height), key, new GUIStyle(setLabel) { alignment = TextAnchor.MiddleCenter }); GUI.color = Color.white;
        }

        void SoundTab(Rect b)
        {
            float y = b.y + 6;
            SoulAudio.masterVolume = SliderRow(new Rect(b.x, y, b.width, 40), "전체 음량", SoulAudio.masterVolume, 0, 1, v => $"{v * 100:F0}%"); y += 54;
            SoulAudio.musicVolume = SliderRow(new Rect(b.x, y, b.width, 40), "음악", SoulAudio.musicVolume, 0, 1, v => $"{v * 100:F0}%"); y += 54;
            SoulAudio.sfxVolume = SliderRow(new Rect(b.x, y, b.width, 40), "효과음", SoulAudio.sfxVolume, 0, 1, v => $"{v * 100:F0}%"); y += 64;
            SoulAudio.muted = Toggle(new Rect(b.x, y, b.width, 30), "음소거 (F8)", SoulAudio.muted); y += 44;
            GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(b.x, y, b.width, 22), "값은 바로 적용되고 창을 닫을 때 저장됩니다.", setLabel); GUI.color = Color.white;
        }
        void ScreenTab(Rect b)
        {
            float y = b.y + 6;
            float z = SliderRow(new Rect(b.x, y, b.width, 40), "카메라 줌 기본값", zoomDefault, .55f, 2.2f, v => $"{v:F2}x");
            if (Mathf.Abs(z - zoomDefault) > 1e-4f) { zoomDefault = z; cam.zoom = z; }
            y += 54;
            FollowCam.shakeScale = SliderRow(new Rect(b.x, y, b.width, 40), "화면 흔들림", FollowCam.shakeScale, 0, 1.5f, v => $"{v * 100:F0}%"); y += 58;
            // 조건부 추적 카메라 (2026-11-10, WASD scheme): on / off and the top turn speed (numbers: FollowCam.FOLLOW_*)
            FollowCam.followOn = Toggle(new Rect(b.x, y, b.width, 30), "카메라 자동 회전", FollowCam.followOn); y += 40;
            FollowCam.followSpeed = SliderRow(new Rect(b.x, y, b.width, 40), "자동 회전 속도", FollowCam.followSpeed, FollowCam.FOLLOW_SPEED_MIN, FollowCam.FOLLOW_SPEED_MAX, v => $"초당 {v:F0}°"); y += 58;
            debugHud = Toggle(new Rect(b.x, y, b.width, 30), "디버그 표시 (F1)", debugHud); y += 42;
            GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(b.x, y, b.width, 60), "자동 회전은 WASD 조작에서 전투 중이 아닐 때, 앞으로 1초 넘게 달리면 시점이 이동 방향 뒤로 천천히 돕니다(옆걸음, 뒷걸음은 돌지 않음). T는 언제든 바로 등 뒤로 돌립니다.\n휠 줌은 플레이 중에만 바뀝니다. 다음 실행부터 줌은 여기 기본값으로 시작합니다.", new GUIStyle(setLabel) { wordWrap = true, fontSize = 12 }); GUI.color = Color.white;
        }
        void GameTab(Rect b)
        {
            float bw = 300, x = b.x + (b.width - bw) / 2, y = b.y + 20;
            if (SettingsArt.Button(new Rect(x, y, bw, 46), "계속하기", clearBtn, white)) CloseSettings(); y += 60;
            if (SettingsArt.Button(new Rect(x, y, bw, 46), "마지막 포드로 돌아가기 (디버그)", clearBtn, white)) { CloseSettings(); RespawnAtPod(); player.hp = player.max; } y += 60;
            if (SettingsArt.Button(new Rect(x, y, bw, 46), "게임 종료", clearBtn, white))
            {
                SettingsSave();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            y += 70;
            GUI.color = new Color(.6f, .7f, .78f); GUI.Label(new Rect(b.x, y, b.width, 22), restPod != null ? $"마지막 포드: {restPod.name}" : "", setCenter); GUI.color = Color.white;
        }

        float SliderRow(Rect r, string name, float v, float min, float max, Func<float, string> fmt)
        {
            GUI.Label(new Rect(r.x, r.y, 190, r.height), name, setLabel);
            var track = new Rect(r.x + 200, r.y + r.height / 2 - 10, r.width - 290, 20);
            float nv = SettingsArt.Slider(track, v, min, max, white);
            GUI.Label(new Rect(r.xMax - 80, r.y, 80, r.height), fmt(nv), new GUIStyle(setLabel) { alignment = TextAnchor.MiddleRight });
            return nv;
        }
        bool Toggle(Rect r, string name, bool on)
        {
            GUI.Label(new Rect(r.x, r.y, 190, r.height), name, setLabel);
            return SettingsArt.Button(new Rect(r.x + 200, r.y, 110, r.height), on ? "켜짐" : "꺼짐", clearBtn, white, on) ? !on : on;
        }
    }
}
