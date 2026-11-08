var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
var sb = new System.Text.StringBuilder();
g.GrudgeTick(0); g.VistaTick(0);
int savedParts = Soul.Parts.owned, savedScrap = g.scrap; float savedHp = g.player.hp;
Soul.Skills.Reset(); Soul.Growth.Reset(); Soul.Parts.owned = 0; g.scrap = 0; g.GrowthApplyNow();
sb.Append("owned at start ").Append(Soul.Skills.owned.Count).Append(" max hp ").Append(g.player.max).Append('\n');
// ---- xp + wallet ----
g.GainScrap(50);
sb.Append("gain 50 -> xp ").Append(Soul.Growth.xp).Append(" wallet ").Append(g.scrap).Append(" points ").Append(Soul.Growth.Points).Append(" (thresholds 20, 43, 69)\n");
// ---- nodes: prerequisites ----
sb.Append("open r_edge2 before r_edge1 ").Append(g.GrowthOpenNode("r_edge2")).Append(" (").Append(Soul.Growth.Blocker(Soul.Growth.Node("r_edge2"))).Append(")\n");
sb.Append("open c_frame1 ").Append(g.GrowthOpenNode("c_frame1")).Append(" max hp ").Append(g.player.max).Append(" points ").Append(Soul.Growth.Points).Append('\n');
sb.Append("open r_edge1 (branch root) ").Append(g.GrowthOpenNode("r_edge1")).Append(" light1 dmg x").Append(g.DamageScaleFor("light1").ToString("F2")).Append(" points ").Append(Soul.Growth.Points).Append('\n');
sb.Append("open r_edge2 with 0 points ").Append(g.GrowthOpenNode("r_edge2")).Append('\n');
// ---- part gate ----
g.GainScrap(2000);
sb.Append("gain 2000 -> xp ").Append(Soul.Growth.xp).Append(" points ").Append(Soul.Growth.Points).Append('\n');
g.GrowthOpenNode("c_stable");
sb.Append("c_filter without anti-toxin core ").Append(g.GrowthOpenNode("c_filter")).Append(" (").Append(Soul.Growth.Blocker(Soul.Growth.Node("c_filter"))).Append(")\n");
Soul.Parts.Give(Soul.Parts.Toxic);
sb.Append("c_filter with core ").Append(g.GrowthOpenNode("c_filter")).Append(" max hp ").Append(g.player.max).Append('\n');
// ---- effects ----
float dash0 = g.DashCooldown, speed0 = g.MoveSpeedScale, r0 = g.AreaRadius("light1"), cw0 = g.CounterWindowEnd;
g.GrowthOpenNode("g_run1"); g.GrowthOpenNode("g_dash1");
sb.Append("speed x").Append(speed0.ToString("F2")).Append(" -> x").Append(g.MoveSpeedScale.ToString("F2")).Append("  dash cd ").Append(dash0.ToString("F2")).Append(" -> ").Append(g.DashCooldown.ToString("F3")).Append('\n');
g.GrowthOpenNode("r_edge2"); g.GrowthOpenNode("r_finish"); g.GrowthOpenNode("r_reach");
sb.Append("light1 dmg x").Append(g.DamageScaleFor("light1").ToString("F3")).Append(" light3 x").Append(g.DamageScaleFor("light3").ToString("F3")).Append(" reach ").Append(r0.ToString("F2")).Append(" -> ").Append(g.AreaRadius("light1").ToString("F3")).Append('\n');
g.GrowthOpenNode("l_guard1"); g.GrowthOpenNode("c_plating");
sb.Append("counter window end ").Append(cw0.ToString("F2")).Append(" -> ").Append(g.CounterWindowEnd.ToString("F2")).Append("  damage taken x").Append(g.DamageTakenScale.ToString("F2")).Append('\n');
sb.Append("open nodes ").Append(Soul.Growth.open.Count).Append(" spent ").Append(Soul.Growth.spent).Append(" points left ").Append(Soul.Growth.Points).Append('\n');
// ---- skills: grant, key, cast ----
g.GrantSkill("whirl");
var o = Soul.Skills.Get("whirl");
sb.Append("whirl owned ").Append(o != null).Append(" key ").Append(o.key).Append('\n');
Soul.Skills.Bind(o, UnityEngine.KeyCode.Q);
g.Simulate(.5f);
g.SkillCast("whirl"); g.Simulate(.05f);
sb.Append("after cast anim ").Append(g.anim.name).Append(" cooldown started ").Append(o.ready > 0).Append('\n');
// ---- skill-part upgrade spends the wallet; xp stays ----
int xpBefore = Soul.Growth.xp; float heavy0 = g.AreaRadius("heavy");
g.scrap = 100;
bool u1 = g.SkillUpgrade("whirl"); int w1 = g.scrap;
bool u2 = g.SkillUpgrade("whirl"); int w2 = g.scrap;
bool u3 = g.SkillUpgrade("whirl");
g.GrowthApplyNow();
sb.Append("upgrade Lv2 ").Append(u1).Append(" wallet ").Append(w1).Append(" | Lv3 ").Append(u2).Append(" wallet ").Append(w2).Append(" | Lv4 (needs 100) ").Append(u3).Append(" wallet ").Append(g.scrap).Append('\n');
sb.Append("whirl level ").Append(o.level).Append(" dmg x").Append(Soul.Skills.DamageScale(o).ToString("F2")).Append(" cd x").Append(Soul.Skills.CooldownScale(o).ToString("F2")).Append(" Lv3 range ").Append(heavy0.ToString("F2")).Append(" -> ").Append(g.AreaRadius("heavy").ToString("F2")).Append('\n');
sb.Append("xp before ").Append(xpBefore).Append(" after ").Append(Soul.Growth.xp).Append('\n');
// ---- rebinding a held key moves it ----
g.GrantSkill("wave"); var w = Soul.Skills.Get("wave"); Soul.Skills.Bind(w, UnityEngine.KeyCode.Q);
sb.Append("bind Q to wave -> whirl key ").Append(o.key).Append(" wave key ").Append(w.key).Append('\n');
// ---- core overload: regen % of max HP, speed, damage taken ----
g.GrantSkill("overload"); var ov = Soul.Skills.Get("overload");
g.player.hp = 40; float max = g.player.max;
g.SkillCast("overload");
sb.Append("overload left ").Append(g.OverloadLeft.ToString("F1")).Append(" speed x").Append(g.MoveSpeedScale.ToString("F3")).Append(" taken x").Append(g.DamageTakenScale.ToString("F2")).Append('\n');
g.SkillsTickForTest(2f);
sb.Append("hp after 2 s ").Append(g.player.hp.ToString("F1")).Append(" (expected ").Append((40 + max * .04f * 2).ToString("F1")).Append(")\n");
g.SkillsTickForTest(7f);
sb.Append("overload left after 9 s ").Append(g.OverloadLeft.ToString("F1")).Append('\n');
// ---- restore ----
Soul.Skills.Reset(); Soul.Growth.Reset(); Soul.Parts.owned = savedParts; g.scrap = savedScrap; g.GrowthApplyNow(); g.player.hp = System.Math.Min(savedHp, g.player.max);
sb.Append("restored max hp ").Append(g.player.max).Append(" dash ").Append(g.DashCooldown.ToString("F2")).Append(" reach ").Append(g.AreaRadius("light1").ToString("F2"));
return sb.ToString();
