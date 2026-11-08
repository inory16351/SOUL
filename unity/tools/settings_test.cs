// Esc settings / key map check (Play mode). python unity/tools/umcp.py --code unity/tools/settings_test.cs
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
g.enemies.passive = true; g.CloseSettings(); Soul.KeyBinds.ResetDefaults();
g.anim.Play("idle"); g.Simulate(.3f);
// 1) default: A attacks
sb.Append("A default -> ").Append(g.InputForTest(UnityEngine.KeyCode.A)).Append('\n');
g.anim.Play("idle"); g.Simulate(.6f);
// 2) rebind attack to Z: Z attacks, A does nothing
sb.Append("rebind Attack Z: ").Append(g.RebindForTest("Attack", UnityEngine.KeyCode.Z)).Append('\n');
g.CloseSettings(); g.anim.Play("idle"); g.Simulate(.3f);
sb.Append("A after -> ").Append(g.InputForTest(UnityEngine.KeyCode.A)).Append(" | Z after -> ").Append(g.InputForTest(UnityEngine.KeyCode.Z)).Append('\n');
g.anim.Play("idle"); g.Simulate(.6f);
// dash on X
g.RebindForTest("Dash", UnityEngine.KeyCode.X); g.CloseSettings();
sb.Append("X (dash) -> ").Append(g.InputForTest(UnityEngine.KeyCode.X)).Append(" | reserved X ").Append(Soul.Skills.Reserved.Contains(UnityEngine.KeyCode.X)).Append(" Space ").Append(Soul.Skills.Reserved.Contains(UnityEngine.KeyCode.Space)).Append('\n');
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
g.GrantSkill("whirl"); var o = Soul.Skills.Get("whirl"); Soul.Skills.Bind(o, UnityEngine.KeyCode.Q);
sb.Append("bind Rest Q (skill): ").Append(g.RebindForTest("Rest", UnityEngine.KeyCode.Q)).Append('\n');
g.ResolveForTest(true);
sb.Append("  swap -> Rest ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.Rest)).Append(" whirl ").Append(o.key).Append('\n');
sb.Append("bind whirl B: ").Append(g.RebindForTest("whirl", UnityEngine.KeyCode.B)).Append(" -> ").Append(o.key).Append('\n');
// 4) save / load
Soul.KeyBinds.Set(Soul.KeyBinds.Act.View, UnityEngine.KeyCode.Y);
Soul.SoulAudio.masterVolume = .37f; Soul.SoulAudio.musicVolume = .21f; Soul.SoulAudio.sfxVolume = .64f; Soul.FollowCam.shakeScale = .5f; g.zoomDefault = 1.4f;
g.SettingsSave();
Soul.KeyBinds.ResetDefaults(); Soul.SoulAudio.masterVolume = 1; Soul.SoulAudio.musicVolume = .5f; Soul.FollowCam.shakeScale = 1; g.zoomDefault = 1;
UnityEngine.PlayerPrefs.SetInt("soul.key.View", (int)UnityEngine.KeyCode.Y); UnityEngine.PlayerPrefs.SetFloat("soul.vol.master", .37f); UnityEngine.PlayerPrefs.SetFloat("soul.vol.music", .21f); UnityEngine.PlayerPrefs.SetFloat("soul.cam.shake", .5f); UnityEngine.PlayerPrefs.SetFloat("soul.cam.zoom", 1.4f);
g.SettingsReload();
sb.Append("reload -> View ").Append(Soul.KeyBinds.Name(Soul.KeyBinds.Act.View)).Append(" master ").Append(Soul.SoulAudio.masterVolume.ToString("F2")).Append(" music ").Append(Soul.SoulAudio.musicVolume.ToString("F2")).Append(" shake ").Append(Soul.FollowCam.shakeScale.ToString("F2")).Append(" zoom ").Append(g.cam.zoom.ToString("F2")).Append('\n');
// 5) volume reaches the audio (listener + music source)
sb.Append("AudioListener.volume ").Append(UnityEngine.AudioListener.volume.ToString("F2")).Append(" (applied next audio Update)\n");
// 6) pause: open, the game ignores keys
g.OpenSettings();
g.anim.Play("idle"); g.Simulate(.1f);
string a2 = g.InputForTest(UnityEngine.KeyCode.A);
sb.Append("open: settingsOpen ").Append(g.settingsOpen).Append(" A while open -> ").Append(a2).Append('\n');
g.InputForTest(UnityEngine.KeyCode.Escape);
sb.Append("Esc -> settingsOpen ").Append(g.settingsOpen).Append('\n');
// 7) new-skill prompt Esc wins over settings
g.GrantSkill("wave");
g.InputForTest(UnityEngine.KeyCode.Escape);
sb.Append("Esc with skill prompt -> settingsOpen ").Append(g.settingsOpen).Append(" wave key ").Append(Soul.Skills.Get("wave").key).Append('\n');
return sb.ToString();
