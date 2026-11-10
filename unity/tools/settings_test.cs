// Esc settings / key map check (Play mode). python unity/tools/umcp.py --code unity/tools/settings_test.cs
// 2026-11-10: two control schemes (KeyBinds.Scheme). Parts 1-7 run in the click-move scheme (the old layout this
// script was written for: A attack, new skills ask for a key); part 8 checks the WASD scheme and the scheme switch.
// PlayerPrefs keys are per scheme now: soul.key.<wasd|click>.<Act>. The script restores the saved settings at the end.
// It clears the learned skills first (Skills.Reset), so run it in a Play session you do not need afterwards.
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
g.enemies.passive = true; g.CloseSettings(); g.InputForTest(); Soul.Skills.Reset();
var scheme0 = Soul.KeyBinds.scheme; float m0 = Soul.SoulAudio.masterVolume, mu0 = Soul.SoulAudio.musicVolume, s0 = Soul.SoulAudio.sfxVolume, sh0 = Soul.FollowCam.shakeScale, z0 = g.zoomDefault, fs0 = Soul.FollowCam.followSpeed; bool fo0 = Soul.FollowCam.followOn;
g.SetControlScheme(Soul.KeyBinds.Scheme.Click); Soul.KeyBinds.ResetDefaults();
g.anim.Play("idle"); g.Simulate(.3f);
// 1) default: A attacks
sb.Append("A default -> ").Append(g.InputForTest(UnityEngine.KeyCode.A)).Append('\n');
g.anim.Play("idle"); g.Simulate(.6f);
// 2) rebind attack to Z: Z attacks, A does nothing
sb.Append("rebind Attack Z: ").Append(g.RebindForTest("Attack", UnityEngine.KeyCode.Z)).Append('\n');
g.CloseSettings(); g.anim.Play("idle"); g.Simulate(.3f);
sb.Append("A after -> ").Append(g.InputForTest(UnityEngine.KeyCode.A)).Append(" | Z after -> ").Append(g.InputForTest(UnityEngine.KeyCode.Z)).Append('\n');
g.anim.Play("idle"); g.Simulate(.6f);
// dash on B (X is the repair kit: blocked)
sb.Append("bind Dash X: ").Append(g.RebindForTest("Dash", UnityEngine.KeyCode.X)).Append('\n');
g.RebindForTest("Dash", UnityEngine.KeyCode.B); g.CloseSettings(); g.ClearCooldownsForTest();
sb.Append("B (dash) -> ").Append(g.InputForTest(UnityEngine.KeyCode.B)).Append(" | reserved B ").Append(Soul.Skills.Reserved.Contains(UnityEngine.KeyCode.B)).Append(" Space ").Append(Soul.Skills.Reserved.Contains(UnityEngine.KeyCode.Space)).Append('\n');
g.anim.Play("idle"); g.Simulate(.6f);
// 3) blocked key + conflicts (basic vs basic: swap / clear)
sb.Append("bind Jump P: ").Append(g.RebindForTest("Jump", UnityEngine.KeyCode.P)).Append('\n');
sb.Append("bind Jump Z: ").Append(g.RebindForTest("Jump", UnityEngine.KeyCode.Z)).Append('\n');
g.ResolveForTest(true);
sb.Append("  swap -> Jump ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Jump)).Append(" Attack ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Attack)).Append('\n');
sb.Append("bind Map Z: ").Append(g.RebindForTest("Map", UnityEngine.KeyCode.Z)).Append('\n');
g.ResolveForTest(false);
sb.Append("  clear -> Map ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Map)).Append(" Jump ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Jump)).Append('\n');
// basic vs skill
g.CloseSettings(); g.GrantSkill("whirl"); var o = Soul.Skills.Get("whirl"); Soul.Skills.Bind(o, UnityEngine.KeyCode.Q); g.InputForTest(UnityEngine.KeyCode.Escape);
sb.Append("bind Rest Q (skill): ").Append(g.RebindForTest("Rest", UnityEngine.KeyCode.Q)).Append('\n');
g.ResolveForTest(true);
sb.Append("  swap -> Rest ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Rest)).Append(" whirl ").Append(o.key).Append('\n');
sb.Append("bind whirl N: ").Append(g.RebindForTest("whirl", UnityEngine.KeyCode.N)).Append(" -> ").Append(o.key).Append('\n');
// 4) save / load (per-scheme key names)
Soul.KeyBinds.Set(Soul.KeyBinds.Act.View, UnityEngine.KeyCode.Y);
Soul.SoulAudio.masterVolume = .37f; Soul.SoulAudio.musicVolume = .21f; Soul.SoulAudio.sfxVolume = .64f; Soul.FollowCam.shakeScale = .5f; g.zoomDefault = 1.4f; Soul.FollowCam.followOn = false; Soul.FollowCam.followSpeed = 33;
g.SettingsSave();
Soul.KeyBinds.Set(Soul.KeyBinds.Act.View, UnityEngine.KeyCode.T); Soul.SoulAudio.masterVolume = 1; Soul.SoulAudio.musicVolume = .5f; Soul.FollowCam.shakeScale = 1; g.zoomDefault = 1; Soul.FollowCam.followOn = true; Soul.FollowCam.followSpeed = 45;
UnityEngine.PlayerPrefs.SetInt("soul.key.click.View", (int)UnityEngine.KeyCode.Y); UnityEngine.PlayerPrefs.SetFloat("soul.vol.master", .37f); UnityEngine.PlayerPrefs.SetFloat("soul.vol.music", .21f); UnityEngine.PlayerPrefs.SetFloat("soul.cam.shake", .5f); UnityEngine.PlayerPrefs.SetFloat("soul.cam.zoom", 1.4f);
UnityEngine.PlayerPrefs.SetInt("soul.cam.follow", 0); UnityEngine.PlayerPrefs.SetFloat("soul.cam.followSpeed", 33);
g.SettingsReload();
sb.Append("reload -> scheme ").Append(Soul.KeyBinds.scheme).Append(" View ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.View)).Append(" master ").Append(Soul.SoulAudio.masterVolume.ToString("F2")).Append(" music ").Append(Soul.SoulAudio.musicVolume.ToString("F2")).Append(" shake ").Append(Soul.FollowCam.shakeScale.ToString("F2")).Append(" zoom ").Append(g.cam.zoom.ToString("F2")).Append(" follow ").Append(Soul.FollowCam.followOn).Append(" followSpeed ").Append(Soul.FollowCam.followSpeed.ToString("F0")).Append('\n');
// 5) volume reaches the audio (listener + music source)
sb.Append("AudioListener.volume ").Append(UnityEngine.AudioListener.volume.ToString("F2")).Append(" (applied next audio Update)\n");
// 6) pause: open, the game ignores keys
Soul.KeyBinds.ResetDefaults(); g.OpenSettings();
g.anim.Play("idle"); g.Simulate(.1f);
string a2 = g.InputForTest(UnityEngine.KeyCode.A);
sb.Append("open: settingsOpen ").Append(g.settingsOpen).Append(" A while open -> ").Append(a2).Append('\n');
g.InputForTest(UnityEngine.KeyCode.Escape);
sb.Append("Esc -> settingsOpen ").Append(g.settingsOpen).Append('\n');
// 7) click-move: the new-skill prompt asks for a key, and its Esc wins over settings
// (반격 = arm slot, free; a second weapon skill would go to storage without a prompt: 1 weapon slot at the start)
g.GrantSkill("counter"); var c = Soul.Skills.Get("counter");
g.InputForTest(UnityEngine.KeyCode.Escape);
sb.Append("Esc with skill prompt -> settingsOpen ").Append(g.settingsOpen).Append(" counter key ").Append(c.key).Append('\n');
// 8) WASD scheme: its own key set, fixed move keys refused, skills re-keyed onto 1 2 3 4 Q E; back to click keeps the click keys
Soul.Skills.Bind(c, UnityEngine.KeyCode.E);
g.SetControlScheme(Soul.KeyBinds.Scheme.Wasd);
sb.Append("WASD: Attack ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Attack)).Append(" Dash ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Dash)).Append(" Jump ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Jump)).Append(" | whirl ").Append(o.key).Append(" counter ").Append(c.key).Append('\n');
// WASD: a new active skill takes the next free key by itself (no prompt)
g.CloseSettings(); g.GrantSkill("overload"); sb.Append("WASD grant 코어 과부하 -> key ").Append(Soul.Skills.Get("overload").key).Append('\n');
sb.Append("bind Jump W (move key): ").Append(g.RebindForTest("Jump", UnityEngine.KeyCode.W)).Append('\n');
sb.Append("bind Jump 5: ").Append(g.RebindForTest("Jump", UnityEngine.KeyCode.Alpha5)).Append('\n');
Soul.KeyBinds.ResetDefaults(); g.CloseSettings();
g.SetControlScheme(Soul.KeyBinds.Scheme.Click);
sb.Append("back to click: Attack ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Attack)).Append(" Jump ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Jump)).Append(" | whirl ").Append(o.key).Append(" counter ").Append(c.key).Append(" overload ").Append(Soul.Skills.Get("overload").key).Append('\n');
// restore what was saved before the test
Soul.KeyBinds.ResetDefaults(); g.SetControlScheme(scheme0);
Soul.SoulAudio.masterVolume = m0; Soul.SoulAudio.musicVolume = mu0; Soul.SoulAudio.sfxVolume = s0; Soul.FollowCam.shakeScale = sh0; g.zoomDefault = z0; g.cam.zoom = z0; Soul.FollowCam.followOn = fo0; Soul.FollowCam.followSpeed = fs0;
g.SettingsSave();
return sb.ToString();
