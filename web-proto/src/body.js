import * as THREE from 'three';

// Body colliders: capsules (segment + radius) attached to joints, sized to the
// armour silhouette. Used to keep the blade out of the body and to audit limbs.
const CAPSULES = [
  ['pelvis', 'pelvis', [-.10, -.03, 0], [.10, -.03, 0], .13],
  ['belly', 'spine', [-.06, .02, 0], [.06, .02, 0], .135],
  ['chest', 'chest', [-.11, -.01, 0], [.11, -.01, 0], .165],
  ['neck', 'neck', [0, -.02, 0], [0, .09, 0], .09],
  ['head', 'head', [0, .07, .01], [0, .23, -.03], .20],
  ['Lthigh', 'Lthigh', [0, -.03, 0], [0, -.33, 0], .11],
  ['Rthigh', 'Rthigh', [0, -.03, 0], [0, -.33, 0], .11],
  ['Lshin', 'Lshin', [0, -.06, .02], [0, -.36, .03], .10],
  ['Rshin', 'Rshin', [0, -.06, .02], [0, -.36, .03], .10],
  ['LupperArm', 'LupperArm', [0, -.02, 0], [0, -.30, 0], .09],
  ['RupperArm', 'RupperArm', [0, -.02, 0], [0, -.30, 0], .09],
];
export const TORSO = ['pelvis', 'belly', 'chest', 'neck', 'head'];
export const BLADE_BLOCKERS = ['pelvis', 'belly', 'chest', 'neck', 'head', 'Lthigh', 'Rthigh', 'Lshin', 'Rshin'];

export function createBody(joints) {
  const caps = CAPSULES.map(([name, joint, a, b, r]) => ({ name, joint: joints[joint], la: new THREE.Vector3(...a), lb: new THREE.Vector3(...b), r, a: new THREE.Vector3(), b: new THREE.Vector3() }));
  const byName = Object.fromEntries(caps.map(c => [c.name, c]));
  const group = new THREE.Group(); group.visible = false;
  const material = new THREE.MeshBasicMaterial({ color: 0x6cf3ff, wireframe: true, transparent: true, opacity: .35, depthTest: false });
  for (const c of caps) {
    const len = c.la.distanceTo(c.lb);
    const mesh = new THREE.Mesh(new THREE.CapsuleGeometry(c.r, len, 3, 8), material); mesh.renderOrder = 99;
    c.mesh = mesh; group.add(mesh);
  }
  const up = new THREE.Vector3(0, 1, 0);
  return {
    caps, byName, group,
    update() {
      for (const c of caps) { c.a.copy(c.la).applyMatrix4(c.joint.matrixWorld); c.b.copy(c.lb).applyMatrix4(c.joint.matrixWorld); }
      if (group.visible) for (const c of caps) {
        c.mesh.position.copy(c.a).add(c.b).multiplyScalar(.5);
        c.mesh.quaternion.setFromUnitVectors(up, c.b.clone().sub(c.a).normalize());
      }
    },
  };
}

// Closest points between segments p1-q1 and p2-q2 (Ericson, RTCD 5.1.9).
const d1 = new THREE.Vector3(), d2 = new THREE.Vector3(), rr = new THREE.Vector3();
export function closestSegments(p1, q1, p2, q2, c1, c2) {
  d1.subVectors(q1, p1); d2.subVectors(q2, p2); rr.subVectors(p1, p2);
  const a = d1.dot(d1), e = d2.dot(d2), f = d2.dot(rr);
  let s, t;
  if (a <= 1e-9 && e <= 1e-9) { s = t = 0; }
  else if (a <= 1e-9) { s = 0; t = THREE.MathUtils.clamp(f / e, 0, 1); }
  else {
    const c = d1.dot(rr);
    if (e <= 1e-9) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); }
    else {
      const b = d1.dot(d2), denom = a * e - b * b;
      s = denom > 1e-9 ? THREE.MathUtils.clamp((b * f - c * e) / denom, 0, 1) : 0;
      t = (b * s + f) / e;
      if (t < 0) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); }
      else if (t > 1) { t = 1; s = THREE.MathUtils.clamp((b - c) / a, 0, 1); }
    }
  }
  c1.copy(p1).addScaledVector(d1, s); c2.copy(p2).addScaledVector(d2, t);
  return c1.distanceTo(c2);
}

// Deepest penetration of segment p-q (radius r) into the listed capsules.
const cA = new THREE.Vector3(), cB = new THREE.Vector3();
export function deepest(body, names, p, q, r) {
  let best = null;
  for (const name of names) {
    const c = body.byName[name], dist = closestSegments(p, q, c.a, c.b, cA, cB), depth = c.r + r - dist;
    if (depth > 0 && (!best || depth > best.depth)) {
      const normal = cA.clone().sub(cB);
      if (normal.lengthSq() < 1e-10) normal.copy(q).sub(p).cross(c.b.clone().sub(c.a)); // fully crossed: push sideways
      if (normal.lengthSq() < 1e-10) normal.set(0, 1, 0);
      best = { name, depth, point: cA.clone(), normal: normal.normalize() };
    }
  }
  return best;
}
