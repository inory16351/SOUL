import * as THREE from 'three';

// Neon-blue effect kit. Colours are HDR (>1) with toneMapped:false so the bloom pass makes
// them glow: blade trail, slash crescents, laser waves, dash afterimages, sparks, flashes.
function radialTexture(stops) {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'), grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  for (const [o, col] of stops) grad.addColorStop(o, col);
  g.fillStyle = grad; g.fillRect(0, 0, 64, 64);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
const catmull = (p0, p1, p2, p3, t, out) => {
  const t2 = t * t, t3 = t2 * t;
  return out.set(0, 0, 0)
    .addScaledVector(p0, -.5 * t3 + t2 - .5 * t).addScaledVector(p1, 1.5 * t3 - 2.5 * t2 + 1)
    .addScaledVector(p2, -1.5 * t3 + 2 * t2 + .5 * t).addScaledVector(p3, .5 * t3 - .5 * t2);
};
const TRAIL_LIFE = .16, SUB = 4, TRAIL_GAIN = 1.25;
const TRAIL_NEW = new THREE.Color(.75, .95, 1.0), TRAIL_OLD = new THREE.Color(.10, .38, 1.0);
const NEON = new THREE.Color(.25, .65, 1.0);
const add = (opts = {}) => new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false, side: THREE.DoubleSide, ...opts });

export class VFX {
  constructor(scene, character) {
    this.scene = scene; this.character = character; this.particles = []; this.rings = []; this.flashes = []; this.samples = [];
    this.arcs = []; this.waves = []; this.ghosts = [];
    this.trailClock = 0; this.lastBase = null; this.lastTip = null; this.time = 0;
    this.maxSamples = 44;
    const maxVerts = this.maxSamples * SUB * 3;
    this.positions = new Float32Array(maxVerts * 3); this.colors = new Float32Array(maxVerts * 3);
    const index = []; for (let i = 0; i < this.maxSamples * SUB - 1; i++) { const a = i * 3, b = a + 3; index.push(a, a + 1, b, a + 1, b + 1, b, a + 1, a + 2, b + 1, a + 2, b + 2, b + 1); }
    this.geometry = new THREE.BufferGeometry();
    this.geometry.setAttribute('position', new THREE.BufferAttribute(this.positions, 3)); this.geometry.setAttribute('color', new THREE.BufferAttribute(this.colors, 3));
    this.geometry.setIndex(index); this.geometry.setDrawRange(0, 0);
    this.trail = new THREE.Mesh(this.geometry, add({ vertexColors: true })); this.trail.frustumCulled = false; scene.add(this.trail);
    this.sparkGeometry = new THREE.BoxGeometry(1, 1, 1);
    this.glow = radialTexture([[0, 'rgba(235,248,255,1)'], [.25, 'rgba(90,180,255,.8)'], [1, 'rgba(30,110,255,0)']]);
    this.puff = radialTexture([[0, 'rgba(150,135,110,.5)'], [.6, 'rgba(130,118,96,.2)'], [1, 'rgba(120,110,90,0)']]);
    this.ringTex = (() => { const c = document.createElement('canvas'); c.width = c.height = 128; const g = c.getContext('2d'), gr = g.createRadialGradient(64, 64, 40, 64, 64, 64); gr.addColorStop(0, 'rgba(80,170,255,0)'); gr.addColorStop(.75, 'rgba(170,225,255,.95)'); gr.addColorStop(1, 'rgba(80,170,255,0)'); g.fillStyle = gr; g.fillRect(0, 0, 128, 128); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t; })();
  }
  // ---------- one-shot effects ----------
  burst(position, count = 22, power = 1, dust = false) {
    if (dust) return this.dust(position, power);
    for (let i = 0; i < count; i++) {
      const c = i % 3 ? NEON.clone().multiplyScalar(3) : new THREE.Color(3, 3.2, 3.4);
      const mesh = new THREE.Mesh(this.sparkGeometry, add({ color: c, side: THREE.FrontSide })); mesh.position.copy(position); this.scene.add(mesh);
      const dir = new THREE.Vector3(Math.random() * 2 - 1, Math.random() * .9 + .2, Math.random() * 2 - 1).normalize();
      this.particles.push({ mesh, velocity: dir.multiplyScalar((2.5 + Math.random() * 5.5) * power), life: 0, max: .16 + Math.random() * .24, width: .008 + Math.random() * .01, spark: true });
    }
    if (count > 3) { this.flash(position, .5 * power); this.ring(position, .35 * power, true); }
  }
  edgeSpark(position, bladeVel) {
    const hot = Math.random() < .3, c = hot ? new THREE.Color(3.2, 2.2, 1.1) : Math.random() < .5 ? new THREE.Color(2.6, 3.0, 3.4) : NEON.clone().multiplyScalar(3.2);
    const mesh = new THREE.Mesh(this.sparkGeometry, add({ color: c, side: THREE.FrontSide })); mesh.position.copy(position); this.scene.add(mesh);
    const v = bladeVel.clone().multiplyScalar(.25 + Math.random() * .25).add(new THREE.Vector3(Math.random() - .5, Math.random() * .8, Math.random() - .5).multiplyScalar(3));
    this.particles.push({ mesh, velocity: v, life: 0, max: .12 + Math.random() * .2, width: .006 + Math.random() * .006, spark: true });
  }
  flash(position, size = .5) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.glow, color: new THREE.Color(2, 2, 2), transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false }));
    s.position.copy(position); s.scale.setScalar(size * .4); this.scene.add(s); this.flashes.push({ s, life: 0, max: .14, size });
  }
  dust(position, power = .65) {
    const n = Math.round(5 + power * 5);
    for (let i = 0; i < n; i++) {
      const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.puff, transparent: true, depthWrite: false }));
      const a = Math.random() * Math.PI * 2, r = Math.random() * .12;
      s.position.copy(position).add(new THREE.Vector3(Math.cos(a) * r, .06 + Math.random() * .05, Math.sin(a) * r)); this.scene.add(s);
      const sp = (.35 + Math.random() * .8) * power;
      this.particles.push({ mesh: s, velocity: new THREE.Vector3(Math.cos(a) * sp, .15 + Math.random() * .35 * power, Math.sin(a) * sp), life: 0, max: .55 + Math.random() * .45, size: .16 + Math.random() * .14 * power, dust: true });
    }
  }
  ring(position, power = 1, upright = false) {
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), add({ map: this.ringTex, color: new THREE.Color(1.8, 2.2, 2.6) }));
    mesh.position.copy(position);
    if (!upright) { mesh.position.y = .03; mesh.rotation.x = -Math.PI / 2; } else mesh.rotation.y = this.character.root.rotation.y;
    mesh.scale.setScalar(.3); this.scene.add(mesh); this.rings.push({ mesh, life: 0, power, upright });
  }
  // Glowing crescent left in the swing plane: centred on the shoulder, radius = arm + blade,
  // spanning the last part of the swing up to the current blade direction.
  slashArc(center, bladeDir, velocity, radius = 1.95, span = 1.9) {
    const n = bladeDir.clone().cross(velocity); if (n.lengthSq() < 1e-6) return; n.normalize();
    const x = bladeDir.clone().applyAxisAngle(n, -span), y = n.clone().cross(x).normalize();
    const geo = new THREE.RingGeometry(radius * .52, radius, 40, 1, 0, span);
    const col = [], p = geo.attributes.position;
    for (let i = 0; i < p.count; i++) { const ang = Math.atan2(p.getY(i), p.getX(i)), k = THREE.MathUtils.clamp(ang / span, 0, 1), r = Math.hypot(p.getX(i), p.getY(i)) / radius; const f = k ** 1.6 * THREE.MathUtils.smoothstep(r, .55, .97); col.push(f * .7, f * 1.6, f * 2.6); }
    geo.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    const mesh = new THREE.Mesh(geo, add({ vertexColors: true }));
    mesh.matrixAutoUpdate = false; mesh.matrix.makeBasis(x, y, n).setPosition(center); this.scene.add(mesh);
    this.arcs.push({ mesh, life: 0, max: .24 });
  }
  // Travelling laser crescent (W). The returned object is used by the game loop for hit tests.
  spawnWave(origin, dir, roll = -.35) {
    const group = new THREE.Group(); group.position.copy(origin);
    const make = (inner, outer, color) => new THREE.Mesh(new THREE.RingGeometry(inner, outer, 40, 1, Math.PI * .12, Math.PI * .76), add({ color }));
    group.add(make(.95, 1.25, NEON.clone().multiplyScalar(2.2)), make(1.08, 1.16, new THREE.Color(3, 3.4, 3.8)));
    group.lookAt(origin.clone().add(dir)); group.rotateZ(roll); this.scene.add(group);
    const light = new THREE.PointLight(0x4aa8ff, 3, 4, 2); group.add(light);
    const wave = { group, dir: dir.clone().setY(0).normalize(), life: 0, max: .62, speed: 15, hit: false };
    this.waves.push(wave); return wave;
  }
  // Afterimage: a translucent neon snapshot of the character's current pose.
  afterimage(root, strength = 1) {
    root.updateMatrixWorld(true);
    const mat = add({ color: new THREE.Color(.12, .42, 1.1), opacity: .2 * strength, side: THREE.FrontSide });
    const group = new THREE.Group();
    root.traverseVisible(o => { if (o.isMesh && o.geometry && !o.material.transparent) { const m = new THREE.Mesh(o.geometry, mat); m.matrixAutoUpdate = false; m.matrix.copy(o.matrixWorld); group.add(m); } });
    this.scene.add(group); this.ghosts.push({ group, mat, life: 0, max: .28, start: .2 * strength });
  }
  // ---------- per-frame ----------
  update(dt, active, charge) {
    this.time += dt;
    const base = this.character.bladeBase.getWorldPosition(new THREE.Vector3()), tip = this.character.bladeTip.getWorldPosition(new THREE.Vector3());
    const inner = base.clone().lerp(tip, .25);
    const interval = 1 / 120;
    // Only the fast part of the arc leaves a trail.
    const fast = this.lastTip && dt > 0 && tip.distanceTo(this.lastTip) / dt > 4.5;
    if (active && fast && dt > 0) {
      // sparks thrown off the edge near the tip, more the faster the blade travels
      const vel = tip.clone().sub(this.lastTip).divideScalar(dt), speed = vel.length();
      this.sparkDebt = (this.sparkDebt ?? 0) + speed * dt * 2.2;
      while (this.sparkDebt >= 1) { this.sparkDebt -= 1; this.edgeSpark(this.lastTip.clone().lerp(tip, Math.random()).lerp(base, Math.random() * .35), vel); }
    }
    if (active && fast && this.lastBase) {
      this.trailClock += dt;
      while (this.trailClock >= interval) { this.trailClock -= interval; const a = 1 - this.trailClock / dt; this.samples.push({ base: this.lastBase.clone().lerp(inner, a), tip: this.lastTip.clone().lerp(tip, a), time: this.time - this.trailClock }); }
    } else this.trailClock = 0;
    this.lastBase = inner; this.lastTip = tip;
    this.samples = this.samples.filter(s => this.time - s.time < TRAIL_LIFE).slice(-this.maxSamples);
    this.buildTrail();
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i]; p.life += dt;
      if (p.life >= p.max) { this.scene.remove(p.mesh); p.mesh.material.dispose(); this.particles.splice(i, 1); continue; }
      const t = p.life / p.max;
      if (p.spark) {
        p.velocity.y -= dt * 9; p.velocity.multiplyScalar(1 - dt * 2.5); p.mesh.position.addScaledVector(p.velocity, dt);
        if (p.mesh.position.y < .02) { p.mesh.position.y = .02; p.velocity.y *= -.35; }
        const speed = p.velocity.length(); p.mesh.scale.set(p.width, p.width, Math.max(.03, speed * .045));
        p.mesh.lookAt(p.mesh.position.clone().add(p.velocity)); p.mesh.material.opacity = 1 - t * t;
      } else {
        p.velocity.multiplyScalar(1 - dt * 3); p.mesh.position.addScaledVector(p.velocity, dt);
        p.mesh.scale.setScalar(p.size * (1 + t * 2.2)); p.mesh.material.opacity = (1 - t) ** 1.5;
      }
    }
    for (let i = this.flashes.length - 1; i >= 0; i--) {
      const f = this.flashes[i]; f.life += dt; const t = f.life / f.max;
      if (t >= 1) { this.scene.remove(f.s); f.s.material.dispose(); this.flashes.splice(i, 1); continue; }
      f.s.scale.setScalar(f.size * (.4 + t * 1.1)); f.s.material.opacity = (1 - t) ** 2;
    }
    for (let i = this.rings.length - 1; i >= 0; i--) {
      const r = this.rings[i]; r.life += dt; const t = r.life / (r.upright ? .26 : .45);
      if (t >= 1) { this.scene.remove(r.mesh); r.mesh.geometry.dispose(); r.mesh.material.dispose(); this.rings.splice(i, 1); continue; }
      r.mesh.scale.setScalar(.3 + (r.upright ? 1.6 : 4.2) * Math.sqrt(t) * r.power); r.mesh.material.opacity = (1 - t) ** 2;
    }
    for (let i = this.arcs.length - 1; i >= 0; i--) {
      const a = this.arcs[i]; a.life += dt; const t = a.life / a.max;
      if (t >= 1) { this.scene.remove(a.mesh); a.mesh.geometry.dispose(); a.mesh.material.dispose(); this.arcs.splice(i, 1); continue; }
      a.mesh.material.opacity = (1 - t) ** 1.5;
    }
    for (let i = this.waves.length - 1; i >= 0; i--) {
      const w = this.waves[i]; w.life += dt; const t = w.life / w.max;
      if (t >= 1) { this.scene.remove(w.group); w.group.traverse(o => { if (o.isMesh) { o.geometry.dispose(); o.material.dispose(); } }); this.waves.splice(i, 1); continue; }
      w.group.position.addScaledVector(w.dir, w.speed * dt); w.group.scale.setScalar(1 + t * .5);
      w.group.children.forEach(o => { if (o.material) o.material.opacity = (1 - t) ** 1.2; });
      if (Math.random() < .5) this.burst(w.group.position.clone().add(new THREE.Vector3(0, .1, 0)), 1, .4);
    }
    for (let i = this.ghosts.length - 1; i >= 0; i--) {
      const g = this.ghosts[i]; g.life += dt; const t = g.life / g.max;
      if (t >= 1) { this.scene.remove(g.group); g.mat.dispose(); this.ghosts.splice(i, 1); continue; }
      g.mat.opacity = g.start * (1 - t) ** 1.4;
    }
    const rune = this.character.rune; if (rune?.material?.emissiveIntensity !== undefined) rune.material.emissiveIntensity = .12 + charge * 2;
  }
  buildTrail() {
    const s = this.samples, n = s.length; let v = 0;
    if (n >= 2) {
      const pB = new THREE.Vector3(), pT = new THREE.Vector3(), pM = new THREE.Vector3();
      for (let i = 0; i < n - 1; i++) {
        const a = s[Math.max(0, i - 1)], b = s[i], c = s[i + 1], d = s[Math.min(n - 1, i + 2)];
        for (let k = 0; k < SUB; k++) {
          if (v / 3 >= this.maxSamples * SUB) break;
          const u = k / SUB, time = b.time + (c.time - b.time) * u, age = THREE.MathUtils.clamp((this.time - time) / TRAIL_LIFE, 0, 1);
          catmull(a.base, b.base, c.base, d.base, u, pB); catmull(a.tip, b.tip, c.tip, d.tip, u, pT);
          const fade = (1 - age) ** 1.5, col = TRAIL_NEW.clone().lerp(TRAIL_OLD, age);
          pM.copy(pB).lerp(pT, .78);
          const peak = fade * TRAIL_GAIN * 1.6, edge = peak * .45;
          this.positions.set([pB.x, pB.y, pB.z, pM.x, pM.y, pM.z, pT.x, pT.y, pT.z], v * 3);
          this.colors.set([0, 0, 0, col.r * peak, col.g * peak, col.b * peak, col.r * edge, col.g * edge, col.b * edge], v * 3);
          v += 3;
        }
      }
    }
    this.geometry.setDrawRange(0, Math.max(0, (v / 3 - 1)) * 12);
    this.geometry.attributes.position.needsUpdate = true; this.geometry.attributes.color.needsUpdate = true;
  }
  resetTrail() { this.samples.length = 0; this.lastBase = null; this.lastTip = null; }
}
