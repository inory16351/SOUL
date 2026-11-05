import * as THREE from 'three';
import { neutral, moves, BASE_BODY, STANCE_FEET, IDLE_GRIP, ARM_BLADE_BLOCKERS } from './moves.js';
import { REST_HEIGHT, ARM, solveTwoBone } from './rig.js';
import { clampJoint } from './limits.js';
import { createBody, deepest, BLADE_BLOCKERS, closestSegments } from './body.js';

const V3 = THREE.Vector3, Q4 = THREE.Quaternion;
const rad = Math.PI / 180, up = new V3(0, 1, 0);
const quat = angles => new Q4().setFromEuler(new THREE.Euler(...angles.map(a => a * rad), 'XYZ'));
const smooth = t => t * t * (3 - 2 * t);
const ease = (t, mode) => mode === 'in' ? t * t : mode === 'out' ? 1 - (1 - t) ** 2 : mode === 'smooth' ? smooth(t) : t;
const damp = (rate, dt) => 1 - Math.exp(-rate * dt);
const lerpAngle = (a, b, t) => a + (THREE.MathUtils.euclideanModulo(b - a + Math.PI, Math.PI * 2) - Math.PI) * t;
function hermite(a, b, c, d, t) { return .5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t); }

// Rig dimensions
const ANKLE = .10, THIGH = .43, SHIN = .40, UPPER = ARM.upper, FORE = ARM.fore, REACH = UPPER + FORE - .004;
// Arm-blade on a mechanical wrist: the blade model's origin is the hinge pin, the blade runs
// along its +Y to the tip, the edge faces +X. It is mounted on the right hand joint so that a
// straight wrist puts the blade in line with the forearm.
// The wrist is locked: forearm and blade form one rigid line (prosthetic blade). The line lags the
// upper arm slightly, so the shoulder leads and the blade follows through (whip from the shoulder).
const BLADE_FROM = .12, BLADE_END = 1.0, BLADE_R = .05, WRIST_MAX = 0, LAG_F = 0, LAG_D = .022;
const BLADE_LEN = BLADE_END;
// Joints that stay FK are clamped to their anatomical range every frame. Arms and legs are
// IK-driven (grip / planted feet), so they are audited after the solve instead.
const FK_CLAMP = ['spine', 'chest', 'neck', 'head', 'Lclavicle', 'Rclavicle', 'LupperArm', 'Lforearm'];
const TORSO_ELBOW = ['pelvis', 'belly', 'chest'];
const FOREARM_BLOCKERS = ['belly', 'chest', 'neck', 'head'];

function compile(move) {
  const keys = move.keys.map(k => ({ ...k, body: k.body.map((v, i) => v + BASE_BODY[i]), quaternions: Object.fromEntries(Object.entries(k.angles).map(([n, a]) => [n, quat(a)])) }));
  for (const name of Object.keys(neutral)) keys.forEach((k, i) => { if (i && k.quaternions[name].dot(keys[i - 1].quaternions[name]) < 0) k.quaternions[name].set(...k.quaternions[name].toArray().map(v => -v)); });
  return keys;
}
const compileGuides = list => list?.map(([t, u, f, d]) => ({ t, u: new V3(...u).normalize(), f: new V3(...f).normalize(), d: new V3(...d).normalize() }));
function sampleKey(gs, k, time, out) {
  let i = 0; while (i < gs.length - 2 && time > gs[i + 1].t) i++;
  const a = gs[Math.max(0, i - 1)], b = gs[i], c = gs[i + 1] ?? b, d = gs[Math.min(gs.length - 1, i + 2)];
  const t = c === b ? 0 : THREE.MathUtils.clamp((time - b.t) / (c.t - b.t), 0, 1);
  for (const ax of ['x', 'y', 'z']) out[ax] = hermite(a[k][ax], b[k][ax], c[k][ax], d[k][ax], t);
  return out.normalize();
}
function limitAngle(v, ref, maxDeg) {
  const ang = v.angleTo(ref) / rad; if (ang <= maxDeg) return v;
  const axis = ref.clone().cross(v); if (axis.lengthSq() < 1e-9) return v;
  return v.copy(ref).applyAxisAngle(axis.normalize(), maxDeg * rad);
}
// Catmull-Rom through the key directions with a LINEAR parameter (no stop at every key),
// renormalized: keys are authored less than 90 degrees apart so the arc stays clean.
function sampleUD(gs, time, outU, outD) {
  let i = 0; while (i < gs.length - 2 && time > gs[i + 1].t) i++;
  const a = gs[Math.max(0, i - 1)], b = gs[i], c = gs[i + 1] ?? b, d = gs[Math.min(gs.length - 1, i + 2)];
  const t = c === b ? 0 : THREE.MathUtils.clamp((time - b.t) / (c.t - b.t), 0, 1);
  for (const ax of ['x', 'y', 'z']) { outU[ax] = hermite(a.u[ax], b.u[ax], c.u[ax], d.u[ax], t); outD[ax] = hermite(a.d[ax], b.d[ax], c.d[ax], d.d[ax], t); }
  outU.normalize(); outD.normalize();
}
function slerpDir(v, target, w) {
  if (w >= 1) return v.copy(target);
  const q = new Q4().setFromUnitVectors(v.clone().normalize(), target), id = new Q4();
  return v.applyQuaternion(id.slerp(q, w)).normalize();
}
function sampleGuide(gs, time, outP, outD) {
  let i = 0; while (i < gs.length - 2 && time > gs[i + 1].t) i++;
  const a = gs[Math.max(0, i - 1)], b = gs[i], c = gs[i + 1] ?? b, d = gs[Math.min(gs.length - 1, i + 2)];
  const t = c === b ? 0 : smooth(THREE.MathUtils.clamp((time - b.t) / (c.t - b.t), 0, 1));
  for (const ax of ['x', 'y', 'z']) { outP[ax] = hermite(a.p[ax], b.p[ax], c.p[ax], d.p[ax], t); outD[ax] = hermite(a.d[ax], b.d[ax], c.d[ax], d.d[ax], t); }
  outD.normalize();
}
// Run cycle per leg, phase p in [0,1): [p, thigh X, knee X, foot X] (thigh -X = forward).
const RUN_KEYS = [[0, -30, 22, -5], [.15, -12, 38, 5], [.35, 18, 20, 35], [.60, -8, 105, 15], [.85, -48, 50, -10]];
function runLeg(p) {
  p = ((p % 1) + 1) % 1; const n = RUN_KEYS.length;
  let i = n - 1; for (let k = 0; k < n; k++) if (p >= RUN_KEYS[k][0]) i = k;
  const k1 = RUN_KEYS[i], k2 = RUN_KEYS[(i + 1) % n], k0 = RUN_KEYS[(i - 1 + n) % n], k3 = RUN_KEYS[(i + 2) % n];
  const t1 = k1[0], t2 = k2[0] > t1 ? k2[0] : k2[0] + 1, t = (p - t1) / (t2 - t1);
  return [1, 2, 3].map(c => hermite(k0[c], k1[c], k2[c], k3[c], t));
}
function curveAt(keys, time) {
  let i = 0; while (i < keys.length - 2 && time > keys[i + 1][0]) i++;
  const t = THREE.MathUtils.clamp((time - keys[i][0]) / (keys[i + 1][0] - keys[i][0]), 0, 1); return THREE.MathUtils.lerp(keys[i][1], keys[i + 1][1], smooth(t));
}
function rootAt(move, time) {
  const keys = move.root; let i = 0; while (i < keys.length - 2 && time > keys[i + 1][0]) i++;
  const t = THREE.MathUtils.clamp((time - keys[i][0]) / (keys[i + 1][0] - keys[i][0]), 0, 1); return THREE.MathUtils.lerp(keys[i][1], keys[i + 1][1], move.rootEase === 'linear' ? t : smooth(t));
}
function elbowFlexR(j) {
  const s = j.RupperArm.getWorldPosition(new V3()), e = j.Rforearm.getWorldPosition(new V3()), w = j.Rhand.getWorldPosition(new V3());
  return e.clone().sub(s).angleTo(w.clone().sub(e)) / rad;
}
const yawOf = q => { const f = new V3(0, 0, 1).applyQuaternion(q); return Math.atan2(f.x, f.z); };

export class Animator {
  constructor(character, events) {
    this.character = character; this.j = character.joints; this.events = events;
    this.name = 'idle'; this.time = 0; this.worldTime = 0; this.speed = 0; this.phase = 0; this.buffered = false; this.charge = 0;
    this.compiled = Object.fromEntries(Object.entries(moves).map(([n, m]) => [n, compile(m)]));
    this.guides = Object.fromEntries(Object.entries(moves).map(([n, m]) => [n, compileGuides(m.weapon)]));
    this.idleGuide = compileGuides([[0, ...IDLE_GRIP]]);
    this.velocity = new V3(); this.feet = {}; this.mfeet = {}; this.blend = 1; this.body = new V3(); this.idleBob = new V3();
    this.pose = Object.fromEntries(Object.entries(neutral).map(([n, a]) => [n, quat(a)]));
    this.stats = {}; this.edge = null; this.startYaw = 0;
    this.mountBlade(character);
    // Stretch the arm meshes along the bone to the longer arm bones.
    for (const s of ['L', 'R']) for (const [bone, ratio] of [['upperArm', ARM.upper / ARM.modelUpper], ['forearm', ARM.fore / ARM.modelFore]]) {
      const joint = this.j[s + bone], group = joint.children.find(c => c.name === s + bone + '-girth');
      if (group) group.scale.y *= ratio; else joint.children.filter(c => c.isMesh).forEach(c => { c.scale.y *= ratio; c.position.y *= ratio; });
    }
    this.bodyColliders = createBody(this.j); character.colliders = this.bodyColliders.group; character.bodyColliders = this.bodyColliders;
    this.apply();
  }
  mountBlade(character) {
    const sword = character.sword, hand = this.j.Rhand;
    for (const c of hand.children) if (c !== sword) c.visible = false;     // no hand: the wrist housing replaces it
    hand.add(sword);
    sword.quaternion.setFromAxisAngle(new V3(1, 0, 0), Math.PI);          // blade +Y -> hand -Y (continues the forearm)
    sword.position.set(0, 0, 0); sword.scale.set(1, 1, 1);
  }
  // ---------- Full-body dynamics ----------
  // The whole body reacts to the blade: its turning rate around the body twists pelvis, spine
  // and chest along with the cut (springs with overshoot -> follow-through), its velocity
  // leans the torso into the cut and drops the hips, the head counter-rotates to keep the gaze
  // on the target and the free arm flings out against the twist for balance.
  dynamics(dt) {
    const D = this.dyn ??= { twist: [0, 0], lean: [0, 0], side: [0, 0], drop: [0, 0] };
    const v = this.tipVel ?? new V3(), r = this.tipRel ?? new V3(0, 0, 1);
    // full strength through wind-up and the cut, calmer once the strike has resolved
    const m = this.move, after = m?.active ? this.time - m.active[1] : -1;
    const on = this.busy && !m.roll ? (after < .12 ? 1 : THREE.MathUtils.lerp(1, .3, Math.min(1, (after - .12) / .12))) : 0;
    const rr = Math.max(.15, r.x * r.x + r.z * r.z), omega = (r.z * v.x - r.x * v.z) / rr;   // rad/s about +Y (+ = turning right)
    const speed = v.length();
    const targets = {
      twist: on * THREE.MathUtils.clamp(omega * 9, -30, 30),
      lean: on * THREE.MathUtils.clamp(v.z * 2.2, -10, 14),
      side: on * THREE.MathUtils.clamp(-v.x * 2.0, -12, 12),
      drop: on * Math.min(.075, speed * .007),
    };
    const spring = (s, target, f, zeta) => { const w = 2 * Math.PI * f; s[1] += (w * w * (target - s[0]) - 2 * zeta * w * s[1]) * dt; s[0] += s[1] * dt; return s[0]; };
    const tw = spring(D.twist, targets.twist, 3.2, .42), ln = spring(D.lean, targets.lean, 4, .6), sd = spring(D.side, targets.side, 4, .6), dr = spring(D.drop, targets.drop, 5, .7);
    const add = (name, x, y, z) => { const e = new THREE.Euler().setFromQuaternion(this.pose[name], 'XYZ'); e.x += x * rad; e.y += y * rad; e.z += z * rad; this.pose[name].setFromEuler(e); };
    add('pelvis', 0, tw * .35, 0);
    add('spine', ln * .5, tw * .30, sd * .5);
    add('chest', ln * .5, tw * .45, sd * .5);
    add('neck', -ln * .3, -tw * .35, -sd * .3);
    add('head', -ln * .3, -tw * .55, -sd * .3);
    add('LupperArm', -tw * 1.1, 0, -Math.abs(tw) * .7);
    add('Lforearm', -Math.abs(tw) * .4, 0, 0);
    this.body.y -= dr;
    this.dynOut = { tw, ln, sd, dr };
  }
  get move() { return moves[this.name]; }
  get busy() { return !!this.move; }
  stat(k, v) { if (v > (this.stats[k] ?? 0)) this.stats[k] = v; }
  play(name) {
    if (!moves[name] && name !== 'idle') return false;
    this.from = Object.fromEntries(Object.keys(neutral).map(n => [n, this.j[n].quaternion.clone()]));
    this.fromBody = this.j.pelvis.position.clone().sub(new V3(0, REST_HEIGHT, 0));
    const ankles = Object.fromEntries(['L', 'R'].map(s => { const p = this.j[s + 'foot'].getWorldPosition(new V3()); p.y = Math.max(p.y, ANKLE); return [s, p]; }));
    if (name === 'idle') this.feet = Object.fromEntries(['L', 'R'].map(s => [s, { target: ankles[s].clone(), start: ankles[s].clone(), wasStance: true, moving: true }]));
    else this.mfeet = Object.fromEntries(['L', 'R'].map(s => [s, { pos: ankles[s].clone(), yaw: yawOf(this.j[s + 'foot'].getWorldQuaternion(new Q4())) }]));
    if (name !== 'idle') this.events.willPlay?.(name);
    this.name = name; this.time = 0; this.blend = 0; this.buffered = false; this.impacted = false;
    this.startYaw = this.pendingYaw ?? this.character.root.rotation.y; this.pendingYaw = undefined;
    this.yawOffset = THREE.MathUtils.euclideanModulo(this.character.root.rotation.y - this.startYaw + Math.PI, Math.PI * 2) - Math.PI; this.prevTipL = null;
    this.velocity.set(0, 0, 0); this.speed = 0;
    if (name === 'heavyCharge') this.charge = 0;
    if (name === 'roll' || name === 'backstep') this.events.dust?.();
    return true;
  }
  resetFeet() { this.feet = {}; this.mfeet = {}; this.resetSpring(); }
  light() {
    if (this.name === 'jump' && this.time > .16 && this.time < .80) return this.play('flurry');
    if (!this.busy) return this.play('light1');
    if (this.move.buffer && this.time >= this.move.buffer[0] && this.time <= this.move.buffer[1]) { this.buffered = true; this.events.buffer?.(); }
  }
  update(dt, direction, running) {
    this.worldTime += dt; this.dt = dt;
    if (this.busy) this.updateMove(dt); else this.locomotion(dt, direction, running);
    this.blend = Math.min(1, this.blend + dt / Math.max(.12, this.move?.blend ?? .22));
    if (dt > 0) this.dynamics(dt);
    this.apply();
    if (this.busy) this.moveLegs(dt);
    else if ((this.runW ?? 0) > .5) { this.runLegs(); this.wasRunning = true; }
    else {
      if (this.wasRunning) { this.wasRunning = false; this.feet = Object.fromEntries(['L', 'R'].map(s => { const p = this.j[s + 'foot'].getWorldPosition(new V3()); p.y = Math.max(p.y, ANKLE); return [s, { target: p.clone(), start: p.clone(), wasStance: true, moving: true }]; })); }
      this.plantFeet(direction, dt);
    }
    this.character.root.updateMatrixWorld(true); this.bodyColliders.update();
    this.weapon(dt);
    this.character.root.updateMatrixWorld(true); this.bodyColliders.update();
    this.audit();
  }
  updateMove(dt) {
    const move = this.move, previous = this.time; this.time = Math.min(this.time + dt, move.duration);
    if (this.name === 'heavyCharge') this.charge = Math.min(1, this.time / move.duration);
    const distance = rootAt(move, this.time) - rootAt(move, previous);
    this.character.root.position.add(new V3(0, 0, distance).applyAxisAngle(up, this.startYaw));
    if (move.yaw) this.character.root.rotation.y = this.startYaw + curveAt(move.yaw, this.time) * rad + this.yawOffset * (1 - smooth(Math.min(1, this.time / .14)));
    else { const r = this.character.root, dy = THREE.MathUtils.euclideanModulo(this.startYaw - r.rotation.y + Math.PI, Math.PI * 2) - Math.PI; r.rotation.y += dy * damp(26, dt); }
    const keys = this.compiled[this.name]; let i = 0; while (i < keys.length - 2 && this.time > keys[i + 1].t) i++;
    const k0 = keys[Math.max(0, i - 1)], k1 = keys[i], k2 = keys[i + 1], k3 = keys[Math.min(keys.length - 1, i + 2)];
    const t = ease(THREE.MathUtils.clamp((this.time - k1.t) / (k2.t - k1.t), 0, 1), k2.ease);
    for (const name of Object.keys(neutral)) {
      this.pose[name].set(...['x', 'y', 'z', 'w'].map(axis => hermite(k0.quaternions[name][axis], k1.quaternions[name][axis], k2.quaternions[name][axis], k3.quaternions[name][axis], t))).normalize();
    }
    this.body.set(...[0, 1, 2].map(axis => hermite(k0.body[axis], k1.body[axis], k2.body[axis], k3.body[axis], t)));
    // ballistic flight: parabola between take-off and landing (keys only shape the body)
    if (move.arc) { const [t0, t1, h] = move.arc; if (this.time > t0 && this.time < t1) { const s = (this.time - t0) / (t1 - t0); this.body.y += h * 4 * s * (1 - s); } }
    if (!this.impacted && move.impact !== undefined && this.time >= move.impact) { this.impacted = true; this.events.impact?.(this.name, move, this.charge); }
    if (move.land !== undefined && previous < move.land && this.time >= move.land) this.events.land?.();
    for (const [at, name] of move.fx ?? []) if (previous < at && this.time >= at) this.events.fx?.(name, this.name);
    if (this.buffered && move.chain && this.time >= (move.chainAt ?? move.duration * .79)) { this.play(move.chain); return; }
    if (this.time >= move.duration && !move.loopHold) this.play('idle');
  }
  // Breathing: ~3.2 s cycle, the inhale (55%) is slower than the exhale.
  breath() {
    const ph = (this.worldTime / 3.2) % 1;
    return ph < .55 ? Math.sin(ph / .55 * Math.PI / 2) * 2 - 1 : Math.cos((ph - .55) / .45 * Math.PI / 2) * 2 - 1;
  }
  locomotion(dt, direction, running) {
    const root = this.character.root, previousSpeed = this.speed, targetSpeed = direction.lengthSq() > .01 ? (running ? 4.2 : 1.65) * Math.min(1, direction.length()) : 0;
    const desired = direction.lengthSq() > 1e-6 ? direction.clone().normalize().multiplyScalar(targetSpeed) : new V3();
    this.accel ??= new V3(); { const w = 2 * Math.PI * 1.7; this.accel.addScaledVector(desired.clone().sub(this.velocity).multiplyScalar(w * w).addScaledVector(this.accel, -2 * w), dt); this.velocity.addScaledVector(this.accel, dt); }
    this.speed = this.velocity.length(); if (this.speed < .015) { this.velocity.set(0, 0, 0); this.speed = 0; }
    root.position.addScaledVector(this.velocity, dt);
    if (direction.lengthSq() > .01) { const targetYaw = Math.atan2(direction.x, direction.z), delta = THREE.MathUtils.euclideanModulo(targetYaw - root.rotation.y + Math.PI, Math.PI * 2) - Math.PI; root.rotation.y += delta * damp(10, dt); }
    this.phase += this.speed * dt * (running ? 4.3 : 5.4);
    const amount = Math.min(1, this.speed / 1.65), still = 1 - amount, wave = Math.sin(this.phase), b = this.breath();
    const accel = dt ? THREE.MathUtils.clamp((this.speed - previousSpeed) / dt, -4, 5) : 0;
    const N = neutral;
    const angles = { ...N,
      pelvis: [N.pelvis[0] + this.speed * 2.2, N.pelvis[1] * still + wave * 5 * amount, Math.cos(this.phase) * 2 * amount],
      spine: [N.spine[0] + accel * 1.3, N.spine[1] * still, 0],
      chest: [N.chest[0] + this.speed * 1.7 - b * 1.6 * still, N.chest[1] * still - wave * 9 * amount, -Math.cos(this.phase) * 2 * amount],
      head: [N.head[0] - this.speed * 2 + b * .8 * still, N.head[1] * still, 0],
      Lclavicle: [0, 0, -b * 2 * still], Rclavicle: [0, 0, b * 2 * still],
    };
    // Running: FK run cycle (skeleton-driven), torso lean, counter-rotation, arm swing, bob.
    const runW = this.runW = THREE.MathUtils.smoothstep(this.speed, .7, 2.2); this.runBob = 0;
    if (runW > 0) {
      this.runPhase = (this.runPhase ?? 0) + this.speed * dt / 2.2;
      const p = this.runPhase, s = Math.sin(2 * Math.PI * (p - .65)), lean = 8 + this.speed * 2.2;
      const legs = { L: runLeg(p), R: runLeg(p + .5) };
      const run = {
        pelvis: [6 + this.speed * 1.2, 9 * s, 3 * Math.sin(2 * Math.PI * p)], spine: [lean * .5, -5 * s, 0], chest: [lean * .5, -12 * s, 0],
        neck: [-lean * .4, 6 * s, 0], head: [-lean * .4, 6 * s, 0],
        Lthigh: [legs.L[0], 0, 4], Lshin: [legs.L[1], 0, 0], Lfoot: [legs.L[2], 0, 0],
        Rthigh: [legs.R[0], 0, -4], Rshin: [legs.R[1], 0, 0], Rfoot: [legs.R[2], 0, 0],
        LupperArm: [38 * s, 0, -14], Lforearm: [-88 + 12 * s, 0, 0],
      };
      for (const [n, r] of Object.entries(run)) { const base = angles[n] ?? N[n]; angles[n] = base.map((v, i) => v + (r[i] - v) * runW); }
      this.runBob = runW * (.035 * Math.cos(4 * Math.PI * (p - .42)) - .015);
    }
    for (const [n, a] of Object.entries(angles)) this.pose[n].copy(quat(a));
    this.body.set(Math.sin(this.worldTime * .85) * .010 * still, BASE_BODY[1] * (1 - amount * .6) + b * .006 * still + Math.abs(wave) * .027 * amount, 0);
    this.body.y += this.runBob - Math.abs(wave) * .027 * amount * runW;
    this.idleBob.set(0, b * .009 * still, 0);
    this.time = 0; this.name = this.speed > .15 ? (running ? 'run' : 'walk') : 'idle';
  }
  apply() {
    const b = this.blend, w = b * b * b * (b * (b * 6 - 15) + 10);          // smootherstep: eased in and out
    for (const [name, q] of Object.entries(this.pose)) this.j[name].quaternion.copy(this.from?.[name] ?? q).slerp(q, w);
    for (const n of FK_CLAMP) this.stat('limit ' + n, clampJoint(n, this.j[n].quaternion));
    const body = this.fromBody ? this.fromBody.clone().lerp(this.body, w) : this.body;
    this.j.pelvis.position.set(body.x, REST_HEIGHT + body.y, body.z); this.character.root.updateMatrixWorld(true);
  }
  // Pole from the FK knee/elbow: the IK then reproduces the authored bend plane.
  fkPole(upper, lower, end) {
    const a = upper.getWorldPosition(new V3()), k = lower.getWorldPosition(new V3()), e = end.getWorldPosition(new V3());
    const off = k.clone().sub(a.clone().add(e).multiplyScalar(.5));
    if (off.lengthSq() < 1e-6) off.set(0, 0, 1).applyQuaternion(this.character.root.quaternion);
    return k.add(off.setLength(.6));
  }
  setFootWorld(side, yaw) {
    const foot = this.j[side + 'foot'], parentQ = foot.parent.getWorldQuaternion(new Q4());
    foot.quaternion.copy(parentQ.invert().multiply(new Q4().setFromAxisAngle(up, yaw)));
  }
  // Feet during moves: planted feet are locked in world space, authored step windows move
  // them to where the FK leg wants to land, airborne windows release them, and nothing
  // is ever allowed below the ground.
  moveLegs(dt) {
    const move = this.move, t = this.time, root = this.character.root;
    const air = !!(move.roll || (move.air && t >= move.air[0] && t <= move.air[1]));
    for (const side of ['L', 'R']) {
      const thigh = this.j[side + 'thigh'], shin = this.j[side + 'shin'], foot = this.j[side + 'foot'];
      const fk = foot.getWorldPosition(new V3()), ground = fk.clone(); ground.y = Math.max(ground.y, ANKLE);
      const fkYaw = yawOf(foot.getWorldQuaternion(new Q4()));
      const pole = this.fkPole(thigh, shin, foot);
      const f = this.mfeet[side] ??= { pos: ground.clone(), yaw: fkYaw };
      let target, flat = true;
      if (air) {
        f.pos.copy(ground); f.yaw = fkYaw; f.step = null; flat = false;
        if (fk.y >= ANKLE) continue;            // pure FK while airborne / rolling
        target = ground;
      } else {
        const step = move.steps?.find(s => s[0] === side && t >= s[1] && t <= s[2]);
        if (step) {
          if (f.step !== step) { f.step = step; f.from = f.pos.clone(); f.fromYaw = f.yaw; }
          const p = (t - step[1]) / (step[2] - step[1]), e = smooth(p);
          target = f.from.clone().lerp(ground, e); target.y = Math.max(target.y, ANKLE) + Math.sin(Math.PI * p) * .11;
          f.yaw = lerpAngle(f.fromYaw, fkYaw, e); f.pos.copy(target);
        } else {
          f.step = null; f.pos.y = ANKLE;
          // Out of reach (e.g. long root motion with no authored step): slide toward the FK foot.
          const hip = thigh.getWorldPosition(new V3()), over = hip.distanceTo(f.pos) - (THIGH + SHIN + .03);
          if (over > 0) { const before = f.pos.clone(); f.pos.lerp(ground, Math.min(1, damp(10, dt) + over)); f.pos.y = ANKLE; this.stat('foot slide (unplanned)', before.distanceTo(f.pos)); }
          target = f.pos.clone();
        }
      }
      solveTwoBone(thigh, shin, foot, target, pole, THIGH, SHIN);
      if (flat) this.setFootWorld(side, f.yaw);
      foot.updateWorldMatrix(true, false);
      const ankle = foot.getWorldPosition(new V3());
      if (!air) this.stat('planted foot error', Math.hypot(ankle.x - target.x, ankle.z - target.z));
    }
  }
  runLegs() {
    for (const side of ['L', 'R']) {
      const thigh = this.j[side + 'thigh'], shin = this.j[side + 'shin'], foot = this.j[side + 'foot'];
      const fk = foot.getWorldPosition(new V3());
      if (fk.y < ANKLE) { const q = foot.getWorldQuaternion(new Q4()); solveTwoBone(thigh, shin, foot, new V3(fk.x, ANKLE, fk.z), this.fkPole(thigh, shin, foot), THIGH, SHIN); const pq = foot.parent.getWorldQuaternion(new Q4()); foot.quaternion.copy(pq.invert().multiply(q)); }
    }
  }
  plantFeet(direction, dt) {
    const root = this.character.root, moving = this.speed > .12, amount = Math.min(1, this.speed / 1.65);
    for (const [side, offset] of [['L', 0], ['R', Math.PI]]) {
      const phase = THREE.MathUtils.euclideanModulo(this.phase + offset, Math.PI * 2) / (Math.PI * 2), stance = phase < .60, sign = side === 'L' ? -1 : 1;
      const walk = [sign * .19, side === 'L' ? .12 : -.10], st = STANCE_FEET[side];
      const rest = new V3(THREE.MathUtils.lerp(st[0], walk[0], amount), ANKLE, THREE.MathUtils.lerp(st[1], walk[1], amount)); root.localToWorld(rest); rest.y = ANKLE;
      let foot = this.feet[side];
      if (!foot) foot = this.feet[side] = { target: rest.clone(), start: rest.clone(), wasStance: stance, moving };
      if (moving) {
        if (foot.wasStance && !stance) foot.start.copy(foot.target);
        if (!stance) {
          const t = (phase - .60) / .40, ahead = this.velocity.clone().multiplyScalar(.19);
          foot.target.copy(foot.start).lerp(rest.clone().add(ahead), smooth(t)); foot.target.y = ANKLE + Math.sin(Math.PI * t) * (this.speed > 2 ? .19 : .12);
        } else if (!foot.wasStance) { foot.target.y = ANKLE; if (this.speed > 2) this.events.step?.(foot.target); }
      } else if (foot.moving) {
        foot.target.lerp(rest, damp(10, dt));
        const d = Math.hypot(foot.target.x - rest.x, foot.target.z - rest.z); foot.target.y = ANKLE + Math.min(.07, d * .6);
      }
      if (foot.target.distanceTo(rest) > .70) foot.target.copy(rest);
      foot.wasStance = stance; foot.moving = moving || foot.target.distanceTo(rest) > .01;
      const pole = root.localToWorld(new V3(sign * .20, .48, 1));
      solveTwoBone(this.j[side + 'thigh'], this.j[side + 'shin'], this.j[side + 'foot'], foot.target, pole, THIGH, SHIN);
      this.setFootWorld(side, root.rotation.y + (side === 'L' ? -.45 : .12) * (1 - amount));
    }
  }
  // ---------- Arm-blade with a mechanical wrist ----------
  // Real cuts (analysis): the swing runs on a tilted plane (diagonal down, diagonal up, or
  // waist-high horizontal), driven through the kinetic chain hips -> torso -> shoulder ->
  // elbow -> wrist. The hands lead while the blade lags cocked behind the forearm, then the
  // wrist releases and the tip whips through contact and follows through past the target.
  // Keys therefore give three directions: upper arm u, forearm f, blade d. The forearm and the
  // blade are sampled slightly later than the upper arm (overlapping action), the wrist hinge
  // is limited to 110 degrees, the elbow to 150.
  weapon(dt) {
    const j = this.j, root = this.character.root, move = this.move, busy = this.busy;
    const S = j.RupperArm.getWorldPosition(new V3());
    const fkE = j.Rforearm.getWorldPosition(new V3()), fkW = j.Rhand.getWorldPosition(new V3());
    const fkTip = this.character.bladeTip.getWorldPosition(new V3());
    const u = fkE.clone().sub(S).normalize(), f = fkW.clone().sub(fkE).normalize(), d = fkTip.clone().sub(fkW).normalize(), vel = new V3();
    const gs = busy ? this.guides[this.name] : this.idleGuide;
    if (!busy && (this.runW ?? 0) > 0) this.runBlend = this.runW;
    if (gs) {
      const time = busy ? this.time : 0, frame = move?.weaponSpace === 'chest' ? j.chest.matrixWorld : root.matrixWorld;
      const lagF = busy ? LAG_F : 0, lagD = busy ? LAG_D : 0;
      const gu = new V3(), gf = new V3(), gd = new V3(), t2 = new V3();
      sampleKey(gs, 'u', time, gu); sampleKey(gs, 'd', Math.max(0, time - lagD), gd); gf.copy(gd);
      const tipAt = tt => { const a = new V3(), c = new V3(); sampleKey(gs, 'u', tt, a); sampleKey(gs, 'd', Math.max(0, tt - lagD), c); return a.multiplyScalar(UPPER).addScaledVector(c, FORE + BLADE_END); };
      vel.copy(tipAt(time + 1 / 240)).sub(tipAt(time)).multiplyScalar(240);
      const speed = vel.length(); if (speed > 1e-6) vel.transformDirection(frame).multiplyScalar(speed);
      if (!busy) { gu.y += this.idleBob.y * 2; gu.normalize(); }
      if (!busy && this.runW > 0) { slerpDir(gu, new V3(.42, -.88, .20).normalize(), this.runW); slerpDir(gd, new V3(.76, -.50, .42).normalize(), this.runW); gf.copy(gd); }
      for (const v of [gu, gf, gd]) v.transformDirection(frame);
      const w = busy ? smooth(this.blend) : 1;
      slerpDir(u, gu, w); slerpDir(f, gf, w); slerpDir(d, gd, w);
      this.springDirs(u, d, dt); f.copy(d);
    }
    if (busy && move.yaw) { const om = (curveAt(move.yaw, this.time + 1 / 240) - curveAt(move.yaw, this.time)) * 240 * rad; vel.add(new V3(0, om, 0).cross(S.clone().addScaledVector(u, UPPER).addScaledVector(f, FORE).addScaledVector(d, BLADE_END).sub(root.position))); }
    limitAngle(d, u, 150); f.copy(d);
    // Collisions: small rotations of blade, then forearm, then upper arm around the authored pose.
    const Ut = u.clone(), Ft = f.clone(), Dt = d.clone();
    const score = (cu, cf, cd) => {
      const e = S.clone().addScaledVector(cu, UPPER), w = e.clone().addScaledVector(cf, FORE);
      const pen = this.armBladePen(e, w, cd, move);
      const lim = Math.max(0, cu.angleTo(cf) / rad - 150) + Math.max(0, cf.angleTo(cd) / rad - WRIST_MAX);
      return { pen, cost: pen * 25 + cd.angleTo(Dt) + cf.angleTo(Ft) * 1.2 + cu.angleTo(Ut) * 1.5 + lim * .05 };
    };
    let best = { u: Ut, f: Ft, d: Dt, ...score(Ut, Ft, Dt) };
    this.stat('blade in body (before fix)', best.pen);
    if (best.pen > 0) {
      const around = (v, ring, k) => { const a = Math.abs(v.y) < .9 ? new V3(0, 1, 0) : new V3(1, 0, 0), b1 = v.clone().cross(a).normalize(), b2 = v.clone().cross(b1).normalize(), phi = k / 10 * Math.PI * 2; return v.clone().applyAxisAngle(b1.multiplyScalar(Math.cos(phi)).addScaledVector(b2, Math.sin(phi)), ring * rad); };
      for (const ring of [8, 18, 30, 45, 62]) {
        for (let k = 0; k < 10; k++) { const rd = around(Dt, ring, k), ru = around(Ut, ring, k); for (const [cu, cf, cd] of [[Ut, rd, rd], [ru, Dt, Dt]]) {
          const s = score(cu, cf, cd); if (s.cost < best.cost) best = { u: cu, f: cf, d: cd, ...s };
        } }
        if (best.pen === 0) break;
      }
    }
    u.copy(best.u); f.copy(best.f); d.copy(best.d);
    // Outside the strike itself, low-pass the final arm directions so collision corrections
    // and run-cycle wobble never make the blade shiver; strikes stay frame-exact.
    const striking = busy && move.active && this.time >= move.active[0] - .05 && this.time <= move.active[1] + .05;
    if (this.smoothU && !busy && dt > 0) { const k = damp(16, dt); slerpDir(this.smoothU, u, k); slerpDir(this.smoothD, d, k); u.copy(this.smoothU); d.copy(this.smoothD); f.copy(d); }
    this.smoothU = u.clone(); this.smoothD = d.clone();
    // ---- Hinge-consistent arm ----
    // The elbow is a real hinge: it only bends one way and neither bone twists about its own
    // axis (that read as twisted muscles). The hinge axis X = d x u; if a key would make the
    // elbow bend backwards while passing through straight, the upper arm is moved to the
    // allowed side instead (the blade direction, i.e. the cut, is kept).
    {
      const rq = root.quaternion, rqi = rq.clone().invert();
      let h = d.clone().cross(u); const bend = u.angleTo(d), prev = this.hingeL ? this.hingeL.clone().applyQuaternion(rq) : null;
      if (h.length() < .17) h = prev ? prev.clone() : new V3(1, 0, 0).applyQuaternion(root.quaternion);
      else {
        h.normalize();
        if (prev && h.dot(prev) < 0 && bend < .75) {
          h = prev.clone().addScaledVector(d, -prev.dot(d)).normalize();
          u.copy(d).applyAxisAngle(h, Math.max(bend, .12)).normalize();
        }
      }
      h.addScaledVector(u, -h.dot(u)).normalize();
      // a nearly straight elbow defines its hinge poorly: hold the previous twist there
      const trust = THREE.MathUtils.smoothstep(bend, 12 * rad, 40 * rad);
      this.hinge = prev ? prev.clone().lerp(h, damp(26 * trust, dt || .016)).normalize() : h.clone();
      this.hingeL = this.hinge.clone().applyQuaternion(rqi);
    }
    const H = this.hinge.clone().addScaledVector(u, -this.hinge.dot(u)).normalize();
    const Hd = H.clone().addScaledVector(d, -H.dot(d)).normalize();
    this.setBasis(j.RupperArm, u, H);
    this.setBasis(j.Rforearm, d, Hd);
    // Edge roll: the hand housing rolls about the blade so the edge leads the cut, but only
    // within +-100 degrees of the forearm and at a limited rate (no sudden 180-degree wrench).
    const perp = v => v.clone().addScaledVector(d, -v.dot(d));
    const restEdge = perp(new V3(0, -1, 0)); if (restEdge.lengthSq() < 1e-6) restEdge.copy(perp(new V3(0, 0, 1)));
    restEdge.normalize();
    let target = perp(vel); const speed = target.length();
    target = speed > .05 ? target.normalize().lerp(restEdge, THREE.MathUtils.clamp(1 - speed / 3, 0, 1)) : restEdge.clone();
    if (target.lengthSq() < 1e-6) target.copy(restEdge); target.normalize();
    let phi = Math.atan2(Hd.clone().cross(target).dot(d), Hd.dot(target));
    phi = THREE.MathUtils.clamp(phi, -100 * rad, 100 * rad);
    const maxStep = 900 * rad * (dt || .016);
    this.roll = (this.roll ?? 0) + THREE.MathUtils.clamp((phi - (this.roll ?? 0)) * damp(18, dt || .016), -maxStep, maxStep);
    this.setBasis(j.Rhand, d, Hd.clone().applyAxisAngle(d, this.roll));
    this.character.root.updateMatrixWorld(true);
    this.dbg = { Ut, Ft, Dt, u: u.clone(), f: f.clone(), d: d.clone() };
    const inv = root.matrixWorld.clone().invert(), tipL = this.character.bladeTip.getWorldPosition(new V3()).applyMatrix4(inv);
    if (this.prevTipL && dt > 0) this.tipVel = tipL.clone().sub(this.prevTipL).divideScalar(dt);
    this.prevTipL = tipL; this.tipRel = tipL.clone().sub(j.pelvis.position);
    const fh = this.bladeHit(); this.stat('blade in body (final)', fh?.depth ?? 0); if (fh) this.stat('final hit: ' + fh.name, fh.depth);
    this.stat('elbow in torso (after fix)', this.pointPen(S.clone().addScaledVector(u, UPPER), .06, TORSO_ELBOW));
    this.stat('wrist hinge over 110 (deg)', f.angleTo(d) / rad - WRIST_MAX);
    this.stat('elbow R flex over 150 (deg)', u.angleTo(f) / rad - 150);
  }
  pointPen(p, r, names) {
    let worst = 0;
    for (const n of names) { const c = this.bodyColliders.byName[n], x = new V3(), y = new V3(); worst = Math.max(worst, c.r + r - closestSegments(p, p, c.a, c.b, x, y)); }
    return worst;
  }
  // Penetration of the arm-blade chain: blade (wrist->tip), forearm (elbow->wrist), elbow.
  armBladePen(e, w, d, move) {
    const p = w.clone().addScaledVector(d, BLADE_FROM), q = w.clone().addScaledVector(d, BLADE_LEN);
    let pen = deepest(this.bodyColliders, ARM_BLADE_BLOCKERS, p, q, BLADE_R)?.depth ?? 0;
    pen = Math.max(pen, deepest(this.bodyColliders, FOREARM_BLOCKERS, e, w, .05)?.depth ?? 0, this.pointPen(e, .06, TORSO_ELBOW));
    if (!move?.stab) pen = Math.max(pen, .02 - q.y, .02 - p.y);
    return pen;
  }
  // Shoulder, blade direction and blade-tip velocity in world space (for slash crescents).
  slashInfo() {
    const root = this.character.root, v = this.tipVel ?? new V3(), speed = v.length();
    return { center: this.j.RupperArm.getWorldPosition(new V3()), dir: this.dbg?.d?.clone() ?? new V3(0, 0, 1), vel: speed > 1e-6 ? v.clone().transformDirection(root.matrixWorld).multiplyScalar(speed) : new V3() };
  }
  // Critically damped 2nd-order follow (≈11 Hz): the arm accelerates out of every key and
  // decelerates into it instead of changing speed instantly.
  springDirs(u, d, dt) {
    if (!(dt > 0)) return;
    const S = this.armSpring ??= { u: u.clone(), d: d.clone(), vu: new V3(), vd: new V3() }, w = 2 * Math.PI * 11;
    for (const [x, v, target] of [[S.u, S.vu, u], [S.d, S.vd, d]]) {
      v.addScaledVector(target.clone().sub(x).multiplyScalar(w * w).addScaledVector(v, -2 * w), dt); x.addScaledVector(v, dt).normalize(); target.copy(x);
    }
  }
  resetSpring() { this.armSpring = null; this.accel = null; this.hinge = null; this.hingeL = null; this.roll = 0; }
  // Bone frame from its direction (bone runs along local -Y) and the hinge axis (local X).
  setBasis(joint, dir, xAxis) {
    const Y = dir.clone().negate().normalize(), X = xAxis.clone().addScaledVector(Y, -xAxis.dot(Y)).normalize(), Z = X.clone().cross(Y).normalize();
    const world = new Q4().setFromRotationMatrix(new THREE.Matrix4().makeBasis(X, Y, Z));
    joint.parent.updateWorldMatrix(true, false);
    joint.quaternion.copy(joint.parent.getWorldQuaternion(new Q4()).invert().multiply(world)); joint.updateWorldMatrix(false, true);
  }
  aimBone(joint, dir) {
    joint.updateWorldMatrix(true, false);
    const wq = joint.getWorldQuaternion(new Q4()), cur = new V3(0, -1, 0).applyQuaternion(wq);
    const next = new Q4().setFromUnitVectors(cur, dir).multiply(wq), pq = joint.parent.getWorldQuaternion(new Q4());
    joint.quaternion.copy(pq.invert().multiply(next)); joint.updateWorldMatrix(false, true);
  }
  bladeHit() {
    const s = this.character.sword; s.updateWorldMatrix(true, false);
    const p = s.localToWorld(new V3(0, BLADE_FROM, 0)), q = s.localToWorld(new V3(0, BLADE_END, 0));
    return deepest(this.bodyColliders, ARM_BLADE_BLOCKERS, p, q, BLADE_R - .01);
  }
  audit() {
    // IK limbs: elbow/knee flexion must stay within 0..150 degrees.
    for (const [a, b, c, label] of [['Rthigh', 'Rshin', 'Rfoot', 'knee R'], ['Lthigh', 'Lshin', 'Lfoot', 'knee L']]) {
      const p = [a, b, c].map(n => this.j[n].getWorldPosition(new V3())), u = p[1].clone().sub(p[0]), v = p[2].clone().sub(p[1]);
      this.stat(label + ' flex over 150 (deg)', u.angleTo(v) / rad - 150);
    }
    for (const s of ['L', 'R']) {
      const y = this.j[s + 'foot'].getWorldPosition(new V3()).y; this.stat('ankle below ground', ANKLE - .005 - y);
    }
    let low = Infinity; for (const c of this.bodyColliders.caps) low = Math.min(low, c.a.y - c.r, c.b.y - c.r);
    this.stat('body below ground', -low - .01);
  }
}
