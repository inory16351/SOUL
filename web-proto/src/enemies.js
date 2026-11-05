import * as THREE from 'three';
import { createCrawler, createWarden } from './enemy_models.js';

// ---------- Machines possessed by human souls ----------
// Every attack is telegraphed on the ground first (red area that fills up), then lands at
// the end of the wind-up: step out of it or dash through it (dash = brief invulnerability).
const RED = new THREE.Color(1.6, .16, .08), SOUL = new THREE.Color(3, .75, .3);
const V = () => new THREE.Vector3();
const damp = (r, dt) => 1 - Math.exp(-r * dt);
const angleTo = (from, to) => THREE.MathUtils.euclideanModulo(to - from + Math.PI, Math.PI * 2) - Math.PI;

// ---------- ground telegraphs ----------
class Telegraph {
  // kind: 'line' {width, length}, 'cone' {radius, angle}, 'circle' {radius}; yaw = facing (0 = +Z)
  constructor(scene, kind, origin, yaw, opts, duration) {
    Object.assign(this, { scene, kind, origin: origin.clone().setY(0), yaw, opts, duration, t: 0 });
    const mat = op => new THREE.MeshBasicMaterial({ color: RED, transparent: true, opacity: op, depthWrite: false, toneMapped: false, side: THREE.DoubleSide });
    const shape = () => {
      if (kind === 'line') { const g = new THREE.PlaneGeometry(opts.width, opts.length); g.translate(0, opts.length / 2, 0); return g; }
      if (kind === 'cone') return new THREE.CircleGeometry(opts.radius, 40, Math.PI / 2 - opts.angle / 2, opts.angle);
      return new THREE.CircleGeometry(opts.radius, 48);
    };
    const edge = () => {
      if (kind === 'line') { const g = new THREE.EdgesGeometry(shape()); return g; }
      if (kind === 'cone') { const r = opts.radius, a = opts.angle, pts = [new THREE.Vector3()]; for (let i = 0; i <= 32; i++) { const th = Math.PI / 2 - a / 2 + a * i / 32; pts.push(new THREE.Vector3(Math.cos(th) * r, Math.sin(th) * r, 0)); } pts.push(new THREE.Vector3()); return new THREE.BufferGeometry().setFromPoints(pts); }
      return new THREE.EdgesGeometry(new THREE.CircleGeometry(opts.radius, 48));
    };
    this.group = new THREE.Group(); this.group.position.copy(this.origin).setY(.045); this.group.rotation.set(Math.PI / 2, 0, 0);   // shape +Y -> world +Z (facing)
    const holder = new THREE.Group(); holder.rotation.z = -yaw; this.group.add(holder);   // ground plane: +Y of the shape = facing
    this.base = new THREE.Mesh(shape(), mat(.16)); this.fill = new THREE.Mesh(shape(), mat(.34));
    this.line = new THREE.LineLoop(edge(), new THREE.LineBasicMaterial({ color: RED, transparent: true, opacity: .9, toneMapped: false }));
    holder.add(this.base, this.fill, this.line); this.fill.scale.setScalar(.001);
    scene.add(this.group);
  }
  update(dt) {
    this.t += dt; const p = Math.min(1, this.t / this.duration);
    if (this.kind === 'line') this.fill.scale.set(1, Math.max(.001, p), 1); else this.fill.scale.setScalar(Math.max(.001, p));
    const blink = p > .8 ? .55 + .45 * Math.sin(this.t * 50) : 1;
    this.line.material.opacity = .9 * blink; this.fill.material.opacity = .22 + .2 * p;
    return p >= 1;
  }
  contains(point, pad = .35) {
    const d = point.clone().setY(0).sub(this.origin), f = new THREE.Vector3(Math.sin(this.yaw), 0, Math.cos(this.yaw)), r = new THREE.Vector3(f.z, 0, -f.x);
    const along = d.dot(f), side = d.dot(r);
    if (this.kind === 'line') return along > -pad && along < this.opts.length + pad && Math.abs(side) < this.opts.width / 2 + pad;
    const dist = d.length();
    if (this.kind === 'circle') return dist < this.opts.radius + pad;
    if (dist > this.opts.radius + pad) return false;
    return dist < pad * 2 || Math.abs(Math.atan2(side, along)) < this.opts.angle / 2 + pad / Math.max(dist, .3);
  }
  dispose() { this.scene.remove(this.group); this.group.traverse(o => { o.geometry?.dispose(); o.material?.dispose(); }); }
}

// ---------- souls ----------
function glowTexture() {
  const c = document.createElement('canvas'); c.width = c.height = 64; const g = c.getContext('2d'), gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  gr.addColorStop(0, 'rgba(255,220,180,1)'); gr.addColorStop(.3, 'rgba(255,90,40,.7)'); gr.addColorStop(1, 'rgba(160,20,10,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 64, 64); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

// ---------- enemy types ----------
const TYPES = {
  crawler: {
    make: createCrawler, hp: 60, speed: 3.3, turn: 7, keep: 1.1, poise: 0, damageScale: 1,
    attacks: [
      { name: 'lunge', range: [2.5, 6.5], cooldown: 2.6, windup: .75, kind: 'line', opts: { width: 1.3, length: 5.2 }, damage: 14, dash: 5.0, dashTime: .22, recover: .7 },
      { name: 'bite', range: [0, 2.2], cooldown: 1.6, windup: .55, kind: 'cone', opts: { radius: 2.1, angle: 1.3 }, damage: 10, recover: .55 },
    ],
  },
  warden: {
    make: createWarden, hp: 240, speed: 1.55, turn: 3.2, keep: 1.8, poise: 60, damageScale: 1,
    attacks: [
      { name: 'sweep', range: [0, 3.4], cooldown: 3.0, windup: .95, kind: 'cone', opts: { radius: 3.8, angle: 2.5 }, damage: 22, recover: .85 },
      { name: 'slam', range: [2.5, 7], cooldown: 4.5, windup: 1.15, kind: 'circle', opts: { radius: 2.5 }, damage: 30, recover: 1.0, atTarget: true },
      { name: 'thrust', range: [3, 7.5], cooldown: 3.8, windup: .85, kind: 'line', opts: { width: 1.4, length: 6.5 }, damage: 24, recover: .9 },
    ],
  },
};

class Enemy {
  constructor(mgr, type, pos) {
    const def = TYPES[type]; this.mgr = mgr; this.type = type; this.def = def;
    const m = def.make(); Object.assign(this, { root: m.root, parts: m.parts, radius: m.radius, height: m.height });
    this.root.position.copy(pos); this.root.rotation.y = Math.random() * Math.PI * 2; mgr.scene.add(this.root);
    this.hp = def.hp; this.state = 'spawn'; this.t = 0; this.cool = {}; this.walk = Math.random() * 10; this.poiseDmg = 0;
    this.flash = 0; this.velocity = V(); this.attack = null; this.tele = null; this.hitPlayer = false;
    this.eyeColor = this.parts.eye?.material.color.clone() ?? SOUL.clone(); this.coreColor = this.parts.core?.material.color.clone() ?? SOUL.clone();
    this.hpBar = mgr.makeBar(); this.root.add(this.hpBar.sprite); this.hpBar.sprite.position.y = this.height + .45;
    this.root.scale.setScalar(.01);
    mgr.vfx.flash(pos.clone().setY(.8), 1.2);
  }
  get alive() { return this.state !== 'dead'; }
  center() { return this.root.position.clone().setY(this.height * .5); }
  axis() { const p = this.root.position; return [p.clone().setY(.2), p.clone().setY(Math.max(.4, this.height - .25))]; }
  damage(amount, from, heavy = false) {
    if (!this.alive || this.state === 'spawn') return false;
    this.hp -= amount; this.flash = 1; this.hpBar.set(this.hp / this.def.hp);
    const away = this.root.position.clone().sub(from).setY(0).normalize();
    this.velocity.addScaledVector(away, this.type === 'crawler' ? 4 : 1.2);
    this.poiseDmg += amount;
    if (this.hp <= 0) { this.die(); return true; }
    // light machines flinch out of wind-ups; the warden only breaks when its poise is exhausted
    if ((this.type === 'crawler' && (heavy || Math.random() < .35)) || (this.def.poise && this.poiseDmg >= this.def.poise)) { this.poiseDmg = 0; this.toStagger(heavy ? .9 : .45); }
    return true;
  }
  toStagger(time) { this.clearTele(); this.state = 'stagger'; this.t = 0; this.staggerTime = time; }
  clearTele() { if (this.tele) { this.tele.dispose(); this.tele = null; } }
  die() {
    this.clearTele(); this.state = 'dead'; this.t = 0; this.hpBar.sprite.visible = false;
    this.mgr.releaseSoul(this.center(), this.type === 'warden' ? 34 : 16);
    this.mgr.onKill?.(this);
  }
  update(dt, player) {
    this.t += dt; const P = this.parts, root = this.root, def = this.def;
    for (const k in this.cool) this.cool[k] -= dt;
    // knockback / separation velocity
    root.position.addScaledVector(this.velocity, dt); this.velocity.multiplyScalar(Math.exp(-dt * 8));
    const toP = player.pos.clone().sub(root.position).setY(0), dist = toP.length(), yawToP = Math.atan2(toP.x, toP.z);
    const turnTo = (yaw, rate) => { root.rotation.y += angleTo(root.rotation.y, yaw) * damp(rate, dt); };
    let move = 0, pose = 'walk';
    if (this.state === 'spawn') {
      root.scale.setScalar(Math.min(1, .01 + this.t * 1.6)); if (this.t > .65) { root.scale.setScalar(1); this.state = 'chase'; this.t = 0; }
      pose = 'idle';
    } else if (this.state === 'chase') {
      turnTo(yawToP, def.turn);
      const options = def.attacks.filter(a => dist >= a.range[0] && dist <= a.range[1] && !(this.cool[a.name] > 0));
      if (options.length && this.t > .35 && player.alive && Math.abs(angleTo(root.rotation.y, yawToP)) < .5) {
        const a = options[Math.floor(Math.random() * options.length)];
        this.attack = a; this.state = 'windup'; this.t = 0; this.hitPlayer = false;
        const origin = a.atTarget ? player.pos.clone() : root.position.clone().addScaledVector(new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y)), a.kind === 'line' ? this.radius * .6 : .1);
        this.tele = new Telegraph(this.mgr.scene, a.kind, origin, root.rotation.y, a.opts, a.windup);
      } else if (dist > def.keep + this.radius) move = def.speed * Math.min(1, (dist - def.keep - this.radius) / 1.2 + .3);
    } else if (this.state === 'windup') {
      pose = 'windup';
      if (this.tele.update(dt)) {
        // resolve: is the player inside the telegraph right now?
        if (player.alive && this.tele.contains(player.pos)) this.mgr.onPlayerHit(this.attack.damage, root.position.clone(), this);
        this.mgr.impact(this, this.attack);
        this.clearTele(); this.state = 'attack'; this.t = 0; this.cool[this.attack.name] = this.attack.cooldown;
        if (this.attack.dash) this.dashDir = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y));
      }
    } else if (this.state === 'attack') {
      pose = 'attack';
      const a = this.attack;
      if (a.dash && this.t - dt < a.dashTime) { const e = x => 1 - (1 - Math.min(1, x)) ** 3, k = e(this.t / a.dashTime) - e((this.t - dt) / a.dashTime); root.position.addScaledVector(this.dashDir, a.dash * k); }
      if (this.t > (a.dashTime ?? .18)) { this.state = 'recover'; this.t = 0; }
    } else if (this.state === 'recover') {
      pose = 'recover'; if (this.t > this.attack.recover) { this.state = 'chase'; this.t = 0; }
    } else if (this.state === 'stagger') {
      pose = 'stagger'; if (this.t > this.staggerTime) { this.state = 'chase'; this.t = 0; }
    } else if (this.state === 'dead') {
      pose = 'dead';
      root.position.y = -Math.max(0, this.t - 1.2) * .5;
      if (this.t > 3) return false;
    }
    this.curSpeed = THREE.MathUtils.lerp(this.curSpeed ?? 0, move, damp(move > this.curSpeed ? 3.5 : 6, dt));
    if (this.curSpeed > .01) { const f = new THREE.Vector3(Math.sin(root.rotation.y), 0, Math.cos(root.rotation.y)); root.position.addScaledVector(f, this.curSpeed * dt); }
    move = this.curSpeed;
    this.walk += dt * (move > 0 ? move * (this.type === 'crawler' ? 3.2 : 1.6) : .4);
    // keep inside the arena
    const b = this.mgr.bounds; if (root.position.length() > b) root.position.setLength(b);
    this.animate(dt, pose, move);
    // eye / soul glow: flare on wind-up, white flash on hit
    const flare = pose === 'windup' ? .8 + .6 * Math.min(1, this.t / (this.attack?.windup ?? 1)) : 0;
    this.flash = Math.max(0, this.flash - dt * 6);
    if (P.eye) P.eye.material.color.copy(this.eyeColor).multiplyScalar(1 + flare).lerp(new THREE.Color(4, 4, 4), this.flash);
    if (P.core) P.core.material.color.copy(this.coreColor).multiplyScalar(.9 + .25 * Math.sin(this.t * 6) + flare * .6);
    if (this.alive && Math.random() < dt * (this.type === 'warden' ? 6 : 2.5)) this.mgr.wisp(P.core ? P.core.getWorldPosition(V()) : this.center(), .5);
    return true;
  }
  animate(dt, pose, move) {
    const P = this.parts, w = this.walk, s = Math.sin, k = THREE.MathUtils.lerp;
    const wind = pose === 'windup' ? Math.min(1, this.t / this.attack.windup) : 0;
    if (this.type === 'crawler') {
      const gait = move > 0 ? 1 : .15;
      (P.legs ?? []).forEach((L, i) => {
        const ph = w * 2 + (i % 2 ? Math.PI : 0) + L.index * .5;
        L.hip.rotation.x = s(ph) * .55 * gait; L.hip.rotation.z = L.side * (.25 + Math.max(0, Math.cos(ph)) * .35 * gait + wind * .3);
        if (L.knee) L.knee.rotation.x = -.4 - Math.max(0, s(ph + 1)) * .5 * gait - wind * .3;
      });
      const rear = pose === 'windup' ? wind : pose === 'attack' ? 1 : pose === 'recover' ? Math.max(0, 1 - this.t * 2) : 0;
      if (P.body) { P.body.rotation.x = -rear * .45 + (pose === 'stagger' ? .3 : 0); P.body.position.y = (P.body.userData.y0 ??= P.body.position.y) + Math.abs(s(w * 4)) * .04 * (move > 0 ? 1 : 0); }
      if (P.head) P.head.rotation.x = -rear * .3 + s(this.t * 7) * .04 * (1 - rear);
      if (P.jaw) P.jaw.rotation.x = rear * .7 + (pose === 'attack' ? .3 : 0);
      if (pose === 'dead') this.root.rotation.z = k(this.root.rotation.z, 2.6, damp(5, dt));
    } else {
      const gait = move > 0 ? 1 : 0;
      for (const [L, sd] of [[P.legL, 1], [P.legR, -1]]) {
        if (!L) continue; const ph = w * 2 + (sd > 0 ? 0 : Math.PI);
        L.hip.rotation.x = s(ph) * .45 * gait - .2; if (L.knee) L.knee.rotation.x = .6 + Math.max(0, s(ph + 1)) * .5 * gait; if (L.ankle) L.ankle.rotation.x = -.4;
      }
      const a = this.attack?.name, rec = pose === 'recover' ? Math.max(0, 1 - this.t * 1.5) : 0, hit = pose === 'attack' ? 1 : rec;
      if (P.torso) { P.torso.rotation.x = .12 + (a === 'slam' ? (-wind * .4 + hit * .6) : hit * .2) + s(this.t * 1.3) * .03; P.torso.rotation.y = a === 'sweep' ? (wind * .9 - hit * 1.2) : s(w) * .08; }
      if (P.head) P.head.rotation.x = -.1 + s(this.t * 2.1) * .05;
      for (const [arm, sd] of [[P.armL, -1], [P.armR, 1]]) {
        if (!arm) continue;
        let sx = -.2 + s(w + (sd > 0 ? Math.PI : 0)) * .25 * gait, sz = sd * .25, el = -.5;
        if (a === 'slam') { sx = k(sx, -2.7, wind) * (1 - hit) + (-.6) * hit; el = k(-.5, -.3, wind); }
        else if (a === 'sweep' && sd > 0) { sx = k(sx, -1.2, wind); sz = k(sz, 1.4, wind) * (1 - hit) + (-.6) * hit; }
        else if (a === 'thrust' && sd > 0) { sx = k(sx, .6, wind) * (1 - hit) + (-1.55) * hit; el = k(-.5, -1.6, wind) * (1 - hit); }
        arm.shoulder.rotation.x = sx; arm.shoulder.rotation.z = sz; if (arm.elbow) arm.elbow.rotation.x = el;
      }
      if (pose === 'stagger' && P.torso) P.torso.rotation.x -= .35;
      if (pose === 'dead') { this.root.rotation.x = k(this.root.rotation.x, -1.45, damp(3, dt)); }
    }
  }
  dispose() { this.clearTele(); this.mgr.scene.remove(this.root); }
}

export class EnemyManager {
  constructor(scene, vfx, { bounds = 18, onPlayerHit, onImpact, onKill } = {}) {
    Object.assign(this, { scene, vfx, bounds, onPlayerHit, onImpact, onKill });
    this.list = []; this.wisps = []; this.glow = glowTexture(); this.spawnClock = 1.2; this.wave = 0;
    this.want = { crawler: 3, warden: 1 };
  }
  makeBar() {
    const c = document.createElement('canvas'); c.width = 128; c.height = 14; const g = c.getContext('2d'), tex = new THREE.CanvasTexture(c);
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, depthWrite: false, toneMapped: false })); sprite.scale.set(1.1, .12, 1);
    const set = f => { g.clearRect(0, 0, 128, 14); g.fillStyle = '#000a'; g.fillRect(0, 0, 128, 14); g.fillStyle = '#ff5a3a'; g.fillRect(2, 2, Math.max(0, f) * 124, 10); tex.needsUpdate = true; };
    set(1); return { sprite, set };
  }
  spawn(type, near) {
    for (let tries = 0; tries < 12; tries++) {
      const a = Math.random() * Math.PI * 2, r = 8 + Math.random() * 4, p = near.clone().add(new THREE.Vector3(Math.cos(a) * r, 0, Math.sin(a) * r)).setY(0);
      if (p.length() > this.bounds - 1) continue;
      const e = new Enemy(this, type, p); this.list.push(e); this.releaseSoul(p.clone().setY(.4), 10, true); return e;
    }
  }
  impact(enemy, attack) {
    const p = attack.atTarget && enemy.tele ? enemy.tele.origin : enemy.root.position.clone().addScaledVector(new THREE.Vector3(Math.sin(enemy.root.rotation.y), 0, Math.cos(enemy.root.rotation.y)), 1.2);
    this.onImpact?.(enemy, attack, p);
  }
  releaseSoul(pos, n, inward = false) {
    this.vfx.flash(pos, inward ? .8 : 1.6);
    for (let i = 0; i < n; i++) this.wisp(pos, 1.2, inward);
  }
  wisp(pos, power = .5, inward = false) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.glow, color: new THREE.Color(1.6, 1.1, 1), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false }));
    const off = new THREE.Vector3((Math.random() - .5) * .4, (Math.random() - .5) * .3, (Math.random() - .5) * .4);
    s.position.copy(pos).add(off); s.scale.setScalar(.12 + Math.random() * .12 * power); this.scene.add(s);
    const v = new THREE.Vector3((Math.random() - .5) * .6 * power, .6 + Math.random() * 1.2 * power, (Math.random() - .5) * .6 * power);
    if (inward) v.copy(off).multiplyScalar(-2).setY(.8);
    this.wisps.push({ s, v, life: 0, max: .8 + Math.random() * 1.2 * power, size: s.scale.x });
  }
  // blade / wave hit test against every living enemy's body axis
  closestHit(p, q, reach) {
    let best = null; const a = V(), b = V();
    for (const e of this.list) {
      if (!e.alive || e.state === 'spawn') continue;
      const [s0, s1] = e.axis(), d = segDist(p, q, s0, s1, a, b) - e.radius * .8;
      if (d < reach && (!best || d < best.d)) best = { e, d, point: a.clone().lerp(b, .4) };
    }
    return best;
  }
  hitsAlong(p, q, reach) {
    const out = [], a = V(), b = V();
    for (const e of this.list) { if (!e.alive || e.state === 'spawn') continue; const [s0, s1] = e.axis(); if (segDist(p, q, s0, s1, a, b) - e.radius * .8 < reach) out.push({ e, point: a.clone().lerp(b, .4) }); }
    return out;
  }
  update(dt, player) {
    // keep the field populated
    this.spawnClock -= dt;
    if (this.spawnClock <= 0 && player.alive) {
      for (const [type, n] of Object.entries(this.want)) { const have = this.list.filter(e => e.type === type && e.alive).length; if (have < n) { this.spawn(type, player.pos); break; } }
      this.spawnClock = 1.6;
    }
    // separation between machines and from the player
    for (const e of this.list) {
      if (!e.alive) continue;
      for (const o of this.list) { if (o === e || !o.alive) continue; const d = e.root.position.clone().sub(o.root.position).setY(0), l = d.length(), min = e.radius + o.radius; if (l < min && l > 1e-4) e.root.position.addScaledVector(d.normalize(), (min - l) * .5); }
      const d = e.root.position.clone().sub(player.pos).setY(0), l = d.length(), min = e.radius + .4; if (l < min && l > 1e-4) e.root.position.addScaledVector(d.normalize(), min - l);
    }
    this.list = this.list.filter(e => { const keep = e.update(dt, player); if (!keep) e.dispose(); return keep; });
    for (let i = this.wisps.length - 1; i >= 0; i--) {
      const w = this.wisps[i]; w.life += dt; const t = w.life / w.max;
      if (t >= 1) { this.scene.remove(w.s); w.s.material.dispose(); this.wisps.splice(i, 1); continue; }
      w.v.x += Math.sin(w.life * 5 + i) * dt * .8; w.s.position.addScaledVector(w.v, dt); w.s.material.opacity = (1 - t) ** 1.5; w.s.scale.setScalar(w.size * (1 + t * .8));
    }
  }
  clearNear(pos, r) { for (const e of this.list) if (e.alive && e.root.position.distanceTo(pos) < r) e.die(); }
}

const _d1 = V(), _d2 = V(), _r = V();
function segDist(p1, q1, p2, q2, c1, c2) {
  _d1.subVectors(q1, p1); _d2.subVectors(q2, p2); _r.subVectors(p1, p2);
  const a = _d1.dot(_d1), e = _d2.dot(_d2), f = _d2.dot(_r), c = _d1.dot(_r), b = _d1.dot(_d2), den = a * e - b * b;
  let s = den > 1e-9 ? THREE.MathUtils.clamp((b * f - c * e) / den, 0, 1) : 0, t = e > 1e-9 ? (b * s + f) / e : 0;
  if (t < 0) { t = 0; s = a > 1e-9 ? THREE.MathUtils.clamp(-c / a, 0, 1) : 0; } else if (t > 1) { t = 1; s = a > 1e-9 ? THREE.MathUtils.clamp((b - c) / a, 0, 1) : 0; }
  c1.copy(p1).addScaledVector(_d1, s); c2.copy(p2).addScaledVector(_d2, t); return c1.distanceTo(c2);
}
