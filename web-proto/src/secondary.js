import * as THREE from 'three';

const CLOTH_BLOCKERS = new Set(['pelvis', 'belly', 'chest', 'Lthigh', 'Rthigh', 'Lshin', 'Rshin', 'LupperArm', 'RupperArm']);
const ab = new THREE.Vector3(), ap = new THREE.Vector3(), cp = new THREE.Vector3();
// World-space Verlet cloth: fixed 120 Hz substeps, constrained stretch and body collisions.
class Cloth {
  constructor(scene, anchor, { width, length, rows, columns = 4, offset, color, front = false, name, arc = null, flare = .13, damping = .952, wind = .18, gravity = 7.5, bend = .32 }) {
    Object.assign(this, { anchor, width, length, rows, columns, offset: new THREE.Vector3(...offset), front, arc, flare, damping, wind, gravity });
    this.points = []; this.previous = []; this.accumulator = 0;
    const vertices = [], indices = [], colors = [], base = new THREE.Color(color);
    for (let y = 0; y <= rows; y++) for (let x = 0; x <= columns; x++) {
      this.points.push(new THREE.Vector3()); this.previous.push(new THREE.Vector3()); vertices.push(0, 0, 0);
      const edge = x === 0 || x === columns;
      const t = y / rows;
      const stripe = .96 - .29 * t ** 1.7 - (edge ? .045 : 0) + .035 * Math.sin(x * 7 + y * 11);
      const repair = x >= 2 && x <= 3 && y >= Math.floor(rows * .55) && y <= Math.floor(rows * .55) + 1;
      const dye = repair ? new THREE.Color(0x9a8e7c) : base.clone().lerp(new THREE.Color(0x756651), .24 * t ** 3);
      colors.push(dye.r * stripe, dye.g * stripe, dye.b * stripe);
      if (y < rows && x < columns) { const a = y * (columns + 1) + x, b = a + columns + 1; indices.push(a, b, a + 1, a + 1, b, b + 1); }
    }
    const geometry = new THREE.BufferGeometry(); geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3)); geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3)); geometry.setIndex(indices);
    this.mesh = new THREE.Mesh(geometry, new THREE.MeshStandardMaterial({ vertexColors: true, side: THREE.DoubleSide, roughness: .95, flatShading: true }));
    this.mesh.name = name ?? 'patched-desert-cloth';
    // Rest lengths come from the cut pattern, including curved belt attachment
    // and flared hem. Both shear directions keep the long tails from twisting.
    this.links = [];
    const link = (x, y, xx, yy, stiffness = 1) => this.links.push([y * (columns + 1) + x, yy * (columns + 1) + xx, this.localPoint(x, y).distanceTo(this.localPoint(xx, yy)), stiffness]);
    for (let y = 0; y <= rows; y++) for (let x = 0; x <= columns; x++) {
      if (x < columns && y > 0) link(x, y, x + 1, y);
      if (y < rows) link(x, y, x, y + 1);
      if (x < columns && y < rows) { link(x, y, x + 1, y + 1); link(x + 1, y, x, y + 1); }
      if (y < rows - 1) link(x, y, x, y + 2, bend);
    }
    // Stitches share the simulated vertices, so repairs and edge seams cannot
    // float away from their cloth during a turn.
    this.stitches = [];
    for (let y = 1; y < rows; y++) for (const x of [0, columns]) this.stitches.push([y * (columns + 1) + x, (y + 1) * (columns + 1) + x]);
    const repairY = Math.floor(rows * .55);
    for (const y of [repairY, repairY + 1]) for (let x = 2; x < Math.min(4, columns); x++) this.stitches.push([y * (columns + 1) + x, y * (columns + 1) + x + 1]);
    const seamGeometry = new THREE.BufferGeometry();
    seamGeometry.setAttribute('position', new THREE.Float32BufferAttribute(new Float32Array(this.stitches.length * 6), 3));
    this.seams = new THREE.LineSegments(seamGeometry, new THREE.LineBasicMaterial({ color: 0x514937 }));
    this.seams.name = `${this.mesh.name}-stitches`; this.seams.frustumCulled = false;
    this.mesh.add(this.seams);
    this.mesh.material.polygonOffset = true; this.mesh.material.polygonOffsetFactor = 1; this.mesh.material.polygonOffsetUnits = 1;
    this.mesh.castShadow = true; this.mesh.receiveShadow = true; this.mesh.frustumCulled = false; scene.add(this.mesh); this.reset();
  }
  localPoint(x, y = 0) {
    const t = y / this.rows, spread = 1 + this.flare * t;
    if (this.arc) {
      const a = THREE.MathUtils.lerp(this.arc[0], this.arc[1], x / this.columns);
      return new THREE.Vector3(Math.sin(a) * .242 * spread, -t * this.length, Math.cos(a) * .185 * spread).add(this.offset);
    }
    return new THREE.Vector3((x / this.columns - .5) * this.width * spread, -t * this.length, (this.front ? 1 : -1) * t * .065).add(this.offset);
  }
  attach(x, y = 0) {
    const p = this.anchor.localToWorld(this.localPoint(x, y));
    // The sewn edge can ride over a raised arm instead of being pinned inside
    // its capsule. This also keeps the poncho attached over the shoulders.
    if (y === 0) for (let pass = 0; pass < 6; pass++) this.collide(p);
    return p;
  }
  reset() {
    this.accumulator = 0; this.anchor.updateWorldMatrix(true, false);
    this.points.forEach((p, i) => {
      p.copy(this.attach(i % (this.columns + 1), Math.floor(i / (this.columns + 1))));
      if (i > this.columns) this.collide(p);
      this.previous[i].copy(p);
    });
    this.render();
  }
  update(dt, time) {
    this.anchor.updateWorldMatrix(true, false);
    // Teleports reset history instead of injecting unbounded energy.
    if (this.points[0].distanceTo(this.attach(0)) > 1.2) this.reset();
    this.accumulator = Math.min(this.accumulator + dt, .05);
    while (this.accumulator >= 1 / 120) { this.step(1 / 120, time); this.accumulator -= 1 / 120; }
    this.render();
  }
  render() {
    const attr = this.mesh.geometry.attributes.position;
    const cut = new THREE.Vector3();
    this.points.forEach((p, i) => {
      // Ragged cut is render-only; project the cut edge too because a segment
      // between two collision-safe nodes can still cross a bent knee capsule.
      const lastRow = i >= this.rows * (this.columns + 1);
      const fray = lastRow ? [0, .41, .08, .29, .025, .52, .12][i % (this.columns + 1) % 7] : 0;
      const above = lastRow ? this.points[i - this.columns - 1] : p;
      cut.copy(p).lerp(above, fray);
      if (lastRow) for (let pass = 0; pass < 3; pass++) this.collide(cut);
      attr.setXYZ(i, cut.x, cut.y, cut.z);
    }); attr.needsUpdate = true; this.mesh.geometry.computeVertexNormals();
    const seam = this.seams.geometry.attributes.position, a = new THREE.Vector3(), b = new THREE.Vector3();
    this.stitches.forEach(([start, end], i) => {
      a.fromBufferAttribute(attr, start); b.fromBufferAttribute(attr, end);
      const from = a.clone().lerp(b, .22), to = a.clone().lerp(b, .58);
      seam.setXYZ(i * 2, from.x, from.y, from.z); seam.setXYZ(i * 2 + 1, to.x, to.y, to.z);
    }); seam.needsUpdate = true;
  }
  step(dt, time) {
    const count = this.columns + 1;
    // The anchor is fixed within a substep. Cache its pins and inverse matrix
    // so the longer cloth can afford more constraint passes without repeated
    // world-matrix updates for every node.
    const pins = Array.from({ length: count }, (_, x) => this.attach(x));
    const inverse = this.anchor.matrixWorld.clone().invert();
    for (let i = count; i < this.points.length; i++) {
      const p = this.points[i], old = p.clone(), velocity = p.clone().sub(this.previous[i]).multiplyScalar(this.damping);
      if (velocity.length() > .06) velocity.setLength(.06);
      p.add(velocity); p.y -= this.gravity * dt * dt;
      p.x += Math.sin(time * 1.8 + i * .47) * this.wind * dt * dt;
      p.z += (this.front ? .22 : -.35) * dt * dt;
      this.previous[i].copy(old);
    }
    for (let pass = 0; pass < 16; pass++) {
      for (let x = 0; x < count; x++) this.points[x].copy(pins[x]);
      for (const [a, b, rest, stiffness] of this.links) this.constrain(a, b, rest, stiffness);
      for (let i = count; i < this.points.length; i++) {
        const local = this.points[i].clone().applyMatrix4(inverse);
        // Only guard the sewn top against flipping across the torso. Lower
        // panels can wrap around stepping legs instead of forming a rigid tent.
        if (Math.abs(local.x) < .25 && local.y > (this.arc ? -.16 : -.48) && local.y < .13) {
          const clearance = this.arc ? .065 : .18;
          if (this.front) local.z = Math.max(local.z, clearance); else local.z = Math.min(local.z, -clearance);
          this.points[i].copy(local.applyMatrix4(this.anchor.matrixWorld));
        }
        this.collide(this.points[i]);
        this.points[i].y = Math.max(.035, this.points[i].y);
      }
    }
  }
  // Push cloth nodes out of the body capsules (legs, hips, torso, upper arms).
  collide(p) {
    const body = this.body?.(); if (!body) return;
    for (const c of body.caps) {
      if (!CLOTH_BLOCKERS.has(c.name)) continue;
      ab.subVectors(c.b, c.a); const t = THREE.MathUtils.clamp(ap.subVectors(p, c.a).dot(ab) / Math.max(ab.lengthSq(), 1e-9), 0, 1);
      cp.copy(c.a).addScaledVector(ab, t); const d = p.distanceTo(cp), min = c.r + .025;
      if (d < min) { if (d < 1e-6) p.y += min; else p.sub(cp).setLength(min).add(cp); }
    }
  }
  constrain(a, b, rest, stiffness = 1) {
    const delta = this.points[b].clone().sub(this.points[a]), d = delta.length(); if (d < .00001) return;
    delta.multiplyScalar((d - rest) / d * stiffness);
    if (a <= this.columns) this.points[b].sub(delta); else { this.points[a].addScaledVector(delta, .5); this.points[b].addScaledVector(delta, -.5); }
  }
}
export function createSecondary(scene, character) {
  // No back drape: the back is carried by the long left/back panels only.
  // The left side of the robe is whole; the right side is ripped away high up (Dune-like):
  // only a few short shredded strips hang from the front and back, the hip side is bare.
  const skirts = [
    ['front-left-robe-panel', -1.46, -.13, true, .75, 7],
    ['back-left-robe-panel', -Math.PI, -1.68, false, .84, 7],
    ['torn-front-right-strip-a', .13, .45, true, .30, 2],
    ['torn-front-right-strip-b', .58, .82, true, .17, 2],
    ['torn-back-right-strip-a', 2.30, 2.70, false, .22, 2],
    ['torn-back-right-strip-b', 2.80, Math.PI, false, .36, 2],
  ].map(([name, from, to, front, length, columns]) => new Cloth(scene, character.joints.pelvis, {
    name, width: .25, length, rows: Math.max(6, Math.round(length * 17)), columns, offset: [0, .065, 0],
    arc: [from, to], flare: .26, color: front ? 0xc5b694 : 0xae9c80, front,
  }));
  const scarf = new Cloth(scene, character.joints.head, { name: 'long-frayed-face-scarf', width: .105, length: .72, rows: 12, columns: 4, offset: [-.15, -.015, .20], color: 0xb6a68b, front: true, flare: -.18, damping: .969, wind: .28, gravity: 5.5, bend: .22 });
  const cloths = [...skirts, scarf];
  for (const cloth of cloths) { cloth.body = () => character.bodyColliders; cloth.reset(); }
  // The gathered scarf knot has a small spring, independent of the long cloth.
  const hoodTail = character.hoodTail ?? character.plume;
  const previousHead = character.joints.head.getWorldQuaternion(new THREE.Quaternion());
  const head = new THREE.Quaternion(), headDelta = new THREE.Quaternion();
  let spring = 0, velocity = 0;
  return { update(dt, time, speed) {
    for (const cloth of cloths) cloth.update(dt, time);
    character.joints.head.getWorldQuaternion(head);
    headDelta.copy(previousHead).invert().multiply(head);
    // Shortest quaternion arc avoids a kick when the character crosses +/- pi.
    const sign = headDelta.w < 0 ? -1 : 1;
    const impulse = 2 * sign * (headDelta.y + headDelta.x * .6);
    previousHead.copy(head);
    const target = THREE.MathUtils.clamp(-impulse * 2 - speed * .02 + Math.sin(time * 2.2) * .015, -.18, .18);
    let remaining = Math.min(dt, .05);
    while (remaining > 0) { const step = Math.min(1 / 120, remaining); velocity += ((target - spring) * 110 - velocity * 15) * step; spring += velocity * step; remaining -= step; }
    hoodTail.rotation.x = spring; hoodTail.rotation.z = spring * .35;
  }, reset() {
    for (const cloth of cloths) cloth.reset();
    spring = 0; velocity = 0; hoodTail.rotation.set(0, 0, 0);
    character.joints.head.getWorldQuaternion(previousHead);
  } };
}
