import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { createScene } from './scene.js';
import { createCharacter } from './character.js';
import { Animator } from './anim.js';
import { moves } from './moves.js';
import { createSecondary } from './secondary.js';
import { VFX } from './vfx.js';
import { FollowCamera } from './camera.js';
import { createLaserBlade, createFace, tearRightSide } from './neon.js';
import { EnemyManager } from './enemies.js';
import { createBreakables } from './props.js';

// ---------- renderer / scene ----------
const canvas = document.querySelector('#scene');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2)); renderer.setSize(innerWidth, innerHeight);
renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap; renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.05;
const world = createScene(), { scene, dummy, sun } = world, character = createCharacter();
const BOUNDS = world.bounds ?? 18;
if (dummy?.root) dummy.root.visible = false;
scene.add(character.root, character.skeleton); character.root.position.z = -.25;
const camera = new FollowCamera(innerWidth / innerHeight), secondary = createSecondary(scene, character), vfx = new VFX(scene, character);
const composer = new EffectComposer(renderer); composer.addPass(new RenderPass(scene, camera.camera));
composer.addPass(new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), .62, .5, .88)); composer.addPass(new OutputPass());

// ---------- state ----------
let timeScale = 1, hitStop = 0, hitCount = 0, lastHitAt = -10, realTime = 0, connected = false, lastConnect = -1, previousTip = null, cueUntil = 0, ghostClock = 0;
const timers = [];                       // [time, fn]: queued follow-ups (charge -> cyclone, leap -> storm)
const after = (s, fn) => timers.push([realTime + s, fn]);
const stateEl = document.querySelector('#state'), progressEl = document.querySelector('#progress'), cueEl = document.querySelector('#cue-text');
function cue(text, seconds = 2) { cueEl.textContent = text; cueUntil = realTime + seconds; }
function groundPoint() { return character.root.position.clone().add(new THREE.Vector3(0, .05, 0)); }

// ---------- mouse ----------
const mouse = { ndc: new THREE.Vector2(), right: false };
let moveTarget = null, aimYaw = null;
function cursorPoint() { return camera.pick(mouse.ndc); }
function yawToCursor() { const p = cursorPoint(); if (!p) return character.root.rotation.y; const d = p.sub(character.root.position); return Math.atan2(d.x, d.z); }
function faceCursor() { animator.pendingYaw = yawToCursor(); }
canvas.addEventListener('pointermove', e => { mouse.ndc.set(e.clientX / innerWidth * 2 - 1, -(e.clientY / innerHeight) * 2 + 1); });
canvas.addEventListener('pointerdown', e => {
  mouse.ndc.set(e.clientX / innerWidth * 2 - 1, -(e.clientY / innerHeight) * 2 + 1);
  if (e.button === 2) { mouse.right = true; setMoveTarget(true); }
});
window.addEventListener('pointerup', e => { if (e.button === 2) mouse.right = false; });
canvas.addEventListener('contextmenu', e => e.preventDefault());
canvas.addEventListener('wheel', e => { e.preventDefault(); camera.zoomBy(Math.sign(e.deltaY) * .08); }, { passive: false });
function setMoveTarget(marker) {
  const p = cursorPoint(); if (!p || !player.alive) return;
  moveTarget = p; if (marker) vfx.ring(p.clone().setY(.03), .18);
}

// ---------- combat ----------
const animator = new Animator(character, {
  willPlay: name => { if (aimYaw !== null && /^light/.test(name)) { animator.pendingYaw = aimYaw; aimYaw = null; } },
  dust: () => vfx.dust(groundPoint(), 1), step: p => vfx.dust(p, .35),
  land: () => { vfx.dust(groundPoint(), .9); camera.shake(.035); },
  buffer: () => {},
  impact: (name, move) => {
    // (no crescent / range-indicator arc: only the blade trail shows the swing)
    if (move.slam) { const point = character.root.localToWorld(new THREE.Vector3(0, .04, 1)); vfx.ring(point, 1.3); vfx.dust(point, 1.5); camera.shake(.08); }
  },
  fx: (kind) => {
    if (kind === 'wave') {
      const root = character.root, dir = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
      vfx.spawnWave(root.position.clone().add(new THREE.Vector3(0, .35, 0)).addScaledVector(dir, .6), dir); camera.shake(.05);
    } else if (kind === 'parry') {
      const p = character.bladeBase.getWorldPosition(new THREE.Vector3()).lerp(character.bladeTip.getWorldPosition(new THREE.Vector3()), .4);
      vfx.ring(p, 1, true); vfx.flash(p, .8); face.set('wide', .3);
    } else if (kind === 'dash') { ghostClock = 0; vfx.dust(groundPoint(), 1.2); }
  },
});
scene.add(character.colliders);
const laser = createLaserBlade(character), face = createFace(character);
tearRightSide(character);
window.__laser = laser;

// ---------- player health / survival ----------
const player = { pos: character.root.position, hp: 100, max: 100, alive: true };
let hurtFlash = 0, kills = 0;
// dash and roll give invulnerability frames; the blade storm is airborne and untouchable
function invulnerable() {
  const n = animator.name, t = animator.time;
  return (n === 'dash' && t < .34) || (n === 'roll' && t > .04 && t < .78) || (n === 'flurry' && t < 1.18) || (n === 'jump' && t > .2 && t < .85);
}
function playerDie() {
  player.alive = false; moveTarget = null; start('stagger'); face.set('hurt', 2.2); cue('SYSTEM FAILURE · 재기동 중', 2.2);
  after(2.2, () => { player.hp = player.max; player.alive = true; enemies.clearNear(character.root.position, 7); animator.play('idle'); face.set('happy', 1); cue('재기동 완료', 1.2); });
}
const enemies = new EnemyManager(scene, vfx, {
  bounds: BOUNDS - 1,
  onPlayerHit: (dmg, from, enemy) => {
    if (!player.alive) return;
    if (invulnerable()) { cue('회피!', .6); vfx.flash(character.root.position.clone().setY(1.1), .7); return; }
    // perfect parry: E's deflect window turns the blow back on the attacker
    if (animator.name === 'counter' && animator.time >= .03 && animator.time <= .27) {
      enemy.toStagger(1.4); enemy.damage(10, character.root.position, true); face.set('happy', .9); cue('반격!', .8);
      const p = character.bladeBase.getWorldPosition(new THREE.Vector3()); vfx.ring(p, 1.4, true); vfx.burst(p, 26, 1.3); camera.shake(.09); hitStop = .1; return;
    }
    player.hp = Math.max(0, player.hp - dmg); hurtFlash = 1;
    const d = from.clone().sub(character.root.position); character.root.rotation.y = Math.atan2(d.x, d.z);
    moveTarget = null; getHit(); camera.shake(.11); hitStop = Math.max(hitStop, .05);
    if (player.hp <= 0) playerDie();
  },
  onImpact: (enemy, attack, p) => {
    if (attack.name === 'slam') { vfx.ring(p.clone().setY(.03), 1.7); vfx.dust(p, 1.8); vfx.burst(p.clone().setY(.2), 14, 1); }
    else if (attack.name === 'lunge') vfx.dust(enemy.root.position, .9);
    else vfx.dust(p, .7);
    if (p.distanceTo(character.root.position) < 8) camera.shake(attack.name === 'slam' ? .1 : .04);
  },
  onKill: () => { kills++; },
});
const breakables = createBreakables(scene, vfx, 12, BOUNDS - 1);
const hitSet = new Map();
const DMG = { light1: 18, light2: 18, light3: 24, heavy: 13, flurry: 11, counter: 40, jump: 0 };
// Hit areas are deliberately LARGER than the visible effect (player comfort): radius +~0.7 m,
// fans ~20% wider than the trail they come with.
const AREA = { light1: { r: 3.6, a: 3.0 }, light2: { r: 3.7, a: 3.2 }, light3: { r: 3.8 }, heavy: { r: 4.0 }, flurry: { r: 3.8 }, counter: { r: 3.8, a: 3.3 } };
const HIT = { bladeReach: 1.1, tipExtend: .5, breakReach: .8, wave: 2.6 };
function areaHits(move) {
  const name = animator.name, area = AREA[name]; if (!area) return;
  const root = character.root, f = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
  const heavy = name === 'light3' || name === 'counter' || (name === 'flurry' && animator.time > 1.15);
  for (const e of enemies.list) {
    if (!e.alive || e.state === 'spawn') continue;
    const d = e.root.position.clone().sub(root.position).setY(0), dist = d.length() - e.radius * .7;
    if (dist > area.r) continue;
    if (area.a && dist > .6 && d.normalize().dot(f) < Math.cos(area.a / 2)) continue;
    const last = hitSet.get(e); if (last !== undefined && !(move.multi && animator.time - last >= move.multi)) continue;
    hitSet.set(e, animator.time); e.damage(DMG[name] ?? 15, root.position, heavy);
    vfx.burst(e.center(), heavy ? 28 : 18, heavy ? 1.3 : 1); camera.shake(heavy ? .1 : .06); hitStop = Math.max(hitStop, heavy ? .07 : .045);
    if (heavy) face.set('happy', .7);
  }
}
// blade sweep (this frame + the arc since last frame) against every machine and breakable
function bladeHits(move) {
  const name = animator.name, base = character.bladeBase.getWorldPosition(new THREE.Vector3()), tip = character.bladeTip.getWorldPosition(new THREE.Vector3());
  tip.addScaledVector(tip.clone().sub(base).normalize(), HIT.tipExtend);       // invisible reach past the tip
  const segs = [[base, tip]]; if (previousTip) segs.push([previousTip, tip]); previousTip = tip.clone();
  const heavy = name === 'light3' || name === 'counter' || (name === 'flurry' && animator.time > 1.15);
  for (const [p, q] of segs) {
    breakables.hit(p, q, HIT.breakReach, character.root.position);
    for (const h of enemies.hitsAlong(p, q, HIT.bladeReach)) {
      const last = hitSet.get(h.e); if (last !== undefined && !(move.multi && animator.time - last >= move.multi)) continue;
      hitSet.set(h.e, animator.time);
      h.e.damage(DMG[name] ?? 15, character.root.position, heavy);
      vfx.burst(h.point, heavy ? 30 : 20, heavy ? 1.4 : 1); camera.shake(heavy ? .1 : .06); hitStop = Math.max(hitStop, heavy ? .07 : .045);
      if (heavy) face.set('happy', .7);
    }
  }
}
// HUD: health bar + hurt vignette
const hud = document.createElement('div'); hud.id = 'hud'; hud.innerHTML = '<div id="hp"><i></i><span></span></div><div id="kills"></div>'; document.body.append(hud);
const vignette = document.createElement('div'); vignette.id = 'hurt'; document.body.append(vignette);
// Summoning (monsters no longer spawn on their own): at the cursor, or 4 m in front.
let paused = false, stepFrames = 0;
const pauseTag = document.createElement('div'); pauseTag.id = 'paused'; pauseTag.innerHTML = '<b>PAUSED</b><span>C 다시 재생 · . 한 프레임 진행</span>'; document.body.append(pauseTag);
function togglePause() { paused = !paused; stepFrames = 0; pauseTag.classList.toggle('on', paused); mouse.right = false; }
function summon(type) {
  const root = character.root, p = cursorPoint() ?? root.position.clone().add(new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y)).multiplyScalar(4));
  if (p.distanceTo(root.position) < 1.6) p.copy(root.position).add(p.clone().sub(root.position).setY(0).setLength(1.6));
  enemies.spawnAt(type, p);
}
function clearMonsters() { for (const e of enemies.list) if (e.alive) e.die(); }
function togglePassive() { enemies.passive = !enemies.passive; summonBar.querySelector('[data-a=passive]').classList.toggle('on', enemies.passive); cue(enemies.passive ? '허수아비 모드: 몬스터가 공격하지 않음' : '몬스터 공격 켜짐', 1.4); }
const summonBar = document.createElement('div'); summonBar.id = 'summon';
summonBar.innerHTML = '<button data-a="crawler"><b>1</b>하운드 소환</button><button data-a="warden"><b>2</b>워든 소환</button><button data-a="clear"><b>0</b>전부 제거</button><button data-a="passive"><b>P</b>허수아비 모드</button>';
summonBar.addEventListener('click', e => { const a = e.target.closest('button')?.dataset.a; if (a === 'crawler' || a === 'warden') summon(a); else if (a === 'clear') clearMonsters(); else if (a === 'passive') togglePassive(); e.currentTarget.blur(); });
document.body.append(summonBar);

function start(name) { connected = false; previousTip = null; vfx.resetTrail(); return animator.play(name); }
const COOLDOWN = { KeyQ: 4, KeyW: 3, KeyE: 5, KeyR: 9, Space: 1.1, KeyF: .8 };
const ready = {};                        // key -> time when usable again
const canAct = () => !animator.busy || (/^light/.test(animator.name) && animator.time > (animator.move.impact ?? 0) + .04) || animator.name === 'heavyCharge';
function skill(code) {
  if (!player.alive || (ready[code] ?? 0) > realTime) return;
  if (code !== 'Space' && !canAct()) return;
  moveTarget = null;
  if (code === 'Space') { const p = cursorPoint(); if (p) faceCursor(); start('dash'); }
  else if (code === 'KeyQ') { faceCursor(); start('heavyCharge'); after(.2, () => { if (animator.name === 'heavyCharge') start('heavy'); }); face.set('wide', .2); }
  else if (code === 'KeyW') { faceCursor(); start('wave'); }
  else if (code === 'KeyE') { faceCursor(); start('counter'); }
  else if (code === 'KeyR') { faceCursor(); start('jump'); after(.3, () => { if (animator.name === 'jump') start('flurry'); }); }
  else if (code === 'KeyF') { start('jump'); }
  ready[code] = realTime + COOLDOWN[code];
}
function attack() {
  if (!player.alive) return;
  moveTarget = null;
  if (!animator.busy) { faceCursor(); start('light1'); return; }
  aimYaw = yawToCursor();
  const before = animator.name; animator.light(); if (before !== animator.name) { connected = false; previousTip = null; vfx.resetTrail(); }
}
function getHit() { const stagger = realTime - lastHitAt < 1; lastHitAt = realTime; start(stagger ? 'stagger' : 'hit'); camera.shake(.055); face.set('hurt', .6); }
window.addEventListener('keydown', e => {
  if (e.target instanceof HTMLInputElement || e.target instanceof HTMLButtonElement || e.target.tagName === 'SUMMARY') return;
  if (['Space', 'KeyQ', 'KeyW', 'KeyE', 'KeyR', 'KeyA', 'KeyF'].includes(e.code)) e.preventDefault();
  if (e.repeat) return;
  if (e.code === 'KeyA') attack();
  else if (e.code in COOLDOWN) skill(e.code);
  else if (e.code === 'Digit1') summon('crawler');
  else if (e.code === 'Digit2') summon('warden');
  else if (e.code === 'Digit0') clearMonsters();
  else if (e.code === 'KeyP') togglePassive();
  else if (e.code === 'KeyH') getHit();
  else if (e.code === 'KeyC') togglePause();
  else if (e.code === 'Period' && paused) stepFrames += 1;
  else if (e.code === 'KeyV') toggleCamera();
});
window.addEventListener('blur', () => { mouse.right = false; });

// Hit test: the blade sweep (this frame's blade + the arc from last frame's tip) against the
// dummy's body capsule, with a generous radius so the 3D swing reads as a wide hit.
const HIT_RADIUS = .95;
function connect(name, move, point) {
  if (connected && !(move.multi && animator.time - lastConnect >= move.multi)) return;
  connected = true; lastConnect = animator.time; hitCount++;
  const power = move.slam || name === 'light3' || name === 'heavy' ? 1.5 : 1;
  dummy.hit(power); vfx.burst(point, move.slam ? 36 : 24, power); camera.shake(move.slam ? .13 : .07); hitStop = move.slam ? .085 : .06;
  if (name === 'light3' || name === 'flurry' || name === 'counter') face.set('happy', .8);
}
function dummyAxis() { return new THREE.Line3(dummy.position.clone().setY(.2), dummy.position.clone().setY(1.7)); }
function bladeHit(move) {
  const base = character.bladeBase.getWorldPosition(new THREE.Vector3()), tip = character.bladeTip.getWorldPosition(new THREE.Vector3());
  const axis = dummyAxis(), a = new THREE.Vector3(), b = new THREE.Vector3();
  const segs = [[base, tip]]; if (previousTip) segs.push([previousTip, tip]);
  let best = null;
  for (const [p, q] of segs) { const d = segDist(p, q, axis.start, axis.end, a, b); if (!best || d < best.d) best = { d, point: a.clone().lerp(b, .35) }; }
  previousTip = tip;
  return best && best.d < HIT_RADIUS ? best.point : null;
}
const _d1 = new THREE.Vector3(), _d2 = new THREE.Vector3(), _r = new THREE.Vector3();
function segDist(p1, q1, p2, q2, c1, c2) {
  _d1.subVectors(q1, p1); _d2.subVectors(q2, p2); _r.subVectors(p1, p2);
  const a = _d1.dot(_d1), e = _d2.dot(_d2), f = _d2.dot(_r), c = _d1.dot(_r), b = _d1.dot(_d2), den = a * e - b * b;
  let s = den > 1e-9 ? THREE.MathUtils.clamp((b * f - c * e) / den, 0, 1) : 0, t = (b * s + f) / e;
  if (t < 0) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); } else if (t > 1) { t = 1; s = THREE.MathUtils.clamp((b - c) / a, 0, 1); }
  c1.copy(p1).addScaledVector(_d1, s); c2.copy(p2).addScaledVector(_d2, t); return c1.distanceTo(c2);
}

// ---------- UI ----------
function setTimeScale(value) { const x = Number(value); if (!Number.isFinite(x)) return; timeScale = THREE.MathUtils.clamp(x, .05, 1.5); document.querySelector('#speed').value = String(timeScale); document.querySelector('#speed-value').textContent = `${timeScale.toFixed(2)}×`; }
function toggleCamera() { document.querySelector('#view-name').textContent = camera.toggle() ? 'SIDE / MOTION PROFILE' : 'TOP-DOWN'; }
function preview(name) {
  if (!(name in moves)) return false;
  character.root.position.set(0, 0, name === 'roll' ? -2 : name === 'dash' ? -1.8 : -.25); character.root.rotation.set(0, 0, 0); moveTarget = null;
  hitStop = 0; animator.pendingYaw = 0; character.root.updateMatrixWorld(true); start(name); animator.resetFeet(); secondary.reset(); return true;
}
const buttons = new Map();
Object.entries(moves).forEach(([name, move], i) => { const button = document.createElement('button'); button.type = 'button'; button.title = `Preview ${move.label}`; button.innerHTML = `<span class="num">${String(i + 1).padStart(2, '0')}</span>${move.label}<small>${move.key}</small>`; button.addEventListener('click', () => preview(name)); document.querySelector('#moves').append(button); buttons.set(name, button); });
document.querySelector('#speed').addEventListener('input', e => setTimeScale(e.target.value));
document.querySelector('#skeleton').addEventListener('change', e => character.skeleton.visible = e.target.checked);
document.querySelector('#colliders')?.addEventListener('change', e => character.colliders.visible = e.target.checked);
document.querySelector('#camera').addEventListener('click', toggleCamera);
// Skill bar
const bar = document.createElement('div'); bar.id = 'skillbar';
const SLOTS = [['KeyA', 'A', '연속 베기'], ['KeyQ', 'Q', '회오리'], ['KeyW', 'W', '레이저 파동'], ['KeyE', 'E', '반격'], ['KeyR', 'R', '칼날 폭풍'], ['Space', 'SPACE', '대쉬'], ['KeyF', 'F', '점프']];
const slotEls = {};
for (const [code, keyLabel, name] of SLOTS) { const el = document.createElement('div'); el.className = 'slot'; el.innerHTML = `<b>${keyLabel}</b><span>${name}</span><i></i>`; bar.append(el); slotEls[code] = el; }
document.body.append(bar);

// ---------- automation / audit ----------
window.__proto = { audit, state: () => animator.name, play: preview, setTimeScale, moves: Object.keys(moves), colliders: v => { character.colliders.visible = !!v; },
  inspect: () => ({ time: animator.time, root: character.root.position.toArray(), hitCount, speed: animator.speed, stats: { ...animator.stats } }),
  input: { attack, skill, moveTo: (x, z) => { moveTarget = new THREE.Vector3(x, 0, z); } },
  // run the game synchronously (works with the pane hidden): seconds of 120 Hz substeps
  simulate: (seconds, input = new THREE.Vector3()) => { const n = Math.round(seconds * 120); for (let i = 0; i < n; i++) worldStep(1 / 120, input); } };
function audit(list = Object.keys(moves)) {
  const out = {}, zero = new THREE.Vector3();
  for (const name of list) {
    character.root.position.set(0, 0, 0); character.root.rotation.set(0, 0, 0); animator.play('idle'); animator.resetFeet();
    for (let i = 0; i < 60; i++) animator.update(1 / 120, zero, false);
    animator.stats = {};
    if (name === 'heavy') { animator.play('heavyCharge'); for (let i = 0; i < 40; i++) animator.update(1 / 120, zero, false); }
    animator.play(name); const steps = Math.ceil((moves[name].duration + .35) * 120);
    for (let i = 0; i < steps; i++) { animator.update(1 / 120, zero, false); if (animator.name === 'heavyCharge' && i > 150) break; }
    out[name] = Object.fromEntries(Object.entries(animator.stats).filter(([, v]) => v > .0005).map(([k, v]) => [k, +v.toFixed(3)]));
  }
  character.root.position.set(0, 0, -.25); animator.play('idle'); animator.resetFeet(); animator.stats = {};
  return out;
}
window.__dbg = { character, animator, THREE, vfx, camera, face, enemies, player, breakables };
function resize() { renderer.setSize(innerWidth, innerHeight); composer.setSize(innerWidth, innerHeight); camera.camera.aspect = innerWidth / innerHeight; camera.camera.updateProjectionMatrix(); }
window.addEventListener('resize', resize);

// ---------- frame ----------
let previousTime = performance.now(), lastName = animator.name;
document.addEventListener('visibilitychange', () => { previousTime = performance.now(); });
function frame(now) {
  requestAnimationFrame(frame); const realDt = Math.min((now - previousTime) / 1000, .05); previousTime = now; realTime += realDt;
  // Pause (C): the world freezes, the camera keeps rendering; '.' advances one 1/60 s frame.
  if (paused) { const step = stepFrames > 0 ? 1 / 60 : 0; stepFrames = Math.max(0, stepFrames - 1); if (!step) { camera.update(0, character.root.position); composer.render(); return; } }
  for (let i = timers.length - 1; i >= 0; i--) if (timers[i][0] <= realTime) { const fn = timers[i][1]; timers.splice(i, 1); fn(); }
  let dt = paused ? 1 / 60 : realDt * timeScale; if (hitStop > 0) { hitStop = Math.max(0, hitStop - realDt); dt = 0; }
  if (mouse.right) setMoveTarget(false);
  // click-to-move: steer toward the target, stop on arrival; attacks cancel movement
  let input = new THREE.Vector3();
  if (moveTarget && !animator.busy) {
    const d = moveTarget.clone().sub(character.root.position); d.y = 0; const dist = d.length();
    if (dist < .12) moveTarget = null; else input = d.normalize().multiplyScalar(Math.min(1, dist / .45 + .25));
  }
  let remaining = dt;
  do {
    const step = Math.min(remaining, 1 / 120); worldStep(step, input);
    remaining -= step;
  } while (remaining > 1e-7);
  frameTail(realDt);
}
// One simulation substep: animation, hits, waves, enemies, effects. Also used by
// __proto.simulate() so tests run without a visible browser (rAF stops when hidden).
function worldStep(step, input) {
    animator.update(step, input, true);
    if (animator.name !== lastName) { connected = false; previousTip = null; lastName = animator.name; hitSet.clear(); }
    const move = animator.move, active = !!move?.active && animator.time >= move.active[0] && animator.time <= move.active[1];
    if (active && !move.parry) { bladeHits(move); if (animator.time >= (move.impact ?? move.active[0]) - .02) areaHits(move); } else if (!active) previousTip = null;
    for (const w of vfx.waves) {
      w.hitSet ??= new Set(); const c = w.group.position;
      for (const e of enemies.list) if (e.alive && !w.hitSet.has(e) && Math.hypot(c.x - e.root.position.x, c.z - e.root.position.z) < HIT.wave + e.radius * .5) { w.hitSet.add(e); e.damage(30, character.root.position, true); vfx.burst(e.center(), 24, 1.2); camera.shake(.06); }
      breakables.hit(c.clone().setY(.2), c.clone().setY(1.2), .9, c);
    }
    enemies.update(step, player); breakables.update(step);
    // afterimages: dense on the dash, lighter through spins
    if (step > 0) {
      ghostClock -= step;
      const dashing = animator.name === 'dash' && animator.time > .02 && animator.time < .32;
      const spinning = (animator.name === 'light3' && active) || animator.name === 'heavy' && active || animator.name === 'flurry' && active;
      if ((dashing || spinning) && ghostClock <= 0) { vfx.afterimage(character.root, dashing ? 1 : .55); ghostClock = dashing ? .03 : .07; }
    }
    secondary.update(step, animator.worldTime, animator.speed); dummy.update(step);
    vfx.update(step, active, animator.name === 'heavyCharge' ? animator.charge : 0);
    laser.update(step, active ? 1 : animator.name === 'heavyCharge' ? .6 : 0);
}
function frameTail(realDt) {
  const state = /^(hit|stagger)$/.test(animator.name) ? 'hurt' : animator.name === 'heavyCharge' ? 'wide' : animator.busy && animator.name !== 'jump' ? 'focus' : 'idle';
  face.update(realDt, state);
  if (character.root.position.length() > BOUNDS) character.root.position.setLength(BOUNDS);
  world.updateAmbient?.(realDt, character.root.position);
  hurtFlash = Math.max(0, hurtFlash - realDt * 2.5); vignette.style.opacity = String(hurtFlash * .8);
  hud.querySelector('#hp i').style.width = `${player.hp / player.max * 100}%`; hud.querySelector('#hp span').textContent = `${Math.ceil(player.hp)} / ${player.max}`;
  hud.querySelector('#kills').textContent = `처치 ${kills}`;
  character.updateSkeleton(); camera.update(realDt, character.root.position);
  sun.target.position.copy(character.root.position); sun.position.copy(character.root.position).add(new THREE.Vector3(-5, 9, 4));
  stateEl.textContent = animator.name.replace(/([a-z])([A-Z])/g, '$1 $2').toUpperCase(); progressEl.style.width = `${animator.move ? animator.time / animator.move.duration * 100 : 0}%`;
  document.querySelector('#chain').textContent = animator.name.startsWith('light') ? `0${animator.name.slice(-1)} / 03` : '— / 03';
  for (const [name, button] of buttons) button.classList.toggle('active', name === animator.name);
  for (const [code, el] of Object.entries(slotEls)) { const left = Math.max(0, (ready[code] ?? 0) - realTime), cd = COOLDOWN[code] ?? 1; el.style.setProperty('--cd', `${left > 0 ? left / cd * 360 : 0}deg`); el.classList.toggle('cooling', left > 0); }
  if (realTime > cueUntil) cueEl.textContent = '우클릭 이동 · A 공격 · Q W E R 스킬 · SPACE 대쉬 · F 점프 · 휠 줌';
  composer.render();
}
camera.update(1, character.root.position); requestAnimationFrame(frame); document.querySelector('#loading').classList.add('done');
