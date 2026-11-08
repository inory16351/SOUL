using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Sound hooks (SoulAudio does the playing). Two kinds of hooks, so no other file has to change:
    //  - callbacks: existing delegates (animator step/land/impact/fx/willPlay, EnemyManager onPlayerHit/onImpact/
    //    onKill/onStep, Pickups.onCollect) are wrapped once in AudioHook; the original handler still runs first.
    //  - polling: AudioTick (SoulAudio.LateUpdate) diffs state every frame: enemy HP (hits), enemy state (boss
    //    roar / phase / throw), telegraph progress (warning beep), player alive (death / reboot), rope state,
    //    boss pillars, purification, grudge drop, cue text (pod rest, shrine resonance), and picks the music.
    public partial class SoulGame
    {
        SoulAudio audioMgr;
        float auHp = -1; bool auAlive = true, auInit; RopeState auRope; bool auPurifying, auGrudge; string auCue = "";
        int auFails, auWins;
        readonly Dictionary<Enemy, (float hp, string state, int phase)> auEnemies = new Dictionary<Enemy, (float, string, int)>();
        readonly HashSet<Telegraph> auWarned = new HashSet<Telegraph>(), auThrown = new HashSet<Telegraph>();
        readonly Dictionary<Pillar, (bool lit, bool broken, int hp)> auPillars = new Dictionary<Pillar, (bool, bool, int)>();
        static readonly Dictionary<string, (float impact, string clip)> SWINGS = new Dictionary<string, (float, string)>
        { { "light1", (.275f, "swing1") }, { "light2", (.27f, "swing2") }, { "light3", (.3f, "swing3") } };

        internal void AudioHook(SoulAudio a)
        {
            audioMgr = a;
            if (anim == null || enemies == null) return;
            var willPlay = anim.willPlay; anim.willPlay = n =>
            {
                willPlay?.Invoke(n);
                var at = ch.root.position;
                if (SWINGS.TryGetValue(n, out var s)) SoulAudio.Play(s.clip, at, 1, 1, .07f, Mathf.Max(0, s.impact - .14f));
                else if (n == "jump") SoulAudio.Play("jump", at);
                else if (n == "heavyCharge") SoulAudio.Play("swing3", at, .6f, .7f);
            };
            var step = anim.step; anim.step = p =>
            {
                step?.Invoke(p);
                bool wet = Terrain.Water(p.x, p.z, out _) > .05f;
                SoulAudio.Play(wet ? "stepWater" : "step", p, .55f, 1, .12f, 0, .06f);
            };
            var land = anim.land; anim.land = () => { land?.Invoke(); SoulAudio.Play("land", ch.root.position, .8f); };
            var impact = anim.impact; anim.impact = (n, m, c) => { impact?.Invoke(n, m, c); if (m.slam) SoulAudio.Play("slam", ch.root.position, .9f, 1.25f); };
            var fx = anim.fx; anim.fx = (kind, n) =>
            {
                fx?.Invoke(kind, n);
                if (kind == "wave") SoulAudio.Play("laser", ch.root.position);
                else if (kind == "parry") SoulAudio.Play("guard", ch.root.position, .7f);
                else if (kind == "dash") SoulAudio.Play("dash", ch.root.position);
            };
            var hit = enemies.onPlayerHit; enemies.onPlayerHit = (dmg, from, e) =>
            {
                float hp = player.hp; bool wasAlive = player.alive, countering = anim.name == "counter";
                hit?.Invoke(dmg, from, e);
                if (!wasAlive) return;
                if (player.hp < hp) SoulAudio.Play("hurt", ch.root.position, 1, 1, .05f, 0, .1f);
                else if (countering && e != null && e.state == "stagger") SoulAudio.Play("parry", ch.root.position, 1, 1, .03f);
                else SoulAudio.Play("dash", ch.root.position, .5f, 1.3f);   // dodged through it
            };
            var onImpact = enemies.onImpact; enemies.onImpact = (e, atk, p) =>
            {
                onImpact?.Invoke(e, atk, p);
                string n = atk.name;
                if (n == "slam" || n == "quake") SoulAudio.Play("slam", p, 1, n == "quake" ? .8f : 1);
                else if (n == "throw") { SoulAudio.Play("slam", p, .8f, 1.1f); SoulAudio.Play("clatter", p, .9f); }
                else if (n == "roll") SoulAudio.Play("roll", e.pos, 1, 1, .05f);
                else if (n == "lunge" || n == "claw") SoulAudio.Play("land", e.pos, .9f, n == "claw" ? .7f : 1.1f);
                else SoulAudio.Play("swing3", p, .8f, e.IsBoss ? .6f : .8f);
            };
            var kill = enemies.onKill; enemies.onKill = e => { kill?.Invoke(e); SoulAudio.Play("kill", e.Center(), e.IsBoss ? 1 : .85f, e.IsBoss ? .7f : 1); };
            var bossStep = enemies.onStep; enemies.onStep = (p, s) => { bossStep?.Invoke(p, s); SoulAudio.Play("land", p, .6f * s, .55f, .08f, 0, .08f); };
            if (pickups != null) { var collect = pickups.onCollect; pickups.onCollect = it => { collect?.Invoke(it); SoulAudio.Play("pickup", null, .7f, 1, .08f); }; }
        }

        internal void AudioTick(float dt)
        {
            if (audioMgr == null || ch == null || enemies == null) return;
            var me = ch.root.position; audioMgr.listener = me;
            if (!auInit)
            {   // baseline on the first tick: nothing that was already true at startup makes a sound
                auInit = true; auAlive = player.alive; auRope = rs; auPurifying = Purifying; auGrudge = grudgeDropped; auFails = pillarFails; auWins = pillarWins; auCue = cueText;
                foreach (var e in enemies.list) auEnemies[e] = (e.hp, e.state, e.phase);
                foreach (var pl in pillars) auPillars[pl] = (pl.lit, pl.broken, pl.hp);
            }

            // ---- player ----
            if (auAlive && !player.alive) SoulAudio.Play("death", me);
            else if (!auAlive && player.alive) SoulAudio.Play("reboot", me);
            auAlive = player.alive; auHp = player.hp;
            string n = anim.name;
            SoulAudio.Loop("whirl", "whirl", n == "heavy" || n == "flurry", me, .8f, n == "flurry" ? 1.25f : 1);
            if (auRope == RopeState.None && rs == RopeState.Fly) SoulAudio.Play("ropeFire", me);
            SoulAudio.Loop("rope", "ropeReel", rs == RopeState.Retract || rs == RopeState.Reel || rs == RopeState.Zip || rs == RopeState.Yank, me, .7f);
            auRope = rs;

            // ---- enemies: hits, roar, phase change, telegraph warning, thrown scrap ----
            var seen = new HashSet<Enemy>();
            foreach (var e in enemies.list)
            {
                seen.Add(e);
                if (auEnemies.TryGetValue(e, out var was))
                {
                    float drop = was.hp - e.hp;
                    if (drop > .5f && e.state != "dead" && e.phase == was.phase) SoulAudio.Play(drop >= 24 || Heavy(n) ? "hitHeavy" : "hit", e.Center(), 1, e.IsBoss ? .85f : 1, .08f, 0, .02f);
                    if (e.state == "wake" && was.state != "wake") SoulAudio.Play("roar", e.pos, 1, e.IsBoss ? .9f : 1.2f, .03f);
                    if (e.phase != was.phase && e.phase == 2) { SoulAudio.Play("phase", e.pos, 1, 1, 0); SoulAudio.Play("roar", e.pos, 1, .8f, 0, 1.2f, 0); audioMgr.MusicSeek(.5f); }
                }
                auEnemies[e] = (e.hp, e.state, e.phase);
                var tele = e.Tele;
                if (tele != null && tele.duration > 0)
                {
                    float p = tele.t / tele.duration;
                    if (p >= .8f && auWarned.Add(tele)) SoulAudio.Play("warn", tele.origin, e.IsBoss ? 1 : .7f, e.IsBoss ? .9f : 1.1f, .02f, 0, .12f);
                    if (e.attack != null && e.attack.name == "throw" && p >= .55f && auThrown.Add(tele)) SoulAudio.Play("throw", Vector3.Lerp(e.pos, tele.origin, .5f));
                }
            }
            if (auEnemies.Count > seen.Count) { var gone = new List<Enemy>(); foreach (var k in auEnemies.Keys) if (!seen.Contains(k)) gone.Add(k); foreach (var k in gone) auEnemies.Remove(k); }
            if (auWarned.Count > 64) auWarned.Clear(); if (auThrown.Count > 32) auThrown.Clear();

            // ---- boss pillars ----
            foreach (var pl in pillars)
            {
                if (auPillars.TryGetValue(pl, out var w))
                {
                    if (pl.lit && !w.lit) SoulAudio.Play("pillarOn", pl.pos, .9f, boss != null && boss.phase == 2 ? .8f : 1, .02f);
                    if (pl.broken && !w.broken) SoulAudio.Play("pillarBreak", pl.pos);
                    else if (pl.hp < w.hp) SoulAudio.Play("hit", pl.pos, .9f, 1.3f);
                }
                else SoulAudio.Play("pillarRise", pl.pos, .9f, 1, .05f, 0, .2f);
                auPillars[pl] = (pl.lit, pl.broken, pl.hp);
            }
            if (auPillars.Count > pillars.Count + 8) auPillars.Clear();
            if (pillarFails > auFails) SoulAudio.Play("pillarWrong", me);
            if (pillarWins > auWins) SoulAudio.Play("parry", me, .8f, .75f);
            auFails = pillarFails; auWins = pillarWins;

            // ---- world ----
            if (Purifying && !auPurifying) { SoulAudio.Play("purify", null, 1, 1, 0); audioMgr.MusicOnce("purify"); }
            auPurifying = Purifying;
            if (grudgeDropped && !auGrudge) SoulAudio.Play("grudge", null, 1, 1, 0);
            auGrudge = grudgeDropped;
            if (cueText != auCue && realTime < cueUntil)
            {
                if (cueText.StartsWith("충전 완료")) { SoulAudio.Play("rest", null, 1, 1, 0); audioMgr.MusicOnce("rest"); }
                else if (cueText.Contains(" 공명 · ")) SoulAudio.Play("shrine", null, 1, 1, 0);
                else if (cueText.StartsWith("원혼 파츠")) SoulAudio.Play("grudge", null, .9f, .9f, 0);
                else if (cueText.StartsWith("빠른 이동")) SoulAudio.Play("reboot", null, .6f, 1.2f, 0);
            }
            auCue = cueText;

            if (audioMgr.OnceKey == "rest" && (NearPod(10) == null || fightArena != null)) audioMgr.MusicCancelOnce();   // pod music only while at the pod
            // ---- music: boss fight from the wake-up, purification (one-shot), field otherwise ----
            audioMgr.wantKey = fightArena != null && boss != null && (bossFlow != "" || boss.state == "wake") && boss.state != "dormant" ? "boss" : "field";
        }

        /// MCP: "field t=12.3/131 vol=0.50 playing=True | hit=3, swing1=1, ..."
        public string AudioReport() => audioMgr == null ? "no audio" : audioMgr.MusicState + " | " + SoulAudio.Stats();
    }
}
